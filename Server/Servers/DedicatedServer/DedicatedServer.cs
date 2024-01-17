using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZQ
{
    public class DedicatedServerConfig
    {
        public string externalIp { get; set; } = null!;
        public ushort externalPort { get; set; }

        public string masterIp { get; set; } = null!;
        public ushort masterPort { get; set; }
    }

    public class DedicatedServer : Server
    {
        private const string k_serverConfigName = "Assets/Config/LoginServerConfig.json";

        public DedicatedServerConfig Config = null!;

        private string m_serverId = string.Empty;
        public override string ServerId => m_serverId;

        private ServerType m_serverType = ServerType.DedicatedServer;
        public override ServerType ServerType => m_serverType;

        public override bool Init(string[] args)
        {
            if (!base.Init(args))
            {
                return false;
            }

            DedicatedServerConfig? config = ReadConfig<DedicatedServerConfig>(k_serverConfigName);
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
            if (!AddModule<TimerModule>()) return false;

            // if (!AddModule<Dedicate2MasterModule>(this, Config.masterIp, Config.masterPort)) return false;
            if (!AddModule<C2DedicatedModule>(this, Config.externalIp, Config.externalPort)) return false;

            return true;
        }
    }
}