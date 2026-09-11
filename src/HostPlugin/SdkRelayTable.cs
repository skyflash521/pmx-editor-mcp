using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// 能力対応表の1行が指すSDKのメンバーを直接呼ぶ中継。ビルド時に生成したものだけを持つので、
    /// 呼び出し先を名前で引く経路は無い。
    /// </summary>
    /// <param name="target">呼ぶ相手。静的なメンバーでは null。</param>
    /// <param name="arguments">引数。並びは行キーの引数の列と同じ。</param>
    public delegate object SdkCall(object target, object[] arguments);

    /// <summary>
    /// 宣言型の受け手を、接続の根から辿って得る。辿る道はビルド時に決めて生成するので、配布物が
    /// 名前で辿る経路は無い。
    /// </summary>
    /// <param name="connection">接続の根を保つ常駐。道の起点と、自動注入のコネクタをここから採る。</param>
    public delegate object SdkReceiver(ResidentConnection connection);

    /// <summary>中継を断った理由。</summary>
    public enum SdkRelayRefusal
    {
        /// <summary>断っていない。</summary>
        None,

        /// <summary>能力対応表に無い行キー。</summary>
        Unknown,

        /// <summary>生成の時点でSDKのメンバーを解決できなかった。</summary>
        Unresolved,

        /// <summary>読み込まれたSDKにそのメンバーが無く、呼び出しが解決の失敗で落ちた。</summary>
        Disabled,
    }

    /// <summary>
    /// 行キーから中継を引く表。生成に使ったSDKの版を持ち、解決できない行はその行だけを断る
    /// ——版が違うだけで待受ごと止めると、エンドユーザーには何も渡らない。
    /// 複数のスレッドから同時に呼んでよい。
    /// </summary>
    public sealed class SdkRelayTable
    {
        private readonly Dictionary<string, SdkCall> _calls;

        private readonly HashSet<string> _unresolved;

        private readonly HashSet<string> _disabled = new HashSet<string>(StringComparer.Ordinal);

        private readonly object _gate = new object();

        /// <summary>
        /// 生成に使ったSDKの版・中継を作った能力対応表の指紋・解決できた行の中継・生成の時点で
        /// 解決できなかった行を与えて生成する。
        /// </summary>
        public SdkRelayTable(
            string generatedSdkVersion,
            string toolMapDigest,
            IDictionary<string, SdkCall> calls,
            IEnumerable<string> unresolved)
        {
            if (generatedSdkVersion == null)
            {
                throw new ArgumentNullException(nameof(generatedSdkVersion));
            }

            if (toolMapDigest == null)
            {
                throw new ArgumentNullException(nameof(toolMapDigest));
            }

            if (calls == null)
            {
                throw new ArgumentNullException(nameof(calls));
            }

            if (unresolved == null)
            {
                throw new ArgumentNullException(nameof(unresolved));
            }

            GeneratedSdkVersion = generatedSdkVersion;
            ToolMapDigest = toolMapDigest;
            _calls = new Dictionary<string, SdkCall>(calls, StringComparer.Ordinal);
            _unresolved = new HashSet<string>(unresolved, StringComparer.Ordinal);
        }

        /// <summary>生成に使ったSDKの版。</summary>
        public string GeneratedSdkVersion { get; }

        /// <summary>中継を作った能力対応表の指紋。ブリッジの定義と同じ表から作られたことを示す。</summary>
        public string ToolMapDigest { get; }

        /// <summary>生成の時点で解決できなかった行。識別子の序数昇順。</summary>
        public IList<string> Unresolved
        {
            get { return Sorted(_unresolved); }
        }

        /// <summary>呼び出しが解決の失敗で落ちたため、以後断る行。識別子の序数昇順。</summary>
        public IList<string> Disabled
        {
            get
            {
                lock (_gate)
                {
                    return Sorted(_disabled);
                }
            }
        }

        /// <summary>中継できる行の数。</summary>
        public int Count
        {
            get { return _calls.Count; }
        }

        /// <summary>
        /// 行キーの指すメンバーを呼ぶ。断ったときは偽で、<paramref name="refusal"/> がその理由。
        /// 読み込まれたSDKが生成時と違う版で、生成時に在ったメンバーが失われている場合は、その行の
        /// 呼び出しが型かメンバーの解決の失敗として落ちるので、それを捕らえてその行だけを断り、
        /// 以後その行を無効にする。
        /// </summary>
        public bool TryInvoke(
            string rowKey,
            object target,
            object[] arguments,
            out object result,
            out SdkRelayRefusal refusal)
        {
            if (rowKey == null)
            {
                throw new ArgumentNullException(nameof(rowKey));
            }

            result = null;

            if (_unresolved.Contains(rowKey))
            {
                refusal = SdkRelayRefusal.Unresolved;
                return false;
            }

            lock (_gate)
            {
                if (_disabled.Contains(rowKey))
                {
                    refusal = SdkRelayRefusal.Disabled;
                    return false;
                }
            }

            SdkCall call;
            if (!_calls.TryGetValue(rowKey, out call))
            {
                refusal = SdkRelayRefusal.Unknown;
                return false;
            }

            try
            {
                result = call(target, arguments ?? new object[0]);
            }
            catch (Exception exception) when (IsResolutionFailure(exception))
            {
                lock (_gate)
                {
                    _disabled.Add(rowKey);
                }

                refusal = SdkRelayRefusal.Disabled;
                return false;
            }

            refusal = SdkRelayRefusal.None;

            return true;
        }

        /// <summary>
        /// 読み込まれたSDKの型かメンバーへ届かないことを表す失敗かどうか。無くなった場合だけでなく
        /// 公開をやめた場合も届かないので、どちらも同じ扱いにする。処理そのものの失敗は含めない
        /// ——含めると、SDKが返した誤りを版の違いとして無効化してしまう。
        /// </summary>
        private static bool IsResolutionFailure(Exception exception)
        {
            return exception is MemberAccessException
                || exception is TypeLoadException
                || exception is EntryPointNotFoundException;
        }

        private static IList<string> Sorted(IEnumerable<string> keys)
        {
            return new ReadOnlyCollection<string>(
                keys.OrderBy(k => k, StringComparer.Ordinal).ToList());
        }
    }
}
