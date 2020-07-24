using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using WinSW.Tests.Util;
using Xunit;
using Xunit.Abstractions;

namespace WinSW.Tests
{
    public class ServiceDescriptorTests
    {
        private const string ExpectedWorkingDirectory = @"Z:\Path\SubPath";
        private const string Username = "User";
        private const string Password = "Password";
        private const string Domain = "Domain";
        private const string AllowServiceAccountLogonRight = "true";

        private readonly ITestOutputHelper output;
        
        private ServiceDescriptor extendedServiceDescriptor;

        public ServiceDescriptorTests(ITestOutputHelper output)
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
            this.extendedServiceDescriptor = ServiceDescriptor.FromXml(seedXml);
        }

        [Fact]
        public void DefaultStartMode()
        {
            Assert.Equal(ServiceStartMode.Automatic, this.extendedServiceDescriptor.StartMode);
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

            this.extendedServiceDescriptor = ServiceDescriptor.FromXml(seedXml);
            _ = Assert.Throws<InvalidDataException>(() => this.extendedServiceDescriptor.StartMode);
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

            this.extendedServiceDescriptor = ServiceDescriptor.FromXml(seedXml);
            Assert.Equal(ServiceStartMode.Manual, this.extendedServiceDescriptor.StartMode);
        }

        [Fact]
        public void VerifyWorkingDirectory()
        {
            Debug.WriteLine("_extendedServiceDescriptor.WorkingDirectory :: " + this.extendedServiceDescriptor.WorkingDirectory);
            Assert.Equal(ExpectedWorkingDirectory, this.extendedServiceDescriptor.WorkingDirectory);
        }

        [Fact]
        public void VerifyServiceLogonRight()
        {
            Assert.True(this.extendedServiceDescriptor.AllowServiceAcountLogonRight);
        }

        [Fact]
        public void VerifyUsername()
        {
            Debug.WriteLine("_extendedServiceDescriptor.WorkingDirectory :: " + this.extendedServiceDescriptor.WorkingDirectory);
            Assert.Equal(Domain + "\\" + Username, this.extendedServiceDescriptor.ServiceAccountUserName);
        }

        [Fact]
        public void VerifyPassword()
        {
            Debug.WriteLine("_extendedServiceDescriptor.WorkingDirectory :: " + this.extendedServiceDescriptor.WorkingDirectory);
            Assert.Equal(Password, this.extendedServiceDescriptor.ServiceAccountPassword);
        }

        [Fact]
        public void Priority()
        {
            var sd = ServiceDescriptor.FromXml("<service><id>test</id><priority>normal</priority></service>");
            Assert.Equal(ProcessPriorityClass.Normal, sd.Priority);

            sd = ServiceDescriptor.FromXml("<service><id>test</id><priority>idle</priority></service>");
            Assert.Equal(ProcessPriorityClass.Idle, sd.Priority);

            sd = ServiceDescriptor.FromXml("<service><id>test</id></service>");
            Assert.Equal(ProcessPriorityClass.Normal, sd.Priority);
        }

        [Fact]
        public void CanParseStopTimeout()
        {
            const string seedXml = "<service>"
                                   + "<stoptimeout>60sec</stoptimeout>"
                                   + "</service>";
            var serviceDescriptor = ServiceDescriptor.FromXml(seedXml);

            Assert.Equal(TimeSpan.FromSeconds(60), serviceDescriptor.StopTimeout);
        }

        [Fact]
        public void CanParseStopTimeoutFromMinutes()
        {
            const string seedXml = "<service>"
                                   + "<stoptimeout>10min</stoptimeout>"
                                   + "</service>";
            var serviceDescriptor = ServiceDescriptor.FromXml(seedXml);

            Assert.Equal(TimeSpan.FromMinutes(10), serviceDescriptor.StopTimeout);
        }

        [Fact]
        public void CanParseLogname()
        {
            const string seedXml = "<service>"
                                   + "<logname>MyTestApp</logname>"
                                   + "</service>";
            var serviceDescriptor = ServiceDescriptor.FromXml(seedXml);

            Assert.Equal("MyTestApp", serviceDescriptor.LogName);
        }

        [Fact]
        public void CanParseOutfileDisabled()
        {
            const string seedXml = "<service>"
                                   + "<outfiledisabled>true</outfiledisabled>"
                                   + "</service>";
            var serviceDescriptor = ServiceDescriptor.FromXml(seedXml);

            Assert.True(serviceDescriptor.OutFileDisabled);
        }

        [Fact]
        public void CanParseErrfileDisabled()
        {
            const string seedXml = "<service>"
                                   + "<errfiledisabled>true</errfiledisabled>"
                                   + "</service>";
            var serviceDescriptor = ServiceDescriptor.FromXml(seedXml);

            Assert.True(serviceDescriptor.ErrFileDisabled);
        }

        [Fact]
        public void CanParseOutfilePattern()
        {
            const string seedXml = "<service>"
                                   + "<outfilepattern>.out.test.log</outfilepattern>"
                                   + "</service>";
            var serviceDescriptor = ServiceDescriptor.FromXml(seedXml);

            Assert.Equal(".out.test.log", serviceDescriptor.OutFilePattern);
        }

        [Fact]
        public void CanParseErrfilePattern()
        {
            const string seedXml = "<service>"
                                   + "<errfilepattern>.err.test.log</errfilepattern>"
                                   + "</service>";
            var serviceDescriptor = ServiceDescriptor.FromXml(seedXml);

            Assert.Equal(".err.test.log", serviceDescriptor.ErrFilePattern);
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

            var serviceDescriptor = ServiceDescriptor.FromXml(seedXml);
            serviceDescriptor.BaseName = "service";

            var logHandler = serviceDescriptor.LogHandler as SizeBasedRollingLogAppender;
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

            var serviceDescriptor = ServiceDescriptor.FromXml(seedXml);
            serviceDescriptor.BaseName = "service";

            var logHandler = serviceDescriptor.LogHandler as TimeBasedRollingLogAppender;
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

            var serviceDescriptor = ServiceDescriptor.FromXml(seedXml);
            serviceDescriptor.BaseName = "service";

            var logHandler = serviceDescriptor.LogHandler as RollingSizeTimeLogAppender;
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
            var serviceDescriptor = ServiceDescriptor.FromXml(seedXml);
            Assert.False(serviceDescriptor.AllowServiceAcountLogonRight);
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
            var serviceDescriptor = ServiceDescriptor.FromXml(seedXml);
            Assert.False(serviceDescriptor.AllowServiceAcountLogonRight);
        }

        [Fact]
        public void VerifyResetFailureAfter()
        {
            var sd = ConfigXmlBuilder.Create(this.output).WithTag("resetfailure", "75 sec").ToServiceDescriptor(true);
            Assert.Equal(TimeSpan.FromSeconds(75), sd.ResetFailureAfter);
        }

        [Fact]
        public void VerifyStopTimeout()
        {
            var sd = ConfigXmlBuilder.Create(this.output).WithTag("stoptimeout", "35 secs").ToServiceDescriptor(true);
            Assert.Equal(TimeSpan.FromSeconds(35), sd.StopTimeout);
        }

        /// <summary>
        /// https://github.com/kohsuke/winsw/issues/178
        /// </summary>
        [Fact]
        public void Arguments_LegacyParam()
        {
            var sd = ConfigXmlBuilder.Create(this.output).WithTag("arguments", "arg").ToServiceDescriptor(true);
            Assert.Equal("arg", sd.Arguments);
        }

        [Fact]
        public void Arguments_NewParam_Single()
        {
            var sd = ConfigXmlBuilder.Create(this.output)
                .WithTag("argument", "--arg1=2")
                .ToServiceDescriptor(true);
            Assert.Equal(" --arg1=2", sd.Arguments);
        }

        [Fact]
        public void Arguments_NewParam_MultipleArgs()
        {
            var sd = ConfigXmlBuilder.Create(this.output)
                .WithTag("argument", "--arg1=2")
                .WithTag("argument", "--arg2=123")
                .WithTag("argument", "--arg3=null")
                .ToServiceDescriptor(true);
            Assert.Equal(" --arg1=2 --arg2=123 --arg3=null", sd.Arguments);
        }

        /// <summary>
        /// Ensures that the new single-argument field has a higher priority.
        /// </summary>
        [Fact]
        public void Arguments_Bothparam_Priorities()
        {
            var sd = ConfigXmlBuilder.Create(this.output)
                .WithTag("arguments", "--arg1=2 --arg2=3")
                .WithTag("argument", "--arg2=123")
                .WithTag("argument", "--arg3=null")
                .ToServiceDescriptor(true);
            Assert.Equal(" --arg2=123 --arg3=null", sd.Arguments);
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

            var sd = bldr.ToServiceDescriptor();
            Assert.Equal(enabled, sd.DelayedAutoStart);
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

            ServiceDescriptor descriptor = ServiceDescriptor.FromXml(seedXml);

            Assert.Equal(prestartExecutable, descriptor.PrestartExecutable);
            Assert.Equal(prestartArguments, descriptor.PrestartArguments);
            Assert.Equal(poststartExecutable, descriptor.PoststartExecutable);
            Assert.Equal(poststartArguments, descriptor.PoststartArguments);
            Assert.Equal(prestopExecutable, descriptor.PrestopExecutable);
            Assert.Equal(prestopArguments, descriptor.PrestopArguments);
            Assert.Equal(poststopExecutable, descriptor.PoststopExecutable);
            Assert.Equal(poststopArguments, descriptor.PoststopArguments);
        }
    }
}
