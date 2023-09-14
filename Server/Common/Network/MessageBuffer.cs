using System;
using System.Collections.Generic;
using System.IO;

namespace ZQ
{
    public class MessageBuffer
    {
        private byte[] m_buffer;
        private int m_wpos;
        private int m_rpos;

        public MessageBuffer(int size)
        {
            m_buffer = new byte[size];
            Reset();
        }

        public int GetBufferSize()
        {
            return m_buffer.Length;
        }

        public int GetWritePos()
        {
            return m_wpos;
        }

        public int GetReadPos()
        {
            return m_rpos;
        }

        public byte[] GetBuffer()
        {
            return m_buffer;
        }

        public int GetActiveSize()
        {
            return m_wpos - m_rpos;
        }

        public void ReadCompleted(int size)
        {
            m_rpos += size;
        }

        public void WriteCompleted(int size)
        {
            m_wpos += size;
        }

        public void Write(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || buffer.Length > GetRemainingSpace())
            {
                return;
            }

            Array.Copy(buffer, 0, m_buffer, GetWritePos(), buffer.Length);
            WriteCompleted(buffer.Length);
        }

        public void Normalize()
        {
            if (m_rpos > 0)
            {
                if (m_rpos != m_wpos)
                {
                    Array.Copy(m_buffer, m_rpos, m_buffer, 0, GetActiveSize());
                }

                m_wpos -= m_rpos;
                m_rpos = 0;
            }
        }

        public void EnsureFreeSpace()
        {
            Array.Resize(ref m_buffer, m_buffer.Length * 3 / 2);
        }

        public int GetRemainingSpace()
        {
            return m_buffer.Length - m_wpos;
        }

        public void Reset()
        {
            m_wpos = 0;
            m_rpos = 0;
        }
    }
}