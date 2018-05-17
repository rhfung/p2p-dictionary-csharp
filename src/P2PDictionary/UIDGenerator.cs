using System;

namespace com.rhfung.P2PDictionary
{
    class UIDGenerator
    {
        static int used_random_bits = 0;

        static UIDGenerator()
        {
            // to better randomize the seed, include all MAC addresses on the local interfaces
            System.Net.NetworkInformation.NetworkInterface[] ifaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (System.Net.NetworkInformation.NetworkInterface face in ifaces)
            {
                used_random_bits += face.GetPhysicalAddress().GetHashCode();
            }
            
        }

        public static int GetNextInteger()
        {
            Random r = new Random((int)(DateTime.Now.Ticks + used_random_bits )) ;
            int rnd = r.Next();
            used_random_bits += rnd;
            return rnd;
        }

        public static int GetNextInteger(int max_value)
        {
            Random r = new Random((int)(DateTime.Now.Ticks + used_random_bits));
            int rnd = r.Next(max_value);
            used_random_bits += rnd;
            return rnd;
        }
    }
}
