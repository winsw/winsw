using System;
using System.Collections.Generic;

namespace WinSW.Util
{
    public static class ConfigHelper
    {
        public static TimeSpan ParseTimeSpan(string v)
        {
            v = v.Trim();
            foreach (var s in Suffix)
            {
                if (v.EndsWith(s.Key))
                {
                    return TimeSpan.FromMilliseconds(int.Parse(v.Substring(0, v.Length - s.Key.Length).Trim()) * s.Value);
                }
            }

            return TimeSpan.FromMilliseconds(int.Parse(v));
        }

        private static readonly Dictionary<string, long> Suffix = new()
        {
            { "ms",     1 },
            { "sec",    1000L },
            { "secs",   1000L },
            { "min",    1000L * 60L },
            { "mins",   1000L * 60L },
            { "hr",     1000L * 60L * 60L },
            { "hrs",    1000L * 60L * 60L },
            { "hour",   1000L * 60L * 60L },
            { "hours",  1000L * 60L * 60L },
            { "day",    1000L * 60L * 60L * 24L },
            { "days",   1000L * 60L * 60L * 24L }
        };

        public static bool YamlBoolParse(string value)
        {
            value = value.ToLower();

            if (value.Equals("true") || value.Equals("yes") || value.Equals("on") || value.Equals("y") || value.Equals("1"))
            {
                return true;
            }

            return false;
        }
    }
}
