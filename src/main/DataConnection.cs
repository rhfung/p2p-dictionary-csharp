using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

// Reader writer lock
// http://www.bluebytesoftware.com/blog/PermaLink,guid,c4ea3d6d-190a-48f8-a677-44a438d8386b.aspx

namespace com.rhfung.P2PDictionary
{
    // limitation in revision #
    // once it rolls negative, the whole thing falls apart

    enum ConnectionType
    {
        Server,
        Client
    }

    class DataConnection
    {
        public const string DATA_NAMESPACE = "/data";
        public const string CLOSE_MESSAGE = "/close"; 
        public const string SUBSCRIPTIONS_NS = "/subscriptions";
        public const string CONNECTIONS_NS = "/connections";
        public const string PROXY_PREFIX = "/proxy";
        public const string BONJOUR_NS = "/network";
        public const int PROXYPREFIX_REMOVE = 6;
        
        // verbs
        public const string GET = "GET";        // carries payload
        public const string HEAD = "HEAD";      // does not carry payload
        public const string PUT = "PUT";        // PUT creates or overwrites resource; PUT updates or modifies a resource
        public const string DELETE = "DELETE";  // removes a resource
        public const string PUSH = "PUSH";      // not in HTTP/REST: this announces changes but carries no payload

        // response codes
        public const string RESPONSECODE_GOOD = "200";
        public const string RESPONSECODE_PROXY = "305";
        public const string RESPONSECODE_PROXY2 = "307";
        public const string RESPONSECODE_DELETED = "404";

        const string NEWLINE = "\r\n";
        const string SPECIAL_HEADER = "P2P-Dictionary";
        const int BACKLOG = 1024;

        private int local_uid = 0;
        private int remote_uid = 0;

        private volatile bool killBit = false;

        private int adaptive_conflict_bound = P2PDictionary.MAX_RANDOM_NUMBER;

        private Thread runThread;

        public LogInstructions debugBuffer;

        private Dictionary<string, DataEntry> data;
        private ReaderWriterLockSlim dataLock;
        private TcpClient client;
        private NetworkStream netStream;

        private Queue<MemoryStream> sendBuffer;
        private Dictionary<string, DataHeader> receiveEntries;
        private Dictionary<string, SendMemory> sendEntries;
        private Queue<string> sendQueue;


        private volatile ConnectionState state;
        private Subscription keysToListen;
        private ConnectionType _connectionType ;
        // messages
        private IMessageController controller;

        private enum ConnectionState
        {
            NewConnection,
            WebClientConnected,
            PeerNodeConnected,
            Closing,
            Closed
        }

        private enum ResponseAction
        {
            ForwardToSuccessor,
            ForwardToAll,
            DoNotForward
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="loopThread"></param>
        /// <param name="localUID"></param>
        /// <param name="sharedData"></param>
        /// <param name="sharedDataLock">all operations that modify sharedData (add and remove) should lock using this object</param>
        /// <param name="controller"></param>
        /// <param name="keysToListen"></param>
        /// <param name="debug"></param>
        public DataConnection(ConnectionType type, Thread loopThread, int localUID,
            Dictionary<string, DataEntry> sharedData, ReaderWriterLockSlim sharedDataLock, IMessageController controller,
            Subscription keysToListen, LogInstructions debug)
        {
            this._connectionType = type;
            this.runThread = loopThread;
            this.local_uid = localUID;
            this.data = sharedData;
            this.dataLock = sharedDataLock;

            // send and receive queues
            this.sendBuffer = new Queue<MemoryStream>();
            this.sendEntries = new Dictionary<string, SendMemory>();
            this.sendQueue = new Queue<string>();
            this.receiveEntries = new Dictionary<string,DataHeader>();

            this.keysToListen = keysToListen;

            // delegate to send messages
            this.controller = controller;

            this.debugBuffer = debug;
        }

        public override string ToString()
        {
            return local_uid + " to " + remote_uid + " (" + RemoteEndPoint.ToString() + ")";
        }

        /// <summary>
        /// Closes the TCP connection as soon as possible.
        /// </summary>
        public void Close()
        {
            Close(false);
        }

        /// <summary>
        /// Closes the TCP connection as soon as possible.
        /// </summary>
        internal void Close(bool disposing)
        {
            // TODO: need to create an asymmetric close handshake
            SendMemoryToPeer endMsg = new SendMemoryToPeer(CLOSE_MESSAGE, new List<int>() { this.remote_uid });
            ResponseCode(endMsg.MemBuffer, CLOSE_MESSAGE, GetListOfThisLocalID(), 0, 0, 200);
            SendToRemoteClient(endMsg);

            if (!disposing)
            {
                // spin wait
                while (this.state != ConnectionState.Closed)
                {
                    Thread.Sleep(P2PDictionary.SLEEP_WAIT_TO_CLOSE);
                }
            }
        }


        /// <summary>
        /// Closes the TCP connection immediately.
        /// </summary>
        public void Abort()
        {
            if (this.state != ConnectionState.Closed )
            {
                this.state = ConnectionState.Closing;
                this.killBit = true;
            }
            
            // spin wait
            while (this.state != ConnectionState.Closed)
            {
                Thread.Sleep(P2PDictionary.SLEEP_WAIT_TO_CLOSE);
            }
        }

        /// <summary>
        /// Closes the TCP connection immediately and then terminates the thread.
        /// </summary>
        public void Kill()
        {
            this.killBit = true;
            Abort();
            if (!Thread.CurrentThread.Equals(runThread))
            {
                runThread.Abort();
            }
            else
            {
                throw new NotSupportedException("Running thread cannot close itself");
            }
        }

        public bool IsConnected
        {
            get
            {
                return this.client != null && this.client.Connected;
            }
        }

        public ConnectionType IsClientConnection
        {
            get
            {
                return this._connectionType;
            }
        }

        public int LocalUID
        {
            get
            {
                return this.local_uid;
            }
        }

        public int RemoteUID
        {
            get
            {
                return this.remote_uid;
            }
        }

        public System.Net.EndPoint RemoteEndPoint
        {
            get
            {
                if (this.client != null)

                    return this.client.Client.RemoteEndPoint;
                else
                    return null;
            }
        }

        public bool IsWebClientConnected
        {
            get
            {
                return state == ConnectionState.WebClientConnected || state == ConnectionState.NewConnection;
            }
        }

        /// <summary>
        /// Creates a message with LocalUID as the sender list
        /// </summary>
        /// <param name="key">Any dictionary element in the data/* namespace, or the data dictionary itself.</param>
        /// <returns></returns>
        public SendMemoryToPeer CreateResponseMessage(string key)
        {
            return CreateResponseMessage(key, key, GetListOfThisLocalID(), null, null);
        }

        /// <summary>
        /// Creates a message with LocalUID as the sender list
        /// </summary>
        /// <param name="proxyKey">The resource name to return, prefixed with proxy/* for a proxy response.</param>
        /// <param name="key">Any dictionary element in the data/* namespace, or the data dictionary itself.</param>
        /// <returns></returns>
        public SendMemoryToPeer CreateResponseMessage(string key, string proxyKey, List<int> senderPath, List<int>  includeList, List<int> proxyResponsePath)
        {
            SendMemoryToPeer msg = new SendMemoryToPeer(proxyKey, includeList);

            if (key == DATA_NAMESPACE)
            {
                ResponseDictionary(GET, msg.MemBuffer, senderPath, proxyResponsePath, false);
            }
            else
            {
                DataEntry entry = P2PDictionary.GetEntry( this.data, this.dataLock, key);
                System.Diagnostics.Debug.Assert(entry != null);
                if (!entry.subscribed)
                {
                    throw new NotImplementedException();
                }

                Response(GET, proxyKey, senderPath, proxyResponsePath, entry, msg.MemBuffer, false);
            }

            return msg;
        }

        /// <summary>
        /// Adds a packet to the out-buffer of the current connection.
        /// Duplicate content is removed.
        /// </summary>
        /// <param name="msg"></param>
        public void SendToRemoteClient(SendMemory msg)
        {
            lock (sendEntries)
            {
                if (sendEntries.ContainsKey(msg.ContentLocation))
                {
                    // newer content overrides older content sent on the wire
                    sendEntries[msg.ContentLocation] = msg;
                    //sendQueue.Enqueue(msg.ContentLocation);
                }
                else
                {
                    sendEntries.Add(msg.ContentLocation, msg);
                    
                }
            }

            lock (sendQueue)
            {
                sendQueue.Enqueue(msg.ContentLocation);
            }
        }

        /// <summary>
        /// Adds a request to get data from the remote side.
        /// Call RemoveOldRequest() before this method.
        /// </summary>
        /// <param name="h"></param>
        public void AddRequest(DataHeader h)
        {
            lock (this.receiveEntries)
            {
                this.receiveEntries.Add(h.key, h);
            }
            lock(this.sendQueue)
            {
                this.sendQueue.Enqueue(h.key);
            }
        }

        /// <summary>
        /// Add a request to get all data from the remote side.
        /// </summary>
        public void AddRequestDictionary()
        {
            lock (this.receiveEntries)
            {
                this.receiveEntries.Add(DATA_NAMESPACE, new DataHeader(DATA_NAMESPACE, new ETag(0, 0),  this.local_uid)); //trigger a full update
            }
            lock(this.sendQueue)
            {
                this.sendQueue.Enqueue(DATA_NAMESPACE);
            }
        }

        /// <summary>
        /// Removes a previous request based on its contentLocation and old version requested.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>true if the request is removed, false otherwise</returns>
        public bool RemoveOldRequest(DataHeader request)
        {
            lock (this.receiveEntries)
            {
                
                if (receiveEntries.ContainsKey(request.key))
                {
                    DataHeader h = receiveEntries[request.key];
                    ETagCompare result = ETag.CompareETags(h.GetETag(), request.GetETag());
                    if (result == ETagCompare.SecondIsNewer || result == ETagCompare.Conflict)
                    {
                        // another version of the tag arrived, pull this request and have the new data requested
                        this.receiveEntries.Remove(request.key);
                        return true;
                    }
                }
            }

            // this request is the newest
            return false;
        }


        public bool HasRequest(string contentLocation)  
        {
            lock (this.receiveEntries)
            {
                return this.receiveEntries.ContainsKey(contentLocation);
            }
        }

        private void WriteDebug(string msg)
        {
            if (debugBuffer != null)
            {
                debugBuffer.Log(1, msg);
            }
        }

        /// <summary>
        /// Thread's main function.
        /// </summary>
        /// <param name="data">TCP channel for communication. Bi-directional.</param>
        public void ReadLoop(TcpClient data)
        {
            this.client = (TcpClient)data;
            netStream = client.GetStream();
            BinaryReader reader = new BinaryReader(netStream, Encoding.ASCII);

            WriteDebug(this.local_uid +  " Connection " + client.Client.LocalEndPoint.ToString() + " -> " + client.Client.RemoteEndPoint.ToString() + " " + this.runThread.Name);

            try
            {
                while (client.Connected && !killBit)
                { 
                

                    if (netStream.DataAvailable && !killBit) // data being sent on the wire
                    {

                        HandleRead(reader);

                    }// after DataAvailable 
                    else
                    {
                        //Thread.Sleep(15);
                        Thread.Sleep(P2PDictionary.SLEEP_IDLE_SLEEP);
                    }


                    if (state == ConnectionState.Closing ||
                        killBit == true)
                    {
                        // actually close the stream
                        break;
                    }

                    
                        //Thread.Yield();
                        
                    
                }
            }
            catch (SocketException ex)
            {
                // good bye
            }
            catch (IOException ex)
            {
                // good bye
            }
            

            this.state = ConnectionState.Closed;

            WriteDebug(this.local_uid + " Closed "+ this.runThread.Name);
            

            try
            {
                // only report P2P connections
                if (this.remote_uid !=0)
                    controller.Disconnected(this);

                this.client.Close();
            }
            catch
            {
            }
            finally
            {
                this.client = null;
                this.netStream = null;
            }
        }

        private void HandleRead(BinaryReader reader)
        {

            int bytesRead = 0;

            string command = ReadLineFromBinary(reader);
            if (command == null)
                return;


            string[] parts = command.Split(new char[] { ' ' }, 3);


            // pull using a GET or HEAD command
            bytesRead += command.Length + NetworkDelay.LENGTH_NEWLINE;

            if (debugBuffer != null)
                debugBuffer.Log(0, command);

            if (parts[0] == GET || parts[0] == HEAD)
            {
                Dictionary<string, string> headers = ReadHeaders(reader);
                HandleReadGetOrHead(reader, headers, parts[0], parts[1]);
            }
            else if (parts[0] == PUT)
            {
                // TODO: verify that resources with spaces works
                Dictionary<string, string> headers = ReadHeaders(reader);
                string contentLocation = parts[1];
                HandleReadOne(PUT, contentLocation, reader, headers);
            }
            else if (parts[0] == DELETE)
            {
                Dictionary<string, string> headers = ReadHeaders(reader);
                string contentLocation = parts[1];
                HandleReadOne(DELETE, contentLocation, reader, headers);
            }
            else if (parts[0] == PUSH)
            {
                Dictionary<string, string> headers = ReadHeaders(reader);
                string contentLocation = parts[1];
                HandleReadOne(PUSH, contentLocation, reader, headers);
            }
            // handle server 
            else if (parts[0] == "HTTP/1.0" || parts[0] == "HTTP/1.1")
            {
                int readBytes = 0;
                string responseCode = parts[1];// 200, 305, 307, 404

                Dictionary<string, string> headers = ReadHeaders(reader);
                bytesRead += command.Length + NetworkDelay.CountHeaders(headers);
                if (headers.ContainsKey("Content-Length")
                    && headers.ContainsKey("Response-To")
                    && headers["Response-To"] == "GET")
                {
                    readBytes += int.Parse(headers["Content-Length"]);
                }

#if SIMULATION
                            // 8 - 81ms delay in N.America      http://ipnetwork.bgtmo.ip.att.net/pws/network_delay.html
                            // 100 Mb/s link
                            Thread.Sleep(NetworkDelay.GetLatency(8, 81, 13107200, bytesRead));
#endif

                string verb;
                if (responseCode.Equals(RESPONSECODE_PROXY))
                {
                    verb = RESPONSECODE_PROXY;
                }
                else if (responseCode.Equals(RESPONSECODE_PROXY2))
                {
                    verb = RESPONSECODE_PROXY2;
                }
                else// assume RESPONSECODE_GOOD
                {
                    // detect a response to a GET or HEAD request
                    if (headers.ContainsKey("Response-To"))
                    {
                        verb = headers["Response-To"];

                        if (verb.Equals(GET))
                        {
                            if (responseCode.Equals(RESPONSECODE_DELETED))
                                verb = DELETE;
                            else
                                verb = PUT;
                        }
                        else if (verb.Equals(HEAD))
                        {
                            verb = PUSH;  // HEAD carries no payload
                        }
                        else
                        {
                            throw new NotSupportedException("Unsupported verb in Response-To");
                        }
                    }
                    else
                    {
                        throw new NotImplementedException("GET or HEAD required in Response-To");
                    }
                }

                string contentLocation = headers["Content-Location"];

                HandleReadOne(verb, contentLocation, reader, headers);

            }



            else // not a GET command or a HTTP response that server can understand
            {
                //WriteDebug(this.local_uid + " Reading unknown request " + command);

                // finish reading the command, read until a blank line is reached
                do
                {
                    command = ReadLineFromBinary(reader);
                } while (command.Length > 0);

                MemoryStream bufferedOutput = new MemoryStream();
                WriteError405(bufferedOutput);
                lock (sendBuffer)
                {
                    sendBuffer.Enqueue(bufferedOutput);
                }
            }
        }
        
        private void HandleReadGetOrHead(BinaryReader reader, Dictionary<string, string> headers, string verb, string resource)
        {
            bool browserRequest = false;            // detect browser
            MemoryStream bufferedOutput = null;

            bufferedOutput = new MemoryStream();
            StreamWriter writer = new StreamWriter(bufferedOutput, Encoding.ASCII);
            writer.NewLine = NEWLINE;

            // this part is very simple
            // just look up the data that the other side requested and give the data

            // detect for web browser
            //bytesRead += command.Length + NetworkDelay.CountHeaders(headers);


//#if SIMULATION
//                            // 8 - 81ms delay in N.America      http://ipnetwork.bgtmo.ip.att.net/pws/network_delay.html
//                            // 100 Mb/s link
//                            Thread.Sleep(NetworkDelay.GetLatency(8, 81, 13107200, bytesRead));
//#endif

            if (this.state == ConnectionState.NewConnection)
            {
                browserRequest = !headers.ContainsKey(SPECIAL_HEADER);

                // assign remote UID
                if (headers.ContainsKey(SPECIAL_HEADER))
                {
                    int remoteID = int.Parse(headers[SPECIAL_HEADER]);

                    // stop duplicate connections
                    if (controller.IsConnected(remoteID) || remoteID == this.local_uid)
                    {
                        //force close
                        this.remote_uid = remoteID;
                        this.state = ConnectionState.Closing;
                        browserRequest = true;
                    }
                    else
                    {
                        // finish the connection
                        this.remote_uid = remoteID;
                        this.state = ConnectionState.PeerNodeConnected;

                        controller.Connected(this);
                    }
                }
                else
                {
                    this.state = ConnectionState.WebClientConnected;
                }
            }

            string printEtag = "[nover]";

            if (headers.ContainsKey("ETag"))
            {
                printEtag = headers["ETag"];

            }

            // see which resource is being accessed
            // latter half of condition is only for web browsers
            if (resource == DATA_NAMESPACE || resource == DATA_NAMESPACE + "/")
            {
                // whole dictionary
                ResponseDictionary(verb, bufferedOutput, GetListOfThisLocalID(), null, browserRequest);
            }
            else if (resource == "/")
            {
                ResponseIndex(verb, writer, browserRequest);
            }
            else if (resource == CLOSE_MESSAGE || resource == CLOSE_MESSAGE + "/")
            {
                // don't know what to do here
                WriteErrorNotFound(writer, verb, CLOSE_MESSAGE, 200);
            }
            else if (resource == SUBSCRIPTIONS_NS || resource == SUBSCRIPTIONS_NS + "/")
            {

                ResponseSubscriptions(verb, writer, browserRequest);
            }
            else if (resource == CONNECTIONS_NS || resource == CONNECTIONS_NS + "/")
            {

                ResponseConnections(verb, writer, browserRequest);
            }
            else if (resource == BONJOUR_NS || resource == BONJOUR_NS + "/")
            {
                ResponseBonjour(verb, writer, browserRequest);
            }
            else if (this.data.ContainsKey(resource))
            {
                // handles current and expired data
                DataEntry entry = P2PDictionary.GetEntry(this.data, this.dataLock, resource); //this.data[parts[1]];
                lock (entry)
                {
                    if (entry.subscribed && !DataMissing.IsSingleton(entry.value))
                    {
                        // give the caller the data
                        Response(verb, resource, GetListOfThisLocalID(), null, entry, bufferedOutput, browserRequest);
                    }
                    else
                    {
                        // tell the caller to look somewhere else
                        //if (IsWebClientConnected)
                        //{
                        // tell the caller that a proxy must be used
                        ResponseCode(bufferedOutput, resource, GetListOfThisLocalID(), entry.lastOwnerID, entry.lastOwnerRevision, 305);
                        //}
                        //else
                        //{

                        //    // tell the caller that a proxy must be used
                        //    ResponseCode(bufferedOutput, parts[1], GetListOfThisLocalID(), headers["P2P-Path"], entry.lastOwnerID, entry.lastOwnerRevision, 305);
                        //}
                    }
                }
            }
            else if (resource.StartsWith(PROXY_PREFIX + "/", StringComparison.Ordinal))
            {
                throw new NotImplementedException();
            }
            else
            {
                // anything else
                WriteErrorNotFound(writer, verb, resource, 404, GetListOfThisLocalID());
            }

            // spit everything out of the writer
            writer.Flush();
            lock (sendBuffer)
            {
                sendBuffer.Enqueue(bufferedOutput);
            }
        }

        private void HandleReadPut(string contentLocation, string contentType, byte[] readData, string eTag, List<int> senders, List<int> responsePath)
        {
            // process the packet
                if (contentLocation == DATA_NAMESPACE)
                {
                    List<DataHeader> missingData = new List<DataHeader>();
                    List<SendMemoryToPeer> sendBack = new List<SendMemoryToPeer>();
                    ReadDictionaryTextFile(new StreamReader(new MemoryStream(readData)), new List<int>(senders), missingData, sendBack);

                    // and then update my copy of the dictionary 
                    controller.PullFromPeer(missingData);

                    // and update the sender's dictionary
                    controller.SendToPeer(sendBack);
                }
                else
                {
                    if (contentLocation.StartsWith(PROXY_PREFIX + "/"))
                    {
                        // this is a pushed message from a proxy request
                        // so I should subscribe to the key

                        contentLocation = contentLocation.Substring(PROXYPREFIX_REMOVE);
                        if (!keysToListen.IsSubscribed(contentLocation))
                        {
                            keysToListen.AddSubscription(contentLocation, SubscriptionInitiator.AutoProxyKey);
                        }

                    }

                    ResponseAction status = ReadData(contentLocation, eTag, contentType, new List<int>(senders), readData);

                    // data propagation for following a proxy
                    if (responsePath != null)
                    {
                        List<int> followPath = new List<int>(responsePath);
                        followPath.Remove(this.remote_uid);

                        // send data along the path
                        senders.Add(this.local_uid);
                        SendMemoryToPeer sendMsg = CreateResponseMessage(contentLocation, PROXY_PREFIX + contentLocation, senders, followPath, responsePath);
                        controller.SendToPeer(sendMsg);
                    }

                    if (status == ResponseAction.ForwardToAll)
                    {
                        //conflict happened in data somewhere
                        // return new data to sender
                        senders.Clear();
                    }

                    if (status != ResponseAction.DoNotForward)
                    {
                        // add my sender to the packet
                        senders.Add(this.local_uid);

                        // add to wire to send out
                        SendBroadcastMemory sendMsg = new SendBroadcastMemory(contentLocation, senders);
                        
                        DataEntry get = P2PDictionary.GetEntry(this.data, this.dataLock, contentLocation);
                        System.Diagnostics.Debug.Assert(get != null);

                        WriteMethodPush(contentLocation, senders, responsePath, 0, get.GetMime(), get.GetETag(), get.IsDeleted, false, sendMsg.MemBuffer);
                        //Response(verb, contentLocation, senders, this.data[contentLocation], sendMsg.MemBuffer, false);
                        controller.BroadcastToWire(sendMsg);
                    }

                }
        }

        private void HandleReadPush(string contentLocation, string contentType, string eTag, List<int> senders, int lastSender, List<int> responsePath)
        {
            ETag tag = ReadETag(eTag);

            if (contentLocation == DATA_NAMESPACE)
            {
                //// tell others that a new dictionary entered
                //// add to wire to send out
                //SendMemory sendMsg = new SendMemory(senders);
                //ResponseDictionary(verb, sendMsg.MemBuffer, senders, false);
                //controller.BroadcastToWire(sendMsg);

                // don't forward message because the GET method call will do it

                // let me update my model first by
                // requesting to pull data from the other side
                // before sending out a HEAD
                DataHeader hdr = new DataHeader(contentLocation, tag, lastSender);
                controller.PullFromPeer(hdr);

            }
            else
            {
                if (contentLocation.StartsWith(PROXY_PREFIX + "/"))
                {
                    // this is a pushed message from a proxy request
                    // so I should subscribe to the key

                    contentLocation = contentLocation.Substring(PROXYPREFIX_REMOVE);
                    throw new NotImplementedException();
                }

                DataHeader getEntryFromSender;
                SendMemoryToPeer addEntryToSender;

                ResponseAction action = ReadDataStub(contentLocation, contentType, eTag, new List<int>(senders), out getEntryFromSender, out addEntryToSender);

                if (action == ResponseAction.ForwardToAll)
                {
                    senders.Clear();
                }

                if (action != ResponseAction.DoNotForward)
                {
                    // forward a HEAD message (because we didn't do it when we got a 200/HEAD notification)
                    DataEntry get = P2PDictionary.GetEntry(this.data, this.dataLock, contentLocation);
                    System.Diagnostics.Debug.Assert(get != null);

                    senders.Add(this.local_uid);
                    SendBroadcastMemory sendMsg = new SendBroadcastMemory(contentLocation, senders);
                    WriteMethodPush(contentLocation, senders, responsePath, 0, get.GetMime(), get.GetETag(), get.IsDeleted, false, sendMsg.MemBuffer);
                    controller.BroadcastToWire(sendMsg);
                }

                if (getEntryFromSender != null)
                {
                    // and get data from the caller
                    controller.PullFromPeer(getEntryFromSender);
                }


                if (addEntryToSender != null)
                {
                    // send any updates to the peer
                    controller.SendToPeer(addEntryToSender);
                }
            }
        }

        private void HandleReadDelete(string contentLocation,string eTag, List<int> senders, List<int> responsePath )
        {
            // handle proxy messages
            if (contentLocation.StartsWith(PROXY_PREFIX + "/"))
            {
                contentLocation = contentLocation.Substring(PROXYPREFIX_REMOVE);
                if (!keysToListen.IsSubscribed(contentLocation))
                {
                    keysToListen.AddSubscription(contentLocation, SubscriptionInitiator.AutoProxyKey);
                }
            }

            // read
            ResponseAction status = ReadDelete(contentLocation, eTag, new List<int>(senders));

            if (status == ResponseAction.ForwardToAll)
            {
                //conflict happened in data somewhere
                // return new data to sender
                senders.Clear();
            }

            if (status != ResponseAction.DoNotForward)
            {
                // send a notification of deleted content immediately
                DataEntry entry = P2PDictionary.GetEntry(this.data, this.dataLock, contentLocation);
                System.Diagnostics.Debug.Assert(entry != null);

                // add to wire to send out
                senders.Add(this.local_uid);// add my sender to the packet
                SendBroadcastMemory sendMsg = new SendBroadcastMemory(entry.key, senders);
                WriteMethodDeleted(sendMsg.MemBuffer, contentLocation, senders, responsePath, entry.lastOwnerID, entry.lastOwnerRevision);
                controller.BroadcastToWire(sendMsg);
            }

            if (responsePath != null)
            {
                // well, i still have to send out this message because there is a path requested to follow
                DataEntry entry = P2PDictionary.GetEntry(this.data, this.dataLock, contentLocation);
                System.Diagnostics.Debug.Assert(entry != null);

                SendMemoryToPeer sendMsg = new SendMemoryToPeer(entry.key, responsePath);
                senders.Add(this.local_uid);// add my sender to the packet
                WriteMethodDeleted(sendMsg.MemBuffer, PROXY_PREFIX + contentLocation, senders, responsePath, entry.lastOwnerID, entry.lastOwnerRevision);

            }
        }
        
        /// <summary>
        /// Handle action verbs
        /// </summary>
        /// <param name="verb">PUT, DELETE, PUSH, 305 (construct proxy path), 307 (follow proxy path)</param>
        /// <param name="reader"></param>
        /// <param name="headers">prepopulated headers from HTTP</param>
        private void HandleReadOne(string verb, string contentLocation, BinaryReader reader, Dictionary<string, string> headers)
        {
            byte[] readData = null;
            List<int> senders = new List<int>(10);

            // do a bunch of checks before processing the packet

            // assign remote UID
            if (headers.ContainsKey(SPECIAL_HEADER))
            {
                // also change state of app
                if (state == ConnectionState.NewConnection)
                {
                    state = ConnectionState.PeerNodeConnected;
                    this.remote_uid = int.Parse(headers[SPECIAL_HEADER]);
                }
            }

            // read data if GET request
            if (headers.ContainsKey("Content-Length") && !verb.Equals(PUSH))
            {
                int length = int.Parse(headers["Content-Length"]);
                readData = reader.ReadBytes(length);

                if (debugBuffer != null)
                {
                    MemoryStream s = new MemoryStream(readData);
                    debugBuffer.Log(0, s);
                }

            }
            else 
            {
                // no data was sent; this is a notification
            }

            // inspect the sender list
            int lastSender = 0;
            if (headers.ContainsKey("P2P-Sender-List"))
            {
                // save the list of senders
                senders.AddRange(GetArrayOf(headers["P2P-Sender-List"]));
                lastSender = senders[senders.Count - 1];
            }
            else
            {
                throw new NotImplementedException();
            }

            // inspect for a response path
            List<int> responsePath = null;
            if (headers.ContainsKey("P2P-Response-Path"))
            {
                responsePath =  new List<int>( GetArrayOf(headers["P2P-Response-Path"]));
            }


            // inspect for a closing command issued by the caller,
            // which happens when this is a duplicate connection
            if (headers.ContainsKey("Connection"))
            {
                if (headers["Connection"] == "close")
                {
                    this.state = ConnectionState.Closing;
                }
            }

            WriteDebug(this.local_uid + " read " + verb + " " +  contentLocation + " from " + this.remote_uid +  " Senders: " + headers["P2P-Sender-List"]);

            // !senders.Contains(this.local_uid) --> if message hasn't been stamped by this node before...

            if (!senders.Contains(this.local_uid) && verb.Equals(DELETE) && headers.ContainsKey("ETag"))
            {
                HandleReadDelete(contentLocation, headers["ETag"], senders, responsePath);
            }
            else if (!senders.Contains(this.local_uid) && contentLocation.Equals(CLOSE_MESSAGE))
            {
                this.state = ConnectionState.Closing;
                this.killBit = true;
            }
            else if (!senders.Contains(this.local_uid) &&  verb.Equals(PUT))
            {
                HandleReadPut(contentLocation, headers["Content-Type"], readData, headers["ETag"], senders, responsePath);
            }
            else if (!senders.Contains(this.local_uid) && verb.Equals(PUSH))
            {
                HandleReadPush(contentLocation, headers["Content-Type"], headers["ETag"], senders, lastSender, responsePath);
            }
            else if (!senders.Contains(this.local_uid) && verb.Equals(RESPONSECODE_PROXY))
            {
                SendMemoryToPeer mem = RespondOrForwardProxy(PROXY_PREFIX + contentLocation, new List<int>());
                if (mem != null)
                {
                    // well, I already have the result so why is 305 being used
                    // should broadcast the result
                    //mem.Senders = new List<int>(1) { lastSender };
                    //controller.SendToPeer(mem);
                    controller.SendToPeer(mem);
                }
                else
                {
                    // TODO: figure out what this does
                }
            }
            else if (!senders.Contains(this.local_uid) && verb.Equals(RESPONSECODE_PROXY2))
            {

                SendMemoryToPeer mem = RespondOrForwardProxy(contentLocation, new List<int>(senders));
                if (mem != null)
                {
                    // well, I already have the result so why is 305 being used
                    // should broadcast the result
                    //mem.Senders = new List<int>(1) { lastSender };
                    //controller.SendToPeer(mem);
                    controller.SendToPeer(mem);
                }
                else
                {
                    // TODO: figure out what this does
                }
            }
            else if (senders.Contains(this.local_uid))
            {
                // drop packet , already read
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Entrance for writing to a thread
        /// <returns>true if more data needs to be written</returns>
        /// </summary>
        public bool HandleWrite()
        {
            MemoryStream bufferedOutput = null;

            
            if (netStream == null)
                return false;

            if (killBit)
                return false;

            if (this.state == ConnectionState.Closed || this.state == ConnectionState.Closing)
                return false;

            lock (sendBuffer)
            {
                if (sendBuffer.Count > 0)
                    bufferedOutput = sendBuffer.Dequeue();
            }

            if (bufferedOutput != null)
            {
                try
                {

                    bufferedOutput.WriteTo(netStream);
                    if (debugBuffer != null)
                    {
                        debugBuffer.Log(1, this.local_uid + " wrote memory to " + this.remote_uid);
                        debugBuffer.Log(0, bufferedOutput);
                    }
#if SIMULATION
                    Thread.Sleep(NetworkDelay.GetLatency(8, 81, 13107200, bufferedOutput.Length));
#endif
                    bufferedOutput.Dispose();
                    }
                catch
                {
                }

                if (this.state == ConnectionState.WebClientConnected)
                {
                    this.state = ConnectionState.Closing;
                }
            }

            // bounded by number of dictionary entries
            // messages can still be pushed to others, especially close


            if (bufferedOutput == null)
            {
                string key="";
                lock (sendQueue)
                {
                    if (sendQueue.Count > 0)
                    {
                        key = sendQueue.Dequeue();
                    }
                }

                MemoryStream srcStream=null;
                DataHeader hdr=null;


                // pull message (won't be cascading to other peers)
                lock (receiveEntries)
                {
                    if (receiveEntries.ContainsKey(key))
                    {
                        hdr = receiveEntries[key];
                        receiveEntries.Remove(key);
                    }
                }

                // write to buffer
                if (hdr != null)
                {
                    bufferedOutput = new MemoryStream();
                    WriteSimpleGetRequest(bufferedOutput, hdr);

                }

                // get push message
                lock (sendEntries)
                {
                    if (sendEntries.ContainsKey(key))
                    {
                        srcStream = sendEntries[key].MemBuffer;
                        sendEntries.Remove(key);

                        if (key == CLOSE_MESSAGE)
                        {
                            this.state = ConnectionState.Closing;
                        }

                    }
                }


                // package up push message
                if (srcStream != null)
                {

                    if (bufferedOutput == null)
                    {
                        bufferedOutput = new MemoryStream();
                    }
                    srcStream.WriteTo(bufferedOutput);
                }

                // wire everything off in a pair
                if (bufferedOutput != null)
                {
                    try
                    {
                        if (netStream != null && !killBit)//check again because other thread may have cleared the netStream
                        {
                            bufferedOutput.WriteTo(netStream);
                            if (debugBuffer != null)
                            {
                                debugBuffer.Log(1, this.local_uid + " wrote " + key + " to " + this.remote_uid);
                                debugBuffer.Log(0, bufferedOutput);
                            }
#if SIMULATION
                            Thread.Sleep(NetworkDelay.GetLatency(8, 81, 13107200, bufferedOutput.Length));
#endif
                        }
                        bufferedOutput.Dispose();

                    }
                    catch
                    {
                    }
                }
                    

                    
            }



            return (sendBuffer.Count > 0) || (sendEntries.Count > 0) || (receiveEntries.Count > 0);
            
        }

        private  string ReadLineFromBinary(BinaryReader reader)
        {
            StringBuilder builder = new StringBuilder();
            byte byte1 = reader.ReadByte();
            byte byte2 = reader.ReadByte();

            while (byte1 != 13 && byte2 != 10)
            {
                builder.Append((char)byte1);

                byte1 = byte2;
                byte2 = reader.ReadByte();

            }

            return builder.ToString().TrimEnd('\r');
        }

        /// <summary>
        /// Fills bufferedOutput with the response, or asks the controller to pull the contentLocation from another peer.
        /// </summary>
        /// <param name="contentLocation">A location prefixed with /proxy.</param>
        /// <param name="requestPath">Path that the request should follow.</param>
        /// <returns>A new object to reply to the sender</returns>
        private SendMemoryToPeer RespondOrForwardProxy(string contentLocation, List<int> senderList)
        {
            // first 6 characters of /proxy are removed
            bool proxyPart = contentLocation.StartsWith(PROXY_PREFIX + "/");

            if (proxyPart == false)
                throw new NotSupportedException("RespondOrForwardProxy no proxy part");

            string key = contentLocation.Substring(PROXYPREFIX_REMOVE);
            List<int> hintPath = null;

            // cannnot proxy request the whole dictionary
            if (key == DATA_NAMESPACE)
                throw new NotSupportedException("RespondOrForwardProxy DATA_NAMESPACE");

            bool responded = false;


            DataEntry e = P2PDictionary.GetEntry( this.data, this.dataLock, key);
            if (e != null)
            {
                WriteDebug(this.local_uid + " following proxy path, found content for " + key);

                lock (e)
                {
                    if (e.subscribed && !DataMissing.IsSingleton(e.value))
                    {
                        responded = true;

                    }

                }

                if (responded)
                {
                    // change the return path of the response message
                    List<int> followList = new List<int>(senderList);
                    followList.Remove(this.local_uid);

                    SendMemoryToPeer sendMsg = CreateResponseMessage(key, PROXY_PREFIX + key, GetListOfThisLocalID(), followList, followList);

                    return sendMsg;
                }

                hintPath = e.senderPath;

            }

            

            if (!responded)
            {
                // fix the requestPath with the hintPath if there is no requestPath,
                // or if the requestPath is now at the current peer
                if (hintPath == null || hintPath.Count  == 0)
                {
                    WriteDebug(this.local_uid + " forwarding request dropped " + key);
                }
                else
                {

                    // since the path contains all the nodes to contact in order,
                    // we don't have to broadcast a request. Instead, we just specify
                    // the path to the next peer and it will get to the destination.
                        senderList = new List<int>(senderList);
                        senderList.Add(this.local_uid);

                    
                        WriteDebug(this.local_uid + " following proxy path " + key + " to " + GetStringOf(hintPath));
                        SendMemoryToPeer sendMsg = new SendMemoryToPeer(PROXY_PREFIX + key, hintPath);
                        ResponseFollowProxy(sendMsg.MemBuffer, PROXY_PREFIX + key, senderList);
                        return sendMsg;
                    }
                
                
               
            }

            return null;
        }


        // respond to GET/HEAD or push data on wire
        private void ResponseDictionary(string verb, MemoryStream bufferedOutput, List<int> senderList, List<int> proxyResponsePath, bool willClose)
        {
            StreamWriter writer = new StreamWriter(bufferedOutput, Encoding.ASCII);
            
            writer.NewLine = NEWLINE;

            string file = GetDictionaryAsTextFile();
            WriteResponseHeader(writer, DATA_NAMESPACE, "text/plain", file.Length, this.local_uid, 0, senderList, proxyResponsePath, verb, willClose);
            if (verb == GET)
            {
                writer.Write(file);
            }
            writer.Flush();
            
        }

        private void ResponseIndex(string verb, StreamWriter writer, bool willClose)
        {
            string file = string.Format(Properties.Resources.index, this.controller.Description);
            WriteResponseHeader(writer, DATA_NAMESPACE, "text/html", file.Length, this.local_uid, 0, GetListOfThisLocalID(), null,verb, willClose);
            if (verb == GET)
            {
                writer.Write(file);
            }
            writer.Flush();
        }


        private void ResponseSubscriptions(string verb, StreamWriter writer, bool willClose)
        {
            string file = string.Join("\r\n", this.keysToListen);
            WriteResponseHeader(writer, DATA_NAMESPACE, "text/plain", file.Length, this.local_uid, 0, GetListOfThisLocalID(), null,verb, willClose);
            if (verb == GET)
            {
                writer.Write(file);
            }
            writer.Flush();
        }

        private void ResponseBonjour(string verb, StreamWriter writer, bool willClose)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var conn in this.controller.AllEndPoints)
            {
                builder.AppendFormat("{0}\t{1}:{2}\r\n", conn.UID, conn.Address.ToString(), conn.Port);
            }
            string file = builder.ToString();
            WriteResponseHeader(writer, DATA_NAMESPACE, "text/plain", file.Length, this.local_uid, 0, GetListOfThisLocalID(),null, verb, willClose);
            if (verb == GET)
            {
                writer.Write(file);
            }
            writer.Flush();
        }

        private void ResponseConnections(string verb, StreamWriter writer, bool willClose)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var conn in this.controller.ActiveEndPoints)
            {
                string part1 = "";
                string part2 = "";
                string part3 = "";

                if (conn.Item1 != null)
                {
                    part1 = conn.Item1.ToString();
                }
                else
                {
                    part1 = "disconnected";
                }
                if (conn.Item2 != 0)
                {
                    part2 = conn.Item2.ToString();
                }
                else{
                    part2 = "web-browser";
                }

                if (conn.Item3 == ConnectionType.Client)
                {
                    part3 = "client";
                }
                else{
                    part3 = "server";
                }
                    
                        builder.AppendFormat("{0}\t{1}\t{2}\r\n", part1, part2, part3);
                    
                    
            }
            string file = builder.ToString();
            WriteResponseHeader(writer, DATA_NAMESPACE, "text/plain", file.Length, this.local_uid, 0, GetListOfThisLocalID(),null, verb, willClose);
            if (verb == GET)
            {
                writer.Write(file);
            }
            writer.Flush();
        }

        private void WriteMethodPush(string resource,List<int> senderList, List<int> proxyResponsePath, int contentLength,
            string mimeType, ETag lastVer, bool isDeleted, bool willClose, MemoryStream bufferedOutput)
        {
            StreamWriter writer = new StreamWriter(bufferedOutput, Encoding.ASCII);

            writer.NewLine = NEWLINE;

            if (isDeleted)
            {
                WriteMethodDeleted(bufferedOutput, resource, senderList, proxyResponsePath, lastVer.UID, lastVer.Revision);
            }
            else
            {
                WriteMethodHeader(writer, resource, mimeType, contentLength, lastVer.UID, lastVer.Revision, senderList, proxyResponsePath, willClose);
            }
        }



        ///// <summary>
        ///// respond only by header
        ///// </summary>
        ///// <param name="verb">GET or HEAD</param>
        ///// <param name="resource">Same as key or contentLocation</param>
        ///// <param name="senderList"></param>
        ///// <param name="contentLength"></param>
        ///// <param name="mimeType"></param>
        ///// <param name="lastVer"></param>
        ///// <param name="isDeleted">Indicates that the current entry is deleted</param>
        ///// <param name="willClose">Writes a close message in the header</param>
        ///// <param name="bufferedOutput">A memory buffer that will be filled with contents produced
        ///// from this method</param>
        //private void ResponseHeadStub(string verb, string resource, List<int> senderList, List<int> proxyResponsePath, int contentLength,
        //    string mimeType, ETag lastVer, bool isDeleted, bool willClose, MemoryStream bufferedOutput)
        //{
        //    StreamWriter writer = new StreamWriter(bufferedOutput, Encoding.ASCII);
            
        //        writer.NewLine = NEWLINE;

        //        if (isDeleted)
        //        {
        //            WriteResponseDeleted(bufferedOutput, resource, senderList, proxyResponsePath, lastVer.UID, lastVer.Revision);
        //        }
        //        else
        //        {
        //            WriteResponseHeader(writer, resource, mimeType, contentLength, lastVer.UID, lastVer.Revision, senderList, proxyResponsePath, verb, willClose);
        //        }
            
        //}

        // respond to a GET request, HEAD request, and push data on wire
        private void Response(string verb, string resource, List<int> senderList,List<int> proxyResponsePath,  DataEntry entry, MemoryStream bufferedOutput, bool willClose)
        {

                lock (entry)
                {
                    if (entry.IsEmpty)
                    {
                        WriteResponseDeleted(bufferedOutput, resource, senderList, proxyResponsePath, entry.lastOwnerID, entry.lastOwnerRevision);
                    }
                    
                    else if (entry.IsSimpleValue || entry.type == DataEntry.ValueType.String)
                    {
                        StreamWriter writer = new StreamWriter(bufferedOutput, Encoding.ASCII);
                        writer.NewLine = NEWLINE;


                        
                        string translation ="";
                        if (entry.value != null)
                            translation = entry.value.ToString();

                        byte[] bytesToWrite = System.Text.Encoding.UTF8.GetBytes(translation);

                        WriteResponseHeader(writer, resource, entry.GetMime(), bytesToWrite.Length, entry.lastOwnerID, entry.lastOwnerRevision, senderList, proxyResponsePath, verb, willClose);
                        writer.Flush();
                        if (verb == GET)
                        {
                            bufferedOutput.Write(bytesToWrite, 0, bytesToWrite.Length);
                            //StreamWriter tw = new StreamWriter(bufferedOutput, Encoding.UTF8);
                            //    tw.NewLine = NEWLINE;

                            //    tw.BaseStream.Seek(0, SeekOrigin.End);
                            //    tw.Write(translation);
                            //    tw.Flush();
                            
                        }
                        writer.Flush();
                    }
                    else if (entry.IsComplexValue)
                    {
                        if (entry.type == DataEntry.ValueType.Binary)
                        {
                            StreamWriter writer = new StreamWriter(bufferedOutput, Encoding.ASCII);
                            writer.NewLine = NEWLINE;

                            byte[] bentry = (byte[])entry.value;
                            WriteResponseHeader(writer, resource, entry.GetMime(), bentry.Length, entry.lastOwnerID, entry.lastOwnerRevision, senderList, proxyResponsePath, verb, willClose);
                            writer.Flush();

                            if (verb == GET)
                            {
                                bufferedOutput.Write(bentry, 0, bentry.Length);
                            }

                        }
                        else if (entry.type == DataEntry.ValueType.Object)
                        {
                            StreamWriter writer = new StreamWriter(bufferedOutput, Encoding.ASCII);
                            writer.NewLine = NEWLINE;

                            MemoryStream bentry;
                            try
                            {
                                bentry = new MemoryStream();
                                BinaryFormatter formatter = new BinaryFormatter();
                                formatter.Serialize(bentry, entry.value);
                            }
                            catch
                            {
                                throw;
                            }

                            System.Diagnostics.Debug.Assert(bentry.Length <= int.MaxValue);

                            WriteResponseHeader(writer, resource, entry.GetMime(), (int)bentry.Length, entry.lastOwnerID, entry.lastOwnerRevision, senderList, proxyResponsePath, verb, willClose);
                            writer.Flush();

                            if (verb == GET)
                            {

                                bentry.WriteTo(bufferedOutput);
                            }
                        }
                        else if (entry.type == Data.ValueType.Unknown)
                        {
                            StreamWriter writer = new StreamWriter(bufferedOutput, Encoding.ASCII);
                            writer.NewLine = NEWLINE;

                            byte[] bentry = ((DataUnsupported) entry.value).GetPayload();
                            WriteResponseHeader(writer, resource, entry.GetMime(), bentry.Length, entry.lastOwnerID, entry.lastOwnerRevision, senderList, proxyResponsePath, verb, willClose);
                            writer.Flush();

                            if (verb == GET)
                            {
                                bufferedOutput.Write(bentry, 0, bentry.Length);
                            }
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        

                    }

                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            
        }

        // read the format
        // key: value 
        // lines until a blank line is read
        private Dictionary<string, string> ReadHeaders(BinaryReader reader)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(8);

            string command = ReadLineFromBinary(reader);
            while (command.Length > 0)
            {
                string[] read = command.Split(new char[] { ':' }, 2);
                headers.Add(read[0], read[1].Trim());

                command = ReadLineFromBinary(reader);

                if (debugBuffer != null)
                    debugBuffer.Log(0, command, false);
            }

            if (debugBuffer != null)
                debugBuffer.Log(0, command);

            return headers;
        }

        /// <summary>
        /// Gets a text file that is in the following format:
        /// 
        /// First row
        ///     /data       _UID_   0       RW  _CNT_       _UID_ 
        /// Subsequent rows
        ///     /data/_key  _UID_   _REV_   RW  mime-type   _SENDERS_
        /// </summary>
        /// <returns></returns>
        private string GetDictionaryAsTextFile()
        {
            using (StringWriter writer = new StringWriter())
            {
                writer.NewLine = NEWLINE;// CR LF

                List<DataEntry> entries;
                this.dataLock.EnterWriteLock();
                try
                {
                    entries = new List<DataEntry>(this.data.Count);
                    //entries.AddRange(this.data.Values.Where(x => x.subscribed));
                    entries.AddRange(this.data.Values);
                }
                finally { this.dataLock.ExitWriteLock(); }

                // write count of data entries
                writer.WriteLine(DATA_NAMESPACE + "/\t" + this.local_uid + "\t0\tRW\t" + entries.Count + "\t" + this.local_uid);

                // write each data entry, converting simple data immediately
                // (pretend i don't know about these non-subscribed entries)
                foreach (DataEntry d in entries)
                {
                        string permissions = "";

                        if (d.subscribed)
                        {
                            
                            if (DataMissing.IsSingleton(d.value))
                            {
                                permissions = "-W";
                            }
                            else
                            {
                                permissions = "RW";
                            }
                        }
                        else
                        {

                            if (DataMissing.IsSingleton(d.value))
                            {
                                permissions = "--";
                            }
                            else
                            {
                                permissions = "=-";

                            }

                        }

                        writer.WriteLine(d.key + "\t" + d.lastOwnerID + "\t" + d.lastOwnerRevision + "\t" 
                            + permissions + "\t" + d.GetMime() + d.GetMimeSimpleData() + "\t"
                            + GetStringOf(d.senderPath));
                    
                }

                return writer.ToString();
            }
        }


        // randomize the adding so somebody will eventually win over the others since everyone wants to 
        // say that they are the "correct" one
        private int IncrementRevisionRandomizer(int originalRevision)
        {
            return originalRevision + UIDGenerator.GetNextInteger(adaptive_conflict_bound) + 1;
        }



        /// <summary>
        /// TODO: add in some code to reduce round-trips to simple data types 
        /// </summary>
        /// <param name="reader">File to read</param>
        /// <param name="sentFromList">Sent from list</param>
        /// <param name="getEntriesFromSender">This function fills in a list of entries that need to be requested from the sender</param>
        /// <param name="addEntriesToSender">These are entries that the sender does not know about</param>
        /// <seealso cref="ReadHeadStub"/>
        private void ReadDictionaryTextFile(StreamReader reader, List<int> sentFromList, List<DataHeader> getEntriesFromSender, List<SendMemoryToPeer> addEntriesToSender)
        {
            // 0 - key name
            // 1 - owner
            // 2 - revision
            // 3 - rw flag
            // 4 - MIME type

            // WriteDebug(this.local_uid + " ReadDictionaryTextFile");

            string nsLine = reader.ReadLine();
            string[] nsLineParts = nsLine.Split('\t');

            System.Diagnostics.Debug.Assert(nsLineParts[0] == DATA_NAMESPACE + "/");

            // if the owner of the dictionary is the same as myself, skip reading the changes
            if (nsLineParts[1] == this.local_uid.ToString())
            {
                throw new NotSupportedException("ReadDictionaryTextFile cannot read itself");// i want to see if this actually can happen (only when multiple connections happen on the same server)
              
            }

            int itemCount = int.Parse(nsLineParts[4]); // count of all the items in the dictionary

            List<DataEntry> entriesCovered = new List<DataEntry>(itemCount + this.data.Count);

            for (int i = 0; i < itemCount; i++)
            {
                nsLine = reader.ReadLine();
                nsLineParts = nsLine.Split('\t');
                //WriteDebug(nsLine);

                DataHeader getEntry = null;
                SendMemoryToPeer addEntryToSender = null;

                ETag tag = new ETag(int.Parse(nsLineParts[1]), int.Parse(nsLineParts[2]));

                // this entry is used only to call ReadMimeSimpleData
                DataEntry fakeEntry = new DataEntry("/fake", tag, new List<int>(0));
                fakeEntry.ReadMimeSimpleData(nsLineParts[4]);

                dataLock.EnterReadLock();
                try
                {
                    if (this.data.ContainsKey(nsLineParts[0]))
                    {
                        entriesCovered.Add(this.data[nsLineParts[0]]);
                    }
                }
                finally { dataLock.ExitReadLock(); }

                // the dictionary does not report the current sender so let's tack it on
                List<int> listOfSenders = new List<int>(GetArrayOf(nsLineParts[5]));
                if (!listOfSenders.Contains(this.remote_uid))
                    listOfSenders.Add(this.remote_uid);

                ResponseAction action = ReadDataStub(nsLineParts[0], fakeEntry.GetMime(), nsLineParts[1] + "." + nsLineParts[2], new List<int>( listOfSenders), out getEntry, out addEntryToSender);

                if (getEntry != null)
                {
                    getEntriesFromSender.Add(getEntry);
                }
                if (addEntryToSender != null)
                {
                    addEntriesToSender.Add(addEntryToSender);
                }

                if (action == ResponseAction.ForwardToAll)
                {
                    listOfSenders.Clear();
                }
                if (action != ResponseAction.DoNotForward)
                {
                    DataEntry get = P2PDictionary.GetEntry( this.data, this.dataLock, nsLineParts[0]);
                    System.Diagnostics.Debug.Assert(get != null);

                    listOfSenders.Add(this.local_uid);
                    SendBroadcastMemory msg = new SendBroadcastMemory(get.key , listOfSenders);
                    WriteMethodPush(get.key, listOfSenders, null, 0, get.GetMime(), get.GetETag(), get.IsEmpty, false, msg.MemBuffer);
                    this.controller.BroadcastToWire(msg);
                }
            }


            // now check to see which dictionary entries that the sender does not have; i'll send my entries to the caller
            this.dataLock.EnterWriteLock();
            try
            {
                foreach (KeyValuePair<string, DataEntry> senderDoesNotHave in this.data.SkipWhile(x => entriesCovered.Contains(x.Value)))
                {
                    DataEntry get = senderDoesNotHave.Value;

                    // i know about something that the sender does not know
                    SendMemoryToPeer mem = new SendMemoryToPeer(get.key, sentFromList);
                    WriteMethodPush(get.key, GetListOfThisLocalID(), null, 0, get.GetMime(), get.GetETag(), get.IsEmpty, false, mem.MemBuffer);
                    addEntriesToSender.Add(mem);
                }
            }
            finally { this.dataLock.ExitWriteLock();}
        }

        /// <summary>
        ///  reads all sorts of data types
        /// </summary>
        /// <param name="contentLocation">location of the data</param>
        /// <param name="eTag">latest version of data being read</param>
        /// <param name="contentType"></param>
        /// <param name="dataOnWire">data read</param>
        /// <returns></returns>
        private ResponseAction ReadData(string contentLocation, string eTag, string contentType, List<int> senders, byte[] dataOnWire)
        {
            ResponseAction success = ResponseAction.DoNotForward;
            ETag tag = ReadETag(eTag);

            // constitute object
            DataEntry create = new DataEntry(contentLocation, tag, senders);
            create.subscribed = this.keysToListen.IsSubscribed(contentLocation);
            create.ReadBytesUsingMime(contentType, dataOnWire);

            bool upgradeable = true;
            DataEntry get = null;

            this.dataLock.EnterUpgradeableReadLock();
            try
            {
                if (this.data.ContainsKey(contentLocation))
                {
                    // update exisitng entry
                    get = this.data[contentLocation];
                }


                if (get == null)
                {
                    this.dataLock.EnterWriteLock();
                    try
                    {
                        // don't save the value if not subscribed
                        if (!create.subscribed)
                        {
                            create.value = DataMissing.Singleton;
                        }

                        this.data.Add(contentLocation, create);
                    }
                    finally
                    {
                        this.dataLock.ExitWriteLock();
                        this.dataLock.ExitUpgradeableReadLock();
                        upgradeable = false;
                    }

                    if (create.subscribed)
                    {
                        // notify API user
                        this.controller.Notified(new NotificationEventArgs(create, contentLocation, NotificationReason.Add, null));
                    }

                    // never seen before, thus tell others
                    success = ResponseAction.ForwardToSuccessor;
                }
            }
            finally
            {
                if (upgradeable)
                    this.dataLock.ExitUpgradeableReadLock();
            }
            
                
            if (get != null)
            {
                object oldValue = null;

                lock (get)
                {
                    if (create.subscribed)
                    {

                        ETagCompare compareResult = ETag.CompareETags(create.GetETag(), get.GetETag());
                        if (compareResult == ETagCompare.FirstIsNewer || compareResult == ETagCompare.Conflict || compareResult == ETagCompare.Same)
                        {
                            oldValue = get.value;

                            if (compareResult == ETagCompare.Conflict)
                            {
                                success = ResponseAction.ForwardToAll;

                                // increment the revision and take ownership
                                create.lastOwnerID = this.local_uid;
                                create.lastOwnerRevision = IncrementRevisionRandomizer(create.lastOwnerRevision);
                            }
                            else if (DataMissing.IsSingleton(oldValue))
                            {
                                success = ResponseAction.ForwardToSuccessor;
                            }
                            else if (compareResult == ETagCompare.Same)
                            {
                                success = ResponseAction.DoNotForward;
                            }
                            else//first is newer
                            {
                                success = ResponseAction.ForwardToSuccessor;
                            }


                            get.lastOwnerID = create.lastOwnerID;
                            get.lastOwnerRevision = create.lastOwnerRevision;
                            get.type = create.type;
                            get.value = create.value;

                            
                        }
                        else // SecondIsNewer
                        {
                            // return this data to the sender
                            success = ResponseAction.ForwardToAll;
                        }

                    }
                    else
                    {
                        ETagCompare compareResult = ETag.CompareETags(create.GetETag(), get.GetETag());

                        if (compareResult == ETagCompare.FirstIsNewer || compareResult == ETagCompare.Conflict || compareResult == ETagCompare.Same)
                        {
                            if (compareResult == ETagCompare.Conflict)
                            {
                                success = ResponseAction.ForwardToAll;
                            }
                            else if (compareResult == ETagCompare.Same)
                            {
                                success = ResponseAction.DoNotForward;
                            }
                            else//first is newer
                            {
                                success = ResponseAction.ForwardToSuccessor;
                            }

                            if (compareResult != ETagCompare.Same)
                            {
                                get.lastOwnerID = create.lastOwnerID;
                                get.lastOwnerRevision = create.lastOwnerRevision;
                                get.type = create.type;
                                get.value = DataMissing.Singleton;
                                get.senderPath = create.senderPath;
                            }

                            System.Diagnostics.Debug.Assert(get.type != Data.ValueType.Removed);
                        }
                        else // second is newer
                        {
                            // return this data to the sender
                            success = ResponseAction.ForwardToAll;
                        }

                    }
                }//lock


                // notify API user
                if (success != ResponseAction.DoNotForward && get.subscribed && !DataMissing.IsSingleton(get.value))
                {
                    get.senderPath = create.senderPath;

                    this.controller.Notified(new NotificationEventArgs(get,contentLocation, NotificationReason.Change, oldValue));
                }

            } // else if
            

            return success;
        }

        /// <summary>
        /// Reads data using only header information. Can be used by ReadDictionary
        /// so it handles deleted content too.
        /// </summary>
        /// <param name="contentLocation">Location of the data item without /proxy</param>
        /// <param name="contentType">GetMime()</param>
        /// <param name="eTag">Version number</param>
        /// <param name="addEntryToSender">These are entries that the sender does not know about</param>
        /// <param name="getEntryFromSender">This function fills in a list of entries that need to be requested from the sender</param>
        private ResponseAction ReadDataStub(string contentLocation, string contentType, string eTag, 
            List<int> sentFromList, out DataHeader getEntryFromSender, out SendMemoryToPeer addEntryToSender)
        {
            ResponseAction success = ResponseAction.DoNotForward;
            ETag tag = ReadETag(eTag);

            DataEntry get = null;
            getEntryFromSender = null;
            addEntryToSender = null;

            // create a stub of the item
            DataEntry create = new DataEntry(contentLocation, tag, sentFromList);
            create.subscribed = keysToListen.IsSubscribed(contentLocation);
            create.SetMime(contentType);

            // manually erase the value (TODO, don't erase the value)
            // always default to singleton because we assume that a GET is required to complete the request
            if (create.type != Data.ValueType.Removed)
                create.value = DataMissing.Singleton;

            this.dataLock.EnterUpgradeableReadLock();
            try
            {
                if (this.data.ContainsKey(contentLocation))
                {
                    // update the version number of the stub
                    get = this.data[contentLocation];
                }
                else
                {
                    this.dataLock.EnterWriteLock();
                    try
                    {
                        this.data.Add(contentLocation, create);
                    }
                    finally { this.dataLock.ExitWriteLock(); }

                    if (create.subscribed && DataMissing.IsSingleton(create.value))
                    {
                        // we'll have to wait for the data to arrive on the wire
                        // actually get the data
                        getEntryFromSender = new DataHeader(contentLocation, tag, sentFromList);
                        success = ResponseAction.DoNotForward;
                    }
                    else
                    {
                        success = ResponseAction.ForwardToSuccessor;
                    }

                }
            }

            finally
            {
                this.dataLock.ExitUpgradeableReadLock();
            }

            if (get != null)
            {
                lock (get)
                {
                    if (create.subscribed)
                    {
                        ETagCompare compareResult = ETag.CompareETags(tag, get.GetETag());

                        if (compareResult == ETagCompare.FirstIsNewer || 
                            compareResult == ETagCompare.Same || compareResult == ETagCompare.Conflict)
                        {
                            getEntryFromSender = new DataHeader(create.key, create.GetETag(), sentFromList);
                            success = ResponseAction.DoNotForward;
                        }
                        else //if (compareResult == ETagCompare.SecondIsNewer )
                        {
                            // i know about something newer than the sender, tell the sender
                            //SendMemoryToPeer mem = new SendMemoryToPeer(get.key,sentFromList);
                            //ResponseHeadStub(HEAD, get.key, GetListOfThisLocalID(), 0, get.GetMime(), get.GetETag(), get.IsEmpty, mem.MemBuffer, false);
                            //addEntryToSender = mem;

                            // well, predecessor already been handled above, so we only need to tell
                            // the others
                            success = ResponseAction.ForwardToAll;
                        }
                    }
                    else
                    {
                        // not subscribed
                        // just record the fact that there is newer data on the wire; cannot resolve conflicts without being a subscriber
                        ETagCompare compareResult = ETag.CompareETags(create.GetETag(), get.GetETag());
                        if (compareResult == ETagCompare.FirstIsNewer || compareResult == ETagCompare.Same || compareResult == ETagCompare.Conflict)
                        {
                            get.lastOwnerID = create.lastOwnerID;
                            get.lastOwnerRevision = create.lastOwnerRevision;
                            get.value = DataMissing.Singleton;

                            if (compareResult != ETagCompare.Same)
                            {
                                get.senderPath = create.senderPath;

                                success = ResponseAction.ForwardToSuccessor;
                            }
                            else
                                success = ResponseAction.DoNotForward;
                        }
                        else // if (compareResult == ETagCompare.SecondIsNewer )
                        {
                            //// i know about something newer than the sender
                            //SendMemoryToPeer mem = new SendMemoryToPeer(get.key,sentFromList);
                            //ResponseHeadStub(HEAD, get.key, GetListOfThisLocalID(), 0, get.GetMime(), get.GetETag(), get.IsEmpty, mem.MemBuffer, false);
                            //addEntryToSender = mem;

                            // tell the others too (already told predecessor above)
                            success = ResponseAction.ForwardToAll;
                        }
                    }
                }
            }

            return success;
        }

        private List<int> GetListOfThisLocalID()
        {
            return new List<int>(1) { this.local_uid };
        }

        private ResponseAction ReadDelete(string contentLocation, string eTag, List<int> senderPath)
        {
            ResponseAction success = ResponseAction.DoNotForward;
            ETag tag = ReadETag(eTag);

            bool upgradeable = true;
            this.dataLock.EnterUpgradeableReadLock();
            try
            {
                if (this.data.ContainsKey(contentLocation))
                {
                    DataEntry get = this.data[contentLocation];
                    object oldValue = null;

                    // exit lock
                    this.dataLock.ExitUpgradeableReadLock();
                    upgradeable = false;

                    lock (get)
                    {
                        ETagCompare compareResult = ETag.CompareETags(tag, get.GetETag());
                        if (compareResult == ETagCompare.FirstIsNewer || compareResult == ETagCompare.Conflict
                            || compareResult == ETagCompare.Same)
                        {
                            oldValue = get.value;

                            if (compareResult == ETagCompare.Conflict)
                            {
                                success = ResponseAction.ForwardToAll;

                                tag.UID = this.local_uid;
                                tag.Revision = IncrementRevisionRandomizer(tag.Revision);
                            }
                            else if (DataMissing.IsSingleton(oldValue))
                            {
                                success = ResponseAction.ForwardToSuccessor;
                            }
                            else if (compareResult == ETagCompare.Same)
                            {
                                success = ResponseAction.DoNotForward;
                            }
                            else//first is newer
                            {
                                success = ResponseAction.ForwardToSuccessor;
                            }

                            get.lastOwnerID = tag.UID;
                            get.lastOwnerRevision = tag.Revision;
                            get.Delete();

                            if (compareResult != ETagCompare.Same)
                            {
                                get.senderPath = senderPath;
                            }

                            if (!get.subscribed)
                            {
                                get.value = DataMissing.Singleton;
                            }
                        }
                        else//if (compareResult == ETagCompare.SecondIsNewer)
                        {
                            // return to sender
                            success = ResponseAction.ForwardToAll;

                        }
                    } // end lock

                    // notify to subscribers
                    if (success != ResponseAction.DoNotForward && get.subscribed)
                        this.controller.Notified(new NotificationEventArgs(get, "", NotificationReason.Remove, oldValue));

                }
                else
                {

                    // create a stub of the item
                    DataEntry create = new DataEntry(contentLocation, tag, senderPath);
                    create.Delete();
                    create.subscribed = keysToListen.IsSubscribed(contentLocation);
                    if (!create.subscribed)
                    {
                        create.value = DataMissing.Singleton;
                    }

                    dataLock.EnterWriteLock();
                    try
                    {
                        this.data.Add(contentLocation, create);
                    }
                    finally
                    {
                        this.dataLock.ExitWriteLock();
                    }

                    if (create.subscribed)
                    {
                        // notify for subscribers
                        this.controller.Notified(new NotificationEventArgs(create, "", NotificationReason.Remove, null));
                    }

                    success = ResponseAction.ForwardToSuccessor;
                }
            }
            finally
            {
                if (upgradeable)
                    this.dataLock.ExitUpgradeableReadLock();
            }

            return success;
        }


        // reads "32921.42198" and converts to two numbers
        private static ETag ReadETag(string eTag)
        {
            return new ETag(eTag);
        }

        // web browser handlers
        private string MSG_ANY = Properties.Resources.error; //"<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01 Transitional//EN\"><html><head><title>{0} {1}</title></head><body>{0} {1}</body></html>";

        private static string GetErrorMessage(int errorNum)
        {
            switch (errorNum)
            {
                case 200: 
                    return "OK";
                case 305:
                    return "Use Proxy";
                case 404:
                    return "Not Found";
                case 405:
                    return "Method Not Allowed";
                default:
                    return "Unknown";
            }
        }

        private void WriteErrorNotFound(StreamWriter writer, string verb, string contentLocation, int errorNumber)
        {
            string payload = String.Format(MSG_ANY, errorNumber, GetErrorMessage(errorNumber));

            writer.WriteLine("HTTP/1.1 {0} {1}", errorNumber, GetErrorMessage(errorNumber));
            writer.WriteLine("Content-Type: text/html");
            writer.WriteLine("Content-Location: " + contentLocation);
            writer.WriteLine("Content-Length: " + payload.Length);
            writer.WriteLine("Response-To: " + verb);
            writer.WriteLine(SPECIAL_HEADER + ": " + this.local_uid);
            writer.WriteLine("P2P-Sender-List: " + GetStringOf(GetListOfThisLocalID()));
            writer.WriteLine();
            writer.Write(payload);
            writer.Flush();
        }

        private void WriteErrorNotFound(StreamWriter writer, string verb, string contentLocation, int errorNumber, List<int> senderList)
        {
            string payload = String.Format(MSG_ANY, errorNumber, GetErrorMessage(errorNumber));

            writer.WriteLine("HTTP/1.1 {0} {1}", errorNumber, GetErrorMessage(errorNumber));
            writer.WriteLine("Content-Type: text/html");
            writer.WriteLine("Content-Location: " + contentLocation);
            writer.WriteLine("Content-Length: " + payload.Length);
            writer.WriteLine("P2P-Sender-List: " + GetStringOf(senderList));
            writer.WriteLine("Response-To: " + verb);
            writer.WriteLine(SPECIAL_HEADER + ": " + this.local_uid);
            writer.WriteLine();
            writer.Write(payload);
            writer.Flush();
        }


        private void ResponseCode(MemoryStream stream, string contentLocation, List<int> senderList, int dataOwner, int dataRevision, int code)
        {
            string payload = String.Format(MSG_ANY, code, GetErrorMessage(code));

            StreamWriter writer = new StreamWriter(stream, Encoding.ASCII);
            writer.WriteLine("HTTP/1.1 " + code + " " + GetErrorMessage(code));

            writer.WriteLine("Content-Type: text/html");
            writer.WriteLine("Content-Location: " + contentLocation);
            writer.WriteLine("Content-Length: " + payload.Length);
            writer.WriteLine(SPECIAL_HEADER + ": " + this.local_uid);
            writer.WriteLine("P2P-Sender-List: " + GetStringOf(senderList));
            writer.WriteLine("ETag: \"" + dataOwner + "." + dataRevision + "\"");
            writer.WriteLine("Response-To: GET");
            writer.WriteLine();
            writer.Write(payload);
            writer.Flush();
        }

        private void ResponseFollowProxy(MemoryStream stream, string contentLocation, List<int> senderList)
        {
            const int code = 307;
            string payload = String.Format(MSG_ANY, code, GetErrorMessage(code));

            StreamWriter writer = new StreamWriter(stream, Encoding.ASCII);
            writer.WriteLine("HTTP/1.1 " + code + " " + GetErrorMessage(code));

            writer.WriteLine("Content-Type: text/html");
            writer.WriteLine("Content-Location: " + contentLocation);
            writer.WriteLine("Content-Length: " + payload.Length);
            writer.WriteLine(SPECIAL_HEADER + ": " + this.local_uid);
            writer.WriteLine("P2P-Sender-List: " + GetStringOf(senderList));
            writer.WriteLine("ETag: \"" + 0 + "." + 0 + "\"");
            writer.WriteLine("Response-To: GET");
            writer.WriteLine();
            writer.Write(payload);
            writer.Flush();
        }




        private void WriteResponseDeleted(MemoryStream stream, string contentLocation, List<int> senderList, List<int> proxyResponsePath, int dataOwner, int dataRevision)
        {
            string payload = String.Format(MSG_ANY, 404, GetErrorMessage(404));

            StreamWriter writer = new StreamWriter(stream, Encoding.ASCII);
            writer.WriteLine("HTTP/1.1 404 Not Found");
            writer.WriteLine("Content-Type: text/html");
            writer.WriteLine("Content-Location: " + contentLocation);
            writer.WriteLine("Content-Length: " + payload.Length);
            writer.WriteLine(SPECIAL_HEADER + ": " + this.local_uid);
            writer.WriteLine("P2P-Sender-List: " + GetStringOf(senderList));
            if (proxyResponsePath != null)
            {
                writer.WriteLine("P2P-Response-Path: " + GetStringOf(proxyResponsePath));
            }
            writer.WriteLine("ETag: \"" + dataOwner + "." + dataRevision + "\"");
            writer.WriteLine("Response-To: GET");
            writer.WriteLine();
            writer.Write(payload);
            writer.Flush();
        }

        /// <summary>
        /// REST request method to delete a resource
        /// </summary>
        /// <param name="stream">writer stream</param>
        /// <param name="contentLocation">resource</param>
        /// <param name="senderList"></param>
        /// <param name="proxyResponsePath"></param>
        /// <param name="dataOwner">resource version</param>
        /// <param name="dataRevision">resource version</param>
        private void WriteMethodDeleted(MemoryStream stream, string contentLocation, List<int> senderList, List<int> proxyResponsePath, int dataOwner, int dataRevision)
        {
            StreamWriter writer = new StreamWriter(stream, Encoding.ASCII);
            writer.WriteLine("{0} {1} HTTP/1.1" , DELETE, contentLocation);
            writer.WriteLine(SPECIAL_HEADER + ": " + this.local_uid);
            writer.WriteLine("P2P-Sender-List: " + GetStringOf(senderList));
            if (proxyResponsePath != null)
            {
                writer.WriteLine("P2P-Response-Path: " + GetStringOf(proxyResponsePath));
            }
            writer.WriteLine("ETag: \"" + dataOwner + "." + dataRevision + "\"");
            writer.WriteLine();
            writer.Flush();
        }

        private void WriteError405(MemoryStream stream)
        {
            StreamWriter writer = new StreamWriter(stream, Encoding.ASCII);
            writer.NewLine = NEWLINE;

            string payload = String.Format(MSG_ANY, 405, GetErrorMessage(405));
            writer.WriteLine("HTTP/1.1 405 Method Not Allowed");
            writer.WriteLine("Allow: GET, HEAD");
            writer.WriteLine(SPECIAL_HEADER + ": " + this.local_uid);
            writer.WriteLine("Content-Length: " + payload.Length);
            writer.WriteLine("Response-To: GET");
            writer.WriteLine("P2P-Sender-List: " + GetStringOf(GetListOfThisLocalID()));
            writer.WriteLine();
            writer.Write(payload);
            writer.WriteLine();
            writer.Flush();
        }

        private static IEnumerable<int> GetArrayOf(string integerList)
        {
            if (integerList.Length == 0)
                return new List<int>(0);
            
            string[] strSenders = integerList.Split(',');
            return strSenders.Select(x => int.Parse(x));
        }


        // converts a list of numbers into
        // 1,2,3
        private static string GetStringOf(List<int> senderList)
        {
            if (senderList.Count == 0)
                return "";

            StringBuilder str = new StringBuilder();
            foreach (int i in senderList)
            {
                str.Append(i);
                str.Append(",");
            }

            str.Remove(str.Length - 1, 1);
            return str.ToString();
        }

        private void WriteMethodHeader(StreamWriter writer, string contentLocation, string contentType, int contentSize, 
            int dataOwner, int dataRevision, List<int> senderList, List<int> responsePath, bool willClose)
        {
            writer.WriteLine("{0} {1} HTTP/1.1", PUSH, contentLocation);
            writer.WriteLine(SPECIAL_HEADER + ": " + this.local_uid);
            writer.WriteLine("ETag: \"" + dataOwner + "." + dataRevision + "\"");
            writer.WriteLine("P2P-Sender-List: " + GetStringOf(senderList));
            if (responsePath != null)
            {
                writer.WriteLine("P2P-Response-Path: " + GetStringOf(responsePath));
            }
            writer.WriteLine("Content-Type: " + contentType);
            writer.WriteLine("Content-Length: " + contentSize.ToString());
           
            if (willClose)
            {
                writer.WriteLine("Connection: close");
            }

            writer.WriteLine();
            writer.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="contentLocation"></param>
        /// <param name="contentType"></param>
        /// <param name="contentSize"></param>
        /// <param name="dataOwner"></param>
        /// <param name="dataRevision"></param>
        /// <param name="senderList"></param>
        /// <param name="responsePath">Can be null.</param>
        /// <param name="responseToVerb"></param>
        /// <param name="willClose"></param>
        private void WriteResponseHeader(StreamWriter writer, string contentLocation, string contentType, int contentSize, 
            int dataOwner, int dataRevision, List<int> senderList, List<int> responsePath, string responseToVerb, bool willClose)
        {
            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine(SPECIAL_HEADER + ": " + this.local_uid);
            writer.WriteLine("ETag: \"" + dataOwner + "." + dataRevision + "\"");
            writer.WriteLine("Content-Location: " + contentLocation);
            writer.WriteLine("P2P-Sender-List: " + GetStringOf(senderList));
            if (responsePath!=null)
            {
                writer.WriteLine("P2P-Response-Path: " + GetStringOf(responsePath));
            }
            writer.WriteLine("Content-Type: " + contentType);
            writer.WriteLine("Content-Length: " + contentSize.ToString());
            if (responseToVerb.Length > 0)
            {
                writer.WriteLine("Response-To: " + responseToVerb);
            }
            if (willClose)
            {
                writer.WriteLine("Connection: close");
            }

            writer.WriteLine();
            writer.Flush();
        }

        private void WriteSimpleGetRequest(MemoryStream stream, DataHeader request)
        {
            StreamWriter writer = new StreamWriter(stream, Encoding.ASCII);
            writer.NewLine = NEWLINE;

            writer.WriteLine("{0} {1} HTTP/1.1" , GET, request.key);
            writer.WriteLine("P2P-Sender-List: " + GetStringOf(request.sentFrom));
            writer.WriteLine(SPECIAL_HEADER + ": " + this.local_uid);
            writer.WriteLine();
            writer.Flush();
        }

        

    }
}
