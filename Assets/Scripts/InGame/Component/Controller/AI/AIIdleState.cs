using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.Controller.AI
{
    /// <summary>제자리에서 대기하다가 일정 시간이 지나면 순찰로 전환</summary>
    public class AIIdleState : AIStateBase
    {
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
            //아직은.. 패트롤이 필요없기 때문에 일단 여기서 대기
            return;
            if (TryChangeToChase()) return;

            _timer += Time.fixedDeltaTime;
            if (_timer >= IdleDuration)
                StateMachine.ChangeState(AIState.Patrol);
        }
    }
}
