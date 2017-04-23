using System;
using System.Collections.Generic;
using System.Text;

namespace winsw.Logging
{
    /// <summary>
    /// Defines Log Handler types supported in WinSW
    /// </summary>
    /// <seealso cref="winsw.LogHandler"/>
    public enum LogHandlerType
    {
        None,
        Rotate,
        Reset,
        Roll,
        RollByTime,
        RollBySize,
        Append,
        RedirectToLog4Net,
        ConfigDefinedLog4Net
    }
}
