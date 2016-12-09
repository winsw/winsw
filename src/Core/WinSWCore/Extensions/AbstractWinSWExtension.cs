using System;
using System.Xml;
using winsw.Util;

namespace winsw.Extensions
{
    public abstract class AbstractWinSWExtension : IWinSWExtension 
    {
        public abstract String DisplayName { get; }
        public WinSWExtensionDescriptor Descriptor { get; set; }

        public virtual void Configure(ServiceDescriptor descriptor, XmlNode node)
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
