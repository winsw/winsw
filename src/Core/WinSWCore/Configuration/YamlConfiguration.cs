using System.Collections.Generic;

namespace winsw.Configuration
{
    public class YamlConfiguration
    {
        public string? id;
        public string? name;
        public string? description;
        public string? executable;
        public string? workingdirectory;

        public ServiceAccount? serviceaccount;
        public Log? log;

        public List<Download>? download;
    }
}
