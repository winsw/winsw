using WinSW.Util;

namespace WinSW.Extensions
{
    public class WinSWExtensionConfiguration
    {
        public string Id { get; set; }

        public bool Enabled { get; set; }

        public string ClassName { get; set; }

        public ObjectQuery Settings { get; set; }

        public WinSWExtensionConfiguration(string id, bool enabled, string className, ObjectQuery settings)
        {
            this.Id = id;
            this.Enabled = enabled;
            this.ClassName = className;
            this.Settings = settings;
        }
    }
}
