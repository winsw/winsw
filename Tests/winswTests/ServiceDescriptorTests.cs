using NUnit.Framework;
using winsw;
using System.Diagnostics;
using System.Xml;

namespace winswTests
{
    [TestFixture]
    public class ServiceDescriptorTests
    {

        private ServiceDescriptor extendedServiceDescriptor;

        private const string ExpectedWorkingDirectory = @"Z:\Path\SubPath";
        private const string Username = "User";
        private const string Password = "Password";
        private const string Domain = "Domain";

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
    }
}
