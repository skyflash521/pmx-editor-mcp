using System;
using PEPlugin;
using PXCPlugin;

namespace PmxEditorMcp
{
    /// <summary>
    /// 接続初期化。起動時に受け取った接続の根を常駐期間中保持し、Cプラグイン連携のコネクタを
    /// 一度だけ得て、終了時に手放す。取得経路は一次資料が利用非推奨としているので、再取得はしない。
    /// </summary>
    public sealed class ResidentConnection : IDisposable
    {
        private readonly HostLog _log;

        private IPXCPluginConnector _cPluginConnector;

        private ResidentConnection(IPERunArgs runArgs, HostLog log)
        {
            RunArgs = runArgs;
            _log = log;
        }

        /// <summary>常駐保持する接続の根。各コネクタ・ビルダはここから辿って得る。</summary>
        public IPERunArgs RunArgs { get; }

        /// <summary>
        /// 常駐保持するCプラグイン連携のコネクタ。まだ得ていないときと、手放した後は null。
        /// </summary>
        public IPXCPluginConnector CPluginConnector
        {
            get { return _cPluginConnector; }
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
        /// 接続の根から辿ってCプラグイン連携のコネクタを得る。求めるときに渡すホストプラグイン
        /// 自身の位置は、接続の根が持つものを使う。得られなければ
        /// <see cref="InvalidOperationException"/> で、記録は残さない——取得の記録は、得たものを
        /// 手放すまでの対で読むためである。二度目の呼び出しも同じ例外で止める。
        /// </summary>
        public void TakeCPluginConnector()
        {
            if (_cPluginConnector != null)
            {
                throw new InvalidOperationException("Cプラグインコネクタを二度得ようとした。");
            }

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
        }

        /// <summary>
        /// 保持しているコネクタを手放す。得ていないときは何もせず、二度呼んでも記録は一度だけ書く。
        /// </summary>
        public void Dispose()
        {
            if (_cPluginConnector == null)
            {
                return;
            }

            _cPluginConnector = null;
            _log.Write("Cプラグインコネクタの破棄");
        }
    }
}
