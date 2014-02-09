using System;
using System.Collections.Generic;
using System.Text;
using winsw.Utils;

namespace winsw.Extensions
{
    public class ExtensionException : WinSWException
    {
        public String ExtensionName { get; private set; }

        public ExtensionException(String extensionName, String message)
            : base(message)
        {
            ExtensionName = extensionName;
        }

        public ExtensionException(String extensionName, String message, Exception innerException)
            : base(message, innerException)
        {
            ExtensionName = extensionName;
        }
    }
}
