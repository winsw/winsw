using System.Diagnostics;

namespace WinSW.Logging
{
    /// <summary>
    /// Implements caching of the WindowsService reference in WinSW.
    /// </summary>
    internal sealed class WrapperServiceEventLogProvider
    {
        public WrapperService? Service { get; set; }

        public EventLog? Locate()
        {
            var service = this.Service;
            if (service is not null && !service.IsShuttingDown)
            {
                return service.EventLog;
            }

            // By default return null
            return null;
        }
    }
}
