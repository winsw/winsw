using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Reflection;

namespace winsw.extensions
{
    internal class WinSWExtensionManager
    {
        private Dictionary<string, IWinSWExtension> extensions = new Dictionary<string, IWinSWExtension>();

        internal ServiceDescriptor ServiceDescriptor { private set; get; }

        internal WinSWExtensionManager(ServiceDescriptor serviceDescriptor)
        {
            ServiceDescriptor = serviceDescriptor;
        }

        /// <summary>
        /// Starts all extensions
        /// </summary>
        /// <exception cref="ExtensionException">Start failure</exception>
        internal void OnStart(WrapperService service) 
        {
            foreach (var ext in extensions)
            {
                ext.Value.OnStart(service);     
            }
        }

        /// <summary>
        /// Stops all extensions
        /// </summary>
        /// <exception cref="ExtensionException">Stop failure</exception>
        internal void OnStop(WrapperService service)
        {
            foreach (var ext in extensions)
            {
                ext.Value.OnStop(service);
            }
        }

        #region Extension load management
        //TODO: Implement loading of external extensions. Current version supports internal hack

        internal void LoadExtensions()
        {
            var extensionIds = ServiceDescriptor.ExtensionIds;
            foreach (String extensionId in extensionIds) 
            {
                LoadExtension(extensionId);
            }
        }

        /// <summary>
        /// Loads extensions from the configuration file
        /// </summary>
        /// <param name="id">Extension ID</param>
        /// <exception cref="ExtensionException">Loading failure</exception>
        private void LoadExtension(string id)
        {
            if (extensions.ContainsKey(id))
            {
                throw new ExtensionException(id, "Extension has been already loaded");
            }

            var extensionsConfig = ServiceDescriptor.ExtensionsConfiguration;
            XmlElement configNode =(extensionsConfig != null) ? extensionsConfig.SelectSingleNode("extension[@id='"+id+"'][1]") as XmlElement : null;
            if (configNode == null)
            {
                throw new ExtensionException(id, "Cannot get the configuration entry");
            }

            var descriptor = WinSWExtensionDescriptor.FromXml(configNode);
            if (descriptor.Enabled) 
            {
                IWinSWExtension extension = CreateExtensionInstance(descriptor.Id, descriptor.ClassName);
                extension.Descriptor = descriptor;
                extension.Configure(ServiceDescriptor, configNode);
                extensions.Add(id, extension);
            }
            
        }

        private IWinSWExtension CreateExtensionInstance(string id, string className)
        {
            ActivationContext ac = AppDomain.CurrentDomain.ActivationContext;
            Assembly assembly = Assembly.GetCallingAssembly();
            Object created;

            try 
            {
                created = Activator.CreateInstance(Type.GetType(className));
            } 
            catch (Exception ex)
            {
                throw new ExtensionException(id, "Cannot load the class by name: "+className, ex);
            }
            
            var extension = created as IWinSWExtension;
            if (extension == null)
            {
                throw new ExtensionException(id, "The loaded class is not a WinSW extension: " + className + ". Type is " + created.GetType());
            }
            return extension;
        }

        #endregion   
    }
}
