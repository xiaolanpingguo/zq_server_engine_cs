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
        LobbyServer,
        DedicatedServer,
        GSManagerServer,
        TestServer,
    }

    public class MongoConfig
    {
        public class DBInfo
        {
            public string dbName { get; set; } = null!;
            public CollectionInfo[] collections { get; set; } = null!;
        }
        public class CollectionInfo
        {
            public string collectionName { get; set; } = null!;
            public string[] indexNames { get; set; } = null!;
        }

        public string ip { get; set; } = null!;
        public ushort port { get; set; }
        public string srv { get; set; } = null!;
        public string user { get; set; } = null!;
        public string pwd { get; set; } = null!;
        public DBInfo[] dbs { get; set; } = null!;
    }

    public class RedisConfig
    {
        public string ip { get; set; } = null!;
        public ushort port { get; set; }
        public string pwd { get; set; } = null!;
    }

    public abstract class Server
    {
        protected const string k_logConfigName = "../../../Assets/Config/NLog.config";

        protected readonly List<IModule> m_modules = new List<IModule>();
        protected readonly Dictionary<Type, IModule> m_moduleDic = new Dictionary<Type, IModule>();

        protected bool m_running = false;

        public abstract string ServerId { get;}
        public abstract ServerType ServerType { get;}
        public abstract bool Init(string[] args);
        protected abstract bool RegisterModules(string[] args);

        public void Run()
        {
            while (m_running)
            {
                long timeNow = TimeHelper.TimeStampNowMs();
                try
                {
                    foreach (IModule m in m_modules)
                    {
                        m.Update(timeNow);
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

        public T GetModule<T>() where T : IModule
        {
            Type type = typeof(T);
            if (m_moduleDic.TryGetValue(type, out var v))
            {
                return (T)v;
            }

            throw new Exception($"can't find Component:{type.Name}");
        }

        protected bool AddModule<T>(params object[] p) where T: IModule
        {
            Type type = typeof(T);
            try
            {
                if (m_moduleDic.ContainsKey(type))
                {
                    Log.Error($"AddModule failed, the component has exist., type:{type}.");
                    return false;
                }

                T? component = (T?)Activator.CreateInstance(type, p);
                if (component == null) 
                {
                    Log.Error($"AddModule failed, the component is null, type:{type}.");
                    return false;
                }
  
                if (!component.Init())
                {
                    Log.Error($"AddModule failed, init component failed., type:{type}.");
                    return false;
                }

                Log.Info($"add module: {type}.");
                m_modules.Add(component);
                m_moduleDic[type] = component;
                return true;
            }
            catch(Exception ex)
            {
                Log.Error($"AddModule failed, please check your construct params, type:{type}, ex:{ex}");
                return false;
            }
        }

        protected void Shutdown()
        {
            foreach (IModule m in m_modules)
            {
                m.Shutdown();
            }

            m_modules.Clear();
            m_moduleDic.Clear();
        }

        protected T? ReadConfig<T>(string configFile) where T : class
        {
            try
            {
                string jsonString = File.ReadAllText(configFile);
                if (string.IsNullOrEmpty(jsonString))
                {
                    Console.WriteLine($"ReadConfig:{configFile} error, can't found this file.");
                    return null;
                }

                T? config = JsonSerializer.Deserialize<T>(jsonString);
                if (config == null)
                {
                    return null;
                }

                return config;
            }
            catch (Exception e)
            {
                Console.WriteLine($"ReadConfig:{configFile} error, exception:{e.Message}");
                return null;
            }
        }

        protected bool InitLog()
        {
            return Log.Instance.Init("server", k_logConfigName, Log.LogType.Debug, true);
        }
    }
}