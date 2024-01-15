using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZQ
{
    public static class KcpProtocalType
    {
        public const byte SYN = 1;
        public const byte ACK = 2;
        public const byte FIN = 3;
        public const byte MSG = 4;
        public const byte RouterReconnectSYN = 5;
        public const byte RouterReconnectACK = 6;
        public const byte RouterSYN = 7;
        public const byte RouterACK = 8;
    }

    public sealed class KcpService
    {
        public const int k_receiveBufferSize = 1024 * 1024 * 4;
        public const int k_sendBufferSize = 1024 * 1024 * 4;
        public const int k_connectTimeoutTime = 10 * 1000;
        public int m_sendMaxWaitSize = 0;

        private const int k_maxMemoryBufferSize = 1024 * 8;

        private readonly Queue<MessageBuffer> m_memoryBufferPool = new();

        // KService创建的时间
        private readonly long m_startTime;

        private uint m_acceptIdGenerator = 0;

        // 保存所有的channel
        private readonly Dictionary<ulong, KcpChannel> m_localConnChannels = new Dictionary<ulong, KcpChannel>();
        private readonly Dictionary<ulong, KcpChannel> m_waitAcceptChannels = new Dictionary<ulong, KcpChannel>();

        private readonly byte[] m_cache = new byte[2048];

        private EndPoint m_ipEndPoint = new IPEndPointNonAlloc(IPAddress.Any, 0);

        private readonly List<ulong> m_cacheIds = new List<ulong>();


        // 下帧要更新的channel
        private readonly HashSet<ulong> m_updateIds = new HashSet<ulong>();

        // 下次时间更新的channel
        private readonly NativeCollection.MultiMap<long, ulong> m_timeId = new();
        private readonly List<long> m_timeOutTime = new List<long>();

        // 记录最小时间，不用每次都去MultiMap取第一个值
        private long m_minTime;

        public Socket Socket { get; private set; }

        // 当前时间 - KService创建的时间, 线程安全
        public uint TimeNow
        {
            get
            {
                return (uint)((DateTime.UtcNow.Ticks - m_startTime) / 10000);
            }
        }

        // for server
        public Action<ulong, IPEndPoint> AcceptCallback;
        public Action<ulong, IPEndPoint, int> ClientDisconnectCallback;

        // for client
        public Action<uint, uint, IPEndPoint> ConnectSuccessCallback;
        public Action<ulong, IPEndPoint, int> CannotConnectServerCallback;

        // for client/server
        public Action<ulong, MessageBuffer> DataReceivedCallback;

        public KcpService(IPEndPoint ipEndPoint, int sendMaxWaitSize = 1024 * 16)
        {
            m_sendMaxWaitSize = sendMaxWaitSize;
            m_startTime = DateTime.UtcNow.Ticks;
            Socket = new Socket(ipEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Socket.SendBufferSize = k_sendBufferSize;
                Socket.ReceiveBufferSize = k_receiveBufferSize;
            }

            Socket.Bind(ipEndPoint);
            SetSioUdpConnReset(Socket);
        }

        public KcpService(int sendMaxWaitSize = 1024 * 16)
        {
            m_sendMaxWaitSize = sendMaxWaitSize;
            m_startTime = DateTime.UtcNow.Ticks;
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            SetSioUdpConnReset(Socket);
        }

        public void Send(ulong channelId, MessageBuffer buffer)
        {
            KcpChannel channel = GetChannel(channelId);
            if (channel == null)
            {
                return;
            }

            channel.Send(buffer);
        }

        public void Update()
        {
            uint timeNow = this.TimeNow;
            TimerOut(timeNow);
            CheckWaitAcceptChannel(timeNow);
            Recv();
            UpdateChannel(timeNow);
        }

        public static void SetSioUdpConnReset(Socket socket)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const uint IOC_IN = 0x80000000;
            const uint IOC_VENDOR = 0x18000000;
            const int SIO_UDP_CONNRESET = unchecked((int)(IOC_IN | IOC_VENDOR | 12));

            socket.IOControl(SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
        }

        public MessageBuffer Fetch(int size = 0)
        {
            if (size > k_maxMemoryBufferSize)
            {
                return new MessageBuffer(size);
            }

            if (size < k_maxMemoryBufferSize)
            {
                size = k_maxMemoryBufferSize;
            }

            if (m_memoryBufferPool.Count == 0)
            {
                return new MessageBuffer(size);
            }

            return m_memoryBufferPool.Dequeue();
        }

        public void Recycle(MessageBuffer memoryBuffer)
        {
            if (memoryBuffer.GetBufferSize() > 1024)
            {
                return;
            }

            if (m_memoryBufferPool.Count > 10) // 这里不需要太大，其实Kcp跟Tcp,这里1就足够了
            {
                return;
            }

            memoryBuffer.Reset();
            m_memoryBufferPool.Enqueue(memoryBuffer);
        }

        public void Stop()
        {
            if (Socket == null)
            {
                return;
            }

            foreach (ulong channelId in m_localConnChannels.Keys.ToArray())
            {
                Close(channelId);
            }

            Socket.Close();
            Socket = null;
        }

        public (uint, uint) GetChannelConn(ulong channelId)
        {
            KcpChannel kChannel = GetChannel(channelId);
            if (kChannel == null)
            {
                throw new Exception($"GetChannelConn conn not found KChannel! {channelId}");
            }

            return (kChannel.LocalConn, kChannel.RemoteConn);
        }

        public bool IsChannelOpen(ulong channelId)
        {
            KcpChannel channel = GetChannel(channelId);
            if (channel == null)
            {
                return false;
            }

            return channel.IsConnected;
        }

        public void ChangeAddress(ulong channelId, IPEndPoint newIPEndPoint)
        {
            KcpChannel kChannel = GetChannel(channelId);
            if (kChannel == null)
            {
                return;
            }
            kChannel.RemoteAddress = newIPEndPoint;
        }

        public void OnError(ulong channelId, int error)
        {
            Close(channelId, error);
        }

        public void OnConnectSuccess(uint localConn, uint remoteConn, IPEndPoint remoteAddress)
        {
            ConnectSuccessCallback?.Invoke(localConn, remoteConn, remoteAddress);
        }

        public void OnDataReceived(ulong channelId, MessageBuffer buffer)
        {
            DataReceivedCallback?.Invoke(channelId, buffer);
        }

        private void Recv()
        {
            if (Socket == null)
            {
                return;
            }

            while (Socket != null && Socket.Available > 0)
            {
                int messageLength = ReceiveFromNonAlloc(Socket, m_cache, ref m_ipEndPoint);

                // 长度小于1，不是正常的消息
                if (messageLength < 1)
                {
                    continue;
                }

                // accept
                byte flag = m_cache[0];

                // conn从100开始，如果为1，2，3则是特殊包
                uint remoteConn = 0;
                uint localConn = 0;

                try
                {
                    KcpChannel kChannel = null;
                    switch (flag)
                    {
                        case KcpProtocalType.RouterReconnectSYN:
                            {
                                // 长度!=5，不是RouterReconnectSYN消息
                                if (messageLength != 13)
                                {
                                    break;
                                }

                                string realAddress = null;
                                remoteConn = BitConverter.ToUInt32(m_cache, 1);
                                localConn = BitConverter.ToUInt32(m_cache, 5);
                                uint connectId = BitConverter.ToUInt32(m_cache, 9);

                                m_localConnChannels.TryGetValue(localConn, out kChannel);
                                if (kChannel == null)
                                {
                                    Log.Warning($"kchannel reconnect not found channel: {localConn} {remoteConn} {realAddress}");
                                    break;
                                }

                                // 这里必须校验localConn，客户端重连，localConn一定是一样的
                                if (localConn != kChannel.LocalConn)
                                {
                                    Log.Warning($"kchannel reconnect localconn error: {localConn} {remoteConn} {realAddress} {kChannel.LocalConn}");
                                    break;
                                }

                                if (remoteConn != kChannel.RemoteConn)
                                {
                                    Log.Warning($"kchannel reconnect remoteconn error: {localConn} {remoteConn} {realAddress} {kChannel.RemoteConn}");
                                    break;
                                }

                                // 重连的时候router地址变化, 这个不能放到msg中，必须经过严格的验证才能切换
                                if (!m_ipEndPoint.Equals(kChannel.RemoteAddress))
                                {
                                    kChannel.RemoteAddress = m_ipEndPoint.Clone();
                                }

                                try
                                {
                                    byte[] buffer = m_cache;
                                    buffer.WriteTo(0, KcpProtocalType.RouterReconnectACK);
                                    buffer.WriteTo(1, kChannel.LocalConn);
                                    buffer.WriteTo(5, kChannel.RemoteConn);
                                    buffer.WriteTo(9, connectId);
                                    Socket.SendTo(buffer, 0, 13, SocketFlags.None, m_ipEndPoint);
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e.Message);
                                    kChannel.OnError(NetworkError.ERR_KcpSocketCantSend);
                                }

                                break;
                            }
                        case KcpProtocalType.SYN: // accept
                            {
                                if (messageLength < 9)
                                {
                                    break;
                                }

                                string realAddress = null;
                                if (messageLength > 9)
                                {
                                    realAddress = m_cache.ToStr(9, messageLength - 9);
                                }

                                remoteConn = BitConverter.ToUInt32(m_cache, 1);
                                localConn = BitConverter.ToUInt32(m_cache, 5);

                                m_waitAcceptChannels.TryGetValue(remoteConn, out kChannel);
                                if (kChannel == null)
                                {
                                    // accept的localConn不能与connect的localConn冲突，所以设置为一个大的数
                                    // localConn被人猜出来问题不大，因为remoteConn是随机的,第三方并不知道
                                    localConn = m_acceptIdGenerator++;
                                    // 已存在同样的localConn，则不处理，等待下次sync
                                    if (m_localConnChannels.ContainsKey(localConn))
                                    {
                                        break;
                                    }

                                    kChannel = new KcpChannel(localConn, remoteConn, m_ipEndPoint.Clone(), this);
                                    kChannel.RealAddress = realAddress;
                                    m_waitAcceptChannels.Add(kChannel.RemoteConn, kChannel); // 连接上了或者超时后会删除
                                    m_localConnChannels.Add(kChannel.LocalConn, kChannel);

                                    IPEndPoint realEndPoint = kChannel.RealAddress == null ? kChannel.RemoteAddress : ToIPEndPoint(kChannel.RealAddress);
                                    AcceptCallback(kChannel.ChannelId, realEndPoint);
                                }
                                if (kChannel.RemoteConn != remoteConn)
                                {
                                    break;
                                }

                                // 地址跟上次的不一致则跳过
                                if (kChannel.RealAddress != realAddress)
                                {
                                    Log.Error($"kchannel syn address diff: {kChannel.ChannelId} {kChannel.RealAddress} {realAddress}");
                                    break;
                                }

                                try
                                {
                                    byte[] buffer = m_cache;
                                    buffer.WriteTo(0, KcpProtocalType.ACK);
                                    buffer.WriteTo(1, kChannel.LocalConn);
                                    buffer.WriteTo(5, kChannel.RemoteConn);
                                    Log.Info($"kservice syn: {kChannel.ChannelId} {remoteConn} {localConn}");

                                    Socket.SendTo(buffer, 0, 9, SocketFlags.None, kChannel.RemoteAddress);
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e.Message);
                                    kChannel.OnError(NetworkError.ERR_KcpSocketCantSend);
                                }

                                break;
                            }
                        case KcpProtocalType.ACK: // connect返回
                            // 长度!=9，不是connect消息
                            if (messageLength != 9)
                            {
                                break;
                            }

                            remoteConn = BitConverter.ToUInt32(m_cache, 1);
                            localConn = BitConverter.ToUInt32(m_cache, 5);
                            kChannel = GetChannel(localConn);
                            if (kChannel != null)
                            {
                                Log.Info($"kservice ack: {localConn} {remoteConn}");
                                kChannel.RemoteConn = remoteConn;
                                kChannel.HandleConnnect();
                            }

                            break;
                        case KcpProtocalType.FIN: // 断开
                            // 长度!=13，不是DisConnect消息
                            if (messageLength != 13)
                            {
                                break;
                            }

                            remoteConn = BitConverter.ToUInt32(m_cache, 1);
                            localConn = BitConverter.ToUInt32(m_cache, 5);
                            int error = BitConverter.ToInt32(m_cache, 9);

                            // 处理chanel
                            kChannel = GetChannel(localConn);
                            if (kChannel == null)
                            {
                                break;
                            }

                            // 校验remoteConn，防止第三方攻击
                            if (kChannel.RemoteConn != remoteConn)
                            {
                                break;
                            }

                            Log.Info($"kservice recv fin: {localConn} {remoteConn} {error}");
                            kChannel.OnError(NetworkError.ERR_PeerDisconnect);

                            break;
                        case KcpProtocalType.MSG: // 断开
                            // 长度<9，不是Msg消息
                            if (messageLength < 9)
                            {
                                break;
                            }

                            // 处理chanel
                            remoteConn = BitConverter.ToUInt32(m_cache, 1);
                            localConn = BitConverter.ToUInt32(m_cache, 5);

                            kChannel = GetChannel(localConn);
                            if (kChannel == null)
                            {
                                // 通知对方断开
                                Disconnect(localConn, remoteConn, NetworkError.ERR_KcpNotFoundChannel, m_ipEndPoint, 1);
                                break;
                            }

                            // 校验remoteConn，防止第三方攻击
                            if (kChannel.RemoteConn != remoteConn)
                            {
                                break;
                            }

                            // 对方发来msg，说明kchannel连接完成
                            if (!kChannel.IsConnected)
                            {
                                kChannel.IsConnected = true;
                                m_waitAcceptChannels.Remove(kChannel.RemoteConn);
                            }

                            kChannel.HandleRecv(m_cache, 9, messageLength - 9);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"kservice error: {flag} {remoteConn} {localConn}\n{e}");
                }
            }
        }

        public KcpChannel GetChannel(ulong id)
        {
            if(m_localConnChannels.TryGetValue(id, out var channel))
            {
                return channel;
            }

            return null;
        }

        public void CreateChannel(ulong channelId, IPEndPoint endPoint)
        {
            if (m_localConnChannels.TryGetValue(channelId, out KcpChannel kChannel))
            {
                return;
            }

            try
            {
                // 低32bit是localConn
                uint localConn = (uint)channelId;
                kChannel = new KcpChannel(localConn, endPoint, this);
                m_localConnChannels.Add(kChannel.LocalConn, kChannel);
            }
            catch (Exception e)
            {
                Log.Error($"kservice get error: {channelId}\n{e}");
            }
        }

        public void Close(ulong id, int error = 0)
        {
            if (!m_localConnChannels.TryGetValue(id, out KcpChannel channel))
            {
                return;
            }

            Log.Info($"kcpservice channel closed: {id} {channel.LocalConn} {channel.RemoteConn} {error}");

            channel.ChannelId = 0;
            m_localConnChannels.Remove(channel.LocalConn);
            if (m_waitAcceptChannels.TryGetValue(channel.RemoteConn, out KcpChannel waitChannel))
            {
                if (waitChannel.LocalConn == channel.LocalConn)
                {
                    m_waitAcceptChannels.Remove(channel.RemoteConn);
                }
            }

            Disconnect(channel.LocalConn, channel.RemoteConn, error, channel.RemoteAddress, 3);
        }

        public void Disconnect(uint localConn, uint remoteConn, int error, EndPoint address, int times)
        {
            try
            {
                if (Socket == null)
                {
                    return;
                }

                byte[] buffer = m_cache;
                buffer.WriteTo(0, KcpProtocalType.FIN);
                buffer.WriteTo(1, localConn);
                buffer.WriteTo(5, remoteConn);
                buffer.WriteTo(9, (uint)error);
                for (int i = 0; i < times; ++i)
                {
                    Socket.SendTo(buffer, 0, 13, SocketFlags.None, address);
                }
            }
            catch (Exception e)
            {
                Log.Error($"Disconnect error {localConn} {remoteConn} {error} {address} {e}");
            }

            Log.Info($"channel send fin: {localConn} {remoteConn} {address} {error}");
        }

        private void CheckWaitAcceptChannel(uint timeNow)
        {
            m_cacheIds.Clear();
            foreach (var kv in m_waitAcceptChannels)
            {
                KcpChannel kChannel = kv.Value;
                if (kChannel.IsConnected)
                {
                    continue;
                }

                if (timeNow < kChannel.CreateTime + k_connectTimeoutTime)
                {
                    continue;
                }

                m_cacheIds.Add(kChannel.ChannelId);
            }

            foreach (ulong id in m_cacheIds)
            {
                if (!m_waitAcceptChannels.TryGetValue(id, out KcpChannel kChannel))
                {
                    continue;
                }

                kChannel.OnError(NetworkError.ERR_KcpAcceptTimeout);
            }
        }

        private void UpdateChannel(uint timeNow)
        {
            foreach (ulong id in m_updateIds)
            {
                KcpChannel kChannel = GetChannel(id);
                if (kChannel == null)
                {
                    continue;
                }

                kChannel.Update(timeNow);
            }
            
            m_updateIds.Clear();
        }

        // 服务端需要看channel的update时间是否已到
        public void AddToUpdate(long time, ulong id)
        {
            if (time == 0)
            {
                m_updateIds.Add(id);
                return;
            }

            if (time < m_minTime)
            {
                m_minTime = time;
            }

            m_timeId.Add(time, id);
        }


        // 计算到期需要update的channel
        private void TimerOut(uint timeNow)
        {
            if (m_timeId.Count == 0)
            {
                return;
            }

            if (timeNow < m_minTime)
            {
                return;
            }

            m_timeOutTime.Clear();

            foreach (var kv in m_timeId)
            {
                long k = kv.Key;
                if (k > timeNow)
                {
                    m_minTime = k;
                    break;
                }

                m_timeOutTime.Add(k);
            }

            foreach (long k in m_timeOutTime)
            {
                foreach (ulong v in m_timeId[k])
                {
                    m_updateIds.Add(v);
                }

                m_timeId.Remove(k);
            }
        }

        private IPEndPoint ToIPEndPoint(string address)
        {
            int index = address.LastIndexOf(':');
            string host = address.Substring(0, index);
            string p = address.Substring(index + 1);
            int port = int.Parse(p);
            return new IPEndPoint(IPAddress.Parse(host), port);
        }

        // always pass the same IPEndPointNonAlloc instead of allocating a new
        // one each time.
        //
        // use IPEndPointNonAlloc.temp to get the latest SocketAdddress written
        // by ReceiveFrom_Internal!
        //
        // IMPORTANT: .temp will be overwritten in next call!
        //            hash or manually copy it if you need to store it, e.g.
        //            when adding a new connection.
        public static int ReceiveFromNonAlloc(Socket socket, byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEndPoint)
        {
            // call ReceiveFrom with IPEndPointNonAlloc.
            // need to wrap this in ReceiveFrom_NonAlloc because it's not
            // obvious that IPEndPointNonAlloc.Create does NOT create a new
            // IPEndPoint. it saves the result in IPEndPointNonAlloc.temp!
#if UNITY
            EndPoint casted = remoteEndPoint;
            return socket.ReceiveFrom(buffer, offset, size, socketFlags, ref casted);
#else
            return socket.ReceiveFrom(buffer, offset, size, socketFlags, ref remoteEndPoint);
#endif
        }

        // same as above, different parameters
        public static int ReceiveFromNonAlloc(Socket socket, byte[] buffer, ref EndPoint remoteEndPoint)
        {
#if UNITY
            EndPoint casted = remoteEndPoint;
            return socket.ReceiveFrom(buffer, ref casted);
#else
            return socket.ReceiveFrom(buffer, ref remoteEndPoint);
#endif

        }
    }
}