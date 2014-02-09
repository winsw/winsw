using System;
using System.Collections.Generic;
using System.Text;

namespace winsw.Extensions
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
