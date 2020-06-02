using System;
using System.Collections.Generic;
using System.Text;

namespace winsw.Configuration
{
    public class YamlConfiguration
    {
        readonly ServiceAccount? serviceAccount;
        readonly BasicConfigs? basicConfigs;
    }

    public class ServiceAccount
    {
        public string name { get; set; }
        public string domain { get; set; }
        public string user { get; set; }
        public string allowservicelogon { get; set; }
    }

    public class BasicConfigs
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string executable { get; set; }
    }
}
