using NUnit.Framework;
using winsw;
using winswTests.Util;

namespace winswTests
{
    [TestFixture]
    class MainTest
    {
        [Test]
        public void PrintVersion()
        {
            string expectedVersion = WrapperService.Version.ToString();
            string cliOut = CLITestHelper.CLITest(new[] { "version" });
            StringAssert.Contains(expectedVersion, cliOut, "Expected that version contains " + expectedVersion);
        }

        [Test]
        public void PrintHelp()
        {
            string expectedVersion = WrapperService.Version.ToString();
            string cliOut = CLITestHelper.CLITest(new[] { "help" });

            StringAssert.Contains(expectedVersion, cliOut, "Expected that help contains " + expectedVersion);
            StringAssert.Contains("start", cliOut, "Expected that help refers start command");
            StringAssert.Contains("help", cliOut, "Expected that help refers help command");
            StringAssert.Contains("version", cliOut, "Expected that help refers version command");
            // TODO: check all commands after the migration of ccommands to enum

            // Extra options
            StringAssert.Contains("/redirect", cliOut, "Expected that help message refers the redirect message");
        }

        [Test]
        public void FailOnUnsupportedCommand()
        {
            const string commandName = "nonExistentCommand";
            string expectedMessage = "Unknown command: " + commandName.ToLower();
            CLITestResult res = CLITestHelper.CLIErrorTest(new[] { commandName });

            Assert.True(res.HasException, "Expected an exception due to the wrong command");
            StringAssert.Contains(expectedMessage, res.Out, "Expected the message about unknown command");
            // ReSharper disable once PossibleNullReferenceException
            StringAssert.Contains(expectedMessage, res.Exception.Message, "Expected the message about unknown command");
        }

        /// <summary>
        /// https://github.com/kohsuke/winsw/issues/206
        /// </summary>
        [Test]
        public void ShouldNotPrintLogsForStatusCommand()
        {
            string cliOut = CLITestHelper.CLITest(new[] { "status" });
            StringAssert.AreEqualIgnoringCase("NonExistent\r\n", cliOut);
        }
    }
}
