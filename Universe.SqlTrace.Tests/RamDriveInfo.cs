using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Universe.SqlTrace.Tests
{
    static class RamDriveInfo
    {
        public static readonly string RamDrivePath;

        static RamDriveInfo()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives)
            {
                try
                {
                    if (drive.DriveType == DriveType.Ram || drive.VolumeLabel == "RamDisk")
                        if (drive.TotalSize >= 100L * 1024 * 1204)
                        {
                            RamDrivePath = drive.RootDirectory.FullName;
                            return;
                        }
                }
                catch
                {
                }
            }

        }

    }
}
