using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace winsw
{
    /// <summary>
    /// Specify the download activities prior to the launch.
    /// This enables self-updating services.
    /// </summary>
    public class Download
    {
        public enum AuthType { none = 0, sspi, basic }

        public readonly string From;
        public readonly string To;
        public readonly AuthType Auth = AuthType.none;
        public readonly string Username;
        public readonly string Password;
        public readonly bool UnsecureAuth = false;
        public readonly bool FailOnError;

        public Download(string from, string to, bool failOnError = false)
        {
            From = from;
            To = to;
            FailOnError = failOnError;
        }

        internal Download(XmlNode n)
        {
            From = Environment.ExpandEnvironmentVariables(n.Attributes["from"].Value);
            To = Environment.ExpandEnvironmentVariables(n.Attributes["to"].Value);
            
            var failOnErrorNode = n.Attributes["failOnError"];
            FailOnError = failOnErrorNode != null ? Boolean.Parse(failOnErrorNode.Value) : false;

            string tmpStr = "";
            try
            {
                tmpStr = Environment.ExpandEnvironmentVariables(n.Attributes["auth"].Value);
            }
            catch (Exception)
            {
            }
            Auth = tmpStr != "" ? (AuthType)Enum.Parse(typeof(AuthType), tmpStr) : AuthType.none;

            try
            {
                tmpStr = Environment.ExpandEnvironmentVariables(n.Attributes["username"].Value);
            }
            catch (Exception)
            {
            }
            Username = tmpStr;

            try
            {
                tmpStr = Environment.ExpandEnvironmentVariables(n.Attributes["password"].Value);
            }
            catch (Exception)
            {
            }
            Password = tmpStr;

            try
            {
                tmpStr = Environment.ExpandEnvironmentVariables(n.Attributes["unsecureAuth"].Value);
            }
            catch (Exception)
            {
            }
            UnsecureAuth = tmpStr == "enabled" ? true : false;

            if (Auth == AuthType.basic)
            {
                if (From.StartsWith("http:") && UnsecureAuth == false)
                {
                    throw new Exception("Warning: you're sending your credentials in clear text to the server. If you really want this you must enable this in the configuration!");
                }
            }
        }

        // Source: http://stackoverflow.com/questions/2764577/forcing-basic-authentication-in-webrequest
        public void SetBasicAuthHeader(WebRequest request, String username, String password)
        {
            string authInfo = username + ":" + password;
            authInfo = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(authInfo));
            request.Headers["Authorization"] = "Basic " + authInfo;
        }

        /// <summary>
        ///     Downloads the requested file and puts it to the specified target.
        /// </summary>
        /// <exception cref="System.Net.WebException">
        ///     Download failure. FailOnError flag should be processed outside.
        /// </exception>
        public void Perform()
        {
            WebRequest req = WebRequest.Create(From);

            switch (Auth)
            {
                case AuthType.sspi:
                    req.UseDefaultCredentials = true;
                    req.PreAuthenticate = true;
                    req.Credentials = CredentialCache.DefaultCredentials;
                    break;

                case AuthType.basic:
                    SetBasicAuthHeader(req, Username, Password);
                    break;
            }

            WebResponse rsp = req.GetResponse();
            FileStream tmpstream = new FileStream(To + ".tmp", FileMode.Create);
            CopyStream(rsp.GetResponseStream(), tmpstream);
            // only after we successfully downloaded a file, overwrite the existing one
            if (File.Exists(To))
                File.Delete(To);
            File.Move(To + ".tmp", To);
        }

        /// <summary>
        /// Produces the XML configuuration entry.
        /// </summary>
        /// <returns>XML String for the configuration file</returns>
        public String toXMLConfig()
        {
            return "<download from=\"" + From + "\" to=\"" + To + "\" failOnError=\"" + FailOnError + "\"/>";
        }

        private static void CopyStream(Stream i, Stream o)
        {
            byte[] buf = new byte[8192];
            while (true)
            {
                int len = i.Read(buf, 0, buf.Length);
                if (len <= 0) break;
                o.Write(buf, 0, len);
            }
            i.Close();
            o.Close();
        }
    }
}
