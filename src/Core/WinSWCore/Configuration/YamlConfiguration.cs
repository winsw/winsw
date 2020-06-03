using System.Collections.Generic;

namespace winsw.Configuration
{
    public class YamlConfiguration
    {
        public readonly string id;
        public readonly string? name;
        public readonly string? description;
        public readonly string? executable;
        public readonly string? workingdirectory;

        public readonly ServiceAccount? serviceaccount;
        public readonly Log? log;
        public readonly List<Download>? download;
    }

    public class ServiceAccount
    {
        public readonly string? name;
        public readonly string? domain;
        public readonly string? user;
        public readonly string? allowservicelogon;
    }

    public class Log
    {
        public readonly string? sizeThreshold;
        public readonly string? keepFiles;
        public readonly string? pattern;
        public readonly string? autoRollAtTime;
        public readonly string? period;
        public readonly string? mod;
    }

    public class Download
    {
        public readonly string from;
        public readonly string to;
        public readonly string auth;
        public readonly string? username;
        public readonly string? passsword;
        public readonly bool unsecureAuth;
        public readonly bool failOnError;
        public readonly string? proxy;
    }
}
