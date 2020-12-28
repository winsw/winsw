using System.ServiceProcess;
using WinSW.Configuration;

namespace WinSW
{
    internal static class FormatExtensions
    {
        internal static string Format(IServiceConfig config)
        {
            string name = config.Name;
            string displayName = config.DisplayName;
            return $"{(string.IsNullOrEmpty(displayName) ? name : displayName)} ({name})";
        }

        internal static string Format(ServiceController controller)
        {
            return $"{controller.DisplayName} ({controller.ServiceName})";
        }
    }
}
