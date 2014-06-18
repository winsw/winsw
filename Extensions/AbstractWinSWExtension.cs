using System;
using System.Collections.Generic;
using System.Text;
using winsw.util;
using System.Xml;

namespace winsw.extensions
{
    public abstract class AbstractWinSWExtension : IWinSWExtension 
    {
        public abstract String DisplayName { get; }
        public WinSWExtensionDescriptor Descriptor { get; set; }

        public virtual void Configure(ServiceDescriptor descriptor, XmlNode node)
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
    }
}
