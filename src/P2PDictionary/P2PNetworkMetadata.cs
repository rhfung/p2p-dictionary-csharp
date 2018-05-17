using System.Collections.Generic;

namespace com.rhfung.P2PDictionary
{
    /// <summary>
    /// This class provides information regarding the P2P network topology.
    /// Every call is guaranteed thread-safe, i.e., may block on another thread.
    /// </summary>
    public class P2PNetworkMetadata
    {
        private P2PDictionary m_dict;

        public P2PNetworkMetadata(P2PDictionary dictionary)
        {
            m_dict = dictionary;
        }

        public P2PDictionary Dictionary
        {
            get { return m_dict; }
        }

        /// <summary>
        /// Returns a count of the number of connected peers
        /// </summary>
        public int RemotePeersCount
        {
            get
            {
                lock (m_dict.Connections)
                {
                    return m_dict.Connections.Count;
                }
            }

        }

        /// <summary>
        /// Returns a list of the IP addresses of the connected peers.
        /// Not guaranteed to match RemotePeersCount.
        /// </summary>
        /// <returns></returns>
        public List<System.Net.IPEndPoint> GetRemotePeerEndpoints()
        {
            List<System.Net.IPEndPoint> endPoints = new List<System.Net.IPEndPoint>();
            lock(m_dict.Connections)
            {
                foreach(DataConnection conn in m_dict.Connections)
                {
                    if (conn.RemoteEndPoint is System.Net.IPEndPoint)
                    {
                        endPoints.Add((System.Net.IPEndPoint)conn.RemoteEndPoint);
                    }
                }
            }

            return endPoints;
        }

        /// <summary>
        /// Returns a list of UIDs of each remotely connected dictionary.
        /// </summary>
        /// <returns></returns>
        public List<int> GetRemotePeerUID()
        {
            List<int> endPoints = new List<int>();
            lock (m_dict.Connections)
            {
                foreach (DataConnection conn in m_dict.Connections)
                {
                  endPoints.Add(conn.RemoteUID);
                }
            }

            return endPoints;
        }
    }
}
