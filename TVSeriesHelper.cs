using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

using WindowPlugins.GUITVSeries;


namespace EventGhostPlus
{
    public static class TvSeriesHelper
    {
        public static string CheckDb(string searchFile)
        {
            string genre = "";
            if (IsAssemblyAvailable("MP-TVSeries", new Version(2, 6, 3, 1242)))
            {
                if (EventGhostPlus.DebugMode) Logger.Debug("MP-TVSeries found, searching Database for: " + searchFile);
                try
                {
                    SQLCondition query = new SQLCondition(new DBEpisode(), DBEpisode.cFilename, searchFile, SQLConditionType.Equal);
                    List<DBEpisode> episodes = DBEpisode.Get(query);
                    if (EventGhostPlus.DebugMode) Logger.Debug("Found: "+ episodes.Count.ToString() + " episodes.");
                    if (episodes.Count > 0)
                    {
                        DBSeries s = Helper.getCorrespondingSeries(episodes[0].onlineEpisode[DBOnlineEpisode.cSeriesID]);
                        genre = s[DBOnlineSeries.cGenre];
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("Error getting info from TVSeries Database: " + e.Message);
                }
            }
            if (EventGhostPlus.DebugMode) Logger.Debug("Returning Genre: " + genre);
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
