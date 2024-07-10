using System;


namespace ZQ
{
    public class GameTime
    {
        private double m_interval;
        private DateTime m_lastUpdateTimeStamp;
        private DateTime m_startUpTimeStamp;
        private double _deltaTime;
        private double m_timeSinceStartUp;
        public long TimeNow { get; private set; }

        public GameTime(int frameRate)
        {
            m_interval = frameRate / 1000.0f;
            m_lastUpdateTimeStamp = DateTime.Now;
            m_startUpTimeStamp = DateTime.Now;
        }

        public void Update()
        {
            TimeNow = TimeHelper.TimeStampNowMs();
        }

        public long FrameTime(int frame)
        {
            return 0;
            //return m_startTime + frame * m_interval;
        }
    }
}