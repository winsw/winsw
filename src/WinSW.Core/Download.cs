using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using log4net;
using WinSW.Logging;
using WinSW.Util;

namespace WinSW
{
    /// <summary>
    /// Specify the download activities prior to the launch.
    /// This enables self-updating services.
    /// </summary>
    public class Download
    {
        public enum AuthType
        {
            None = 0,
            Sspi,
            Basic
        }

        private static readonly ILog Logger = LogManager.GetLogger(LoggerNames.Service);

        public readonly string From;
        public readonly string To;
        public readonly AuthType Auth;
        public readonly string? Username;
        public readonly string? Password;
        public readonly bool UnsecureAuth;
        public readonly bool FailOnError;
        public readonly string? Proxy;

        public string ShortId => $"(download from {this.From})";

#if NET461
        static Download()
        {
            // If your app runs on .NET Framework 4.7 or later versions, but targets an earlier version
            AppContext.SetSwitch("Switch.System.Net.DontEnableSystemDefaultTlsVersions", false);
        }
#endif

        // internal
        public Download(
            string from,
            string to,
            bool failOnError = false,
            AuthType auth = AuthType.None,
            string? username = null,
            string? password = null,
            bool unsecureAuth = false,
            string? proxy = null)
        {
            this.From = from;
            this.To = to;
            this.FailOnError = failOnError;
            this.Proxy = proxy;
            this.Auth = auth;
            this.Username = username;
            this.Password = password;
            this.UnsecureAuth = unsecureAuth;
        }

        /// <summary>
        /// Constructs the download setting sfrom the XML entry
        /// </summary>
        /// <param name="n">XML element</param>
        /// <exception cref="InvalidDataException">The required attribute is missing or the configuration is invalid</exception>
        internal Download(XmlElement n)
        {
            this.From = XmlHelper.SingleAttribute<string>(n, "from");
            this.To = XmlHelper.SingleAttribute<string>(n, "to");

            // All arguments below are optional
            this.FailOnError = XmlHelper.SingleAttribute(n, "failOnError", false);
            this.Proxy = XmlHelper.SingleAttribute<string>(n, "proxy", null);

            this.Auth = XmlHelper.EnumAttribute(n, "auth", AuthType.None);
            this.Username = XmlHelper.SingleAttribute<string>(n, "user", null);
            this.Password = XmlHelper.SingleAttribute<string>(n, "password", null);
            this.UnsecureAuth = XmlHelper.SingleAttribute(n, "unsecureAuth", false);

            if (this.Auth == AuthType.Basic)
            {
                // Allow it only for HTTPS or for UnsecureAuth
                if (!this.From.StartsWith("https:") && !this.UnsecureAuth)
                {
                    throw new InvalidDataException("Warning: you're sending your credentials in clear text to the server " + this.ShortId +
                                                   "If you really want this you must enable 'unsecureAuth' in the configuration");
                }

                // Also fail if there is no user/password
                if (this.Username is null)
                {
                    throw new InvalidDataException("Basic Auth is enabled, but username is not specified " + this.ShortId);
                }

                if (this.Password is null)
                {
                    throw new InvalidDataException("Basic Auth is enabled, but password is not specified " + this.ShortId);
                }
            }
        }

        // Source: http://stackoverflow.com/questions/2764577/forcing-basic-authentication-in-webrequest
        private static void SetBasicAuthHeader(WebRequest request, string username, string password)
        {
            string authInfo = username + ":" + password;
            authInfo = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(authInfo));
            request.Headers["Authorization"] = "Basic " + authInfo;
        }

        /// <summary>
        ///     Downloads the requested file and puts it to the specified target.
        /// </summary>
        /// <exception cref="WebException">
        ///     Download failure. FailOnError flag should be processed outside.
        /// </exception>
        public async Task PerformAsync()
        {
#pragma warning disable SYSLIB0014 // Type or member is obsolete
            var request = WebRequest.Create(this.From);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
            if (!string.IsNullOrEmpty(this.Proxy))
            {
                var proxyInformation = new CustomProxyInformation(this.Proxy!);
                if (proxyInformation.Credentials != null)
                {
                    request.Proxy = new WebProxy(proxyInformation.ServerAddress, false, null, proxyInformation.Credentials);
                }
                else
                {
                    request.Proxy = new WebProxy(proxyInformation.ServerAddress);
                }
            }

            switch (this.Auth)
            {
                case AuthType.None:
                    // Do nothing
                    break;

                case AuthType.Sspi:
                    request.UseDefaultCredentials = true;
                    request.PreAuthenticate = true;
                    request.Credentials = CredentialCache.DefaultCredentials;
                    break;

                case AuthType.Basic:
                    SetBasicAuthHeader(request, this.Username!, this.Password!);
                    break;

                default:
                    throw new WebException("Code defect. Unsupported authentication type: " + this.Auth);
            }

            bool supportsIfModifiedSince = false;
            if (request is HttpWebRequest httpRequest && File.Exists(this.To))
            {
                supportsIfModifiedSince = true;
                httpRequest.IfModifiedSince = File.GetLastWriteTime(this.To);
            }

            DateTime lastModified = default;
            string tmpFilePath = this.To + ".tmp";
            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                using (var responseStream = response.GetResponseStream())
                using (var tmpStream = new FileStream(tmpFilePath, FileMode.Create))
                {
                    if (supportsIfModifiedSince)
                    {
                        lastModified = ((HttpWebResponse)response).LastModified;
                    }

                    await responseStream.CopyToAsync(tmpStream).ConfigureAwait(false);
                }

                FileHelper.MoveOrReplaceFile(this.To + ".tmp", this.To);

                if (supportsIfModifiedSince)
                {
                    File.SetLastWriteTime(this.To, lastModified);
                }
            }
            catch (WebException e)
            {
                if (supportsIfModifiedSince && ((HttpWebResponse?)e.Response)?.StatusCode == HttpStatusCode.NotModified)
                {
                    Logger.Info($"Skipped downloading unmodified resource '{this.From}'");
                    return;
                }

                string errorMessage = $"Failed to download {this.From} to {this.To}";
                Logger.Error(errorMessage, e);
                if (this.FailOnError)
                {
                    throw new IOException(errorMessage, e);
                }
            }
        }
    }

    public class CustomProxyInformation
    {
        public string ServerAddress { get; }

        public NetworkCredential? Credentials { get; }

        public CustomProxyInformation(string proxy)
        {
            if (proxy.Contains("@"))
            {
                // Extract proxy credentials
                int credsFrom = proxy.IndexOf("://") + 3;
                int credsTo = proxy.LastIndexOf("@");
                string completeCredsStr = proxy.Substring(credsFrom, credsTo - credsFrom);
                int credsSeparator = completeCredsStr.IndexOf(":");

                string username = completeCredsStr.Substring(0, credsSeparator);
                string password = completeCredsStr.Substring(credsSeparator + 1);
                this.Credentials = new NetworkCredential(username, password);
                this.ServerAddress = proxy.Replace(completeCredsStr + "@", null);
            }
            else
            {
                this.ServerAddress = proxy;
            }
        }
    }
}
