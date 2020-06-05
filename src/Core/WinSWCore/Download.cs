using System;
using System.IO;
using System.Net;
#if !VNEXT
using System.Reflection;
#endif
using System.Text;
#if VNEXT
using System.Threading.Tasks;
#endif
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

        public string from;
        public string to;
        public AuthType auth;
        public string? username;
        public string? password;
        public bool unsecureAuth;
        public bool failOnError;
        public string? proxy;

        public string ShortId => $"(download from {from})";

        public Download()
        {

        }

        static Download()
        {
#if NET461
            // If your app runs on .NET Framework 4.7 or later versions, but targets an earlier version
            AppContext.SetSwitch("Switch.System.Net.DontEnableSystemDefaultTlsVersions", false);
#elif !VNEXT
            // If your app runs on .NET Framework 4.6, but targets an earlier version
            Type.GetType("System.AppContext")?.InvokeMember("SetSwitch", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static, null, null, new object[] { "Switch.System.Net.DontEnableSchUseStrongCrypto", false });

            const SecurityProtocolType Tls12 = (SecurityProtocolType)0x00000C00;
            const SecurityProtocolType Tls11 = (SecurityProtocolType)0x00000300;

            // Windows 7 and Windows Server 2008 R2
            if (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor == 1)
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= Tls11 | Tls12;
                    Logger.Info("TLS 1.1/1.2 enabled");
                }
                catch (NotSupportedException)
                {
                    Logger.Info("TLS 1.1/1.2 disabled");
                }
            }
#endif
        }

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
            this.from = from;
            this.to = to;
            this.failOnError = failOnError;
            this.proxy = proxy;
            this.auth = auth;
            this.username = username;
            this.password = password;
            this.unsecureAuth = unsecureAuth;
        }

        /// <summary>
        /// Constructs the download setting sfrom the XML entry
        /// </summary>
        /// <param name="n">XML element</param>
        /// <exception cref="InvalidDataException">The required attribute is missing or the configuration is invalid</exception>
        internal Download(XmlElement n)
        {
            from = XmlHelper.SingleAttribute<string>(n, "from");
            to = XmlHelper.SingleAttribute<string>(n, "to");

            // All arguments below are optional
            failOnError = XmlHelper.SingleAttribute(n, "failOnError", false);
            proxy = XmlHelper.SingleAttribute<string>(n, "proxy", null);

            auth = XmlHelper.EnumAttribute(n, "auth", AuthType.none);
            username = XmlHelper.SingleAttribute<string>(n, "user", null);
            password = XmlHelper.SingleAttribute<string>(n, "password", null);
            unsecureAuth = XmlHelper.SingleAttribute(n, "unsecureAuth", false);

            if (auth == AuthType.basic)
            {
                // Allow it only for HTTPS or for UnsecureAuth
                if (!from.StartsWith("https:") && !unsecureAuth)
                {
                    throw new InvalidDataException("Warning: you're sending your credentials in clear text to the server " + ShortId +
                                                   "If you really want this you must enable 'unsecureAuth' in the configuration");
                }

                // Also fail if there is no user/password
                if (username is null)
                {
                    throw new InvalidDataException("Basic Auth is enabled, but username is not specified " + ShortId);
                }

                if (password is null)
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
#if VNEXT
        public async Task PerformAsync()
#else
        public void Perform()
#endif
        {
            WebRequest request = WebRequest.Create(from);
            if (!string.IsNullOrEmpty(proxy))
            {
                CustomProxyInformation proxyInformation = new CustomProxyInformation(proxy);
                if (proxyInformation.Credentials != null)
                {
                    request.Proxy = new WebProxy(proxyInformation.ServerAddress, false, null, proxyInformation.Credentials);
                }
                else
                {
                    request.Proxy = new WebProxy(proxyInformation.ServerAddress);
                }
            }

            switch (auth)
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
                    SetBasicAuthHeader(request, username!, password!);
                    break;

                default:
                    throw new WebException("Code defect. Unsupported authentication type: " + auth);
            }

            bool supportsIfModifiedSince = false;
            if (request is HttpWebRequest httpRequest && File.Exists(to))
            {
                supportsIfModifiedSince = true;
                httpRequest.IfModifiedSince = File.GetLastWriteTime(to);
            }

            DateTime lastModified = default;
            string tmpFilePath = to + ".tmp";
            try
            {
#if VNEXT
                using (WebResponse response = await request.GetResponseAsync())
#else
                using (WebResponse response = request.GetResponse())
#endif
                using (Stream responseStream = response.GetResponseStream())
                using (FileStream tmpStream = new FileStream(tmpFilePath, FileMode.Create))
                {
                    if (supportsIfModifiedSince)
                    {
                        lastModified = ((HttpWebResponse)response).LastModified;
                    }

#if VNEXT
                    await responseStream.CopyToAsync(tmpStream);
#elif NET20
                    CopyStream(responseStream, tmpStream);
#else
                    responseStream.CopyTo(tmpStream);
#endif
                }

                FileHelper.MoveOrReplaceFile(to + ".tmp", to);

                if (supportsIfModifiedSince)
                {
                    File.SetLastWriteTime(to, lastModified);
                }
            }
            catch (WebException e)
            {
                if (supportsIfModifiedSince && ((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.NotModified)
                {
                    Logger.Info($"Skipped downloading unmodified resource '{from}'");
                }
                else
                {
                    throw;
                }
            }
        }

#if NET20
        private static void CopyStream(Stream source, Stream destination)
        {
            byte[] buffer = new byte[8192];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
            {
                destination.Write(buffer, 0, read);
            }
        }
#endif
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
