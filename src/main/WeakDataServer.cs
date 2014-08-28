using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.rhfung.P2PDictionary
{
    // http://blogs.msdn.com/b/greg_schechter/archive/2004/05/27/143605.aspx
    class WeakDataServer : WeakReference, IMessageController
    {
        public WeakDataServer(P2PDictionary target):base(target)
        { }
  
        IMessageController CastTargetController
        {
            get
            {
                return this.Target as IMessageController;
            }
        }

        string IMessageController.Description
        {
            get 
            {
                if (this.IsAlive)
                    return CastTargetController.Description;
                else
                    return "";
            }
        }

        List<Tuple<System.Net.IPEndPoint, int, ConnectionType>> IMessageController.ActiveEndPoints
        {
            get
            {
                if (this.IsAlive)
                    return CastTargetController.ActiveEndPoints;
                else
                    return new List<Tuple<System.Net.IPEndPoint, int, ConnectionType>>();

            }
        }

        List<EndpointInfo> IMessageController.AllEndPoints
        {
            get {
                if (this.IsAlive)
                    return CastTargetController.AllEndPoints;
                else
                    return new List<EndpointInfo>();
            }
        }

        int IMessageController.BroadcastToWire(SendBroadcastMemory message)
        {
            if (this.IsAlive)
                return CastTargetController.BroadcastToWire(message);
            else
                return 0;
        }

        int IMessageController.PullFromPeer(DataHeader header)
        {
            if (this.IsAlive)
                return CastTargetController.PullFromPeer(header);
            else
                return 0;
        }

        int IMessageController.PullFromPeer(IEnumerable<DataHeader> header)
        {
            if (this.IsAlive)
                return CastTargetController.PullFromPeer(header);
            else
                return 0;
        }

        int IMessageController.SendToPeer(SendMemoryToPeer message)
        {
            if (this.IsAlive)
                return CastTargetController.SendToPeer(message);
            else
                return 0;
        }

        int IMessageController.SendToPeer(IEnumerable<SendMemoryToPeer> message)
        {
            if (this.IsAlive)

                return CastTargetController.SendToPeer(message);
            else
                return 0;
        }

        bool IMessageController.IsConnected(int uniqueID)
        {
            if (this.IsAlive)
                return CastTargetController.IsConnected(uniqueID);
            else
                return false;
        }

        void IMessageController.Connected(DataConnection conn)
        {
            if (this.IsAlive)
                CastTargetController.Connected(conn);
        }

        void IMessageController.Disconnected(DataConnection conn)
        {
            if (this.IsAlive)
            CastTargetController.Disconnected(conn);
        }

        void IMessageController.Notified(NotificationEventArgs args)
        {
            if (this.IsAlive)
                CastTargetController.Notified(args);
        }

        void IMessageController.SubscriptionChanged(SubscriptionEventArgs args)
        {
            if (this.IsAlive)
                CastTargetController.SubscriptionChanged(args);
        }
    }
}
