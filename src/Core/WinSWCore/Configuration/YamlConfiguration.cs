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

        public List<Downloading>? download;
    }

    public class ServiceAccount
    {
        public string name;
        public string domain;
        public string user;
        public string allowservicelogon;
    }

    public class Log
    {
        public string? sizeThreshold;
        public string? keepFiles;
        public string? pattern;
        public string? autoRollAtTime;
        public string? period;
        public string? mod;
    }

    public class Downloading
    {
        public string from;
        public string to;
        public string auth;
        public string? username;
        public string? password;
        public bool unsecureAuth;
        public bool failOnError;
        public string? proxy;
    }
}
