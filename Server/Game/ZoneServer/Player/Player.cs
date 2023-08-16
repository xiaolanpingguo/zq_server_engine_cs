using C2S;
using Google.Protobuf;
using System.Xml.Linq;
using ZQ.Mongo;

namespace ZQ
{
    public class Player
    {
        public enum StatusType
        {
            Loggingin,
            Online,
            Disconnect,
        }
        public class AccountInfo
        {
            public int SDKChannelId { get; set; }
            public string SDKUserId { get; set; }
            public string ProfileId { get; set; }
        }

        private readonly PlayerManager m_playerManager;
        private Dictionary<Type, IPlayerModule> m_playerModules = new Dictionary<Type, IPlayerModule>();
        private Dictionary<ushort, Action<ushort, int, IMessage>> m_messageHandlers = new();

        public bool DataDirty = false;

        public StatusType Status { get; private set; }
        public long DisconnectTime = 0;

        public ulong ChannelId { get; set; }
        public readonly AccountInfo AccountData;
        public string ProfileId => AccountData.ProfileId;
        public string IP;
        public DBPlayerData PlayerData { get; private set; }

        public Player(PlayerManager playerManager, ulong channelId, AccountInfo accoutData, DBPlayerData playerData)
        {
            m_playerManager = playerManager;
            ChannelId = channelId;
            PlayerData = playerData;
            AccountData = accoutData;
            PlayerData.ProfileId = AccountData.ProfileId;
            Status = StatusType.Online;

            RegisterMessage(C2S_MSG_ID.IdC2SHeartbeatReq, typeof(C2SHeartBeatRes), OnHeartbeatReq);

            AddModule<PlayerBaseInfoModule>(this);
        }

        public bool Update(long timeNow)
        {
            foreach(var kv in m_playerModules)
            {
                IPlayerModule module = kv.Value;
                module.Update(timeNow);
                if (module.DataDirty) 
                {
                    DataDirty = true;
                }
            }

            return true;
        }

        public bool LoadFromDB(DBPlayerData dbData)
        {
            try
            {
                foreach (var kv in m_playerModules)
                {
                    if (!kv.Value.Init(dbData))
                    {
                        Log.Error($"player load db data error: profileId:{ProfileId}, module:{kv.Key.Name}");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"player load db data error: profileId:{ProfileId}, e:{e}");
                return false;
            }

            return true;
        }

        public void OnLoginSuccess(C2ZLoginZoneRes res)
        {
            foreach (var kv in m_playerModules)
            {
                kv.Value.OnLoginSuccess(res);
            }
        }

        public bool RegisterMessage(C2S_MSG_ID messageId, Type type, Action<ushort, int, IMessage> handler)
        {
            if (m_playerManager.RegisterPlayerMessage(messageId, type))
            {
                m_messageHandlers[(ushort)messageId] = handler;
            }

            return true;
        }

        public bool Response(ushort messageId, int rpcId, IMessage res)
        {
            return m_playerManager.Response(ChannelId, messageId, rpcId, res);
        }

        public bool SendMessage(ushort messageId, IMessage packet)
        {
            return m_playerManager.SendMessage(ChannelId, messageId, packet);
        }

        public bool OnMessage(ushort messageId, int rpcId, IMessage packet)
        {
            if (m_messageHandlers.TryGetValue(messageId, out var handler))
            {
                handler?.Invoke(messageId, rpcId, packet);
            }

            return true;
        }

        public void SetStatus(StatusType status)
        {
            if (Status == status)
            {
                return;
            }

            Status = status;
            if (Status == StatusType.Disconnect)
            {
                DisconnectTime = TimeHelper.TimeStampNowMs();
            }
        }

        private void AddModule<T>(params object[] p) where T : IPlayerModule
        {
            Type type = typeof(T);
            T module = (T)Activator.CreateInstance(type, p);
            if (m_playerModules.ContainsKey(type))
            {
                Log.Error($"AddModule failed, the component has exist., type:{type}.");
                return;
            }

            m_playerModules[type] = module;
        }

        private T GetModule<T>() where T : IPlayerModule
        {
            Type type = typeof(T);
            if (m_playerModules.TryGetValue(type, out var v))
            {
                return (T)v;
            }

            throw new Exception($"can't find player module:{type.Name}");
        }

        private void OnHeartbeatReq(ushort messageId, int rpcId, IMessage message)
        {
            Log.Info($"Player:OnHeartbeatReq, id:{ProfileId}");
        }
    }
}
