using System.Collections.Concurrent;
using StackExchange.Redis;

namespace ZQ
{
    public class RedisComponent : IComponent
    {
        internal class RedisTask
        {
            public readonly TaskCompletionSource<RedisResult> resultTcs;
            public string command;
            public object[] args;
            public RedisTask(TaskCompletionSource<RedisResult> resultTcs, string command, object[] args)
            {
                this.resultTcs = resultTcs;
                this.command = command;
                this.args = args;
            }
        }

        internal class RedisTaskCallback
        {
            public readonly TaskCompletionSource completeTcs;
            public readonly TaskCompletionSource<RedisResult> resultTcs;
            public RedisTaskCallback(TaskCompletionSource completeTcs, TaskCompletionSource<RedisResult> resultTcs)
            {
                this.completeTcs = completeTcs;
                this.resultTcs = resultTcs;
            }
        }

        private const int k_connectTimeout = 3000;
        private const int k_checkAlive = 60;
        private const int k_reconnectTime = 3000;
        private const int k_operationTimeout = 3000;

        private string m_endPoint;
        private string m_pwd;
        private ConfigurationOptions m_config = new ConfigurationOptions();
        private IDatabase m_database;

        private Thread m_thread;

        private int k_requestLimitSize = 1024;
        private ConcurrentQueue<RedisTask> m_queueTasks = new ConcurrentQueue<RedisTask>();
        private Queue<RedisTaskCallback> m_queueCallbacks = new Queue<RedisTaskCallback>();

        public RedisComponent(string ip, ushort port, string pwd)
        {
            m_endPoint = ip + ":" + port.ToString();
            m_pwd = pwd;

            m_config.Password = m_pwd;
            m_config.EndPoints.Add(m_endPoint);
            m_config.ConnectTimeout = k_connectTimeout;
            m_config.KeepAlive = k_checkAlive;
            m_config.ReconnectRetryPolicy = new LinearRetry(k_reconnectTime);
            m_config.SyncTimeout = k_operationTimeout;
            m_config.AsyncTimeout = k_operationTimeout;
        }

        public bool Init()
        {
            if (!Connect())
            {
                return false;
            }

            m_thread = new Thread(TaskThread);
            m_thread.Start();
            return true;
        }

        public async Task<RedisResult> ExecuteCmd(string command, params object[] args)
        {
            if (m_queueTasks.Count > k_requestLimitSize)
            {
                Log.Error("too many redis request!");
                return null;
            }

            if (string.IsNullOrEmpty(command))
            {
                return null;
            }

            var completeTcs = new TaskCompletionSource();
            var resultTcs = new TaskCompletionSource<RedisResult>();

            RedisTask task = new RedisTask(resultTcs, command, args);
            m_queueTasks.Enqueue(task);

            RedisTaskCallback cb = new RedisTaskCallback(completeTcs, resultTcs);
            m_queueCallbacks.Enqueue(cb);

            await completeTcs.Task;
            return await resultTcs.Task;
        }

        #region KEY

        public async Task<bool> SET(string key, object value, bool NX = false, int EX = -1)
        {
            RedisResult redisResult = null;
            if (NX && EX > 0)
            {
                redisResult = await ExecuteCmd("SET", key, value, "NX", "EX", EX);
            }
            else if (NX)
            {
                redisResult = await ExecuteCmd("SET", key, value, "NX");
            }
            else if (EX > 0)
            {
                redisResult = await ExecuteCmd("SET", key, value, "EX", EX);
            }
            else
            {
                redisResult = await ExecuteCmd("SET", key, value);
            }

            return redisResult == null ? false : (string)redisResult == "OK";
        }

        public async Task<RedisResult> GET(string key)
        {
            return await ExecuteCmd("GET", key);
        }

        public async Task<RedisResult> GETSET(string key, object value)
        {
            return await ExecuteCmd("GETSET", key, value);
        }

        public async Task<RedisResult> DELETE(string key)
        {
            return await ExecuteCmd("DEL", key);
        }

        #endregion

        #region ZSORT
        public async Task<bool> ZSCORE(string key, string member, double score)
        {
            var redisResult = await ExecuteCmd("ZSCORE", key, member, score);
            return (double)redisResult == score;
        }
        #endregion

        public bool Update(long timeNow)
        {
            processCallbacks();
            return true;
        }

        public bool Shutdown()
        {
            return true;
        }

        private bool Connect()
        {
            try
            {
                var redis = ConnectionMultiplexer.Connect(m_config);
                m_database = redis.GetDatabase();
            }
            catch (Exception e)
            {
                Log.Error($"Connect to Redis failed:{e}");
                return false;
            }

            return true;
        }

        private void processCallbacks()
        {
            if (m_queueCallbacks.Count == 0 || !m_queueCallbacks.TryPeek(out var callback))
            {
                return;
            }

            if (!callback.resultTcs.Task.IsCompleted)
            {
                return;
            }

            m_queueCallbacks.Dequeue();
            callback.completeTcs.SetResult();
        }

        private void TaskThread()
        {
            while (true)
            {
                if (m_queueTasks.Count == 0 || !m_queueTasks.TryDequeue(out var task))
                {
                    continue;
                }

                var t = ExecuteTask(task);
                t.Wait();
            }
        }

        private async Task ExecuteTask(RedisTask task)
        {
            try
            {
                var redisResult = await m_database.ExecuteAsync(task.command, task.args);
                task.resultTcs.SetResult(redisResult);
            }
            catch (Exception e)
            {
                Log.Error($"execute redis command failed, ex:{e}");
                task.resultTcs.SetResult(null);
            }
        }
    }

}
