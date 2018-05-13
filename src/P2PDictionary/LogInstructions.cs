using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace com.rhfung.P2PDictionary
{
    class LogInstructions
    {
        private TextWriter m_writer;
        int m_min_level = 0;
        bool m_autoFlush = false;

        public LogInstructions(TextWriter writer, int min_level_to_log, bool autoFlush)
        {
            m_writer = writer;
            m_min_level = min_level_to_log;
            m_autoFlush = false;
        }

        public TextWriter GetTextWriter()
        {
            return m_writer;
        }

        /// <summary>
        /// Writes a log message, if the level of the message matches/exceeds initial configuration.
        /// Auto-flush is controlled on creation.
        /// </summary>
        /// <param name="level">level of the message</param>
        /// <param name="message"></param>
        public void Log(int level, string message, bool flushThisMessage = true)
        {
            if (level >= m_min_level)
            {
                lock (m_writer)
                {
                    m_writer.WriteLine(DateTime.Now.Ticks + "t [" + level + "] "  + message);
                    if (m_autoFlush && flushThisMessage)
                        m_writer.Flush();
                }
            }
        }

        /// <summary>
        /// Writes a log message, if the level of the message matches/exceeds initial configuration.
        /// Auto-flush is controlled on creation.
        /// </summary>
        /// <param name="level">level of the message</param>
        /// <param name="message"></param>
        public void Log(int level, MemoryStream message)
        {
            if (level >= m_min_level)
            {
                StreamReader reader = new StreamReader(message);
                message.Seek(0, SeekOrigin.Begin);

                lock (m_writer)
                {
                    m_writer.WriteLine(DateTime.Now.Ticks + "t [" + level + "] " + " memory stream length " + message.Length);
                    m_writer.WriteLine(reader.ReadToEnd());
                    m_writer.WriteLine(DateTime.Now.Ticks + "t [" + level + "] " + " end memory stream");

                    if (m_autoFlush)
                        m_writer.Flush();
                }
            }
        }
    }
}
