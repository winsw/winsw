namespace WinSW
{
    public sealed class SharedDirectoryMapperConfig
    {
        public string Label { get; }

        public string UncPath { get; }

        public SharedDirectoryMapperConfig(string driveLabel, string directoryUncPath)
        {
            this.Label = driveLabel;
            this.UncPath = directoryUncPath;
        }
    }
}
