using System;

namespace com.rhfung.P2PDictionary
{
    public class NotificationEventArgs :EventArgs
    {
        internal NotificationEventArgs(DataEntry entry, string userKey, NotificationReason reason, object oldValue)
        {
            this._Entry = entry;
            this._reason = reason;
            this._userKey = userKey;
            this._owner = entry.lastOwnerID;
            this._value = entry.value;
            this._oldValue = oldValue;
        }

        internal DataEntry _Entry;
        private string _userKey;
        private int _owner;
        private NotificationReason _reason;
        private object _value;
        private object _oldValue;

        public string Key
        {
            get
            {
                return _userKey;
            }
        }

        /// <summary>
        /// Null if Reason is Removed.
        /// </summary>
        public object Value
        {
            get
            {
                return this._value;
            }
        }
        
        /// <summary>
        /// Null if the entry was deleted before a value was acquired by a peer.
        /// </summary>
        public object PreviousValue
        {
            get
            {
                return this._oldValue;
            }
        }

        public NotificationReason Reason
        {
            get
            {
                return this._reason;
            }
        }

        /// <summary>
        /// UID of the sender
        /// </summary>
        public int Sender
        {
            get
            {
                return this._owner;
            }
        }
    }

    public enum NotificationReason
    {
        Add,
        Change,
        Remove
    }

}
