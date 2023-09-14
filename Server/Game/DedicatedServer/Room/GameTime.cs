using System;

namespace ZQ
{
    public class GameTime
    {
        private int m_timeZone; 
        private DateTime m_dt1970;
        private DateTime m_dt;
        
        public long ServerMinusClientTime { private get; set; }

        public long FrameTime { get; private set; }

        public int TimeZone
        {
            get
            {
                return m_timeZone;
            }
            set
            {
                m_timeZone = value;
                m_dt = m_dt1970.AddHours(TimeZone);
            }
        }

        public GameTime()
        {
            m_dt1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            m_dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            FrameTime = this.ClientNow();
        }

        public void Update()
        {
            this.FrameTime = this.ClientNow();
        }
        
        public DateTime ToDateTime(long timeStamp)
        {
            return m_dt.AddTicks(timeStamp * 10000);
        }
        
        public long ClientNow()
        {
            return (DateTime.UtcNow.Ticks - m_dt1970.Ticks) / 10000;
        }
        
        public long ServerNow()
        {
            return ClientNow() + this.ServerMinusClientTime;
        }
        
        public long ClientFrameTime()
        {
            return this.FrameTime;
        }
        
        public long ServerFrameTime()
        {
            return this.FrameTime + this.ServerMinusClientTime;
        }
        
        public long Transition(DateTime d)
        {
            return (d.Ticks - m_dt.Ticks) / 10000;
        }
    }
}