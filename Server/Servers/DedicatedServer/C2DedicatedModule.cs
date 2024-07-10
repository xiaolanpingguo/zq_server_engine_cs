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

        private Dictionary<int, Room> m_playerRoom = new();
        private List<Room> m_rooms = new List<Room>();

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
                // force NoDelay and minimum interval.
               // this way UpdateSeveralTimes() doesn't need to wait very long and
               // tests run a lot faster.
               NoDelay: true,
               // not all platforms support DualMode.
               // run tests without it so they work on all platforms.
               DualMode: false,
               Interval: 1, // 1ms so at interval code at least runs.
               Timeout: 10000,

               // large window sizes so large messages are flushed with very few
               // update calls. otherwise tests take too long.
               SendWindowSize: Kcp.WND_SND * 1000,
               ReceiveWindowSize: Kcp.WND_RCV * 1000,

               // congestion window _heavily_ restricts send/recv window sizes
               // sending a max sized message would require thousands of updates.
               CongestionWindow: false,

               // maximum retransmit attempts until dead_link detected
               // default * 2 to check if configuration works
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
            foreach(Room room in m_rooms)
            {
                room.Update();
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
            Log.Info($"a client has disconnected to dedicated server, id:{connectionId}, ip:{ipEndPoint}");
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

        private Room CreateRoom(int playerCount)
        {
            return new Room(this, m_messageDispatcher, playerCount);
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
            foreach (var v in m_rooms)
            {
                int count = v.PlayersCount();
                if (count < k_roomPlayersCount)
                {
                    room = v;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                room = CreateRoom(k_roomPlayersCount);
                m_rooms.Add(room);
            }

            Log.Info($"a client has joined the server,connectionId:{connectionId}, profileId:{profileId}.");

            Player player = new Player();
            player.ConnectionId = connectionId;
            player.ProfileId = profileId;
            room.AddPlayer(player);
            m_playerRoom[connectionId] = room;

            C2DS.C2DSJoinServerRes res = new C2DS.C2DSJoinServerRes();
            res.ErrorCode = C2DS_ERROR_CODE.Success;
            res.PlayerId = 0;
            m_messageDispatcher.Response(res, connectionId, (ushort)C2DS_MSG_ID.IdC2DsJoinServerRes, rpcId);
        }
    }

}
