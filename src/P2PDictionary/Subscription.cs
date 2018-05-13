using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace com.rhfung.P2PDictionary
{
    class Subscription : IEnumerable<string>
    {
        private List<string> subscriptions;
        private ISubscriptionChanged notifier;

        public Subscription(ISubscriptionChanged notifier)
        {
            this.subscriptions = new List<string>();
            this.notifier = notifier;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="wildcardString">Case-sensitive string that includes *, ?, and [] for ranges of characters to match.</param>
        public void AddSubscription(string wildcardString, SubscriptionInitiator initiator)
        {
            lock (subscriptions)
            {
                subscriptions.Add(wildcardString);
            }
            notifier.AddedSubscription(this, wildcardString, initiator);
        }

        public void RemoveSubscription(string wildcardString)
        {
            lock (subscriptions)
            {
                subscriptions.Remove(wildcardString);
            }
            notifier.RemovedSubscription(this, wildcardString);
        }

        private static bool LikeString(string key, string subscription)
        {
            string regexSubscription = subscription.Replace("/", "\\/"); // safeguard paths
            regexSubscription = regexSubscription.Replace(".", "\\.");  // remove dot operator
            regexSubscription = regexSubscription.Replace("*", ".*");   // wildcard search
            regexSubscription = regexSubscription.Replace("?", ".");    // single character
            regexSubscription = regexSubscription.Replace("#", "\\d");  // digit
            regexSubscription = "^" + regexSubscription + "$";
            var exp = new Regex(regexSubscription);
            return exp.Match(key).Success;
        }


        public bool IsSubscribed(string key)
        {
            lock (subscriptions)
            {
                return subscriptions.Any(subscription => LikeString(key, subscription));
            }
        }


        IEnumerator<string> IEnumerable<string>.GetEnumerator()
        {
            return subscriptions.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return subscriptions.GetEnumerator();
        }
    }

    interface ISubscriptionChanged
    {
        void AddedSubscription(Subscription s, string wildcardString, SubscriptionInitiator initiator);
        void RemovedSubscription(Subscription s, string wildcardString);
    }
}
