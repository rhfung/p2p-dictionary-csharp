using System.Collections.Generic;

namespace com.rhfung.P2PDictionary
{
    class DataHeader
    {
    

        public string key;

        public int lastOwnerID;
        public int lastOwnerRevision;

        public List<int> sentFrom;

        public ETag GetETag()
        {
            return new ETag() { UID = lastOwnerID, Revision = lastOwnerRevision };
        }

        public DataHeader(string key, ETag version, List<int> sentFrom)
        {
            this.key = key;
            this.lastOwnerID = version.UID;
            this.lastOwnerRevision = version.Revision;
            this.sentFrom = sentFrom;
        }

        public DataHeader(string key, ETag version, int sentFromID)
        {
            this.key = key;
            this.lastOwnerID = version.UID;
            this.lastOwnerRevision = version.Revision;

            this.sentFrom = new List<int>() { sentFromID };
        }

        public override string ToString()
        {
            return key + " [" + lastOwnerID + "." + lastOwnerRevision + "] #senders=" + sentFrom.Count;
        }
     
    }

}
