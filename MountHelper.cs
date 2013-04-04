using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.Util;

namespace EventGhostPlus
{
    public static class MountHelper
    {
        public static string CheckMount(ref string SearchFile)
        {
            string MountDrive = "";
            if (EventGhostPlus.DebugMode) Logger.Debug("Getting mount drive (if any)");
            MountDrive = DaemonTools.GetVirtualDrive();
            if (!MountDrive.Equals(""))
            {
                if (EventGhostPlus.DebugMode) Logger.Debug("Found mount drive: " + MountDrive);
                if (DaemonTools.MountedIsoFile != "") // if drive is mounted.
                {
                    if (EventGhostPlus.DebugMode) Logger.Debug("An ISO is mounted.");
                    if (SearchFile.Contains(MountDrive)) // if the mountdrive is the same as the drive in the Current playing file.
                    {
                        if (EventGhostPlus.DebugMode) Logger.Debug("Playing file from a mounted drive.");
                        SearchFile = DaemonTools.MountedIsoFile;
                    }
               }
            }
            if (EventGhostPlus.DebugMode) Logger.Debug("Returning filename: "+SearchFile);
            return SearchFile;
        }
    }
}
