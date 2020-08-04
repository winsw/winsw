namespace WinSW
{
    public struct ProcessCommand
    {
        public string? Executable;
        public string? Arguments;
        public string? StdoutPath;
        public string? StderrPath;

        public LogHandler CreateLogHandler() => new TempLogHandler(this.StdoutPath, this.StderrPath);
    }
}
