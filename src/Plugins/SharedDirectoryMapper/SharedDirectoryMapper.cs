using System;
using System.Collections.Generic;
using System.Xml;
using log4net;
using winsw.Extensions;
using winsw.Util;

namespace winsw.Plugins.SharedDirectoryMapper
{
    public class SharedDirectoryMapper : AbstractWinSWExtension
    {
        private readonly SharedDirectoryMappingHelper _mapper = new SharedDirectoryMappingHelper();
        private readonly List<SharedDirectoryMapperConfig> _entries = new List<SharedDirectoryMapperConfig>();

        public override String DisplayName => "Shared Directory Mapper";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(SharedDirectoryMapper));

        public SharedDirectoryMapper()
        {
        }

        public SharedDirectoryMapper(bool enableMapping, string directoryUNC, string driveLabel)
        {
            SharedDirectoryMapperConfig config = new SharedDirectoryMapperConfig(enableMapping, driveLabel, directoryUNC);
            _entries.Add(config);
        }

        public override void Configure(ServiceDescriptor descriptor, XmlNode node)
        {
            var nodes = XmlHelper.SingleNode(node, "mapping", false).SelectNodes("map");
            if (nodes != null)
            {
                foreach (XmlNode mapNode in nodes)
                {
                    if (mapNode is XmlElement mapElement)
                    {
                        var config = SharedDirectoryMapperConfig.FromXml(mapElement);
                        _entries.Add(config);
                    }
                }
            }
        }

        public override void OnWrapperStarted()
        {
            foreach (SharedDirectoryMapperConfig config in _entries)
            {
                if (config.EnableMapping)
                {
                    Logger.Info(DisplayName + ": Mapping shared directory " + config.UNCPath + " to " + config.Label);
                    try
                    {
                        _mapper.MapDirectory(config.Label, config.UNCPath);
                    }
                    catch (MapperException ex)
                    {
                        HandleMappingError(config, ex);
                    }
                }
                else
                {
                    Logger.Warn(DisplayName + ": Mapping of " + config.Label + " is disabled");
                }
            }
        }

        public override void BeforeWrapperStopped()
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
                        HandleMappingError(config, ex);
                    }
                }
            }
        }

        private void HandleMappingError(SharedDirectoryMapperConfig config, MapperException ex)
        {
            Logger.Error("Mapping of " + config.Label + " failed. STDOUT: " + ex.Process.StandardOutput.ReadToEnd()
                + " \r\nSTDERR: " + ex.Process.StandardError.ReadToEnd(), ex);
            throw new ExtensionException(Descriptor.Id, DisplayName + ": Mapping of " + config.Label + "failed", ex);
        }
    }
}
