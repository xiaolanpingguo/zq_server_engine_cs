using System.Net;
using System.Threading.Channels;
using ZQ.S2S;

namespace ZQ
{
    public class C2DedicatedComponent : IComponent
    {
        private KcpService m_service;
        private C2DSMessageDispatcher m_messageDispatcher;

        private IPEndPoint m_endPoint;

        private Dictionary<ulong, AuthorityPlayer> m_players= new();
        private Dictionary<ulong, Room> m_playerRoom = new();
        private List<Room> m_rooms = new List<Room>();

        public C2DedicatedComponent(string ip, ushort port)
        {
            m_endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        public bool Init()
        {
            m_service = new KcpService(m_endPoint);
            m_service.AcceptCallback = OnClientAcccept;
            m_service.DataReceivedCallback = OnDataReceived;
            m_service.ClientDisconnectCallback = OnClientDisconnect;

            m_messageDispatcher = new C2DSMessageDispatcher(m_service);
            m_messageDispatcher.RegisterMessage((ushort)C2DSMessageId.C2DS_PingReq, typeof(C2DS_PingReq), OnClientPing);
            m_messageDispatcher.RegisterMessage((ushort)C2DSMessageId.C2DS_LoadingProgress, typeof(C2DS_LoadingProgressReq), OnLoadingProgress);
            m_messageDispatcher.RegisterMessage((ushort)C2DSMessageId.C2DS_FrameMessage, typeof(C2DS_FrameMessage), OnFrameMessage);

            Log.Info($"dedicated server has started, ip:{m_endPoint}");
            return true;
        }

        public bool Update(long timeNow)
        {
            m_service.Update();
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

        private void OnClientAcccept(ulong channelId, IPEndPoint ipEndPoint)
        {
            if (m_players.ContainsKey(channelId))
            {
                Log.Error($"[dedicated server]OnClientAcccept, player has exist! id:{channelId}, ip:{ipEndPoint}");
                return;
            }

            Log.Info($"a client has connected to dedicated server, id:{channelId}, ip:{ipEndPoint}");
            AuthorityPlayer player = new AuthorityPlayer();
            player.ChannelId = channelId;
            player.ProfileId = channelId.ToString();
            m_players[channelId] = player;
        }

        private void OnDataReceived(ulong channelId, MessageBuffer buffer)
        {
            m_messageDispatcher.DispatchMessage(channelId, buffer);
        }

        private void OnClientDisconnect(ulong channelId, IPEndPoint ipEndPoint, int error)
        {
            Log.Info($"a server has disconnected to dedicated server, id:{channelId}, ip:{ipEndPoint}, error:{error}");
        }

        private void OnClientPing(ushort messageId, ulong channelId, int rpcId, object message)
        {
            if (message is not C2DS_PingReq req)
            {
                Log.Error($"OnServerHeartBeat error: cannot convert message to S2S_ServerHeartBeat");
                return;
            }

        }

        private void OnLoadingProgress(ushort messageId, ulong channelId, int rpcId, object message)
        {
            if (message is not C2DS_LoadingProgressReq req)
            {
                Log.Error($"OnLoadingProgress error: cannot convert message to C2DS_LoadingProgressReq");
                return;
            }

            AuthorityPlayer player = GetPlayer(channelId);
            if (player == null)
            {
                return;
            }

            bool startGame = true;
            player.Progress = req.Progress;
            foreach (var kv in m_players)
            {
                Log.Info($"OnLoadingProgress, player channelId:{channelId}, Progress{player.Progress}");
                if (player.Progress != 100)
                {
                    startGame = false;
                }
            }

            if (startGame) 
            {
                Room room = CreateRoom(1);
                foreach (var kv in m_players)
                {
                    room.AddPlayer(kv.Value);
                }
                m_rooms.Add(room);
                if (!m_playerRoom.ContainsKey(channelId))
                {
                    m_playerRoom[channelId] = room;
                }
            }
        }

        private void OnFrameMessage(ushort messageId, ulong channelId, int rpcId, object message)
        {
            if (message is not C2DS_FrameMessage req)
            {
                Log.Error($"OnLoadingProgress error: cannot convert message to C2DS_FrameMessage");
                return;
            }

            if (!m_playerRoom.TryGetValue(channelId, out var room)) 
            {
                return;
            }

            room.OnClientFrameMessage(channelId, rpcId, req);
        }

        private AuthorityPlayer GetPlayer(ulong channelId)
        {
            if (m_players.TryGetValue(channelId, out var playerEntity))
            {
                return playerEntity;
            }

            return null;
        }

        private Room CreateRoom(int playerCount)
        {
            return new Room(m_messageDispatcher, playerCount);
        }
    }

}
