using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

using MediaPortal.Plugins.MovingPictures.Database;

namespace EventGhostPlus
{
    public static class MovingPicturesHelper
    {
        public static string CheckDb(ref string searchFile)
        {
            string genre = "";
            if (IsAssemblyAvailable("MovingPictures", new Version(1, 0, 6, 1116)))
            {
                if (EventGhostPlus.DebugMode) Logger.Debug("MovingPictures found.");
                if (searchFile.IndexOf(".MPLS") != -1) // Blu-Ray played with BDHandler
                {
                    if (EventGhostPlus.DebugMode) Logger.Debug("Blu-Ray being played with BDHandler, converting filename.");
                    int bdmVindex;
                    string oldFile = searchFile;
                    bdmVindex = searchFile.IndexOf("\\BDMV\\");
                    if (bdmVindex != -1)
                    {
                        searchFile = searchFile.Substring(0, bdmVindex + 6) + "INDEX.BDMV";
                    }
                    if (EventGhostPlus.DebugMode) Logger.Debug("Filename converted from: " + oldFile + " to: " + searchFile);
                }
                if (EventGhostPlus.DebugMode) Logger.Debug("Searching Database for: " + searchFile);
                DBLocalMedia matches = DBLocalMedia.Get(searchFile);
                if (matches.AttachedMovies.Count > 0)
                {
                    if (EventGhostPlus.DebugMode) Logger.Debug("Found " + matches.AttachedMovies.Count.ToString() + " matches.");
                    DBMovieInfo moviematch = matches.AttachedMovies[0];
                    genre = moviematch.Genres.ToString();
                }
            }
            if (EventGhostPlus.DebugMode) Logger.Debug("Returning Genre: "+genre);
            return genre;
        }
        internal static bool IsAssemblyAvailable(string name, Version ver)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly a in assemblies)
            {
                try
                {
                    if (a.GetName().Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return (ver == null || a.GetName().Version >= ver);
                    }
                }
                catch { }
            }
            return false;
        }
    }
    
}
