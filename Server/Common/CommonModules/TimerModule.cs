using System.Collections.Generic;
using System.Threading;

namespace ZQ
{
    public class TimerModule: IModule
    {
        internal class TimerAction
        {
            public bool Repeat;
            public int Interval;
            public object Args;
            public Action<object> Fun;
            public static TimerAction Create(Action<object> fun, object args, int interval, bool repeat)
            {
                TimerAction timerAction = new TimerAction();
                timerAction.Repeat = repeat;
                timerAction.Interval = interval;
                timerAction.Fun = fun;
                timerAction.Args = args;
                return timerAction;
            }
        }

        private long m_idGenerator;
        private long m_minTime = long.MaxValue;
        private readonly MultiMap<long, long> m_timeId = new();
        private readonly Dictionary<long, TimerAction> m_timerActions = new();
        private readonly Queue<long> m_timeOutTime = new();
        private readonly Queue<long> m_timeOutTimerIds = new();

        public bool Init()
        {
            return true;
        }

        public bool Shutdown()
        {
            m_timeId.Clear();
            m_timerActions.Clear();
            m_timeOutTime.Clear();
            m_timeOutTimerIds.Clear();
            return true;
        }

        public long AddTimer(int interval, Action<object> fun, object arg = null, bool repeat = true)
        {
            TimerAction timer = TimerAction.Create(fun, arg, interval, repeat);
            long id = m_idGenerator++;
            long expire = TimeHelper.TimeStampNowMs() + interval;
            m_timeId.Add(expire, id);
            m_timerActions.Add(id, timer);
            if (expire < m_minTime)
            {
                m_minTime = expire;
            }

            return id;
        }

        public bool RemoveTimer(long id)
        {
            if (id == 0)
            {
                return false;
            }

            if (!m_timerActions.Remove(id, out TimerAction timerAction))
            {
                return false;
            }

            return true;
        }


        public bool Update(long timeNow)
        {
            if (m_timeId.Count == 0)
            {
                return true;
            }

            if (timeNow < m_minTime)
            {
                return true;
            }

            foreach (KeyValuePair<long, List<long>> kv in m_timeId)
            {
                long k = kv.Key;
                if (k > timeNow)
                {
                    m_minTime = k;
                    break;
                }

                m_timeOutTime.Enqueue(k);
            }

            while (m_timeOutTime.Count > 0)
            {
                long time = m_timeOutTime.Dequeue();
                var list = m_timeId[time];
                for (int i = 0; i < list.Count; ++i)
                {
                    long timerId = list[i];
                    m_timeOutTimerIds.Enqueue(timerId);
                }
                m_timeId.Remove(time);
            }

            while (m_timeOutTimerIds.Count > 0)
            {
                long timerId = m_timeOutTimerIds.Dequeue();

                if (!m_timerActions.Remove(timerId, out var timerAction))
                {
                    continue;
                }

                timerAction.Fun?.Invoke(timerAction.Args);
                if (timerAction.Repeat)
                {
                    long expire = timeNow + timerAction.Interval;
                    if (expire < m_minTime)
                    {
                        m_minTime = expire;
                    }
                    m_timeId.Add(expire, timerId);
                    m_timerActions.Add(timerId, timerAction);
                }
            }

            return true;
        }
    }
}