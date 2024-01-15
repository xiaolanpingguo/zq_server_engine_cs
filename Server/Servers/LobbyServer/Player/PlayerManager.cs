using C2S;
using Google.Protobuf;
using ZQ.Mongo;
using ZQ.Redis;

namespace ZQ
{
    public class PlayerManager
    {
        private const int k_deletePlayerInterval = 60 * 1000;
        private const int k_saveToDBInterval = 10 * 1000;
        private const int k_reportPlayersNumInterval = 5 * 1000;

        private long m_lastSaveDBCheckTime = 0;

        private readonly LobbyServer m_server = null!;
        private readonly TcpService m_service = null!;
        private readonly C2SMessageDispatcher m_messageDispatcher = null!;
        private readonly MongoModule m_mongoComponent = null!;
        private readonly RedisModule m_redisComponent = null!;
        private readonly TimerModule m_timerComponent = null!;

        private Dictionary<string, Player> m_players = new();
        private Dictionary<ulong, Player> m_channelPlayers = new();

        public PlayerManager(LobbyServer server, TcpService service, C2SMessageDispatcher messageDispatcher, 
            MongoModule mongoComponent, RedisModule redisComponent, TimerModule timerComponent)
        {
            m_server = server;
            m_service = service;
            m_messageDispatcher = messageDispatcher;
            m_mongoComponent = mongoComponent;
            m_redisComponent = redisComponent;
            m_timerComponent = timerComponent;
            m_timerComponent.AddTimer(k_reportPlayersNumInterval, ReportPlayersNum);
        }

        public bool Update(long timeNow)
        {
            foreach (var kv in m_players) 
            { 
                Player player = kv.Value;
                if (player.Status == Player.StatusType.Disconnect)
                {
                    RegularDeletePlayer(timeNow, player);
                }
                else
                {
                    player.Update(timeNow);
                    RegularSave(timeNow, player);
                }
            }

            return true;
        }

        public async Task<C2S_ERROR_CODE> ProcessPlayerLogin(C2ZLoginZoneRes res, ulong channelId, string profileId, string ip)
        {
            Player? player = GetPlayer(profileId);
            if (player != null)
            {
                // the player is online, it means other player are logingin this account
                // so kickout the old player
                if (GetPlayerByChannelId(player.ChannelId) != null)
                {
                    KickoutPlayer(player);
                }
                // the player has disconnect and waitting to delete data
                else
                {
                }

                // set status here in case the redis player session delete on RegularDeletePlayer()
                player.SetStatus(Player.StatusType.Online);
                player.ChannelId = channelId;
                m_channelPlayers[channelId] = player;
                return C2S_ERROR_CODE.Success;
            }

            // there is no session, player has not login to LoginServer
            var sessionResult = await FindPlayerSesession(profileId);
            if (sessionResult.Item1 != C2S_ERROR_CODE.Success || sessionResult.Item2 == null)
            {
                return sessionResult.Item1;
            }

            // case1: player login with a wrong zone server
            // case2: zone server has creashed, so all player seesion on this zone server has not be deleted
            // in this case, we need to palyer re-login from LoginServer 
            RedisPlayerSession session = sessionResult.Item2;
            if (session.ZoneServerId != m_server.ServerId)
            {
                return C2S_ERROR_CODE.LoginSessionHasExpired;
            }

            if (string.IsNullOrEmpty(session.SDKUserId) || string.IsNullOrEmpty(profileId))
            {
                return C2S_ERROR_CODE.GenerralError;
            }

            // in case the player has disconnect while getting redis data
            if (!m_service.IsChannelOpen(channelId))
            {
                return C2S_ERROR_CODE.GenerralError;
            }

            // get player data from db
            DBPlayerData? playerData = await GetPlayerDBData(profileId);

            // in case the player has disconnect while getting db data
            if (!m_service.IsChannelOpen(channelId))
            {
                return C2S_ERROR_CODE.GenerralError;
            }

            Player.AccountInfo accout = new Player.AccountInfo();
            accout.SDKChannelId = session.SDKChannelId;
            accout.SDKUserId = session.SDKUserId;
            accout.ProfileId = profileId;
            if (playerData == null) 
            {
                // new user
                playerData = new DBPlayerData();
                player = new Player(this, channelId, accout, playerData);
                player.DataDirty = true;
            }
            else
            {
                player = new Player(this, channelId, accout, playerData);
            }

            if (!player.LoadFromDB(playerData))
            {
                Log.Error($"OnPlayerLogin: player load db data error!, profileId:{profileId}");
                return C2S_ERROR_CODE.GenerralError;
            }

            player.OnLoginSuccess(res);

            player.IP = ip;
            m_players[profileId] = player;
            m_channelPlayers[channelId] = player;
            return C2S_ERROR_CODE.Success;
        }


        public async Task<bool> SavePlayerData(Player player)
        {
            var result = await m_mongoComponent.Save<DBPlayerData>(MongoDefines.DBName, MongoDefines.ColPlayers, MongoDefines.ColAccountKeyProfileId, player.ProfileId, player.PlayerData);
            if (result == null || !result.Success)
            {
                Log.Error($"SavePlayerData:save  player data error!,  profileId:{player.ProfileId}");
                return false;
            }

            return true;
        }

        public bool RegisterPlayerMessage(C2S_MSG_ID messageId, Type type)
        {
            if (!m_messageDispatcher.IsMessageRegistered((ushort)messageId))
            {
                return m_messageDispatcher.RegisterMessage((ushort)messageId, type, OnPlayerMessage);
            }

            return true;
        }

        public bool Response(ulong channelId, ushort messageId, int rpcId, IMessage res)
        {
            return m_messageDispatcher.Response(res, channelId, messageId, rpcId);
        }

        public bool SendMessage(ulong channelId, ushort messageId, IMessage packet)
        {
            return m_messageDispatcher.Send(packet, channelId, messageId);
        }

        public void OnClientDisconnect(ulong channelId)
        {
            Player? player = GetPlayerByChannelId(channelId);
            if (player != null)
            {
                player.SetStatus(Player.StatusType.Disconnect);
                m_channelPlayers.Remove(channelId);
                SavePlayerData(player).FireAndForget();
            }
        }

        public void DeletePlayer(Player player)
        {
            m_players.Remove(player.ProfileId);
            m_channelPlayers.Remove(player.ChannelId);
            m_redisComponent.DELETE(RedisDefines.GetPlayerSessionKey(player.ProfileId)).FireAndForget();
        }

        public void ForceClosePlayer(string profileId, int reason)
        {
            Player? player = GetPlayer(profileId);
            if (player == null) 
            {
                Log.Error($"ForceClosePlayer error, can't find profileId:{profileId}, reason:{reason}");
                return;
            }

            m_service.Close(player.ChannelId);
            DeletePlayer(player);
        }

        private Player? GetPlayer(string profileId)
        {
            if (m_players.TryGetValue(profileId, out var player))
            {
                return player;
            }

            return null;
        }

        private Player? GetPlayerByChannelId(ulong channelId)
        {
            if (m_channelPlayers.TryGetValue(channelId, out var player))
            {
                return player;
            }

            return null;
        }

        private async Task<(C2S_ERROR_CODE, RedisPlayerSession?)> FindPlayerSesession(string profileId)
        {
            string key = RedisDefines.GetPlayerSessionKey(profileId);

            // player is online
            byte[]? redisResult = (byte[]?)await m_redisComponent.GET(key);
            if (redisResult != null)
            {
                RedisPlayerSession onlineSession = MemoryPackHelper.Deserialize<RedisPlayerSession>(redisResult);
                if (onlineSession == null)
                {
                    return (C2S_ERROR_CODE.LoginSessionHasExpired, null);
                }

                return (C2S_ERROR_CODE.Success, onlineSession);
            }

            return (C2S_ERROR_CODE.LoginSessionHasExpired, null);
        }

        private async Task<bool> UpdatePlayerSesession(RedisPlayerSession sessionInfo)
        {
            string key = RedisDefines.GetPlayerSessionKey(sessionInfo.ProfileId);
            byte[] bytes = MemoryPackHelper.Serialize(sessionInfo);
            bool success = await m_redisComponent.SET(key, bytes, false);
            if (!success)
            {
                Log.Error($"C2Z:UpdatePlayerSesession: update player session failed,: userId: {sessionInfo.SDKUserId}, profileId:{sessionInfo.ProfileId}");
                return false;
            }

            return true;
        }

        private void RegularDeletePlayer(long timeNow, Player player)
        {
            if (player.Status != Player.StatusType.Disconnect)
            {
                return;
            }

            if (timeNow - player.DisconnectTime > k_deletePlayerInterval)
            {
                DeletePlayer(player);
                return;
            }
        }

        private void RegularSave(long timeNow, Player player)
        {
            if (timeNow - m_lastSaveDBCheckTime < k_saveToDBInterval)
            {
                return;
            }
            m_lastSaveDBCheckTime = timeNow;

            if (!player.DataDirty || player.Status != Player.StatusType.Online)
            {
                return;
            }

            SavePlayerData(player).FireAndForget();
            player.DataDirty = false;
        }

        private void StaySessionAlive(long timeNow, Player player)
        {
            if (timeNow - player.DisconnectTime > k_deletePlayerInterval)
            {
                DeletePlayer(player);
                return;
            }
        }

        private void ReportPlayersNum(object arg)
        {
            Log.Info($"current player num:{m_players.Count}");
        }

        private void KickoutPlayer(Player player)
        {
            // disconnect old player
            Z2CKickoutReq req = new Z2CKickoutReq();
            req.ErrorCode = C2S_ERROR_CODE.Success;
            m_messageDispatcher.Send(req, player.ChannelId, (ushort)C2S_MSG_ID.IdZ2CKickoutPlayerReq);
            m_service.DelayClose(player.ChannelId);

            // remove old channelId
            m_channelPlayers.Remove(player.ChannelId);
        }

        private async Task<DBPlayerData?> GetPlayerDBData(string profileId)
        {
            MongoModule mongoComponent = m_server.GetModule<MongoModule>();
            var findResult = await mongoComponent.Find<DBPlayerData>(MongoDefines.DBName, MongoDefines.ColPlayers, MongoDefines.ColAccountKeyProfileId, profileId);
            if (findResult == null || !findResult.Success)
            {
                return null;
            }

            if (findResult.Result.Count == 0)
            {
                return null;
            }

            if (findResult.Result.Count > 1)
            {
                Log.Error($"GetPlayerDBData: player data error!, there are {findResult.Result.Count} results! profileId:{profileId}");
                return null;
            }

            return findResult.Result[0];
        }

        private void OnPlayerMessage(ushort messageId, ulong channelId, int rpcId, IMessage? message)
        {
            Player? player = GetPlayerByChannelId(channelId);
            if (player == null)
            {
                return;
            }

            player.OnMessage(messageId, rpcId, message);
        }
    }
}
