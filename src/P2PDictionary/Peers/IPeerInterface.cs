using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace com.rhfung.P2PDictionary.Peers
{
    public interface IPeerInterface : IDisposable
    {
        /**
         * Register the given dictionary to the service.
         * @param dict
         */
        void RegisterServer(P2PDictionary dict);

        /**
         * Remove the dictionary from the service.
         */
        void UnregisterServer();

        /**
         * Start looking for neighbouring nodes.
         */
        void BrowseServices();

        /**
         * Return list of discovered nodes. 
         */
        ReadOnlyDictionary<int, List<EndpointInfo>> DiscoveredPeers
        {
            get;
        }
    }
}
