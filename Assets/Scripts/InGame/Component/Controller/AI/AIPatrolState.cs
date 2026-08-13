using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.Controller.AI
{
    /// <summary>
    /// 바라보는 방향으로 순찰한다.
    /// 벽이나 낭떠러지를 만나면 반대로 돌아서고, 일정 시간이 지나면 대기로 돌아간다.
    /// </summary>
    public class AIPatrolState : AIStateBase
    {
        private float _timer;
        private float _directionX;

        public AIPatrolState(StateMachine<AIState> stateMachine, InputContext inputContext, ObjectContext objectContext, ControllerAI controller)
            : base(stateMachine, inputContext, objectContext, controller)
        {
        }

        public override void OnEnter()
        {
            _timer = 0f;
            _directionX = ObjectContext.Direction == Direction.Left ? -1f : 1f;
        }

        public override void OnExit()
        {
            Controller.StopMove();
        }

        public override void OnFixedUpdate()
        {
            if (TryChangeToChase()) return;

            _timer += Time.fixedDeltaTime;
            if (_timer >= PatrolDuration)
            {
                StateMachine.ChangeState(AIState.Idle);
                return;
            }

            //진행할 수 없으면 한 번 돌아서고, 반대쪽도 막혀 있으면 대기로 돌아간다
            if (!CanMove(_directionX))
            {
                _directionX = -_directionX;
                if (!CanMove(_directionX))
                {
                    StateMachine.ChangeState(AIState.Idle);
                    return;
                }
            }

            Controller.SetMoveInput(_directionX);
        }
    }
}
