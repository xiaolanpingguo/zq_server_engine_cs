using C2S;
using Google.Protobuf;
using System;
using System.Net;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ZQ.Redis;

namespace ZQ
{
    public class C2ZoneComponent : IComponent
    {
        private const int k_connectionTimeout = 60 * 10;
        private TcpService m_service;
        private C2SMessageDispatcher m_messageDispatcher;

        private IPEndPoint m_endPoint;

        private PlayerManager m_playerManager;

        public C2ZoneComponent(string ip, ushort port)
        {
            m_endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        public bool Init()
        {
            m_service = new TcpService(m_endPoint, false, k_connectionTimeout);
            m_service.AcceptCallback = OnClientAcccept;
            m_service.DataReceivedCallback = OnDataReceived;
            m_service.ClientDisconnectCallback = OnClientDisconnect;

            m_messageDispatcher = new C2SMessageDispatcher(m_service);
            m_messageDispatcher.RegisterMessage((ushort)C2S_MSG_ID.IdC2ZLoginZoneReq, typeof(C2ZLoginZoneReq), OnLoginZoneReq);

            var mongoComponent = Game.Instance.GetComponent<MongoComponent>();
            var redisComponent = Game.Instance.GetComponent<RedisComponent>();
            var timerComponent = Game.Instance.GetComponent<TimerComponent>();
            m_playerManager = new PlayerManager(m_service, m_messageDispatcher, mongoComponent, redisComponent, timerComponent);

            return true;
        }

        public bool Update(long timeNow)
        {
            m_service.Update();
            m_messageDispatcher.Update(timeNow);
            m_playerManager.Update(timeNow);
            return true;
        }

        public bool Shutdown()
        {
            return true;
        }

        public void Start()
        {
            Log.Info($"zone server has started, ip:{m_endPoint}");
            m_service.Start();
        }

        private void OnClientAcccept(ulong channelId, IPEndPoint ipEndPoint)
        {
            Log.Info($"a new client has connected to zone server, id:{channelId}, ip:{ipEndPoint}");
        }

        private void OnDataReceived(ulong channelId, MessageBuffer buffer)
        {
            m_messageDispatcher.DispatchMessage(channelId, buffer);
        }

        private void OnClientDisconnect(ulong channelId, IPEndPoint ipEndPoint, int error)
        {
            Log.Info($"a client has disconnected to zone server, id:{channelId}, ip:{ipEndPoint}, error:{error}");
            m_playerManager.OnClientDisconnect(channelId);
        }

        private async void OnLoginZoneReq(ushort messageId, ulong channelId, int rpcId, IMessage message)
        {
            if (message is not C2ZLoginZoneReq req)
            {
                Log.Error($"OnLoginZoneReq error: cannot conver message to C2SLoginZoneReq");
                return;
            }

            string profileId = req.ProfileId;

            C2ZLoginZoneRes res = new C2ZLoginZoneRes();
            C2S_ERROR_CODE errorCode = C2S_ERROR_CODE.GenerralError;

            do
            {
                if (string.IsNullOrEmpty(profileId))
                {
                    errorCode = C2S_ERROR_CODE.InvalidParameter;
                    break;
                }

                var endpoint = m_service.GetChannelEndpoint(channelId);
                string ip = endpoint == null ? "" : endpoint.Address.ToString();
                var ec = await m_playerManager.ProcessPlayerLogin(res, channelId, profileId, ip);
                if (ec != C2S_ERROR_CODE.Success)
                {
                    errorCode = ec;
                    break;
                }

                Log.Info($"a client has login to zone: profileId: {profileId}");
                errorCode = C2S_ERROR_CODE.Success;
            } while (false);

            res.ErrorCode = errorCode;
            m_messageDispatcher.Response(res, channelId, (ushort)C2S_MSG_ID.IdC2ZLoginZoneRes, rpcId);
        }
    }

}
