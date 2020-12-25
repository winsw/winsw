#pragma warning disable SA1310 // Field names should not contain underscore

namespace WinSW.Native
{
    internal static class Errors
    {
        internal const int ERROR_ACCESS_DENIED = 5;
        internal const int ERROR_INVALID_HANDLE = 6;
        internal const int ERROR_INVALID_PARAMETER = 7;
        internal const int ERROR_SERVICE_ALREADY_RUNNING = 1056;
        internal const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;
        internal const int ERROR_SERVICE_NOT_ACTIVE = 1062;
        internal const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;
        internal const int ERROR_SERVICE_EXISTS = 1073;
        internal const int ERROR_CANCELLED = 1223;
    }
}
