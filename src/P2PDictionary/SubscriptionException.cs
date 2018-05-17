using System;

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
