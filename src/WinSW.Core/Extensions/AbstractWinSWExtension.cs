using System.Xml;
using WinSW.Configuration;

namespace WinSW.Extensions
{
    public abstract class AbstractWinSWExtension : IWinSWExtension
    {
        public abstract string DisplayName { get; }

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public WinSWExtensionDescriptor Descriptor { get; set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        public virtual void Configure(IServiceConfig descriptor, XmlNode node)
        {
            // Do nothing
        }

        public virtual void Configure(IServiceConfig descriptor, YamlExtensionConfig config)
        {
            // Do nothing
        }

        public virtual void OnWrapperStarted()
        {
            // Do nothing
        }

        public virtual void BeforeWrapperStopped()
        {
            // Do nothing
        }

        public virtual void OnProcessStarted(System.Diagnostics.Process process)
        {
            // Do nothing
        }

        public virtual void OnProcessTerminated(System.Diagnostics.Process process)
        {
            // Do nothing
        }
    }
}
