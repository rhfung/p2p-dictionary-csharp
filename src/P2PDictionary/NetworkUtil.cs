using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;

namespace com.rhfung.P2PDictionary
{
    static class NetworkUtil
    {
        public static List<int> GetUsedServerPorts()
        {
            
            // Evaluate current system tcp connections. This is the same information provided
            // by the netstat command line application, just in .Net strongly-typed object
            // form.  We will look through the list, and if our port we would like to use
            // in our TcpClient is occupied, we will set isAvailable to false.
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();
            List<int> ports = new List<int>(tcpConnInfoArray.Length);

            foreach (IPEndPoint endpoint in tcpConnInfoArray)
            {
                // add port number once
                if (!ports.Contains(endpoint.Port))
                {
                    ports.Add(endpoint.Port);
                }
            }


            return ports;
        }

        /// <summary>
        /// Finds a free port, tries 50 times.
        /// Throws ApplicationException if cannot find a free port.
        /// </summary>
        /// <param name="startingPort"></param>
        /// <returns></returns>
        public static int GetFreePort(int startingPort)
        {
            int tries = 0;

            List<int> usedPorts = GetUsedServerPorts();
            usedPorts.Sort();
            while (usedPorts.Contains(startingPort))
            {
                startingPort++;
                tries++;

                if (tries > 50)
                    throw new ApplicationException("no free port found");
            }

            return startingPort;
        }
    }
}
