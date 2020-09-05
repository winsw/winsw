using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;
using Xunit;

namespace WinSW.Tests.Util
{
    /// <summary>
    /// Helper for WinSW CLI testing
    /// </summary>
    public static class CommandLineTestHelper
    {
        public const string Name = "WinSW.Tests";
        public const string DisplayName = "WinSW Test Service";

        internal static readonly string SeedXml =
$@"<service>
  <id>{Name}</id>
  <name>{DisplayName}</name>
  <executable>cmd.exe</executable>
  <arguments>/c timeout /t -1 /nobreak</arguments>
</service>";

        private static readonly XmlServiceConfig DefaultServiceConfig = XmlServiceConfig.FromXml(SeedXml);

        /// <summary>
        /// Runs a simle test, which returns the output CLI
        /// </summary>
        /// <param name="arguments">CLI arguments to be passed</param>
        /// <param name="config">Optional Service config (will be used for initialization purposes)</param>
        /// <returns>STDOUT if there's no exceptions</returns>
        /// <exception cref="Exception">Command failure</exception>
        public static string Test(string[] arguments, XmlServiceConfig config = null)
        {
            var tmpOut = Console.Out;
            var tmpError = Console.Error;

            using var swOut = new StringWriter();
            using var swError = new StringWriter();

            Console.SetOut(swOut);
            Console.SetError(swError);
            Program.TestConfig = config ?? DefaultServiceConfig;
            try
            {
                _ = Program.Main(arguments);
            }
            finally
            {
                Console.SetOut(tmpOut);
                Console.SetError(tmpError);
                Program.TestConfig = null;
            }

            Assert.Equal(string.Empty, swError.ToString());
            return swOut.ToString();
        }

        /// <summary>
        /// Runs a simle test, which returns the output CLI
        /// </summary>
        /// <param name="arguments">CLI arguments to be passed</param>
        /// <param name="config">Optional Service config (will be used for initialization purposes)</param>
        /// <returns>Test results</returns>
        public static CommandLineTestResult ErrorTest(string[] arguments, XmlServiceConfig config = null)
        {
            Exception exception = null;

            var tmpOut = Console.Out;
            var tmpError = Console.Error;

            using var swOut = new StringWriter();
            using var swError = new StringWriter();

            Console.SetOut(swOut);
            Console.SetError(swError);
            Program.TestConfig = config ?? DefaultServiceConfig;
            Program.TestExceptionHandler = (e, _) => exception = e;
            try
            {
                _ = Program.Main(arguments);
            }
            catch (Exception e)
            {
                exception = e;
            }
            finally
            {
                Console.SetOut(tmpOut);
                Console.SetError(tmpError);
                Program.TestConfig = null;
                Program.TestExceptionHandler = null;
            }

            return new CommandLineTestResult(swOut.ToString(), swError.ToString(), exception);
        }

        internal sealed class TestXmlServiceConfig : XmlServiceConfig, IDisposable
        {
            private readonly string directory;

            private bool disposed;

            internal TestXmlServiceConfig(XmlDocument document, string name)
                : base(document)
            {
                string directory = this.directory = Path.Combine(Path.GetTempPath(), name);
                _ = Directory.CreateDirectory(directory);

                try
                {
                    string path = this.FullPath = Path.Combine(directory, "config.xml");
                    using (var file = File.CreateText(path))
                    {
                        file.Write(SeedXml);
                    }

                    this.BaseName = name;
                    this.BasePath = Path.Combine(directory, name);
                }
                catch
                {
                    Directory.Delete(directory, true);
                    throw;
                }
            }

            ~TestXmlServiceConfig() => this.Dispose(false);

            public override string FullPath { get; }

            public override string BasePath { get; }

            public override string BaseName { get; set; }

            public override string ExecutablePath => Layout.WinSWExe;

            internal static TestXmlServiceConfig FromXml(string xml, [CallerMemberName] string name = null)
            {
                var document = new XmlDocument();
                document.LoadXml(xml);
                return new TestXmlServiceConfig(document, name);
            }

            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool _)
            {
                if (!disposed)
                {
                    Directory.Delete(this.directory, true);
                    disposed = true;
                }
            }
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
