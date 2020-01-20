using System.Diagnostics;

namespace winsw.Logging
{
    /// <summary>
    /// Implements caching of the WindowsService reference in WinSW.
    /// </summary>
    public class WrapperServiceEventLogProvider : IServiceEventLogProvider
    {
        public WrapperService? service { get; set; }

        public EventLog? locate()
        {
            WrapperService? _service = service;
            if (_service != null && !_service.IsShuttingDown)
            {
                return _service.EventLog;
            }

            // By default return null
            return null;
        }
    }
}
