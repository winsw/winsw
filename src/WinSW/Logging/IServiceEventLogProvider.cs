namespace WinSW.Logging
{
    /// <summary>
    /// Indicates that the class may reference the event log
    /// </summary>
    internal interface IServiceEventLogProvider
    {
        /// <summary>
        /// Locates Event Log for the service.
        /// </summary>
        /// <returns>Event Log or null if it is not avilable</returns>
        IServiceEventLog? Locate();
    }
}
