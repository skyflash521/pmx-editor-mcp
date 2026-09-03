using System;
using PEPlugin;
using PEPlugin.Form;
using PEPlugin.Pmd;
using PEPlugin.Pmx;
using PEPlugin.View;
using PXCPlugin;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 接続初期化が辿る経路だけを持つ題材。辿らないメンバーは呼ばれたら止まる——通ってはいけない
    /// 経路を通ったことを、値を返して隠さないためである。
    /// </summary>
    public sealed class StubRunArgs : IPERunArgs
    {
        public StubRunArgs(IPEPluginHost host, string modulePath)
        {
            Host = host;
            ModulePath = modulePath;
        }

        public IPEPluginHost Host { get; }

        public string ModulePath { get; }

        public bool IsBootup
        {
            get { throw new NotSupportedException(); }
        }
    }

    public sealed class StubPluginHost : IPEPluginHost
    {
        public StubPluginHost(IPEConnector connector)
        {
            Connector = connector;
        }

        public IPEConnector Connector { get; }

        public IPEBuilder Builder
        {
            get { throw new NotSupportedException(); }
        }

        public string Name
        {
            get { throw new NotSupportedException(); }
        }

        public string Version
        {
            get { throw new NotSupportedException(); }
        }
    }

    public sealed class StubConnector : IPEConnector
    {
        public StubConnector(IPESystemConnector system)
        {
            System = system;
        }

        public IPESystemConnector System { get; }

        public IPEFormConnector Form
        {
            get { throw new NotSupportedException(); }
        }

        public IPEPmdConnector Pmd
        {
            get { throw new NotSupportedException(); }
        }

        public IPXPmxConnector Pmx
        {
            get { throw new NotSupportedException(); }
        }

        public IPEViewConnector View
        {
            get { throw new NotSupportedException(); }
        }
    }

    public sealed class StubSystemConnector : IPESystemConnector
    {
        private readonly IPXCPluginRunArgs _cPluginRunArgs;

        public StubSystemConnector(IPXCPluginRunArgs cPluginRunArgs)
        {
            _cPluginRunArgs = cPluginRunArgs;
        }

        /// <summary>Cプラグインの実行引数を求めた回数。取得が一度だけであることを見るために数える。</summary>
        public int CloneCount { get; private set; }

        /// <summary>最後に渡されたモジュールパス。</summary>
        public string LastModulePath { get; private set; }

        public IPXCPluginRunArgs GetCPluginRunArgsClone(string modulePath)
        {
            CloneCount++;
            LastModulePath = modulePath;

            return _cPluginRunArgs;
        }

        public string DefaultPluginFolderPath
        {
            get { throw new NotSupportedException(); }
        }

        public string HostApplicationPath
        {
            get { throw new NotSupportedException(); }
        }

        public string PEPluginAssemblyPath
        {
            get { throw new NotSupportedException(); }
        }

        public Version PEPluginAssemblyVersion
        {
            get { throw new NotSupportedException(); }
        }

        public int RegisteredCPluginCount
        {
            get { throw new NotSupportedException(); }
        }

        public int RegisteredPluginCount
        {
            get { throw new NotSupportedException(); }
        }

        public string SlimDXAssemblyPath
        {
            get { throw new NotSupportedException(); }
        }

        public int[] FindRegisteredPluginsFromMenuText(string menuText, bool contains)
        {
            throw new NotSupportedException();
        }

        public IPERegisteredPluginInfo GetCPluginInfo(int n)
        {
            throw new NotSupportedException();
        }

        public IPERegisteredPluginInfo GetPluginInfo(int n)
        {
            throw new NotSupportedException();
        }

        public object GetShareObject(string key, bool clear)
        {
            throw new NotSupportedException();
        }

        public bool RemoveShareObject(string key)
        {
            throw new NotSupportedException();
        }

        public void RunCPlugin(int n)
        {
            throw new NotSupportedException();
        }

        public void RunPlugin(int n)
        {
            throw new NotSupportedException();
        }

        public bool SetShareObject(string key, object obj)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class StubCPluginConnector : IPXCPluginConnector
    {
        public object Connect(int n, object obj)
        {
            throw new NotSupportedException();
        }

        public int[] GetSelectedBodyIndices()
        {
            throw new NotSupportedException();
        }

        public int[] GetSelectedBoneIndices()
        {
            throw new NotSupportedException();
        }

        public int[] GetSelectedFaceIndices()
        {
            throw new NotSupportedException();
        }

        public int[] GetSelectedJointIndices()
        {
            throw new NotSupportedException();
        }

        public int[] GetSelectedVertexIndices()
        {
            throw new NotSupportedException();
        }

        public int[] GetVisibleMaterialIndices()
        {
            throw new NotSupportedException();
        }

        public void SetSelectedBodyIndices(int[] indices)
        {
            throw new NotSupportedException();
        }

        public void SetSelectedBoneIndices(int[] indices)
        {
            throw new NotSupportedException();
        }

        public void SetSelectedFaceIndices(int[] indices)
        {
            throw new NotSupportedException();
        }

        public void SetSelectedJointIndices(int[] indices)
        {
            throw new NotSupportedException();
        }

        public void SetSelectedVertexIndices(int[] indices)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class StubCPluginRunArgs : IPXCPluginRunArgs
    {
        public StubCPluginRunArgs(IPXCPluginConnector connector)
        {
            Connector = connector;
        }

        public IPXCPluginConnector Connector { get; }

        public string ModulePath
        {
            get { throw new NotSupportedException(); }
        }
    }
}
