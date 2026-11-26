using System.Collections.Generic;
using UnityEngine;

namespace Common.Template.FSM
{
    public class StateMachine<TKey>
    {
        #region Fields
        private readonly Dictionary<TKey, IState> _states = new();

        public TKey PreviousKey { get; private set; }
        public TKey CurrentKey { get; private set; }
        public IState CurrentState { get; private set; }
        #endregion

        #region Public
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
            PreviousKey = CurrentKey;
            CurrentKey = key;
            CurrentState = next;
            CurrentState.OnEnter();
        }

        /// <summary>
        /// 지금 상태를 끝내고 어떤 상태에도 있지 않게 한다. 주인이 사라질 때 상태가 잡고 있던 작업(비동기 패턴 등)을 거두는 데 쓴다.
        /// 이미 멈춰 있으면 아무것도 하지 않는다
        /// </summary>
        public void Stop()
        {
            if (CurrentState == null) return;

            //먼저 비워둔다. OnExit 안에서 다시 ChangeState를 불러도 끝난 상태로 되돌아가지 않게 한다
            var current = CurrentState;
            CurrentState = null;
            current.OnExit();
        }

        public void OnUpdate() => CurrentState?.OnUpdate();
        public void OnFixedUpdate() => CurrentState?.OnFixedUpdate();
        #endregion
    }
}