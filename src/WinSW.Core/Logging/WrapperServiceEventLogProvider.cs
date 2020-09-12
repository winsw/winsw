namespace WinSW.Logging
{
    /// <summary>
    /// Implements caching of the WindowsService reference in WinSW.
    /// </summary>
    internal sealed class WrapperServiceEventLogProvider
    {
        public WrapperService? Service { get; set; }

        public IServiceEventLog? Locate() => this.Service;
    }
}
