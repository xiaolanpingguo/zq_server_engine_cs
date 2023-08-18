using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Threading.Channels;
using Amazon.Runtime;
using Google.Protobuf;

namespace ZQ
{
    public class C2SMessageDispatcher
    {
        internal class RPCData
        {
            public int rpcId;
            public int messageId;
            public readonly TaskCompletionSource<IMessage> tcs;
            public long rpcTimestampMs;
            public RPCData(int rpcId, int messageId, TaskCompletionSource<IMessage> tcs, long rpcTimestampMs)
            {
                this.rpcId = rpcId;
                this.messageId = messageId;
                this.tcs = tcs;
                this.rpcTimestampMs = rpcTimestampMs;
            }
        }

        private const int k_packetSize = 4;
        private const int k_messageIdSize = 2;
        private const int k_rpcIdSize = 4;
        private const int k_headSize = k_packetSize + k_messageIdSize + k_rpcIdSize;
        private const int k_maxSize = 1024 * 32;
        private const int k_rpcTimeoutMs = 10000;

        private int m_rpcId = 0;

        private Dictionary<ushort, Type> m_messageTypes = new();
        private Dictionary<ushort, Action<ushort, ulong, int, IMessage>> m_messageHandlers = new();
        private Dictionary<int, RPCData> m_rpcRequests = new();
        private readonly TcpService m_service;

        public C2SMessageDispatcher(TcpService server)
        {
            m_service = server;
        }

        public bool IsMessageRegistered(ushort messageId)
        {
            if (m_messageTypes.ContainsKey(messageId) || m_messageHandlers.ContainsKey(messageId))
            {
                return true;
            }

            return false;
        }

        public bool RegisterMessage(ushort messageId, Type type, Action<ushort, ulong, int, IMessage> handler = null)
        {
            if (m_messageTypes.ContainsKey(messageId) || m_messageHandlers.ContainsKey(messageId))
            {
                Log.Error($"messageId:{messageId} has register.");
                return false;
            }

            m_messageTypes[messageId] = type;
            if (handler != null)
            {
               m_messageHandlers[messageId] = handler;
            }
            return true;
        }

        public bool Response(IMessage packet, ulong channelId, ushort messageId, int rpcId)
        {
            if (packet == null)
            {
                return false;
            }

            return Send(packet, channelId, messageId, rpcId);
        }

        public bool Send(IMessage packet, ulong channelId, ushort messageId, int rpcId = -1)
        {
            if (packet == null) 
            {
                return false;
            }
            try
            {
                byte[] packetBytes = packet.ToByteArray();
                MessageBuffer buffer = Serialize(packetBytes, messageId, rpcId);
                if (buffer == null)
                {
                    return false;
                }

                m_service.Send(channelId, buffer);

                return true;
            }
            catch (Exception e)
            {
                Log.Error($"c2s: send data failed, channelId:{channelId}, messageId:{messageId}, packet:{packet.GetType().Name}, ex: {e.Message}");
                return false;
            }
        }

        public async Task<IMessage> SendAsync(IMessage packet, ulong channelId, ushort messageId)
        {
            if (packet == null)
            {
                return null;
            }

            try
            {
                byte[] packetBytes = packet.ToByteArray();
                MessageBuffer buffer = Serialize(packetBytes, messageId, m_rpcId);
                if (buffer == null)
                {
                    return null;
                }

                m_service.Send(channelId, buffer);

                var tcs = new TaskCompletionSource<IMessage>();

                RPCData rPCData = new RPCData(m_rpcId, messageId, tcs, TimeHelper.TimeStampNowMs());
                m_rpcRequests[m_rpcId++] = rPCData;

                var res = await tcs.Task;
                return res;
            }
            catch (Exception e)
            {
                Log.Error($"send packet failed, ex, {e.Message}");
                return null;
            }
        }

        public void Update(long timeNow)
        {
            if (m_rpcRequests.Count == 0)
            {
                return;
            }

            foreach (var pair in m_rpcRequests)
            {
                RPCData data = pair.Value;
                if (timeNow - data.rpcTimestampMs >= k_rpcTimeoutMs)
                {
                    Log.Warning($"C2SMessageDispatcher:rpc timeout, messageId:{data.messageId}, rpcId:{data.rpcId}");
                    m_rpcRequests.Remove(pair.Key);
                    data.tcs.TrySetResult(null);
                }
            }
        }

        public void DispatchMessage(ulong channelId, MessageBuffer buffer)
        {
            try
            {
                while (buffer.GetActiveSize() > 0)
                {
                    ushort messageId;
                    int rpcId;
                    byte[] packetBytes;
                    if (!Deserialize(buffer, channelId, out messageId, out rpcId, out packetBytes))
                    {
                        return;
                    }

                    if (!m_messageTypes.TryGetValue(messageId, out Type type))
                    {
                        Log.Error($"cannot find message type by id:{messageId}");
                        return;
                    }

                    IMessage message = (IMessage)Activator.CreateInstance(type);
                    IMessage packet = message.Descriptor.Parser.ParseFrom(packetBytes);

                    if (m_rpcRequests.Count == 0 || !m_rpcRequests.TryGetValue(rpcId, out var rpcData))
                    {
                        if (!m_messageHandlers.TryGetValue(messageId, out var handler))
                        {
                            m_service.OnError(channelId, NetworkError.ERR_ErrorMessageId, $"cannot find message handler by id:{messageId}");
                            return;
                        }
                        else
                        {
                            handler?.Invoke(messageId, channelId, rpcId, packet);
                        }
                    }
                    else
                    {
                        m_rpcRequests.Remove(rpcId);
                        rpcData.tcs.SetResult(packet);
                    }
                }
            }
            catch (Exception e)
            {
                string errorDesc = $"DispatchPacket packet failed, ex: {e.Message}";
                Log.Error(errorDesc);
            }
        }

        private MessageBuffer Serialize(in byte[] buffer, ushort messageId, int rpcId)
        {
            // packet length
            int packetLength = buffer.Length;
            byte[] packetLengthBytes = new byte[k_packetSize];
            packetLengthBytes.WriteTo(0, packetLength);

            // message id
            byte[] messageIdBytes = new byte[k_messageIdSize];
            messageIdBytes.WriteTo(0, messageId);

            // rpc id
            byte[] rpcIdBytes = new byte[k_rpcIdSize];
            rpcIdBytes.WriteTo(0, rpcId);

            // packet
            MessageBuffer memoryBuffer = new MessageBuffer(k_headSize + packetLength);
            memoryBuffer.Write(packetLengthBytes);
            memoryBuffer.Write(messageIdBytes);
            memoryBuffer.Write(rpcIdBytes);
            memoryBuffer.Write(buffer);
            return memoryBuffer;
        }

        private bool Deserialize(MessageBuffer buffer, ulong channelId, out ushort messageId, out int rpcId, out byte[] packetBuffer)
        {
            do
            {
                if (buffer.GetActiveSize() <= k_headSize)
                {
                    break;
                }

                // packet length
                byte[] packetLengthBytes = new byte[k_packetSize];
                Array.Copy(buffer.GetBuffer(), buffer.GetReadPos(), packetLengthBytes, 0, packetLengthBytes.Length);
                int packetSize = BitConverter.ToInt32(packetLengthBytes, 0);
                if (packetSize > k_maxSize)
                {
                    string errorDesc = $"recv packet size error: {packetSize}";
                    Log.Error(errorDesc);
                    m_service.OnError(channelId, NetworkError.ERR_SocketError, errorDesc);
                    break;
                }

                if (buffer.GetBufferSize() < k_headSize + packetSize)
                {
                    buffer.EnsureFreeSpace();
                    break;
                }

                buffer.ReadCompleted(packetLengthBytes.Length);

                // message id
                byte[] messageIdBytes = new byte[k_messageIdSize];
                Array.Copy(buffer.GetBuffer(), buffer.GetReadPos(), messageIdBytes, 0, messageIdBytes.Length);
                messageId = BitConverter.ToUInt16(messageIdBytes, 0);
                buffer.ReadCompleted(messageIdBytes.Length);

                // rpc id
                byte[] rpcIdBytes = new byte[k_rpcIdSize];
                Array.Copy(buffer.GetBuffer(), buffer.GetReadPos(), rpcIdBytes, 0, rpcIdBytes.Length);
                rpcId = BitConverter.ToInt32(rpcIdBytes, 0);
                buffer.ReadCompleted(rpcIdBytes.Length);

                // check the remain size
                if (buffer.GetActiveSize() < packetSize)
                {
                    break;
                }

                // packet
                byte[] packageBytes = new byte[packetSize];
                Array.Copy(buffer.GetBuffer(), buffer.GetReadPos(), packageBytes, 0, packageBytes.Length);
                packetBuffer = packageBytes;
                buffer.ReadCompleted(packageBytes.Length);

                buffer.Normalize();
                return true;
            } while (false);

            rpcId = -1;
            messageId = 0;
            packetBuffer = null;
            return false;
        }
    }
}