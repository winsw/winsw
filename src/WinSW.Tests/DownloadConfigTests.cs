using System.IO;
using NUnit.Framework;
using WinSW;
using winswTests.Util;

namespace winswTests
{
    [TestFixture]
    public class DownloadConfigTests
    {
        private const string From = "https://www.nosuchhostexists.foo.myorg/foo.xml";
        private const string To = "%BASE%\\foo.xml";

        [Test]
        public void Roundtrip_Defaults()
        {
            // Roundtrip data
            var d = new Download(From, To);
            var sd = ConfigXmlBuilder.create()
                .WithDownload(d)
                .ToServiceDescriptor(true);
            var loaded = this.GetSingleEntry(sd);

            // Check default values
            Assert.That(loaded.FailOnError, Is.False);
            Assert.That(loaded.Auth, Is.EqualTo(Download.AuthType.None));
            Assert.That(loaded.Username, Is.Null);
            Assert.That(loaded.Password, Is.Null);
            Assert.That(loaded.UnsecureAuth, Is.False);
        }

        [Test]
        public void Roundtrip_BasicAuth()
        {
            // Roundtrip data
            var d = new Download(From, To, true, Download.AuthType.Basic, "aUser", "aPassword", true);
            var sd = ConfigXmlBuilder.create()
                .WithDownload(d)
                .ToServiceDescriptor(true);
            var loaded = this.GetSingleEntry(sd);

            // Check default values
            Assert.That(loaded.FailOnError, Is.True);
            Assert.That(loaded.Auth, Is.EqualTo(Download.AuthType.Basic));
            Assert.That(loaded.Username, Is.EqualTo("aUser"));
            Assert.That(loaded.Password, Is.EqualTo("aPassword"));
            Assert.That(loaded.UnsecureAuth, Is.True);
        }

        [Test]
        public void Roundtrip_SSPI()
        {
            // Roundtrip data
            var d = new Download(From, To, false, Download.AuthType.Sspi);
            var sd = ConfigXmlBuilder.create()
                .WithDownload(d)
                .ToServiceDescriptor(true);
            var loaded = this.GetSingleEntry(sd);

            // Check default values
            Assert.That(loaded.FailOnError, Is.False);
            Assert.That(loaded.Auth, Is.EqualTo(Download.AuthType.Sspi));
            Assert.That(loaded.Username, Is.Null);
            Assert.That(loaded.Password, Is.Null);
            Assert.That(loaded.UnsecureAuth, Is.False);
        }

        [TestCase("http://")]
        [TestCase("ftp://")]
        [TestCase("file://")]
        [TestCase("jar://")]
        [TestCase("\\\\")] // UNC
        public void RejectBasicAuth_With_UnsecureProtocol(string protocolPrefix)
        {
            string unsecureFrom = protocolPrefix + "myServer.com:8080/file.txt";
            var d = new Download(unsecureFrom, To, auth: Download.AuthType.Basic, username: "aUser", password: "aPassword");
            this.AssertInitializationFails(d, "Warning: you're sending your credentials in clear text to the server");
        }

        [Test]
        public void RejectBasicAuth_Without_Username()
        {
            var d = new Download(From, To, auth: Download.AuthType.Basic, username: null, password: "aPassword");
            this.AssertInitializationFails(d, "Basic Auth is enabled, but username is not specified");
        }

        [Test]
        public void RejectBasicAuth_Without_Password()
        {
            var d = new Download(From, To, auth: Download.AuthType.Basic, username: "aUser", password: null);
            this.AssertInitializationFails(d, "Basic Auth is enabled, but password is not specified");
        }

        /// <summary>
        /// Ensures that the fail-on-error field is being processed correctly.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void Download_FailOnError(bool failOnError)
        {
            var d = new Download(From, To, failOnError);

            var sd = ConfigXmlBuilder.create()
                .WithDownload(d)
                .ToServiceDescriptor(true);

            var loaded = this.GetSingleEntry(sd);
            Assert.That(loaded.From, Is.EqualTo(From));
            Assert.That(loaded.To, Is.EqualTo(To));
            Assert.That(loaded.FailOnError, Is.EqualTo(failOnError), "Unexpected FailOnError value");
        }

        /// <summary>
        /// Ensures that the fail-on-error field is being processed correctly.
        /// </summary>
        [Test]
        public void Download_FailOnError_Undefined()
        {
            var sd = ConfigXmlBuilder.create()
                .WithRawEntry("<download from=\"http://www.nosuchhostexists.foo.myorg/foo.xml\" to=\"%BASE%\\foo.xml\"/>")
                .ToServiceDescriptor(true);

            var loaded = this.GetSingleEntry(sd);
            Assert.That(loaded.FailOnError, Is.False);
        }

        [TestCase("sspi")]
        [TestCase("SSPI")]
        [TestCase("SsPI")]
        [TestCase("Sspi")]
        public void AuthType_Is_CaseInsensitive(string authType)
        {
            var sd = ConfigXmlBuilder.create()
                    .WithRawEntry("<download from=\"http://www.nosuchhostexists.foo.myorg/foo.xml\" to=\"%BASE%\\foo.xml\" auth=\"" + authType + "\"/>")
                    .ToServiceDescriptor(true);
            var loaded = this.GetSingleEntry(sd);
            Assert.That(loaded.Auth, Is.EqualTo(Download.AuthType.Sspi));
        }

        [Test]
        public void Should_Fail_On_Unsupported_AuthType()
        {
            // TODO: will need refactoring once all fields are being parsed on startup
            var sd = ConfigXmlBuilder.create()
                    .WithRawEntry("<download from=\"http://www.nosuchhostexists.foo.myorg/foo.xml\" to=\"%BASE%\\foo.xml\" auth=\"digest\"/>")
                    .ToServiceDescriptor(true);

            Assert.That(() => this.GetSingleEntry(sd), Throws.TypeOf<InvalidDataException>().With.Message.StartsWith("Cannot parse <auth> Enum value from string 'digest'"));
        }

        [TestCase("http://", "127.0.0.1:80", "egarcia", "Passw0rd")]
        [TestCase("https://", "myurl.com.co:2298", "MyUsername", "P@ssw:rd")]
        [TestCase("http://", "192.168.0.8:3030")]
        public void Proxy_Credentials(string protocol, string address, string username = null, string password = null)
        {
            CustomProxyInformation cpi;
            if (string.IsNullOrEmpty(username))
            {
                cpi = new CustomProxyInformation(protocol + address + "/");
            }
            else
            {
                cpi = new CustomProxyInformation(protocol + username + ":" + password + "@" + address + "/");
            }

            Assert.That(cpi.ServerAddress, Is.EqualTo(protocol + address + "/"));

            if (string.IsNullOrEmpty(username))
            {
                Assert.IsNull(cpi.Credentials);
            }
            else
            {
                Assert.IsNotNull(cpi.Credentials);
                Assert.That(cpi.Credentials.UserName, Is.EqualTo(username));
                Assert.That(cpi.Credentials.Password, Is.EqualTo(password));
            }
        }

        private Download GetSingleEntry(XmlServiceConfig sd)
        {
            var downloads = sd.Downloads.ToArray();
            Assert.That(downloads.Length, Is.EqualTo(1), "Service Descriptor is expected to have only one entry");
            return downloads[0];
        }

        private void AssertInitializationFails(Download download, string expectedMessagePart = null)
        {
            var sd = ConfigXmlBuilder.create()
                .WithDownload(download)
                .ToServiceDescriptor(true);

            Assert.That(() => this.GetSingleEntry(sd), Throws.TypeOf<InvalidDataException>().With.Message.StartsWith(expectedMessagePart));
        }
    }
}
