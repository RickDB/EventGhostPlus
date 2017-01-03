using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using Microsoft.Win32;

namespace EventGhostPlus
{
    class SkinInfo
    {

        #region Enums

        public enum ErrorCode
        {
            Info,
            InfoQuestion,
            LoadError,
            ReadError,
            Major,
        };

        #endregion

        #region Structs


        public struct EditorPaths
        {
            public string SMPbaseDir;
            public string SkinBasePath;
            public string CacheBasePath;
            public string ConfigBasePath;
            public string LangBasePath;
            public string ConfiguredSkinPath;
            public string PluginPath;
            public string FanartBasePath;
            public string ThumbsPath;
            public string DatabasePath;
        }

        #endregion

        #region Variables
        // Private Variables
        // Protected Variables
        // Public Variables
        public static EditorPaths MpPaths = new EditorPaths();

        #endregion

        #region Constructor

        public SkinInfo()
        {
            GetMediaPortalPath(ref MpPaths);
            ReadMediaPortalDirs();
        }

        public static string GetMpThumbsPath()
        {
            return MpPaths.ThumbsPath;
        }


        #endregion

        #region Public methods

        public string ConfiguredSkin
        {
            get
            {
                return ReadMpConfiguration("skin", "name");
            }
        }

        public string MinimiseMpOnExit
        {
            get
            {
                return ReadMpConfiguration("general", "minimizeonexit");
            }
        }

        #endregion

        #region Private methods
        
        void GetMediaPortalPath(ref EditorPaths mpPaths)
        {
            string sRegRoot = "SOFTWARE";
            if (IntPtr.Size > 4)
                sRegRoot += "\\Wow6432Node";
            try
            {
                RegistryKey mediaPortalKey = Registry.LocalMachine.OpenSubKey(sRegRoot + "\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\MediaPortal\\", false);
                if (mediaPortalKey != null)
                {
                    mpPaths.SMPbaseDir = mediaPortalKey.GetValue("InstallPath").ToString();
                }
                else
                {
                    mediaPortalKey = mediaPortalKey.OpenSubKey(sRegRoot + "\\Team MediaPortal\\MediaPortal\\", false);
                    if (mediaPortalKey != null)
                    {
                        mpPaths.SMPbaseDir = mediaPortalKey.GetValue("ApplicationDir").ToString();
                    }
                    else
                        mpPaths.SMPbaseDir = null;
                }
            }
            catch (Exception e)
            {
                Logger.Error("Exception while attempting to read MediaPortal location from registry\n\nMediaPortal must be installed, is MediaPortal Installed?\n\n" + e.Message.ToString());
                mpPaths.SMPbaseDir = null;
            }
        }

        void ReadMediaPortalDirs()
        {
            // Check if user MediaPortalDirs.xml exists in Personal Directory
            string personalFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            string fMPdirs = Path.Combine(personalFolder, @"Team MediaPortal\MediaPortalDirs.xml");
            
            if (!File.Exists(fMPdirs))
                fMPdirs = MpPaths.SMPbaseDir + "\\MediaPortalDirs.xml";

            XmlDocument doc = new XmlDocument();
            if (!File.Exists(fMPdirs))
            {
                Logger.Error("Can't find MediaPortalDirs.xml \r\r" + fMPdirs);
                return;
            }
            doc.Load(fMPdirs);
            XmlNodeList nodeList = doc.DocumentElement.SelectNodes("/Config/Dir");
            foreach (XmlNode node in nodeList)
            {
                XmlNode innerNode = node.Attributes.GetNamedItem("id");
                // get the Skin base path
                if (innerNode.InnerText == "Skin")
                {
                    XmlNode path = node.SelectSingleNode("Path");
                    if (path != null)
                    {
                        MpPaths.SkinBasePath = GetMediaPortalDir(path.InnerText);
                    }
                }
                // get the Cache base path
                if (innerNode.InnerText == "Cache")
                {
                    XmlNode path = node.SelectSingleNode("Path");
                    if (path != null)
                    {
                        MpPaths.CacheBasePath = GetMediaPortalDir(path.InnerText);
                    }
                }
                // get the Config base path
                if (innerNode.InnerText == "Config")
                {
                    XmlNode path = node.SelectSingleNode("Path");
                    if (path != null)
                    {
                        MpPaths.ConfigBasePath = GetMediaPortalDir(path.InnerText);
                    }
                }
                // get the Plugin base path
                if (innerNode.InnerText == "Plugins")
                {
                    XmlNode path = node.SelectSingleNode("Path");
                    if (path != null)
                    {
                        MpPaths.PluginPath = GetMediaPortalDir(path.InnerText);
                    }
                }
                // get the Thumbs base path
                if (innerNode.InnerText == "Thumbs")
                {
                    XmlNode path = node.SelectSingleNode("Path");
                    if (path != null)
                    {
                        MpPaths.ThumbsPath = GetMediaPortalDir(path.InnerText);
                        MpPaths.FanartBasePath = MpPaths.ThumbsPath + "Skin Fanart\\";
                    }
                }
                // get the Languages base path
                if (innerNode.InnerText == "Language")
                {
                    XmlNode path = node.SelectSingleNode("Path");
                    if (path != null)
                    {
                        MpPaths.LangBasePath = GetMediaPortalDir(path.InnerText);
                    }
                }
                // get the Database base path
                if (innerNode.InnerText == "Database")
                {
                    XmlNode path = node.SelectSingleNode("Path");
                    if (path != null)
                    {
                        MpPaths.DatabasePath = GetMediaPortalDir(path.InnerText);
                    }
                }
            }
            MpPaths.ConfiguredSkinPath = MpPaths.SkinBasePath + ConfiguredSkin + "\\";
        }

        string GetMediaPortalDir(string path)
        {
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            // Replace special folder variables if they exist
            // MediaPortal currently only uses two types
            path = path.ToLower();
            path = path.Replace("%appdata%", appData);
            path = path.Replace("%programdata%", commonAppData);
            // Check if the path is not rooted ie. a custom directory (including UNC)
            // If directory is relative e.g. 'skin\', prefix with Base Dir
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(SkinInfo.MpPaths.SMPbaseDir, path);
            }
            // Check if there is a trailing backslash
            if (!path.EndsWith(@"\"))
            {
                path += @"\";
            }
            return path;
        }

        string ReadMpConfiguration(string sectionName, string entryName)
        {
            string fMPdirs = MpPaths.ConfigBasePath + "MediaPortal.xml";
            XmlDocument doc = new XmlDocument();
            if (!File.Exists(fMPdirs))
            {
                Logger.Error("Can't find MediaPortal.xml \r\r" + fMPdirs);
                return null;
            }
            doc.Load(fMPdirs);
            XmlNodeList nodeList = doc.DocumentElement.SelectNodes("/profile/section");
            foreach (XmlNode node in nodeList)
            {
                XmlNode innerNode = node.Attributes.GetNamedItem("name");
                if (innerNode.InnerText == sectionName)
                {
                    XmlNode path = node.SelectSingleNode("entry[@name=\"" + entryName + "\"]");
                    if (path != null)
                    {
                        entryName = path.InnerText;
                        return entryName;
                    }
                }
            }
            return string.Empty;
        }

        #endregion

    }
}
