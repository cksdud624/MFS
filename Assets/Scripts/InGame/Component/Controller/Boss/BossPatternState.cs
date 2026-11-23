using Common.Template.FSM;
using InGame.Component.Controller.Boss.Patterns;
using InGame.Context;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 패턴 진행 중. 컨트롤러가 정해둔 패턴을 받아서 끝날 때까지 굴린다.
    /// 맞아서 굳거나 다른 패턴(연타 대응)으로 끊기면 상태를 나가면서 예고와 이동을 거둔다
    /// </summary>
    public class BossPatternState : BossStateBase
    {
        #region Fields
        //진행 중인 패턴. 도중에 다음 패턴이 정해져도 나갈 때까지는 들어올 때의 것을 들고 있는다
        private BossPattern _pattern;
        #endregion

        #region Init
        public BossPatternState(StateMachine<BossAIState> stateMachine, ObjectContext objectContext, ControllerBossAI controller)
            : base(stateMachine, objectContext, controller)
        {
        }
        #endregion

        #region LifeCycle
        public override void OnEnter()
        {
            _pattern = Controller.NextPattern;
            _pattern.Begin(Controller, Controller.NextPatternStartBeat);
            Controller.SetPatternName(_pattern.Name);
        }

        public override void OnExit()
        {
            _pattern = null;
            Controller.SetPatternName(string.Empty);
            Controller.SetTelegraph(AttackTelegraph.None);
            Controller.PreviewAttack(null);
            Controller.StopMove();
        }

        public override void OnFixedUpdate()
        {
            _pattern.OnFixedUpdate();
            if (_pattern.IsFinished)
                Controller.FinishPattern();
        }
        #endregion

        #region Public
        /// <summary>
        /// 진행 중인 패턴을 다음 패턴으로 바꾼다. 같은 상태로는 다시 들어갈 수 없어서, 나갈 때 거두는 것(예고, 이동)을 여기서 거두고 새로 시작한다
        /// </summary>
        public void Restart()
        {
            OnExit();
            OnEnter();
        }
        #endregion
    }
}
