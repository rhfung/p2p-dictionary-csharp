using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.rhfung.P2PDictionary
{
    interface IMessageController
    {
        /// <summary>
        /// User-friendly description of the working dictionary for web access
        /// </summary>
        string Description
        {
            get;
        }

        /// <summary>
        /// Item2: UID
        /// Item3: false - server, true - client
        /// </summary>
        List<Tuple<System.Net.IPEndPoint, int, ConnectionType>> ActiveEndPoints
        {
            get;
        }

        List<EndpointInfo> AllEndPoints
        {
            get;
        }

        /// <summary>
        /// Broadcasts a message to all peers.
        /// </summary>
        /// <param name="message">Message is sent to every peer except those listed in Senders.</param>
        int BroadcastToWire(SendBroadcastMemory message);

        /// <summary>
        /// Requests data from a specific sender(s).
        /// </summary>
        /// <param name="header"></param>
        int PullFromPeer(DataHeader header);

        /// <summary>
        /// Requests data from a specific sender(s).
        /// </summary>
        /// <param name="header"></param>
        int PullFromPeer(IEnumerable<DataHeader> header);

        /// <summary>
        /// Sends a message to a specific sender(s).
        /// </summary>
        /// <param name="message">senders list is used to target a peer</param>
        int SendToPeer(SendMemoryToPeer message);

        /// <summary>
        /// Sends a message to a specific sender(s).
        /// </summary>
        /// <param name="message">senders list is used to target a peer</param>
        int SendToPeer(IEnumerable<SendMemoryToPeer> message);

        
        bool IsConnected(int uniqueID);

        // wire through events

        void Connected(DataConnection conn);
        void Disconnected(DataConnection conn);
        void Notified(NotificationEventArgs args);
        void SubscriptionChanged(SubscriptionEventArgs args);
    }
}

