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
			Dom.LoadXml(xml);
		}
	}


	[TestFixture]
	public class ServiceDescriptorTests
	{

		private ServiceDescriptorExtended extendedServiceDescriptor;

		private const string ExpectedWorkingDirectory = @"Z:\Path\Holding\Secret\World\Domination\Plans";
        private const string Username = @"User";
        private const string Password = @"Password";
        private const string Domain = @"Domain";

		[SetUp]
		public void SetUp()
		{
			
			var seedXml = "<service>"
				+ "<id>service.exe</id>"
				+ "<name>Service</name>"
				+ "<description>The service.</description>"
				+ "<executable>node.exe</executable>"
				+ "<arguments>My Arguments</arguments>"
				+ "<logmode>rotate</logmode>"
                + "<serviceaccount domain=\"Domain\" user=\"" + Username + "\" password=\"" + Password + "\"/>"
				+ @"<workingdirectory>"
				+ ExpectedWorkingDirectory
				+ "</workingdirectory>"
				+ @"<logpath>C:\logs</logpath>"
				+ "</service>";

			System.Diagnostics.Debug.WriteLine(seedXml);

			extendedServiceDescriptor = new ServiceDescriptorExtended(seedXml);
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
