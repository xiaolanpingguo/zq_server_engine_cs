﻿using System.Net;
using ZQ.S2S;

namespace ZQ
{
    public class Lobby2MasterModule : IModule
    {
        private const int k_heartBeatInternal = 30 * 1000;

        private readonly LobbyServer m_server = null!;
        private TcpService m_service = null!;
        private S2SMessageDispatcher m_messageDispatcher = null!;
        private TimerModule m_timerComponent = null!;

        private IPEndPoint m_endPoint;
        private ulong m_channelId = 0;

        private ServerInfo m_myServerInfo = new ServerInfo();

        public Action? ConnectMasterSuccessCallback;

        public Lobby2MasterModule(LobbyServer server, string ip, ushort port)
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

            m_timerComponent = m_server.GetModule<TimerModule>();
            m_timerComponent.AddTimer(k_heartBeatInternal, ReportHeartbeat);

            m_myServerInfo.ServerId = m_server.ServerId;
            m_myServerInfo.ServerType = (short)ServerType.LobbyServer;
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

        private void OnConnectSuccess(ulong channelId, IPEndPoint ipEndPoint)
        {
            if (channelId != m_channelId)
            {
                Log.Error($"error channel, masterChannelId:{m_channelId}, id:{channelId}, ip:{ipEndPoint}");
                return;
            }

            Log.Info($"Zone2MasterComponent: connect:{ipEndPoint} succcess, id:{channelId}");
            ReportServerInfo().FireAndForget();
        }

        private void OnCannotConnectServer(ulong channelId, IPEndPoint ipEndPoint, int error)
        {
            Log.Warning($"Zone2MasterComponent: cannot connect server, id:{channelId}, ip:{ipEndPoint}, error:{error}");
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
            object? message = await m_messageDispatcher.SendAsync(req, m_channelId, (ushort)S2SMessageId.S2M_SERVER_REPORT_REQ);
            if (message == null || message is not S2M_ServerInfoReportRes res)
            {
                Log.Error($"Z2M ReportServerInfo error: cannot convert message to S2M_ServerInfoReportRes)");
                m_server.Stop();
                return;
            }

            if (res.ErrorCode != S2SErrorCode.SUCCESS)
            {
                Log.Error($"Z2M ReportServerInfo error: register server info to master failed:{res.ErrorCode}");
                m_server.Stop();
                return;
            }

            m_server.GetModule<C2LobbyModule>().Start();
        }

        private void ReportHeartbeat(object arg)
        {
            S2S_ServerHeartBeat req = new S2S_ServerHeartBeat();
            m_messageDispatcher.SendAsync(req, m_channelId, (ushort)S2SMessageId.HEARTBEAT).FireAndForget();
        }
    }

}
