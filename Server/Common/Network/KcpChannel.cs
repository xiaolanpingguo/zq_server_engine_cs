using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ZQ
{
	public class KcpChannel
	{
        static private readonly System.Buffers.ArrayPool<byte> s_byteArrayPool = System.Buffers.ArrayPool<byte>.Create(2048, 1000);

        private const int k_mtuSize = 1472;

        private readonly KcpService m_service;
        private Kcp m_kcp { get; set; }
		private readonly Queue<MessageBuffer> m_waitSendMessages = new();

        private readonly byte[] m_sendCache = new byte[2 * 1024];
        private MessageBuffer m_readMemory;
        private int m_needReadSplitCount;

        private IPEndPoint m_remoteAddress;

        private long m_lastConnectTime = long.MaxValue;

		private bool m_isClient;

        public ulong ChannelId;
        public readonly uint CreateTime;
        public uint LocalConn
		{
			get
			{
				return (uint)ChannelId;
			}
			private set
			{
				ChannelId = value;
			}
		}

		public uint RemoteConn { get; set; }
		public bool IsConnected { get; set; }
		public string RealAddress { get; set; }

        public IPEndPoint RemoteAddress
		{
			get
			{
				return m_remoteAddress;
			}
			set
			{
                m_remoteAddress = new IPEndPointNonAlloc(value.Address, value.Port);
			}
		}

		private void InitKcp()
		{
            m_kcp = new Kcp(RemoteConn, Output);
            m_kcp.SetNoDelay(1, 10, 2, true);
			m_kcp.SetWindowSize(1024, 1024);
			m_kcp.SetMtu(k_mtuSize);
			m_kcp.SetMinrto(30);
			m_kcp.SetArrayPool(s_byteArrayPool);
		}

		public KcpChannel(uint localConn, IPEndPoint remoteEndPoint, KcpService kService)
		{
			m_service = kService;
			LocalConn = localConn;
			RemoteAddress = remoteEndPoint;
			CreateTime = m_service.TimeNow;
			m_isClient = true;
            IsConnected = false;
            Connect(CreateTime);
		}

		public KcpChannel(uint localConn, uint remoteConn, IPEndPoint remoteEndPoint, KcpService kService)
		{
			m_service = kService;
			LocalConn = localConn;
			RemoteAddress = remoteEndPoint;
			CreateTime = m_service.TimeNow;
            RemoteConn = remoteConn;
            m_isClient = false;
            IsConnected = true;
            InitKcp();
        }

		public void Close()
		{
			if (ChannelId == 0)
			{
				return;
			}

            ChannelId = 0;
			m_kcp = null;
		}

		public void HandleConnnect()
		{
			// 如果连接上了就不用处理了
			if (IsConnected)
			{
				return;
			}

			InitKcp();
            IsConnected = true;
            m_service.OnConnectSuccess(LocalConn, RemoteConn, RemoteAddress);

			while (true)
			{
				if (m_waitSendMessages.Count <= 0)
				{
					break;
				}

                MessageBuffer buffer = m_waitSendMessages.Dequeue();
				Send(buffer);
			}
		}

		private void Connect(uint timeNow)
		{
			try
			{
				if (IsConnected)
				{
					return;
				}

				// 300毫秒后再次update发送connect请求
				if (timeNow < m_lastConnectTime + 300)
				{
					m_service.AddToUpdate(300, ChannelId);
					return;
				}

				// 连接超时
				if (timeNow > CreateTime + KcpService.k_connectTimeoutTime)
				{
					Log.Error($"kChannel connect timeout: {ChannelId} {RemoteConn} {timeNow} {CreateTime} {RemoteAddress}");
					OnError(NetworkError.ERR_KcpConnectTimeout);
					return;
				}

				byte[] buffer = m_sendCache;
				buffer.WriteTo(0, KcpProtocalType.SYN);
				buffer.WriteTo(1, LocalConn);
				buffer.WriteTo(5, RemoteConn);
				m_service.Socket.SendTo(buffer, 0, 9, SocketFlags.None, RemoteAddress);

                m_lastConnectTime = timeNow;
				m_service.AddToUpdate(300, ChannelId);
			}
			catch (Exception e)
			{
				Log.Error(e.Message);
				OnError(NetworkError.ERR_KcpSocketCantSend);
			}
		}

		public void Update(uint timeNow)
		{
			// 如果还没连接上，发送连接请求
			if (!IsConnected && m_isClient)
			{
				Connect(timeNow);
				return;
			}

            if (!IsConnected)
			{
				return;
			}

            if (m_kcp == null)
			{
				return;
			}

			try
			{
				m_kcp.Update(timeNow);
			}
			catch (Exception e)
			{
				Log.Error(e.Message);
				OnError(NetworkError.ERR_KcpSocketError);
				return;
			}

			uint nextUpdateTime = m_kcp.Check(timeNow);
			m_service.AddToUpdate(nextUpdateTime, ChannelId);
		}

		public unsafe void HandleRecv(byte[] date, int offset, int length)
		{
			if (!IsConnected)
			{
				return;
			}

			m_kcp.Input(date.AsSpan(offset, length));
			m_service.AddToUpdate(0, ChannelId);
			while (true)
			{
				if (!IsConnected)
				{
					break;
				}

				int n = m_kcp.PeekSize();
				if (n < 0)
				{
					break;
				}
				if (n == 0)
				{
					OnError((int)SocketError.NetworkReset);
					return;
				}

				if (m_needReadSplitCount > 0) // 说明消息分片了
				{
					if (m_readMemory.GetRemainingSpace() == 0)
					{
                        Log.Error($"kchannel read error: there is no fre space to receive:{LocalConn} {RemoteConn}");
                        OnError(NetworkError.ERR_KcpPacketSizeError);
                        return;
                    }

					byte[] buffer = m_readMemory.GetBuffer();
					int count = m_kcp.Receive(buffer.AsSpan(m_readMemory.GetWritePos(), m_readMemory.GetRemainingSpace()));
					m_needReadSplitCount -= count;
					if (n != count)
					{
						Log.Error($"kchannel read error1: {LocalConn} {RemoteConn}");
						OnError(NetworkError.ERR_KcpReadNotSame);
						return;
					}

					if (m_needReadSplitCount < 0)
					{
						Log.Error($"kchannel read error2: {this.LocalConn} {this.RemoteConn}");
						OnError(NetworkError.ERR_KcpSplitError);
						return;
					}

                    m_readMemory.WriteCompleted(n);

                    // 没有读完
                    if (m_needReadSplitCount != 0)
					{
						continue;
					}
				}
				else
				{
					m_readMemory = m_service.Fetch(n);
					byte[] buffer = m_readMemory.GetBuffer();
					int count = m_kcp.Receive(buffer.AsSpan(0, n));
					if (n != count)
					{
						break;
					}

                    m_readMemory.WriteCompleted(n);

                    // 判断是不是分片
                    if (n == 8)
					{
						int headInt = BitConverter.ToInt32(m_readMemory.GetBuffer(), 0);
						if (headInt == 0)
						{
							m_needReadSplitCount = BitConverter.ToInt32(m_readMemory.GetBuffer(), 4);
							if (m_needReadSplitCount <= k_mtuSize)
							{
								Log.Error($"kchannel read error3: {this.m_needReadSplitCount} {this.LocalConn} {this.RemoteConn}");
								OnError(NetworkError.ERR_KcpSplitCountError);
								return;
							}

                            m_readMemory.Reset();
                            continue;
						}
					}
				}

                MessageBuffer memoryBuffer = m_readMemory;
                m_readMemory = null;
                m_service.OnDataReceived(ChannelId, memoryBuffer);
				m_service.Recycle(memoryBuffer);
			}
		}

		private void Output(byte[] bytes, int count)
		{
			try
			{
				if (!IsConnected)
				{
					return;
				}

				if (count == 0)
				{
					return;
				}

                // 每个消息头部写下该channel的id;
                bytes.WriteTo(0, KcpProtocalType.MSG);
                bytes.WriteTo(1, LocalConn);
                bytes.WriteTo(5, RemoteConn);
                m_service.Socket.SendTo(bytes, 0, count, SocketFlags.None, this.RemoteAddress);
			}
			catch (Exception e)
			{
				Log.Error(e.Message);
				OnError(NetworkError.ERR_KcpSocketCantSend);
			}
		}

		private void KcpSend(MessageBuffer memoryStream)
		{
			if (!IsConnected)
			{
				return;
			}

            // 超出maxPacketSize需要分片
            int count = memoryStream.GetActiveSize();
            if (count <= k_mtuSize)
			{
                m_kcp.Send(memoryStream.GetBuffer().AsSpan(memoryStream.GetReadPos(), count));
			}
			else
			{
                // 先发分片信息
                m_sendCache.WriteTo(0, 0);
				m_sendCache.WriteTo(4, count);
				m_kcp.Send(m_sendCache.AsSpan(0, 8));

				// 分片发送
				while (true)
				{
                    count = memoryStream.GetActiveSize();
					if (count == 0)
					{
						break;
					}

					int sendCount = count < k_mtuSize ? count : k_mtuSize;
					m_kcp.Send(memoryStream.GetBuffer().AsSpan(memoryStream.GetReadPos(), sendCount));
					memoryStream.ReadCompleted(sendCount);
				}
			}

			m_service.AddToUpdate(0, ChannelId);
		}

		public void Send(MessageBuffer message)
		{
            if (m_kcp == null)
            {
				return;
            }

            if (!IsConnected)
			{
				m_waitSendMessages.Enqueue(message);
				return;
			}

			// 检查等待发送的消息，如果超出最大等待大小，应该断开连接
			int n = m_kcp.WaitSnd;
			int maxWaitSize = m_service.m_sendMaxWaitSize;
			if (n > maxWaitSize)
			{
				Log.Error($"kcp wait snd too large: {n}: {LocalConn} {RemoteConn}");
				OnError(NetworkError.ERR_KcpWaitSendSizeTooLarge);
				return;
			}

            KcpSend(message);
		}

		public void OnError(int error)
		{
            m_service.OnError(ChannelId, error);
		}
	}
}
