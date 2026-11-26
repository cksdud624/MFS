using Common.Template.FSM;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 보스 AI의 큰 상태. ControllerBossAI의 상태 머신에 들어간다.
    /// 상태는 스스로 끝날 때 갈 곳 하나만 생성자로 받는다. 굳음, 페이즈 전환, 쓰러짐 같은 끼어들기는 ControllerBossAI가 ChangeState로 직접 넣는다.
    /// 재정의할 때 OnEnter와 OnFixedUpdate는 base를 먼저 불러야 StateTime이 맞게 흐른다
    /// </summary>
    public abstract class BossStateBase : IState
    {
        #region Fields
        protected readonly StateMachine<BossState> StateMachine;
        protected readonly ControllerBossAI Controller;

        //이 상태에 들어온 뒤 지난 시간(초). 보스 AI는 물리 스텝마다 판단하므로 물리 스텝 시간으로 센다.
        //굳어 있거나 시나리오가 멈춘 동안에는 판단이 멈추므로 이 시간도 흐르지 않는다
        protected float StateTime { get; private set; }
        #endregion

        #region Init
        protected BossStateBase(StateMachine<BossState> stateMachine, ControllerBossAI controller)
        {
            StateMachine = stateMachine;
            Controller = controller;
        }
        #endregion

        #region LifeCycle
        public virtual void OnEnter() => StateTime = 0f;
        public virtual void OnExit() { }
        public virtual void OnUpdate() { }
        public virtual void OnFixedUpdate() => StateTime += Time.fixedDeltaTime;
        #endregion
    }
}
