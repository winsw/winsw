using System.IO;

namespace WinSW.Util
{
    public static class FileHelper
    {
        public static void MoveOrReplaceFile(string sourceFileName, string destFileName)
        {
            File.Move(sourceFileName, destFileName, true);
        }
    }
}
