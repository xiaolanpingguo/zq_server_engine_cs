using System;
using Amazon.Runtime;
using Google.Protobuf;
using System.Threading.Tasks;
using Google.Protobuf.Compiler;
using kcp2k;


namespace ZQ
{
    public class C2DSMessageDispatcher
    {
        internal class RPCData
        {
            public int rpcId;
            public int messageId;
            public readonly TaskCompletionSource<IMessage?>? tcs;
            public long rpcTimestampMs;
            public RPCData(int rpcId, int messageId, TaskCompletionSource<IMessage?>? tcs, long rpcTimestampMs)
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
        private Dictionary<ushort, Action<ushort, int, int, IMessage?>> m_messageHandlers = new();
        private Dictionary<int, RPCData> m_rpcRequests = new();
        private readonly KcpServer m_network;

        public C2DSMessageDispatcher(KcpServer network)
        {
            m_network = network;
        }

        public bool IsMessageRegistered(ushort messageId)
        {
            if (m_messageTypes.ContainsKey(messageId) || m_messageHandlers.ContainsKey(messageId))
            {
                return true;
            }

            return false;
        }

        public bool RegisterMessage(ushort messageId, Type type, Action<ushort, int, int, IMessage?>? handler = null)
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

        public bool Response(IMessage packet, int connectionId, ushort messageId, int rpcId)
        {
            if (packet == null)
            {
                return false;
            }

            return Send(packet, connectionId, messageId, rpcId);
        }

        public bool Send(IMessage packet, int connectionId, ushort messageId, int rpcId = -1)
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

                m_network.Send(connectionId, buffer.GetBuffer(), KcpChannel.Reliable);

                return true;
            }
            catch (Exception e)
            {
                Log.Error($"c2s: send data failed, connectionId:{connectionId}, messageId:{messageId}, packet:{packet.GetType().Name}, ex: {e.Message}");
                return false;
            }
        }

        public async Task<IMessage?> SendAsync(IMessage packet, int connectionId, ushort messageId)
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

                m_network.Send(connectionId, buffer.GetBuffer(), KcpChannel.Reliable);

                var tcs = new TaskCompletionSource<IMessage?>();

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
                if (data == null)
                {
                    continue;
                }
                if (timeNow - data.rpcTimestampMs >= k_rpcTimeoutMs)
                {
                    Log.Warning($"C2SMessageDispatcher:rpc timeout, messageId:{data.messageId}, rpcId:{data.rpcId}");
                    m_rpcRequests.Remove(pair.Key);
                    data.tcs?.TrySetResult(null);
                }
            }
        }

        public void DispatchMessage(int connectionId, ArraySegment<byte> buffer, KcpChannel channel) 
        {
            try
            {
                ushort messageId;
                int rpcId;
                byte[]? packetBytes;
                if (!Deserialize(buffer, connectionId, out messageId, out rpcId, out packetBytes))
                {
                    return;
                }

                if (!m_messageTypes.TryGetValue(messageId, out Type? type))
                {
                    Log.Error($"cannot find message type by id:{messageId}");
                    return;
                }

                IMessage? message = (IMessage?)Activator.CreateInstance(type);
                IMessage? packet = message?.Descriptor.Parser.ParseFrom(packetBytes);

                if (m_rpcRequests.Count == 0 || !m_rpcRequests.TryGetValue(rpcId, out var rpcData))
                {
                    if (!m_messageHandlers.TryGetValue(messageId, out var handler))
                    {
                        Log.Error($"both message and rcp request cannot be found, id:{messageId}");
                        m_network.Disconnect(connectionId);
                        return;
                    }
                    else
                    {
                        handler?.Invoke(messageId, connectionId, rpcId, packet);
                    }
                }
                else
                {
                    m_rpcRequests.Remove(rpcId);
                    rpcData?.tcs?.SetResult(packet);
                }
            }
            catch (Exception e)
            {
                string errorDesc = $"DispatchPacket packet failed, ex, {e.Message}";
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

        private bool Deserialize(ArraySegment<byte> data, int connectionId, out ushort messageId, out int rpcId, out byte[]? packetBuffer)
        {
            do
            {
                if (data.Array == null || data.Count <= k_headSize)
                {
                    break;
                }

                int offset = data.Offset;
                int packetSize = BitConverter.ToInt32(data.Array, offset);
                if (packetSize > k_maxSize)
                {
                    string errorDesc = $"recv packet size error: {packetSize}";
                    Log.Error(errorDesc);
                    m_network.Disconnect(connectionId);
                    break;
                }
                offset += k_packetSize;

                // message id
                messageId = BitConverter.ToUInt16(data.Array, offset);
                offset += k_messageIdSize;

                // rpc id
                rpcId = BitConverter.ToInt32(data.Array, offset);
                offset += k_rpcIdSize;

                // packet
                byte[] packageBytes = new byte[packetSize];
                Buffer.BlockCopy(data.Array, offset, packageBytes, 0, packageBytes.Length);
                packetBuffer = packageBytes;

                return true;
            } while (false);

            rpcId = -1;
            messageId = 0;
            packetBuffer = null;
            return false;
        }
    }
}