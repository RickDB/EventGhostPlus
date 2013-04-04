using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace EventGhostPlus
{
    class EG_Network_Event_Receiver_Sender
    {

        private string localNetworkEventReceiverAddress;
        private int localNetworkEventReceiverPort;
        private string localPassword;
        private bool enduringEvents;

        private Dictionary<string, List<string>> eventsReceived;
        private string lastEventReceived;

        private Thread localNetworkEventReceiverThread;
        private bool stopFlag_for_localNetworkEventReceiverThread;
        private string stopCook;
        private object locker;

        public delegate void EnduringEvent_FromNetworkEventSender_Start_Handler(object sender, NetWorkEventReceiver_EventArgs e);
        public event EnduringEvent_FromNetworkEventSender_Start_Handler EnduringEvent_FromNetworkEventSender_Start;

        public delegate void EnduringEvent_FromNetworkEventSender_End_Handler(object sender, NetWorkEventReceiver_EventArgs e);
        public event EnduringEvent_FromNetworkEventSender_End_Handler EnduringEvent_FromNetworkEventSender_End;

        public delegate void Event_FromNetworkEventSender_Handler(object sender, NetWorkEventReceiver_EventArgs e);
        public event Event_FromNetworkEventSender_Handler Event_FromNetworkEventSender;




        public EG_Network_Event_Receiver_Sender(string localNerAddr, string localPW, bool generateEnduringEvents)
        {
            if (localNerAddr != "")
            {
                this.localNetworkEventReceiverAddress = localNerAddr.Substring(0, localNerAddr.IndexOf(":"));
                if (this.localNetworkEventReceiverAddress == "*") this.localNetworkEventReceiverAddress = "0.0.0.0";
                this.localNetworkEventReceiverPort = System.Convert.ToInt32(localNerAddr.Substring(localNerAddr.IndexOf(":") + 1));
                this.eventsReceived = new Dictionary<string, List<string>>();
                this.lastEventReceived = "";
                this.locker = new object();
                this.StartLocalNetworkEventReceiver();
            }
            else
            {
                this.localNetworkEventReceiverAddress = "";
                this.localNetworkEventReceiverPort = 0;
            }
            this.localPassword = localPW;
            this.stopCook = new Random().Next(65536).ToString("X8").Substring(4, 4);
            this.enduringEvents = generateEnduringEvents;
        }

        public string SendEventToNetworkEventReceiver(string remoteAddress, string pwd, string eventString, string[] payload)
        {
            if (EventGhostPlus.DebugMode) Logger.Debug("Starting Network Event Sender");
            string status = "";
            string result = "";
            Socket socket;
            string remoteIP = "";
            int remotePort = 0;
            if (remoteAddress != "")
            {
                remoteIP = remoteAddress.Substring(0, remoteAddress.IndexOf(":"));
                remotePort = System.Convert.ToInt32(remoteAddress.Substring(remoteAddress.IndexOf(":") + 1));
            }
            else
            {
                remoteIP = "";
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
                    status = "trying to connect to " + remoteIP + ":" + remotePort.ToString();
                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                }
                socket.Connect(remoteIP, remotePort);

                status = "trying to send the magic word";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket.Send(Encoding.ASCII.GetBytes("quintessence\n\r"));

                status = "trying to receive the cookie";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                byte[] buff = new byte[128];
                socket.Receive(buff);
                string cookie = Encoding.ASCII.GetString(buff);
                cookie = cookie.Substring(0, cookie.IndexOf("\n"));

                status = "trying to compute the digest";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                string token = cookie + ":" + pwd;
                string digest = HashString(token) + "\n";

                status = "trying to send the digest";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket.Send(Encoding.ASCII.GetBytes(digest));

                status = "trying to receive the \"accept\", check the password in settings.";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                buff = new byte[512];
                socket.Receive(buff);
                string answer = Encoding.ASCII.GetString(buff);
                answer = answer.Substring(0, answer.IndexOf("\n"));
                if (answer == "accept")
                {
                    if (payload != null && payload.Length > 0)
                    {
                        for (int count = 0; count < payload.Length; count++)
                        {
                            status = "trying to send payload[" + count.ToString() + "]";
                            if (EventGhostPlus.DebugMode) Logger.Debug(status);
                            socket.Send(Encoding.ASCII.GetBytes("payload " + payload[count] + "\n"));
                        }
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
            {
                if (!status.StartsWith("Error")) status = "Error while " + status;
            }
            result = status;
            return result;
        }
        public void StartLocalNetworkEventReceiver()
        {
            if (this.localNetworkEventReceiverThread == null)
            {
                this.stopFlag_for_localNetworkEventReceiverThread = false;
                this.localNetworkEventReceiverThread = new Thread(LocalNetworkEventReceiver);
                this.localNetworkEventReceiverThread.Start();

            }
        }
        public void StopLocalNetworkEventReceiver()
        {
            string status = "";
            Socket socket;

            lock (locker)
            {
                this.stopCook = new Random().Next(65536).ToString("X8").Substring(4, 4);
            }

            this.Request_StopLocalNetworkEventReceiver = true;
            try
            {
                status = "trying to create the socket";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                if (this.localNetworkEventReceiverPort == 0) status = "trying to connect remote host because address not provided.";
                else status = "trying to connect to " + this.localNetworkEventReceiverAddress + ":" + this.localNetworkEventReceiverPort.ToString();
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    
                if (this.localNetworkEventReceiverAddress != "0.0.0.0") socket.Connect(this.localNetworkEventReceiverAddress, this.localNetworkEventReceiverPort);
                else socket.Connect("127.0.0.1", this.localNetworkEventReceiverPort);

                status = "trying to send the magic word";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket.Send(Encoding.ASCII.GetBytes(this.stopCook + "\n"));
                status = "trying to close the socket";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket.Close();
            }
            catch
            {
                Logger.Error("Error while " + status);
            }
            this.localNetworkEventReceiverThread.Join();
        }

        private bool Request_StopLocalNetworkEventReceiver
        {
            get
            {
                lock (this.locker)
                {
                    return this.stopFlag_for_localNetworkEventReceiverThread;
                }
            }
            set
            {
                lock (this.locker)
                {
                    this.stopFlag_for_localNetworkEventReceiverThread = value;
                }
            }
        }
        private void LocalNetworkEventReceiver()
        {
            string status = "";
            IPEndPoint ep;
            Socket socket;
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
                ep = new IPEndPoint(IPAddress.Parse(this.localNetworkEventReceiverAddress), this.localNetworkEventReceiverPort);
                status = "trying to create the socket";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.ReceiveBufferSize = 16384;
                socket.SendBufferSize = 16384;
                socket.ExclusiveAddressUse = true;
                status = "trying to bind on " + this.localNetworkEventReceiverAddress + ":" + this.localNetworkEventReceiverPort.ToString();
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket.Bind(ep);

                status = "trying to listen";
                if (EventGhostPlus.DebugMode) Logger.Debug(status);
                socket.Listen(10);


                while (!this.Request_StopLocalNetworkEventReceiver)
                {

                    status = "waiting for new connection";
                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                    client = socket.Accept();
                    lock (locker)
                    {
                        stopKey = this.stopCook;
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
                        token = cook + ":" + this.localPassword;
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
                                    data = data.Trim(new char[] { '\0' });
                                    receiveBuff += data;
                                }
                                data = receiveBuff.Substring(0, receiveBuff.IndexOf("\n"));
                                receiveBuff = receiveBuff.Substring(receiveBuff.IndexOf("\n") + 1);
                                if (data == "close")
                                {
                                    status = "trying to close the connection (requested by client)";
                                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                                    try { client.Send(Encoding.ASCII.GetBytes("close\n")); }
                                    catch { }
                                    client.Close();
                                    status = "success";
                                    if (EventGhostPlus.DebugMode) Logger.Debug(status);
                                    this.EndLastEvent();
                                }
                                else if (data.StartsWith("payload ")) payload.Add(data.Substring(data.IndexOf(" ") + 1));
                                else
                                {
                                    if (data == "ButtonReleased") this.EndLastEvent();
                                    else
                                    {
                                        if (payload.Count > 0 && payload[payload.Count - 1] == "withoutRelease") this.TriggerEnduringEvent(data, payload);
                                        else this.TriggerEvent(data, payload);
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
                        this.Request_StopLocalNetworkEventReceiver = true;
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
                status = "Error while " + status + ".";
            }
            if (status != "success") Logger.Error(status);
        }

        private void TriggerEvent(string eventName, List<string> payload)
        {
            this.Fire_Event_FromNetworkEventSender(eventName, payload);
        }
        private void TriggerEnduringEvent(string eventName, List<string> payload)
        {
            if (this.enduringEvents)
            {
                this.eventsReceived.Add(eventName, payload);
                this.lastEventReceived = eventName;
                this.Fire_EnduringEvent_FromNetworkEventSender_Start(eventName, payload);
            }
            else this.TriggerEvent(eventName, payload);
        }
        private void EndLastEvent()
        {
            if (this.eventsReceived.Count > 0 && this.lastEventReceived != "" && this.enduringEvents)
            {
                this.Fire_EnduringEvent_FromNetworkEventSender_End(this.lastEventReceived, this.eventsReceived[this.lastEventReceived]);
                this.eventsReceived.Remove(this.lastEventReceived);
                this.lastEventReceived = "";
            }
        }

        private void Fire_EnduringEvent_FromNetworkEventSender_Start(string name, List<string> payload)
        {
            if (this.EnduringEvent_FromNetworkEventSender_Start != null)
            {
                this.EnduringEvent_FromNetworkEventSender_Start(this, new NetWorkEventReceiver_EventArgs(name, payload));
            }
        }
        private void Fire_EnduringEvent_FromNetworkEventSender_End(string name, List<string> payload)
        {
            if (this.EnduringEvent_FromNetworkEventSender_End != null)
            {
                this.EnduringEvent_FromNetworkEventSender_End(this, new NetWorkEventReceiver_EventArgs(name, payload));
            }
        }
        private void Fire_Event_FromNetworkEventSender(string name, List<string> payload)
        {
            if (this.Event_FromNetworkEventSender != null) this.Event_FromNetworkEventSender(this, new NetWorkEventReceiver_EventArgs(name, payload));
        }

        private static string HashString(string value)
        {

            byte[] data = new MD5CryptoServiceProvider().ComputeHash(Encoding.ASCII.GetBytes(value));

            StringBuilder hashedString = new StringBuilder();

            for (int i = 0; i < data.Length; i++)

                hashedString.Append(data[i].ToString("X2"));

            return hashedString.ToString();

        }

    }

    public class NetWorkEventReceiver_EventArgs : EventArgs
    {
        public string name;
        public List<string> payload;
        public NetWorkEventReceiver_EventArgs(string name, List<string> payload)
        {
            this.name = name;
            this.payload = payload;
        }
    }

}
