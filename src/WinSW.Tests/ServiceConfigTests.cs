using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using WinSW.Tests.Util;
using Xunit;
using Xunit.Abstractions;

namespace WinSW.Tests
{
    public class ServiceConfigTests
    {
        private const string ExpectedWorkingDirectory = @"Z:\Path\SubPath";
        private const string Username = "User";
        private const string Password = "Password";
        private const string Domain = "Domain";
        private const string AllowServiceAccountLogonRight = "true";

        private readonly ITestOutputHelper output;
        
        private XmlServiceConfig extendedServiceConfig;

        public ServiceConfigTests(ITestOutputHelper output)
        {
            this.output = output;

            string seedXml =
$@"<service>
  <id>service.exe</id>
  <name>Service</name>
  <description>The service.</description>
  <executable>node.exe</executable>
  <arguments>My Arguments</arguments>
  <log mode=""roll""></log>
  <serviceaccount>
    <username>{Domain}\{Username}</username>
    <password>{Password}</password>
    <allowservicelogon>{AllowServiceAccountLogonRight}</allowservicelogon>
  </serviceaccount>
  <workingdirectory>{ExpectedWorkingDirectory}</workingdirectory>
  <logpath>C:\logs</logpath>
</service>";
            this.extendedServiceConfig = XmlServiceConfig.FromXml(seedXml);
        }

        [Fact]
        public void DefaultStartMode()
        {
            Assert.Equal(ServiceStartMode.Automatic, this.extendedServiceConfig.StartMode);
        }

        [Fact]
        public void IncorrectStartMode()
        {
            string seedXml =
$@"<service>
  <id>service.exe</id>
  <name>Service</name>
  <description>The service.</description>
  <executable>node.exe</executable>
  <arguments>My Arguments</arguments>
  <startmode>roll</startmode>
  <log mode=""roll""></log>
  <serviceaccount>
    <username>{Domain}\{Username}</username>
    <password>{Password}</password>
    <allowservicelogon>{AllowServiceAccountLogonRight}</allowservicelogon>
  </serviceaccount>
  <workingdirectory>{ExpectedWorkingDirectory}</workingdirectory>
  <logpath>C:\logs</logpath>
</service>";

            this.extendedServiceConfig = XmlServiceConfig.FromXml(seedXml);
            _ = Assert.Throws<InvalidDataException>(() => this.extendedServiceConfig.StartMode);
        }

        [Fact]
        public void ChangedStartMode()
        {
            string seedXml =
$@"<service>
  <id>service.exe</id>
  <name>Service</name>
  <description>The service.</description>
  <executable>node.exe</executable>
  <arguments>My Arguments</arguments>
  <startmode>manual</startmode>
  <log mode=""roll""></log>
  <serviceaccount>
    <username>{Domain}\{Username}</username>
    <password>{Password}</password>
    <allowservicelogon>{AllowServiceAccountLogonRight}</allowservicelogon>
  </serviceaccount>
  <workingdirectory>{ExpectedWorkingDirectory}</workingdirectory>
  <logpath>C:\logs</logpath>
</service>";

            this.extendedServiceConfig = XmlServiceConfig.FromXml(seedXml);
            Assert.Equal(ServiceStartMode.Manual, this.extendedServiceConfig.StartMode);
        }

        [Fact]
        public void VerifyWorkingDirectory()
        {
            Assert.Equal(ExpectedWorkingDirectory, this.extendedServiceConfig.WorkingDirectory);
        }

        [Fact]
        public void VerifyServiceLogonRight()
        {
            Assert.True(this.extendedServiceConfig.AllowServiceAcountLogonRight);
        }

        [Fact]
        public void VerifyUsername()
        {
            Assert.Equal(Domain + "\\" + Username, this.extendedServiceConfig.ServiceAccountUserName);
        }

        [Fact]
        public void VerifyPassword()
        {
            Assert.Equal(Password, this.extendedServiceConfig.ServiceAccountPassword);
        }

        [Fact]
        public void Priority()
        {
            var config = XmlServiceConfig.FromXml("<service><id>test</id><priority>normal</priority></service>");
            Assert.Equal(ProcessPriorityClass.Normal, config.Priority);

            config = XmlServiceConfig.FromXml("<service><id>test</id><priority>idle</priority></service>");
            Assert.Equal(ProcessPriorityClass.Idle, config.Priority);

            config = XmlServiceConfig.FromXml("<service><id>test</id></service>");
            Assert.Equal(ProcessPriorityClass.Normal, config.Priority);
        }

        [Fact]
        public void CanParseStopTimeout()
        {
            const string seedXml = "<service>"
                                   + "<stoptimeout>60sec</stoptimeout>"
                                   + "</service>";
            var config = XmlServiceConfig.FromXml(seedXml);

            Assert.Equal(TimeSpan.FromSeconds(60), config.StopTimeout);
        }

        [Fact]
        public void CanParseStopTimeoutFromMinutes()
        {
            const string seedXml = "<service>"
                                   + "<stoptimeout>10min</stoptimeout>"
                                   + "</service>";
            var config = XmlServiceConfig.FromXml(seedXml);

            Assert.Equal(TimeSpan.FromMinutes(10), config.StopTimeout);
        }

        [Fact]
        public void CanParseLogname()
        {
            const string seedXml = "<service>"
                                   + "<logname>MyTestApp</logname>"
                                   + "</service>";
            var config = XmlServiceConfig.FromXml(seedXml);

            Assert.Equal("MyTestApp", config.LogName);
        }

        [Fact]
        public void CanParseOutfileDisabled()
        {
            const string seedXml = "<service>"
                                   + "<outfiledisabled>true</outfiledisabled>"
                                   + "</service>";
            var config = XmlServiceConfig.FromXml(seedXml);

            Assert.True(config.OutFileDisabled);
        }

        [Fact]
        public void CanParseErrfileDisabled()
        {
            const string seedXml = "<service>"
                                   + "<errfiledisabled>true</errfiledisabled>"
                                   + "</service>";
            var config = XmlServiceConfig.FromXml(seedXml);

            Assert.True(config.ErrFileDisabled);
        }

        [Fact]
        public void CanParseOutfilePattern()
        {
            const string seedXml = "<service>"
                                   + "<outfilepattern>.out.test.log</outfilepattern>"
                                   + "</service>";
            var config = XmlServiceConfig.FromXml(seedXml);

            Assert.Equal(".out.test.log", config.OutFilePattern);
        }

        [Fact]
        public void CanParseErrfilePattern()
        {
            const string seedXml = "<service>"
                                   + "<errfilepattern>.err.test.log</errfilepattern>"
                                   + "</service>";
            var config = XmlServiceConfig.FromXml(seedXml);

            Assert.Equal(".err.test.log", config.ErrFilePattern);
        }

        [Fact]
        public void LogModeRollBySize()
        {
            const string seedXml = "<service>"
                                   + "<logpath>c:\\</logpath>"
                                   + "<log mode=\"roll-by-size\">"
                                   + "<sizeThreshold>112</sizeThreshold>"
                                   + "<keepFiles>113</keepFiles>"
                                   + "</log>"
                                   + "</service>";

            var config = XmlServiceConfig.FromXml(seedXml);
            config.BaseName = "service";

            var logHandler = config.LogHandler as SizeBasedRollingLogAppender;
            Assert.NotNull(logHandler);
            Assert.Equal(112 * 1024, logHandler.SizeThreshold);
            Assert.Equal(113, logHandler.FilesToKeep);
        }

        [Fact]
        public void LogModeRollByTime()
        {
            const string seedXml = "<service>"
                                   + "<logpath>c:\\</logpath>"
                                   + "<log mode=\"roll-by-time\">"
                                   + "<period>7</period>"
                                   + "<pattern>log pattern</pattern>"
                                   + "</log>"
                                   + "</service>";

            var config = XmlServiceConfig.FromXml(seedXml);
            config.BaseName = "service";

            var logHandler = config.LogHandler as TimeBasedRollingLogAppender;
            Assert.NotNull(logHandler);
            Assert.Equal(7, logHandler.Period);
            Assert.Equal("log pattern", logHandler.Pattern);
        }

        [Fact]
        public void LogModeRollBySizeTime()
        {
            const string seedXml = "<service>"
                                   + "<logpath>c:\\</logpath>"
                                   + "<log mode=\"roll-by-size-time\">"
                                   + "<sizeThreshold>10240</sizeThreshold>"
                                   + "<pattern>yyyy-MM-dd</pattern>"
                                   + "<autoRollAtTime>00:00:00</autoRollAtTime>"
                                   + "</log>"
                                   + "</service>";

            var config = XmlServiceConfig.FromXml(seedXml);
            config.BaseName = "service";

            var logHandler = config.LogHandler as RollingSizeTimeLogAppender;
            Assert.NotNull(logHandler);
            Assert.Equal(10240 * 1024, logHandler.SizeThreshold);
            Assert.Equal("yyyy-MM-dd", logHandler.FilePattern);
            Assert.Equal((TimeSpan?)new TimeSpan(0, 0, 0), logHandler.AutoRollAtTime);
        }

        [Fact]
        public void VerifyServiceLogonRightGraceful()
        {
            const string seedXml = "<service>"
                                   + "<serviceaccount>"
                                   + "<domain>" + Domain + "</domain>"
                                   + "<user>" + Username + "</user>"
                                   + "<password>" + Password + "</password>"
                                   + "<allowservicelogon>true1</allowservicelogon>"
                                   + "</serviceaccount>"
                                   + "</service>";
            var config = XmlServiceConfig.FromXml(seedXml);
            Assert.False(config.AllowServiceAcountLogonRight);
        }

        [Fact]
        public void VerifyServiceLogonRightOmitted()
        {
            const string seedXml = "<service>"
                                   + "<serviceaccount>"
                                   + "<domain>" + Domain + "</domain>"
                                   + "<user>" + Username + "</user>"
                                   + "<password>" + Password + "</password>"
                                   + "</serviceaccount>"
                                   + "</service>";
            var config = XmlServiceConfig.FromXml(seedXml);
            Assert.False(config.AllowServiceAcountLogonRight);
        }

        [Fact]
        public void VerifyResetFailureAfter()
        {
            var config = ConfigXmlBuilder.Create(this.output).WithTag("resetfailure", "75 sec").ToServiceConfig(true);
            Assert.Equal(TimeSpan.FromSeconds(75), config.ResetFailureAfter);
        }

        [Fact]
        public void VerifyStopTimeout()
        {
            var config = ConfigXmlBuilder.Create(this.output).WithTag("stoptimeout", "35 secs").ToServiceConfig(true);
            Assert.Equal(TimeSpan.FromSeconds(35), config.StopTimeout);
        }

        /// <summary>
        /// https://github.com/kohsuke/winsw/issues/178
        /// </summary>
        [Fact]
        public void Arguments_LegacyParam()
        {
            var config = ConfigXmlBuilder.Create(this.output).WithTag("arguments", "arg").ToServiceConfig(true);
            Assert.Equal("arg", config.Arguments);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DelayedStart_RoundTrip(bool enabled)
        {
            var bldr = ConfigXmlBuilder.Create(this.output);
            if (enabled)
            {
                bldr = bldr.WithDelayedAutoStart();
            }

            var config = bldr.ToServiceConfig();
            Assert.Equal(enabled, config.DelayedAutoStart);
        }

        [Fact]
        public void Additional_Executable_And_Arguments()
        {
            const string prestartExecutable = "1";
            const string prestartArguments = "2";
            const string poststartExecutable = "3";
            const string poststartArguments = "4";
            const string prestopExecutable = "5";
            const string prestopArguments = "6";
            const string poststopExecutable = "7";
            const string poststopArguments = "8";

            string seedXml =
$@"<service>
  <prestart>
    <executable>{prestartExecutable}</executable>
    <arguments>{prestartArguments}</arguments>
  </prestart>
  <poststart>
    <executable>{poststartExecutable}</executable>
    <arguments>{poststartArguments}</arguments>
  </poststart>
  <prestop>
    <executable>{prestopExecutable}</executable>
    <arguments>{prestopArguments}</arguments>
  </prestop>
  <poststop>
    <executable>{poststopExecutable}</executable>
    <arguments>{poststopArguments}</arguments>
  </poststop>
</service>";

            XmlServiceConfig config = XmlServiceConfig.FromXml(seedXml);

            Assert.Equal(prestartExecutable, config.PrestartExecutable);
            Assert.Equal(prestartArguments, config.PrestartArguments);
            Assert.Equal(poststartExecutable, config.PoststartExecutable);
            Assert.Equal(poststartArguments, config.PoststartArguments);
            Assert.Equal(prestopExecutable, config.PrestopExecutable);
            Assert.Equal(prestopArguments, config.PrestopArguments);
            Assert.Equal(poststopExecutable, config.PoststopExecutable);
            Assert.Equal(poststopArguments, config.PoststopArguments);
        }
    }
}
