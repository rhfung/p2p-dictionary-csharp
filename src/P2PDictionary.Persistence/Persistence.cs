using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml;

namespace com.rhfung.P2PDictionary.Persistence
{
    public  class Persistence
    {


        /// <summary>
        /// Reads a XML and loads a dictionary. Dictionary version
        /// numbers are not preserved. 
        /// </summary>
        /// <remarks>
        /// The dictionary namespace must be the same. Dictionary
        /// comments are not restored.
        /// </remarks>
        /// <param name="dict"></param>
        /// <param name="readStream">Reads an XML file produced by Save</param>
        public static void Load(P2PDictionary dict, Stream readStream)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(readStream);
            XmlElement ns = doc["p2pdictionary"]["namespace"];
            if (ns.Attributes["name"].Value != dict.Namespace)
            {
                throw new NotSupportedException("cannot load the dictionary of another namespace");
            }

            IFormatter formatter = new BinaryFormatter();
            foreach (XmlNode entry in  ns.GetElementsByTagName("entry"))
            {
                string key = ((XmlElement)entry).GetAttribute("key");

                string serializedValue = ((XmlElement)entry).InnerText;
                byte[] bytes = Convert.FromBase64String(serializedValue);
                MemoryStream memory = new MemoryStream(bytes);
                object value = formatter.Deserialize(memory);

                dict[key] = value;
            }

        }

        /// <summary>
        /// Only subscribed keys in the same namespace are saved.
        /// </summary>
        /// <param name="dict"></param>
        /// <returns>XML file</returns>
        public static MemoryStream Save(P2PDictionary dict)
        {
            MemoryStream writeStream = new MemoryStream();
            
            System.Xml.XmlTextWriter writer = new XmlTextWriter(writeStream,  Encoding.UTF8);
            ICollection<string> keys = dict.Keys;

            writer.WriteStartDocument();
            writer.WriteStartElement("p2pdictionary");
            writer.WriteStartElement("namespace");
            writer.WriteAttributeString("name", dict.Namespace);
            writer.WriteAttributeString("description", dict.Description);

            IFormatter formatter = new BinaryFormatter();

            foreach (string k in keys)
            {
                writer.WriteStartElement("entry");
                writer.WriteAttributeString("key", k);

                using (MemoryStream contents = new MemoryStream())
                {
                    formatter.Serialize(contents, dict[k]);
                    writer.WriteBase64(contents.GetBuffer(), 0, (int)contents.Length);
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.Flush();

            return writeStream;
        }
    }
}
