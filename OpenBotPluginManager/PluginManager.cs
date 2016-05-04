using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using OpenBot.Plugins.Interfaces;
using OpenBot.Plugins.Proxies;
using OpenBot.Plugins.Delegates;

namespace OpenBot.Plugins
{
    public class PluginManager : MarshalByRefObject, IDependencyResolver
    {
        private List<PluginAssemblyProxy> _assemblyProxies;
        private List<AssemblyName> _baseAssemblies;

        public AssemblyName[] BaseAssemblies
        {
            get{ return _baseAssemblies.ToArray(); }
        }

        public PluginAssemblyProxy[] PluginAssemblies { get { return _assemblyProxies.ToArray(); } }
        public string PluginDirectory { get; set; }
        public PluginManager()
        {
            _assemblyProxies = new List<PluginAssemblyProxy>();
            _baseAssemblies = new List<AssemblyName>();
            _baseAssemblies.Add(typeof(PluginAssemblyProxy).Assembly.GetName());

            PluginDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Plugins");
        }

        public void LoadAllPluginAssemblies(IBotAdapter adapter)
        {
            if (!Directory.Exists(PluginDirectory))
            {
                Directory.CreateDirectory(PluginDirectory);
                return;
            }

            foreach(string i in Directory.GetFiles(PluginDirectory).Where((a) => a.EndsWith(".dll")))
            {
                AssemblyName pluginAssemblyName = AssemblyName.GetAssemblyName(i);

                LoadPluginAssembly(adapter, pluginAssemblyName);
            }
        }

        public PluginAssemblyProxy LoadPluginAssembly(IBotAdapter adapter, AssemblyName pluginAssemblyName)
        {
            AppDomainSetup domainSetup = new AppDomainSetup();
            domainSetup.ApplicationName = pluginAssemblyName.Name;
            domainSetup.ApplicationBase = PluginDirectory;

            AppDomain pluginDomain = AppDomain.CreateDomain(domainSetup.ApplicationName, AppDomain.CurrentDomain.Evidence, domainSetup);
            PluginAssemblyProxy assemblyProxy = (PluginAssemblyProxy)pluginDomain.CreateInstanceFromAndUnwrap(typeof(PluginAssemblyProxy).Assembly.CodeBase, typeof(PluginAssemblyProxy).FullName);

            assemblyProxy.LoadAssembly(pluginAssemblyName, _baseAssemblies.ToArray());

            assemblyProxy.SetAdapter(adapter);
            assemblyProxy.SetDomainSafeDependencyResolver(this);

            _assemblyProxies.Add(assemblyProxy);

            return assemblyProxy;
        }

        public void AddBaseAssembly(AssemblyName assembly)
        {
            if (!_baseAssemblies.Contains(assembly))
                _baseAssemblies.Add(assembly);
        }

        public void RemoveBaseAssembly(AssemblyName assembly)
        {
            if (_baseAssemblies.Contains(assembly))
                if (assembly != _baseAssemblies[0])
                    _baseAssemblies.Remove(assembly);
        }

        public void InitializeAllPlugins()
        {
            foreach (PluginAssemblyProxy i in _assemblyProxies)
                i.LoadPlugins();
        }

        public void InitializeAllServices()
        {
            foreach (PluginAssemblyProxy i in _assemblyProxies)
                i.InitializeServices();
        }

        public void UnloadPluginAssembly(PluginAssemblyProxy assemblyProxy)
        {
            if (!_assemblyProxies.Contains(assemblyProxy))
                return;

            _assemblyProxies.Remove(assemblyProxy);
            assemblyProxy.Unload();
            AppDomain.Unload(assemblyProxy.OperatingDomain);
        }

        public IService ResolveService(string typeName)
        {
            IService returnValue = null;
            
            foreach(PluginAssemblyProxy i in _assemblyProxies)
            {
                if (i.DefinesType(typeName))
                    if (returnValue == null)
                        returnValue = i.GetServiceIfExists(typeName);
            }

            return returnValue;
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
