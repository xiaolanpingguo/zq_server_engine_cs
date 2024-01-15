using C2S;
using System.Net;
using ZQ.S2S;
using ZQ.Mongo;
using ZQ.Redis;
using Google.Protobuf;
using MemoryPack;
using System.Net.Sockets;
using System.Threading.Channels;

namespace ZQ
{
    public class C2LoginModule : IModule
    {
        private const int k_connectionTimeout = 60 * 10;

        private LoginServer m_server = null!;
        private TcpService m_service = null!;
        private C2SMessageDispatcher m_messageDispatcher = null!;
        private Login2MasterModule m_login2MasterComponent = null!;
        private RedisModule m_redisComponent = null!;

        private IPEndPoint m_endPoint;

        public C2LoginModule(LoginServer server, string ip, ushort port)
        {
            m_server = server;
            m_endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        public bool Init() 
        {
            //m_login2MasterComponent = m_server.GetModule<Login2MasterModule>();
            m_redisComponent = m_server.GetModule<RedisModule>();

            m_service = new TcpService(m_endPoint, false, k_connectionTimeout);
            m_service.AcceptCallback = OnClientAcccept;
            m_service.DataReceivedCallback = OnDataReceived;
            m_service.ClientDisconnectCallback = OnClientDisconnect;

            m_messageDispatcher = new C2SMessageDispatcher(m_service);
            m_messageDispatcher.RegisterMessage((ushort)C2S_MSG_ID.IdC2LLoginReq, typeof(C2LLoginReq), OnLoginReq);
            return true;
        }

        public bool Update(long timeNow) 
        {
            m_service?.Update();
            m_messageDispatcher?.Update(timeNow);
            return true;
        }

        public bool Shutdown() 
        {
            return true; 
        }

        public void Start()
        {
            Log.Info($"login server has started, ip:{m_endPoint}");
            m_service?.Start();
        }

        private void OnClientAcccept(ulong channelId, IPEndPoint ipEndPoint)
        {
            Log.Info($"a client has connected to login server, id:{channelId}, ip:{ipEndPoint}");
        }

        private void OnDataReceived(ulong channelId, MessageBuffer buffer)
        {
            m_messageDispatcher?.DispatchMessage(channelId, buffer);
        }

        private void OnClientDisconnect(ulong channelId, IPEndPoint ipEndPoint, int error)
        {
            Log.Info($"a client has disconnected to login server, id:{channelId}, ip:{ipEndPoint}, error:{error}");
        }

        private async void OnLoginReq(ushort messageId, ulong channelId, int rpcId, IMessage? message)
        {
            if (message == null || message is not C2LLoginReq req)
            {
                Log.Error($"OnMessageLoginReq error: cannot conver message to C2SLoginReq");
                return;
            }

            string sdkUserId = req.SdkUserId;
            string sdkToken = req.SdkToken;
            int sdkChannel = req.SdkChannel;
            Log.Info($"a client has login: userId: {sdkUserId}, token: {sdkToken},channel: {sdkChannel}");


            C2LLoginRes res = new C2LLoginRes();
            C2S_ERROR_CODE errorCode = C2S_ERROR_CODE.GenerralError;

            do
            {
                if (string.IsNullOrEmpty(sdkUserId) || string.IsNullOrEmpty(sdkToken))
                {
                    errorCode = C2S_ERROR_CODE.InvalidParameter;
                    break;
                }

                ServerInfo? suitableZoneServer = m_login2MasterComponent.SuitableZoneServerInfo;
                if (suitableZoneServer == null)
                {
                    errorCode = C2S_ERROR_CODE.ServerNotReady;
                    break;
                }

                var dbResult = await FindAndSaveUser(sdkUserId, sdkToken, sdkChannel);
                if (dbResult.Item1 != C2S_ERROR_CODE.Success || dbResult.Item2 == null)
                {
                    errorCode = dbResult.Item1;
                    break;
                }
                DBAccount account = dbResult.Item2;

                RedisPlayerSession? session = await FindSession(account.ProfileId);
                if (session == null)
                {
                    RedisPlayerSession newSession = new RedisPlayerSession();
                    newSession.SDKChannelId = sdkChannel;
                    newSession.SDKUserId = sdkUserId;
                    newSession.SDKToken = sdkToken;
                    newSession.ZoneServerId = suitableZoneServer.ServerId;
                    newSession.ZoneIP = suitableZoneServer.IP;
                    newSession.ZonePort = suitableZoneServer.Port;
                    newSession.ProfileId = account.ProfileId;
                    bool success = await CreateSession(newSession);
                    if (!success)
                    {
                        errorCode = C2S_ERROR_CODE.GenerralError;
                        break;
                    }

                    // set response
                    res.Ip = suitableZoneServer.IP;
                    res.Port = suitableZoneServer.Port;
                    res.ProfileId = account.ProfileId;
                }
                else
                {
                    // user is online
                    bool zoneServerAvailable = m_login2MasterComponent.CheckZoneServerAvailable(session.ZoneServerId);
                    if (!zoneServerAvailable)
                    {
                        // the zone server are not available, it means the zone server of this session has creashed
                        // and all session on this server has not be deleted, so we need to set a new zone server
                        string oldServerId = session.ZoneServerId;

                        session.SDKChannelId = sdkChannel;
                        session.SDKUserId = sdkUserId;
                        session.SDKToken = sdkToken;
                        session.ZoneServerId = suitableZoneServer.ServerId;
                        session.ZoneIP = suitableZoneServer.IP;
                        session.ZonePort = suitableZoneServer.Port;
                        session.ProfileId = account.ProfileId;
                        var oldSession = await OverwriteSession(session);
                        if (oldSession == null)
                        {
                            errorCode = C2S_ERROR_CODE.GenerralError;
                            break;
                        }

                        // very unlikely to fail,if failed, the client may have logged in to different login servers at the same time
                        if (oldSession.ZoneServerId != oldServerId)
                        {
                            errorCode = C2S_ERROR_CODE.GenerralError;
                            break;
                        }
                    }

                    res.Ip = session.ZoneIP;
                    res.Port = session.ZonePort;
                    res.ProfileId = account.ProfileId;
                }

                errorCode = C2S_ERROR_CODE.Success;
            } while (false);

            res.ErrorCode = errorCode;
            m_messageDispatcher.Response(res, channelId, (ushort)C2S_MSG_ID.IdC2LLoginRes, rpcId);
            m_service.DelayClose(channelId);
        }

        private async Task<(C2S_ERROR_CODE, DBAccount?)> FindAndSaveUser(string userId, string token, int channel)
        {
            MongoModule mongoComponent = m_server.GetModule<MongoModule>();
            var findResult = await mongoComponent.Find<DBAccount>(MongoDefines.DBName, MongoDefines.ColAccount, MongoDefines.ColAccountKeySDKUserID, userId);
            if (findResult == null || !findResult.Success) 
            {
                string error = findResult != null ? findResult.ErrorDesc : "";
                Log.Error($"FindAndSaveUser: find error: userId:{userId},channel:{channel}, error:{error}");
                return (C2S_ERROR_CODE.ServerInternalError, null);
            }

            if (findResult.Result.Count > 1)
            {
                Log.Error($"FindAndSaveUser: user account data error!, there are {findResult.Result.Count}, userId:{userId},hannel:{channel}");
                return (C2S_ERROR_CODE.ServerInternalError, null);
            }

            // we got a new user, save this account data
            if (findResult.Result.Count == 0)
            {
                DBAccount account = new DBAccount();
                account.SDKUserId = userId;
                account.SDKChannelId = channel;
                account.ProfileId = Guid.NewGuid().ToString();
                var saveResult = await mongoComponent.Save<DBAccount>(MongoDefines.DBName, MongoDefines.ColAccount, MongoDefines.ColAccountKeySDKUserID, userId, account);
                if (saveResult == null || !saveResult.Success || saveResult.Result == null || saveResult.Result.Count == 0)
                {
                    Log.Error($"FindAndSaveUser: save account data error!, there are {findResult.Result.Count}, userId:{userId},hannel:{channel}");
                    return (C2S_ERROR_CODE.ServerInternalError, null);
                }

                return (C2S_ERROR_CODE.Success, saveResult.Result[0]);
            }
            else
            {
                return (C2S_ERROR_CODE.Success, findResult.Result[0]);
            }
        }

        private async Task<RedisPlayerSession?> FindSession(string profileId)
        {
            string key = RedisDefines.GetPlayerSessionKey(profileId);

            byte[]? redisResult = (byte[]?)await m_redisComponent.GET(key);
            if (redisResult != null)
            {
                RedisPlayerSession onlineSession = MemoryPackHelper.Deserialize<RedisPlayerSession>(redisResult);
                if (onlineSession == null)
                {
                    return null;
                }

                return onlineSession;
            }

            return null;
        }

        private async Task<bool> CreateSession(RedisPlayerSession sessionInfo)
        {
            string key = RedisDefines.GetPlayerSessionKey(sessionInfo.ProfileId);

            // new session
            byte[] bytes = MemoryPackHelper.Serialize(sessionInfo);
            bool success = await m_redisComponent.SET(key, bytes, true);
            if (!success)
            {
                // very unlikely to fail, because we have check it before by Redis GET
                // if failed, the client may have logged in to different login servers at the same time
                Log.Warning($"client try to logged in to different login servers at the same time: userId: {sessionInfo.SDKUserId}, profileId:{sessionInfo.ProfileId}");
                return false;
            }

            return true;
        }

        private async Task<RedisPlayerSession?> OverwriteSession(RedisPlayerSession sessionInfo)
        {
            string key = RedisDefines.GetPlayerSessionKey(sessionInfo.ProfileId);

            // new session
            byte[] bytes = MemoryPackHelper.Serialize(sessionInfo);
            byte[]? redisResult = (byte[]?)await m_redisComponent.GETSET(key, bytes);
            if (redisResult != null)
            {
                RedisPlayerSession session = MemoryPackHelper.Deserialize<RedisPlayerSession>(redisResult);
                if (session == null)
                {
                    return null;
                }

                return session;
            }

            return null;
        }
    }

}
