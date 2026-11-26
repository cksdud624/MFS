using System.Threading;
using Cysharp.Threading.Tasks;

namespace InGame.Component.Controller.Boss.Patterns
{
    /// <summary>
    /// 보스 패턴 하나를 실행하는 객체. 보스마다 따로 만들어서 쿨타임을 들고 있는다.
    /// 거의 모든 패턴은 표의 커맨드를 차례로 실행하는 BossCommandPattern이고, 표로 나타낼 수 없는 패턴만 이 클래스를 상속해서 만든다
    /// (ControllerBossAI.RegisterPatterns). 맞아서 굳거나 페이즈가 바뀌면 토큰이 취소되므로 패턴은 끼어들기를 직접 처리하지 않는다
    /// </summary>
    public abstract class BossPattern
    {
        #region Fields
        private readonly BossPatternSettings _settings;
        //다시 뽑힐 수 있는 시각(초. Time.time)
        private float _nextAvailableTime;

        //체력바 아래에 띄우고 로그에 남기는 이름
        public string Name { get; }
        public float Weight => _settings.Weight;
        public int Priority => _settings.Priority;
        public float RecoveryDuration => _settings.RecoveryDuration;
        //거리를 따지는 패턴인지. 대상이 없으면 거리를 모르므로 이런 패턴은 뽑지 않는다
        public bool HasDistanceLimit => _settings.MinDistance > 0f || _settings.MaxDistance > 0f;
        #endregion

        #region Init
        protected BossPattern(string name, BossPatternSettings settings)
        {
            Name = name;
            _settings = settings;
        }
        #endregion

        #region Public
        /// <summary>
        /// 지금 시작할 수 있는지. 패턴을 고르는 조건은 모두 여기 한 곳에서 본다. 상태를 바꾸지 않는다.
        /// 기본은 가중치가 0보다 큰지, 쿨타임이 끝났는지, 대상이 거리 범위 안인지 본다.
        /// 재정의할 때는 base.CanStart를 같이 확인한다. 빠뜨리면 쿨타임과 거리 검사가 모두 사라진다
        /// </summary>
        public virtual bool CanStart(in BossPatternContext context)
        {
            if (Weight <= 0f || context.Time < _nextAvailableTime) return false;
            if (!HasDistanceLimit) return true;
            if (!context.HasTarget) return false;

            if (_settings.MinDistance > 0f && context.TargetDistance < _settings.MinDistance) return false;
            return _settings.MaxDistance <= 0f || context.TargetDistance <= _settings.MaxDistance;
        }

        /// <summary>time에 시작했다고 기록해 쿨타임을 건다. BossPatternSelector.MarkStarted가 부른다. 여러 페이즈에 등록된 패턴도 쿨타임은 하나다</summary>
        public void MarkStarted(float time) => _nextAvailableTime = time + _settings.Cooldown;

        /// <summary>
        /// 패턴을 끝까지 하면 끝난다. 상태가 바뀌거나 보스가 사라지면 cancellationToken이 취소되고 OperationCanceledException으로 끝나야 한다.
        /// 패턴이 바꾼 값(입력, 예고)은 패턴 상태(BossPatternState)가 끝날 때 거둔다
        /// </summary>
        public abstract UniTask Run(CancellationToken cancellationToken);
        #endregion
    }
}
