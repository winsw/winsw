using System;
using System.Collections.Generic;
using System.Text;

namespace winsw
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
        public static readonly string SYSTEM_EVNVVAR_PREFIX = "WINSW_";

        /// <summary>
        /// Variable, which points to the service ID.
        /// It may be used to determine runaway processes.
        /// </summary>
        public static string ENVVAR_NAME_SERVICE_ID { get { return SYSTEM_EVNVVAR_PREFIX + "SERVICE_ID"; } }

        /// <summary>
        /// Variable, which specifies path to the executable being launched by WinSW.
        /// </summary>
        public static string ENVVAR_NAME_EXECUTABLE_PATH { get { return SYSTEM_EVNVVAR_PREFIX + "EXECUTABLE"; } }
    }
}
