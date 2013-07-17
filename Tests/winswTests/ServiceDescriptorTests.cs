using NUnit.Framework;
using winsw;

namespace winswTests
{

    public class ServiceDescriptorExtended : ServiceDescriptor
    {

        public ServiceDescriptorExtended(string descriptorXml)
        {
            LoadTestXml(descriptorXml);
        }

        private void LoadTestXml(string xml)
        {
            dom.LoadXml(xml);
        }
    }


    [TestFixture]
    public class ServiceDescriptorTests
    {

        private ServiceDescriptorExtended extendedServiceDescriptor;

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

            extendedServiceDescriptor = new ServiceDescriptorExtended(SeedXml);
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
    }
}
