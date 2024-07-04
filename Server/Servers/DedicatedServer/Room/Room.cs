using C2S;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Threading.Channels;
using C2DS;
using Google.Protobuf;
using static MongoDB.Driver.WriteConcern;


namespace ZQ
{
    public class Room
    {
        private enum ERoomState
        {
            WaittingForPlayer,
            Playing,
            End
        }

        private readonly C2DedicatedModule m_c2dedicatedComponent;
        private readonly C2DSMessageDispatcher m_messageDispatcher;
        private readonly Dictionary<ushort, Action<ushort, int, int, IMessage?>> m_messageHandlers = new();
        private readonly StateMachine<ERoomState> m_stateMachine;

        private GameTime m_gameTime;
        private readonly int m_playerMaxCount = 1;
        private int m_currentCount = 0;

        private GameWorld m_world;

        private Dictionary<int, AuthorityPlayer> m_players = new();

        private FrameBuffer m_frameBuffer;

        private int m_authorityFrame = -1;

        public const int k_updateInterval = 50;
        public const int k_frameCountPerSecond = 1000 / k_updateInterval;

        public Room(C2DedicatedModule c2dedicatedComponent, C2DSMessageDispatcher messageDispatcher, int playerMaxCount = 1) 
        {
            m_c2dedicatedComponent = c2dedicatedComponent;
            m_messageDispatcher = messageDispatcher;
            m_playerMaxCount = playerMaxCount;
            m_gameTime = new GameTime(k_updateInterval);
            m_frameBuffer = new FrameBuffer(-1, k_frameCountPerSecond);

            RegisterMessage((ushort)C2DS_MSG_ID.IdC2DsPingReq, typeof(C2DSPingReq), OnClientPing);
            RegisterMessage((ushort)C2DS_MSG_ID.IdC2DsJoinServerReq, typeof(C2DSJoinServerReq), OnJoinServer);
            RegisterMessage((ushort)C2DS_MSG_ID.IdC2DsClientInputReq, typeof(C2DSClientInputReq), OnClientInput);

            m_stateMachine = new StateMachine<ERoomState>();
            m_stateMachine.Add(ERoomState.WaittingForPlayer, null, UpdatePlayrLoading, null);
            m_stateMachine.Add(ERoomState.Playing, null, UpdatePlaying, null);
            m_stateMachine.Add(ERoomState.End, null, () => { }, null);
            m_stateMachine.SwitchTo(ERoomState.WaittingForPlayer);

            m_world = new GameWorld();
        }

        public void Update()
        {
            m_stateMachine.Update();
        }

        public void OnMessage(ushort messageId, int connectionId, int rpcId, IMessage? message)
        {
            AuthorityPlayer? player = GetPlayer(connectionId);
            if (player == null)
            {
                return;
            }

            if (m_messageHandlers.TryGetValue(messageId, out var handler))
            {
                handler?.Invoke(messageId, connectionId, rpcId, message);
            }
        }

        public int PlayersCount()
        {
            return m_players.Count;
        }


        private void UpdatePlayrLoading()
        {

        }

        private void UpdatePlaying()
        {
            m_gameTime.Update();
            long timeNow = m_gameTime.FrameTimeNow;

            int nextFrame = m_authorityFrame + 1;
            if (timeNow < m_gameTime.FrameTime(nextFrame))
            {
                return;
            }

            m_authorityFrame = nextFrame;

            //OneFrameInputs oneFrameInputs = GetOneFrameMessage(nextFrame);
            //OneFrameInputs sendInput = new();
            //oneFrameInputs.CopyTo(sendInput);
            //BroadCast(sendInput);

            m_world.Update();
        }

        private bool RegisterMessage(ushort messageId, Type type, Action<ushort, int, int, IMessage?> handler)
        {
            m_c2dedicatedComponent.RegisterRoomMessage(messageId, type);
            m_messageHandlers[messageId] = handler;

            return true;
        }

        private void OnClientPing(ushort messageId, int connectionId, int rpcId, IMessage? message)
        {
            if (message is not C2DSPingReq req)
            {
                Log.Error($"LSRoom error: cannot convert message to C2DSPingReq");
                return;
            }

            C2DSPingRes res = new C2DSPingRes();
            res.ClientTime = req.ClientTime;
            res.ProfileId = req.ProfileId;
            res.ServerTime = m_gameTime.Now();
            m_messageDispatcher.Response(res, connectionId, (ushort)C2DS_MSG_ID.IdC2DsPingRes, rpcId);
        }

        private void OnJoinServer(ushort messageId, int connectionId, int rpcId, IMessage? message)
        {
            if (message is not C2DSJoinServerReq req)
            {
                Log.Error($"OnJoinServer error: cannot convert message to C2DSJoinServerReq");
                return;
            }

            string profileId = req.ProfileId;
            Log.Info($"a client has joined the server,connectionId:{connectionId}, profileId:{profileId}.");

            {
                C2DSJoinServerRes res = new C2DSJoinServerRes();
                res.ErrorCode = C2DS_ERROR_CODE.Success;
                res.PlayerId = 0;
                m_messageDispatcher.Response(res, connectionId, (ushort)C2DS_MSG_ID.IdC2DsJoinServerRes, rpcId);
            }

            {
                m_currentCount++;
                if (m_currentCount >= m_playerMaxCount)
                {
                    DS2CStartGameReq res = new DS2CStartGameReq();
                    res.PlayerCount = m_playerMaxCount;
                    m_messageDispatcher.Response(res, connectionId, (ushort)C2DS_MSG_ID.IdDs2CStartGameReq, rpcId);
                }
            }
        }

        private void OnClientInput(ushort messageId, int connectionId, int rpcId, IMessage? message)
        {

        }

        private void OnLoadingProgress(ushort messageId, int connectionId, int rpcId, IMessage? message)
        {
            //if (message is not C2DS_LoadingProgressReq req)
            //{
            //    Log.Error($"LSRoom error: cannot conver message to C2DS_LoadingProgressReq");
            //    return;
            //}

            //if (m_stateMachine.CurrentState() != ERoomState.WaittingForPlayerLoading)
            //{
            //    return;
            //}

            //AuthorityPlayer? player = GetPlayer(channelId);
            //if (player == null)
            //{
            //    return;
            //}

            //bool startGame = true;
            //player.Progress = req.Progress;
            //foreach (var kv in m_players)
            //{
            //    Log.Info($"OnLoadingProgress, player channelId:{channelId}, Progress{player.Progress}");
            //    if (player.Progress != 100)
            //    {
            //        startGame = false;
            //    }
            //}

            //if (startGame)
            //{
            //    m_stateMachine.SwitchTo(ERoomState.Playing);
            //}
        }

        private void OnFrameMessage(ushort messageId, ulong channelId, int rpcId, object message)
        {
            //if (message is not C2DS_FrameMessage req)
            //{
            //    Log.Error($"LSRoom error: cannot conver message to C2DS_FrameMessage");
            //    return;
            //}

            //if (m_stateMachine.CurrentState() != ERoomState.Playing)
            //{
            //    return;
            //}

            //int clientFrame = req.Frame;
            //FrameBuffer frameBuffer = m_frameBuffer;
            //if (clientFrame % (1000 / k_updateInterval) == 0)
            //{
            //    long nowFrameTime = m_gameTime.FrameTime(clientFrame);
            //    int diffTime = (int)(nowFrameTime - m_gameTime.FrameTimeNow);
            //    m_messageDispatcher.Send(new DS2C_AdjustUpdateTime() { DiffTime = diffTime }, channelId, (ushort)C2DSMessageId.DS2C_AdjustUpdateTime);
            //}

            //if (clientFrame < m_authorityFrame)
            //{
            //    Log.Warning($"FrameMessage < AuthorityFrame discard: {clientFrame}");
            //    return;
            //}

            //if (clientFrame > m_authorityFrame + 10)
            //{
            //    Log.Warning($"FrameMessage > AuthorityFrame + 10 discard: {clientFrame}");
            //    return;
            //}

            //OneFrameInputs oneFrameInputs = frameBuffer.FrameInputs(clientFrame);
            //if (oneFrameInputs == null)
            //{
            //    Log.Error($"FrameMessageHandler get frame is null: {clientFrame}, max frame: {frameBuffer.MaxFrame}");
            //    return;
            //}

            //oneFrameInputs.Inputs[req.ProfileId] = req.Input;
        }

        public bool AddPlayer(AuthorityPlayer player) 
        {
            if (m_players.ContainsKey(player.ConnectionId))
            {
                return false;
            }

            m_players[player.ConnectionId] = player;
            return true;
        }

        private AuthorityPlayer? GetPlayer(int connectionId)
        {
            if (m_players.TryGetValue(connectionId, out var playerEntity))
            {
                return playerEntity;
            }

            return null;
        }

        //private OneFrameInputs GetOneFrameMessage(int frame)
        //{
        //    //OneFrameInputs oneFrameInputs = m_frameBuffer.FrameInputs(frame);
        //    //m_frameBuffer.MoveForward(frame);

        //    //if (oneFrameInputs.Inputs.Count == m_matchCount)
        //    //{
        //    //    return oneFrameInputs;
        //    //}

        //    //// some of players's message has not received in this frame
        //    //// so use last frame input instead
        //    //OneFrameInputs? preFrameInputs = null;
        //    //if (m_frameBuffer.CheckFrame(frame - 1))
        //    //{
        //    //    preFrameInputs = m_frameBuffer.FrameInputs(frame - 1);
        //    //}

        //    //foreach (var kv in m_players)
        //    //{
        //    //    AuthorityPlayer player = kv.Value;
        //    //    string profileId = player.ProfileId;
        //    //    if (oneFrameInputs.Inputs.ContainsKey(profileId))
        //    //    {
        //    //        continue;
        //    //    }

        //    //    if (preFrameInputs != null && preFrameInputs.Inputs.TryGetValue(profileId, out EntityInput input))
        //    //    {
        //    //        oneFrameInputs.Inputs[profileId] = input;
        //    //    }
        //    //    else
        //    //    {
        //    //        oneFrameInputs.Inputs[profileId] = new EntityInput();
        //    //    }
        //    //}

        //    //return oneFrameInputs;
        //    return null!;
        //}

        //private void BroadCast(OneFrameInputs inputs)
        //{
        //    //foreach (var kv in m_players)
        //    //{
        //    //    AuthorityPlayer player = kv.Value;
        //    //    m_messageDispatcher.Send(new DS2C_OneFrameInputs() { FrameInputs = inputs }, player.ChannelId, (ushort)C2DSMessageId.DS2C_OneFrameInputs);
        //    //}
        //}
    }
}