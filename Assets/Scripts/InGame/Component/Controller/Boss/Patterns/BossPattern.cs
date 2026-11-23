namespace InGame.Component.Controller.Boss.Patterns
{
    /// <summary>
    /// 보스 패턴 하나. 준비(다가가기, 예고)부터 타격, 끝날 때까지를 박에 맞춰 진행한다.
    /// 패턴은 플레이어와 같은 입력(이동, 공격, 카운터)만 낸다. 그래서 공격의 판정과 위력, 받아내기와 경직은 플레이어의 공격과 같은 경로로 처리된다.
    /// 패턴 종류 하나당 클래스 하나를 만들고, 어느 보스가 어떤 수치로 쓰는지는 보스 클래스(BossProfile)가 정한다
    /// </summary>
    public abstract class BossPattern
    {
        #region Fields
        //로그에 남기는 이름
        public string Name { get; }
        public BossPatternPick Pick { get; }
        //패턴이 끝났는지. 끝나면 보스는 거리 재기로 돌아간다
        public bool IsFinished { get; protected set; }

        protected ControllerBossAI Controller { get; private set; }
        //패턴이 시작되는 박 (보스의 시계 기준). 반응으로 나가는 패턴은 지금보다 조금 뒤의 박에서 시작한다
        private float _startBeat;
        //패턴이 시작된 뒤 지난 박 수. 아직 시작 전이면 음수다
        protected float Beat => Controller.Beat - _startBeat;
        #endregion

        #region Init
        protected BossPattern(string name, BossPatternPick pick)
        {
            Name = name;
            Pick = pick;
        }
        #endregion

        #region LifeCycle
        /// <summary>물리 스텝마다 패턴을 진행한다. 끝났으면 IsFinished를 켠다</summary>
        public abstract void OnFixedUpdate();
        #endregion

        #region Public
        /// <summary>지금 단계에서 뽑힐 비중. 단계에 따라 비중이 달라지는 패턴이 다시 정한다</summary>
        public virtual float GetWeight(int stage) => Pick.Weight;

        /// <summary>거리와 단계 말고 따로 맞아야 하는 조건. 카운터 자세처럼 쓸 수 있을 때만 고르는 패턴이 다시 정한다</summary>
        public virtual bool CanStart(ControllerBossAI controller) => true;

        /// <summary>패턴을 시작한다. 패턴은 보스가 들고 있다가 다시 쓰므로 앞에서 쓴 상태를 여기서 비운다</summary>
        public void Begin(ControllerBossAI controller, float startBeat)
        {
            Controller = controller;
            _startBeat = startBeat;
            IsFinished = false;
            OnBegin();
        }
        #endregion

        #region Protected
        protected virtual void OnBegin() { }
        #endregion
    }
}
