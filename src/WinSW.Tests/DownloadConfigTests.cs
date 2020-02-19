using System.IO;
using WinSW.Tests.Util;
using Xunit;

namespace WinSW.Tests
{
    public class DownloadConfigTests
    {
        private const string From = "https://www.nosuchhostexists.foo.myorg/foo.xml";
        private const string To = "%BASE%\\foo.xml";

        [Fact]
        public void Roundtrip_Defaults()
        {
            // Roundtrip data
            Download d = new Download(From, To);
            var sd = ConfigXmlBuilder.Create()
                .WithDownload(d)
                .ToServiceDescriptor(true);
            var loaded = this.GetSingleEntry(sd);

            // Check default values
            Assert.False(loaded.FailOnError);
            Assert.Equal(Download.AuthType.None, loaded.Auth);
            Assert.Null(loaded.Username);
            Assert.Null(loaded.Password);
            Assert.False(loaded.UnsecureAuth);
        }

        [Fact]
        public void Roundtrip_BasicAuth()
        {
            // Roundtrip data
            Download d = new Download(From, To, true, Download.AuthType.Basic, "aUser", "aPassword", true);
            var sd = ConfigXmlBuilder.Create()
                .WithDownload(d)
                .ToServiceDescriptor(true);
            var loaded = this.GetSingleEntry(sd);

            // Check default values
            Assert.True(loaded.FailOnError);
            Assert.Equal(Download.AuthType.Basic, loaded.Auth);
            Assert.Equal("aUser", loaded.Username);
            Assert.Equal("aPassword", loaded.Password);
            Assert.True(loaded.UnsecureAuth);
        }

        [Fact]
        public void Roundtrip_SSPI()
        {
            // Roundtrip data
            Download d = new Download(From, To, false, Download.AuthType.Sspi);
            var sd = ConfigXmlBuilder.Create()
                .WithDownload(d)
                .ToServiceDescriptor(true);
            var loaded = this.GetSingleEntry(sd);

            // Check default values
            Assert.False(loaded.FailOnError);
            Assert.Equal(Download.AuthType.Sspi, loaded.Auth);
            Assert.Null(loaded.Username);
            Assert.Null(loaded.Password);
            Assert.False(loaded.UnsecureAuth);
        }

        [Theory]
        [InlineData("http://")]
        [InlineData("ftp://")]
        [InlineData("file://")]
        [InlineData("\\\\")] // UNC
        public void RejectBasicAuth_With_UnsecureProtocol(string protocolPrefix)
        {
            string unsecureFrom = protocolPrefix + "myServer.com:8080/file.txt";
            var d = new Download(unsecureFrom, To, auth: Download.AuthType.Basic, username: "aUser", password: "aPassword");
            this.AssertInitializationFails(d, "Warning: you're sending your credentials in clear text to the server");
        }

        [Fact]
        public void RejectBasicAuth_Without_Username()
        {
            var d = new Download(From, To, auth: Download.AuthType.Basic, username: null, password: "aPassword");
            this.AssertInitializationFails(d, "Basic Auth is enabled, but username is not specified");
        }

        [Fact]
        public void RejectBasicAuth_Without_Password()
        {
            var d = new Download(From, To, auth: Download.AuthType.Basic, username: "aUser", password: null);
            this.AssertInitializationFails(d, "Basic Auth is enabled, but password is not specified");
        }

        /// <summary>
        /// Ensures that the fail-on-error field is being processed correctly.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Download_FailOnError(bool failOnError)
        {
            Download d = new Download(From, To, failOnError);

            var sd = ConfigXmlBuilder.Create()
                .WithDownload(d)
                .ToServiceDescriptor(true);

            var loaded = this.GetSingleEntry(sd);
            Assert.Equal(From, loaded.From);
            Assert.Equal(To, loaded.To);
            Assert.Equal(failOnError, loaded.FailOnError);
        }

        /// <summary>
        /// Ensures that the fail-on-error field is being processed correctly.
        /// </summary>
        [Fact]
        public void Download_FailOnError_Undefined()
        {
            var sd = ConfigXmlBuilder.Create()
                .WithRawEntry("<download from=\"http://www.nosuchhostexists.foo.myorg/foo.xml\" to=\"%BASE%\\foo.xml\"/>")
                .ToServiceDescriptor(true);

            var loaded = this.GetSingleEntry(sd);
            Assert.False(loaded.FailOnError);
        }

        [Theory]
        [InlineData("sspi")]
        [InlineData("SSPI")]
        [InlineData("SsPI")]
        [InlineData("Sspi")]
        public void AuthType_Is_CaseInsensitive(string authType)
        {
            var sd = ConfigXmlBuilder.Create()
                    .WithRawEntry("<download from=\"http://www.nosuchhostexists.foo.myorg/foo.xml\" to=\"%BASE%\\foo.xml\" auth=\"" + authType + "\"/>")
                    .ToServiceDescriptor(true);
            var loaded = this.GetSingleEntry(sd);
            Assert.Equal(Download.AuthType.Sspi, loaded.Auth);
        }

        [Fact]
        public void Should_Fail_On_Unsupported_AuthType()
        {
            // TODO: will need refactoring once all fields are being parsed on startup
            var sd = ConfigXmlBuilder.Create()
                    .WithRawEntry("<download from=\"http://www.nosuchhostexists.foo.myorg/foo.xml\" to=\"%BASE%\\foo.xml\" auth=\"digest\"/>")
                    .ToServiceDescriptor(true);

            var e = Assert.Throws<InvalidDataException>(() => this.GetSingleEntry(sd));
            Assert.StartsWith("Cannot parse <auth> Enum value from string 'digest'", e.Message);
        }

        [Theory]
        [InlineData("http://", "127.0.0.1:80", "egarcia", "Passw0rd")]
        [InlineData("https://", "myurl.com.co:2298", "MyUsername", "P@ssw:rd")]
        [InlineData("http://", "192.168.0.8:3030")]
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

            Assert.Equal(protocol + address + "/", cpi.ServerAddress);

            if (string.IsNullOrEmpty(username))
            {
                Assert.Null(cpi.Credentials);
            }
            else
            {
                Assert.NotNull(cpi.Credentials);
                Assert.Equal(username, cpi.Credentials.UserName);
                Assert.Equal(password, cpi.Credentials.Password);
            }
        }

        private Download GetSingleEntry(ServiceDescriptor sd)
        {
            var downloads = sd.Downloads.ToArray();
            return Assert.Single(downloads);
        }

        private void AssertInitializationFails(Download download, string expectedMessagePart = null)
        {
            var sd = ConfigXmlBuilder.Create()
                .WithDownload(download)
                .ToServiceDescriptor(true);

            var e = Assert.Throws<InvalidDataException>(() => this.GetSingleEntry(sd));
            Assert.StartsWith(expectedMessagePart, e.Message);
        }
    }
}
