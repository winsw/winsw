using System;
using System.Diagnostics;
using System.IO;
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
        [Fact(Skip = "unknown")]
        public void ShouldNotPrintLogsForStatusCommand()
        {
            string cliOut = CommandLineTestHelper.Test(new[] { "status" });
            Assert.Equal("NonExistent" + Environment.NewLine, cliOut);
        }

#if NET461
        [Fact]
        public void Customize()
        {
            const string OldCompanyName = "CloudBees, Inc.";
            const string NewCompanyName = "CLOUDBEES, INC.";

            string inputPath = Path.Combine(Layout.ArtifactsDirectory, "WinSW.NET461.exe");

            Assert.Equal(OldCompanyName, FileVersionInfo.GetVersionInfo(inputPath).CompanyName);

            // deny write access
            using FileStream file = File.OpenRead(inputPath);

            string outputPath = Path.GetTempFileName();
            Program.TestExecutablePath = inputPath;
            try
            {
                _ = CommandLineTestHelper.Test(new[] { "customize", "-o", outputPath, "--manufacturer", NewCompanyName });

                Assert.Equal(NewCompanyName, FileVersionInfo.GetVersionInfo(outputPath).CompanyName);
            }
            finally
            {
                Program.TestExecutablePath = null;
                File.Delete(outputPath);
            }
        }
#endif
    }
}
