namespace ZQ
{
    public class FixedTimeCounter
    {
        private long m_startTime;
        private int m_startFrame;
        public int Interval { get; private set; }

        public FixedTimeCounter(long startTime, int startFrame, int interval)
        {
            m_startTime = startTime;
            m_startFrame = startFrame;
            this.Interval = interval;
        }
        
        public void ChangeInterval(int interval, int frame)
        {
            m_startTime += (frame - m_startFrame) * this.Interval;
            m_startFrame = frame;
            this.Interval = interval;
        }

        public long FrameTime(int frame)
        {
            return m_startTime + (frame - m_startFrame) * this.Interval;
        }
        
        public void Reset(long time, int frame)
        {
            m_startTime = time;
            m_startFrame = frame;
        }
    }
}