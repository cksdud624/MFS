using System.Collections.Generic;
using UnityEngine;

namespace Common.Template.FSM
{
    public class StateMachine<TKey>
    {
        private readonly Dictionary<TKey, IState> _states = new();

        public TKey CurrentKey { get; private set; }
        public IState CurrentState { get; private set; }

        public void AddState(TKey key, IState state) => _states[key] = state;

        public void ChangeState(TKey key)
        {
            if (!_states.TryGetValue(key, out IState next))
            {
                Debug.LogWarning($"State {key} is not registered.");
                return;
            }
            if (CurrentState == next) return;

            CurrentState?.OnExit();
            CurrentKey = key;
            CurrentState = next;
            CurrentState.OnEnter();
        }

        public void OnUpdate() => CurrentState?.OnUpdate();
        public void OnFixedUpdate() => CurrentState?.OnFixedUpdate();
    }
}
