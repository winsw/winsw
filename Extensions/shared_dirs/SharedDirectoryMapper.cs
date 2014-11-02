using System;
using System.Collections.Generic;
using System.Text;
using winsw.util;
using System.Xml;
using winsw.Utils;
using System.Diagnostics;

namespace winsw.extensions.shared_dirs
{
    public class SharedDirectoryMapper : AbstractWinSWExtension
    {
        private SharedDirectoryMappingHelper mapper = new SharedDirectoryMappingHelper();
        private List<SharedDirectoryMapperConfig> entries = new List<SharedDirectoryMapperConfig>();

        public override String DisplayName { get { return "Shared Directory Mapper"; } }

        public SharedDirectoryMapper()
        {
        }

        public SharedDirectoryMapper(bool enableMapping, string directoryUNC, string driveLabel)
        {
            SharedDirectoryMapperConfig config = new SharedDirectoryMapperConfig(enableMapping, driveLabel, directoryUNC);
            entries.Add(config);
        }

        public override void Configure(ServiceDescriptor descriptor, XmlNode node, IEventWriter logger)
        {
            var nodes = XmlHelper.SingleNode(node, "mapping", false).SelectNodes("map");
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
                    eventWriter.LogEvent(DisplayName + ": Mapping shared directory " + config.UNCPath + " to " + config.Label, System.Diagnostics.EventLogEntryType.Information);
                    try
                    {
                        mapper.MapDirectory(config.Label, config.UNCPath);
                    }
                    catch (MapperException ex)
                    {
                        HandleMappingError(config, eventWriter, ex);
                    }
                }
                else
                {
                    eventWriter.LogEvent(DisplayName + ": Mapping of " + config.Label + " is disabled", System.Diagnostics.EventLogEntryType.Warning);
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
                        HandleMappingError(config, eventWriter, ex);
                    }
                }
            }
        }

        private void HandleMappingError(SharedDirectoryMapperConfig config, IEventWriter eventWriter, MapperException ex) {
            String prefix = "Mapping of " + config.Label+ " ";
            eventWriter.LogEvent(prefix + "STDOUT: " + ex.Process.StandardOutput.ReadToEnd(), EventLogEntryType.Information);
            eventWriter.LogEvent(prefix + "STDERR: " + ex.Process.StandardError.ReadToEnd(), EventLogEntryType.Information);

            throw new ExtensionException(Descriptor.Id, DisplayName + ": " + prefix + "failed", ex);
        }
    }
}
