using System;

namespace winswTests.Extensions
{
    /// <summary>
    /// Base class for testing of WinSW Extensions.
    /// </summary>
    public class ExtensionTestBase
    {
        /// <summary>
        /// Defines the name of the extension to be passed in the configuration.
        /// This name should point to assembly in tests, because we do not merge extension DLLs for testing purposes.
        /// </summary>
        /// <param name="type">Type of the extension</param>
        /// <returns>String for Type locator, which includes class and assembly names</returns>
        public static string GetExtensionClassNameWithAssembly(Type type)
        {
            return type.ToString() + ", " + type.Assembly;
        }
    }
}
