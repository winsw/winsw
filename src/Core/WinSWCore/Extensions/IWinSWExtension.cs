﻿using System;
using System.Xml;
using Mono.Addins;
using winsw.Util;

namespace winsw.Extensions
{
    /// <summary>
    /// Interface for Win Service Wrapper Extension
    /// </summary>
    /// <remarks>
    /// All implementations should provide the default empty constructor. 
    /// The initialization will be performed by Init methods
    /// </remarks>
    [TypeExtensionPoint]
    public interface IWinSWExtension
    {
        /// <summary>
        /// Extension name to be displayed in logs
        /// </summary>
        String DisplayName { get; }

        /// <summary>
        /// Extension descriptor
        /// </summary>
        WinSWExtensionDescriptor Descriptor { get; set; }

        /// <summary>
        /// Init handler. Extension should load it's config during that step
        /// </summary>
        /// <param name="descriptor">Service descriptor</param>
        /// <param name="node">Configuration node</param>
        void Configure(ServiceDescriptor descriptor, XmlNode node, IEventWriter logger);

        /// <summary>
        /// Start handler. Called during startup of the service before the child process.
        /// </summary>
        /// <param name="logger">Logger</param>
        /// <exception cref="ExtensionException">Any error during execution</exception>
        void OnStart(IEventWriter logger);

        /// <summary>
        /// Stop handler. Called during stop of the service
        /// </summary>
        /// <param name="logger">Logger</param>
        /// <exception cref="ExtensionException">Any error during execution</exception>
        void OnStop(IEventWriter logger);
    }
}
