using System;
using PEPlugin;
using PXCPlugin;

namespace PmxEditorMcp
{
    /// <summary>
    /// 接続初期化。起動時に受け取った接続の根を常駐期間中保持し、Cプラグイン連携のコネクタを
    /// 求められたときに得て、終了時に手放す。保持しているコネクタが失効したら、次に求められた
    /// ところで取り直す——常駐が長くなるほど、得たときのままで在り続ける保証は薄くなる。
    /// </summary>
    public sealed class ResidentConnection : IDisposable
    {
        private readonly HostLog _log;

        private readonly object _gate = new object();

        private IPXCPluginConnector _cPluginConnector;

        private bool _released;

        private ResidentConnection(IPERunArgs runArgs, HostLog log)
        {
            RunArgs = runArgs;
            _log = log;
        }

        /// <summary>常駐保持する接続の根。各コネクタ・ビルダはここから辿って得る。</summary>
        public IPERunArgs RunArgs { get; }

        /// <summary>コネクタを保持しているかどうか。</summary>
        public bool IsHolding
        {
            get
            {
                lock (_gate)
                {
                    return _cPluginConnector != null;
                }
            }
        }

        /// <summary>
        /// 接続の根を保持する。Cプラグイン連携のコネクタはここでは得ない——得られなくても根は
        /// 保ち、そこから辿るほかの機能を動かし続けるためである。
        /// </summary>
        public static ResidentConnection Hold(IPERunArgs runArgs, HostLog log)
        {
            if (runArgs == null)
            {
                throw new ArgumentNullException(nameof(runArgs));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            return new ResidentConnection(runArgs, log);
        }

        /// <summary>
        /// Cプラグイン連携のコネクタを渡す。保持していなければ接続の根から辿って得る。求めるときに
        /// 渡すホストプラグイン自身の位置は、接続の根が持つものを使う。得られなければ
        /// <see cref="InvalidOperationException"/> で、記録は残さない——取得の記録は、得たものを
        /// 手放すまでの対で読むためである。
        /// </summary>
        public IPXCPluginConnector Use()
        {
            lock (_gate)
            {
                if (_released)
                {
                    throw new InvalidOperationException("手放したあとにコネクタを求めた。");
                }

                if (_cPluginConnector != null)
                {
                    return _cPluginConnector;
                }

                return Take();
            }
        }

        private IPXCPluginConnector Take()
        {
            string modulePath = RunArgs.ModulePath;
            if (string.IsNullOrEmpty(modulePath) || modulePath.Trim().Length == 0)
            {
                throw new InvalidOperationException("接続の根がモジュールパスを持たない。");
            }

            IPXCPluginRunArgs runArgs = RunArgs.Host.Connector.System.GetCPluginRunArgsClone(modulePath);
            if (runArgs == null)
            {
                throw new InvalidOperationException("Cプラグインの実行引数を得られなかった。");
            }

            IPXCPluginConnector connector = runArgs.Connector;
            if (connector == null)
            {
                throw new InvalidOperationException("Cプラグインコネクタを得られなかった。");
            }

            _cPluginConnector = connector;
            _log.Write("Cプラグインコネクタの取得: modulePath=" + modulePath);

            return connector;
        }

        /// <summary>
        /// 保持しているコネクタを失効として手放す。次に求められたところで取り直す。保持して
        /// いなければ何もせず、記録も残さない——失効の記録は、取り直しと対で読むためである。
        /// </summary>
        public void Expire()
        {
            lock (_gate)
            {
                if (_cPluginConnector == null)
                {
                    return;
                }

                _cPluginConnector = null;
                _log.Write("Cプラグインコネクタの失効");
            }
        }

        /// <summary>
        /// 保持しているコネクタを手放し、以後は求められても渡さない。得ていないときは記録を残さず、
        /// 二度呼んでも記録は一度だけ書く。
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                _released = true;
                if (_cPluginConnector == null)
                {
                    return;
                }

                _cPluginConnector = null;
                _log.Write("Cプラグインコネクタの破棄");
            }
        }
    }
}
