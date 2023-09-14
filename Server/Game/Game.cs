using MongoDB.Bson.Serialization.Serializers;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZQ
{
    public enum ServerType : short
    {
        MasterServer,
        LoginServer,
        ZoneServer,
        ZoneManagerServer
    }

    public class ServerConfig
    {
        public class Login
        {
            public string externalIp { get; set; }
            public ushort externalPort { get; set; }
        }

        public class Zone
        {
            public string externalIp { get; set; }
            public ushort externalPort { get; set; }
        }

        public class Master
        {
            public string internalIp { get; set; }
            public ushort internalPort { get; set; }
        }

        public class Redis
        {
            public string ip { get; set; }
            public ushort port { get; set; }
            public string pwd { get; set; }
        }

        public class Mongo
        {
            public class DBInfo
            {
                public string dbName { get; set; }
                public CollectionInfo[] collections { get; set; }
            }
            public class CollectionInfo
            {
                public string collectionName { get; set; }
                public string[] indexNames { get; set; }
            }

            public string ip { get; set; }
            public ushort port { get; set; }
            public string srv { get; set; }
            public string user { get; set; }
            public string pwd { get; set; }
            public DBInfo[] dbs { get; set; }
        }
        public Login login { get; set; }
        public Zone zone { get; set; }
        public Master master { get; set; }
        public Redis redis { get; set; }
        public Mongo mongo { get; set; }
    }

    public class Game
    {
        private const string k_testModeArg = "-test";
        private const string k_testMongoArg = "-test_mongo";
        private const string k_testClientArg = "-test_client";
        private const string k_testKcpClientArg = "-test_kcpclient";

        private const string k_serverConfigName = "../../../Assets/Config/ServerConfig.json";
        private const string k_logConfigName = "../../../Assets/Config/NLog.config";

        private readonly List<IComponent> m_components = new List<IComponent>();
        private readonly Dictionary<Type, IComponent> m_componentsDic = new Dictionary<Type, IComponent>();

        private bool m_running = false;

        public ServerConfig Config;
        public string MasterServerId;
        public string LoginServerId;
        public string ZoneServerId;
        public string ZoneManagerServerId;

        private static Game m_instance = null;
        public static Game Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = new Game();
                }
                return m_instance;
            }
        }

        public bool Init(string[] args)
        {
            if (!ReadConfig(args))
            {
                return false;
            }

            if (!InitLog())
            {
                return false;
            }

            if (EnterTestMode(args))
            {
                m_running = true;
                return true;
            }

            if (!RegisterComponents(args))
            {
                return false;
            }

            string title = $"{MasterServerId}{" "}{LoginServerId}{" "}{ZoneServerId}";
            Console.Title = title;
            m_running = true;
            return true;
        }

        public bool AddComponent<T>(params object[] p) where T: IComponent
        {
            Type type = typeof(T);
            try
            {
                T component = (T)Activator.CreateInstance(type, p);
                if (m_componentsDic.ContainsKey(type))
                {
                    Log.Error($"AddComponent failed, the component has exist., type:{type}.");
                    return false;
                }
                if (!component.Init())
                {
                    Log.Error($"AddComponent failed, init component failed., type:{type}.");
                    return false;
                }

                m_components.Add(component);
                m_componentsDic[type] = component;
                return true;
            }
            catch(Exception ex)
            {
                Log.Error($"AddComponent failed, please check your construct params, type:{type}, ex:{ex}");
                return false;
            }
        }

        public T GetComponent<T>() where T : IComponent
        {
            Type type = typeof(T);
            if (m_componentsDic.TryGetValue(type, out var v))
            {
                return (T)v;
            }

            throw new Exception($"can't find Component:{type.Name}");
        }

        public void Update()
        {
            while (m_running)
            {
                long timeNow = TimeHelper.TimeStampNowMs();
                try
                {
                    foreach (IComponent component in m_components)
                    {
                        component.Update(timeNow);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
            }

            Shutdown();
        }

        public void Stop()
        {
            m_running = false;
        }

        private void Shutdown()
        {
            foreach (IComponent component in m_components)
            {
                component.Shutdown();
            }

            m_components.Clear();
            m_componentsDic.Clear();
        }

        private bool ReadConfig(string[] args)
        {
            try
            {
                string jsonString = File.ReadAllText(k_serverConfigName);
                Config = JsonSerializer.Deserialize<ServerConfig>(jsonString);
            }
            catch (Exception e)
            {
                Console.WriteLine($"ReadConfig:{k_serverConfigName} error:{e.Message}");
                return false;
            }

            return true;
        }

        private bool InitLog()
        {
            return Log.Instance.Init("server", k_logConfigName, Log.LogType.Debug, true);
        }

        private bool RegisterComponents(string[] args)
        {
            List<MongoDBSetupConfig.DBInfo> dbs = new();
            foreach (var db in Config.mongo.dbs)
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
                ip = Config.mongo.ip,
                port = Config.mongo.port,
                srv = Config.mongo.srv,
                user = Config.mongo.user,
                pwd = Config.mongo.pwd,
                dbs = dbs.ToArray()
            };

            if (!AddComponent<TimerComponent>()) return false;
            //if (!AddComponent<RedisComponent>(Config.redis.ip, Config.redis.port, Config.redis.pwd)) return false;
            //if (!AddComponent<MongoComponent>(mongoConfig)) return false;

            //long timeNow = TimeHelper.TimeStampNowSeconds();
            //if (Config.master != null)
            //{
            //    MasterServerId = $"{ServerType.MasterServer}-{Config.master.internalIp}:{Config.master.internalPort}-{timeNow}";
            //    if (!AddComponent<S2MasterComponent>(Config.master.internalIp, Config.master.internalPort)) return false;
            //}

            //if (Config.login != null)
            //{
            //    LoginServerId = $"{ServerType.LoginServer}-{Config.login.externalIp}:{Config.login.externalPort}-{timeNow}";
            //    if (!AddComponent<Login2MasterComponent>(Config.master.internalIp, Config.master.internalPort)) return false;
            //    if (!AddComponent<C2LoginComponent>(Config.login.externalIp, Config.login.externalPort)) return false;
            //}

            //if (Config.zone != null)
            //{
            //    ZoneServerId = $"{ServerType.ZoneServer}-{Config.zone.externalIp}:{Config.zone.externalPort}-{timeNow}";
            //    if (!AddComponent<Zone2MasterComponent>(Config.master.internalIp, Config.master.internalPort)) return false;
            //    if (!AddComponent<C2ZoneComponent>(Config.zone.externalIp, Config.zone.externalPort)) return false;
            //}

            if (!AddComponent<C2DedicatedComponent>(Config.login.externalIp, Config.login.externalPort)) return false;
            return true;
        }

        private bool EnterTestMode(string[] args)
        {
            foreach(var arg in args) 
            {
                if (arg == k_testMongoArg) AddComponent<MongoTestComponent>();
                if (arg == k_testClientArg) AddComponent<ClientTestComponent>(Config.login.externalIp, Config.login.externalPort, 1);
                if (arg == k_testKcpClientArg) AddComponent<KcpClientTestComponent>(Config.login.externalIp, Config.login.externalPort, 2);
            }

            return false;
            return m_components.Count > 0;
        }
    }
}