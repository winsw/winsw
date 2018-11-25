using System;

namespace WinSW
{
    internal sealed class CommandException : Exception
    {
        internal CommandException(Exception inner)
            : base(inner.Message, inner)
        {
        }

        internal CommandException(string message)
            : base(message)
        {
        }

        internal CommandException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
