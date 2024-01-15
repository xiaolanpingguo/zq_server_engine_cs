using System;
using System.Collections.Generic;
using System.IO;

namespace ZQ
{
    public class FrameBuffer
    {
        private int m_frameCountPerSecond;
        private readonly List<OneFrameInputs> m_frameInputs;
        private readonly List<MemoryBuffer> m_snapshots;
        private readonly List<long> m_hashs;

        public int MaxFrame { get; private set; }

        public FrameBuffer(int frame, int frameCountPerSecond)
        {
            m_frameCountPerSecond = frameCountPerSecond;
            int capacity = m_frameCountPerSecond * 60;
            MaxFrame = frame + m_frameCountPerSecond * 30;
            m_frameInputs = new List<OneFrameInputs>(capacity);
            m_snapshots = new List<MemoryBuffer>(capacity);
            m_hashs = new List<long>(capacity);
            
            for (int i = 0; i < m_snapshots.Capacity; ++i)
            {
                m_hashs.Add(0);
                m_frameInputs.Add(new OneFrameInputs());
                MemoryBuffer memoryBuffer = new(10240);
                memoryBuffer.SetLength(0);
                memoryBuffer.Seek(0, SeekOrigin.Begin);
                m_snapshots.Add(memoryBuffer);
            }
        }

        public void SetHash(int frame, long hash)
        {
            EnsureFrame(frame);
            m_hashs[frame % m_frameInputs.Capacity] = hash;
        }
        
        public long GetHash(int frame)
        {
            EnsureFrame(frame);
            return m_hashs[frame % m_frameInputs.Capacity];
        }

        public bool CheckFrame(int frame)
        {
            if (frame < 0)
            {
                return false;
            }

            if (frame > MaxFrame)
            {
                return false;
            }

            return true;
        }

        private void EnsureFrame(int frame)
        {
            if (!CheckFrame(frame))
            {
                throw new Exception($"frame out: {frame}, maxframe: {MaxFrame}");
            }
        }
        
        public OneFrameInputs FrameInputs(int frame)
        {
            EnsureFrame(frame);
            OneFrameInputs oneFrameInputs = m_frameInputs[frame % m_frameInputs.Capacity];
            return oneFrameInputs;
        }

        public void MoveForward(int frame)
        {
            // 至少留出1秒的空间
            if (MaxFrame - frame > m_frameCountPerSecond)
            {
                return;
            }
            
            ++MaxFrame;
            
            OneFrameInputs oneFrameInputs = FrameInputs(MaxFrame);
            oneFrameInputs.Inputs.Clear();
        }

        public MemoryBuffer Snapshot(int frame)
        {
            EnsureFrame(frame);
            MemoryBuffer memoryBuffer = m_snapshots[frame % m_snapshots.Capacity];
            return memoryBuffer;
        }
    }
}