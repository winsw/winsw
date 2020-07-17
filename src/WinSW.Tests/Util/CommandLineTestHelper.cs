using System;
using System.IO;
using Xunit;

namespace WinSW.Tests.Util
{
    /// <summary>
    /// Helper for WinSW CLI testing
    /// </summary>
    public static class CommandLineTestHelper
    {
        public const string Id = "WinSW.Tests";
        public const string Name = "WinSW Test Service";

        private static readonly string SeedXml =
$@"<service>
  <id>{Id}</id>
  <name>{Name}</name>
  <description>The service.</description>
  <executable>node.exe</executable>
  <arguments>My Arguments</arguments>
  <log mode=""roll""></log>
  <workingdirectory>C:\winsw\workdir</workingdirectory>
  <logpath>C:\winsw\logs</logpath>
</service>";

        public static readonly ServiceDescriptor DefaultServiceDescriptor = ServiceDescriptor.FromXml(SeedXml);

        /// <summary>
        /// Runs a simle test, which returns the output CLI
        /// </summary>
        /// <param name="arguments">CLI arguments to be passed</param>
        /// <param name="descriptor">Optional Service descriptor (will be used for initializationpurposes)</param>
        /// <returns>STDOUT if there's no exceptions</returns>
        /// <exception cref="Exception">Command failure</exception>
        public static string Test(string[] arguments, ServiceDescriptor descriptor = null)
        {
            TextWriter tmpOut = Console.Out;
            TextWriter tmpError = Console.Error;

            using StringWriter swOut = new StringWriter();
            using StringWriter swError = new StringWriter();

            Console.SetOut(swOut);
            Console.SetError(swError);
            ServiceDescriptor.TestDescriptor = descriptor ?? DefaultServiceDescriptor;
            try
            {
                _ = Program.Run(arguments);
            }
            finally
            {
                Console.SetOut(tmpOut);
                Console.SetError(tmpError);
                ServiceDescriptor.TestDescriptor = null;
            }

            Assert.Equal(string.Empty, swError.ToString());
            return swOut.ToString();
        }

        /// <summary>
        /// Runs a simle test, which returns the output CLI
        /// </summary>
        /// <param name="arguments">CLI arguments to be passed</param>
        /// <param name="descriptor">Optional Service descriptor (will be used for initializationpurposes)</param>
        /// <returns>Test results</returns>
        public static CommandLineTestResult ErrorTest(string[] arguments, ServiceDescriptor descriptor = null)
        {
            Exception exception = null;

            TextWriter tmpOut = Console.Out;
            TextWriter tmpError = Console.Error;

            using StringWriter swOut = new StringWriter();
            using StringWriter swError = new StringWriter();

            Console.SetOut(swOut);
            Console.SetError(swError);
            ServiceDescriptor.TestDescriptor = descriptor ?? DefaultServiceDescriptor;
            Program.TestExceptionHandler = (e, _) => exception = e;
            try
            {
                _ = Program.Run(arguments);
            }
            catch (Exception e)
            {
                exception = e;
            }
            finally
            {
                Console.SetOut(tmpOut);
                Console.SetError(tmpError);
                ServiceDescriptor.TestDescriptor = null;
                Program.TestExceptionHandler = null;
            }

            return new CommandLineTestResult(swOut.ToString(), swError.ToString(), exception);
        }
    }

    /// <summary>
    /// Aggregated test report
    /// </summary>
    public class CommandLineTestResult
    {
        public string Out { get; }

        public string Error { get; }

        public Exception Exception { get; }

        public bool HasException => this.Exception != null;

        public CommandLineTestResult(string output, string error, Exception exception = null)
        {
            this.Out = output;
            this.Error = error;
            this.Exception = exception;
        }
    }
}
