using C2S;
using MemoryPack;
using System.Net;
using Google.Protobuf;
using kcp2k;
using C2DS;
using System.Net.NetworkInformation;


namespace ZQ
{
    public class TestKcpClient
    {
        private KcpClient m_network;
        private ulong m_channelId = 0;
        private IPEndPoint m_endPoint;

        private TimerModule m_timerComponent;
        //private C2DSMessageDispatcher m_messageDispatcher;

        public TestKcpClient(IPEndPoint endPoint, ulong channelId)
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

            m_network = new KcpClient(OnConnected, OnDataReceived, OnDisconnect, OnError, config);
            m_network.Connect(endPoint.Address.ToString(), (ushort)endPoint.Port);
            //m_messageDispatcher = new C2DSMessageDispatcher(m_network);
           // m_messageDispatcher.RegisterMessage((ushort)C2DS_MSG_ID.IdC2DsPingRes, typeof(C2DSPingRes), OnMsgPingRes);
        }

        public void Update(long timeNow)
        {
            m_timerComponent.Update(timeNow);
            m_network.Tick();
            //m_messageDispatcher.Update(timeNow);
        }
        private void OnConnected()
        {
            Log.Info($"a client has connected to dedicated server.");
        }

        private void OnDataReceived(ArraySegment<byte> data, KcpChannel channel)
        {
            //m_messageDispatcher.DispatchMessage(0, data, channel);
        }

        private void OnDisconnect()
        {
            Log.Warning($"client has disconnected to server.");
        }

        private void OnError(ErrorCode ec, string reason)
        {
            Log.Info($"a server error has occurred, error:{ec}, reason:{reason}");
        }

        private void OnMsgPingRes(ushort messageId, int rpcId, IMessage message)
        {
            if (message == null || message is not C2DSPingRes res)
            {
                Log.Error($"OnMsgPingRes error: cannot convert message to C2DSPingRes");
                return;
            }

            long clientTime = res.ClientTime;
            //long serverTime = res.ServerTime;
            long now = TimeHelper.TimeStampNowMs();
            int ping = (int)(now - clientTime);
            Log.Info($"Ping:{ping}");
        }

        private void PING(object arg)
        {
            //C2DS.C2DSPingReq req = new C2DS.C2DSPingReq();
            //req.ProfileId = "1234";
            //req.ClientTime = TimeHelper.TimeStampNowMs();
            //Send(req, (ushort)C2DS.C2DS_MSG_ID.IdC2DsPingReq);
        }
    }

    public class KcpClientTestModule : IModule
    {
        private int m_clientNum = 1;
        private IPEndPoint m_serverEndPoint;
        private List<TestKcpClient> m_clients = null!;

        public KcpClientTestModule(string loginIp, ushort port, int clientNum = 1)
        {
            m_serverEndPoint = new IPEndPoint(IPAddress.Parse(loginIp), port);
            m_clientNum = clientNum;
        }

        public bool Init()
        {
            m_clients = new List<TestKcpClient>(m_clientNum);
            ulong id = 0;
            for (int i = 0; i < m_clientNum; ++i)
            {
                m_clients.Add(new TestKcpClient(m_serverEndPoint, id++));
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