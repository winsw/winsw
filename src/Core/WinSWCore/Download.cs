using System;
using System.IO;
using System.Net;
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

        internal Download(XmlNode n)
        {
            From = Environment.ExpandEnvironmentVariables(n.Attributes["from"].Value);
            To = Environment.ExpandEnvironmentVariables(n.Attributes["to"].Value);
        }

        public void Perform()
        {
            WebRequest req = WebRequest.Create(From);
            req.UseDefaultCredentials = true;
            req.PreAuthenticate = true;
            req.Credentials = CredentialCache.DefaultCredentials;

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
                if (len <= 0) break;
                o.Write(buf, 0, len);
            }
            i.Close();
            o.Close();
        }
    }
}
