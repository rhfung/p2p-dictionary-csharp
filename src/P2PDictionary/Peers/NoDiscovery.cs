using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace com.rhfung.P2PDictionary.Peers
{
    public class NoDiscovery : IPeerInterface
    {
        public ReadOnlyDictionary<int, List<EndpointInfo>> DiscoveredPeers {
            get
            {
                return new ReadOnlyDictionary<int, List<EndpointInfo>>(
                    new Dictionary<int, List<EndpointInfo>>());
            }
        }

        public void BrowseServices()
        {
            // no op
        }

        public void Dispose()
        {
            // no op
        }

        public void RegisterServer(P2PDictionary dict)
        {
            // no op
        }

        public void UnregisterServer()
        {
            // no op
        }
    }
}
