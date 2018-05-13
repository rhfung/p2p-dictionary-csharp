using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Zeroconf;
using System.Threading;

namespace com.rhfung.P2PDictionary
{
    // Apple Bonjour's peer discovery only works as a singleton
    class PeerDiscovery: IDisposable
    {
        private const string ZEROCONF_NAME = "_com-rhfung-peer._tcp";

        private static ServiceBrowser browser;
        private RegisterService service;

        // idea: only record all the discovered peers
        // let PeerDictionary choose the network connections

        // must lock for use
        public static Dictionary<int, List<EndpointInfo>> DiscoveredPeers;

        static PeerDiscovery()
        {
            DiscoveredPeers = new Dictionary<int, List<EndpointInfo>>(10);
        }

        public PeerDiscovery()
        {
            
        }

        public void BrowseServices()
        {
            if (browser == null)
            {
                browser = new ServiceBrowser();
                browser.ServiceAdded += new ServiceBrowseEventHandler(browser_ServiceAdded);
                browser.ServiceRemoved += new ServiceBrowseEventHandler(browser_ServiceRemoved);
                browser.Browse(0, AddressProtocol.Any, ZEROCONF_NAME, "local");
            }
        }

        public void RegisterServer(P2PDictionary dict)
        {
            if (service != null)
                throw new NotImplementedException();

            service = new RegisterService();
            service.Name = "com.rhfung.P2PDictionary " + dict.Description;
            service.RegType = ZEROCONF_NAME;
            service.ReplyDomain = "local";
            service.Port = (short)((System.Net.IPEndPoint)dict.LocalEndPoint).Port;
            service.TxtRecord = new TxtRecord();
            service.TxtRecord.Add("uid", dict.LocalID.ToString());
            service.Response += new RegisterServiceEventHandler(service_Response);

            service.Register();
        }

        void service_Response(object o, RegisterServiceEventArgs args)
        {
            
        }


        /// <summary>
        /// Disabled due to crashes
        /// </summary>
        public void UnregisterServer()
        {
            //if (service != null)
            //{
            //    service.Dispose();
            //}
            //service = null;
        }

        /// <summary>
        /// Disabled due to crashes
        /// </summary>
        public static void StopBrowsing()
        {
            //if (browser != null)
            //{
            //    browser.Dispose();
            //}
            //browser = null;
        }


        static void browser_ServiceRemoved(object o, ServiceBrowseEventArgs args)
        {
            if (browser != null)
            {
                args.Service.Resolved += new ServiceResolvedEventHandler(ServiceRemoved_Resolved);
                args.Service.Resolve();
            }
        }

        static void browser_ServiceAdded(object o, ServiceBrowseEventArgs args)
        {
            if (browser != null)
            {
                args.Service.Resolved += new ServiceResolvedEventHandler(Service_Resolved);
                args.Service.Resolve();
            }
        }

        static void Service_Resolved(object o, ServiceResolvedEventArgs args)
        {
            if (browser != null)
            {
                System.Net.IPAddress[] addresses = args.Service.HostEntry.AddressList;
                string strUid = args.Service.TxtRecord["uid"].ValueString;
                int uid = int.Parse(strUid);
                List<EndpointInfo> ei = null;

                if (DiscoveredPeers.ContainsKey(uid))
                {
                    ei = DiscoveredPeers[uid];
                }
                else
                {
                    ei = new List<EndpointInfo>(10);
                    lock (DiscoveredPeers)
                    {
                        DiscoveredPeers.Add(uid, ei);
                    }
                }

                lock (ei)
                {
                    foreach (System.Net.IPAddress addr in addresses)
                    {
                        ei.Add(new EndpointInfo() { UID = uid, Address = addr, Port = args.Service.Port });
                        //dict.StartClient(addr.ToString(), args.Service.Port);
                    }
                }
            }
        }

        static void ServiceRemoved_Resolved(object o, ServiceResolvedEventArgs args)
        {
            if (browser != null)
            {
                System.Net.IPAddress[] addresses = args.Service.HostEntry.AddressList;
                string strUid = args.Service.TxtRecord["uid"].ValueString;
                int uid = int.Parse(strUid);

                lock (DiscoveredPeers)
                {
                    DiscoveredPeers.Remove(uid);
                }
            }
        }

        public void Dispose()
        {
            if (service != null)
            {
                service.Dispose();
                service = null;
            }
        }
    }


}
