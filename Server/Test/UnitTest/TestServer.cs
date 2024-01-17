using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZQ
{
    public class TestServer : Server
    {
        private const string k_testMongoArg = "-test_mongo";
        private const string k_testRedisArg = "-test_redis";
        private const string k_testClientArg = "-test_client";
        private const string k_testKcpClientArg = "-test_kcpclient";

        private string m_serverId = string.Empty;
        public override string ServerId => m_serverId;

        private ServerType m_serverType = ServerType.TestServer;
        public override ServerType ServerType => m_serverType;

        public override bool Init(string[] args)
        {
            if (!base.Init(args))
            {
                return false;
            }

            if (!InitLog())
            {
                return false;
            }

            if (!RegisterModules(args))
            {
                return false;
            }

            long timeNow = TimeHelper.TimeStampNowSeconds();
            m_serverId = $"{ServerType}-{timeNow}";
            Console.Title = "test server";
            m_running = true;
            Log.Info($"server start successfully, server id: test_server");
            return true;
        }

        protected override bool RegisterModules(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg == k_testMongoArg) AddModule<MongoTestModule>();
                if (arg == k_testRedisArg) AddModule<RedisTestModule>();
                if (arg == k_testClientArg) AddModule<ClientTestModule>();
                if (arg == k_testKcpClientArg) AddModule<KcpClientTestModule>();
            }

            return true;
        }
    }
}