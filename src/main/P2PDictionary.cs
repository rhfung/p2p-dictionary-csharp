using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace com.rhfung.P2PDictionary
{
    //IDictionary<TKey, TValue>
    public class P2PDictionary : IMessageController, ISubscriptionChanged, IDictionary<string, object>
    {
        internal const string DATA_NAMESPACE = DataConnection.DATA_NAMESPACE; // /BLAH
        internal const int MAX_RANDOM_NUMBER = 10;

        const int SENDER_THREADS = 3;
        const int BACKLOG = 2048;
        const int SIMULATENOUS_REQUESTS = 4;

        internal const int SLEEP_WAIT_TO_CLOSE = 100;
        internal const int SLEEP_IDLE_SLEEP = 8;
        internal const int SLEEP_IDLE_SERVER = 50;
        internal const int SLEEP_USER_RETRY_READ = 30;

        volatile bool killbit = false;
        volatile bool killbitSenderThreads = false;

        Thread runLoop;
        Thread[] senderThreads;
        
        Timer constructNwTimer;
        TcpListener listener; 

        Dictionary<string, DataEntry> data;
        Dictionary<int, int> messages;
        ReaderWriterLockSlim dataLock;

        List<DataConnection> connections;
        Subscription subscription;
        PeerDiscovery discovery;

        int constructNwNextPeer=0;
        int constructNwRandomPeer=0;

        int mSearchForClientsTimeout = 0;
        
        int _localUID;
        string _description;
        string _namespace;

        //http://msdn.microsoft.com/en-us/library/system.idisposable.aspx

        LogInstructions debugBuffer;

        public event EventHandler<SubscriptionEventArgs> SubscriptionChanged;
        public event EventHandler<NotificationEventArgs> Notification;
        public event EventHandler<ConnectionEventArgs> Connected;
        public event EventHandler<ConnectionEventArgs> Disconnected;
        public event EventHandler<ConnectionErrorEventArgs> ConnectionFailure;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="description">Descriptive name that describes this dictionary. Only used for web access.</param>
        /// <param name="port">Server port number. Only one instance of the server can run on a computer.</param>
        /// <param name="ns">Dictionary partition that is defines a unique shared dictionary.</param>
        /// <param name="serverMode"></param>
        /// <param name="clientMode"></param>
        /// <param name="searchForClients">Time to search for clients in ms if clientMode == AutoConnect</param>
        public P2PDictionary(string description,  int port, string ns,
            P2PDictionaryServerMode serverMode = P2PDictionaryServerMode.AutoRegister,
            P2PDictionaryClientMode clientMode = P2PDictionaryClientMode.AutoConnect,
            int searchForClients = 1500)
        {
            // some random ID
            this._description = description;
            this._localUID = UIDGenerator.GetNextInteger();
            this._namespace = ns;

            // load data from caller
            this.data = new Dictionary<string, DataEntry>();
            this.dataLock = new ReaderWriterLockSlim();
            this.messages = new Dictionary<int, int>();

            this.connections = new List<DataConnection>();
            this.subscription = new Subscription(this);

            this.discovery = new PeerDiscovery();

            // sender threads
            ConstructSenderThreads();

            // okay, some auto startup
            if (serverMode == P2PDictionaryServerMode.AutoRegister || serverMode == P2PDictionaryServerMode.Hidden)
            {
                this.OpenServer( System.Net.IPAddress.Any, port);
                if (serverMode == P2PDictionaryServerMode.AutoRegister)
                {
                    this.discovery.RegisterServer(this);
                }
            }

            if (clientMode == P2PDictionaryClientMode.AutoConnect)
            {
                this.discovery.BrowseServices();
                mSearchForClientsTimeout = searchForClients;
                constructNwTimer = new Timer(OnConstructNetwork, null, searchForClients,0 );
            }

            this.debugBuffer = null;
        }

        ~P2PDictionary()
        {
            Close(true);
        }

        /// <summary>
        /// Returns a unique ID number for the dictionary. Should be unique to all peers.
        /// </summary>
        public int LocalID
        {
            get
            {
                return this._localUID;
            }
        }

        /// <summary>
        /// Returns the connections for internal use only
        /// </summary>
        internal List<DataConnection> Connections
        {
            get{
                return this.connections;
            }
        }

        ///// <summary>
        ///// Returns the number of remotely connected peers
        ///// </summary>
        //public int RemotePeersCount
        //{
        //    get 
        //    {
        //        lock (this.connections)
        //        {
        //            return this.connections.Count;
        //        }
        //    }
        //}


        /// <summary>
        /// Name of the dictionary partition. Only data is accessible if in the same partition.
        /// </summary>
        public string Namespace
        {
            get
            {
                return this._namespace;
            }
        }
    
        /// <summary>
        /// Sets/gets the buffer for debug messages. Set to null to disable.
        /// </summary>
        /// <seealso cref="SetDebugBuffer"/>
        public TextWriter DebugBuffer
        {
            get
            {
                if (this.debugBuffer == null)
                    return null;
                else
                    return this.debugBuffer.GetTextWriter();
            }
            set
            {
                SetDebugBuffer(value, 1, true);
            }
        }

        /// <summary>
        /// Configures logging.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="level">0 = all in/out messages, 1 = connection messages only</param>
        /// <param name="autoFlush">flush writer stream after every log message</param>
        public void SetDebugBuffer(TextWriter writer, int level, bool autoFlush)
        {
            if (writer == null)
                this.debugBuffer = null;
            else
                this.debugBuffer = new LogInstructions(writer, level, autoFlush);

            lock (connections)
            {
                foreach (DataConnection c in connections)
                {
                    c.debugBuffer = this.debugBuffer;
                }
            }
        }

        /// <summary>
        /// Describes the user-friendly name of this P2P client for web browser requests.
        /// </summary>
        public string Description
        {
            get
            {
                return this._description;
            }
        }

        /// <summary>
        /// Returns the IP address of the server. Cast return value as System.Net.IPEndPoint.
        /// </summary>
        public System.Net.EndPoint LocalEndPoint
        {
            get
            {
                if (this.listener != null)
                    return this.listener.LocalEndpoint;
                else
                    return null;
            }
        }


        // thread safe
        public bool ContainsKey(string key)
        {
            // return data.ContainsKey(GetFullKey(DATA_NAMESPACE ,key));
            DataEntry e = GetEntry(this.data, this.dataLock, GetFullKey(DATA_NAMESPACE, _namespace, key));
            if (e == null)
                return false;
            else
                return e.subscribed;
        }

        internal static string GetFullKey(string ns, string partition, string key)
        {
            if (key.StartsWith(ns))
                throw new ApplicationException("GetFullKey should not operate on keys within namespace");
            return ns + "/" + partition + "/" + key; 
        }

        internal static string GetUserKey(string ns, string partition, string fullKey)
        {
            if (IsFullKeyInNamespace(ns,partition,fullKey))
                return fullKey.Substring(ns.Length + 1 + partition.Length + 1);
            else
                throw new ApplicationException("GetUserKey should not operate on keys outside of namespace");
        }

        internal static bool IsFullKeyInNamespace(string ns, string partition, string fullKey)
        {
            return fullKey.StartsWith(ns + "/" + partition + "/");
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="rwl"></param>
        /// <param name="fullKey"></param>
        /// <returns></returns>
        /// <remarks>called in both P2PDictionary and DataConnection</remarks>
        internal static DataEntry GetEntry(Dictionary<string,DataEntry> data, ReaderWriterLockSlim rwl, string fullKey)
        {
            rwl.EnterReadLock();
            DataEntry entry = null;
            try
            {
                
                if (data.ContainsKey(fullKey))
                {
                    entry = data[fullKey];
                }
            }
            finally
            {
                rwl.ExitReadLock();
            }

            return entry;
        }


        /// <summary>
        /// Blocking call to read from the dictionary, throws IndexOutOfRangeException
        /// </summary>
        /// <param name="key"></param>
        /// <param name="msTimeout"></param>
        /// <returns></returns>
        public  object GetValue(string key, int msTimeout = 500)
        {
            int sleepLength = 0;
            DataEntry e = GetEntry(this.data, this.dataLock ,GetFullKey(DATA_NAMESPACE, _namespace, key));
            while(sleepLength < msTimeout && e == null)
            {
                Thread.Sleep(SLEEP_USER_RETRY_READ);
                e = GetEntry(this.data, this.dataLock, GetFullKey(DATA_NAMESPACE, _namespace, key));
                sleepLength += SLEEP_USER_RETRY_READ;
            }
            if (e == null)
            {
                throw new IndexOutOfRangeException("No dictionary element with the key exists");
            }
            if (!e.subscribed)
            {
                throw new SubscriptionException("Not subscribed to key");
            }
            return e.value;
        }

        /// <summary>
        /// blocking call to read from the dictionary, returns false if cannot get the value
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">value is returned here</param>
        /// <returns></returns>
        public bool TryGetValue(string key, out object value)
        {
            return TryGetValue(key, out value, 0);
        }

        /// <summary>
        /// blocking call to read from the dictionary, waits for msTimeout, returns false if cannot get the value
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">value is returned here</param>
        /// <param name="msTimeout"></param>
        /// <returns></returns>
        public bool TryGetValue(string key, out object value, int msTimeout = 0)
        {
            int sleepLength = 0;
            DataEntry e = GetEntry(this.data, this.dataLock, GetFullKey(DATA_NAMESPACE, _namespace, key));
            while (sleepLength < msTimeout && e == null)
            {
                Thread.Sleep(P2PDictionary.SLEEP_USER_RETRY_READ);
                e = GetEntry(this.data, this.dataLock, GetFullKey(DATA_NAMESPACE, _namespace, key));
                sleepLength += P2PDictionary.SLEEP_USER_RETRY_READ;
            }
            if (!e.subscribed)
            {
                value = null;
                return false;
            }
            value = e.value;
            return true;
        }

        // thread safe
        public object this[string key]
        {
            get
            {
                // todo: decide to crash or return null
                DataEntry e = GetEntry(this.data, this.dataLock, GetFullKey(DATA_NAMESPACE, _namespace, key));
                if (!e.subscribed)
                {
                    throw new SubscriptionException("Not subscribed to key");
                }
                return e.value;
            }
            set
            {
                bool upgraded = true;
                DataEntry get = null;
                NotificationReason reason;
                object oldValue = null;

                dataLock.EnterUpgradeableReadLock();

                try
                {
                    
                    if (this.data.ContainsKey(GetFullKey(DATA_NAMESPACE, _namespace, key)))
                        get = this.data[GetFullKey(DATA_NAMESPACE, _namespace, key)];

                    if (get != null)
                    {
                        // leave the lock now
                        dataLock.ExitUpgradeableReadLock();
                        upgraded = false;


                        // work with the dictionary entry
                        reason = NotificationReason.Change;
                        lock (get)
                        {
                            oldValue = get.value; // save old value for notificatoin event

                            get.lastOwnerID = _localUID;
                            get.lastOwnerRevision = data[GetFullKey(DATA_NAMESPACE, _namespace, key)].lastOwnerRevision + 1;
                            get.value = value;
                            get.subscribed = true;
                            get.senderPath = new List<int>() { this._localUID };

                            get.DetectTypeFromValue();
                        }
                        // if some pattern does not have this subscription, then add it automatically
                        if (!subscription.IsSubscribed(GetFullKey(DATA_NAMESPACE, _namespace, key)))
                        {
                            AddSubscription(key);
                        }
                    }
                    else
                    {
                        dataLock.EnterWriteLock();

                        try
                        {
                            reason = NotificationReason.Add;
                            get = new DataEntry(GetFullKey(DATA_NAMESPACE, _namespace, key), value, new ETag(_localUID, 0), new List<int>() { this._localUID }, true);

                            data.Add(GetFullKey(DATA_NAMESPACE, _namespace, key), get);
                        }
                        finally 
                        {
                            dataLock.ExitWriteLock();
                        }

                        // if some pattern does not have this subscription, then add it automatically
                        if (!subscription.IsSubscribed(GetFullKey(DATA_NAMESPACE, _namespace, key)))
                        {
                            AddSubscription(key, SubscriptionInitiator.AutoAddKey);
                        }
                    }

                }
                finally
                {
                    if (upgraded)
                        dataLock.ExitUpgradeableReadLock();
                }

                    // send data outside on each connection
                    // ask any connection to formulate a message
                    // and then we handle sending it here
                    if (connections.Count > 0 && !this.killbit)
                    {
                        SendMemoryToPeer msg = connections[0].CreateResponseMessage(GetFullKey(DATA_NAMESPACE, _namespace, key));
                        SendBroadcastMemory msg2 = new SendBroadcastMemory(msg.ContentLocation, new List<int>() { this.LocalID });
                        msg2.MemBuffer = msg.MemBuffer;
                        BroadcastToWire(msg2);
                    }

                    // notify local loopback
                    if (Notification != null)
                    {
                        NotificationEventArgs args = new NotificationEventArgs(get, key, reason, oldValue);
                        Notification.Invoke(this, args);
                    }
                
            }
        }

        /// <summary>
        /// Removes all keys that are currently owned by this peer.
        /// </summary>
        public void Clear()
        {
            dataLock.EnterWriteLock();
            try
            {
                foreach (DataEntry entry in this.data.Values)
                {
                    if (entry.lastOwnerID == this._localUID)
                    {
                        Remove(entry.key);
                    }
                }
            }
            finally
            {
                dataLock.ExitWriteLock();
            }

        }

        /// <summary>
        /// Removes a dictionary entry
        /// </summary>
        /// <param name="item">value is ignored</param>
        /// <returns></returns>
        public bool Remove(KeyValuePair<string, object> item)
        {
            return Remove(item.Key);
        }

        /// <summary>
        /// Removes a dictionary entry
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(string key)
        {
            DataEntry get = GetEntry(this.data, this.dataLock, GetFullKey(DATA_NAMESPACE, _namespace, key));
            if (get != null)
            {
                lock (get)
                {
                    get.lastOwnerID = _localUID;
                    get.lastOwnerRevision = data[GetFullKey(DATA_NAMESPACE, _namespace, key)].lastOwnerRevision + 1;
                    get.Delete();
                    get.subscribed = true;
                    get.senderPath = new List<int>() { this._localUID };
                }

                // if some pattern does not have this subscription, then add it automatically
                if (!subscription.IsSubscribed(GetFullKey(DATA_NAMESPACE, _namespace, key)))
                {
                    AddSubscription(key);
                }

                // send data outside on each connection
                // ask any connection to formulate a message
                // and then we handle sending it here
                if (connections.Count > 0 && !this.killbit)
                {
                    SendMemoryToPeer msg = connections[0].CreateResponseMessage(GetFullKey(DATA_NAMESPACE, _namespace, key));
                    SendBroadcastMemory msg2 = new SendBroadcastMemory(msg.ContentLocation, new List<int>(){this.LocalID});
                    msg2.MemBuffer = msg.MemBuffer;
                    BroadcastToWire(msg2);
                }
                
                return true;
            }
            else
            {
                return false;
            }

            
        }

        /// <summary>
        /// Returns number of dictionary entries including non-subscribed entries
        /// </summary>
        public int Count
        {
            get
            {
                int retValue;
                dataLock.EnterReadLock();
                try
                {
                    retValue = this.data.Count(x => IsFullKeyInNamespace(DATA_NAMESPACE, _namespace, x.Key));
                }
                finally { dataLock.ExitReadLock(); }
                return retValue;
            }
        }

        /// <summary>
        /// opens a server instance if not already specified on constructor
        /// </summary>
        /// <param name="addr">A string that contains an IP address in dotted-quad notation
        /// for IPv4 </param>
        /// <param name="port"></param>
        public void OpenServer(System.Net.IPAddress addr, int port)
        {
            if (runLoop != null)
            {
                throw new NotSupportedException("Cannot run the server more than once");
            }

            // open connection
            listener = new TcpListener(addr, port);
            listener.Start(BACKLOG);

            // fake local UID
            //this._localUID = port;

            // start
            runLoop = new Thread(new ParameterizedThreadStart(RunServerThread));
            runLoop.IsBackground = true;
            // runLoop.Priority = ThreadPriority.BelowNormal;//not sure if this line would be useful
            runLoop.Name = this.Description + " listener " + listener.LocalEndpoint.ToString();
            runLoop.Start();
        }


        /// <summary>
        /// Aborts all connection and server threads immediately.
        /// </summary>
        public void Abort()
        {
            if (this.discovery != null)
            {
                this.discovery.UnregisterServer();
                this.discovery = null;
            }

            // stop receving connections
            runLoop.Abort();
            runLoop = null;

            // end each connection
            foreach (DataConnection c in connections)
            {
                c.Kill();
            }

  
        }


        /// <summary>
        /// Closes all connections when data is finished being served.
        /// </summary>
        public void Close()
        {
            Close(false);
        }

        private void Close(bool disposing)
        {
            // stop auto connect
            if (this.constructNwTimer != null)
            {
                this.constructNwTimer.Dispose();
                this.constructNwTimer = null;
            }

            // disconnect discovery
            if (this.discovery != null)
            {
                this.discovery.UnregisterServer();
                this.discovery = null;
            }

            // stop listener
            killbit = true;

            if (!disposing)
            {
                if (this.runLoop != null)
                {
                    runLoop.Join();
                    runLoop = null;
                }
            }

            List<DataConnection> closeConn = new List<DataConnection>(connections);

            // close all reader connections
            foreach (DataConnection c in closeConn)
            {
                c.Close(disposing);
            }

            // stop sending all data on sender threads
            killbitSenderThreads = true;

            if (!disposing)
            {
                foreach (Thread thd in senderThreads)
                {
                    if (thd.IsAlive)
                    {
                        thd.Join();
                    }
                }
            }
        }


        class ClientThreadStart
        {
            public DataConnection conn;
            public System.Net.IPEndPoint ep;
            public AutoResetEvent waitBlock;
            public bool connectionIsSuccessful;

            public ClientThreadStart(DataConnection conn, System.Net.IPEndPoint endpoint)
            {
                this.conn = conn;
                this.ep = endpoint;
                this.connectionIsSuccessful = false;
            }
        }

        /// <summary>
        /// Manually connect to another peer. Not guaranteed to actually connect to the client
        /// because it may be already connected or unreachable.
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="port"></param>
        /// <returns>true if a TCP connection to the client is possible, false otherwise</returns>
        public bool OpenClient(System.Net.IPAddress addr, int port)
        {
            System.Net.IPEndPoint ep = new System.Net.IPEndPoint(addr, port);

            // start
            ParameterizedThreadStart ts = new ParameterizedThreadStart(RunClientStart);
            Thread runLoop = new Thread(ts);
            runLoop.IsBackground = true;
            runLoop.Name = this.Description + " Client thread " + ep.ToString();


            DataConnection conn = new DataConnection(ConnectionType.Client, runLoop, this._localUID, this.data, this.dataLock, new WeakDataServer( this), subscription, debugBuffer);
            lock (connections)
            {
                this.connections.Add(conn);
            }

            ClientThreadStart cts = new ClientThreadStart(conn, ep);
            cts.waitBlock = new AutoResetEvent(false);
            runLoop.Start(cts);
            cts.waitBlock.WaitOne();
            cts.waitBlock.Close();
            cts.waitBlock = null;

            return cts.connectionIsSuccessful;
        }


        /// <summary>
        /// Used by StartClient
        /// </summary>
        /// <param name="data"></param>
        private void RunClientStart(object data)
        {
            ClientThreadStart tinfo = (ClientThreadStart)data;

            
            

            try
            {
                TcpClient client = new TcpClient();
                client.Connect(tinfo.ep);

                // tell the calling thread  of success
                tinfo.connectionIsSuccessful = true;
                tinfo.waitBlock.Set();

                // updates are always made by the "CLIENT"
                tinfo.conn.AddRequestDictionary();
                // conn.SendToRemoteClient(conn.CreateDataMessage(DATA_NAMESPACE)); // I use this instead of PushOnWire() because I don't want to bother other TCP connections

                tinfo.conn.ReadLoop(client);
                
            }
            catch(Exception ex)
            {
                // clear the signal
                tinfo.connectionIsSuccessful = false;
                if (tinfo.waitBlock != null)
                    tinfo.waitBlock.Set();

                WriteDebug(this.Description + " " + this.LocalID + " " + ex.Message);

                if (this.ConnectionFailure != null)
                {
                    ConnectionErrorEventArgs args = new ConnectionErrorEventArgs();
                    args.EndPoint = tinfo.ep;
                    args.RemoteUID = 0;
                    args.Error = ex;
                    this.ConnectionFailure(this, args);
                }
            }
            finally
            {
                lock (connections)
                {
                    this.connections.Remove(tinfo.conn);
                }
            }

        }


        class ServerThreadStart
        {
            public TcpClient connection;
            public Thread thread;

            public ServerThreadStart(TcpClient tcpConnection, Thread startThread)
            {
                this.connection = tcpConnection;
                this.thread = startThread;
            }
        }

        private void RunServerThread(object data)
        {
            while(!killbit)
            {
                if (listener.Pending())
                {
                    
                    ParameterizedThreadStart ps = new ParameterizedThreadStart(RunServerStart);
                    Thread t = new Thread(ps);
                    t.IsBackground = true;

                    WriteDebug(this._localUID + " Server: Accepting connection...");
                    ServerThreadStart tinfo = new ServerThreadStart(listener.AcceptTcpClient(), t);
                    t.Name = this.Description + " Server thread " + tinfo.connection.Client.RemoteEndPoint.ToString();
                    t.Start(tinfo);
                    WriteDebug(this._localUID + " Server: Connection opened");
                }
                else
                {
                    Thread.Sleep(P2PDictionary.SLEEP_IDLE_SERVER);
                }

            }

            listener.Stop();
        }

       
        private void RunServerStart(object data)
        {
            ServerThreadStart tinfo = (ServerThreadStart)data;
            DataConnection conn = new DataConnection(ConnectionType.Server,tinfo.thread, this._localUID, this.data, this.dataLock, new WeakDataServer( this), subscription, debugBuffer);
            lock (connections)
            {
                this.connections.Add(conn);
            }

            try
            {
                conn.ReadLoop(tinfo.connection);
            }
            catch(Exception ex)
            {

            }
            finally
            {
                lock (connections)
                {
                    this.connections.Remove(conn);
                }
            }
        }

        private void WriteDebug(string msg)
        {
            if (debugBuffer != null)
            {
                debugBuffer.Log(1, msg);
            }
        }


        internal int BroadcastToWire(SendBroadcastMemory msg)
        {
            List<DataConnection> copyConn;

            lock (connections)
            {
                copyConn =new List<DataConnection>(this.connections);
            }

            // this list assumes that a link  is connected more than onece, 
            // but message is only sent on one of the links
            List<int> sentTo = new List<int>(copyConn.Count);
            int broadcasts = 0;

            foreach (DataConnection c in copyConn)
            {
                if (c.RemoteUID != this._localUID &&            // don't send to myself
                    !msg.PeerList.Contains(c.RemoteUID) &&       // don't send to previous sender
                    !c.IsWebClientConnected &&                  // don't send to web browser
                    !sentTo.Contains(c.RemoteUID))              // don't send twice
                {
                    //WriteDebug(this.system_id + " pushes a data packet to " + c.RemoteUID);
                    sentTo.Add(c.RemoteUID);
                    c.SendToRemoteClient(msg);
                    broadcasts++;
                }
            }

            //System.Diagnostics.Debug.Assert(broadcasts>0);

            return broadcasts;
            
        }


        int IMessageController.BroadcastToWire(SendBroadcastMemory msg)
        {
            return this.BroadcastToWire(msg);
        }
        
        // pick up to any random N number of connections and return them to the caller
        // guaranteed to return at least one connection
        private IEnumerable<DataConnection> RandomPickConnections(int numConnections)
        {
            List<DataConnection> returns = new List<DataConnection>(numConnections);

            // do not lock connections here
            int[] drawNumbers = new int[numConnections];
            int cnt = connections.Count;

            for (int i = 0; i < Math.Min( numConnections, cnt) ; i++)
            {
                drawNumbers[i] = UIDGenerator.GetNextInteger(cnt);
                if ( i == 0 || drawNumbers.First(x => x == drawNumbers[i]) == i) // first use of number
                {
                    // add it to the return pool
                    returns.Add(connections[i]);
                }
            }

            return returns;
        }

        // asks the wire to get data from the specific sender
        internal int PullFromPeer(DataHeader header)
        {
            int sentNum = 0;
            lock (connections)
            {
                // if no node has made the request, then make a request out to the wire
                // TODO: maybe i should send out a few requests just in case one of them fails
                if (!connections.Exists(x => x.HasRequest(header.key)) )
                {
                    DataConnection thisCon = connections.FirstOrDefault(x => header.sentFrom.Contains(x.RemoteUID) && x.RemoteUID != LocalID && x.IsConnected);
                    if (thisCon != null)
                    {
                        // ask the sender to give the data
                        //WriteDebug(this.system_id + " requests GET from " + thisCon.RemoteUID);
                        thisCon.AddRequest(header);
                        sentNum++;
                    }
                    else
                    {
                        if (connections.Count > 0)
                        {
                            // make simultaneous requests on different links for an answer
                            foreach (DataConnection backupConn in RandomPickConnections(SIMULATENOUS_REQUESTS))
                            {
                                //WriteDebug(this.system_id + " broadcast requests GET from " + backupConn.RemoteUID);
                                backupConn.AddRequest(header);
                                sentNum++;
                            }
                        }
                        else
                        {
                            WriteDebug(this._localUID + " pullFromPeer because no connections are open");
                        }
                    }
                }
                else
                {
                    // the data has already been requested on any one of the links, just need to wait
                    // but let's check to see if the link is dead
                    DataConnection checkCon = connections.FirstOrDefault(x => x.HasRequest(header.key));
                    if (checkCon != null && !checkCon.IsConnected)
                    {
                        // remove dead connection and try again
                        connections.Remove(checkCon);
                        sentNum+=PullFromPeer(header);
                    }
                    else  if (checkCon != null)
                    {
                        // check version
                        if (checkCon.RemoveOldRequest(header))
                        {
                            // make another request from the new data source
                            sentNum += PullFromPeer(header);
                        }
                        else
                        {
                            // okay, request has already been made and it is the same request
                            sentNum += 1;
                        }
                    }
                    // else: duplicate request is dropped
                }

                if (sentNum == 0)
                {
                    WriteDebug("failed to pull from a peer");
                    //System.Diagnostics.Debug.Assert(false);
                }
            }

           

            return sentNum;
        }

        int IMessageController.PullFromPeer(DataHeader header)
        {
            return this.PullFromPeer(header);
        }

        int IMessageController.PullFromPeer(IEnumerable<DataHeader> headers)
        {
            int i = 0;
            foreach (DataHeader h in headers)
            {
                i+=PullFromPeer(h);
            }
            return i;
        }



        internal int SendToPeer(SendMemoryToPeer message)
        {
            DataConnection thisCon;
            lock (connections)
            {
                thisCon = connections.FirstOrDefault(x => message.PeerList.Contains(x.RemoteUID) && x.RemoteUID != LocalID);
            }

            if (thisCon != null)
            {
                thisCon.SendToRemoteClient(message);
                return 1;
            }
            else
            {
                WriteDebug(this._localUID + " sendToPeer could not find a peer to respond to "+ message.ContentLocation );
                //System.Diagnostics.Debug.Assert(false);
            }
            

            //System.Diagnostics.Debug.Assert(false);

            return 0;
        }

        int IMessageController.SendToPeer(SendMemoryToPeer message)
        {
            return this.SendToPeer(message);
        }

        int IMessageController.SendToPeer(IEnumerable<SendMemoryToPeer> message)
        {
            int i = 0;
            foreach (SendMemoryToPeer mem in message)
            {
                i+=SendToPeer(mem);
            }
            return i;
        }

        // Subscriptions


        /// <summary>
        /// adds a subscription that matches the pattern. Pattern matching is Visual Basic patterns (* for many characters, ? for a single character) 
        /// </summary>
        /// <param name="wildcardString">Case-sensitive string that includes *, ?, and [] for ranges of characters to match.</param>
        public void AddSubscription(string wildcardKey)
        {
            subscription.AddSubscription(GetFullKey(DATA_NAMESPACE, _namespace, wildcardKey), SubscriptionInitiator.Manual);
        }


        /// <summary>
        /// adds a subscription that matches the pattern. Pattern matching is Visual Basic patterns (* for many characters, ? for a single character) 
        /// </summary>
        /// <param name="wildcardString">Case-sensitive string that includes *, ?, and [] for ranges of characters to match.</param>
        internal void AddSubscription(string wildcardKey, SubscriptionInitiator initiator)
        {
            subscription.AddSubscription(GetFullKey(DATA_NAMESPACE, _namespace, wildcardKey), initiator);
        }

        /// <summary>
        /// removes a previously added subscription -- not tested
        /// </summary>
        /// <param name="wildcardKey">The exact string that was added to the subscription.</param>
        public void RemoveSubscription(string wildcardKey)
        {
            subscription.RemoveSubscription(GetFullKey(DATA_NAMESPACE, _namespace, wildcardKey));
        }

        /// <summary>
        /// returns a list of subscriptions
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetSubscriptions()
        {
            return subscription;
        }

        void ISubscriptionChanged.AddedSubscription(Subscription s, string wildcardString, SubscriptionInitiator initiator)
        {
            lock(data)
            {
                foreach (DataEntry item in data.Values)
                {
                    item.subscribed = s.IsSubscribed(item.key);
                }
            }

            // raise events as necessary
            if (this.SubscriptionChanged != null)
            {
                SubscriptionEventArgs args = new SubscriptionEventArgs();
                args.SubscripitonPattern = wildcardString;
                args.Reason = SubscriptionEventReason.Add;
                args.Initiator = initiator;
                SubscriptionChanged.Invoke(this, args);
            }
        }

        void ISubscriptionChanged.RemovedSubscription(Subscription s, string wildcardString)
        {
            dataLock.EnterReadLock();
            try
            {
                foreach (DataEntry item in data.Values)
                {
                    item.subscribed = s.IsSubscribed(item.key);
                }
            }
            finally
            {
                dataLock.ExitReadLock();
            }

            // raise events as necessary
            if (this.SubscriptionChanged != null)
            {
                SubscriptionEventArgs args = new SubscriptionEventArgs();
                args.SubscripitonPattern = wildcardString;
                args.Reason = SubscriptionEventReason.Remove;
                SubscriptionChanged.Invoke(this, args);
            }
        }

        // callback for timer
        private void OnConstructNetwork(object state)
        {
            this.ConstructNetwork();

            //restart timer
            constructNwTimer.Change(mSearchForClientsTimeout, 0);
        }

        /// <summary>
        /// searches for peers on the network using Apple Bonjour -- must be enabled ahead of time??
        /// </summary>
        /// <returns>true if network is constructed using Client.AutoConnect</returns>
        public bool ConstructNetwork()
        {
            
            List<int> keys ;
            lock (PeerDiscovery.DiscoveredPeers)
            {
                keys = new List<int>(PeerDiscovery.DiscoveredPeers.Keys);
            }

            keys.Remove(this.LocalID);
            if (keys.Count == 0)
            {
                // BUG: not all peers know about the other peers in the network
                return false;
            }

            keys.Sort();
            int nextUID = keys.FirstOrDefault(x => x > this.LocalID);
            if (nextUID == 0)
            {
                nextUID = keys[0];
            }
            

            bool hasNextConnection = this.connections.Exists(x => x.RemoteUID == nextUID);
            bool hasAnyConnection = this.connections.Exists(x => x.RemoteUID != nextUID);

            // the next available peer is not connected
            if (!hasNextConnection)
            {
                // remove/re-categorize the old next available peer
                if (constructNwNextPeer != 0)
                {
                    if (constructNwRandomPeer == 0)
                    {
                        constructNwRandomPeer = constructNwNextPeer;
                    }
                    else if (constructNwNextPeer != constructNwRandomPeer)
                    {
                        // too many connections, disconnect but not the random peer
                        List<DataConnection> disconnectConns= this.connections.FindAll(x => x.RemoteUID == constructNwNextPeer && x.IsClientConnection == ConnectionType.Client);
                        foreach (DataConnection d in disconnectConns)
                        {
                            d.Close();
                        }
                    }
                }
                List<EndpointInfo> nextConnInfo;
                lock (PeerDiscovery.DiscoveredPeers)
                {
                    nextConnInfo = PeerDiscovery.DiscoveredPeers[nextUID];
                }
                lock (nextConnInfo)
                {
                    this.OpenClient(nextConnInfo[0].Address, nextConnInfo[0].Port);
                }
                constructNwNextPeer = nextUID;
            }
            else
            {
                constructNwNextPeer = nextUID;
            }
            
            // only pick another connection if there is something other than the next UID to choose from
            if (!hasAnyConnection &&  keys.Count > 1)
            {
                int pickNum = UIDGenerator.GetNextInteger(keys.Count);
                while (keys[pickNum] == nextUID )
                {
                    pickNum = UIDGenerator.GetNextInteger(keys.Count);
                }

                List<EndpointInfo> nextConnInfo;
                lock (PeerDiscovery.DiscoveredPeers)
                {
                    nextConnInfo = PeerDiscovery.DiscoveredPeers[keys[pickNum]];
                }
                lock (nextConnInfo)
                {
                    this.OpenClient(nextConnInfo[0].Address, nextConnInfo[0].Port);
                }
                constructNwRandomPeer = keys[pickNum];
            }
            

            return true;
        }

        private void ConstructSenderThreads()
        {
            this.senderThreads = new Thread[SENDER_THREADS];
            
            for (int i = 0; i < SENDER_THREADS; i++)
            {
                this.senderThreads[i] = new Thread(Thread_ServiceSenders);
                this.senderThreads[i].IsBackground = true;
                this.senderThreads[i].Name = this._description + " sender thread " + i;
                this.senderThreads[i].Start(i);
            }
        }

        /// <summary>
        /// Round robin thread scheduler
        /// </summary>
        /// <param name="obj">integer of the thread number is passed</param>
        private void Thread_ServiceSenders(object obj)
        {
            int offset = (int) obj;
            bool stuff = false;
            while (!killbitSenderThreads)
            {
                do
                {
                    stuff = false;
                    // only write one thing at a time
                    for (int i = offset; i < connections.Count; i = i + SENDER_THREADS)
                    {
                        DataConnection c =null;
                        // make this an atomic check
                        lock (connections)
                        {
                            if ( i < connections.Count)
                                c = connections[i];
                        }
                        if (c != null)
                        {
                            stuff = c.HandleWrite() || stuff;
                        }
                    }
                } while (stuff);
                // wait until something needs to be written
                Thread.Sleep(P2PDictionary.SLEEP_IDLE_SLEEP);
            }
        }

        // Dictionary Interface

        #region Dictionary Interface

        public void Add(string key, object value)
        {
            this[key] = value;
        }

        public void Add(KeyValuePair<string, object> item)
        {
            this[item.Key] = item.Value;
        }

        public ICollection<string> Keys
        {
            get 
            {
                dataLock.EnterReadLock();
                List<string> retValue;
                try
                {
                    retValue = new List<string>(this.data.Where(x => IsFullKeyInNamespace(DATA_NAMESPACE, _namespace, x.Key)).Select(x => GetUserKey(DATA_NAMESPACE, _namespace, x.Key)));
                }
                finally
                {
                    dataLock.ExitReadLock();
                }
                return retValue;
            }
        }

        public ICollection<object> Values
        {
            get { throw new NotImplementedException(); }
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            DataEntry entry = GetEntry(this.data, this.dataLock, GetFullKey(DATA_NAMESPACE, _namespace, item.Key));
            return (entry.subscribed && entry.value.Equals(item.Value));
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            var en = this.GetEnumerator();
            int index = arrayIndex;
            while (en.MoveNext() && index < array.Length)
            {
                array[index] = en.Current;
                index++;
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Tag is consumed solely by the caller
        /// </summary>
        public object Tag
        {
            get;
            set;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            List<KeyValuePair<string, object>>.Enumerator retValue;
            dataLock.EnterReadLock();
            try
            {
                retValue = new List<KeyValuePair<string, object>>(this.data.Where(x => IsFullKeyInNamespace(DATA_NAMESPACE, _namespace, x.Key)).Select(x => new KeyValuePair<string, object>(GetUserKey(DATA_NAMESPACE, _namespace, x.Key), x.Value.value))).GetEnumerator();
            }
            finally
            {
                dataLock.ExitReadLock();
            }
            return retValue;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            List<KeyValuePair<string, object>>.Enumerator retValue;
            dataLock.EnterReadLock();
            try
            {
                retValue = new List<KeyValuePair<string, object>>(this.data.Where(x => IsFullKeyInNamespace(DATA_NAMESPACE, _namespace, x.Key)).Select(x => new KeyValuePair<string, object>(GetUserKey(DATA_NAMESPACE, _namespace, x.Key), x.Value.value))).GetEnumerator();
            }
            finally { dataLock.ExitReadLock(); }
            return retValue;
        }
        #endregion

        // static helpers
        #region static helpers

        /// <summary>
        /// Searches for the next highest free port starting at basePort.
        /// Throws ApplicationException if port not found.
        /// </summary>
        /// <param name="basePort">valid port number</param>
        /// <returns>free port number</returns>
        public static int GetFreePort(int basePort)
        {
            return NetworkUtil.GetFreePort(basePort);
        }
        #endregion


        // interface messages
        #region interface messages
        
        void IMessageController.Notified(NotificationEventArgs args)
        {
            if (this.Notification != null)
            {
                try
                {
                    NotificationEventArgs newarg = new NotificationEventArgs(args._Entry, GetUserKey(DATA_NAMESPACE, _namespace, args.Key ), args.Reason, args.Value);
                    Notification.Invoke(this, newarg);
                }
                catch(Exception ex)
                {
                    System.Diagnostics.Debug.Assert(false);
                }
            }
        }

        void IMessageController.SubscriptionChanged(SubscriptionEventArgs args)
        {
            // raise events as necessary
            if (this.SubscriptionChanged != null)
            {
                try
                {
                    SubscriptionEventArgs newarg = new SubscriptionEventArgs();
                    newarg.SubscripitonPattern = args.SubscripitonPattern;
                    newarg.Reason = args.Reason;
                    SubscriptionChanged.Invoke(this, newarg);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Assert(false);
                }

            }
        }

        string IMessageController.Description
        {
            get 
            { 
                return this._description; 
            }
        }

        List<Tuple<System.Net.IPEndPoint,int, ConnectionType>> IMessageController.ActiveEndPoints
        {
            get 
            {
                List<Tuple<System.Net.IPEndPoint, int, ConnectionType>> list = new List<Tuple<System.Net.IPEndPoint, int, ConnectionType>>(connections.Count);
                lock (this.connections)
                {
                    foreach (DataConnection conn in this.connections)
                    {
                        list.Add(Tuple.Create((System.Net.IPEndPoint) conn.RemoteEndPoint, conn.RemoteUID, conn.IsClientConnection));
                    }
                }
                return list;
            }
        }


        bool IMessageController.IsConnected(int uniqueID)
        {
            if (uniqueID == 0)
                return false;

            if (uniqueID == LocalID)
                return true;

            lock (this.connections)
            {
                return this.connections.Exists(x => x.RemoteUID == uniqueID);
            }
        }

        void IMessageController.Connected(DataConnection conn)
        {
            if (Connected != null)
            {
                try
                {
                    ConnectionEventArgs args = new ConnectionEventArgs();
                    args.EndPoint = conn.RemoteEndPoint;
                    args.RemoteUID = conn.RemoteUID;
                    Connected(this, args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Assert(false);
                }
            }
        }

        void IMessageController.Disconnected(DataConnection conn)
        {
            if (Disconnected != null)
            {
                try
                {
                    ConnectionEventArgs args = new ConnectionEventArgs();
                    args.EndPoint = conn.RemoteEndPoint;
                    args.RemoteUID = conn.RemoteUID;
                    Disconnected(this, args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Assert(false);
                }
            }
        }


        List<EndpointInfo> IMessageController.AllEndPoints
        {
            get
            {
                if (discovery != null)
                {
                    List<EndpointInfo> list = new List<EndpointInfo>(PeerDiscovery.DiscoveredPeers.Count);
                    foreach (List<EndpointInfo> l in PeerDiscovery.DiscoveredPeers.Values)
                    {
                        foreach (EndpointInfo m in l)
                        {
                            list.Add(m);
                        }
                    }

                    return list;
                }
                else
                {
                    return null;
                }
            }
        }

        #endregion

    }
}
