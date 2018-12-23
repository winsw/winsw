using System.IO;
using System.Net;
#if VNEXT
using System.Threading.Tasks;
#endif
using NUnit.Framework;
using winsw;
using winswTests.Util;

namespace winswTests
{
    [TestFixture]
    class DownloadTest
    {
        private const string From = "https://www.nosuchhostexists.foo.myorg/foo.xml";
        private const string To = "%BASE%\\foo.xml";

        [Test]
#if VNEXT
        public async Task DownloadFileAsync()
#else
        public void DownloadFile()
#endif
        {
            string from = Path.GetTempFileName();
            string to = Path.GetTempFileName();

            try
            {
                const string contents = "WinSW";
                File.WriteAllText(from, contents);
#if VNEXT
                await new Download(from, to).PerformAsync();
#else
                new Download(from, to).Perform();
#endif
                Assert.That(File.ReadAllText(to), Is.EqualTo(contents));
            }
            finally
            {
                File.Delete(from);
                File.Delete(to);
            }
        }

        [Test]
        public void DownloadFile_NonExistent()
        {
            string from = Path.GetTempPath() + Path.GetRandomFileName();
            string to = Path.GetTempFileName();

            try
            {
                Assert.That(
#if VNEXT
                    async () => await new Download(from, to).PerformAsync(),
#else
                    () => new Download(from, to).Perform(),
#endif
                    Throws.TypeOf<WebException>());
            }
            finally
            {
                File.Delete(to);
            }
        }

        [Test]
        public void Roundtrip_Defaults()
        {
            // Roundtrip data
            Download d = new Download(From, To);
            var sd = ConfigXmlBuilder.create()
                .WithDownload(d)
                .ToServiceDescriptor(true);
            var loaded = GetSingleEntry(sd);

            // Check default values
            Assert.That(loaded.FailOnError, Is.False);
            Assert.That(loaded.Auth, Is.EqualTo(Download.AuthType.none));
            Assert.That(loaded.Username, Is.Null);
            Assert.That(loaded.Password, Is.Null);
            Assert.That(loaded.UnsecureAuth, Is.False);
        }

        [Test]
        public void Roundtrip_BasicAuth()
        {
            // Roundtrip data
            Download d = new Download(From, To, true, Download.AuthType.basic, "aUser", "aPassword", true);
            var sd = ConfigXmlBuilder.create()
                .WithDownload(d)
                .ToServiceDescriptor(true);
            var loaded = GetSingleEntry(sd);

            // Check default values
            Assert.That(loaded.FailOnError, Is.True);
            Assert.That(loaded.Auth, Is.EqualTo(Download.AuthType.basic));
            Assert.That(loaded.Username, Is.EqualTo("aUser"));
            Assert.That(loaded.Password, Is.EqualTo("aPassword"));
            Assert.That(loaded.UnsecureAuth, Is.True);
        }

        [Test]
        public void Roundtrip_SSPI()
        {
            // Roundtrip data
            Download d = new Download(From, To, false, Download.AuthType.sspi);
            var sd = ConfigXmlBuilder.create()
                .WithDownload(d)
                .ToServiceDescriptor(true);
            var loaded = GetSingleEntry(sd);

            // Check default values
            Assert.That(loaded.FailOnError, Is.False);
            Assert.That(loaded.Auth, Is.EqualTo(Download.AuthType.sspi));
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
            var d = new Download(unsecureFrom, To, auth: Download.AuthType.basic, username: "aUser", password: "aPassword");
            AssertInitializationFails(d, "Warning: you're sending your credentials in clear text to the server");
        }

        [Test]
        public void RejectBasicAuth_Without_Username()
        {
            var d = new Download(From, To, auth: Download.AuthType.basic, username: null, password: "aPassword");
            AssertInitializationFails(d, "Basic Auth is enabled, but username is not specified");
        }

        [Test]
        public void RejectBasicAuth_Without_Password()
        {
            var d = new Download(From, To, auth: Download.AuthType.basic, username: "aUser", password: null);
            AssertInitializationFails(d, "Basic Auth is enabled, but password is not specified");
        }

        /// <summary>
        /// Ensures that the fail-on-error field is being processed correctly.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void Download_FailOnError(bool failOnError)
        {
            Download d = new Download(From, To, failOnError);

            var sd = ConfigXmlBuilder.create()
                .WithDownload(d)
                .ToServiceDescriptor(true);

            var loaded = GetSingleEntry(sd);
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

            var loaded = GetSingleEntry(sd);
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
            var loaded = GetSingleEntry(sd);
            Assert.That(loaded.Auth, Is.EqualTo(Download.AuthType.sspi));
        }

        [Test]
        public void Should_Fail_On_Unsupported_AuthType()
        {
            // TODO: will need refactoring once all fields are being parsed on startup
            var sd = ConfigXmlBuilder.create()
                    .WithRawEntry("<download from=\"http://www.nosuchhostexists.foo.myorg/foo.xml\" to=\"%BASE%\\foo.xml\" auth=\"digest\"/>")
                    .ToServiceDescriptor(true);

            Assert.That(() => GetSingleEntry(sd), Throws.TypeOf<InvalidDataException>().With.Message.StartsWith("Cannot parse <auth> Enum value from string 'digest'"));
        }

        private Download GetSingleEntry(ServiceDescriptor sd)
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

            Assert.That(() => GetSingleEntry(sd), Throws.TypeOf<InvalidDataException>().With.Message.StartsWith(expectedMessagePart));
        }
    }
}
