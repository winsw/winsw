using System;

namespace winsw.Util
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
