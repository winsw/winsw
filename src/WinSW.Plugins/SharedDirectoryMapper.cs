using System.Collections.Generic;
using System.IO;
using System.Xml;
using log4net;
using WinSW.Configuration;
using WinSW.Extensions;
using WinSW.Util;

namespace WinSW.Plugins
{
    public class SharedDirectoryMapper : AbstractWinSWExtension
    {
        private readonly SharedDirectoryMappingHelper _mapper = new();
        private readonly List<SharedDirectoryMapperConfig> _entries = new();

        public override string DisplayName => "Shared Directory Mapper";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(SharedDirectoryMapper));

        public SharedDirectoryMapper()
        {
        }

        public SharedDirectoryMapper(bool enableMapping, string directoryUNC, string driveLabel)
        {
            var config = new SharedDirectoryMapperConfig(enableMapping, driveLabel, directoryUNC);
            this._entries.Add(config);
        }

        public override void Configure(IWinSWConfiguration descriptor, XmlNode node)
        {
            var mapNodes = XmlHelper.SingleNode(node, "mapping", false)!.SelectNodes("map");
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

        public override void Configure(IWinSWConfiguration descriptor, YamlExtensionConfiguration config)
        {
            var dict = config.GetSettings();

            object mappingNode = dict["mapping"];

            if (!(mappingNode is List<object> mappings))
            {
                throw new InvalidDataException("SharedDirectoryMapper mapping should be a list");
            }

            foreach (object map in mappings)
            {
                var mapConfig = SharedDirectoryMapperConfig.FromYaml(map);
                this._entries.Add(mapConfig);
            }
        }

        public override void OnWrapperStarted()
        {
            foreach (var config in this._entries)
            {
                if (config.EnableMapping)
                {
                    Logger.Info(this.DisplayName + ": Mapping shared directory " + config.UNCPath + " to " + config.Label);
                    try
                    {
                        this._mapper.MapDirectory(config.Label, config.UNCPath);
                    }
                    catch (MapperException ex)
                    {
                        this.HandleMappingError(config, ex);
                    }
                }
                else
                {
                    Logger.Warn(this.DisplayName + ": Mapping of " + config.Label + " is disabled");
                }
            }
        }

        public override void BeforeWrapperStopped()
        {
            foreach (var config in this._entries)
            {
                if (config.EnableMapping)
                {
                    try
                    {
                        this._mapper.UnmapDirectory(config.Label);
                    }
                    catch (MapperException ex)
                    {
                        this.HandleMappingError(config, ex);
                    }
                }
            }
        }

        private void HandleMappingError(SharedDirectoryMapperConfig config, MapperException ex)
        {
            Logger.Error("Mapping of " + config.Label + " failed. STDOUT: " + ex.Process.StandardOutput.ReadToEnd()
                + " \r\nSTDERR: " + ex.Process.StandardError.ReadToEnd(), ex);
            throw new ExtensionException(this.Descriptor.Id, this.DisplayName + ": Mapping of " + config.Label + "failed", ex);
        }
    }
}
