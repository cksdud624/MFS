using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using AIState = Common.GameDefine.AIState;

namespace InGame.Component.Controller.AI
{
    /// <summary>제자리에서 대기하다가 일정 시간이 지나면 순찰로 전환</summary>
    public class AIIdleState : AIStateBase
    {
        //테이블로 차후 관리해야하는 부분
        private const float IdleDuration = 1.5f;

        private float _timer;

        public AIIdleState(StateMachine<AIState> stateMachine, InputContext inputContext, ObjectContext objectContext, ControllerAI controller)
            : base(stateMachine, inputContext, objectContext, controller)
        {
        }

        public override void OnEnter()
        {
            _timer = 0f;
            Controller.StopMove();
        }

        public override void OnFixedUpdate()
        {
            if (TryChangeToChase()) return;

            _timer += Time.fixedDeltaTime;
            if (_timer >= IdleDuration)
                StateMachine.ChangeState(AIState.Patrol);
        }
    }
}
