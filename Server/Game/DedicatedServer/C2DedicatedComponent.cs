using C2S;
using System.Net;
using System.Threading.Channels;
using ZQ.S2S;

namespace ZQ
{
    public class C2DedicatedComponent : IComponent
    {
        const int k_roomPlayersCount = 1;
        private KcpService m_service;
        private C2DSMessageDispatcher m_messageDispatcher;

        private IPEndPoint m_endPoint;

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

        public bool RegisterRoomMessage(ushort messageId, Type type)
        {
            if (!m_messageDispatcher.IsMessageRegistered(messageId))
            {
                return m_messageDispatcher.RegisterMessage(messageId, type, OnRoomMessage);
            }

            return false;
        }

        private void OnClientAcccept(ulong channelId, IPEndPoint ipEndPoint)
        {
            Log.Info($"a client has connected to dedicated server, id:{channelId}, ip:{ipEndPoint}");

            if (m_playerRoom.ContainsKey(channelId))
            {
                return;
            }

            Room room = null;
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

            AuthorityPlayer player = new AuthorityPlayer();
            player.ChannelId = channelId;
            player.ProfileId = channelId.ToString();
            room.AddPlayer(player);
            m_playerRoom[channelId] = room;
        }

        private void OnDataReceived(ulong channelId, MessageBuffer buffer)
        {
            m_messageDispatcher.DispatchMessage(channelId, buffer);
        }

        private void OnClientDisconnect(ulong channelId, IPEndPoint ipEndPoint, int error)
        {
            Log.Info($"a server has disconnected to dedicated server, id:{channelId}, ip:{ipEndPoint}, error:{error}");
        }

        private void OnRoomMessage(ushort messageId, ulong channelId, int rpcId, object message)
        {
            if (m_playerRoom.TryGetValue(channelId, out var room)) 
            {
                room.OnMessage(messageId, channelId, rpcId, message);
            }
        }

        private Room CreateRoom(int playerCount)
        {
            return new Room(this, m_messageDispatcher, playerCount);
        }
    }

}
