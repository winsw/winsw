namespace WMI
{
    public enum ServiceType
    {
        KernalDriver = 1,
        FileSystemDriver = 2,
        Adapter = 4,
        RecognizerDriver = 8,
        OwnProcess = 16,
        ShareProcess = 32,
        InteractiveProcess = 256,
    }

    public enum ErrorControl
    {
        UserNotNotified = 0,
        UserNotified = 1,
        SystemRestartedWithLastKnownGoodConfiguration = 2,
        SystemAttemptsToStartWithAGoodConfiguration = 3
    }

    public enum StartMode
    {
        /// <summary>
        /// Device driver started by the operating system loader. This value is valid only for driver services.
        /// </summary>
        Boot,

        /// <summary>
        /// Device driver started by the operating system initialization process. This value is valid only for driver services.
        /// </summary>
        System,

        /// <summary>
        /// Service to be started automatically by the Service Control Manager during system startup.
        /// </summary>
        Automatic,

        /// <summary>
        /// Service to be started by the Service Control Manager when a process calls the StartService method.
        /// </summary>
        Manual,

        /// <summary>
        /// Service that can no longer be started.
        /// </summary>
        Disabled,
    }

    [WmiClassName("Win32_Service")]
    public interface IWin32Services : IWmiCollection
    {
        // ReturnValue Create(bool desktopInteract, string displayName, int errorControl, string loadOrderGroup, string loadOrderGroupDependencies, string name, string pathName, string serviceDependencies, string serviceType, string startMode, string startName, string startPassword);
        void Create(string name, string displayName, string pathName, ServiceType serviceType, ErrorControl errorControl, string startMode, bool desktopInteract, string? startName, string? startPassword, string[] serviceDependencies);

        void Create(string name, string displayName, string pathName, ServiceType serviceType, ErrorControl errorControl, string startMode, bool desktopInteract, string[] serviceDependencies);

        IWin32Service? Select(string name);
    }

    // https://docs.microsoft.com/windows/win32/cimwin32prov/win32-service
    public interface IWin32Service : IWmiObject
    {
        bool Started { get; }

        void Delete();

        void StartService();

        void StopService();
    }
}
