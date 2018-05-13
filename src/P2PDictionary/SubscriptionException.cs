using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.rhfung.P2PDictionary
{
    [Serializable]
    public class SubscriptionException : Exception
    {
        public SubscriptionException(string message):base(message)
        {
            
        }
    }
}
