using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;


namespace ZQ
{
	public struct TArgs
	{
		public ulong ChannelId;
		public SocketAsyncEventArgs SocketAsyncEventArgs;
	}
	
	public sealed class TcpService
	{
        private readonly SocketAsyncEventArgs m_acceptEventArgs = new SocketAsyncEventArgs();
        private Socket m_acceptor;

        private bool m_stop = false;
        private ulong m_id = 0;
        private readonly Dictionary<ulong, TcpChannel> m_channels = new Dictionary<ulong, TcpChannel>();
        private bool m_isClient;

        private int m_connectionTimeout;
        private int m_channelBufferSize;

        public int ChannelBufferSize => m_channelBufferSize;
        public ConcurrentQueue<TArgs> Queue = new ConcurrentQueue<TArgs>();

        // for server
        public Action<ulong, IPEndPoint> AcceptCallback;
        public Action<ulong, IPEndPoint, int> ClientDisconnectCallback;

        // for client
        public Action<ulong, IPEndPoint> ConnectSuccessCallback;
        public Action<ulong, IPEndPoint, int> CannotConnectServerCallback;

        // for client/server
        public Action<ulong, MessageBuffer> DataReceivedCallback;

        public TcpService(IPEndPoint ipEndPoint, bool isClient, int connectionTimeout = 45, int channelBufferSize = 1024 * 8)
		{
            m_isClient = isClient;
            m_channelBufferSize = channelBufferSize;
            m_connectionTimeout = connectionTimeout;
            if (!m_isClient)
            {
                m_acceptor = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                m_acceptor.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                m_acceptEventArgs.Completed += OnAcceptEvent;
                m_acceptor.Bind(ipEndPoint);
                m_acceptor.Listen();
            }
        }

        public void Start()
        {
            if (!m_isClient)
            {
                AcceptAsync();
            }
        }

        public bool CreateChannel(ulong channelId, IPEndPoint ipEndPoint)
        {
            TcpChannel channel = GetChannel(channelId);
            if (channel == null)
            {
                channel = new TcpChannel(channelId, ipEndPoint, this, m_channelBufferSize);
                m_channels.Add(channelId, channel);
                channel.ConnectAsync();
                return true;
            }
            else
            {
                Log.Error($"CreateChannel failed, channelId:{channelId} has exist.");
                return false;
            }
        }

        public void Close(ulong channelId)
        {
            if (m_channels.TryGetValue(channelId, out var channel))
            {
                channel.Close();
                m_channels.Remove(channelId);
            }
        }

        public void DelayClose(ulong channelId)
        {
            if (m_channels.TryGetValue(channelId, out var channel))
            {
                channel.DelayClose();
            }
        }

        public bool IsChannelOpen(ulong channelId)
        {
            TcpChannel channel = GetChannel(channelId);
            if (channel == null)
            {
                return false;
            }

            return channel.IsOpen;
        }

        public IPEndPoint GetChannelEndpoint(ulong channelId)
        {
            TcpChannel channel = GetChannel(channelId);
            if (channel == null)
            {
                return null;
            }

            return channel.RemoteAddress;
        }

        public void OnDataReceived(ulong channelId, MessageBuffer buffer)
        {
            DataReceivedCallback?.Invoke(channelId, buffer);
        }

        public void OnConnectComplete(ulong channelId, SocketAsyncEventArgs e)
        {
            TcpChannel channel = GetChannel(channelId);
            if (channel == null)
            {
                return;
            }

            channel.ConnectStatus = TcpChannel.ConnectStatusType.Connected;

            if (e.SocketError != SocketError.Success)
            {
                OnError(channelId, (int)e.SocketError, "");
                return;
            }

            channel.IsOpen = true;

            ConnectSuccessCallback?.Invoke(channelId, channel.RemoteAddress);
            
            channel.StartRecv();
            channel.StartSend();
        }

        public void Stop()
        {
            m_acceptor?.Close();
            m_acceptor = null;
            m_acceptEventArgs.Dispose();

            foreach (ulong id in m_channels.Keys.ToArray())
            {
                TcpChannel channel = m_channels[id];
                channel.Close();
            }

            m_channels.Clear();
        }

        public bool Send(ulong channelId, MessageBuffer buffer)
        {
            TcpChannel aChannel = GetChannel(channelId);
            if (aChannel == null)
            {
                Log.Error($"cannot found channel by id:{channelId}");
                return false;
            }

            aChannel.Send(buffer);

            return true;
        }

        public void OnError(ulong channelId, int error, string errorDesc)
        {
            if (!m_channels.TryGetValue(channelId, out TcpChannel channel))
            {
                return;
            }

            IPEndPoint ip = channel.RemoteAddress;

            channel.Close();
            m_channels.Remove(channelId);

            if (m_isClient)
            {
                CannotConnectServerCallback?.Invoke(channelId, ip, error);
            }
            else
            {
                ClientDisconnectCallback?.Invoke(channelId, ip, error);
            }
        }

        public void Update()
        {
            if (m_stop)
            {
                return;
            }

            ProcessNetworkEvent();
            DeleteTimeoutConnection();
        }

        private void DeleteTimeoutConnection()
        {
            long timeNow = TimeHelper.TimeStampNowSeconds();
            foreach(var kv in m_channels)
            {
                TcpChannel channel = kv.Value;
                if (channel == null || channel.IsClient)
                {
                    continue;
                }

                if (timeNow - channel.LastActiveTime > m_connectionTimeout) 
                {
                    OnError(kv.Key, NetworkError.ERR_SessionTimeout, "");
                }
            }
        }

        private void ProcessNetworkEvent()
        {
            if (!Queue.TryDequeue(out var result))
            {
                return;
            }

            SocketAsyncEventArgs e = result.SocketAsyncEventArgs;
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    {
                        OnAcceptComplete(e);
                        break;
                    }
                case SocketAsyncOperation.Connect:
                    {
                        OnConnectComplete(result.ChannelId, e);
                        break;
                    }
                case SocketAsyncOperation.Disconnect:
                    {
                        OnDisonnectComplete(result.ChannelId, e);
                        break;
                    }
                case SocketAsyncOperation.Receive:
                    {
                        TcpChannel tChannel = GetChannel(result.ChannelId);
                        if (tChannel != null)
                        {
                            tChannel.OnRecvComplete(e);
                        }
                        break;
                    }
                case SocketAsyncOperation.Send:
                    {
                        TcpChannel tChannel = GetChannel(result.ChannelId);
                        if (tChannel != null)
                        {
                            tChannel.OnSendComplete(e);
                        }
                        break;
                    }
                default:
                    break;
            }
        }

        private void OnAcceptComplete(SocketAsyncEventArgs e)
		{
			if (m_acceptor == null)
			{
				return;
			}

			if (e.SocketError != SocketError.Success)
			{
				Log.Error($"accept error {e.SocketError}");
				return;
			}

			try
			{
                ulong channelId = m_id++;
                TcpChannel channel = new TcpChannel(channelId, e.AcceptSocket, this, m_channelBufferSize);
				m_channels.Add(channelId, channel);
				AcceptCallback?.Invoke(channelId, channel.RemoteAddress);

                if (channel.IsOpen)
                {
                    channel.StartRecv();
                    channel.StartSend();
                }
            }
			catch (Exception exception)
			{
				Log.Error(exception.Message);
			}		
			
			AcceptAsync();
		}
		
		private void AcceptAsync()
		{
			this.m_acceptEventArgs.AcceptSocket = null;
			if (this.m_acceptor.AcceptAsync(this.m_acceptEventArgs))
			{
				return;
			}
			OnAcceptComplete(m_acceptEventArgs);
		}

        private void OnAcceptEvent(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    this.Queue.Enqueue(new TArgs() { SocketAsyncEventArgs = e });
                    break;
                default:
					Log.Error($"socket error on OnAcceptEvent, op: {e.LastOperation}");
					break;
            }
        }

        private void OnDisonnectComplete(ulong channelId, SocketAsyncEventArgs e)
        {
            TcpChannel channel = GetChannel(channelId);
            if (channel == null)
            {
                return;
            }

            channel.IsOpen = false;
            OnError(channelId, (int)e.SocketError, "");
        }
		
		private TcpChannel GetChannel(ulong id)
		{
			if (m_channels.TryGetValue(id, out var channel))
            {
                return channel;
            }

			return null;
		}
	}
}