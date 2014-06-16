using System;
using System.Collections.Generic;
using System.Text;
using winsw.Utils;

namespace winsw.Extensions
{
    /// <summary>
    /// Interface for Win Service Wrapper Extension
    /// </summary>
    public interface IWinSWExtension
    {
        /// <summary>
        /// Extension name
        /// </summary>
        String Name { get; }


        /// <summary>
        /// Init handler. Extension should load it's config during that step
        /// </summary>
        /// <param name="descriptor"></param>
        void Init(ServiceDescriptor descriptor);

        /// <summary>
        /// Start handler. Called during start of the service
        /// </summary>
        /// <param name="eventWriter">Logger</param>
        /// <exception cref="ExtensionException">Any error during execution</exception>
        void OnStart(IEventWriter eventWriter);

        /// <summary>
        /// Stop handler. Called during stop of the service
        /// </summary>
        /// <param name="eventWriter">Logger</param>
        /// <exception cref="ExtensionException">Any error during execution</exception>
        void OnStop(IEventWriter eventWriter);
    }
}
