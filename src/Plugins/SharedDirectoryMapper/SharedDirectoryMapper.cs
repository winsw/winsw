using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;
using log4net;
using winsw.Extensions;
using winsw.Util;
using static winsw.Plugins.SharedDirectoryMapper.NativeMethods;

namespace winsw.Plugins.SharedDirectoryMapper
{
    public class SharedDirectoryMapper : AbstractWinSWExtension
    {
        private readonly List<SharedDirectoryMapperConfig> _entries = new List<SharedDirectoryMapperConfig>();

        public override string DisplayName => "Shared Directory Mapper";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(SharedDirectoryMapper));

        public SharedDirectoryMapper()
        {
        }

        public SharedDirectoryMapper(bool enableMapping, string directoryUNC, string driveLabel)
        {
            SharedDirectoryMapperConfig config = new SharedDirectoryMapperConfig(enableMapping, driveLabel, directoryUNC);
            this._entries.Add(config);
        }

        public override void Configure(ServiceDescriptor descriptor, XmlNode node)
        {
            XmlNodeList? mapNodes = XmlHelper.SingleNode(node, "mapping", false)!.SelectNodes("map");
            if (mapNodes != null)
            {
                for (int i = 0; i < mapNodes.Count; i++)
                {
                    if (mapNodes[i] is XmlElement mapElement)
                    {
                        var config = SharedDirectoryMapperConfig.FromXml(mapElement);
                        this._entries.Add(config);
                    }
                }
            }
        }

        public override void OnWrapperStarted()
        {
            foreach (SharedDirectoryMapperConfig config in this._entries)
            {
                string label = config.Label;
                string uncPath = config.UNCPath;
                if (config.EnableMapping)
                {
                    Logger.Info(this.DisplayName + ": Mapping shared directory " + uncPath + " to " + label);

                    int error = WNetAddConnection2(new NETRESOURCE
                    {
                        Type = RESOURCETYPE_DISK,
                        LocalName = label,
                        RemoteName = uncPath,
                    });
                    if (error != 0)
                    {
                        this.ThrowExtensionException(error, $"Mapping of {label} failed.");
                    }
                }
                else
                {
                    Logger.Warn(this.DisplayName + ": Mapping of " + label + " is disabled");
                }
            }
        }

        public override void BeforeWrapperStopped()
        {
            foreach (SharedDirectoryMapperConfig config in this._entries)
            {
                string label = config.Label;
                if (config.EnableMapping)
                {
                    int error = WNetCancelConnection2(label);
                    if (error != 0)
                    {
                        this.ThrowExtensionException(error, $"Unmapping of {label} failed.");
                    }
                }
            }
        }

        private void ThrowExtensionException(int error, string message)
        {
            Win32Exception inner = new Win32Exception(error);
            throw new ExtensionException(this.Descriptor.Id, $"{this.DisplayName}: {message} {inner.Message}", inner);
        }
    }
}
