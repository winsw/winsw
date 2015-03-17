using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using Mono.Addins;
using winsw.Extensions;
using winsw.Util;

[assembly: Addin]
[assembly: AddinDependency("SharedDirectoryMapper", "1.0")]

namespace winsw.Plugins.SharedDirectoryMapper
{
    [Extension]
    public class SharedDirectoryMapper : AbstractWinSWExtension
    {
        private readonly SharedDirectoryMappingHelper _mapper = new SharedDirectoryMappingHelper();
        private readonly List<SharedDirectoryMapperConfig> _entries = new List<SharedDirectoryMapperConfig>();

        public override String DisplayName { get { return "Shared Directory Mapper"; } }

        public SharedDirectoryMapper()
        {
        }

        public SharedDirectoryMapper(bool enableMapping, string directoryUNC, string driveLabel)
        {
            SharedDirectoryMapperConfig config = new SharedDirectoryMapperConfig(enableMapping, driveLabel, directoryUNC);
            _entries.Add(config);
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
                        _entries.Add(config);
                    }
                }
            }
        }

        public override void OnStart(IEventWriter eventWriter)
        {
            foreach (SharedDirectoryMapperConfig config in _entries)
            {
                if (config.EnableMapping)
                {
                    eventWriter.LogEvent(DisplayName + ": Mapping shared directory " + config.UNCPath + " to " + config.Label, EventLogEntryType.Information);
                    try
                    {
                        _mapper.MapDirectory(config.Label, config.UNCPath);
                    }
                    catch (MapperException ex)
                    {
                        HandleMappingError(config, eventWriter, ex);
                    }
                }
                else
                {
                    eventWriter.LogEvent(DisplayName + ": Mapping of " + config.Label + " is disabled", EventLogEntryType.Warning);
                }
            }
        }

        public override void OnStop(IEventWriter eventWriter)
        {
            foreach (SharedDirectoryMapperConfig config in _entries)
            {
                if (config.EnableMapping)
                {
                    try
                    {
                        _mapper.UnmapDirectory(config.Label);
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
