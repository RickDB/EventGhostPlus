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
using Microsoft.Win32;


[assembly: CompatibleVersion("1.15.100.0", "1.15.100.0")]
[assembly: UsesSubsystem("MP.Plugins")]
[assembly: UsesSubsystem("MP.Config")]
[assembly: UsesSubsystem("MP.Input.Mapping")]
[assembly: UsesSubsystem("MP.Players")]

namespace EventGhostPlus
{
    public class QueueRec
    {
        public string Header;
        public string Line1;
        public string Line2;
        public int Timeout;
        public string Image;
    }

    public class QueueHandler
    {
        public delegate void DisplayedMessage(string message);

        public static event DisplayedMessage OnMessageDisplay;
        public static event DisplayedMessage OnMessageClose;

        public void ShowQueue()
        {
            if (EventGhostPlus.DebugMode) Logger.Debug("Mark message dialog busy");
            EventGhostPlus.DialogBusy = true;
            var qi = new QueueRec();
            while (EventGhostPlus.Queue.Count > 0)
            {
                if (EventGhostPlus.DebugMode)
                    Logger.Debug($"{"ARG"}Number of messages in queue: " + EventGhostPlus.Queue.Count);
                qi = EventGhostPlus.Queue[0];
                var pDlgNotify =
                    (GUIDialogNotify) GUIWindowManager.GetWindow((int) GUIWindow.Window.WINDOW_DIALOG_NOTIFY);
                if (EventGhostPlus.DebugMode) Logger.Debug("Message Header: " + qi.Header);
                pDlgNotify.SetHeading(qi.Header);
                if (EventGhostPlus.DebugMode) Logger.Debug($"Message Line 1: {qi.Line1}");
                if (EventGhostPlus.DebugMode) Logger.Debug($"Message Line 2: {qi.Line2}");
                pDlgNotify.SetText($"{qi.Line1}\n{qi.Line2}");
                if (EventGhostPlus.DebugMode) Logger.Debug($"Message given image: {qi.Image}");
                if (qi.Image.Equals(""))
                {
                    qi.Image = SkinInfo.GetMpThumbsPath() + "EventGhostPlus\\EventGhostPlusIcon.png";
                }
                else
                {
                    if (!File.Exists(qi.Image))
                        qi.Image = SkinInfo.GetMpThumbsPath() + "EventGhostPlus\\EventGhostPlusIcon.png";
                }
                if (EventGhostPlus.DebugMode) Logger.Debug($"Message processed image: {qi.Image}");
                pDlgNotify.SetImage(qi.Image);
                if (EventGhostPlus.DebugMode) Logger.Debug($"Message timeout: {qi.Timeout}");
                pDlgNotify.TimeOut = qi.Timeout;
                if (EventGhostPlus.DebugMode) Logger.Debug("Showing Message Dialog");
                OnMessageDisplay(qi.Header);
                pDlgNotify.DoModal(GUIWindowManager.ActiveWindow);
                OnMessageClose(qi.Header);
                if (EventGhostPlus.DebugMode) Logger.Debug("Message dialog closed");
                Logger.Info("Message shown.");
                EventGhostPlus.Queue.Remove(qi);
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

        public string PreviousLevel = "";
        public string PreviousMediaType = "";

        public string EgPath;
        public string EgPart2;
        public string EgPart3;
        public bool EgPayload;
        public bool WindowChange;
        public bool PwdEncrypted;

        public int SetLevelForMediaDuration;

        public bool IsTcpip;
        public string Host;
        public string Port;
        public string Password;
        public string RcvPort;
        public string RcvPassword;

        public static bool DebugMode;
        private static bool InitLock;

        readonly ProcessStartInfo _egStartInfo = new ProcessStartInfo();

        readonly WindowName _windows = new WindowName();

        EgNetworkEventReceiverSender _eg;

        #region IPluginReceiver Members

        readonly InputHandler _inputHandler = new InputHandler(PLUGIN_NAME);

        bool IPluginReceiver.WndProc(ref System.Windows.Forms.Message m)
        {
            const int wmApp = 0x8000;
            const int idMessageCommand = 0x18;
            const int wmPowerbroadcast = 0x218;
            const int idStandby = 4;
            const int idResume = 18;
            bool networkUp;

            switch (m.Msg)
            {
                case wmPowerbroadcast:
                    if (DebugMode)
                        Logger.Debug("Window Message received: WM_POWERBROADCAST WParam: " + m.WParam + " LParam: " +
                                     m.LParam);
                    if (m.WParam.ToInt32() == idStandby)
                    {
                        SendEvent("System.Standby", null);
                        SystemStandby = true;
                    }
                    if (m.WParam.ToInt32() == idResume)
                    {
                        if (IsTcpip)
                        {
                            networkUp = false;
                            while (!networkUp)
                                networkUp = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
                        }
                        SystemStandby = false;
                        SendEvent("System.Resume", null);
                    }

                    return false;

                case wmApp:
                    if (m.WParam.ToInt32() == idMessageCommand)
                    {
                        _inputHandler.MapAction(m.LParam.ToInt32() & 0xFFFF);
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
            using (var xmlReader = new Settings(Config.GetFile(Config.Dir.Config, "MediaPortal.xml")))
            {
                EgPath = xmlReader.GetValueAsString(PLUGIN_NAME, "egPath", "");
                EgPart2 = xmlReader.GetValueAsString(PLUGIN_NAME, "egPart2", "Type");
                EgPart3 = xmlReader.GetValueAsString(PLUGIN_NAME, "egPart3", "Status");
                EgPayload = xmlReader.GetValueAsBool(PLUGIN_NAME, "egPayload", false);
                WindowChange = xmlReader.GetValueAsBool(PLUGIN_NAME, "WindowChange", false);

                IsTcpip = xmlReader.GetValueAsBool(PLUGIN_NAME, "tcpipIsEnabled", false);
                Host = xmlReader.GetValueAsString(PLUGIN_NAME, "tcpipHost", "");
                Port = xmlReader.GetValueAsString(PLUGIN_NAME, "tcpipPort", "");
                Password = xmlReader.GetValueAsString(PLUGIN_NAME, "tcpipPassword", "");
                PwdEncrypted = xmlReader.GetValueAsBool(PLUGIN_NAME, "PWDEncrypted", false);

                RcvPort = xmlReader.GetValueAsString(PLUGIN_NAME, "ReceivePort", "1023");
                RcvPassword = xmlReader.GetValueAsString(PLUGIN_NAME, "ReceivePassword", "");

                SetLevelForMediaDuration = xmlReader.GetValueAsInt(PLUGIN_NAME, "setLevelForMediaDuration", 10);

                DebugMode = xmlReader.GetValueAsBool(PLUGIN_NAME, "DebugMode", false);
            }
            _egStartInfo.CreateNoWindow = false;
            _egStartInfo.UseShellExecute = true;
            if (DebugMode) Logger.Debug("EventGhost path: " + EgPath + @"\EventGhost.exe");
            _egStartInfo.FileName = EgPath + @"\EventGhost.exe";
            _egStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
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

        public bool GetHome(out string strButtonText, out string strButtonImage, out string strButtonImageFocus,
            out string strPictureImage)
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
            Init();
        }
        void IPlugin.Stop()
        {
            Dispose();
        }

        #endregion

        #region EG_Network_Event_Receiver_Sender

        void Init(bool firsStart = true)
        {
            if (InitLock)
                return;

            try
            {
                InitLock = true;
                if(firsStart)
                    Logger.Info($"Starting {PLUGIN_NAME} - version: {Assembly.GetExecutingAssembly().GetName().Version}");

                if (DebugMode) Logger.Debug($"Creating socket listener on port: {RcvPort}");

                _eg = new EgNetworkEventReceiverSender($"*:{RcvPort}", Dpapi.DecryptString(RcvPassword), false);
                _eg.EventFromNetworkEventSender += eg_Event_FromNetworkEventSender;
                g_Player.PlayBackStarted += OnVideoStarted;
                g_Player.PlayBackEnded += OnVideoEnded;
                g_Player.PlayBackStopped += OnVideoStopped;
                GUIWindowManager.OnNewAction += GUIWindowManager_OnNewAction;
                GUIWindowManager.OnActivateWindow += GUIWindowManager_OnActivateWindow;
                QueueHandler.OnMessageDisplay += QueueHandler_OnMessageDisplay;
                QueueHandler.OnMessageClose += QueueHandler_OnMessageClose;
                SendEvent("Plugin.Start", null);

                InitLock = false;
            }
            catch (Exception ex)
            {
                InitLock = false;;
                Log.Error($"Eventghost plus - error occured during Init(): {ex}");
            }
        }

        void ReInit()
        {
            if (InitLock)
                return;
            try
            {
                Thread.Sleep(250);
                _eg.StopLocalNetworkEventReceiver();
                InitLock = true;
                Dispose();
                Thread.Sleep(250);
                Init(false);
                InitLock = false;
            }
            catch (Exception ex)
            {
                InitLock = false; ;
                Log.Error($"Eventghost plus - error occured during ReInit(): {ex}");
            }
        }

        void Dispose()
        {
            Logger.Info("Stopping");
            SendEvent("Plugin.Stop", null);
            if (DebugMode) Logger.Debug("Stopping socket listener.");
            _eg.StopLocalNetworkEventReceiver();
        }

        void eg_Event_FromNetworkEventSender(object sender, NetWorkEventReceiverEventArgs e)
        {
            Logger.Info("Message received.");
            if (DebugMode) Logger.Debug("Network Event : " + e.Name);
            if (e.Name.Equals("MediaPortal.Message"))
                if (e.Payload[0] == "EG+BtnSnd")
                {
                    if (DebugMode) Logger.Debug("Remote Button Received: " + e.Payload[1]);
                    _inputHandler.MapAction(Convert.ToInt32(e.Payload[3]) & 0xFFFF);
                }
                else
                {
                    if (DebugMode) Logger.Debug("Payload count: " + e.Payload.Count);
                    foreach (var k in e.Payload)
                        if (DebugMode) Logger.Debug("  payload = " + k);
                    var header = e.Payload[0];
                    var line1 = e.Payload[1];
                    var line2 = e.Payload[2];
                    var timeout = 60;
                    var image = "";
                    if (e.Payload.Count > 3)
                    {
                        if (DebugMode) Logger.Debug("More than 3 payloads");
                        if (e.Payload[3] != null && e.Payload[3] != "")
                            timeout = Convert.ToInt16(e.Payload[3]);
                        else
                            timeout = 60;
                    }
                    if (DebugMode) Logger.Debug("Received Message Timeout: " + timeout);
                    if (e.Payload.Count > 4)
                    {
                        if (DebugMode) Logger.Debug("More than 4 payloads");
                        if (e.Payload[4] != null && e.Payload[4] != "")
                            image = e.Payload[4];
                    }
                    if (DebugMode) Logger.Debug("ImageLocation: " + image);
                    var qi = new QueueRec();
                    qi.Header = header;
                    qi.Line1 = line1;
                    qi.Line2 = line2;
                    qi.Timeout = timeout;
                    qi.Image = image;
                    if (DebugMode) Logger.Debug("Add Message to Queue");
                    Queue.Add(qi);
                    if (!DialogBusy)
                    {
                        if (DebugMode) Logger.Debug("Dialog is not busy, fire ShowQueue Thread");
                        var q = new QueueHandler();
                        var qThread = new Thread(q.ShowQueue);
                        qThread.Start();
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
                else if (g_Player.Playing)
                {
                    if (DebugMode) Logger.Debug("Action is Resume");
                    if (g_Player.Playing) SetLevelForPlayback(GetCurrentMediaType(), "Resume");
                }
            }
            else if (action.wID == MediaPortal.GUI.Library.Action.ActionType.ACTION_PLAY ||
                     action.wID == MediaPortal.GUI.Library.Action.ActionType.ACTION_MUSIC_PLAY)
            {
                if (DebugMode) Logger.Debug("Action is Play");
                SetLevelForPlayback(GetCurrentMediaType(), "Play");
            }
        }


        public void GUIWindowManager_OnActivateWindow(int windowId)
        {
            if (WindowChange)
            {
                if (DebugMode) Logger.Debug("Window Activated: " + windowId);
                var windowName = "";
                windowName = _windows.GetName(windowId);
                if (DebugMode) Logger.Debug("Window name: " + windowName);
                if (!windowName.Equals(""))
                    SendEvent("Window.Activate", new[] {windowId.ToString(), "\"" + windowName + "\""});
                else
                    SendEvent("Window.Activate", new[] {windowId.ToString()});
            }
        }

        public void QueueHandler_OnMessageDisplay(string message)
        {
            SendEvent("Message.Displaying", new[] {message});
            if (DebugMode) Logger.Debug("Displaying Message Trigger: " + message);
        }

        public void QueueHandler_OnMessageClose(string message)
        {
            SendEvent("Message.Closed", new[] {message});
            if (DebugMode) Logger.Debug("Closed Message Trigger: " + message);
        }

        #endregion

        private string GetCurrentMediaType()
        {
            if (g_Player.IsMusic)
            {
                if (DebugMode) Logger.Debug("Media is Music");
                return "Music";
            }
            if (g_Player.IsRadio)
            {
                if (DebugMode) Logger.Debug("Media is Radio");
                return "Radio";
            }
            if (g_Player.IsTVRecording)
            {
                if (DebugMode) Logger.Debug("Media is Recording");
                return "Recording";
            }
            if (g_Player.IsTV)
            {
                if (DebugMode) Logger.Debug("Media is TV");
                return "TV";
            }
            if (DebugMode) Logger.Debug("Media is Video");
            return "Video";
        }

        private void SetLevelForPlayback(string mediaType, string level)
        {
            if ((PreviousLevel == "Play" || PreviousLevel == "Pause") && level == "Play" &&
                PreviousMediaType != mediaType)
                PreviousLevel = "Stop";
            string part2String;
            string part3String;
            var lengthString = "";
            var genre = "";
            var currentFile = "";
            var searchFile = "";
            if (!mediaType.Equals("Plugin"))
            {
                if (mediaType == g_Player.MediaType.Video.ToString() ||
                    mediaType == g_Player.MediaType.Recording.ToString())
                    if (g_Player.Duration < SetLevelForMediaDuration * 60)
                    {
                        if (DebugMode) Logger.Debug("Length is Short");
                        lengthString = "Short.";
                    }
                    else
                    {
                        if (DebugMode) Logger.Debug("Length is Long");
                        lengthString = "Long.";
                    }
                if (g_Player.IsDVD)
                {
                    if (DebugMode) Logger.Debug("Length is Long (Media is DVD)");
                    lengthString = "Long.";
                }

                if (level == "Play")
                {
                    currentFile = g_Player.Player.CurrentFile;
                    searchFile = currentFile;
                    if (g_Player.IsMusic)
                    {
                        var song = new Song();
                        var musicDatabase = MusicDatabase.Instance;
                        musicDatabase.GetSongByFileName(searchFile, ref song);
                        if (song != null)
                            genre = song.Genre;
                    }
                    if (g_Player.IsVideo)
                        if (!searchFile.StartsWith("http://localhost/")) // Online Video is not in DB so skip DB Search
                        {
                            try
                            {
                                if (DebugMode) Logger.Debug("Check to see if the video is a mounted disc.");
                                searchFile = MountHelper.CheckMount(ref searchFile);
                            }
                            catch
                            {
                                Logger.Warning("Daemontools not installed/configured");
                            }
                            if (DebugMode) Logger.Debug("Check to see if video is in MyVideos database.");
                            var movie = new IMDBMovie();
                            var movieId = VideoDatabase.GetMovieId(searchFile);
                            VideoDatabase.GetMovieInfoById(movieId, ref movie);
                            if (movie.ID > 0)
                            {
                                if (DebugMode) Logger.Debug("Video is in MyVideos database.");
                                genre = movie.Genre;
                            }
                            else // Movie not in MyVideo's DB
                            {
                                if (DebugMode) Logger.Debug("Video is not in MyVideos database.");
                                if (DebugMode) Logger.Debug("Check to see if video is in MovingPictures database.");
                                try
                                {
                                    if (genre.Equals("")) genre = MovingPicturesHelper.CheckDb(ref searchFile);
                                }
                                catch
                                {
                                    Logger.Warning(
                                        "Error while searching MovingPictures Database, probaly not installed");
                                }
                                if (DebugMode) Logger.Debug("Check to see if video is in TVSeries database.");
                                try
                                {
                                    if (genre.Equals("")) genre = TvSeriesHelper.CheckDb(searchFile);
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
            part2String = EgPart2.Equals("Type") ? mediaType : level;
            part3String = EgPart3.Equals("Type") ? mediaType : level;
            genre = genre != "" ? "\"" + genre + "\"" : "";
            searchFile = searchFile != "" ? "\"" + searchFile + "\"" : "";
            if (DebugMode) Logger.Debug("ACTION " + level + " Media: " + mediaType);
            if (searchFile != "") if (DebugMode) Logger.Debug("Filename: " + searchFile);
            if (genre != "") if (DebugMode) Logger.Debug("Genre: " + genre);
            if (lengthString != "") if (DebugMode) Logger.Debug("Length: " + lengthString);
            if (level.Equals("Play"))
            {
                if (EgPayload)
                    SendEvent(lengthString + part2String, new[] {part3String, searchFile, genre});
                else
                    SendEvent(lengthString + part2String + "." + part3String, new[] {searchFile, genre});
            }
            else
            {
                if (EgPayload)
                    SendEvent(lengthString + part2String, new[] {part3String});
                else
                    SendEvent(lengthString + part2String + "." + part3String, new string[] {});
            }
            PreviousLevel = level;
            PreviousMediaType = mediaType;
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
                    if (!IsTcpip)
                    {
                        if (DebugMode) Logger.Debug("Submitting event locally.");
                        _egStartInfo.Arguments = "-e " + Event;
                        if (payload != null && payload.Length > 0)
                            foreach (var s in payload)
                            {
                                if (DebugMode) Logger.Debug("Payload: " + s);
                                _egStartInfo.Arguments = _egStartInfo.Arguments + " " + s;
                            }
                        try
                        {
                            if (DebugMode) Logger.Debug("arguments to run: " + _egStartInfo.Arguments);
                            var egExeProcess = Process.Start(_egStartInfo);
                        }
                        catch
                        {
                            Logger.Error("Error starting EventGhost, EventGhost path probably not set, run setup.");
                        }
                    }
                    else
                    {
                        if (DebugMode) Logger.Debug("Submitting event over Network.");
                        if (PwdEncrypted)
                        {
                            if (DebugMode)
                            {
                                Logger.Debug("Password is encrypted.");
                                Logger.Debug("Submitting event to server: " + Host + ":" + Port);
                                if (payload != null)
                                    foreach (var s in payload)
                                        if (DebugMode) Logger.Debug("Payload: " + s);
                            }
                            if ("success" !=
                                (status =
                                    _eg.SendEventToNetworkEventReceiver($"{Host}:{Port}", Dpapi.DecryptString(Password),
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
                                Logger.Debug($"Submitting event to server: {Host}:{Port}");
                                if (payload != null)
                                    foreach (var s in payload)
                                        if (DebugMode) Logger.Debug("Payload: " + s);
                            }
                            if ("success" !=
                                (status =
                                    _eg.SendEventToNetworkEventReceiver($"{Host}:{Port}", Password, Event, payload)))
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

        #region  power state monitoring
        public void PowerModeChanged(PowerModes powerMode)
        {
            if (powerMode == PowerModes.Resume)
            {
                // Wait for network to start up
                Thread.Sleep(15000);
                ReInit();
            }
        }


        #endregion
    }
}