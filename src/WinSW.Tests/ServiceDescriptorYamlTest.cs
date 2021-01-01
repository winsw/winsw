using System;
using System.Diagnostics;
using System.ServiceProcess;
using NUnit.Framework;
using WinSW;
using WinSW.Configuration;

namespace winswTests
{
    class ServiceDescriptorYamlTest
    {
        private IServiceConfig _extendedServiceDescriptor;

        private const string ExpectedWorkingDirectory = @"Z:\Path\SubPath";
        private const string Username = "User";
        private const string Password = "Password";
        private const string Domain = "Domain";
        private const string AllowServiceAccountLogonRight = "true";


        [SetUp]
        public void SetUp()
        {
            string yaml = $@"
id: service.exe
name: Service
description: The Service.
executable: node.exe
arguments: My Arguments
log:
    mode: roll
    logpath: c:\logs
serviceAccount:
    domain: {Domain}
    user: {Username}
    password: {Password}
    allowServiceLogon: {AllowServiceAccountLogonRight}
workingDirectory: {ExpectedWorkingDirectory}";

            this._extendedServiceDescriptor = YamlServiceConfig.FromYaml(yaml);
        }

        [Test]
        public void DefaultStartMode()
        {
            Assert.That(this._extendedServiceDescriptor.StartMode, Is.EqualTo(ServiceStartMode.Automatic));
        }

        [Test]
        public void IncorrectStartMode()
        {
            string yaml = $@"
id: service.exe
name: Service
description: The Service.
executable: node.exe
arguments: My Arguments
startMode: roll";

            this._extendedServiceDescriptor = YamlServiceConfig.FromYaml(yaml);
            Assert.That(() => this._extendedServiceDescriptor.StartMode, Throws.ArgumentException);
        }

        [Test]
        public void ChangedStartMode()
        {
            string yaml = $@"
id: service.exe
name: Service
description: The Service.
executable: node.exe
arguments: My Arguments
startMode: manual";

            this._extendedServiceDescriptor = YamlServiceConfig.FromYaml(yaml);
            Assert.That(this._extendedServiceDescriptor.StartMode, Is.EqualTo(ServiceStartMode.Manual));
        }

        [Test]
        public void VerifyWorkingDirectory()
        {
            Assert.That(this._extendedServiceDescriptor.WorkingDirectory, Is.EqualTo(ExpectedWorkingDirectory));
        }

        [Test]
        public void VerifyServiceLogonRight()
        {
            Assert.That(_extendedServiceDescriptor.ServiceAccount.AllowServiceLogonRight, Is.True);
        }

        [Test]
        public void VerifyUsername()
        {
            Assert.That(_extendedServiceDescriptor.ServiceAccount.FullUser, Is.EqualTo(Domain + "\\" + Username));
        }

        [Test]
        public void VerifyPassword()
        {
            Assert.That(_extendedServiceDescriptor.ServiceAccount.Password, Is.EqualTo(Password));
        }

        [Test]
        public void Priority()
        {
            var sd = YamlServiceConfig.FromYaml(@"
id: service.exe
name: Service
description: The Service.
executable: node.exe
priority: normal");
            Assert.That(sd.Priority, Is.EqualTo(ProcessPriorityClass.Normal));

            sd = YamlServiceConfig.FromYaml(@"
id: service.exe
name: Service
description: The Service.
executable: node.exe
priority: idle");
            Assert.That(sd.Priority, Is.EqualTo(ProcessPriorityClass.Idle));

            sd = YamlServiceConfig.FromYaml(@"
id: service.exe
name: Service
description: The Service.
executable: node.exe");
            Assert.That(sd.Priority, Is.EqualTo(ProcessPriorityClass.Normal));
        }

        [Test]
        public void StopParentProcessFirstIsTrueByDefault()
        {
            Assert.That(this._extendedServiceDescriptor.StopParentProcessFirst, Is.True);
        }

        [Test]
        public void CanParseStopParentProcessFirst()
        {
            const string yaml = @"
id: service.exe
name: Service
description: The Service.
executable: node.exe
stopParentProcessFirst: false";
            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.StopParentProcessFirst, Is.False);
        }

        [Test]
        public void CanParseStopTimeout()
        {
            const string yaml = @"id: service.exe
name: Service
description: The Service.
executable: node.exe
stopTimeout: 60sec";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.StopTimeout, Is.EqualTo(TimeSpan.FromSeconds(60)));
        }

        [Test]
        public void CanParseStopTimeoutFromMinutes()
        {
            const string yaml = @"
id: service.exe
name: Service
description: The Service.
executable: node.exe
stopTimeout: 10min";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.StopTimeout, Is.EqualTo(TimeSpan.FromMinutes(10)));
        }

        [Test]
        public void CanParseLogname()
        {
            const string yaml = @"
id: service.exe
name: Service
description: The Service.
executable: node.exe
log:
    name: MyTestApp";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.Log.Name, Is.EqualTo("MyTestApp"));
        }

        [Test]
        public void CanParseOutfileDisabled()
        {
            const string yaml = @"
id: service.exe
name: Service
description: The Service.
executable: node.exe
log:
    outFileDisabled: true";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.Log.OutFileDisabled, Is.True);
        }

        [Test]
        public void CanParseErrfileDisabled()
        {
            const string yaml = @"
id: service.exe
name: Service
description: The Service.
executable: node.exe
log:
    errFileDisabled: true";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.Log.ErrFileDisabled, Is.True);
        }

        [Test]
        public void CanParseOutfilePattern()
        {
            const string yaml = @"
id: service.exe
name: Service
description: The Service.
executable: node.exe
log:
    outFilePattern: .out.test.log";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.Log.OutFilePattern, Is.EqualTo(".out.test.log"));
        }

        [Test]
        public void CanParseErrfilePattern()
        {
            const string yaml = @"
id: service.exe
name: Service
description: The Service.
executable: node.exe
log:
    errFilePattern: .err.test.log";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.Log.ErrFilePattern, Is.EqualTo(".err.test.log"));
        }

        [Test]
        public void LogModeRollBySize()
        {
            const string yaml = @"
id: service.exe
name: Service
description: The Service.
executable: node.exe
log:
    logpath: 'c:\\'
    mode: roll-by-size
    sizeThreshold: 112
    keepFiles: 113";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            serviceDescriptor.BaseName = "service";

            var logHandler = serviceDescriptor.Log.CreateLogHandler() as SizeBasedRollingLogAppender;
            Assert.That(logHandler, Is.Not.Null);
            Assert.That(logHandler.SizeThreshold, Is.EqualTo(112 * 1024));
            Assert.That(logHandler.FilesToKeep, Is.EqualTo(113));
        }

        [Test]
        public void LogModeRollByTime()
        {
            const string yaml = @"
id: service.exe
name: Service
description: The Service.
executable: node.exe
log:
    logpath: c:\\
    mode: roll-by-time
    period: 7
    pattern: log pattern";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            serviceDescriptor.BaseName = "service";

            var logHandler = serviceDescriptor.Log.CreateLogHandler() as TimeBasedRollingLogAppender;
            Assert.That(logHandler, Is.Not.Null);
            Assert.That(logHandler.Period, Is.EqualTo(7));
            Assert.That(logHandler.Pattern, Is.EqualTo("log pattern"));
        }

        [Test]
        public void LogModeRollBySizeTime()
        {
            const string yaml = @"
id: service.exe
name: Service
description: The Service.
executable: node.exe
log:
    logpath: c:\\
    mode: roll-by-size-time
    sizeThreshold: 10240
    pattern: yyyy-MM-dd
    autoRollAtTime: 00:00:00";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            serviceDescriptor.BaseName = "service";

            var logHandler = serviceDescriptor.Log.CreateLogHandler() as RollingSizeTimeLogAppender;
            Assert.That(logHandler, Is.Not.Null);
            Assert.That(logHandler.SizeThreshold, Is.EqualTo(10240 * 1024));
            Assert.That(logHandler.FilePattern, Is.EqualTo("yyyy-MM-dd"));
            Assert.That(logHandler.AutoRollAtTime, Is.EqualTo((TimeSpan?)new TimeSpan(0, 0, 0)));
        }

        [Test]
        public void VerifyServiceLogonRightGraceful()
        {
            string yaml = $@"
id: service.exe
name: Service
description: The Service.
executable: node.exe
serviceAccount:
    domain: {Domain}
    user: {Username}
    password: {Password}
    allowServiceLogon: false";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.ServiceAccount.AllowServiceLogonRight, Is.False);
        }

        [Test]
        public void VerifyServiceLogonRightOmitted()
        {
            string yaml = $@"
id: service.exe
name: Service
description: The Service.
executable: node.exe
serviceAccount:
    domain: {Domain}
    user: {Username}
    password: {Password}";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.ServiceAccount.AllowServiceLogonRight, Is.False);
        }

        [Test]
        public void VerifyResetFailureAfter()
        {
            string yaml = $@"
id: service.exe
name: Service
description: The Service.
executable: node.exe
resetFailure: 75 sec";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.ResetFailureAfter, Is.EqualTo(TimeSpan.FromSeconds(75)));
        }

        [Test]
        public void VerifyStopTimeout()
        {
            string yaml = $@"
id: service.exe
name: Service
description: The Service.
executable: node.exe
stopTimeout: 35 sec";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.StopTimeout, Is.EqualTo(TimeSpan.FromSeconds(35)));
        }

        [Test]
        public void Arguments_LegacyParam()
        {
            string yaml = $@"
id: service.exe
name: Service
description: The Service.
executable: node.exe
arguments: arg";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.Arguments, Is.EqualTo("arg"));
        }

        public void DelayedStart_RoundTrip(bool enabled)
        {
            string yaml = $@"
id: service.exe
name: Service
description: The Service.
executable: node.exe
delayedAutoStart: true";

            var serviceDescriptor = YamlServiceConfig.FromYaml(yaml);

            Assert.That(serviceDescriptor.DelayedAutoStart, Is.EqualTo(true));
        }


        [Test]
        public void Must_Specify_Values_Test()
        {
            string yml = @"
name: This is a test
executable: 'C:\Program Files\Java\jdk1.8.0_241\bin\java.exe'
description: This is test winsw";

            Assert.That(() =>
            {
                _ = YamlServiceConfig.FromYaml(yml).Name;
            }, Throws.TypeOf<InvalidOperationException>());
        }

    }
}
