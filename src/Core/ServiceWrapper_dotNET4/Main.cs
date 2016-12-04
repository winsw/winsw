using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace winsw.dotNET4
{
    /// <summary>
    /// Just a wrapper class, which redirects the Main entry point to the WinSW main method.
    /// </summary>
    public class dotNET4Support
    {
        public static int Main(string[] args)
        {
            return winsw.WrapperService.Main(args);
        }
    }
}
