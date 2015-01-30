﻿using NUnit.Framework;
using winsw;
using System.Diagnostics;
using System.Xml;

namespace winswTests
{
    using System;

    [TestFixture]
    public class ServiceDescriptorTests
    {

        private ServiceDescriptor extendedServiceDescriptor;

        private const string ExpectedWorkingDirectory = @"Z:\Path\SubPath";
        private const string Username = "User";
        private const string Password = "Password";
        private const string Domain = "Domain";
        private const string AllowServiceAccountLogonRight = "true";

        [SetUp]
        public void SetUp()
        {
            const string SeedXml = "<service>"
                                   + "<id>service.exe</id>"
                                   + "<name>Service</name>"
                                   + "<description>The service.</description>"
                                   + "<executable>node.exe</executable>"
                                   + "<arguments>My Arguments</arguments>"
                                   + "<logmode>rotate</logmode>"
                                   + "<serviceaccount>"
                                   +   "<domain>" + Domain + "</domain>"
                                   +   "<user>" + Username + "</user>"
                                   +   "<password>" + Password + "</password>"
                                   + "<allowservicelogon>" + AllowServiceAccountLogonRight + "</allowservicelogon>"
                                   + "</serviceaccount>"
                                   + "<workingdirectory>"
                                   + ExpectedWorkingDirectory
                                   + "</workingdirectory>"
                                   + @"<logpath>C:\logs</logpath>"
                                   + "</service>";
            extendedServiceDescriptor = ServiceDescriptor.FromXML(SeedXml);
        }

        [Test]
        public void VerifyWorkingDirectory()
        {
            System.Diagnostics.Debug.WriteLine("_extendedServiceDescriptor.WorkingDirectory :: " + extendedServiceDescriptor.WorkingDirectory);
            Assert.That(extendedServiceDescriptor.WorkingDirectory, Is.EqualTo(ExpectedWorkingDirectory));
        }

        [Test]
        public void VerifyServiceLogonRight()
        {
            Assert.That(extendedServiceDescriptor.AllowServiceAcountLogonRight, Is.EqualTo(true));
        }

        [Test]
        public void VerifyUsername()
        {
            System.Diagnostics.Debug.WriteLine("_extendedServiceDescriptor.WorkingDirectory :: " + extendedServiceDescriptor.WorkingDirectory);
            Assert.That(extendedServiceDescriptor.ServiceAccountUser, Is.EqualTo(Domain + "\\" + Username));
        }

        [Test]
        public void VerifyPassword()
        {
            System.Diagnostics.Debug.WriteLine("_extendedServiceDescriptor.WorkingDirectory :: " + extendedServiceDescriptor.WorkingDirectory);
            Assert.That(extendedServiceDescriptor.ServiceAccountPassword, Is.EqualTo(Password));
        }

        [Test]
        public void Priority()
        {
            var sd = ServiceDescriptor.FromXML("<service><id>test</id><priority>normal</priority></service>");
            Assert.That(sd.Priority, Is.EqualTo(ProcessPriorityClass.Normal));

            sd = ServiceDescriptor.FromXML("<service><id>test</id><priority>idle</priority></service>");
            Assert.That(sd.Priority, Is.EqualTo(ProcessPriorityClass.Idle));

            sd = ServiceDescriptor.FromXML("<service><id>test</id></service>");
            Assert.That(sd.Priority, Is.EqualTo(ProcessPriorityClass.Normal));
        }

        [Test]
        public void StopParentProcessFirstIsFalseByDefault()
        {
            Assert.False(extendedServiceDescriptor.StopParentProcessFirst);
        }

        [Test]
        public void CanParseStopParentProcessFirst()
        {
            const string SeedXml =   "<service>"
                                   +    "<stopparentprocessfirst>true</stopparentprocessfirst>"
                                   + "</service>";
            var serviceDescriptor = ServiceDescriptor.FromXML(SeedXml);

            Assert.True(serviceDescriptor.StopParentProcessFirst);
        }

        [Test]
        public void CanParseStopTimeout()
        {
            const string SeedXml =   "<service>"
                                   +    "<stoptimeout>60sec</stoptimeout>"
                                   + "</service>";
            var serviceDescriptor = ServiceDescriptor.FromXML(SeedXml);

            Assert.That(serviceDescriptor.StopTimeout, Is.EqualTo(TimeSpan.FromSeconds(60)));
        }

        [Test]
        public void CanParseStopTimeoutFromMinutes()
        {
            const string SeedXml =   "<service>"
                                   +    "<stoptimeout>10min</stoptimeout>"
                                   + "</service>";
            var serviceDescriptor = ServiceDescriptor.FromXML(SeedXml);

            Assert.That(serviceDescriptor.StopTimeout, Is.EqualTo(TimeSpan.FromMinutes(10)));
        }   
        
        [Test]
        public void LogModeRollBySize()
        {
            const string SeedXml =   "<service>"
                                   + "<logpath>c:\\</logpath>"
                                   + "<log mode=\"roll-by-size\">"
                                   +    "<sizeThreshold>112</sizeThreshold>"
                                   +    "<keepFiles>113</keepFiles>"
                                   + "</log>"
                                   + "</service>";
            
            var serviceDescriptor = ServiceDescriptor.FromXML(SeedXml);
            serviceDescriptor.BaseName = "service";

            var logHandler = serviceDescriptor.LogHandler as SizeBasedRollingLogAppender;
            Assert.NotNull(logHandler);
            Assert.That(logHandler.SizeTheshold, Is.EqualTo(112 * 1024));
            Assert.That(logHandler.FilesToKeep, Is.EqualTo(113));
        }

        [Test]
        public void LogModeRollByTime()
        {
            const string SeedXml = "<service>"
                                   + "<logpath>c:\\</logpath>"
                                   + "<log mode=\"roll-by-time\">"
                                   +    "<period>7</period>"
                                   +    "<pattern>log pattern</pattern>"
                                   + "</log>"
                                   + "</service>";

            var serviceDescriptor = ServiceDescriptor.FromXML(SeedXml);
            serviceDescriptor.BaseName = "service";

            var logHandler = serviceDescriptor.LogHandler as TimeBasedRollingLogAppender;
            Assert.NotNull(logHandler);
            Assert.That(logHandler.Period, Is.EqualTo(7));
            Assert.That(logHandler.Pattern, Is.EqualTo("log pattern"));
        }

        [Test]
        public void VerifyServiceLogonRightGraceful()
        {
            const string SeedXml="<service>"
                                   + "<serviceaccount>"
                                   +   "<domain>" + Domain + "</domain>"
                                   +   "<user>" + Username + "</user>"
                                   +   "<password>" + Password + "</password>"
                                   + "<allowservicelogon>true1</allowservicelogon>"
                                   +  "</serviceaccount>"
                                   + "</service>";
            var serviceDescriptor = ServiceDescriptor.FromXML(SeedXml);
            Assert.That(serviceDescriptor.AllowServiceAcountLogonRight, Is.EqualTo(false));
        }
        [Test]
        public void VerifyServiceLogonRightOmitted()
        {
            const string SeedXml = "<service>"
                                   + "<serviceaccount>"
                                   + "<domain>" + Domain + "</domain>"
                                   + "<user>" + Username + "</user>"
                                   + "<password>" + Password + "</password>"
                                   + "</serviceaccount>"
                                   + "</service>";
            var serviceDescriptor = ServiceDescriptor.FromXML(SeedXml);
            Assert.That(serviceDescriptor.AllowServiceAcountLogonRight, Is.EqualTo(false));
        }
    }
}
