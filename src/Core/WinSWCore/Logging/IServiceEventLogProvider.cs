using System.Diagnostics;

namespace winsw.Logging
{
    /// <summary>
    /// Indicates that the class may reference the event log
    /// </summary>
    public interface IServiceEventLogProvider
    {
        /// <summary>
        /// Locates Event Log for the service.
        /// </summary>
        /// <returns>Event Log or null if it is not avilable</returns>
        EventLog? locate();
    }
}
