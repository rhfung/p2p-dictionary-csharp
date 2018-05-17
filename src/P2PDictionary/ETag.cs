namespace com.rhfung.P2PDictionary
{
    struct ETag
    {
        public int UID;
        public int Revision;

        public ETag(int owner, int rev)
        {
            this.UID = owner;
            this.Revision = rev;
        }

        /// <summary>
        /// Takes an ETag in the format "42.576" and extracts the version number
        /// </summary>
        /// <param name="s"></param>
        public ETag(string eTag)
        {
            eTag = eTag.TrimStart('\"');
            eTag = eTag.TrimEnd('\"');

            
            string[] parts = eTag.Split(new char[] { '.' });
            this.UID = int.Parse(parts[0]);
            this.Revision = int.Parse(parts[1]);
        }

        public static ETagCompare CompareETags(ETag first, ETag second)
        {
            if (first.Revision > second.Revision)
            {
                return ETagCompare.FirstIsNewer;
            }
            else if (first.Revision < second.Revision)
            {
                return ETagCompare.SecondIsNewer;
            }
            else if (first.UID == second.UID)
            {
                return ETagCompare.Same;
            }
            else
            {
                return ETagCompare.Conflict;
            }
        }

    }

    enum ETagCompare
    {
        FirstIsNewer,   // revision different
        SecondIsNewer,  // revision different
        Same,           // same owner and revision
        Conflict        // same revision, different owner
    }

   
}
