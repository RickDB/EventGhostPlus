using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using MediaPortal.Profile;
using MediaPortal.Configuration;
using MediaPortal.GUI.Library;

namespace EventGhostPlus
{
    public static class Logger
    {
        private static string _logFilename = Config.GetFile(Config.Dir.Log, "EventGhostPlus.log");
        private static string _backupFilename = Config.GetFile(Config.Dir.Log, "EventGhostPlus.bak");
        private static object _lockObject = new object();

        static Logger()
        {
            if (File.Exists(_logFilename))
            {
                if (File.Exists(_backupFilename))
                {
                    try
                    {
                        File.Delete(_backupFilename);
                    }
                    catch
                    {
                        Error("Failed to remove old backup log");
                    }
                }
                try
                {
                    File.Move(_logFilename, _backupFilename);
                }
                catch
                {
                    Error("Failed to move logfile to backup");
                }
            }
        }

        public static void Info(String log)
        {
                WriteToFile(String.Format(CreatePrefix(), "Info", log));
        }

        public static void Debug(String log)
        {
                WriteToFile(String.Format(CreatePrefix(), "Debug", log));
        }

        public static void Error(String log)
        {
            WriteToFile(String.Format(CreatePrefix(), "Error", log));
            Log.Error("EventGhostPlus: " + log);
        }

        public static void Warning(String log)
        {
                WriteToFile(String.Format(CreatePrefix(), "Warning", log));
        }

        private static String CreatePrefix()
        {
            return DateTime.Now + "[{0}] {1}";
        }

        private static void WriteToFile(String log)
        {
            try
            {
                lock (_lockObject)
                {
                    StreamWriter sw = File.AppendText(_logFilename);
                    sw.WriteLine(log);
                    sw.Close();
                }
            }
            catch
            {
                Error("Failed to write out to log");
            }
        }
    }
}