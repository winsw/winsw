using System;
using System.Collections.Generic;
using System.Xml;
using System.Reflection;
using System.Diagnostics;
using winsw.Util;

namespace winsw.Extensions
{
    public class WinSWExtensionManager
    {
        public Dictionary<string, IWinSWExtension> Extensions { private set; get; }
        public ServiceDescriptor ServiceDescriptor { private set; get; }

        public WinSWExtensionManager(ServiceDescriptor serviceDescriptor)
        {
            ServiceDescriptor = serviceDescriptor;
            Extensions = new Dictionary<string, IWinSWExtension>();
        }

        /// <summary>
        /// Starts all extensions
        /// </summary>
        /// <exception cref="ExtensionException">Start failure</exception>
        public void OnStart(IEventWriter logger) 
        {
            foreach (var ext in Extensions)
            {
                ext.Value.OnStart(logger);     
            }
        }

        /// <summary>
        /// Stops all extensions
        /// </summary>
        /// <exception cref="ExtensionException">Stop failure</exception>
        public void OnStop(IEventWriter logger)
        {
            foreach (var ext in Extensions)
            {
                ext.Value.OnStop(logger);
            }
        }

        //TODO: Implement loading of external extensions. Current version supports internal hack
        #region Extension load management

        public void LoadExtensions(IEventWriter logger)
        {
            var extensionIds = ServiceDescriptor.ExtensionIds;
            foreach (String extensionId in extensionIds) 
            {
                LoadExtension(extensionId, logger);
            }
        }

        /// <summary>
        /// Loads extensions from the configuration file
        /// </summary>
        /// <param name="id">Extension ID</param>
        /// <param name="logger">Logger</param>
        /// <exception cref="ExtensionException">Loading failure</exception>
        private void LoadExtension(string id, IEventWriter logger)
        {
            if (Extensions.ContainsKey(id))
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
                extension.Configure(ServiceDescriptor, configNode, logger);
                Extensions.Add(id, extension);
                logger.LogEvent("Extension loaded: "+id, EventLogEntryType.Information);
            }
            else
            {
                logger.LogEvent("Extension is disabled: " + id, EventLogEntryType.Warning);
            }
            
        }

        private IWinSWExtension CreateExtensionInstance(string id, string className)
        {
            ActivationContext ac = AppDomain.CurrentDomain.ActivationContext;
            Assembly assembly = Assembly.GetCallingAssembly();
            Object created;
            
            try
            {
                Type t = Type.GetType(className);
                if (t == null)
                {
                    throw new ExtensionException(id, "Class "+className+" does not exist");
                }
                created = Activator.CreateInstance(t);
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
