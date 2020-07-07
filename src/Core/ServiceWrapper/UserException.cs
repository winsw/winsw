using System;

namespace WinSW
{
    internal sealed class UserException : Exception
    {
        internal UserException(string message)
            : base(message)
        {
        }

        internal UserException(string? message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
