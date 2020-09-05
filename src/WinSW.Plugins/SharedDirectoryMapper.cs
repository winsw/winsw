using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Xml;
using log4net;
using WinSW.Extensions;
using WinSW.Util;
using static WinSW.Plugins.SharedDirectoryMapper.SharedDirectoryMapper.Native;

namespace WinSW.Plugins.SharedDirectoryMapper
{
    public class SharedDirectoryMapper : AbstractWinSWExtension
    {
        private readonly List<SharedDirectoryMapperConfig> entries = new List<SharedDirectoryMapperConfig>();

        public override string DisplayName => "Shared Directory Mapper";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(SharedDirectoryMapper));

        public SharedDirectoryMapper()
        {
        }

        public SharedDirectoryMapper(bool enableMapping, string directoryUNC, string driveLabel)
        {
            var config = new SharedDirectoryMapperConfig(enableMapping, driveLabel, directoryUNC);
            this.entries.Add(config);
        }

        public override void Configure(XmlServiceConfig config, XmlNode node)
        {
            var mapNodes = XmlHelper.SingleNode(node, "mapping", false)!.SelectNodes("map");
            if (mapNodes != null)
            {
                for (int i = 0; i < mapNodes.Count; i++)
                {
                    if (mapNodes[i] is XmlElement mapElement)
                    {
                        this.entries.Add(SharedDirectoryMapperConfig.FromXml(mapElement));
                    }
                }
            }
        }

        public override void OnWrapperStarted()
        {
            foreach (var config in this.entries)
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
            foreach (var config in this.entries)
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
            var inner = new Win32Exception(error);
            throw new ExtensionException(this.Descriptor.Id, $"{this.DisplayName}: {message} {inner.Message}", inner);
        }

        internal static class Native
        {
            internal const uint RESOURCETYPE_DISK = 0x00000001;

            private const string MprLibraryName = "mpr.dll";

            [DllImport(MprLibraryName, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "WNetAddConnection2W")]
            internal static extern int WNetAddConnection2(in NETRESOURCE netResource, string? password = null, string? userName = null, uint flags = 0);

            [DllImport(MprLibraryName, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "WNetCancelConnection2W")]
            internal static extern int WNetCancelConnection2(string name, uint flags = 0, bool force = false);

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct NETRESOURCE
            {
                public uint Scope;
                public uint Type;
                public uint DisplayType;
                public uint Usage;
                public string LocalName;
                public string RemoteName;
                public string Comment;
                public string Provider;
            }
        }
    }
}
