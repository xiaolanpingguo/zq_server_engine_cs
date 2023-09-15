using System;

namespace ZQ
{
    public class GameTime
    {
        private long m_startTime;
        private int m_interval;
        private DateTime m_dt1970;
        public long FrameTimeNow { get; private set; }

        public GameTime(int interval)
        {
            m_interval = interval;
            m_dt1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            FrameTimeNow = Now();
            m_startTime = FrameTimeNow;
        }

        public void Update()
        {
            FrameTimeNow = Now();
        }
        
        public long Now()
        {
            return (DateTime.UtcNow.Ticks - m_dt1970.Ticks) / 10000;
        }

        public long FrameTime(int frame)
        {
            return m_startTime + frame * m_interval;
        }
    }
}