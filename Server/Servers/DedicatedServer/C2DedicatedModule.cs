using C2DS;
using Google.Protobuf;
using kcp2k;
using System.Net;


namespace ZQ
{
    public class C2DedicatedModule : IModule
    {
        const int k_roomPlayersCount = 1;

        private readonly DedicatedServer m_server = null!;
        private KcpServer m_network = null!;
        private C2DSMessageDispatcher m_messageDispatcher = null!;

        private IPEndPoint m_endPoint;

        int m_roomId = 0;
        private Dictionary<int, Room> m_playerRoom = new();
        private Dictionary<int, Room> m_rooms = new();

        public C2DedicatedModule(DedicatedServer server, string ip, ushort port)
        {
            m_server = server;
            m_endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        public bool Init()
        {
            kcp2k.Log.Info = ZQ.Log.Info;
            kcp2k.Log.Warning = ZQ.Log.Warning;
            kcp2k.Log.Error = ZQ.Log.Error;
            KcpConfig config = new KcpConfig(
               NoDelay: true,
               DualMode: false,
               Interval: 1,
               Timeout: 10000,
               SendWindowSize: Kcp.WND_SND * 1000,
               ReceiveWindowSize: Kcp.WND_RCV * 1000,
               CongestionWindow: false,
               MaxRetransmits: Kcp.DEADLINK * 2
            );

            m_network = new KcpServer(OnClientAcccept, OnDataReceived, OnClientDisconnect, OnServerError, config);
            m_network.Start((ushort)m_endPoint.Port);

            m_messageDispatcher = new C2DSMessageDispatcher(m_network);
            m_messageDispatcher.RegisterMessage((ushort)C2DS_MSG_ID.IdC2DsJoinServerReq, typeof(C2DSJoinServerReq), OnJoinServer);

            Log.Info($"dedicated server has started, ip:{m_endPoint}");
            return true;
        }

        public bool Update(long timeNow)
        {
            m_network.Tick();
            m_messageDispatcher.Update(timeNow);
            foreach(var kv in m_rooms)
            {
                kv.Value.Update();
            }
            return true;
        }

        public bool Shutdown()
        {
            return true;
        }

        public bool RegisterMessage(ushort messageId, Type type)
        {
            if (!m_messageDispatcher.IsMessageRegistered(messageId))
            {
                return m_messageDispatcher.RegisterMessage(messageId, type, OnRoomMessage);
            }

            return false;
        }

        private void OnClientAcccept(int connectionId)
        {
            Log.Info($"a client has connected to dedicated server, id:{connectionId}");
        }

        private void OnDataReceived(int connectionId, ArraySegment<byte> data, KcpChannel channel)
        {
            m_messageDispatcher.DispatchMessage(connectionId, data, channel);
        }

        private void OnClientDisconnect(int connectionId)
        {
            IPEndPoint ipEndPoint = m_network.GetClientEndPoint(connectionId);
            if (!m_playerRoom.TryGetValue(connectionId, out var room))
            {
                return;
            }

            room.OnPlayersDisconnected(connectionId);
            m_playerRoom.Remove(connectionId);
            if (room.CurrentPlayerCount == 0)
            {
                Log.Info($"all player has leave from room,  room will be deleted, room id:{room.RoomId}");
                DeleteRoom(room.RoomId);
            }
        }

        private void OnServerError(int connectionId, ErrorCode ec, string reason)
        {
            IPEndPoint ipEndPoint = m_network.GetClientEndPoint(connectionId);
            Log.Info($"a server error has occurred on dedicated server, id:{connectionId}, ec:{ipEndPoint}, error:{ec}, reason:{reason}");
            m_network.Disconnect(connectionId);
        }

        private void OnRoomMessage(ushort messageId, int connectionId, int rpcId, IMessage? message)
        {
            if (m_playerRoom.TryGetValue(connectionId, out var room)) 
            {
                room.OnMessage(messageId, connectionId, rpcId, message);
            }
        }

        private Room CreateRoom(int id, int playerCount)
        {
            return new Room(id, this, m_messageDispatcher, playerCount);
        }

        private void DeleteRoom(int id)
        {
            m_rooms.Remove(id);
        }

        private void OnJoinServer(ushort messageId, int connectionId, int rpcId, IMessage? message)
        {
            if (message is not C2DSJoinServerReq req)
            {
                Log.Error($"OnJoinServer error: cannot convert message to C2DSJoinServerReq");
                return;
            }

            if (m_playerRoom.ContainsKey(connectionId))
            {
                return;
            }

            string profileId = req.ProfileId;
            Room room = null!;
            bool found = false;
            foreach (var kv in m_rooms)
            {
                int count = kv.Value.PlayersCount();
                if (count < k_roomPlayersCount)
                {
                    room = kv.Value;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                room = CreateRoom(m_roomId++, k_roomPlayersCount);
                m_rooms[room.RoomId] = room;
            }

            Log.Info($"a client has joined the server,connectionId:{connectionId}, profileId:{profileId}.");

            Player player = new Player();
            player.ConnectionId = connectionId;
            player.ProfileId = profileId;
            room.AddPlayer(player);
            m_playerRoom[connectionId] = room;

            C2DS.C2DSJoinServerRes res = new C2DS.C2DSJoinServerRes();
            res.ErrorCode = C2DS_ERROR_CODE.Success;
            m_messageDispatcher.Response(res, connectionId, (ushort)C2DS_MSG_ID.IdC2DsJoinServerRes, rpcId);
        }
    }

}
