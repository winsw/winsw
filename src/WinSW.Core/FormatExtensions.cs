using System;
using System.Diagnostics;
using System.ServiceProcess;
using WinSW.Configuration;

namespace WinSW
{
    internal static class FormatExtensions
    {
        internal static string Format(this Process process)
        {
            try
            {
                return $"{process.ProcessName} ({process.Id})";
            }
            catch (InvalidOperationException)
            {
                return $"({process.Id})";
            }
        }

        internal static string Format(this ServiceConfig config)
        {
            string name = config.Name;
            string displayName = config.DisplayName;
            return $"{(string.IsNullOrEmpty(displayName) ? name : displayName)} ({name})";
        }

        internal static string Format(this ServiceController controller)
        {
            return $"{controller.DisplayName} ({controller.ServiceName})";
        }
    }
}
