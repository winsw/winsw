using System;

namespace winsw.Extensions
{
    public class ExtensionException : WinSWException
    {
        public String ExtensionId { get; private set; }

        public ExtensionException(String extensionName, String message)
            : base(message)
        {
            ExtensionId = extensionName;
        }

        public ExtensionException(String extensionName, String message, Exception innerException)
            : base(message, innerException)
        {
            ExtensionId = extensionName;
        }

        public override string Message
        {
            get
            {
                return ExtensionId + ": " + base.Message;
            }
        }
    }
}
