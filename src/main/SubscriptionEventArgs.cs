using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.rhfung.P2PDictionary
{
    public class SubscriptionEventArgs:EventArgs
    {
        public SubscriptionEventReason Reason;
        public SubscriptionInitiator Initiator;
        public string SubscripitonPattern;
    }

    public enum SubscriptionEventReason
    {
        Add,
        Change,
        Remove
    }

    public enum SubscriptionInitiator
    {
        /// <summary>
        /// Key was added by assignment in the dictionary
        /// </summary>
        AutoAddKey,

        /// <summary>
        /// Key was added by proxy path between peers
        /// </summary>
        AutoProxyKey,

        /// <summary>
        /// Key was added by the user
        /// </summary>
        Manual
    }
}
