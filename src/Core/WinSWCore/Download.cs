using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using log4net;
using winsw.Util;

namespace winsw
{
    /// <summary>
    /// Specify the download activities prior to the launch.
    /// This enables self-updating services.
    /// </summary>
    public class Download
    {
        public enum AuthType
        {
            none = 0,
            sspi,
            basic
        }

        private static readonly ILog Logger = LogManager.GetLogger(typeof(Download));

        public readonly string From;
        public readonly string To;
        public readonly AuthType Auth;
        public readonly string? Username;
        public readonly string? Password;
        public readonly bool UnsecureAuth;
        public readonly bool FailOnError;
        public readonly string? Proxy;

        public string ShortId => $"(download from {From})";

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
            AuthType auth = AuthType.none,
            string? username = null,
            string? password = null,
            bool unsecureAuth = false,
            string? proxy = null)
        {
            From = from;
            To = to;
            FailOnError = failOnError;
            Proxy = proxy;
            Auth = auth;
            Username = username;
            Password = password;
            UnsecureAuth = unsecureAuth;
        }

        /// <summary>
        /// Constructs the download setting sfrom the XML entry
        /// </summary>
        /// <param name="n">XML element</param>
        /// <exception cref="InvalidDataException">The required attribute is missing or the configuration is invalid</exception>
        internal Download(XmlElement n)
        {
            From = XmlHelper.SingleAttribute<string>(n, "from");
            To = XmlHelper.SingleAttribute<string>(n, "to");

            // All arguments below are optional
            FailOnError = XmlHelper.SingleAttribute(n, "failOnError", false);
            Proxy = XmlHelper.SingleAttribute<string>(n, "proxy", null);

            Auth = XmlHelper.EnumAttribute(n, "auth", AuthType.none);
            Username = XmlHelper.SingleAttribute<string>(n, "user", null);
            Password = XmlHelper.SingleAttribute<string>(n, "password", null);
            UnsecureAuth = XmlHelper.SingleAttribute(n, "unsecureAuth", false);

            if (Auth == AuthType.basic)
            {
                // Allow it only for HTTPS or for UnsecureAuth
                if (!From.StartsWith("https:") && !UnsecureAuth)
                {
                    throw new InvalidDataException("Warning: you're sending your credentials in clear text to the server " + ShortId +
                                                   "If you really want this you must enable 'unsecureAuth' in the configuration");
                }

                // Also fail if there is no user/password
                if (Username is null)
                {
                    throw new InvalidDataException("Basic Auth is enabled, but username is not specified " + ShortId);
                }

                if (Password is null)
                {
                    throw new InvalidDataException("Basic Auth is enabled, but password is not specified " + ShortId);
                }
            }
        }

        // Source: http://stackoverflow.com/questions/2764577/forcing-basic-authentication-in-webrequest
        private void SetBasicAuthHeader(WebRequest request, string username, string password)
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
            WebRequest request = WebRequest.Create(From);
            if (!string.IsNullOrEmpty(Proxy))
            {
                CustomProxyInformation proxyInformation = new CustomProxyInformation(Proxy);
                if (proxyInformation.Credentials != null)
                {
                    request.Proxy = new WebProxy(proxyInformation.ServerAddress, false, null, proxyInformation.Credentials);
                }
                else
                {
                    request.Proxy = new WebProxy(proxyInformation.ServerAddress);
                }
            }

            switch (Auth)
            {
                case AuthType.none:
                    // Do nothing
                    break;

                case AuthType.sspi:
                    request.UseDefaultCredentials = true;
                    request.PreAuthenticate = true;
                    request.Credentials = CredentialCache.DefaultCredentials;
                    break;

                case AuthType.basic:
                    SetBasicAuthHeader(request, Username!, Password!);
                    break;

                default:
                    throw new WebException("Code defect. Unsupported authentication type: " + Auth);
            }

            bool supportsIfModifiedSince = false;
            if (request is HttpWebRequest httpRequest && File.Exists(To))
            {
                supportsIfModifiedSince = true;
                httpRequest.IfModifiedSince = File.GetLastWriteTime(To);
            }

            DateTime lastModified = default;
            string tmpFilePath = To + ".tmp";
            try
            {
                using (WebResponse response = await request.GetResponseAsync())
                using (Stream responseStream = response.GetResponseStream())
                using (FileStream tmpStream = new FileStream(tmpFilePath, FileMode.Create))
                {
                    if (supportsIfModifiedSince)
                    {
                        lastModified = ((HttpWebResponse)response).LastModified;
                    }

                    await responseStream.CopyToAsync(tmpStream);
                }

                FileHelper.MoveOrReplaceFile(To + ".tmp", To);

                if (supportsIfModifiedSince)
                {
                    File.SetLastWriteTime(To, lastModified);
                }
            }
            catch (WebException e)
            {
                if (supportsIfModifiedSince && ((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.NotModified)
                {
                    Logger.Info($"Skipped downloading unmodified resource '{From}'");
                }
                else
                {
                    throw;
                }
            }
        }
    }

    public class CustomProxyInformation
    {
        public string ServerAddress { get; set; }
        public NetworkCredential? Credentials { get; set; }

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
                Credentials = new NetworkCredential(username, password);
                ServerAddress = proxy.Replace(completeCredsStr + "@", "");
            }
            else
            {
                ServerAddress = proxy;
            }
        }
    }
}
