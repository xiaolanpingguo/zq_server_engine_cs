using C2S;
using System.Net;
using ZQ.S2S;

namespace ZQ
{
    public class KcpClient
    {
        private KcpService m_service;
        private ulong m_channelId = 0;
        private IPEndPoint m_endPoint;

        private TimerComponent m_timerComponent;
        private C2DSMessageDispatcher m_messageDispatcher;

        public KcpClient(IPEndPoint endPoint, ulong channelId) 
        {
            m_channelId = channelId;
            m_endPoint = endPoint;
            m_service = new KcpService();
            m_service.ConnectSuccessCallback = OnConnectSuccess;
            m_service.CannotConnectServerCallback = OnCannotConnectServer;
            m_service.DataReceivedCallback = OnDataReceived;

            m_timerComponent = new TimerComponent();

            m_messageDispatcher = new C2DSMessageDispatcher(m_service);
            //m_messageDispatcher.RegisterMessage((ushort)C2S_MSG_ID.IdC2LLoginRes, typeof(C2LLoginRes));

            m_service.CreateChannel(m_channelId, m_endPoint);
        }

        public void Update(long timeNow)
        {
            m_timerComponent.Update(timeNow);
            m_service.Update();
            m_messageDispatcher.Update(timeNow);
        }

        private void OnConnectSuccess(uint localConn, uint remoteConn, IPEndPoint ipEndPoint)
        {
            if (localConn == m_channelId)
            {
                Log.Info($"connect to dedicated server success, localConn:{localConn}, remoteConn:{remoteConn}, ip:{ipEndPoint}");
                m_timerComponent.AddTimer(3000, PING, null, false);
            }
            else
            {
                Log.Error($"error channel, id:{localConn}, ip:{ipEndPoint}");
            }
        }

        private void OnCannotConnectServer(ulong channelId, IPEndPoint ipEndPoint, int error)
        {
            Log.Warning($"Client: cannot connect server, id:{channelId}, ip:{ipEndPoint}, error:{error}");
            if (channelId == m_channelId)
            {
                m_timerComponent.AddTimer(3000, TryToReconnectToServer, null, false);
            }
        }

        private void OnDataReceived(ulong channelId, MessageBuffer buffer)
        {
            m_messageDispatcher.DispatchMessage(channelId, buffer);
        }


        private void TryToReconnectToServer(object arg)
        {
            m_service.CreateChannel(m_channelId, m_endPoint);
        }

        private void PING(object arg)
        {
            if (!m_service.IsChannelOpen(m_channelId))
            {
                return;
            }

            C2DS_PingReq req = new C2DS_PingReq();
            m_messageDispatcher.Send(req, m_channelId, (ushort)C2DSMessageId.C2DS_PingReq);
        }
    }

    public class KcpClientTestComponent : IComponent
    {
        private int m_clientNum = 1;
        private IPEndPoint m_serverEndPoint;
        private List<KcpClient> m_clients;

        public KcpClientTestComponent(string loginIp, ushort port, int clientNum = 1)
        {
            m_serverEndPoint = new IPEndPoint(IPAddress.Parse(loginIp), port);
            m_clientNum = clientNum;
        }

        public bool Init()
        {
            m_clients = new List<KcpClient>(m_clientNum);
            ulong id = 0;
            for (int i = 0; i < m_clientNum; ++i)
            {
                m_clients.Add(new KcpClient(m_serverEndPoint, id++));
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