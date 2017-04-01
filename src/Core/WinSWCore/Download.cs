using System;
using System.Collections;
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
        public readonly string From;
        public readonly string To;
        public readonly string Username;
        public readonly string Password;
        public readonly bool UnsecureAuth = false;

        internal Download(XmlNode n)
        {
            From = Environment.ExpandEnvironmentVariables(n.Attributes["from"].Value);
            To = Environment.ExpandEnvironmentVariables(n.Attributes["to"].Value);

            string tmpStr = "";
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
            UnsecureAuth = tmpStr == "enable" ? true : false;

            if (From.StartsWith("http:") && Username != String.Empty && Password != String.Empty && UnsecureAuth == false)
            {
                throw new Exception("Warning: you're sending your credentials in clear text to the server. If you really want this you must enable this in the configuration!");
            }
        }

        public void Perform()
        {
            WebRequest req = WebRequest.Create(From);

            WebResponse rsp = null;
            bool trySSPI = true;
            bool basicFailed = false;

            // when credential specified by the user we assume that a Basic auth. is required
            if (Username != String.Empty && Password != String.Empty)
            {
                req.Credentials = new NetworkCredential(Username, Password);

                try
                {
                    rsp = req.GetResponse();
                    trySSPI = false;
                }
                catch (Exception)
                {
                    // the connection was not sucessful - reset the request for a SSPI auth.
                    basicFailed = true;
                    req.Abort();
                    req = null;
                }
            }

            // try SSPI, this also works for server without authentication!
            if (trySSPI == true)
            {
                // try to authenticate via SSPI, when a previous Basic auth. was not sucessfull a new
                // request needs to be created!
                if (basicFailed == true)
                {
                    req = WebRequest.Create(From);
                }

                req.UseDefaultCredentials = true;
                req.PreAuthenticate = true;
                req.Credentials = CredentialCache.DefaultCredentials;

                rsp = req.GetResponse();
            }

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
                if (len <= 0) break;
                o.Write(buf, 0, len);
            }
            i.Close();
            o.Close();
        }
    }
}
