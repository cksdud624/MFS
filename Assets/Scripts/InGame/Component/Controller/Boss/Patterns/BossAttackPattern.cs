using static Common.GameDefine;

namespace InGame.Component.Controller.Boss.Patterns
{
    /// <summary>
    /// 공격 패턴. 정해진 박마다 공격 버튼을 눌러서 친다 (베기, 2연격, 3연격, 바닥 쓸기, 돌진).
    /// 첫 타격 전까지는 원하는 거리로 다가가고, 타격 직전에는 멈춰서 자세를 잡는다.
    /// 무엇을 얼마나 세게, 얼마나 나아가며 치는지는 버튼 번호로 찾는 AttackCommand 표가 정한다.
    /// 같은 버튼을 박마다 이어 누르면 콤보 단계로 이어진다 ("2" → "22"). 그래서 앞 단계의 AttackTime은 다음 타격까지의 간격보다 짧게 적는다
    /// </summary>
    public class BossAttackPattern : BossPattern
    {
        #region Fields
        private readonly AttackTelegraph _telegraph;
        //누를 공격 버튼 (AttackCommand 표의 Command를 만드는 번호)
        private readonly int _button;
        //준비하면서 원하는 거리로 다가가는지. 돌진처럼 멀리서 치는 패턴은 제자리에 서서 준비한다
        private readonly bool _approaches;
        //타격하는 박. 패턴이 시작된 박을 0으로 보고 오름차순으로 적는다
        private readonly float[] _hitBeats;
        //타격마다 나가는 공격의 커맨드. 같은 버튼을 이어 누르므로 버튼 번호를 타격 순번만큼 이어붙인 것이다 ("2", "22").
        //범위를 예고할 때 쓴다. 매 틱 문자열을 만들지 않게 미리 만들어둔다
        private readonly string[] _hitCommands;
        //다음에 칠 타격의 순번
        private int _nextHit;
        #endregion

        #region Init
        public BossAttackPattern(string name, BossPatternPick pick, AttackTelegraph telegraph, int button, bool approaches, params float[] hitBeats)
            : base(name, pick)
        {
            _telegraph = telegraph;
            _button = button;
            _approaches = approaches;
            _hitBeats = hitBeats;

            _hitCommands = new string[hitBeats.Length];
            string command = string.Empty;
            for (int i = 0; i < hitBeats.Length; i++)
            {
                command += button;
                _hitCommands[i] = command;
            }
        }
        #endregion

        #region LifeCycle
        public override void OnFixedUpdate()
        {
            float beat = Beat;
            float firstBeat = _hitBeats[0];

            //마지막 타격을 누를 때까지 어떤 공격인지 색으로 알리고, 다음 타격이 닿을 범위를 미리 보여준다
            if (_nextHit < _hitBeats.Length && beat >= firstBeat - BossTelegraphBeats)
            {
                Controller.SetTelegraph(_telegraph);
                Controller.PreviewAttack(_hitCommands[_nextHit]);
            }

            //준비 : 첫 타격 직전까지 원하는 거리를 맞춘다
            if (beat < firstBeat - BossApproachStopBeats)
            {
                if (_approaches)
                    Controller.Approach(Pick.PreferDistance, BossApproachSpeedScale);
                else
                    Controller.Hold();
                return;
            }

            //공격은 시작한 방향으로 잠기므로 누르기 전에 멈춰서 대상을 본다. 휘두르는 중에는 돌아서지 않는다
            Controller.Hold();
            while (_nextHit < _hitBeats.Length && beat >= _hitBeats[_nextHit])
            {
                Controller.RequestAttack(_button);
                _nextHit++;
            }
            if (_nextHit < _hitBeats.Length) return;

            Controller.SetTelegraph(AttackTelegraph.None);
            Controller.PreviewAttack(null);
            //마지막 타격을 다 휘두르고 여유까지 지나면 끝난다. 이 여유와 다음 마디까지의 시간이 플레이어의 공격 기회다
            if (beat >= _hitBeats[^1] + BossPatternEndBeats && !Controller.IsAttacking)
                IsFinished = true;
        }
        #endregion

        #region Protected
        protected override void OnBegin() => _nextHit = 0;
        #endregion
    }
}
