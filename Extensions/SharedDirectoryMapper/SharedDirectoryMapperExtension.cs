using System;
using System.Collections.Generic;
using System.Text;
using winsw.Utils;

namespace winsw.Extensions.SharedDirectoryMapper
{
    public class SharedDirectoryMapperExtension : IWinSWExtension
    {
        public const String EXTENSION_NAME="SharedDirectoryMapper";

        public String Name { get; private set; }

        SharedDirectoryMapper mapper = new SharedDirectoryMapper();
        SharedMemoryMapperConfig config;

        public SharedDirectoryMapperExtension(String name, bool enableMapping, string directoryUNC, string driveLabel)
        {
            Name = name;
            config = new SharedMemoryMapperConfig();
            config.EnableMapping = enableMapping;
            config.Label = driveLabel;
            config.SharedDirPath = directoryUNC;
        }

        public void Init(ServiceDescriptor descriptor)
        {
            //config.EnableMapping = descriptor.MapSharedFolder;
            //config.Label = descriptor.MapSharedFolderLabel;
            //config.SharedDirPath = descriptor.MapSharedFolderPath;

            //TODO: delete
            //config.EnableMapping = true;
            //config.Label = "N:";
            //config.SharedDirPath = "\\\\ru20slowfs01\\ru20ipta01\\arcjenkinsdev\\slaves\\ru20-autowin03";
        }

        public void Init(bool mapSharedFolder, string mapSharedFolderLabel, string mapSharedFolderPath)
        {
         //   config.EnableMapping = mapSharedFolder;
         //   config.Label = mapSharedFolderLabel;
          //  config.SharedDirPath = mapSharedFolderPath;
        }

        public void OnStart(IEventWriter eventWriter)
        {
            
            if (config.EnableMapping)
            {
                eventWriter.LogEvent(Name + ": Mounting shared directory " + config.SharedDirPath + " to " + config.Label, System.Diagnostics.EventLogEntryType.Information);
                try
                {
                    mapper.MountDirectory(config.Label, config.SharedDirPath);
                }
                catch (Exception ex)
                {
                    throw new ExtensionException(Name, "Can't map shared directory", ex);
                }
            }
            else
            {
                eventWriter.LogEvent(Name + ": Mounting is disabled", System.Diagnostics.EventLogEntryType.Warning);
             
            }
        }

        public void OnStop(IEventWriter eventWriter)
        {
            if (config.EnableMapping)
            {
                try
                {
                    mapper.UnmountDirectory(config.Label);
                }
                catch (Exception ex)
                {
                    throw new ExtensionException(Name, "Can't unmap shared directory", ex);
                }
            }
        }
    }
}
