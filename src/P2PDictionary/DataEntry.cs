using System;
using System.Collections.Generic;

namespace com.rhfung.P2PDictionary
{
    class DataEntry : Data
    {
        public string key { get; set; }

        public override object value
        {
            get
            {
                return instantiatedValue;
            }
            set
            {
                instantiatedValue = value;
            }
        }

        private object instantiatedValue;
        //TODO: implement this here -- private byte[] serializedValue;

        /// <summary>
        /// Etag.ID
        /// </summary>
        public int lastOwnerID { get; set; }

        /// <summary>
        /// Etag.revision
        /// </summary>
        public int lastOwnerRevision { get; set; }

        /// <summary>
        /// Save a path to the sender
        /// </summary>
        public List<int> senderPath { get; set; }

        /// <summary>
        /// subscription
        /// </summary>
        public bool subscribed { get; set; }

        public ETag GetETag()
        {
            return new ETag() { UID = lastOwnerID, Revision = lastOwnerRevision };
        }

        public DataEntry(string key, ETag lastOwner, List<int> senderPath)
        {
            this.key = key;
            this.lastOwnerID = lastOwner.UID;
            this.lastOwnerRevision = lastOwner.Revision;
            this.senderPath = senderPath;
        }

        public DataEntry(string key, object value, ETag lastOwner, List<int> senderPath,bool subscribed)
        {
            this.key = key;
            this.value = value;
            this.lastOwnerID = lastOwner.UID;
            this.lastOwnerRevision = lastOwner.Revision;
            this.senderPath = senderPath;
            this.subscribed = subscribed;

            DetectType();
        }

        public void DetectTypeFromValue()
        {
            DetectType();
        }

        private void DetectType()
        {
            if (this.value is byte[])
            {
                this.type = ValueType.Binary;
            }
            else if (this.value is Single)
            {
                this.type = ValueType.Single;
            }
            else if (this.value is Double)
            {
                this.type = ValueType.Double;
            }
            else if (this.value is Boolean)
            {
                this.type = ValueType.Boolean;
            }
            else if (this.value is Int16)
            {
                this.type = ValueType.Int16;
            }
            else if (this.value is Int32)
            {
                this.type = ValueType.Int32;
            }
            else if (this.value is Int64)
            {
                this.type = ValueType.Int64;
            }
            else if (this.value is String)
            {
                this.type = ValueType.String;
            }
            else if (this.value == null)
            {
                this.type = ValueType.Null;
            }
            else if (this.value is DataUnsupported)
            {
                this.type = ValueType.Unknown;
            }
            else
            {
                this.type = ValueType.Object;
            }
        }


        public override string ToString()
        {
            if (subscribed)
            {
                return key + "@[" + lastOwnerID + "." + lastOwnerRevision + "]," + base.ToString();
            }
            else
            {
                return key + "@[" + lastOwnerID + "." + lastOwnerRevision + "],NotSubscribed," + base.ToString();
            }
        }

    }
}
