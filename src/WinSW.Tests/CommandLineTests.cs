using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using WinSW.Tests.Util;
using Xunit;
using Helper = WinSW.Tests.Util.CommandLineTestHelper;

namespace WinSW.Tests
{
    public class CommandLineTests
    {
        [ElevatedFact]
        public void Install_Start_Stop_Uninstall_Console_App()
        {
            using var config = Helper.TestXmlServiceConfig.FromXml(Helper.SeedXml);

            try
            {
                _ = Helper.Test(new[] { "install", config.FullPath }, config);

                using var controller = new ServiceController(Helper.Name);
                Assert.Equal(Helper.DisplayName, controller.DisplayName);
                Assert.False(controller.CanStop);
                Assert.False(controller.CanShutdown);
                Assert.False(controller.CanPauseAndContinue);
                Assert.Equal(ServiceControllerStatus.Stopped, controller.Status);
                Assert.Equal(ServiceType.Win32OwnProcess, controller.ServiceType);

#if NET
                InterProcessCodeCoverageSession session = null;
                try
                {
                    try
                    {
                        _ = Helper.Test(new[] { "start", config.FullPath }, config);
                        controller.Refresh();
                        Assert.Equal(ServiceControllerStatus.Running, controller.Status);
                        Assert.True(controller.CanStop);

                        Assert.EndsWith(
                            ServiceMessages.StartedSuccessfully + Environment.NewLine,
                            File.ReadAllText(Path.ChangeExtension(config.FullPath, ".wrapper.log")));

                        if (Environment.GetEnvironmentVariable("System.DefinitionId") != null)
                        {
                            session = new InterProcessCodeCoverageSession(Helper.Name);
                        }
                    }
                    finally
                    {
                        _ = Helper.Test(new[] { "stop", config.FullPath }, config);
                        controller.Refresh();
                        Assert.Equal(ServiceControllerStatus.Stopped, controller.Status);

                        Assert.EndsWith(
                            ServiceMessages.StoppedSuccessfully + Environment.NewLine,
                            File.ReadAllText(Path.ChangeExtension(config.FullPath, ".wrapper.log")));
                    }
                }
                finally
                {
                    session?.Wait();
                }
#endif
            }
            finally
            {
                _ = Helper.Test(new[] { "uninstall", config.FullPath }, config);
            }
        }

        [Fact]
        public void FailOnUnknownCommand()
        {
            const string commandName = "unknown";

            var result = Helper.ErrorTest(new[] { commandName });

            Assert.Equal($"Unrecognized command or argument '{commandName}'.\r\n\r\n", result.Error);
        }

        /// <summary>
        /// https://github.com/kohsuke/winsw/issues/206
        /// </summary>
        [Fact(Skip = "unknown")]
        public void ShouldNotPrintLogsForStatusCommand()
        {
            string cliOut = Helper.Test(new[] { "status" });
            Assert.Equal("NonExistent" + Environment.NewLine, cliOut);
        }

        [Fact]
        public void Customize()
        {
            const string OldCompanyName = "CloudBees, Inc.";
            const string NewCompanyName = "CLOUDBEES, INC.";

            string inputPath = Layout.WinSWExe;

            Assert.Equal(OldCompanyName, FileVersionInfo.GetVersionInfo(inputPath).CompanyName);

            // deny write access
            using var file = File.OpenRead(inputPath);

            string outputPath = Path.GetTempFileName();
            Program.TestExecutablePath = inputPath;
            try
            {
                _ = Helper.Test(new[] { "customize", "-o", outputPath, "--manufacturer", NewCompanyName });

                Assert.Equal(NewCompanyName, FileVersionInfo.GetVersionInfo(outputPath).CompanyName);
            }
            finally
            {
                Program.TestExecutablePath = null;
                File.Delete(outputPath);
            }
        }
    }
}
