using System;
using System.IO;
using System.Net;
using System.Text;
#if VNEXT
using System.Threading.Tasks;
#endif
using System.Xml;
#if !VNEXT
using log4net;
#endif
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

#if !VNEXT
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Download));
#endif

        public readonly string From;
        public readonly string To;
        public readonly AuthType Auth = AuthType.none;
        public readonly string? Username;
        public readonly string? Password;
        public readonly bool UnsecureAuth;
        public readonly bool FailOnError;

        public string ShortId => $"(download from {From})";

#if !VNEXT
        static Download()
        {
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
            bool unsecureAuth = false)
        {
            From = from;
            To = to;
            FailOnError = failOnError;
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
#if VNEXT
        public async Task PerformAsync()
#else
        public void Perform()
#endif
        {
            WebRequest request = WebRequest.Create(From);

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

            string tmpFilePath = To + ".tmp";
#if VNEXT
            using (WebResponse response = await request.GetResponseAsync())
#else
            using (WebResponse response = request.GetResponse())
#endif
            using (Stream responseStream = response.GetResponseStream())
            using (FileStream tmpStream = new FileStream(tmpFilePath, FileMode.Create))
            {
#if VNEXT
                await responseStream.CopyToAsync(tmpStream);
#elif NET20
                CopyStream(responseStream, tmpStream);
#else
                responseStream.CopyTo(tmpStream);
#endif
            }

            FileHelper.MoveOrReplaceFile(To + ".tmp", To);
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
}
