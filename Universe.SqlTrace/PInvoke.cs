using System;
using System.Runtime.InteropServices;

namespace Universe.SqlTrace
{
    class PInvoke
    {
#if NETSTANDARD1_3
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
#else    
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
#endif
        static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName,
           MoveFileFlags dwFlags);

        [Flags]
        enum MoveFileFlags : int
        {
            MOVEFILE_REPLACE_EXISTING = 0x00000001,
            MOVEFILE_COPY_ALLOWED = 0x00000002,
            MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004,
            MOVEFILE_WRITE_THROUGH = 0x00000008,
            MOVEFILE_CREATE_HARDLINK = 0x00000010,
            MOVEFILE_FAIL_IF_NOT_TRACKABLE = 0x00000020
        }

        public static void DeleteFileOnReboot(string fileName)
        {
            MoveFileEx(fileName, null, MoveFileFlags.MOVEFILE_DELAY_UNTIL_REBOOT);
        }

    }
}
