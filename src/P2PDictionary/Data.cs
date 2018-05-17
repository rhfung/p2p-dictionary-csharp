using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace com.rhfung.P2PDictionary
{
    abstract class Data
    {
        // 3rd column: false = simple; true = complex
        private static List<Tuple<ValueType, string, bool>> encodings;

        abstract public object value { get; set; }
        public ValueType type;
        private string m_hackMimeType; // hack mime type for unknown encoding

        static Data()
        {
      
            encodings = new List<Tuple<ValueType, string,bool>>();
            encodings.Add(new Tuple<ValueType, string, bool>(ValueType.Boolean, "number/bool", false));//1
            encodings.Add(new Tuple<ValueType, string, bool>(ValueType.Int32, "number/int32", false));
            encodings.Add(new Tuple<ValueType, string, bool>(ValueType.Int16, "number/int16", false));//3
            encodings.Add(new Tuple<ValueType, string, bool>(ValueType.Int64, "number/int64",false));
            encodings.Add(new Tuple<ValueType, string, bool>(ValueType.Single, "number/single",false));//5
            encodings.Add(new Tuple<ValueType, string, bool>(ValueType.Double, "number/double",false));
            encodings.Add(new Tuple<ValueType, string, bool>(ValueType.String, "text/plain",true));//7
            encodings.Add(new Tuple<ValueType, string, bool>(ValueType.Binary, "application/octet-stream", true));
            encodings.Add(new Tuple<ValueType, string, bool>(ValueType.Object, "application/vs-object", true));//9
            encodings.Add(new Tuple<ValueType, string, bool>(ValueType.Removed, "application/nothing",false));
            encodings.Add(new Tuple<ValueType, string, bool>(ValueType.Null, "application/null",false));//11
            encodings.Add(new Tuple<ValueType, string, bool>(ValueType.Unknown, "", true));//11

        }

        public enum ValueType
        {
            Removed, // empty
            Boolean,// simple
            Int16,
            Int32,
            Int64,
            Single,
            Double,
            Null,
            String, //complex
            Binary,
            Object,
            Unknown
        }

        public bool IsEmpty
        {
            get
            {
                return type == ValueType.Removed;
            }
        }

        public bool IsSimpleValue
        {
            get
            {
                //return (type == ValueType.Boolean || type == ValueType.Int16 || type == ValueType.Int32 || type == ValueType.Int64
                //    || type == ValueType.Single || type == ValueType.Double || type == ValueType.Null);
                return encodings.Exists(x => x.Item1 == type && x.Item3 == false && x.Item1 != ValueType.Removed );
            }
        }

        public bool IsSimpleType(ValueType type)
        {
            
                //return (type == ValueType.Boolean || type == ValueType.Int16 || type == ValueType.Int32 || type == ValueType.Int64
                //    || type == ValueType.Single || type == ValueType.Double);
            return encodings.Exists(x => x.Item1 == type && x.Item3 == false && x.Item1 != ValueType.Removed);
        }

        public bool IsComplexValue
        {
            get
            {
                // return (type == ValueType.String || type == ValueType.Binary || type == ValueType.Object);
                return encodings.Exists(x => x.Item1 == type  && x.Item3 == true);
            }
        }

        public override string ToString()
        {
            if (DataMissing.IsSingleton(this.value))
            {
                return GetMime() + ";m=DataMissing";
            }
            else
            {
                return GetMime() + GetMimeSimpleData() + "";
            }
        }

        public string GetMimeSimpleData()
        {
            if (DataMissing.IsSingleton(value) )
            {
                // no value to report
                return "";
            }
            else
            {
                if (type == ValueType.Boolean)
                {
                    return ";d=" + value.ToString();
                }
                else if (type == ValueType.Int32)
                {
                    return ";d=" + value.ToString();
                }
                else if (type == ValueType.Int16)
                {
                    return ";d=" + value.ToString();
                }
                else if (type == ValueType.Int64)
                {
                    return ";d=" + value.ToString();
                }
                else if (type == ValueType.Single)
                {
                    return ";d=" + value.ToString();
                }
                else if (type == ValueType.Double)
                {
                    return ";d=" + value.ToString();
                }
                else
                {
                    return "";
                }
            }
        }

        public void ReadBytesUsingMime(string mimeType, byte[] data)
        {
            ReadMimeData(mimeType, "", data);
        }

        public void ReadMimeSimpleData(string mimeType)
        {
            string mime = mimeType.Split(';')[0];
            string nval = "";
            if (mime.Split('/')[0] == "number")
            {
                if (mimeType.IndexOf(";") > 0)
                {
                    nval = mimeType.Split(';')[1].Substring(2);
                    ReadMimeData(mime, nval, null);
                }
                else
                {
                    SetMime(mime);
                    this.value = DataMissing.Singleton;
                }
            }
            else
            {
                ReadMimeData(mime, nval, null);
            }

            
        }

        private void ReadMimeData(string mimeType, string nval, byte[] sourceBytes)
        {
            string mime = mimeType.Split(';')[0];
            string rootMime = mime.Split('/')[0];
            if (rootMime == "number" && mimeType.Contains(';'))
            {
                nval = mimeType.Split(';')[1].Substring(2);
            }

            if (sourceBytes != null && (rootMime == "number" || rootMime == "text"))
            {
                nval = System.Text.Encoding.UTF8.GetString(sourceBytes);
                //StreamReader r = new StreamReader(new MemoryStream(sourceBytes), Encoding.UTF8);
                //nval = r.ReadToEnd();
            }

            switch (mime)
            {
                case "number/bool":
                    this.value = bool.Parse(nval);
                    this.type = ValueType.Boolean;
                    break;
                case "number/int16":
                    this.value = Int16.Parse(nval);
                    this.type = ValueType.Int16;
                    break;
                case "number/int32":
                    this.value = Int32.Parse(nval);
                    this.type = ValueType.Int32;
                    break;
                case "number/int64":
                    this.value = Int64.Parse(nval);
                    this.type = ValueType.Int64;
                    break;

                case "number/single":
                    this.value = Single.Parse(nval);
                    this.type = ValueType.Single;
                    break;

                case "number/double":
                    this.value = Double.Parse(nval);
                    this.type = ValueType.Double;
                    break;

                case "text/plain":
                    this.type = ValueType.String;
                    if (sourceBytes != null)
                    {
                        this.value = nval;
                    }
                    else
                    {
                        this.value = DataMissing.Singleton;
                    }
                    break;
                
                case "application/octet-stream":
                    this.type = ValueType.Binary;
                    if (sourceBytes != null)
                    {
                        this.value = sourceBytes;
                    }
                    else
                    {
                        this.value = DataMissing.Singleton;
                    }
                    break;
                
                case "application/vs-object":
                    this.type = ValueType.Object;
                    if (sourceBytes != null)
                    {
                        try
                        {
                            BinaryFormatter formatter = new BinaryFormatter();
                            this.value = formatter.Deserialize(new MemoryStream(sourceBytes));
                        }
                        catch
                        {
                            throw;
                        }
                    }
                    else
                    {
                        this.value = DataMissing.Singleton;
                    }
                    break;
                case "application/nothing":
                    this.type = ValueType.Removed;
                    this.value = null;
                    break;
                case "application/null":
                    this.type = ValueType.Null;
                    this.value = null;
                    break;
                default:
                    this.type = ValueType.Unknown;
                    m_hackMimeType = mime;
                    if (sourceBytes == null)
                        this.value = DataMissing.Singleton;
                    else
                        this.value = new DataUnsupported(mime, sourceBytes);
                    break;
            }

        }

        public string GetMime()
        {
            if (this.type == ValueType.Unknown)
            {
                if (this.value is DataUnsupported)
                    return ((DataUnsupported)this.value).GetMimeType();
                else
                    return m_hackMimeType;
            }
            else
            {
                // most mime types are not standard
                return encodings.Find(x => x.Item1 == type).Item2;
            }

        }

        /// <summary>
        /// Should use ReadBytesUsingMime or ReadMimeSimpleData. This is used for HEAD.
        /// </summary>
        /// <returns></returns>
        public void SetMime(string type)
        {
            var found = encodings.Find(x => x.Item2 == type);
            if (found == null)
            {
                this.type = ValueType.Unknown;
                m_hackMimeType = type;
            }
            else
            {
                this.type = found.Item1;
            }
        }

        public bool IsDeleted
        {
            get
            {
                return this.type == ValueType.Removed;
            }
        }

        public void Delete()
        {
            this.type = ValueType.Removed;
            this.value = null;
        }
    }
}
