using System;
using System.Collections.Generic;
using System.Text;

namespace winsw.extensions
{
    public class WinSWException : Exception
    {
        public WinSWException(String message)
            : base(message)
        { }

        public WinSWException(String message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
