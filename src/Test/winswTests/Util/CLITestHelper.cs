using System;
using System.IO;
using winsw;

namespace winswTests.Util
{
    /// <summary>
    /// Helper for WinSW CLI testing
    /// </summary>
    public static class CLITestHelper
    {
        private const string SeedXml =
@"<service>
  <id>service.exe</id>
  <name>Service</name>
  <description>The service.</description>
  <executable>node.exe</executable>
  <arguments>My Arguments</arguments>
  <logmode>rotate</logmode>
  <workingdirectory>C:\winsw\workdir</workingdirectory>
  <logpath>C:\winsw\logs</logpath>
</service>";

        private static readonly ServiceDescriptor DefaultServiceDescriptor = ServiceDescriptor.FromXML(SeedXml);

        /// <summary>
        /// Runs a simle test, which returns the output CLI
        /// </summary>
        /// <param name="args">CLI arguments to be passed</param>
        /// <param name="descriptor">Optional Service descriptor (will be used for initializationpurposes)</param>
        /// <returns>STDOUT if there's no exceptions</returns>
        /// <exception cref="Exception">Command failure</exception>
        public static string CLITest(string[] args, ServiceDescriptor descriptor = null)
        {
            using StringWriter sw = new StringWriter();
            TextWriter tmp = Console.Out;
            Console.SetOut(sw);
            WrapperService.Run(args, descriptor ?? DefaultServiceDescriptor);
            Console.SetOut(tmp);
            Console.Write(sw.ToString());
            return sw.ToString();
        }

        /// <summary>
        /// Runs a simle test, which returns the output CLI
        /// </summary>
        /// <param name="args">CLI arguments to be passed</param>
        /// <param name="descriptor">Optional Service descriptor (will be used for initializationpurposes)</param>
        /// <returns>Test results</returns>
        public static CLITestResult CLIErrorTest(string[] args, ServiceDescriptor descriptor = null)
        {
            StringWriter swOut, swErr;
            Exception testEx = null;
            TextWriter tmpOut = Console.Out;
            TextWriter tmpErr = Console.Error;

            using (swOut = new StringWriter())
            using (swErr = new StringWriter())
            {
                try
                {
                    Console.SetOut(swOut);
                    Console.SetError(swErr);
                    WrapperService.Run(args, descriptor ?? DefaultServiceDescriptor);
                }
                catch (Exception ex)
                {
                    testEx = ex;
                }
                finally
                {
                    Console.SetOut(tmpOut);
                    Console.SetError(tmpErr);
                    Console.WriteLine("\n>>> Output: ");
                    Console.Write(swOut.ToString());
                    Console.WriteLine("\n>>> Error: ");
                    Console.Write(swErr.ToString());
                    if (testEx != null)
                    {
                        Console.WriteLine("\n>>> Exception: ");
                        Console.WriteLine(testEx);
                    }
                }
            }

            return new CLITestResult(swOut.ToString(), swErr.ToString(), testEx);
        }
    }

    /// <summary>
    /// Aggregated test report
    /// </summary>
    public class CLITestResult
    {
        public string Out { get; private set; }

        public string Err { get; private set; }

        public Exception Exception { get; private set; }

        public bool HasException => Exception != null;

        public CLITestResult(string output, string err, Exception exception = null)
        {
            Out = output;
            Err = err;
            Exception = exception;
        }
    }
}
