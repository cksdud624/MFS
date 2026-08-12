using Common.Template.FSM;
using InGame.Context;
using AIState = Common.GameDefine.AIState;

namespace InGame.Component.Controller.AI
{
    /// <summary>
    /// 대상을 향해 이동한다.
    /// 공격 거리에 들어오면 멈추고, 벽이 막고 있으면 점프해서 넘으려 한다.
    /// </summary>
    public class AIChaseState : AIStateBase
    {
        public AIChaseState(StateMachine<AIState> stateMachine, InputContext inputContext, ObjectContext objectContext, ControllerAI controller)
            : base(stateMachine, inputContext, objectContext, controller)
        {
        }

        public override void OnExit()
        {
            Controller.StopMove();
        }

        public override void OnFixedUpdate()
        {
            //대상을 잃었거나 너무 멀어지면 추격을 포기한다
            if (!Controller.TryGetTargetDistance(out float distance) || distance > LoseRange)
            {
                StateMachine.ChangeState(AIState.Idle);
                return;
            }

            if (distance <= AttackRange)
            {
                //TODO : 공격 액션 연결
                Controller.StopMove();
                return;
            }

            float directionX = Controller.GetTargetDirectionX();
            if (directionX == 0f)
            {
                Controller.StopMove();
                return;
            }

            //벽은 점프로 넘어보고, 낭떠러지 앞에서는 멈춘다
            if (Controller.IsWallAhead(directionX))
            {
                Controller.SetMoveInput(directionX);
                if (ObjectContext.IsGrounded)
                    Controller.RequestJump();
                return;
            }

            if (Controller.IsLedgeAhead(directionX))
            {
                //방향은 대상 쪽을 본 채로 멈춘다
                Controller.SetMoveInput(directionX);
                Controller.StopMove();
                return;
            }

            Controller.SetMoveInput(directionX);
        }
    }
}
