using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;


namespace com.rhfung.P2PDictionary
{
    abstract class SendMemory
    {
        public string ContentLocation;

        /// <summary>
        /// List of peers
        /// </summary>
        public List<int> PeerList;

        public MemoryStream MemBuffer;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contentLoc">A unique key provided to the send packet</param>
        /// <param name="senderList">These are the senders that have already seen the "data".</param>
        public SendMemory(string contentLoc, List<int> senderList)
        {
            this.ContentLocation = contentLoc;
            this.PeerList = senderList;
            this.MemBuffer = new MemoryStream();
        }

        //public DataSendMessage(string key, ETag version, List<int> senderList)
        //{
        //    this.Key = key;
        //    this.Version = version;
        //    this.Senders = senderList;
        //    this.MemBuffer = new MemoryStream();
        //}
    }


    /// <summary>
    /// PeerList are the peers to avoid
    /// </summary>
    class SendBroadcastMemory : SendMemory
    {
        public SendBroadcastMemory(string contentLoc, List<int> blockList)
            : base(contentLoc, blockList)
        {
        }
    }

    /// <summary>
    /// PeerList are the peers to follow
    /// </summary>
    class SendMemoryToPeer : SendMemory
    {
        public SendMemoryToPeer(string contentLoc, List<int> includeList)
            : base(contentLoc, includeList)
        {
        }
    }
}
