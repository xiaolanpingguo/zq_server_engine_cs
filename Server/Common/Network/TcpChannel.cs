using System.Net;
using System.Net.Sockets;


namespace ZQ
{
	public sealed class TcpChannel
	{
		public enum ConnectStatusType
		{
			Disconnect,
			Connecting,
			Connected,
		}

		private readonly TcpService m_service;
		private Socket m_socket;
		private SocketAsyncEventArgs m_receiveArgs = new SocketAsyncEventArgs();
        private SocketAsyncEventArgs m_sendArgs = new SocketAsyncEventArgs();
        private SocketAsyncEventArgs m_connectArgs = new SocketAsyncEventArgs();

        private MessageBuffer m_recvBuffer;
        private Queue<MessageBuffer> m_sendQueue = new Queue<MessageBuffer>();

        private ulong m_channelId;
        private bool m_isSending;
        private bool m_isOpen;
        private bool m_delayClose;

        public bool IsClient { get; set; }
        public IPEndPoint RemoteAddress { get; set; }
		public ConnectStatusType ConnectStatus { get; set; }
		public long LastActiveTime = 0;

        public bool IsOpen { 
			get => m_isOpen && !m_delayClose; 
			set
			{
				if (!m_delayClose)
				{
                    m_isOpen = value;
                }
			}
		}

        public TcpChannel(ulong channelId, IPEndPoint ipEndPoint, TcpService service, int channelBufferSize)
		{
			IsClient = true;
            m_channelId = channelId;
            m_service = service;
			m_socket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			m_socket.NoDelay = true;
            m_receiveArgs.Completed += OnSocketEvent;
            m_sendArgs.Completed += OnSocketEvent;
            m_connectArgs.Completed += OnSocketEvent;
            RemoteAddress = ipEndPoint;
            m_isOpen = false;
            m_isSending = false;
            m_delayClose = false;
            ConnectStatus = ConnectStatusType.Disconnect;
            m_recvBuffer = new MessageBuffer(channelBufferSize);
        }
		
		public TcpChannel(ulong channelId, Socket socket, TcpService service, int channelBufferSize)
		{
            IsClient = false;
            m_channelId = channelId;
            m_service = service;
			m_socket = socket;
			m_socket.NoDelay = true;
            m_receiveArgs.Completed += OnSocketEvent;
            m_sendArgs.Completed += OnSocketEvent;
            m_connectArgs.Completed += OnSocketEvent;
            RemoteAddress = (IPEndPoint)socket.RemoteEndPoint;
            m_isOpen = true;
            m_isSending = false;
            m_delayClose = false;
            m_recvBuffer = new MessageBuffer(channelBufferSize);
			LastActiveTime = TimeHelper.TimeStampNowSeconds();
        }

		public void Send(MessageBuffer buffer)
		{
			if (!m_isOpen)
			{
				return;
			}

            m_sendQueue.Enqueue(buffer);

			if (!m_isSending)
			{
				StartSend();
			}
		}

        public void Close()
        {
            if (!m_isOpen)
            {
                return;
            }

            m_socket.Shutdown(SocketShutdown.Both);
            m_socket.Close();
            m_receiveArgs.Dispose();
            m_receiveArgs = null;
            m_socket = null;
            m_isOpen = false;
            m_recvBuffer = null;
            m_sendQueue.Clear();
        }

		public void DelayClose()
		{
			if (m_delayClose)
			{
				return;
			}

			m_delayClose = true;
            if (m_sendQueue.Count == 0)
			{
                OnError(NetworkError.ERR_CloseByServer);
            }
			else if (m_isSending)
			{
                m_socket.Shutdown(SocketShutdown.Receive);
            }
			else
			{
                StartSend();
            }
        }

        public void ConnectAsync()
		{
			if (ConnectStatus == TcpChannel.ConnectStatusType.Connecting ||
                ConnectStatus == TcpChannel.ConnectStatusType.Connected)
            {
                return;
            }

            ConnectStatus = TcpChannel.ConnectStatusType.Connecting;
            m_connectArgs.RemoteEndPoint = RemoteAddress;
			if (m_socket.ConnectAsync(m_connectArgs))
			{
				return;
			}

            m_service.OnConnectComplete(m_channelId, m_connectArgs);
		}

        public void OnDisconnectComplete(SocketAsyncEventArgs e)
		{
			OnError((int)e.SocketError);
		}

		public void StartRecv()
		{
			while (true)
			{
				try
				{
					if (m_socket == null)
					{
						return;
					}

                    m_receiveArgs.SetBuffer(m_recvBuffer.GetBuffer(), m_recvBuffer.GetWritePos(), m_recvBuffer.GetRemainingSpace());
                }
				catch (Exception e)
				{
					OnError(NetworkError.ERR_TcpChannelRecvError, $"tchannel receive exception: {e}");
					return;
				}
			
				if (m_socket.ReceiveAsync(m_receiveArgs))
				{
					return;
				}

				HandleRecv(m_receiveArgs);
			}
		}

        public void OnRecvComplete(SocketAsyncEventArgs o)
        {
            HandleRecv(o);

            if (m_socket == null)
            {
                return;
            }

			StartRecv();
        }

        private void OnSocketEvent(object sender, SocketAsyncEventArgs e)
        {
            m_service.Queue.Enqueue(new TArgs() { ChannelId = m_channelId, SocketAsyncEventArgs = e });
        }

        private void HandleRecv(SocketAsyncEventArgs e)
		{
			if (m_socket == null)
			{
				return;
			}
			if (e.SocketError != SocketError.Success)
			{
				OnError((int)e.SocketError);
				return;
			}

			if (e.BytesTransferred == 0)
			{
				OnError(NetworkError.ERR_PeerDisconnect);
				return;
			}

			LastActiveTime = TimeHelper.TimeStampNowSeconds();

            m_recvBuffer.WriteCompleted(e.BytesTransferred);
            m_service.OnDataReceived(m_channelId, m_recvBuffer);
        }

		public void StartSend()
		{
			if(!m_isOpen)
			{
				return;
			}

			if (m_isSending)
			{
				return;
			}
			
			while (true)
			{
				try
				{
					if (m_socket == null)
					{
                        m_isSending = false;
						return;
					}
					
					// there no data to send
					if (m_sendQueue.Count == 0)
					{
                        m_isSending = false;
						return;
					}

                    m_isSending = true;

					MessageBuffer buffer = m_sendQueue.Peek();
					m_sendArgs.SetBuffer(buffer.GetBuffer(), buffer.GetReadPos(), buffer.GetActiveSize());
					
					if (m_socket.SendAsync(m_sendArgs))
					{
						return;
					}
				
					HandleSend(m_sendArgs);
				}
				catch (Exception e)
				{
					string desc = $"socket send buffer, count: {m_sendQueue.Count}, e:{e.Message}";
                    OnError(NetworkError.ERR_TcpChannelSendError, desc);
                    return;
                }
			}
		}

		public void OnSendComplete(SocketAsyncEventArgs o)
		{
			HandleSend(o);
            m_isSending = false;
			if (m_isOpen)
			{
				StartSend();
			}
        }

		private void HandleSend(SocketAsyncEventArgs e)
		{
			if (m_socket == null)
			{
				return;
			}

			if (e.SocketError != SocketError.Success)
			{
				OnError((int)e.SocketError);
				return;
			}
			
			if (e.BytesTransferred == 0)
			{
				OnError(NetworkError.ERR_PeerDisconnect);
				return;
			}

			if (m_sendQueue.Count == 0 && m_delayClose)
			{
				OnError(NetworkError.ERR_CloseByServer);
				return;
			}

            MessageBuffer buffer = m_sendQueue.Peek();
			buffer.ReadCompleted(e.BytesTransferred);
			if (buffer.GetActiveSize() == 0)
			{
                m_sendQueue.Dequeue();
            }
		}

		private void OnError(int error, string errorDesc = "")
		{
            m_service.OnError(m_channelId, error, errorDesc);
		}
	}
}