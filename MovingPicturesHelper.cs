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
        public static string CheckDB(ref string SearchFile)
        {
            string Genre = "";
            if (IsAssemblyAvailable("MovingPictures", new Version(1, 0, 6, 1116)))
            {
                if (EventGhostPlus.DebugMode) Logger.Debug("MovingPictures found.");
                if (SearchFile.IndexOf(".MPLS") != -1) // Blu-Ray played with BDHandler
                {
                    if (EventGhostPlus.DebugMode) Logger.Debug("Blu-Ray being played with BDHandler, converting filename.");
                    int BDMVindex;
                    string OldFile = SearchFile;
                    BDMVindex = SearchFile.IndexOf("\\BDMV\\");
                    if (BDMVindex != -1)
                    {
                        SearchFile = SearchFile.Substring(0, BDMVindex + 6) + "INDEX.BDMV";
                    }
                    if (EventGhostPlus.DebugMode) Logger.Debug("Filename converted from: " + OldFile + " to: " + SearchFile);
                }
                if (EventGhostPlus.DebugMode) Logger.Debug("Searching Database for: " + SearchFile);
                DBLocalMedia Matches = DBLocalMedia.Get(SearchFile);
                if (Matches.AttachedMovies.Count > 0)
                {
                    if (EventGhostPlus.DebugMode) Logger.Debug("Found " + Matches.AttachedMovies.Count.ToString() + " matches.");
                    DBMovieInfo moviematch = Matches.AttachedMovies[0];
                    Genre = moviematch.Genres.ToString();
                }
            }
            if (EventGhostPlus.DebugMode) Logger.Debug("Returning Genre: "+Genre);
            return Genre;
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
