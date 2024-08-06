using System;


namespace ZQ
{
    public class GameTime
    {
        public long StartTime { get; private set; }
        public long Time { get; private set; }

        public GameTime()
        {
            StartTime = StampNow();
        }

        public void Update()
        {
            Time = StampNow() - StartTime;
        }

        public long StampNow() 
        {
            DateTime currentTime = DateTime.UtcNow;
            return ((DateTimeOffset)currentTime).ToUnixTimeMilliseconds();
        }
    }
}