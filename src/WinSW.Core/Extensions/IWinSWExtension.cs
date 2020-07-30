using System.Xml;

namespace WinSW.Extensions
{
    /// <summary>
    /// Interface for Win Service Wrapper Extension
    /// </summary>
    /// <remarks>
    /// All implementations should provide the default empty constructor.
    /// The initialization will be performed by Init methods.
    /// Binary comparibility of the class is not guaranteed in WinSW 2.
    /// </remarks>
    public interface IWinSWExtension
    {
        /// <summary>
        /// Extension name to be displayed in logs
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Extension descriptor
        /// </summary>
        WinSWExtensionDescriptor Descriptor { get; set; }

        /// <summary>
        /// Init handler. Extension should load it's config during that step
        /// </summary>
        /// <param name="config">Service config</param>
        /// <param name="node">Configuration node</param>
        void Configure(XmlServiceConfig config, XmlNode node);

        /// <summary>
        /// Start handler. Called during startup of the service before the child process.
        /// </summary>
        /// <param name="logger">Logger</param>
        /// <exception cref="ExtensionException">Any error during execution</exception>
        void OnWrapperStarted();

        /// <summary>
        /// Handler, which is being invoked once the child process is started.
        /// </summary>
        /// <param name="process">Process</param>
        /// <param name="logger">Logger</param>
        /// <exception cref="ExtensionException">Any error during execution</exception>
        void OnProcessStarted(System.Diagnostics.Process process);

        /// <summary>
        /// Handler, which is being invoked once the child process is terminated.
        /// </summary>
        /// <param name="process">Process</param>
        /// <param name="logger">Logger</param>
        /// <exception cref="ExtensionException">Any error during execution</exception>
        void OnProcessTerminated(System.Diagnostics.Process process);

        /// <summary>
        /// Stop handler. Called during stop of the service
        /// </summary>
        /// <param name="logger">Logger</param>
        /// <exception cref="ExtensionException">Any error during execution</exception>
        void BeforeWrapperStopped();
    }
}
