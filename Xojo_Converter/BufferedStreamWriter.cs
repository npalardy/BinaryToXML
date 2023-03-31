using System;
using System.IO;

namespace Xojo_Converter
{
    public class BufferedStreamWriter
    {
        private StreamWriter m_outStream;

        public BufferedStreamWriter()
        {
            // get std out as a StreamWriter so we can refer to it using one local
            m_outStream = new StreamWriter(Console.OpenStandardOutput());

            // make sure its set to auto flush
            m_outStream.AutoFlush = true;

            // make sure console uses it
            Console.SetOut(m_outStream);
        }

        public BufferedStreamWriter(Stream outputTo)
        {
            m_outStream = new StreamWriter(outputTo);
        }

        public void Write(string value)
        {
            m_outStream.Write(value);
        }

        public void WriteLine(string value)
        {
            m_outStream.Write(value);
            m_outStream.Write("\n");
        }

        public void Dispose()
        {
            m_outStream.Flush();
            m_outStream.Dispose();
        }
    }
}

