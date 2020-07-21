namespace WinSW
{
    /// <summary>
    /// Class, which contains generic information about WinSW runtime.
    /// This information can be used by the service and extensions.
    /// </summary>
    public class WinSWSystem
    {
        /// <summary>
        /// Prefix for all environment variables being injected for WinSW
        /// </summary>
        public static readonly string SystemEnvVarPrefix = "WINSW_";

        /// <summary>
        /// Variable, which points to the service ID.
        /// It may be used to determine runaway processes.
        /// </summary>
        public static string EnvVarNameServiceId => SystemEnvVarPrefix + "SERVICE_ID";

        /// <summary>
        /// Variable, which specifies path to the executable being launched by WinSW.
        /// </summary>
        public static string EnvVarNameExecutablePath => SystemEnvVarPrefix + "EXECUTABLE";
    }
}
