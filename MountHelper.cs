using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.Util;

namespace EventGhostPlus
{
    public static class MountHelper
    {
        public static string CheckMount(ref string searchFile)
        {
            string mountDrive = "";
            if (EventGhostPlus.DebugMode) Logger.Debug("Getting mount drive (if any)");
            mountDrive = DaemonTools.GetVirtualDrive();
            if (!mountDrive.Equals(""))
            {
                if (EventGhostPlus.DebugMode) Logger.Debug("Found mount drive: " + mountDrive);
                if (DaemonTools.MountedIsoFile != "") // if drive is mounted.
                {
                    if (EventGhostPlus.DebugMode) Logger.Debug("An ISO is mounted.");
                    if (searchFile.Contains(mountDrive)) // if the mountdrive is the same as the drive in the Current playing file.
                    {
                        if (EventGhostPlus.DebugMode) Logger.Debug("Playing file from a mounted drive.");
                        searchFile = DaemonTools.MountedIsoFile;
                    }
               }
            }
            if (EventGhostPlus.DebugMode) Logger.Debug("Returning filename: "+searchFile);
            return searchFile;
        }
    }
}
