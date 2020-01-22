using System;
using System.IO;
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
        public void Roundtrip_Defaults()
        {
            // Roundtrip data
            Download d = new Download(From, To);
            var sd = ConfigXmlBuilder.create()
                .WithDownload(d)
                .ToServiceDescriptor(true);
            var loaded = GetSingleEntry(sd);

            // Check default values
            Assert.That(loaded.FailOnError, Is.EqualTo(false));
            Assert.That(loaded.Auth, Is.EqualTo(Download.AuthType.none));
            Assert.That(loaded.Username, Is.Null);
            Assert.That(loaded.Password, Is.Null);
            Assert.That(loaded.UnsecureAuth, Is.EqualTo(false));
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
            Assert.That(loaded.FailOnError, Is.EqualTo(true));
            Assert.That(loaded.Auth, Is.EqualTo(Download.AuthType.basic));
            Assert.That(loaded.Username, Is.EqualTo("aUser"));
            Assert.That(loaded.Password, Is.EqualTo("aPassword"));
            Assert.That(loaded.UnsecureAuth, Is.EqualTo(true));
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
            Assert.That(loaded.FailOnError, Is.EqualTo(false));
            Assert.That(loaded.Auth, Is.EqualTo(Download.AuthType.sspi));
            Assert.That(loaded.Username, Is.Null);
            Assert.That(loaded.Password, Is.Null);
            Assert.That(loaded.UnsecureAuth, Is.EqualTo(false));
        }

        [TestCase("http://")]
        [TestCase("ftp://")]
        [TestCase("file:///")]
        [TestCase("jar://")]
        [TestCase("\\\\")] // UNC
        public void ShouldReject_BasicAuth_with_UnsecureProtocol(string protocolPrefix)
        {
            var d = new Download(protocolPrefix + "myServer.com:8080/file.txt", To,
                auth: Download.AuthType.basic, username: "aUser", password: "aPassword");
            AssertInitializationFails(d, "you're sending your credentials in clear text to the server");
        }

        public void ShouldRejectBasicAuth_without_username()
        {
            var d = new Download(From, To, auth: Download.AuthType.basic, username: null, password: "aPassword");
            AssertInitializationFails(d, "Basic Auth is enabled, but username is not specified");
        }

        public void ShouldRejectBasicAuth_without_password()
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

            ExceptionHelper.AssertFails("Cannot parse <auth> Enum value from string 'digest'", typeof(InvalidDataException), () => _ = GetSingleEntry(sd));
        }

        private Download GetSingleEntry(ServiceDescriptor sd)
        {
            var downloads = sd.Downloads.ToArray();
            Assert.That(downloads.Length, Is.EqualTo(1), "Service Descriptor is expected to have only one entry");
            return downloads[0];
        }

        private void AssertInitializationFails(Download download, string expectedMessagePart = null, Type expectedExceptionType = null)
        {
            var sd = ConfigXmlBuilder.create()
                .WithDownload(download)
                .ToServiceDescriptor(true);

            ExceptionHelper.AssertFails(expectedMessagePart, expectedExceptionType ?? typeof(InvalidDataException), () => _ = GetSingleEntry(sd));
        }
    }
}
