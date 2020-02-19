using System;

namespace WinSW
{
    public class WinSWException : Exception
    {
        public WinSWException(string message)
            : base(message)
        {
        }

        public WinSWException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
