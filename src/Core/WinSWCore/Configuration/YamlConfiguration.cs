namespace winsw.Configuration
{
    public class YamlConfiguration
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
        public string? executable { get; set; }
        public string? workingdirectory { get; set; }

        public ServiceAccount? serviceaccount { get; set; }
        public Log? log { get; set; }
    }

    public class ServiceAccount
    {
        public string name { get; set; }
        public string domain { get; set; }
        public string user { get; set; }
        public string allowservicelogon { get; set; }
    }

    public class Log
    {
        public string sizeThreshold { get; set; }
        public string keepFiles { get; set; }
        public string pattern { get; set; }
        public string autoRollAtTime { get; set; }
        public string period { get; set; }
        public string mod { get; set; }
    }
}
