using C2S;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Threading.Channels;
using C2DS;
using Google.Protobuf;
using System.Net;


namespace ZQ
{
    public class Room
    {
        private enum State
        {
            WaittingForPlayerJoin,
            WaittingForFirstInput,
            Playing,
            End
        }

        public int RoomId { get;}
        public int CurrentPlayerCount => m_players.Count;

        private readonly C2DedicatedModule m_c2dedicatedComponent;
        private readonly C2DSMessageDispatcher m_messageDispatcher;
        private readonly Dictionary<ushort, Action<ushort, int, int, IMessage?>> m_messageHandlers = new();
        private readonly StateMachine<State> m_stateMachine;

        private GameTime m_gameTime;
        private readonly int m_playerCount = 1;

        private Dictionary<int, Player> m_players = new();

        private FrameBuffer m_frameBuffer;
        private FixedTimeCounter m_fixedTimeCounter = null!;

        private const int k_maxPresendTickCount= 10;
        private const int k_frameRate = 20;
        private const double k_updateIntervalMs = 1000 / k_frameRate;

        private int m_tick = 0;

        public Room(int roomId, C2DedicatedModule c2dedicatedComponent, C2DSMessageDispatcher messageDispatcher, int playerMaxCount = 1) 
        {
            RoomId = roomId;
            m_c2dedicatedComponent = c2dedicatedComponent;
            m_messageDispatcher = messageDispatcher;
            m_playerCount = playerMaxCount;
            m_gameTime = new GameTime();
            m_frameBuffer = new FrameBuffer(-1, k_frameRate);

            RegisterMessage((ushort)C2DS_MSG_ID.IdC2DsPingReq, typeof(C2DSPingReq), OnClientPing);
            RegisterMessage((ushort)C2DS_MSG_ID.IdC2DsClientInputReq, typeof(C2DSClientInputReq), OnClientInput);

            m_stateMachine = new StateMachine<State>();
            m_stateMachine.Add(State.WaittingForPlayerJoin, null, UpdatePlayerJoin, null);
            m_stateMachine.Add(State.WaittingForFirstInput, null, UpdatePlayerFirstInputs, null);
            m_stateMachine.Add(State.Playing, EnterPlaying, UpdatePlaying, null);
            m_stateMachine.Add(State.End, null, () => { }, null);
            m_stateMachine.SwitchTo(State.WaittingForPlayerJoin);
        }

        public void Update()
        {
            m_stateMachine.Update();
        }

        public void OnMessage(ushort messageId, int connectionId, int rpcId, IMessage? message)
        {
            Player? player = GetPlayer(connectionId);
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

        public bool AddPlayer(Player player)
        {
            if (m_players.ContainsKey(player.ConnectionId))
            {
                return false;
            }

            m_players[player.ConnectionId] = player;
            return true;
        }

        public void OnPlayersDisconnected(int connectionId)
        {
            if (m_players.ContainsKey(connectionId))
            {
                Log.Info($"a player has disconnected to room , id:{connectionId}, profileid:{m_players[connectionId].ProfileId}");
                m_players.Remove(connectionId);
            }
        }

        private void UpdatePlayerJoin()
        {
            if (CurrentPlayerCount >= m_playerCount)
            {
                DS2CStartGameReq res = new DS2CStartGameReq();
                foreach (var kv in m_players)
                {
                    Player player = kv.Value;
                    C2DS.PlayerInfo playerInfo = new PlayerInfo();
                    playerInfo.ProfileId = player.ProfileId;
                    res.Players.Add(playerInfo);
                }

                foreach (var kv in m_players)
                {
                    Player player = kv.Value;
                    m_messageDispatcher.Send(res, player.ConnectionId, (ushort)C2DS_MSG_ID.IdDs2CStartGameReq);
                }

                m_stateMachine.SwitchTo(State.Playing);
            }
        }

        private void UpdatePlayerFirstInputs()
        {
            // check if all player inputs received in tick 0
            var frame = m_frameBuffer.GetFrame(m_tick);
            if (frame.Inputs.Count == m_playerCount) 
            {
                m_stateMachine.SwitchTo(State.Playing);
            }
        }

        private void EnterPlaying()
        {
            m_fixedTimeCounter = new FixedTimeCounter(m_gameTime.StampNow(), 0, (int)k_updateIntervalMs);
        }

        private void UpdatePlaying()
        {
            m_gameTime.Update();

            long timeNow = m_gameTime.StampNow();
            int nextTick = m_tick + 1;
            if (timeNow < m_fixedTimeCounter.FrameTime(nextTick))
            {
                return;
            }

            m_tick = nextTick;
            ServerFrame frame = GetOneFrameMessage(m_tick);
            BroadcastInput(frame);
            m_tick++;
        }

        private void BroadcastInput(ServerFrame frame)
        {
            var msg = new DS2CServerFrameReq();
            C2DS.ServerFrame msgFrame = new C2DS.ServerFrame();
            msgFrame.Tick = frame.Tick;
            foreach (var kv in frame.Inputs)
            {
                var frameInput = kv.Value;
                C2DS.PlayerInput playerInput = new C2DS.PlayerInput();
                playerInput.Tick = frame.Tick;
                playerInput.Horizontal = frameInput.Horizontal;
                playerInput.Vertical = frameInput.Vertical;
                playerInput.Button = frameInput.Button;
                playerInput.ProfileId = kv.Key;
                msgFrame.PlayerInputs.Add(playerInput);
            }

            msg.ServrFrame = msgFrame;
            foreach(var kv in m_players)
            {
                m_messageDispatcher.Send(msg, kv.Value.ConnectionId, (ushort)C2DS_MSG_ID.IdDs2CServerFrameReq);
            }
        }

        private ServerFrame GetOneFrameMessage(int tick)
        {
            ServerFrame frame = m_frameBuffer.GetFrame(tick);
            m_frameBuffer.MoveForward(tick);

            // we received all players input
            if (frame.Inputs.Count == m_playerCount)
            {
                return frame;
            }

            ServerFrame preFrameInputs = null;
            if (m_frameBuffer.CheckFrame(tick - 1))
            {
                preFrameInputs = m_frameBuffer.GetFrame(tick - 1);
            }

            // we haven't received some player's msg 
            foreach (var kv in m_players)
            {
                string profileId = kv.Value.ProfileId;
                if (frame.Inputs.ContainsKey(profileId))
                {
                    continue;
                }

                // use last input 
                if (preFrameInputs != null && preFrameInputs.Inputs.TryGetValue(profileId, out PlayerInput input))
                {
                    frame.Inputs[profileId] = input;
                }
                else
                {
                    frame.Inputs[profileId] = PlayerInput.EmptyInput;
                }
            }

            return frame;
        }

        private bool RegisterMessage(ushort messageId, Type type, Action<ushort, int, int, IMessage?> handler)
        {
            m_c2dedicatedComponent.RegisterMessage(messageId, type);
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
            res.ServerTime = TimeHelper.TimeStampNowMs();
            m_messageDispatcher.Response(res, connectionId, (ushort)C2DS_MSG_ID.IdC2DsPingRes, rpcId);
        }

        private void OnClientInput(ushort messageId, int connectionId, int rpcId, IMessage? message)
        {
            if (message is not C2DSClientInputReq req)
            {
                Log.Error($"OnClientInput error: cannot convert message to C2DSClientInputReq");
                return;
            }

            string profileId = req.PlayerInput.ProfileId;
            int clientTick = req.PlayerInput.Tick;
            SendAdjustUpdateTimeMsg(connectionId, clientTick);

            if (clientTick < m_tick)
            {
                return;
            }

            if (clientTick > m_tick + k_maxPresendTickCount)
            {
                Log.Warning($"clientTick > AuthorityFrame + {k_maxPresendTickCount}, discard.");
                return;
            }

            ServerFrame serverframe = m_frameBuffer.GetFrame(clientTick);
            if (serverframe == null)
            {
                Log.Error($"serverframe is null, clientTick: {clientTick}.");
                return;
            }

            PlayerInput newInput = default;
            newInput.Vertical = req.PlayerInput.Vertical;
            newInput.Horizontal = req.PlayerInput.Horizontal;
            newInput.Button = req.PlayerInput.Button;
            serverframe.Inputs[profileId] = newInput;
        }

        private void SendAdjustUpdateTimeMsg(int connectionId, int clientTick)
        {
            // send every second
            if (clientTick % k_frameRate == 0)
            {
                long nowFrameTime = m_fixedTimeCounter.FrameTime(clientTick);
                int diffTime = (int)(nowFrameTime - m_gameTime.StampNow());

                DS2CAdjustUpdateTimeReq req = new DS2CAdjustUpdateTimeReq();
                req.DiffTime = diffTime;
                m_messageDispatcher.Send(req, connectionId, (ushort)C2DS_MSG_ID.IdDs2CAdjustUpdateTimeReq);
            }
        }

        private Player? GetPlayer(int connectionId)
        {
            if (m_players.TryGetValue(connectionId, out var playerEntity))
            {
                return playerEntity;
            }

            return null;
        }
    }
}