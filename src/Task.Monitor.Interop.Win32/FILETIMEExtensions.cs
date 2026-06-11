namespace Task.Monitor.Interop.Win32
{
    public static class FILETIMEExtensions
    {
        public static long ToLong(this MinWinBase.FILETIME fileTime)
        {
            ulong highTime = (ulong)fileTime.dwHighDateTime;
            uint lowTime = (uint)fileTime.dwLowDateTime;

            long result = (long)((highTime << 32) + lowTime);

            return result;
        }
    }
}
