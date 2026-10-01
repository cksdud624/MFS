using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.Controller.AI
{
    /// <summary>
    /// 공격 거리 안의 대상을 공격한다.
    /// 제자리에서 대상 쪽으로 돌아서서 누르고, 대상이 거리 안에 있는 동안은 계속 눌러서 콤보를 끝까지 잇는다.
    /// 콤보가 끝나면 잠시 쉬었다가, 대상이 아직 거리 안이면 다시 공격하고 벗어났으면 추격으로 돌아간다.
    /// </summary>
    public class AIAttackState : AIStateBase
    {
        //다음 공격을 시작하기까지 남은 시간
        private float _cooldown;

        public AIAttackState(StateMachine<AIState> stateMachine, InputContext inputContext, ObjectContext objectContext, ControllerAI controller)
            : base(stateMachine, inputContext, objectContext, controller)
        {
        }

        public override void OnEnter()
        {
            _cooldown = 0f;
            Controller.StopMove();
        }

        public override void OnFixedUpdate()
        {
            //대상을 잃었거나 너무 멀어지면 추격도 포기한다
            if (!Controller.TryGetTargetDistance(out float distance) || distance > LoseRange)
            {
                StateMachine.ChangeState(AIState.Idle);
                return;
            }

            bool inRange = distance <= AttackRange;

            //휘두르는 중이면 대상이 거리 안에 있는 동안 다음 단계를 예약해둔다.
            //쉬는 시간은 콤보가 끝난 뒤부터 센다
            if (ObjectContext.IsAttacking)
            {
                if (inRange)
                    Controller.RequestAttack();
                _cooldown = AIAttackInterval;
                return;
            }

            if (_cooldown > 0f)
            {
                _cooldown -= Time.fixedDeltaTime;
                return;
            }

            if (!inRange)
            {
                StateMachine.ChangeState(AIState.Chase);
                return;
            }

            Controller.FaceTarget();
            Controller.RequestAttack();
            //공격이 나가지 못했더라도(대시 중, 공격 없는 오브젝트 등) 매 틱 누르지 않도록 쉬는 시간을 건다
            _cooldown = AIAttackInterval;
        }
    }
}
