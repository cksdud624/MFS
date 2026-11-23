using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 패턴 사이. 대상과의 거리를 재면서 마디 첫 박마다 다음 패턴을 고른다.
    /// 대상이 멀면 중간 거리까지 다가가고, 가까우면 근접 패턴이 닿는 거리를 유지한다 (너무 붙으면 물러난다).
    /// 패턴이 마디 중간에 끝나면 다음 마디 첫 박까지는 고르지 않으므로, 그 사이가 플레이어의 공격 기회다
    /// </summary>
    public class BossSpacingState : BossStateBase
    {
        #region Fields
        //마지막으로 패턴을 고르려 한 마디. 한 마디에 한 번만 고른다. 상태를 나갔다 와도 같은 마디에서 다시 고르지 않게 남겨둔다
        private int _lastBar = -1;
        #endregion

        #region Init
        public BossSpacingState(StateMachine<BossAIState> stateMachine, ObjectContext objectContext, ControllerBossAI controller)
            : base(stateMachine, objectContext, controller)
        {
        }
        #endregion

        #region LifeCycle
        public override void OnEnter() => Controller.Hold();

        public override void OnExit() => Controller.StopMove();

        public override void OnFixedUpdate()
        {
            float beat = Controller.Beat;
            int bar = Mathf.FloorToInt(beat / BossBeatsPerBar);
            if (bar != _lastBar)
            {
                _lastBar = bar;
                float barStartBeat = bar * BossBeatsPerBar;
                //마디 첫 박을 지나서 들어왔으면 이 마디는 넘긴다. 박에 맞지 않는 타격이 나가지 않게 한다
                if (beat - barStartBeat < BossPatternStartWindow && Controller.TryPickPattern(barStartBeat)) return;
            }

            if (!Controller.TryGetTargetGap(out float distance, out _))
            {
                Controller.Hold();
                return;
            }

            float keepDistance = distance > BossFarDistance ? BossFarKeepDistance : BossKeepDistance;
            Controller.Approach(keepDistance, 1f, BossSpacingDeadZone);
        }
        #endregion
    }
}
