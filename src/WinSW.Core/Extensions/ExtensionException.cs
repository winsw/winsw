using System;

namespace WinSW.Extensions
{
    public class ExtensionException : WinSWException
    {
        public string ExtensionId { get; private set; }

        public ExtensionException(string extensionName, string message)
            : base(message)
        {
            this.ExtensionId = extensionName;
        }

        public ExtensionException(string extensionName, string message, Exception innerException)
            : base(message, innerException)
        {
            this.ExtensionId = extensionName;
        }

        public override string Message => this.ExtensionId + ": " + base.Message;
    }
}
