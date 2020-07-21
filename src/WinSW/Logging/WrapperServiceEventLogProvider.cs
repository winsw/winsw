using System.Diagnostics;

namespace WinSW.Logging
{
    /// <summary>
    /// Implements caching of the WindowsService reference in WinSW.
    /// </summary>
    public class WrapperServiceEventLogProvider : IServiceEventLogProvider
    {
        public WrapperService? Service { get; set; }

        public EventLog? Locate() => this.Service?.EventLog;
    }
}
