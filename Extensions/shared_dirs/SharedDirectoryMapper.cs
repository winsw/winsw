using System;
using System.Collections.Generic;
using System.Text;
using winsw.util;
using System.Xml;

namespace winsw.extensions.shared_dirs
{
    public class SharedDirectoryMapper : AbstractWinSWExtension
    {
        private SharedDirectoryMappingHelper mapper = new SharedDirectoryMappingHelper();
        private List<SharedDirectoryMapperConfig> entries;

        public override String DisplayName { get { return "Shared Directory Mapper"; } }

        public SharedDirectoryMapper()
        {
        }

        public SharedDirectoryMapper(bool enableMapping, string directoryUNC, string driveLabel)
        {
            SharedDirectoryMapperConfig config = new SharedDirectoryMapperConfig(enableMapping, driveLabel, directoryUNC);
            this.entries = new List<SharedDirectoryMapperConfig> { config };
        }

        public override void Configure(ServiceDescriptor descriptor, XmlNode node)
        {
            var nodes = node.SelectNodes("map");
            if (nodes != null)
            {
                foreach (XmlNode mapNode in nodes)
                {
                    var mapElement = mapNode as XmlElement;
                    if (mapElement != null)
                    {
                        var config = SharedDirectoryMapperConfig.FromXml(mapElement);
                        entries.Add(config);
                    }
                }
            }
        }

        public override void OnStart(IEventWriter eventWriter)
        {
            foreach (SharedDirectoryMapperConfig config in entries)
            {
                if (config.EnableMapping)
                {
                    eventWriter.LogEvent(DisplayName + ": Mounting shared directory " + config.UNCPath + " to " + config.Label, System.Diagnostics.EventLogEntryType.Information);
                    try
                    {
                        mapper.MapDirectory(config.Label, config.UNCPath);
                    }
                    catch (MapperException ex)
                    {
                        throw new ExtensionException(DisplayName, "Can't map shared directory", ex);
                    }
                }
                else
                {
                    eventWriter.LogEvent(DisplayName + ": Mounting is disabled", System.Diagnostics.EventLogEntryType.Warning);
                }
            }
        }

        public override void OnStop(IEventWriter eventWriter)
        {
            foreach (SharedDirectoryMapperConfig config in entries)
            {
                if (config.EnableMapping)
                {
                    try
                    {
                        mapper.UnmapDirectory(config.Label);
                    }
                    catch (MapperException ex)
                    {
                        throw new ExtensionException(DisplayName, "Can't unmap shared directory", ex);
                    }
                }
            }
        }
    }
}
