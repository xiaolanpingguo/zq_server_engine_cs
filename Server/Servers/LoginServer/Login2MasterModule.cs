using System.Net;
using ZQ.S2S;

namespace ZQ
{
    public class Login2MasterModule : IModule
    {
        private int k_heartBeatInternal = 30 * 1000;
        private int k_requestZoneServersTimeInterval = 5 * 1000;

        private readonly LoginServer m_server = null!;
        private TcpService m_service = null!;
        private S2SMessageDispatcher m_messageDispatcher = null!;
        private TimerModule m_timerComponent = null!;

        private IPEndPoint m_endPoint;
        private ulong m_channelId = 0;

        private ServerInfo m_myServerInfo = new ServerInfo();

        public ServerInfo? SuitableZoneServerInfo = null;
        public List<ServerInfo> m_allZoneServers = new List<ServerInfo>();

        public Login2MasterModule(LoginServer server, string ip, ushort port)
        {
            m_server = server;
            m_endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        public bool Init()
        {
            m_service = new TcpService(m_endPoint, true);
            m_service.ConnectSuccessCallback = OnConnectSuccess;
            m_service.CannotConnectServerCallback = OnCannotConnectServer;
            m_service.DataReceivedCallback = OnDataReceived;

            m_messageDispatcher = new S2SMessageDispatcher(m_service);
            m_messageDispatcher.RegisterMessage((ushort)S2SMessageId.S2M_SERVER_REPORT_RES, typeof(S2M_ServerInfoReportRes));
            m_messageDispatcher.RegisterMessage((ushort)S2SMessageId.L2M_ALL_ZONE_SERVERS_RES, typeof(L2M_AllZoneServersRes));

            m_timerComponent = m_server.GetModule<TimerModule>();
            m_timerComponent.AddTimer(k_heartBeatInternal, ReportHeartbeat);

            m_myServerInfo.ServerId = m_server.ServerId;
            m_myServerInfo.ServerType = (short)ServerType.LoginServer;
            m_myServerInfo.PlayerNum = 0;
            m_myServerInfo.IP = m_server.Config.externalIp;
            m_myServerInfo.Port = m_server.Config.externalPort;

            m_service.CreateChannel(m_channelId, m_endPoint);
            return true;
        }

        public bool Update(long timeNow)
        {
            m_service.Update();
            return true;
        }

        public bool Shutdown()
        {
            return true;
        }

        public bool CheckZoneServerAvailable(string zoneServerId)
        {
            if (m_allZoneServers == null || m_allZoneServers.Count == 0)
            {
                return false;
            }

            foreach(ServerInfo server in m_allZoneServers)
            {
                if (zoneServerId == server.ServerId)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnConnectSuccess(ulong channelId, IPEndPoint ipEndPoint)
        {
            if (channelId != m_channelId)
            {
                Log.Error($"Login2MasterComponent: error channel, masterChannelId:{m_channelId}, id:{channelId}, ip:{ipEndPoint}");
                return;
            }

            Log.Info($"Login2MasterComponent: connect:{ipEndPoint} succcess, id:{channelId}");
            ReportServerInfo().FireAndForget();
        }

        private void OnCannotConnectServer(ulong channelId, IPEndPoint ipEndPoint, int error)
        {
            Log.Warning($"Login2MasterComponent: cannot connect server, id:{channelId}, ip:{ipEndPoint}, error:{error}");
            if (channelId == m_channelId)
            {
                m_timerComponent.AddTimer(3000, TryToReconnect, null, false);
            }
        }

        private void OnDataReceived(ulong channelId, MessageBuffer buffer)
        {
            m_messageDispatcher.DispatchMessage(channelId, buffer);
        }

        private void TryToReconnect(object arg)
        {
            m_service.CreateChannel(m_channelId, m_endPoint);
        }

        private async Task ReportServerInfo()
        {
            S2M_ServerInfoReportReq req = new S2M_ServerInfoReportReq();
            req.data = m_myServerInfo;
            object? message = await m_messageDispatcher.SendAsync(req, m_channelId, (ushort)S2S.S2SMessageId.S2M_SERVER_REPORT_REQ);
            if (message == null || message is not S2M_ServerInfoReportRes res)
            {
                Log.Error($"L2M ReportServerInfo error: cannot convert message to S2M_ServerInfoReportRes)");
                m_server.Stop();
                return;
            }

            if (res.ErrorCode != S2SErrorCode.SUCCESS)
            {
                Log.Error($"L2M OnConnectSuccess error: register server info to master failed:{res.ErrorCode}");
                m_server.Stop();
                return;
            }

            await GetAllZoneServersAsync();
            m_server.GetModule<C2LoginModule>().Start();
        }

        private void ReportHeartbeat(object arg)
        {
            S2S_ServerHeartBeat req = new S2S_ServerHeartBeat();
            m_messageDispatcher.SendAsync(req, m_channelId, (ushort)S2SMessageId.HEARTBEAT).FireAndForget();
        }

        private void GetAllZoneServers(object arg)
        {
            GetAllZoneServersAsync().FireAndForget();
        }

        private async Task GetAllZoneServersAsync()
        {
            L2M_AllZoneServersReq req = new L2M_AllZoneServersReq();
            object? message = await m_messageDispatcher.SendAsync(req, m_channelId, (ushort)S2S.S2SMessageId.L2M_ALL_ZONE_SERVERS_REQ);
            if (message == null || message is not L2M_AllZoneServersRes res)
            {
                Log.Error($"L2M GetSuitableZoneServerAsync error: cannot convert messageL2M_SuitableZoneRes");
                m_server.Stop();
                return;
            }

            if (res.ErrorCode != S2SErrorCode.SUCCESS || res.ZoneServers == null)
            {
                Log.Error($"L2M GetAllZoneServersAsync error:{res.ErrorCode}");
                m_server.Stop();
                return;
            }

            m_allZoneServers = res.ZoneServers;
            if (m_allZoneServers == null || m_allZoneServers.Count == 0)
            {
                SuitableZoneServerInfo = null;
                return;
            }

            int index = 0;
            int num = int.MaxValue;
            for (int i = 0; i < m_allZoneServers.Count; ++i)
            {
                int playerNum = m_allZoneServers[i].PlayerNum;
                if (playerNum < num)
                {
                    index = i;
                }
            }
            SuitableZoneServerInfo = m_allZoneServers[index];

            m_timerComponent.AddTimer(k_requestZoneServersTimeInterval, GetAllZoneServers, null, false);
        }
    }

}
