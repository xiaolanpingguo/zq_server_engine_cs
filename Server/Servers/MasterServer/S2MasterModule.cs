using System.Net;
using ZQ.S2S;

namespace ZQ
{
    public class S2MasterModule : IModule
    {
        private readonly MasterServer m_server = null!;
        private TcpService m_service = null!;
        private S2SMessageDispatcher m_messageDispatcher = null!;

        private IPEndPoint m_endPoint;

        private Dictionary<string, ServerInfo> m_servers = new();

        public S2MasterModule(MasterServer server, string ip, ushort port)
        {
            m_server = server;
            m_endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        public bool Init()
        {
            m_service = new TcpService(m_endPoint, false);
            m_service.AcceptCallback = OnClientAcccept;
            m_service.DataReceivedCallback = OnDataReceived;
            m_service.ClientDisconnectCallback = OnClientDisconnect;

            m_messageDispatcher = new S2SMessageDispatcher(m_service);
            m_messageDispatcher.RegisterMessage((ushort)S2SMessageId.HEARTBEAT, typeof(S2S_ServerHeartBeat), OnServerHeartBeat);
            m_messageDispatcher.RegisterMessage((ushort)S2SMessageId.S2M_SERVER_REPORT_REQ, typeof(S2M_ServerInfoReportReq), OnServerInfoReportReq);
            m_messageDispatcher.RegisterMessage((ushort)S2SMessageId.L2M_ALL_ZONE_SERVERS_REQ, typeof(L2M_AllZoneServersReq), OnZoneServersReq);
            m_messageDispatcher.RegisterMessage((ushort)S2SMessageId.L2M_SUITABLE_ZONE_REQ, typeof(L2M_SuitableZoneReq), OnSuitableZoneReq);

            m_service.Start();
            return true;
        }

        public bool Update(long timeNow)
        {
            m_service.Update();
            m_messageDispatcher.Update(timeNow);
            return true;
        }

        public bool Shutdown()
        {
            return true;
        }

        private void OnClientAcccept(ulong channelId, IPEndPoint ipEndPoint)
        {
            Log.Info($"a new server has connected to master server, id:{channelId}, ip:{ipEndPoint}");
        }

        private void OnDataReceived(ulong channelId, MessageBuffer buffer)
        {
            m_messageDispatcher.DispatchMessage(channelId, buffer);
        }

        private void OnClientDisconnect(ulong channelId, IPEndPoint ipEndPoint, int error)
        {
            Log.Info($"a server has disconnected to master server, id:{channelId}, ip:{ipEndPoint}, error:{error}");
            RemoveServer(ipEndPoint);
        }

        private void OnServerInfoReportReq(ushort messageId, ulong channelId, int rpcId, object message)
        {
            if (message is not S2M_ServerInfoReportReq req)
            {
                Log.Error($"OnServerInfoReportReq error: cannot convert message to S2M_ServerInfoReportReq");
                return;
            }

            S2M_ServerInfoReportRes res = new S2M_ServerInfoReportRes();
            res.ErrorCode = S2SErrorCode.GENERRAL_ERROR;

            do
            {
                ServerInfo serverInfo = req.data;
                if (!IsServerInfoValid(serverInfo))
                {
                    break;
                }

                if (!TryAddServerInfo(serverInfo))
                {
                    break;
                }

                Log.Info($"a server has register, id:{serverInfo.ServerId}, ip:{serverInfo.IP},port:{serverInfo.Port},playerNum:{serverInfo.PlayerNum}");

                res.ErrorCode = S2SErrorCode.SUCCESS;
            } while (false);

            m_messageDispatcher.Response(res, channelId, (ushort)S2SMessageId.S2M_SERVER_REPORT_RES, rpcId);
        }

        private void OnServerHeartBeat(ushort messageId, ulong channelId, int rpcId, object message)
        {
            if (message is not S2S_ServerHeartBeat req)
            {
                Log.Error($"OnServerHeartBeat error: cannot convert message to S2S_ServerHeartBeat");
                return;
            }
        }

        private void OnSuitableZoneReq(ushort messageId, ulong channelId, int rpcId, object message)
        {
            if (message is not L2M_SuitableZoneReq req)
            {
                Log.Error($"OnSuitableZoneReq error: cannot convert message: to L2M_SuitableZoneReq");
                return;
            }

            L2M_SuitableZoneRes res = new L2M_SuitableZoneRes();
            res.ErrorCode = S2SErrorCode.GENERRAL_ERROR;

            do
            {
                ServerInfo? serverInfo = GetSuitableZoneServer();
                if (serverInfo == null)
                {
                    break;
                }

                res.data = serverInfo;
                res.ErrorCode = S2SErrorCode.SUCCESS;
            } while (false);


            m_messageDispatcher.Response(res, channelId, (ushort)S2S.S2SMessageId.L2M_SUITABLE_ZONE_RES, rpcId);
        }

        private void OnZoneServersReq(ushort messageId, ulong channelId, int rpcId, object message)
        {
            if (message is not L2M_AllZoneServersReq req)
            {
                Log.Error($"OnSuitableZoneReq error: cannot convert message: to L2M_AllZoneServersReq");
                return;
            }

            L2M_AllZoneServersRes res = new L2M_AllZoneServersRes();
            res.ZoneServers = new List<ServerInfo>();
            foreach (var kv in m_servers)
            {
                if (kv.Value.ServerType != (short)ServerType.LobbyServer)
                {
                    continue;
                }

                res.ZoneServers.Add(kv.Value);
            }
            res.ErrorCode = S2SErrorCode.SUCCESS;
            m_messageDispatcher.Response(res, channelId, (ushort)S2S.S2SMessageId.L2M_ALL_ZONE_SERVERS_RES, rpcId);
        }

        private bool TryAddServerInfo(ServerInfo serverInfo)
        {
            if (m_servers.TryGetValue(serverInfo.ServerId, out var _))
            {
                return false;
            }

            m_servers[serverInfo.ServerId] = serverInfo;
            return true;
        }

        private bool RemoveServer(IPEndPoint ipEndPoint)
        {
            if (ipEndPoint == null)
            {
                return false;
            }

            string ip = ipEndPoint.Address.ToString();
            int port = ipEndPoint.Port;

            foreach (var kv in m_servers)
            {
                ServerInfo info = kv.Value;
                if (info.IP == ip && info.Port == port)
                {
                    m_servers.Remove(kv.Key);
                    return true;
                }
            }

            return false;
        }

        private ServerInfo? GetServerInfo(string serverId)
        {
            if (m_servers.TryGetValue(serverId, out ServerInfo? serverInfo))
            {
                return serverInfo;
            }

            return null;
        }

        private ServerInfo? GetSuitableZoneServer()
        {   
            if (m_servers.Count == 0)     
            {
                return null;
            }

            int num = int.MaxValue;
            string serverId = string.Empty;
            foreach(var kv in m_servers)  
            {
                if (kv.Value.ServerType != (short)ServerType.LobbyServer)
                {
                    continue;
                }

                if (kv.Value.PlayerNum < num)
                {
                    num = kv.Value.PlayerNum;
                    serverId = kv.Key;
                }
            }

            if (!m_servers.ContainsKey(serverId))
            {
                return null;
            }

            return m_servers[serverId];
        }

        private bool IsServerInfoValid(ServerInfo serverInfo)
        {
            if (serverInfo == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(serverInfo.ServerId) || string.IsNullOrEmpty(serverInfo.IP))
            {
                return false;
            }

            return true;
        }
    }

}
