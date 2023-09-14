using System.Collections.Generic;
using System.Threading.Channels;

namespace ZQ
{
    public class Room
    {
        private readonly C2DSMessageDispatcher m_messageDispatcher;

        private GameTime m_gameTime = new GameTime();
        private int m_matchCount = 1;
        private long m_startTime;

        private GameWorld m_world;

        private List<AuthorityPlayer> m_players;

        // 帧缓存
        private FrameBuffer m_frameBuffer;

        // 计算fixedTime，fixedTime在客户端是动态调整的，会做时间膨胀缩放
        private FixedTimeCounter m_fixedTimeCounter;

        // 预测帧
        private int m_predictionFrame = -1;

        // 权威帧
        private int m_authorityFrame = -1;

        // 存档
        private Replay m_replay = new();

        public const int k_updateInterval = 50;
        public const int k_frameCountPerSecond = 1000 / k_updateInterval;
        public const int k_saveWorldFrameCount = 60 * k_frameCountPerSecond;

        public bool IsReplay { get; set; }
        
        public int SpeedMultiply { get; set; }

        public Room(C2DSMessageDispatcher messageDispatcher, int matchCount = 1) 
        {
            m_messageDispatcher = messageDispatcher;
            m_matchCount = matchCount;
            m_frameBuffer = new FrameBuffer(-1, k_frameCountPerSecond);
            m_players = new List<AuthorityPlayer>(m_matchCount);
        }

        public void Update()
        {
            m_gameTime.Update();
            long timeNow = m_gameTime.ServerFrameTime();

            int frame = m_predictionFrame + 1;
            if (timeNow < m_fixedTimeCounter.FrameTime(frame))
            {
                return;
            }

            OneFrameInputs oneFrameInputs = GetOneFrameMessage(frame);
            ++m_predictionFrame;

            OneFrameInputs sendInput = new();
            oneFrameInputs.CopyTo(sendInput);
            BroadCast(sendInput);

            //LSWorld lsWorld = self.LSWorld;
            //// 设置输入到每个LSUnit身上
            //LSUnitComponent unitComponent = lsWorld.GetComponent<LSUnitComponent>();
            //foreach (var kv in oneFrameInputs.Inputs)
            //{
            //    LSUnit lsUnit = unitComponent.GetChild<LSUnit>(kv.Key);
            //    LSInputComponent lsInputComponent = lsUnit.GetComponent<LSInputComponent>();
            //    lsInputComponent.LSInput = kv.Value;
            //}

            if (!IsReplay)
            {
                // 保存当前帧场景数据
                SaveLSWorld();
                Record(m_world.Frame);
            }

            m_world.Update();
        }

        //public GameWorld GetLSWorld(int frame)
        //{
        //    MemoryBuffer memoryBuffer = self.FrameBuffer.Snapshot(frame);
        //    memoryBuffer.Seek(0, SeekOrigin.Begin);
        //    GameWorld world = MongoHelper.Deserialize(typeof(LSWorld), memoryBuffer) as LSWorld;
        //    lsWorld.SceneType = sceneType;
        //    memoryBuffer.Seek(0, SeekOrigin.Begin);
        //    return lsWorld;
        //}

        private void SaveLSWorld()
        {
            //int frame = World.Frame;
            //MemoryBuffer memoryBuffer = FrameBuffer.Snapshot(frame);
            //memoryBuffer.Seek(0, SeekOrigin.Begin);
            //memoryBuffer.SetLength(0);

            //MongoHelper.Serialize(self.LSWorld, memoryBuffer);
            //memoryBuffer.Seek(0, SeekOrigin.Begin);

            //long hash = memoryBuffer.GetBuffer().Hash(0, (int)memoryBuffer.Length);

            //FrameBuffer.SetHash(frame, hash);
        }

        public void Record(int frame)
        {
            //if (frame > self.AuthorityFrame)
            //{
            //    return;
            //}

            //C2S.OneFrameInputs oneFrameInputs = self.FrameBuffer.FrameInputs(frame);
            //C2S.OneFrameInputs saveInput = new();
            //oneFrameInputs.CopyTo(saveInput);
            //self.Replay.FrameInputs.Add(saveInput);
            //if (frame % k_saveWorldFrameCount == 0)
            //{
            //    MemoryBuffer memoryBuffer = self.FrameBuffer.Snapshot(frame);
            //    byte[] bytes = memoryBuffer.ToArray();
            //    self.Replay.Snapshots.Add(bytes);
            //}
        }

        public void OnClientFrameMessage(ulong channelId, int rpcId, C2DS_FrameMessage req)
        {
            int clientFrame = req.Frame;
            FrameBuffer frameBuffer = m_frameBuffer;
            if (clientFrame % (1000 / k_updateInterval) == 0)
            {
                long nowFrameTime = m_fixedTimeCounter.FrameTime(clientFrame);
                int diffTime = (int)(nowFrameTime - m_gameTime.ServerFrameTime());
                m_messageDispatcher.Send(new DS2C_AdjustUpdateTime() { DiffTime = diffTime }, channelId, (ushort)C2DSMessageId.DS2C_AdjustUpdateTime);
            }

            // 小于AuthorityFrame，丢弃
            if (clientFrame < m_predictionFrame)
            {
                Log.Warning($"FrameMessage < AuthorityFrame discard: {clientFrame}");
                return;
            }

            // 大于AuthorityFrame + 10，丢弃
            if (clientFrame > m_predictionFrame + 10)
            {
                Log.Warning($"FrameMessage > AuthorityFrame + 10 discard: {clientFrame}");
                return;
            }

            OneFrameInputs oneFrameInputs = frameBuffer.FrameInputs(clientFrame);
            if (oneFrameInputs == null)
            {
                Log.Error($"FrameMessageHandler get frame is null: {clientFrame}, max frame: {frameBuffer.MaxFrame}");
                return;
            }
            oneFrameInputs.Inputs[req.ProfileId] = req.Input;
        }

        public bool AddPlayer(AuthorityPlayer player) 
        {
            bool found = false;
            foreach (AuthorityPlayer p in m_players)
            {
                if (p.ChannelId == player.ChannelId)
                {
                    found = true;
                    break;
                }
            }

            if (!found) 
            {
                return false;
            }

            m_players.Add(player);
            return true;
        }

        private OneFrameInputs GetOneFrameMessage(int frame)
        {
            OneFrameInputs oneFrameInputs = m_frameBuffer.FrameInputs(frame);
            m_frameBuffer.MoveForward(frame);

            if (oneFrameInputs.Inputs.Count == m_matchCount)
            {
                return oneFrameInputs;
            }

            OneFrameInputs preFrameInputs = null;
            if (m_frameBuffer.CheckFrame(frame - 1))
            {
                preFrameInputs = m_frameBuffer.FrameInputs(frame - 1);
            }

            // 有人输入的消息没过来，给他使用上一帧的操作
            foreach (AuthorityPlayer player in m_players)
            {
                string profileId = player.ProfileId;
                if (oneFrameInputs.Inputs.ContainsKey(profileId))
                {
                    continue;
                }

                if (preFrameInputs != null && preFrameInputs.Inputs.TryGetValue(profileId, out EntityInput input))
                {
                    // 使用上一帧的输入
                    oneFrameInputs.Inputs[profileId] = input;
                }
                else
                {
                    oneFrameInputs.Inputs[profileId] = new EntityInput();
                }
            }

            return oneFrameInputs;
        }

        private void BroadCast(OneFrameInputs inputs)
        {
            foreach (AuthorityPlayer player in m_players)
            {
                m_messageDispatcher.Send(new DS2C_OneFrameInputs() { FrameInputs = inputs }, player.ChannelId, (ushort)C2DSMessageId.DS2C_OneFrameInputs);
            }
        }
    }
}