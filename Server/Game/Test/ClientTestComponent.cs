using C2S;
using System.Net;
using Google.Protobuf;

namespace ZQ
{
    public class Client
    {
        private TcpService m_service;
        private ulong m_loginChannelId = 0;
        private ulong m_zoneChannelId = 0;
        private IPEndPoint m_loginEndPoint;
        private IPEndPoint m_zoneEndPoint;

        private TimerComponent m_timerComponent;
        private C2SMessageDispatcher m_messageDispatcher;

        private string m_profileId;

        public Client(IPEndPoint loginEndPoint, ulong loginChannelId, ulong zoneChannelId) 
        {
            m_loginChannelId = loginChannelId;
            m_zoneChannelId = zoneChannelId;

            m_loginEndPoint = loginEndPoint;
            m_service = new TcpService(m_loginEndPoint, true);
            m_service.ConnectSuccessCallback = OnConnectSuccess;
            m_service.CannotConnectServerCallback = OnCannotConnectServer;
            m_service.DataReceivedCallback = OnDataReceived;

            m_timerComponent = new TimerComponent();

            m_messageDispatcher = new C2SMessageDispatcher(m_service);
            m_messageDispatcher.RegisterMessage((ushort)C2S_MSG_ID.IdC2LLoginRes, typeof(C2LLoginRes));
            m_messageDispatcher.RegisterMessage((ushort)C2S_MSG_ID.IdC2ZLoginZoneRes, typeof(C2ZLoginZoneRes));

            m_service.CreateChannel(m_loginChannelId, m_loginEndPoint);
        }

        public void Update(long timeNow)
        {
            m_timerComponent.Update(timeNow);
            m_service.Update();
            m_messageDispatcher.Update(timeNow);
        }

        private async Task Login()
        {
            C2S.C2LLoginReq req = new C2S.C2LLoginReq();
            req.SdkUserId = m_loginChannelId.ToString();
            req.SdkToken = "dwa";
            req.SdkChannel = 1;
            IMessage resMessage = await m_messageDispatcher.SendAsync(req, m_loginChannelId, (ushort)C2S_MSG_ID.IdC2LLoginReq);
            if (resMessage == null)
            {
                Log.Error($"send packet failed, id:{m_loginChannelId}, messageId:{C2S_MSG_ID.IdC2LLoginReq}");
                return;
            }

            if (resMessage is not C2LLoginRes res)
            {
                Log.Error($"OnMessageLoginReq error: cannot conver message: {resMessage.GetType().FullName} to C2LLoginRes");
                return;
            }

            if (res.ErrorCode != C2S_ERROR_CODE.Success)
            {
                Log.Error($"login failed, error: {res.ErrorCode}");
                return;
            }

            Log.Info($"login to login server success, ip:{res.Ip}, port:{res.Port}, profileId:{res.ProfileId}");
            m_profileId = res.ProfileId;
            ConnectToZoneServer(res.Ip, (ushort)res.Port);
        }

        private async Task LoginToZone()
        {
            C2S.C2ZLoginZoneReq req = new C2S.C2ZLoginZoneReq();
            req.ProfileId = m_profileId;
            IMessage resMessage = await m_messageDispatcher.SendAsync(req, m_zoneChannelId, (ushort)C2S_MSG_ID.IdC2ZLoginZoneReq);
            if (resMessage == null)
            {
                Log.Error($"send packet failed, id:{m_zoneChannelId}, messageId:{C2S_MSG_ID.IdC2ZLoginZoneReq}");
                return;
            }

            if (resMessage is not C2ZLoginZoneRes res)
            {
                Log.Error($"OnMessageLoginReq error: cannot conver message: {resMessage.GetType().FullName} to C2ZLoginZoneRes");
                return;
            }

            if (res.ErrorCode != C2S_ERROR_CODE.Success)
            {
                Log.Error($"login failed, error: {res.ErrorCode}");
                return;
            }

            Log.Info($"login to zone server success, profileId:{res.BaseInfo.ProfileId}");
            m_timerComponent.AddTimer(10000, SendHeartbeat);
        }

        private bool ConnectToZoneServer(string ip, ushort port)
        {
            try
            {
                m_zoneEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                m_service.CreateChannel(m_zoneChannelId, m_zoneEndPoint);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"client init failed: ex:{ex}");
                return false;
            }
        }

        private void OnConnectSuccess(ulong channelId, IPEndPoint ipEndPoint)
        {
            if (m_loginChannelId == channelId)
            {
                Log.Info($"connect to login server success, id:{channelId}, ip:{ipEndPoint}");
                int num = 1;
                for (int i = 0; i < num; ++i)
                {
                    Login().FireAndForget();
                }
            }
            else if (m_zoneChannelId == channelId)
            {
                Log.Info($"connect to zone server success, id:{channelId}, ip:{ipEndPoint}");
                LoginToZone().FireAndForget();
            }
            else
            {
                Log.Info($"error channel, id:{channelId}, ip:{ipEndPoint}");
            }
        }

        private void OnCannotConnectServer(ulong channelId, IPEndPoint ipEndPoint, int error)
        {
            Log.Warning($"Client: cannot connect server, id:{channelId}, ip:{ipEndPoint}, error:{error}");
            if (channelId == m_loginChannelId)
            {
                //m_timerComponent.AddTimer(3000, TryToReconnectToLogin, null, false);
            }
            else if (channelId == m_zoneChannelId)
            {
                m_timerComponent.AddTimer(3000, TryToReconnectToZone, null, false);
            }
        }

        private void OnDataReceived(ulong channelId, MessageBuffer buffer)
        {
            m_messageDispatcher.DispatchMessage(channelId, buffer);
        }

        private void TryToReconnectToLogin(object arg)
        {
            m_service.CreateChannel(m_loginChannelId, m_loginEndPoint);
        }

        private void TryToReconnectToZone(object arg)
        {
            m_service.CreateChannel(m_zoneChannelId, m_zoneEndPoint);
        }

        private void SendHeartbeat(object arg)
        {
            if (!m_service.IsChannelOpen(m_zoneChannelId))
            {
                return;
            }

            C2S.C2SHeartBeatReq req = new C2S.C2SHeartBeatReq();
            req.State = 1;
            m_messageDispatcher.Send(req, m_zoneChannelId, (ushort)C2S_MSG_ID.IdC2SHeartbeatReq);
        }
    }

    public class ClientTestComponent : IComponent
    {
        private int m_clientNum = 1;
        private IPEndPoint m_loginEndPoint;
        private List<Client> m_clients;

        public ClientTestComponent(string loginIp, ushort port, int clientNum = 1)
        {
            m_loginEndPoint = new IPEndPoint(IPAddress.Parse(loginIp), port);
            m_clientNum = clientNum;
        }

        public bool Init()
        {
            m_clients = new List<Client>(m_clientNum);
            ulong id = 0;
            for (int i = 0; i < m_clientNum; ++i)
            {
                m_clients.Add(new Client(m_loginEndPoint, id++, id++));
            }

            return true;
        }

        public bool Update(long timeNow)
        {
            for (int i = 0; i < m_clients.Count; ++i)
            {
                m_clients[i].Update(timeNow);
            }

            return true;
        }

        public bool Shutdown()
        {
            return true;
        }
    }
}