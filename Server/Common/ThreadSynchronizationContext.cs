using System.Collections.Concurrent;
using System.Threading;
using System;

namespace ZQ;

public class ThreadSynchronizationContext : SynchronizationContext
{
    private readonly ConcurrentQueue<Action> m_queue = new();
    private Action m_action;

    public void Update()
    {
        while (true)
        {
            if (!m_queue.TryDequeue(out m_action))
            {
                return;
            }

            try
            {
                m_action();
            }
            catch (Exception e)
            {
                Log.Error($"ThreadSynchronizationContext.Update, exception occerred:{e}");
            }
        }
    }

    public override void Post(SendOrPostCallback callback, object state)
    {
        Post(() => callback(state));
    }

    public void Post(Action action)
    {
        m_queue.Enqueue(action);
    }
}
