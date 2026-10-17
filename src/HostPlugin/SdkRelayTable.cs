using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp
{
    /// <param name="target">呼ぶ相手。静的なメンバーでは null。</param>
    /// <param name="arguments">引数。並びは行キーの引数の列と同じ。</param>
    public delegate object SdkCall(object target, object[] arguments);

    /// <param name="connection">接続の根を保つ常駐。道の起点と、自動注入のコネクタをここから採る。</param>
    public delegate object SdkReceiver(ResidentConnection connection);

    public enum SdkRelayRefusal
    {
        None,

        /// <summary>能力対応表に無い行キー。</summary>
        Unknown,

        /// <summary>生成の時点でSDKのメンバーを解決できなかった。</summary>
        Unresolved,

        /// <summary>読み込まれたSDKにそのメンバーが無く、呼び出しが解決の失敗で落ちた。</summary>
        Disabled,
    }

    /// <summary>
    /// 行キーから中継を引く表。生成に使ったSDKのバージョンを持ち、解決できない行はその行だけを断る。
    /// 複数のスレッドから同時に呼んでよい。
    /// </summary>
    public sealed class SdkRelayTable
    {
        private readonly Dictionary<string, SdkCall> _calls;

        private readonly HashSet<string> _unresolved;

        private readonly HashSet<string> _disabled = new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> _refusedTools = new HashSet<string>(StringComparer.Ordinal);

        private readonly object _gate = new object();

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

        /// <summary>名前の序数昇順。</summary>
        public IList<string> RefusedTools
        {
            get
            {
                lock (_gate)
                {
                    return Sorted(_refusedTools);
                }
            }
        }

        public void RefuseTools(IEnumerable<string> tools)
        {
            if (tools == null)
            {
                throw new ArgumentNullException(nameof(tools));
            }

            lock (_gate)
            {
                _refusedTools.UnionWith(tools);
            }
        }

        public int Count
        {
            get { return _calls.Count; }
        }

        /// <summary>
        /// 行キーの指すメンバーを呼ぶ。断ったときは偽で、<paramref name="refusal"/> がその理由。
        /// 呼び出しが型かメンバーの解決の失敗で落ちた行は、以後無効にする。
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
        /// 読み込まれたSDKの型かメンバーへ届かないことを表す失敗かどうか。処理そのものの失敗は含めない。
        /// </summary>
        internal static bool IsResolutionFailure(Exception exception)
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
