using System;
using System.IO;
using System.Net;
using System.Text;
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
                if (Username == null)
                {
                    throw new InvalidDataException("Basic Auth is enabled, but username is not specified " + ShortId);
                }

                if (Password == null)
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
        public void Perform()
        {
            WebRequest req = WebRequest.Create(From);

            switch (Auth)
            {
                case AuthType.none:
                    // Do nothing
                    break;

                case AuthType.sspi:
                    req.UseDefaultCredentials = true;
                    req.PreAuthenticate = true;
                    req.Credentials = CredentialCache.DefaultCredentials;
                    break;

                case AuthType.basic:
                    SetBasicAuthHeader(req, Username!, Password!);
                    break;

                default:
                    throw new WebException("Code defect. Unsupported authentication type: " + Auth);
            }

            WebResponse rsp = req.GetResponse();
            FileStream tmpstream = new FileStream(To + ".tmp", FileMode.Create);
            CopyStream(rsp.GetResponseStream(), tmpstream);
            // only after we successfully downloaded a file, overwrite the existing one
            if (File.Exists(To))
                File.Delete(To);

            File.Move(To + ".tmp", To);
        }

        private static void CopyStream(Stream i, Stream o)
        {
            byte[] buf = new byte[8192];
            while (true)
            {
                int len = i.Read(buf, 0, buf.Length);
                if (len <= 0)
                    break;

                o.Write(buf, 0, len);
            }

            i.Close();
            o.Close();
        }
    }
}
