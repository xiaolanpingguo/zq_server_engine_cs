using Pipelines.Sockets.Unofficial.Arenas;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ZQ
{
    public static class EnumEx<TEnum> where TEnum : unmanaged, Enum
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult To<TResult>(TEnum value) where TResult : unmanaged
        {
            unsafe
            {
                if (sizeof(TResult) > sizeof(TEnum))
                {
                    // We might be spilling in the stack by taking more bytes than value provides,
                    // alloc the largest data-type and 'cast' that instead.
                    TResult o = default;
                    *((TEnum*)&o) = value;
                    return o;
                }
                else
                {
                    return *(TResult*)&value;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TEnum From<TSource>(TSource value) where TSource : unmanaged
        {
            unsafe
            {
                if (sizeof(TEnum) > sizeof(TSource))
                {
                    // We might be spilling in the stack by taking more bytes than value provides,
                    // alloc the largest data-type and 'cast' that instead.
                    TEnum o = default;
                    *((TSource*)&o) = value;
                    return o;
                }
                else
                {
                    return *(TEnum*)&value;
                }
            }
        }
    }

    public class StateMachine
    {
        public interface IState
        {
            public void OnEnter();
            public void OnUpdate();
            public void OnExit();
        }

        public interface IStateChangeObserver
        {
            public void OnStateChanged(StateMachine machine, int previousStateId, int newStateId);
            public void OnStateMachineShutdown();
        }

        public Type StateType => m_stateType;

        private IState m_currentState = null;
        private string m_currentStateName;
        private int m_currentStateId;
        private readonly Type m_stateType;
        private readonly Dictionary<int, IState> m_states = new Dictionary<int, IState>();
        private readonly Dictionary<int, string> m_stateNames = new Dictionary<int, string>();
        private readonly List<IStateChangeObserver> m_observers = new List<IStateChangeObserver>();
        private readonly string m_name;

        private StateMachine(string name, Type stateType)
        {
            m_name = name;
            m_stateType = stateType;
            var enumVals = Enum.GetValues(stateType);
            foreach (Enum enumVal in enumVals)
            {
                m_stateNames[Convert.ToInt32(enumVal)] = enumVal.ToString();
            }
        }

        public static unsafe StateMachine Create<T>(string name) where T : unmanaged, Enum
        {
            if (sizeof(T) != 4)
            {
                throw new Exception("State machine only supports int enums");
            }

            return new StateMachine(name, typeof(T));
        }

        public unsafe void Add<T>(T id, IState state) where T : unmanaged, Enum
        {
            if (sizeof(T) != 4)
            {
                throw new Exception("State machine only supports int enums");
            }
            m_states.Add(*(int*)(&id), state);
        }

        public unsafe T CurrentState<T>() where T : unmanaged, Enum
        {
            if (sizeof(T) != 4)
            {
                throw new Exception("State machine only supports int enums");
            }
            var currentStateId = m_currentStateId;
            return *(T*)(&currentStateId);
        }

        public void Update()
        {
            m_currentState.OnUpdate();
        }

        public void Shutdown()
        {
            foreach (var observer in m_observers)
            {
                observer.OnStateMachineShutdown();
            }

            m_observers.Clear();
            m_currentState?.OnExit();
            m_currentState = null;
        }

        public void SwitchTo(int stateId)
        {
            if (!m_states.ContainsKey(stateId))
            {
                Log.Error($"[StateMachine] Trying to switch to unknown state {stateId.ToString()} ");
                return;
            }

            var newStatename = m_stateNames[stateId];
            if (m_currentStateId == stateId)
            {
                Log.Error($"[StateMachine] \"Trying to switch to {newStatename} but that is already current state\"");
                return;
            }

            var newState = m_states[stateId];
            Log.Info(string.Format("[StateMachine<{0}>] Switching state: {1} -> {2}", m_name, m_currentState != null ? m_currentStateName : "null",newStatename));

            m_currentState?.OnExit();
            newState.OnEnter();

            var oldStateId = m_currentStateId;
            m_currentState = newState;
            m_currentStateId = stateId;
            m_currentStateName = newStatename;

            foreach (var observer in m_observers)
            {
                observer.OnStateChanged(this, oldStateId, stateId);
            }
        }

        public string GetStateName(int stateId)
        {
            return m_stateNames.TryGetValue(stateId, out var name) ? name : null;
        }

        public void AddObserver(IStateChangeObserver observer)
        {
            m_observers.Add(observer);
        }
    }

    public class StateMachine<T> where T : unmanaged, Enum
    {
        public delegate void OnStateChanges(T from, T to);

        private State m_currentState;
        private Dictionary<int, State> m_states = new();
        private readonly string m_name;
        private OnStateChanges m_onStateChanges;

        public delegate void StateFunc();

        public StateMachine(OnStateChanges onStateChanges = null)
        {
            m_onStateChanges = onStateChanges;
        }

        public StateMachine(string name, OnStateChanges onStateChanges = null)
        {
            m_name = name;
            m_onStateChanges = onStateChanges;
        }

        public void Add(T id, StateFunc enter, StateFunc update, StateFunc leave)
        {
            m_states.Add(EnumEx<T>.To<int>(id), new State(id, enter, update, leave));
        }

        public T CurrentState()
        {
            return m_currentState?.Id ?? default;
        }

        public void Update()
        {
            m_currentState?.Update?.Invoke();
        }

        public void Shutdown()
        {
            m_currentState?.Leave?.Invoke();
            m_currentState = null;
        }

        public void SwitchTo(T state)
        {
            int stateAsInt = EnumEx<T>.To<int>(state);
            if (!m_states.ContainsKey(stateAsInt))
            {
                Log.Error($"[StateMachine] Trying to switch to unknown state {stateAsInt.ToString()} ");
                return;
            }

            if (m_currentState.Id.Equals(state))
            {
                Log.Error($"[StateMachine] Trying to switch to {state.ToString()} but that is already current state\"");
                return;
            }

            var newState = m_states[stateAsInt];
            Log.Info(string.Format("[StateMachine<{0}>] Switching state: {1} -> {2}", m_name, m_currentState != null ? m_currentState.Id.ToString() : "null", state.ToString()));

            if (m_currentState != null && m_currentState.Leave != null)
            {
                m_currentState.Leave();
            }

            var previous = m_currentState?.Id ?? default;
            m_currentState = newState;

            m_onStateChanges?.Invoke(previous, newState.Id);

            newState.Enter?.Invoke();
        }

        class State
        {
            public State(T id, StateFunc enter, StateFunc update, StateFunc leave)
            {
                Id = id;
                Enter = enter;
                Update = update;
                Leave = leave;
            }

            public T Id;
            public StateFunc Enter;
            public StateFunc Update;
            public StateFunc Leave;
        }
    }
}
