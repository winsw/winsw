using System;
using System.Xml;
using winsw.Util;

namespace winsw.Extensions
{
    public abstract class AbstractWinSWExtension : IWinSWExtension 
    {
        public abstract String DisplayName { get; }
        public WinSWExtensionDescriptor Descriptor { get; set; }

        public virtual void Configure(ServiceDescriptor descriptor, XmlNode node, IEventWriter logger)
        {
            // Do nothing
        }

        public virtual void OnStart(IEventWriter eventWriter)
        {
            // Do nothing
        }

        public virtual void OnStop(IEventWriter eventWriter)
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
