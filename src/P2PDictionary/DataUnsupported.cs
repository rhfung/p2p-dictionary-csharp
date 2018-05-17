namespace com.rhfung.P2PDictionary
{
    /// <summary>
    /// Data stored in the dictionary with an unknown MIME type.
    /// </summary>
    public class DataUnsupported
    {
        private string m_mimeType;
        private byte[] m_payload;

        internal DataUnsupported(string mimeType, byte[] payload)
        {
            this.m_mimeType = mimeType;
            this.m_payload = payload;
        }

        public string GetMimeType()
        {
            return m_mimeType;
        }

        /// <summary>
        /// Use null to denote missing data
        /// </summary>
        /// <returns></returns>
        public byte[] GetPayload()
        {
            return m_payload;
        }

        override public string ToString()
        {
            return "Data in payload " + m_mimeType;
        }
    }
}
