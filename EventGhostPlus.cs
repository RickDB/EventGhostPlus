
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.IO;

using MediaPortal.GUI.Library;
using MediaPortal.Player;
using MediaPortal.Configuration;
using MediaPortal.Video.Database;
using MediaPortal.Music.Database;
using MediaPortal.Common.Utils;
using MediaPortal.InputDevices;
using MediaPortal.Profile;
using MediaPortal.Dialogs;
using System.Threading;


[assembly: CompatibleVersion("1.3.0.0", "1.1.6.27644")]
[assembly: UsesSubsystem("MP.Plugins")]
[assembly: UsesSubsystem("MP.Config")]
[assembly: UsesSubsystem("MP.Input.Mapping")]
[assembly: UsesSubsystem("MP.Players")]

namespace EventGhostPlus
{
    public class QueueRec
    {
        public string header;
        public string line1;
        public string line2;
        public int timeout;
        public string image;
    }

    public class QueueHandler
    {
        public delegate void DisplayedMessage(string Message);
        public static event DisplayedMessage OnMessageDisplay;
        public static event DisplayedMessage OnMessageClose;
        
        public void ShowQueue()
        {
            if (EventGhostPlus.DebugMode) Logger.Debug("Mark message dialog busy");
            EventGhostPlus.DialogBusy = true;
            var QI = new QueueRec();
            while (EventGhostPlus.Queue.Count > 0)
            {
                if (EventGhostPlus.DebugMode) Logger.Debug("Number of messages in queue: " + EventGhostPlus.Queue.Count);
                QI = EventGhostPlus.Queue[0];
                var pDlgNotify =(GUIDialogNotify) GUIWindowManager.GetWindow((int) GUIWindow.Window.WINDOW_DIALOG_NOTIFY);
                if (EventGhostPlus.DebugMode) Logger.Debug("Message Header: " + QI.header);
                pDlgNotify.SetHeading(QI.header);
                if (EventGhostPlus.DebugMode) Logger.Debug("Message Line 1: " + QI.line1);
                if (EventGhostPlus.DebugMode) Logger.Debug("Message Line 2: " + QI.line2);
                pDlgNotify.SetText(QI.line1 + "\n" + QI.line2);
                if (EventGhostPlus.DebugMode) Logger.Debug("Message given image: " + QI.image);
                if (QI.image.Equals(""))
                {
                    QI.image = SkinInfo.GetMPThumbsPath() + "EventGhostPlus\\EventGhostPlusIcon.png";
                }
                else
                {
                    if (!File.Exists(QI.image))
                    {
                        QI.image = SkinInfo.GetMPThumbsPath() + "EventGhostPlus\\EventGhostPlusIcon.png";
                    }
                }
                if (EventGhostPlus.DebugMode) Logger.Debug("Message processed image: " + QI.image);
                pDlgNotify.SetImage(QI.image);
                if (EventGhostPlus.DebugMode) Logger.Debug("Message timeout: " + QI.timeout.ToString());
                pDlgNotify.TimeOut = QI.timeout;
                if (EventGhostPlus.DebugMode) Logger.Debug("Showing Message Dialog");
                OnMessageDisplay(QI.header);
                pDlgNotify.DoModal(GUIWindowManager.ActiveWindow);
                OnMessageClose(QI.header);
                if (EventGhostPlus.DebugMode) Logger.Debug("Message dialog closed");
                Logger.Info("Message shown.");
                EventGhostPlus.Queue.Remove(QI);
                if (EventGhostPlus.DebugMode) Logger.Debug("Message removed");
            }
            EventGhostPlus.DialogBusy = false;
            if (EventGhostPlus.DebugMode) Logger.Debug("Mark message dialog free");
        }
    }

    [PluginIcons("EventGhostPlus.Resources.EventGhostPlus.png", "EventGhostPlus.Resources.EventGhostPlusDisabled.png")]
    public class EventGhostPlus : IPlugin, ISetupForm, IPluginReceiver
    {
        public static bool SystemStandby;
        public static bool DialogBusy;
        public static bool Sending;
        public static List<QueueRec> Queue = new List<QueueRec>();
        public const string PLUGIN_NAME = "EventGhostPlus";

        public string previousLevel = "";
        public string previousMediaType = "";

        public string egPath;
        public string egPart2;
        public string egPart3;
        public bool egPayload;
        public bool WindowChange;
        public bool PWDEncrypted;

        public int setLevelForMediaDuration;

        public bool isTcpip;
        public string Host;
        public string Port;
        public string Password;
        public string rcvPort;
        public string rcvPassword;

        public static bool DebugMode;


        ProcessStartInfo egStartInfo = new ProcessStartInfo();

        WindowName Windows = new WindowName();

        EG_Network_Event_Receiver_Sender eg;

        #region IPluginReceiver Members

        InputHandler inputHandler = new InputHandler(PLUGIN_NAME);

        bool IPluginReceiver.WndProc(ref System.Windows.Forms.Message m)
        {
            const int WM_APP = 0x8000;
            const int ID_MESSAGE_COMMAND = 0x18;
            const int WM_POWERBROADCAST = 0x218;
            const int ID_STANDBY = 4;
            const int ID_RESUME = 18;
            bool networkUp;

            switch (m.Msg)
            {
                case WM_POWERBROADCAST:
                    if (DebugMode) Logger.Debug("Window Message received: WM_POWERBROADCAST WParam: " + m.WParam.ToString() + " LParam: " + m.LParam.ToString());
                    if (m.WParam.ToInt32() == ID_STANDBY)
                    {
                        SendEvent("System.Standby", null);
                        SystemStandby = true;
                    }
                    if (m.WParam.ToInt32() == ID_RESUME)
                    {
                        if (isTcpip)
                        {
                            networkUp = false;
                            while (!networkUp)
                            {
                                networkUp = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
                            }
                        }
                        SystemStandby = false;
                        SendEvent("System.Resume", null);
                    }

                    return false;

                case WM_APP:
                    if (m.WParam.ToInt32() == ID_MESSAGE_COMMAND)
                    {
                        inputHandler.MapAction(m.LParam.ToInt32() & 0xFFFF);
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }
        #endregion

       
        public EventGhostPlus()
        {
            SystemStandby = false;
            Sending = false;
            using (Settings xmlReader = new Settings(Config.GetFile(Config.Dir.Config, "MediaPortal.xml")))
            {
                egPath = xmlReader.GetValueAsString(PLUGIN_NAME, "egPath", "");
                egPart2 = xmlReader.GetValueAsString(PLUGIN_NAME, "egPart2", "Type");
                egPart3 = xmlReader.GetValueAsString(PLUGIN_NAME, "egPart3", "Status");
                egPayload = xmlReader.GetValueAsBool(PLUGIN_NAME, "egPayload", false);
                WindowChange = xmlReader.GetValueAsBool(PLUGIN_NAME, "WindowChange", false);

                isTcpip = xmlReader.GetValueAsBool(PLUGIN_NAME, "tcpipIsEnabled", false);
                Host = xmlReader.GetValueAsString(PLUGIN_NAME, "tcpipHost", "");
                Port = xmlReader.GetValueAsString(PLUGIN_NAME, "tcpipPort", "");
                Password = xmlReader.GetValueAsString(PLUGIN_NAME, "tcpipPassword", "");
                PWDEncrypted = xmlReader.GetValueAsBool(PLUGIN_NAME, "PWDEncrypted", false);

                rcvPort = xmlReader.GetValueAsString(PLUGIN_NAME, "ReceivePort", "1023");
                rcvPassword = xmlReader.GetValueAsString(PLUGIN_NAME, "ReceivePassword", "");

                setLevelForMediaDuration = xmlReader.GetValueAsInt(PLUGIN_NAME, "setLevelForMediaDuration", 10);

                DebugMode = xmlReader.GetValueAsBool(PLUGIN_NAME, "DebugMode", false);
            }
            egStartInfo.CreateNoWindow = false;
            egStartInfo.UseShellExecute = true;
            if (DebugMode) Logger.Debug("EventGhost path: " + @egPath + @"\EventGhost.exe");
            egStartInfo.FileName = @egPath + @"\EventGhost.exe";
            egStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            DialogBusy = false;
        }

        #region ISetupForm Members

        public string Author()
        {
            return "Da BIG One";
        }

        public bool CanEnable()
        {
            return true;
        }

        public bool DefaultEnabled()
        {
            return true;
        }

        public string Description()
        {
            return "Interact with EventGhost";
        }

        public bool GetHome(out string strButtonText, out string strButtonImage, out string strButtonImageFocus, out string strPictureImage)
        {
            strButtonText = strButtonImage = strButtonImageFocus = strPictureImage = null;
            return false;
        }

        public int GetWindowId()
        {
            return 6658;
        }

        public bool HasSetup()
        {
            return true;
        }

        public string PluginName()
        {
            return PLUGIN_NAME;
        }

        public void ShowPlugin()
        {
            var frm = new FormSettings();
            frm.ShowDialog();
        }

        
        #endregion


        #region IPlugin Members
        void IPlugin.Start()
        {
            Logger.Info("Starting "+PLUGIN_NAME + " version " + Assembly.GetExecutingAssembly().GetName().Version);
            if (DebugMode) Logger.Debug("Creating socket listener on port: " + rcvPort);
            eg = new EG_Network_Event_Receiver_Sender("*:" + rcvPort, DPAPI.DecryptString(rcvPassword), false);
            eg.Event_FromNetworkEventSender += new EG_Network_Event_Receiver_Sender.Event_FromNetworkEventSender_Handler(eg_Event_FromNetworkEventSender);
            g_Player.PlayBackStarted += new g_Player.StartedHandler(OnVideoStarted);
            g_Player.PlayBackEnded += new g_Player.EndedHandler(OnVideoEnded);
            g_Player.PlayBackStopped += new g_Player.StoppedHandler(OnVideoStopped);
            GUIWindowManager.OnNewAction += new OnActionHandler(GUIWindowManager_OnNewAction);
            GUIWindowManager.OnActivateWindow += new GUIWindowManager.WindowActivationHandler(GUIWindowManager_OnActivateWindow);
            QueueHandler.OnMessageDisplay += new QueueHandler.DisplayedMessage(QueueHandler_OnMessageDisplay);
            QueueHandler.OnMessageClose += new QueueHandler.DisplayedMessage(QueueHandler_OnMessageClose);
            SendEvent("Plugin.Start", null);
        }
        void IPlugin.Stop()
        {
            Logger.Info("Stopping");
            SendEvent("Plugin.Stop", null);
            if (DebugMode) Logger.Debug("Stopping socket listener.");
            eg.StopLocalNetworkEventReceiver();
        }
        #endregion

        #region EG_Network_Event_Receiver_Sender

        void eg_Event_FromNetworkEventSender(object sender, NetWorkEventReceiver_EventArgs e)
        {
            Logger.Info("Message received.");
            if (DebugMode) Logger.Debug("Network Event : " + e.name);
            if (e.name.Equals("MediaPortal.Message"))
            {
                if (e.payload[0] == "EG+BtnSnd")
                {
                    if (DebugMode) Logger.Debug("Remote Button Received: " + e.payload[1]);
                    inputHandler.MapAction(Convert.ToInt32(e.payload[3]) & 0xFFFF);
                }
                else
                {
                    if (DebugMode) Logger.Debug("Payload count: " + e.payload.Count.ToString());
                    foreach (string k in e.payload)
                    {
                        if (DebugMode) Logger.Debug("  payload = " + k);
                    }
                    string header = e.payload[0];
                    string line1 = e.payload[1];
                    string line2 = e.payload[2];
                    int timeout = 60;
                    string image = "";
                    if (e.payload.Count > 3)
                    {
                        if (DebugMode) Logger.Debug("More than 3 payloads");
                        if ((e.payload[3] != null) && (e.payload[3] != ""))
                        {
                            timeout = Convert.ToInt16(e.payload[3]);
                        }
                        else
                        {
                            timeout = 60;
                        }
                    }
                    if (DebugMode) Logger.Debug("Received Message Timeout: " + timeout.ToString());
                    if (e.payload.Count > 4)
                    {
                        if (DebugMode) Logger.Debug("More than 4 payloads");
                        if ((e.payload[4] != null) && (e.payload[4] != ""))
                        {
                            image = e.payload[4];
                        }
                    }
                    if (DebugMode) Logger.Debug("ImageLocation: " + image);
                    var QI = new QueueRec();
                    QI.header = header;
                    QI.line1 = line1;
                    QI.line2 = line2;
                    QI.timeout = timeout;
                    QI.image = image;
                    if (DebugMode) Logger.Debug("Add Message to Queue");
                    Queue.Add(QI);
                    if (!EventGhostPlus.DialogBusy)
                    {
                        if (DebugMode) Logger.Debug("Dialog is not busy, fire ShowQueue Thread");
                        var Q = new QueueHandler();
                        var QThread = new Thread(Q.ShowQueue);
                        QThread.Start();
                    }
                }
            }
        }

        #endregion

        #region Playback events
        public void OnVideoStarted(g_Player.MediaType type, string s)
        {
            if (DebugMode) Logger.Debug("Action is Play");
            SetLevelForPlayback(type.ToString(), "Play");
        }

        public void OnVideoEnded(g_Player.MediaType type, string s)
        {
            if (DebugMode) Logger.Debug("Action is End");
            SetLevelForPlayback(type.ToString(), "End");
        }

        public void OnVideoStopped(g_Player.MediaType type, int i, string s)
        {
            if (DebugMode) Logger.Debug("Action is Stop");
            SetLevelForPlayback(GetCurrentMediaType(), "Stop");
        }

        void GUIWindowManager_OnNewAction(MediaPortal.GUI.Library.Action action)
        {
            if (action.wID == MediaPortal.GUI.Library.Action.ActionType.ACTION_PAUSE)
            {
                if (DebugMode) Logger.Debug("Action is Pause, detecting if playing or paused");
                if (g_Player.Paused)
                {
                    if (DebugMode) Logger.Debug("Action is Pause");
                    SetLevelForPlayback(GetCurrentMediaType(), "Pause");
                }
                else
                if (g_Player.Playing)
                {
                    if (DebugMode) Logger.Debug("Action is Resume");
                    if (g_Player.Playing) SetLevelForPlayback(GetCurrentMediaType(), "Resume");
                }
            }
            else if (action.wID == MediaPortal.GUI.Library.Action.ActionType.ACTION_PLAY || action.wID == MediaPortal.GUI.Library.Action.ActionType.ACTION_MUSIC_PLAY)
            {
                if (DebugMode) Logger.Debug("Action is Play");
                SetLevelForPlayback(GetCurrentMediaType(), "Play");
            }
        }

        
        public void GUIWindowManager_OnActivateWindow(int windowID)
        {
            if (WindowChange)
            {
                if (DebugMode) Logger.Debug("Window Activated: " + windowID.ToString());
                string WindowName = "";
                WindowName = Windows.GetName(windowID);
                if (DebugMode) Logger.Debug("Window name: " + WindowName);
                if (!WindowName.Equals(""))
                {
                    SendEvent("Window.Activate", new string[] {windowID.ToString(), "\"" + WindowName + "\""});
                }
                else
                {
                    SendEvent("Window.Activate", new string[] {windowID.ToString()});
                }
            }
        }

        public void QueueHandler_OnMessageDisplay(string Message)
        {   
            SendEvent("Message.Displaying", new string[] { Message });
            if (DebugMode) Logger.Debug("Displaying Message Trigger: " + Message);
        }

        public void QueueHandler_OnMessageClose(string Message)
        {
            SendEvent("Message.Closed", new string[] { Message });
            if (DebugMode) Logger.Debug("Closed Message Trigger: " + Message);
        }

        #endregion


        private string GetCurrentMediaType()
        {
            if (g_Player.IsMusic)
            {
                if (DebugMode) Logger.Debug("Media is Music");
                return ("Music");
            }
            else if (g_Player.IsRadio)
            {
                if (DebugMode) Logger.Debug("Media is Radio");
                return ("Radio");
            }
            else if (g_Player.IsTVRecording)
            {
                if (DebugMode) Logger.Debug("Media is Recording");
                return ("Recording");
            }
            else if (g_Player.IsTV)
            {
                if (DebugMode) Logger.Debug("Media is TV");
                return ("TV");
            }
            else
            {
                if (DebugMode) Logger.Debug("Media is Video");
                return ("Video");
            }
        }

        private void SetLevelForPlayback(string mediaType, string Level)
        {
            if ((previousLevel == "Play" || previousLevel == "Pause") && (Level == "Play") && (previousMediaType != mediaType))
            {
                previousLevel = "Stop";
            }
            string part2String;
            string part3String;
            string lengthString = "";
            string Genre = "";
            string CurrentFile = "";
            string SearchFile = "";
            if (!mediaType.Equals("Plugin"))
            {
                if ((mediaType == g_Player.MediaType.Video.ToString()) || mediaType == g_Player.MediaType.Recording.ToString())
                {
                    if (g_Player.Duration < (setLevelForMediaDuration * 60))
                    {
                        if (DebugMode) Logger.Debug("Length is Short");
                        lengthString = "Short.";
                    }
                    else
                    {
                        if (DebugMode) Logger.Debug("Length is Long");
                        lengthString = "Long.";
                    }
                }
                if (g_Player.IsDVD)
                {
                    if (DebugMode) Logger.Debug("Length is Long (Media is DVD)");
                    lengthString = "Long.";
                }

                if (Level == "Play")
                {
                    CurrentFile = g_Player.Player.CurrentFile;
                    SearchFile = CurrentFile;
                    if (g_Player.IsMusic)
                    {
                        Song song = new Song();
                        MusicDatabase musicDatabase = MusicDatabase.Instance;
                        musicDatabase.GetSongByFileName(SearchFile, ref song);
                        if (song != null)
                        {
                            Genre = song.Genre.ToString();
                        }
                    }
                    if (g_Player.IsVideo)
                    {
                        if (!SearchFile.StartsWith("http://localhost/")) // Online Video is not in DB so skip DB Search
                        {
                            try
                            {
                                if (DebugMode) Logger.Debug("Check to see if the video is a mounted disc.");
                                SearchFile = MountHelper.CheckMount(ref SearchFile);
                            }
                            catch
                            {
                                Logger.Warning("Daemontools not installed/configured");
                            }
                            if (DebugMode) Logger.Debug("Check to see if video is in MyVideos database.");
                            IMDBMovie movie = new IMDBMovie();
                            int movieID = VideoDatabase.GetMovieId(SearchFile);
                            VideoDatabase.GetMovieInfoById(movieID, ref movie);
                            if (movie.ID > 0)
                            {
                                if (DebugMode) Logger.Debug("Video is in MyVideos database.");
                                Genre = movie.Genre.ToString();
                            }
                            else // Movie not in MyVideo's DB
                            {
                                if (DebugMode) Logger.Debug("Video is not in MyVideos database.");
                                if (DebugMode) Logger.Debug("Check to see if video is in MovingPictures database.");
                                try
                                {
                                    if (Genre.Equals("")) Genre = MovingPicturesHelper.CheckDB(ref SearchFile);
                                }
                                catch
                                {
                                    Logger.Warning("Error while searching MovingPictures Database, probaly not installed");
                                }
                                if (DebugMode) Logger.Debug("Check to see if video is in TVSeries database.");
                                try
                                {
                                    if (Genre.Equals("")) Genre = TVSeriesHelper.CheckDB(SearchFile);
                                }
                                catch
                                {
                                    Logger.Warning("Error while searching TVSeries Database, probaly not installed");
                                }
                            }
                        }
                        else
                        {
                            if (DebugMode) Logger.Debug("Media is OnlineVideo");
                        }
                    }
                }
            }
            part2String = (egPart2.Equals("Type")) ? mediaType : Level;
            part3String = (egPart3.Equals("Type")) ? mediaType : Level;
            Genre = (Genre != "") ? "\"" + Genre + "\"" : "";
            SearchFile = (SearchFile != "") ? "\"" + SearchFile + "\"" : "";
            if (DebugMode) Logger.Debug("ACTION " + Level + " Media: " + mediaType);
            if (SearchFile != "") if (DebugMode) Logger.Debug("Filename: " + SearchFile);
            if (Genre != "") if (DebugMode) Logger.Debug("Genre: " + Genre);
            if (lengthString != "") if (DebugMode) Logger.Debug("Length: " + lengthString);
            if (Level.Equals("Play"))
            {
                if (egPayload)
                {
                    SendEvent(lengthString + part2String, new string[] {part3String, SearchFile, Genre});
                }
                else
                {
                    SendEvent(lengthString + part2String + "." + part3String, new string[] {SearchFile, Genre});
                }
            }
            else
            {
                if (egPayload)
                {
                    SendEvent(lengthString + part2String, new string[] { part3String});
                }
                else
                {
                    SendEvent(lengthString + part2String + "." + part3String, new string[] {});
                }
            }
            previousLevel = Level;
            previousMediaType = mediaType;
        }

        private void SendEvent(string Event, string[] payload)
        {
            if (!SystemStandby)
            {
                if (!Sending)
                {
                    Sending = true;
                    Event = "MediaPortal." + Event;
                    if (DebugMode) Logger.Debug("Event to be sent: " + Event);
                    string status;
                    if (!isTcpip)
                    {
                        if (DebugMode) Logger.Debug("Submitting event locally.");
                        egStartInfo.Arguments = "-e " + Event;
                        if (payload != null && payload.Length > 0)
                        {
                            foreach (string s in payload)
                            {
                                if (DebugMode) Logger.Debug("Payload: " + s);
                                egStartInfo.Arguments = egStartInfo.Arguments + " " + s;
                            }
                        }
                        try
                        {
                            if (DebugMode) Logger.Debug("arguments to run: " + egStartInfo.Arguments);
                            Process egExeProcess = Process.Start(egStartInfo);
                        }
                        catch
                        {
                            Logger.Error("Error starting EventGhost, EventGhost path probably not set, run setup.");
                        }
                    }
                    else
                    {
                        if (DebugMode) Logger.Debug("Submitting event over Network.");
                        if (PWDEncrypted)
                        {
                            if (DebugMode)
                            {
                                Logger.Debug("Password is encrypted.");
                                Logger.Debug("Submitting event to server: " + Host + ":" + Port);
                                if (payload != null)
                                {
                                    foreach (string s in payload)
                                    {
                                        if (DebugMode) Logger.Debug("Payload: " + s);
                                    }
                                }
                            }
                            if ("success" !=
                                (status =
                                 eg.SendEventToNetworkEventReceiver(Host + ":" + Port, DPAPI.DecryptString(Password),
                                                                    Event, payload)))
                            {
                                Logger.Error(status);
                            }
                            else
                            {
                                if (DebugMode) Logger.Debug("Event sent succesfull.");
                            }
                        }
                        else
                        {
                            if (DebugMode)
                            {
                                Logger.Debug("Password is not encrypted.");
                                Logger.Debug("Submitting event to server: " + Host + ":" + Port);
                                if (payload != null)
                                {
                                    foreach (string s in payload)
                                    {
                                        if (DebugMode) Logger.Debug("Payload: " + s);
                                    }
                                }
                            }
                            if ("success" !=
                                (status =
                                 eg.SendEventToNetworkEventReceiver(Host + ":" + Port, Password, Event, payload)))
                            {
                                Logger.Error(status);
                            }
                            else
                            {
                                if (DebugMode) Logger.Debug("Event sent succesfull.");
                            }
                        }
                    }
                    Sending = false;
                }
                else
                {
                    if (DebugMode) Logger.Debug("Sender busy, skipping send.");
                }
            }
            else
            {
                if (DebugMode) Logger.Debug("System in Standy, skipping send.");
            }
        }
    }
}
