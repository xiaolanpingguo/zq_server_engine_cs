using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZQ
{
    public class LoginServerConfig
    {
        public string externalIp { get; set; } = null!;
        public ushort externalPort { get; set; }

        public string masterIp { get; set; } = null!;
        public ushort masterPort { get; set; }

        public RedisConfig redisConfig { get; set; } = null!;
        public MongoConfig mongoConfig { get; set; } = null!;
    }

    public class LoginServer : Server
    {
        private const string k_serverConfigName = "../../../Assets/Config/LoginServerConfig.json";

        public LoginServerConfig Config = null!;

        private string m_serverId = string.Empty;
        public override string ServerId => m_serverId;

        private ServerType m_serverType = ServerType.LoginServer;
        public override ServerType ServerType => m_serverType;

        public override bool Init(string[] args)
        {
            LoginServerConfig? config = ReadConfig<LoginServerConfig>(k_serverConfigName);
            if (config == null)
            {
                return false;
            }
            Config = config;

            if (!InitLog())
            {
                return false;
            }

            if (!RegisterModules(args))
            {
                return false;
            }

            long timeNow = TimeHelper.TimeStampNowSeconds();
            m_serverId = $"{ServerType}-{Config.externalIp}:{Config.externalPort}-{timeNow}";
            Console.Title = ServerId;
            m_running = true;
            Log.Info($"server start successfully, server id:{ServerId}");
            return true;
        }

        protected override bool RegisterModules(string[] args)
        {
            List<MongoDBSetupConfig.DBInfo> dbs = new();
            foreach (var db in Config.mongoConfig.dbs)
            {
                MongoDBSetupConfig.DBInfo dbInfo = new MongoDBSetupConfig.DBInfo();
                dbInfo.dbName = db.dbName;

                List<MongoDBSetupConfig.CollectionInfo> collections = new();
                foreach (var col in db.collections)
                {
                    MongoDBSetupConfig.CollectionInfo colInfo = new MongoDBSetupConfig.CollectionInfo();
                    colInfo.collectionName = col.collectionName;
                    colInfo.indexNames = col.indexNames;
                    collections.Add(colInfo);
                }

                dbInfo.collections = collections.ToArray();
                dbs.Add(dbInfo);
            }
            MongoDBSetupConfig mongoConfig = new MongoDBSetupConfig()
            {
                ip = Config.mongoConfig.ip,
                port = Config.mongoConfig.port,
                srv = Config.mongoConfig.srv,
                user = Config.mongoConfig.user,
                pwd = Config.mongoConfig.pwd,
                dbs = dbs.ToArray()
            };

            if (!AddModule<TimerModule>()) return false;
            if (!AddModule<RedisModule>(Config.redisConfig.ip, Config.redisConfig.port, Config.redisConfig.pwd)) return false;
            if (!AddModule<MongoModule>(mongoConfig)) return false;
           // if (!AddModule<Login2MasterModule>(this, Config.masterIp, Config.masterPort)) return false;
            if (!AddModule<C2LoginModule>(this, Config.externalIp, Config.externalPort)) return false;

            return true;
        }
    }
}