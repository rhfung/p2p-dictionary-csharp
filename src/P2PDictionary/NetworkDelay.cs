using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.rhfung.P2PDictionary
{
    static class NetworkDelay
    {

        public  const int LENGTH_NEWLINE = 2;

#if SIMULATION

        /// <summary>
        /// 
        /// </summary>
        /// <param name="transmissionLowerBound">in ms</param>
        /// <param name="transmissionUpperBound">in ms</param>
        /// <param name="bandwidth">bandwidth in Bytes / second</param>
        /// <param name="packetSize">packet size in bytes</param>
        /// <returns>value in milliseconds</returns>
        public static int GetLatency(int transmissionLowerBound, int transmissionUpperBound, double bandwidth, long packetSize)
        {
            int transmission = 0;
            if (transmissionUpperBound != transmissionLowerBound)
                transmission = UIDGenerator.GetNextInteger(transmissionUpperBound - transmissionLowerBound) + transmissionLowerBound;

            int bandwidthDelay = (int) Math.Round((packetSize / bandwidth) * 1000);

            return transmission + bandwidthDelay;
        }

#endif

        /// <summary>
        /// Count size of header in bytes
        /// </summary>
        /// <param name="headers"></param>
        /// <returns></returns>
        public static int CountHeaders(Dictionary<string, string> headers)
        {
            int h = 0;
            foreach (var entry in headers)
            {
                h += entry.Key.Length;
                h += entry.Value.Length;
                h += 4; // space, colon, new line character
            }

            return h;
        }
    }
}
