using System;
using System.ServiceProcess;
using NUnit.Framework;
using WinSW;
using winswTests.Util;

namespace winswTests
{
    [TestFixture]
    public class MainTest
    {
        [Test]
        public void TestInstall()
        {
            TestHelper.RequireProcessElevated();

            try
            {
                _ = CLITestHelper.CLITest(new[] { "install" });

                using var controller = new ServiceController(CLITestHelper.Id);
                Assert.That(controller.DisplayName, Is.EqualTo(CLITestHelper.Name));
                Assert.That(controller.CanStop, Is.False);
                Assert.That(controller.CanShutdown, Is.False);
                Assert.That(controller.CanPauseAndContinue, Is.False);
                Assert.That(controller.Status, Is.EqualTo(ServiceControllerStatus.Stopped));
                Assert.That(controller.ServiceType, Is.EqualTo(ServiceType.Win32OwnProcess));
            }
            finally
            {
                _ = CLITestHelper.CLITest(new[] { "uninstall" });
            }
        }

        [Test]
        public void PrintVersion()
        {
            string expectedVersion = WrapperService.Version.ToString();
            string cliOut = CLITestHelper.CLITest(new[] { "version" });
            Assert.That(cliOut, Does.Contain(expectedVersion));
        }

        [Test]
        public void PrintHelp()
        {
            string expectedVersion = WrapperService.Version.ToString();
            string cliOut = CLITestHelper.CLITest(new[] { "help" });

            Assert.That(cliOut, Does.Contain(expectedVersion));
            Assert.That(cliOut, Does.Contain("start"));
            Assert.That(cliOut, Does.Contain("help"));
            Assert.That(cliOut, Does.Contain("version"));
            // TODO: check all commands after the migration of ccommands to enum

            // Extra options
            Assert.That(cliOut, Does.Contain("/redirect"));
        }

        [Test]
        public void FailOnUnsupportedCommand()
        {
            const string commandName = "nonExistentCommand";
            string expectedMessage = "Unknown command: " + commandName;
            var result = CLITestHelper.CLIErrorTest(new[] { commandName });

            Assert.That(result.HasException, Is.True);
            Assert.That(result.Out, Does.Contain(expectedMessage));
            Assert.That(result.Exception.Message, Does.Contain(expectedMessage));
        }

        /// <summary>
        /// https://github.com/kohsuke/winsw/issues/206
        /// </summary>
        [Test]
        public void ShouldNotPrintLogsForStatusCommand()
        {
            string cliOut = CLITestHelper.CLITest(new[] { "status" });
            Assert.That(cliOut, Is.EqualTo("NonExistent" + Environment.NewLine).IgnoreCase);
        }
    }
}
