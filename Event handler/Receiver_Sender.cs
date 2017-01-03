using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MediaPortal.GUI.Library;

namespace EventGhostPlus
{
    internal class EgNetworkEventReceiverSender
    {
        public delegate void EnduringEventFromNetworkEventSenderEndHandler(
            object sender, NetWorkEventReceiverEventArgs e);

        public delegate void EnduringEventFromNetworkEventSenderStartHandler(
            object sender, NetWorkEventReceiverEventArgs e);

        public delegate void EventFromNetworkEventSenderHandler(object sender, NetWorkEventReceiverEventArgs e);

        private readonly bool _enduringEvents;

        private readonly Dictionary<string, List<string>> _eventsReceived;
        private string _lastEventReceived;

        private readonly string _localNetworkEventReceiverAddress;
        private readonly int _localNetworkEventReceiverPort;

        private Thread _localNetworkEventReceiverThread;
        private readonly string _localPassword;
        private readonly object _locker;
        private string _stopCook;
        private bool _stopFlagForLocalNetworkEventReceiverThread;


        public EgNetworkEventReceiverSender(string localNerAddr, string localPw, bool generateEnduringEvents)
        {
            if (localNerAddr != "")
            {
                _localNetworkEventReceiverAddress = localNerAddr.Substring(0, localNerAddr.IndexOf(":"));
                if (_localNetworkEventReceiverAddress == "*") _localNetworkEventReceiverAddress = "0.0.0.0";
                _localNetworkEventReceiverPort = Convert.ToInt32(localNerAddr.Substring(localNerAddr.IndexOf(":") + 1));
                _eventsReceived = new Dictionary<string, List<string>>();
                _lastEventReceived = "";
                _locker = new object();
                StartLocalNetworkEventReceiver();
            }
            else
            {
                _localNetworkEventReceiverAddress = "";
                _localNetworkEventReceiverPort = 0;
            }
            _localPassword = localPw;
            _stopCook = new Random().Next(65536).ToString("X8").Substring(4, 4);
            _enduringEvents = generateEnduringEvents;
        }

        private bool RequestStopLocalNetworkEventReceiver
        {
            get
            {
                lock (_locker)
                {
                    return _stopFlagForLocalNetworkEventReceiverThread;
                }
            }
            set
            {
                lock (_locker)
                {
                    _stopFlagForLocalNetworkEventReceiverThread = value;
                }
            }
        }

        public event EnduringEventFromNetworkEventSenderStartHandler EnduringEventFromNetworkEventSenderStart;
        public event EnduringEventFromNetworkEventSenderEndHandler EnduringEventFromNetworkEventSenderEnd;
        public event EventFromNetworkEventSenderHandler EventFromNetworkEventSender;

        public string SendEventToNetworkEventReceiver(string remoteAddress, string pwd, string eventString,
            string[] payload)
        {
            var status = "";
            var result = "";
            Socket socket;
            var remoteIp = "";
            var remotePort = 0;

            try
            {
                if (EventGhostPlus.DebugMode) Logger.Debug("Starting Network Event Sender");

                if (remoteAddress != "")
                {
                    remoteIp = remoteAddress.Substring(0, remoteAddress.IndexOf(":"));
                    remotePort = Convert.ToInt32(remoteAddress.Substring(remoteAddress.IndexOf(":") + 1));
                }
                else
                {
                    remoteIp = "";
                    remotePort = 0;
                }

                try
                {
                    status = "trying to create the socket";
                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.ReceiveTimeout = 1000;
                    if (remotePort == 0)
                    {
                        status = "trying to connect remote host because address not provided.";
                        if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    }
                    else
                    {
                        status = "trying to connect to " + remoteIp + ":" + remotePort;
                        if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    }
                    socket.Connect(remoteIp, remotePort);

                    status = "trying to send the magic word";
                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    socket.Send(Encoding.ASCII.GetBytes("quintessence\n\r"));

                    status = "trying to receive the cookie";
                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    var buff = new byte[128];
                    socket.Receive(buff);
                    var cookie = Encoding.ASCII.GetString(buff);
                    cookie = cookie.Substring(0, cookie.IndexOf("\n"));

                    status = "trying to compute the digest";
                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    var token = cookie + ":" + pwd;
                    var digest = HashString(token) + "\n";

                    status = "trying to send the digest";
                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    socket.Send(Encoding.ASCII.GetBytes(digest));

                    status = "trying to receive the \"accept\", check the password in settings.";
                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    buff = new byte[512];
                    socket.Receive(buff);
                    var answer = Encoding.ASCII.GetString(buff);
                    answer = answer.Substring(0, answer.IndexOf("\n"));
                    if (answer == "accept")
                    {
                        if (payload != null && payload.Length > 0)
                            for (var count = 0; count < payload.Length; count++)
                            {
                                status = "trying to send payload[" + count + "]";
                                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                                socket.Send(Encoding.ASCII.GetBytes("payload " + payload[count] + "\n"));
                            }
                        status = "trying to send the eventString";
                        if (EventGhostPlus.DebugMode) Logger.Debug(status);
                        socket.Send(Encoding.ASCII.GetBytes(eventString + "\n"));

                        status = "trying to send the \"close\" (event and payload have been successfully sent)";
                        if (EventGhostPlus.DebugMode) Logger.Debug(status);
                        socket.Send(Encoding.ASCII.GetBytes("close\n"));

                        status = "trying to close the connection (event and payload have been successfully sent)";
                        if (EventGhostPlus.DebugMode) Logger.Debug(status);
                        socket.Close();
                        status = "success";
                        if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    }
                    else
                    {
                        status = "trying to receive \"accept\" (received \"" + answer + "\")";
                        if (EventGhostPlus.DebugMode) Logger.Debug(status);
                        socket.Close();
                    }
                }
                catch
                {
                    status = "Error while " + status;
                }
                if (status != "success")
                    if (!status.StartsWith("Error")) status = "Error while " + status;
                result = status;
            }

            catch (Exception ex)
            {
                Log.Error($"Eventghost plus - error occured during SendEventToNetworkEventReceiver(): {ex}");
            }

            return result;
        }

        public void StartLocalNetworkEventReceiver()
        {
            if (_localNetworkEventReceiverThread == null)
            {
                _stopFlagForLocalNetworkEventReceiverThread = false;
                _localNetworkEventReceiverThread = new Thread(LocalNetworkEventReceiver);
                _localNetworkEventReceiverThread.Start();
            }
        }

        public void StopLocalNetworkEventReceiver()
        {
            var status = "";
            Socket socket;

            lock (_locker)
            {
                _stopCook = new Random().Next(65536).ToString("X8").Substring(4, 4);
            }

            RequestStopLocalNetworkEventReceiver = true;
            try
            {
                status = "trying to create the socket";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                if (_localNetworkEventReceiverPort == 0)
                    status = "trying to connect remote host because address not provided.";
                else
                    status = "trying to connect to " + _localNetworkEventReceiverAddress + ":" +
                             _localNetworkEventReceiverPort;
                if (EventGhostPlus.DebugMode) Logger.Debug(status);

                if (_localNetworkEventReceiverAddress != "0.0.0.0")
                    socket.Connect(_localNetworkEventReceiverAddress, _localNetworkEventReceiverPort);
                else socket.Connect("127.0.0.1", _localNetworkEventReceiverPort);

                status = "trying to send the magic word";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket.Send(Encoding.ASCII.GetBytes(_stopCook + "\n"));
                status = "trying to close the socket";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket.Close();
            }
            catch
            {
                Logger.Error("Error while " + status);
            }
            _localNetworkEventReceiverThread.Join();
        }

        private void LocalNetworkEventReceiver()
        {
            var status = "";
            IPEndPoint ep;
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket client;
            byte[] buff;
            string magicWord;
            string cook;
            string token;
            string digest;
            string remoteDigest;
            string data;
            List<string> payload;
            string receiveBuff;
            int receivedBytes;
            string stopKey;

            try
            {
                if (EventGhostPlus.DebugMode) Logger.Debug("Starting Local Network Event Receiver");
                ep = new IPEndPoint(IPAddress.Parse(_localNetworkEventReceiverAddress), _localNetworkEventReceiverPort);
                status = "trying to create the socket";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket.ReceiveBufferSize = 16384;
                socket.SendBufferSize = 16384;
                socket.ExclusiveAddressUse = true;

                status = "trying to bind on " + _localNetworkEventReceiverAddress + ":" + _localNetworkEventReceiverPort;
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket.Bind(ep);

                status = "trying to listen";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket.Listen(10);

                while (!RequestStopLocalNetworkEventReceiver)
                {
                    status = "waiting for new connection";
                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    client = socket.Accept();
                    lock (_locker)
                    {
                        stopKey = _stopCook;
                    }

                    client.ReceiveTimeout = 1000;
                    client.SendTimeout = 1000;

                    status = "trying to receive the magic word";
                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    buff = new byte[128];
                    client.Receive(buff);
                    magicWord = Encoding.ASCII.GetString(buff);
                    magicWord = magicWord.Substring(0, magicWord.IndexOf("\n"));
                    if (magicWord == "quintessence")
                    {
                        status = "trying to generate the cookie";
                        if (EventGhostPlus.DebugMode) Logger.Debug(status);
                        cook = new Random().Next(65536).ToString("X8").Substring(4, 4);

                        status = "trying to send the cookie";
                        if (EventGhostPlus.DebugMode) Logger.Debug(status);
                        client.Send(Encoding.ASCII.GetBytes(cook + "\n"));

                        status = "trying to compute the digest";
                        if (EventGhostPlus.DebugMode) Logger.Debug(status);
                        token = cook + ":" + _localPassword;
                        digest = HashString(token);

                        status = "trying to get the digest from client";
                        if (EventGhostPlus.DebugMode) Logger.Debug(status);
                        buff = new byte[128];
                        client.Receive(buff);
                        remoteDigest = Encoding.ASCII.GetString(buff);
                        remoteDigest = remoteDigest.Substring(0, remoteDigest.IndexOf("\n"));
                        if (remoteDigest.ToUpper() == digest.ToUpper())
                        {
                            status = "trying to send \"accept\"";
                            if (EventGhostPlus.DebugMode) Logger.Debug(status);
                            client.Send(Encoding.ASCII.GetBytes("accept\n"));

                            data = "";
                            payload = new List<string>();
                            receiveBuff = "";
                            receivedBytes = 0;
                            while (data != "close")
                            {
                                status = "trying to receive eventData";
                                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                                while (receiveBuff.IndexOf("\n") < 0 && receivedBytes < 32768)
                                {
                                    buff = new byte[128];
                                    receivedBytes += client.Receive(buff);
                                    data = Encoding.ASCII.GetString(buff);
                                    data = data.Trim('\0');
                                    receiveBuff += data;
                                }
                                data = receiveBuff.Substring(0, receiveBuff.IndexOf("\n"));
                                receiveBuff = receiveBuff.Substring(receiveBuff.IndexOf("\n") + 1);
                                if (data == "close")
                                {
                                    status = "trying to close the connection (requested by client)";
                                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                                    try
                                    {
                                        client.Send(Encoding.ASCII.GetBytes("close\n"));
                                    }
                                    catch
                                    {
                                    }
                                    client.Close();
                                    status = "success";
                                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                                    EndLastEvent();
                                }
                                else if (data.StartsWith("payload "))
                                {
                                    payload.Add(data.Substring(data.IndexOf(" ") + 1));
                                }
                                else
                                {
                                    if (data == "ButtonReleased")
                                    {
                                        EndLastEvent();
                                    }
                                    else
                                    {
                                        if (payload.Count > 0 && payload[payload.Count - 1] == "withoutRelease")
                                            TriggerEnduringEvent(data, payload);
                                        else TriggerEvent(data, payload);
                                    }
                                }
                            }
                        }
                        else
                        {
                            status = "trying to close the client connection (digests don't match)";
                            if (EventGhostPlus.DebugMode) Logger.Debug(status);
                        }
                    }
                    else if (magicWord == stopKey)
                    {
                        status = "success";
                        RequestStopLocalNetworkEventReceiver = true;
                        if (EventGhostPlus.DebugMode) Logger.Debug("Ending Local Network Event Receiver");
                    }
                    else
                    {
                        status = "trying to close the client connection (magic word not received)";
                        if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    }
                    client.Close();
                }
                socket.Close();
            }
            catch
            {
                if (!socket.Connected)
                {
                    // Try to reconnect
                    Thread.Sleep(2500);
                    StopLocalNetworkEventReceiver();
                    StartLocalNetworkEventReceiver();
                    return;
                }
                status = "Error while " + status + ".";
            }
            if (status != "success") Logger.Error(status);
        }

        private void TriggerEvent(string eventName, List<string> payload)
        {
            Fire_Event_FromNetworkEventSender(eventName, payload);
        }

        private void TriggerEnduringEvent(string eventName, List<string> payload)
        {
            if (_enduringEvents)
            {
                _eventsReceived.Add(eventName, payload);
                _lastEventReceived = eventName;
                Fire_EnduringEvent_FromNetworkEventSender_Start(eventName, payload);
            }
            else
            {
                TriggerEvent(eventName, payload);
            }
        }

        private void EndLastEvent()
        {
            if (_eventsReceived.Count > 0 && _lastEventReceived != "" && _enduringEvents)
            {
                Fire_EnduringEvent_FromNetworkEventSender_End(_lastEventReceived, _eventsReceived[_lastEventReceived]);
                _eventsReceived.Remove(_lastEventReceived);
                _lastEventReceived = "";
            }
        }

        private void Fire_EnduringEvent_FromNetworkEventSender_Start(string name, List<string> payload)
        {
            if (EnduringEventFromNetworkEventSenderStart != null)
                EnduringEventFromNetworkEventSenderStart(this, new NetWorkEventReceiverEventArgs(name, payload));
        }

        private void Fire_EnduringEvent_FromNetworkEventSender_End(string name, List<string> payload)
        {
            if (EnduringEventFromNetworkEventSenderEnd != null)
                EnduringEventFromNetworkEventSenderEnd(this, new NetWorkEventReceiverEventArgs(name, payload));
        }

        private void Fire_Event_FromNetworkEventSender(string name, List<string> payload)
        {
            if (EventFromNetworkEventSender != null)
                EventFromNetworkEventSender(this, new NetWorkEventReceiverEventArgs(name, payload));
        }

        private static string HashString(string value)
        {
            var data = new MD5CryptoServiceProvider().ComputeHash(Encoding.ASCII.GetBytes(value));

            var hashedString = new StringBuilder();

            for (var i = 0; i < data.Length; i++)

                hashedString.Append(data[i].ToString("X2"));

            return hashedString.ToString();
        }
    }

    public class NetWorkEventReceiverEventArgs : EventArgs
    {
        public string Name;
        public List<string> Payload;

        public NetWorkEventReceiverEventArgs(string name, List<string> payload)
        {
            this.Name = name;
            this.Payload = payload;
        }
    }
}