using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.rhfung.P2PDictionary
{
    class DataMissing
    {
        public static DataMissing Singleton = new DataMissing();

        /// <summary>
        /// Tests to see if the object is Singleton
        /// </summary>
        /// <param name="test"></param>
        /// <returns></returns>
        public static bool IsSingleton(object test)
        {
            if (test == null)
            {
                return false;
            }
            else if (Singleton.Equals(test))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
