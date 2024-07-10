using C2S;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Threading.Channels;
using C2DS;
using Google.Protobuf;
using System.Transactions;
using Microsoft.VisualBasic;
using MemoryPack;
using MongoDB.Driver.Core.Connections;
using System.Numerics;


namespace ZQ
{
    public struct PlayerInput
    {
        public int Horizontal;
        public int Vertical;
        public int Button;
    }

    public class ServerFrame
    {
        public Dictionary<string, PlayerInput> Inputs = new();
        public int Tick;
    }

    public class Room
    {
        private enum State
        {
            WaittingForPlayer,
            Playing,
            End
        }

        public int CurrentPlayerCount => m_players.Count;

        private readonly C2DedicatedModule m_c2dedicatedComponent;
        private readonly C2DSMessageDispatcher m_messageDispatcher;
        private readonly Dictionary<ushort, Action<ushort, int, int, IMessage?>> m_messageHandlers = new();
        private readonly StateMachine<State> m_stateMachine;

        private GameTime m_gameTime;
        private readonly int m_playerMaxCount = 1;

        private GameWorld m_world;

        private Dictionary<int, Player> m_players = new();
        private Dictionary<string, int> _playerIdDic = new();

        private FrameBuffer m_frameBuffer;

        private int m_authorityFrame = -1;

        public const int k_frameRate = 50;
        public const double k_updateIntervalMs = k_frameRate / 1000.0f;

        private DateTime m_lastUpdateTimeStamp;
        private DateTime m_startUpTimeStamp;
        private double m_deltaTime;
        private int m_tick = 0;

        private List<ServerFrame?> m_allHistoryFrames = new List<ServerFrame?>();

        public Room(C2DedicatedModule c2dedicatedComponent, C2DSMessageDispatcher messageDispatcher, int playerMaxCount = 1) 
        {
            m_c2dedicatedComponent = c2dedicatedComponent;
            m_messageDispatcher = messageDispatcher;
            m_playerMaxCount = playerMaxCount;
            m_gameTime = new GameTime(k_frameRate);
            m_frameBuffer = new FrameBuffer(-1, k_frameRate);

            RegisterMessage((ushort)C2DS_MSG_ID.IdC2DsPingReq, typeof(C2DSPingReq), OnClientPing);
            RegisterMessage((ushort)C2DS_MSG_ID.IdC2DsClientInputReq, typeof(C2DSClientInputReq), OnClientInput);

            m_stateMachine = new StateMachine<State>();
            m_stateMachine.Add(State.WaittingForPlayer, null, UpdateWaittingForPlayer, null);
            m_stateMachine.Add(State.Playing, null, UpdatePlaying, null);
            m_stateMachine.Add(State.End, null, () => { }, null);
            m_stateMachine.SwitchTo(State.WaittingForPlayer);

            m_world = new GameWorld();
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


        private void UpdateWaittingForPlayer()
        {
            if (CurrentPlayerCount >= m_playerMaxCount)
            {
                foreach (var kv in m_players)
                {
                    Player player = kv.Value;
                    DS2CStartGameReq res = new DS2CStartGameReq();
                    res.PlayerCount = m_playerMaxCount;
                    m_messageDispatcher.Send(res, player.ConnectionId, (ushort)C2DS_MSG_ID.IdDs2CStartGameReq);
                }

                m_stateMachine.SwitchTo(State.Playing);
            }
        }

        private void UpdatePlaying()
        {
            m_gameTime.Update();

            var now = DateTime.Now;
            m_deltaTime = (now - m_lastUpdateTimeStamp).TotalSeconds;
            if (m_deltaTime <= k_updateIntervalMs)
            {
                return;
            }
            m_lastUpdateTimeStamp = now;

            BroadcastInput();
            m_tick++;
        }

        private void BroadcastInput()
        {
            var msg = new DS2CServerFrameReq();
            int count = m_tick < 2 ? m_tick + 1 : 3;
            if (count > m_allHistoryFrames.Count)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                ServerFrame frame = m_allHistoryFrames[m_tick - i];
                if (frame == null)
                {
                    continue;
                }

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
                    msgFrame.PlayerInputs.Add(playerInput);
                }

                msg.ServrFrame.Add(msgFrame);
            }

            foreach(var kv in m_players)
            {
                m_messageDispatcher.Send(msg, kv.Value.ConnectionId, (ushort)C2DS_MSG_ID.IdDs2CServerFrameReq);
            }
        }

        ServerFrame GetOrCreateFrame(int tick)
        {
            var frameCount = m_allHistoryFrames.Count;
            if (frameCount <= tick)
            {
                var count = tick - m_allHistoryFrames.Count + 1;
                for (int i = 0; i < count; i++)
                {
                    m_allHistoryFrames.Add(null);
                }
            }

            if (m_allHistoryFrames[tick] == null)
            {
                m_allHistoryFrames[tick] = new ServerFrame() { Tick = tick };
            }

            var frame = m_allHistoryFrames[tick];
            if (frame == null)
            {
                return null;
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

            int clientTick = req.PlayerInput.Tick;
            if (clientTick < m_tick)
            {
                return;
            }

            string profileId = req.PlayerInput.ProfileId;
            int index = GetPlayerIndexByProfileId(profileId);
            if (index == -1)
            {
                return;
            }

            var frame = GetOrCreateFrame(clientTick);
            var playerInputs = frame.Inputs;
            PlayerInput newInput = default;
            newInput.Vertical = req.PlayerInput.Vertical;
            newInput.Horizontal = req.PlayerInput.Horizontal;
            newInput.Button = req.PlayerInput.Button;

            if (playerInputs.ContainsKey(profileId))
            {
                playerInputs[profileId] = newInput;
            }
            else
            {
                playerInputs.Add(profileId, newInput);
            }
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

        private Player? GetPlayer(int connectionId)
        {
            if (m_players.TryGetValue(connectionId, out var playerEntity))
            {
                return playerEntity;
            }

            return null;
        }

        private int GetPlayerIndexByProfileId(string profileId)
        {
            if (_playerIdDic.TryGetValue(profileId, out var id))
            {
                return id;
            }

            return -1;
        }
    }
}