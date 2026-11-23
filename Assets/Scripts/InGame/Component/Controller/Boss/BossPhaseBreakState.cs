using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 페이즈 전환 중. 체력바 하나가 다 깎여서 다음 체력바로 넘어가는 동안이다.
    /// 이 동안 보스는 맞지 않고, 구석에서 끝났더라도 다음 페이즈를 가운데에서 시작하도록 전투 구역 가운데로 간다.
    /// 플레이어에게는 숨을 고르고 자리를 다시 잡을 시간이다
    /// </summary>
    public class BossPhaseBreakState : BossStateBase
    {
        #region Fields
        private float _elapsed;
        //가서 설 자리(월드 x)
        private float _targetX;
        #endregion

        #region Init
        public BossPhaseBreakState(StateMachine<BossAIState> stateMachine, ObjectContext objectContext, ControllerBossAI controller)
            : base(stateMachine, objectContext, controller)
        {
        }
        #endregion

        #region LifeCycle
        public override void OnEnter()
        {
            _elapsed = 0f;
            _targetX = FindTargetX();
            ObjectContext.SetInvincible(true);
            Controller.SetPatternName(BossLabelPhaseBreak);
        }

        public override void OnExit()
        {
            Controller.SetPatternName(string.Empty);
            ObjectContext.SetInvincible(false);
            Controller.StopMove();
        }

        public override void OnFixedUpdate()
        {
            _elapsed += Time.fixedDeltaTime;
            float duration = BossPhaseBreakBeats * BossBeatDuration;
            if (_elapsed >= duration)
            {
                Controller.EndPhaseBreak();
                return;
            }

            //앞부분에만 움직이고 나머지는 서서 자세를 잡는다. 멀어서 그 안에 닿지 못하면 간 데까지만 간다
            float diffX = _targetX - Controller.transform.position.x;
            if (_elapsed < duration * BossPhaseBreakMoveRatio && Mathf.Abs(diffX) > BossApproachDeadZone)
                Controller.Walk(Mathf.Sign(diffX), BossPhaseBreakSpeedScale);
            else
                Controller.Hold();
        }
        #endregion

        #region Private
        //전투 구역의 가운데. 플레이어가 가운데 근처에 있으면 겹쳐 서지 않게 플레이어의 반대쪽 옆으로 간다
        private float FindTargetX()
        {
            float centerX = Controller.Arena.center.x;
            var target = Controller.Target;
            if (target == null) return centerX;

            float targetOffset = target.position.x - centerX;
            if (Mathf.Abs(targetOffset) >= BossPhaseBreakClearDistance) return centerX;
            return centerX + (targetOffset > 0f ? -BossPhaseBreakSideOffset : BossPhaseBreakSideOffset);
        }
        #endregion
    }
}
