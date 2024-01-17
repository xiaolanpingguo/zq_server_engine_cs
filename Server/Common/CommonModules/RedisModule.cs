using System.Threading.Tasks;
using StackExchange.Redis;

namespace ZQ
{
    public class RedisModule : IModule
    {
        private const int k_connectTimeout = 3000;
        private const int k_checkAlive = 60;
        private const int k_reconnectTime = 3000;
        private const int k_operationTimeout = 3000;

        private string m_endPoint;
        private string m_pwd;
        private ConfigurationOptions m_config = new ConfigurationOptions();
        private IDatabase m_database;

        public RedisModule(string ip, ushort port, string pwd)
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

            return true;
        }

        public async Task<RedisResult> ExecuteCmd(string command, params object[] args)
        {
            if (string.IsNullOrEmpty(command))
            {
                return null;
            }

            return await ExecuteCmdInternal(command, args);
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
            return true;
        }

        public bool Shutdown()
        {
            return true;
        }

        private async Task<RedisResult> ExecuteCmdInternal(string command, params object[] args)
        {
            try
            {
                if (string.IsNullOrEmpty(command))
                {
                    return null;
                }

                var redisResult = await m_database.ExecuteAsync(command, args);
                return redisResult;
            }
            catch (Exception ex) 
            {
                Log.Error($"ExecuteCmdInternal: exe redis cmd failed, command:{command}, exception:{ex}");
                return null;
            }
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
    }

}
