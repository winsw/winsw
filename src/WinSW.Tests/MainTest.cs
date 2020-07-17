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
                _ = CommandLineTestHelper.Test(new[] { "install" });

                using ServiceController controller = new ServiceController(CommandLineTestHelper.Id);
                Assert.Equal(CommandLineTestHelper.Name, controller.DisplayName);
                Assert.False(controller.CanStop);
                Assert.False(controller.CanShutdown);
                Assert.False(controller.CanPauseAndContinue);
                Assert.Equal(ServiceControllerStatus.Stopped, controller.Status);
                Assert.Equal(ServiceType.Win32OwnProcess, controller.ServiceType);
            }
            finally
            {
                _ = CommandLineTestHelper.Test(new[] { "uninstall" });
            }
        }

        [Fact]
        public void FailOnUnknownCommand()
        {
            const string commandName = "unknown";

            CommandLineTestResult result = CommandLineTestHelper.ErrorTest(new[] { commandName });

            Assert.Equal($"Unrecognized command or argument '{commandName}'\r\n\r\n", result.Error);
        }

        /// <summary>
        /// https://github.com/kohsuke/winsw/issues/206
        /// </summary>
        [Fact]
        public void ShouldNotPrintLogsForStatusCommand()
        {
            string cliOut = CommandLineTestHelper.Test(new[] { "status" });
            Assert.Equal("NonExistent" + Environment.NewLine, cliOut);
        }
    }
}
