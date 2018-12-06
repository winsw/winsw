using System;

namespace winsw.Extensions
{
    public class ExtensionException : WinSWException
    {
        public string ExtensionId { get; private set; }

        public ExtensionException(string extensionName, string message)
            : base(message)
        {
            ExtensionId = extensionName;
        }

        public ExtensionException(string extensionName, string message, Exception innerException)
            : base(message, innerException)
        {
            ExtensionId = extensionName;
        }

        public override string Message => ExtensionId + ": " + base.Message;
    }
}
