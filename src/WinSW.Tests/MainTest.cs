using System;
using System.ServiceProcess;
using WinSW.Tests.Util;
using Xunit;

namespace WinSW.Tests
{
    public class MainTest
    {
        [ElevatedFact]
        public void TestInstall()
        {
            try
            {
                _ = CLITestHelper.CLITest(new[] { "install" });

                using ServiceController controller = new ServiceController(CLITestHelper.Id);
                Assert.Equal(CLITestHelper.Name, controller.DisplayName);
                Assert.False(controller.CanStop);
                Assert.False(controller.CanShutdown);
                Assert.False(controller.CanPauseAndContinue);
                Assert.Equal(ServiceControllerStatus.Stopped, controller.Status);
                Assert.Equal(ServiceType.Win32OwnProcess, controller.ServiceType);
            }
            finally
            {
                _ = CLITestHelper.CLITest(new[] { "uninstall" });
            }
        }

        [Fact]
        public void PrintVersion()
        {
            string expectedVersion = WrapperService.Version.ToString();
            string cliOut = CLITestHelper.CLITest(new[] { "version" });
            Assert.Contains(expectedVersion, cliOut);
        }

        [Fact]
        public void PrintHelp()
        {
            string expectedVersion = WrapperService.Version.ToString();
            string cliOut = CLITestHelper.CLITest(new[] { "help" });

            Assert.Contains(expectedVersion, cliOut);
            Assert.Contains("start", cliOut);
            Assert.Contains("help", cliOut);
            Assert.Contains("version", cliOut);

            // TODO: check all commands after the migration of ccommands to enum
        }

        [Fact]
        public void FailOnUnsupportedCommand()
        {
            const string commandName = "nonExistentCommand";
            string expectedMessage = "Unknown command: " + commandName;
            CLITestResult result = CLITestHelper.CLIErrorTest(new[] { commandName });

            Assert.True(result.HasException);
            Assert.Contains(expectedMessage, result.Out);
            Assert.Contains(expectedMessage, result.Exception.Message);
        }

        /// <summary>
        /// https://github.com/kohsuke/winsw/issues/206
        /// </summary>
        [Fact]
        public void ShouldNotPrintLogsForStatusCommand()
        {
            string cliOut = CLITestHelper.CLITest(new[] { "status" });
            Assert.Equal("NonExistent" + Environment.NewLine, cliOut);
        }
    }
}
