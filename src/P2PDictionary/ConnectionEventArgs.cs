using System;

namespace com.rhfung.P2PDictionary
{
    public class ConnectionEventArgs : EventArgs
    {
        public System.Net.EndPoint EndPoint;
        public int RemoteUID;
    }

    public class ConnectionErrorEventArgs : ConnectionEventArgs
    {
        public Exception Error;
    }
}
