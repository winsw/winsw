using System;
using System.Collections.Generic;
using System.Xml;
using log4net;
using WinSW.Logging;

namespace WinSW.Extensions
{
    public class WinSWExtensionManager
    {
        public Dictionary<string, IWinSWExtension> Extensions { get; }

        public XmlServiceConfig ServiceConfig { get; }

        private static readonly ILog Log = LogManager.GetLogger(LoggerNames.Service);

        public WinSWExtensionManager(XmlServiceConfig serviceConfig)
        {
            this.ServiceConfig = serviceConfig;
            this.Extensions = new Dictionary<string, IWinSWExtension>();
        }

        private static IWinSWExtension CreateExtensionInstance(string id, string className)
        {
            object created;

            try
            {
                var t = Type.GetType(className);
                if (t is null)
                {
                    throw new ExtensionException(id, "Class " + className + " does not exist");
                }

                created = Activator.CreateInstance(t)!;
            }
            catch (Exception ex)
            {
                throw new ExtensionException(id, "Cannot load the class by name: " + className, ex);
            }

            if (!(created is IWinSWExtension extension))
            {
                throw new ExtensionException(id, "The loaded class is not a WinSW extension: " + className + ". Type is " + created.GetType());
            }

            return extension;
        }

        /// <summary>
        /// Notifies all extensions that the wrapper is being started.
        /// They are supposed to run the initialization logic.
        /// If any extensions fails, WinSW startup should be interrupted.
        /// </summary>
        /// <exception cref="Exception">Start failure</exception>
        public void FireOnWrapperStarted()
        {
            foreach (var ext in this.Extensions)
            {
                try
                {
                    ext.Value.OnWrapperStarted();
                }
                catch (ExtensionException ex)
                {
                    Log.Fatal("onWrapperStarted() handler failed for " + ext.Value.DisplayName, ex);
                    throw; // Propagate error to stop the startup
                }
            }
        }

        /// <summary>
        /// Notifies all extensions that the wrapper is being stopped.
        /// If an error happens, further extensions will be tried
        /// </summary>
        public void FireBeforeWrapperStopped()
        {
            foreach (var ext in this.Extensions)
            {
                try
                {
                    ext.Value.BeforeWrapperStopped();
                }
                catch (ExtensionException ex)
                {
                    Log.Error("beforeWrapperStopped() handler failed for " + ext.Value.DisplayName, ex);
                }
            }
        }

        /// <summary>
        /// Handler, which is being invoked once the child process is started.
        /// </summary>
        /// <param name="process">Process</param>
        public void FireOnProcessStarted(System.Diagnostics.Process process)
        {
            foreach (var ext in this.Extensions)
            {
                try
                {
                    ext.Value.OnProcessStarted(process);
                }
                catch (ExtensionException ex)
                {
                    Log.Error("onProcessStarted() handler failed for " + ext.Value.DisplayName, ex);
                }
            }
        }

        /// <summary>
        /// Handler, which is being invoked once the child process is terminated.
        /// </summary>
        /// <param name="process">Process</param>
        public void FireOnProcessTerminated(System.Diagnostics.Process process)
        {
            foreach (var ext in this.Extensions)
            {
                try
                {
                    ext.Value.OnProcessTerminated(process);
                }
                catch (ExtensionException ex)
                {
                    Log.Error("onProcessTerminated() handler failed for " + ext.Value.DisplayName, ex);
                }
            }
        }

        // TODO: Implement loading of external extensions. Current version supports internal hack
        #region Extension load management

        /// Loads extensions according to the configuration file.
        /// </summary>
        /// <param name="logger">Logger</param>
        /// <exception cref="Exception">Loading failure</exception>
        public void LoadExtensions()
        {
            var extensionIds = this.ServiceConfig.ExtensionIds;
            foreach (string extensionId in extensionIds)
            {
                this.LoadExtension(extensionId);
            }
        }

        /// <summary>
        /// Loads extensions from the configuration file
        /// </summary>
        /// <param name="id">Extension ID</param>
        /// <param name="logger">Logger</param>
        /// <exception cref="Exception">Loading failure</exception>
        private void LoadExtension(string id)
        {
            if (this.Extensions.ContainsKey(id))
            {
                throw new ExtensionException(id, "Extension has been already loaded");
            }

            var extensionsConfig = this.ServiceConfig.ExtensionsConfiguration;
            var configNode = extensionsConfig is null ? null : extensionsConfig.SelectSingleNode("extension[@id='" + id + "'][1]") as XmlElement;
            if (configNode is null)
            {
                throw new ExtensionException(id, "Cannot get the configuration entry");
            }

            var descriptor = WinSWExtensionDescriptor.FromXml(configNode);
            if (descriptor.Enabled)
            {
                var extension = CreateExtensionInstance(descriptor.Id, descriptor.ClassName);
                extension.Descriptor = descriptor;
                try
                {
                    extension.Configure(this.ServiceConfig, configNode);
                }
                catch (Exception ex)
                { // Consider any unexpected exception as fatal
                    Log.Fatal("Failed to configure the extension " + id, ex);
                    throw;
                }

                this.Extensions.Add(id, extension);
                Log.Info("Extension loaded: " + id);
            }
            else
            {
                Log.Warn("Extension is disabled: " + id);
            }
        }

        #endregion
    }
}
