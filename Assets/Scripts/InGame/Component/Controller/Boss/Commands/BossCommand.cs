using System.Threading;
using Cysharp.Threading.Tasks;
using Generated.Table;

namespace InGame.Component.Controller.Boss.Commands
{
    /// <summary>
    /// 보스 패턴을 이루는 동작 하나. BossCommand 표의 한 줄이고, 패턴이 Id 순으로 하나씩 끝나기를 기다리며 실행한다.
    /// 보스도 플레이어와 같은 입력(이동, 공격 버튼, 카운터)만 내므로 커맨드는 ControllerBossAI의 입력 함수만 부른다.
    /// 판정, 피해, 받아내기, 경직은 플레이어의 입력과 같은 경로로 처리된다.
    /// 칸에서 읽은 값만 필드에 두고, 실행 중의 값(지난 시간 등)은 Run의 지역 변수로 둔다.
    /// 커맨드는 보스의 설계(BossDefinition)에 들어 있어서 같은 설계를 쓰는 보스끼리 같이 쓸 수 있다. 필드에 두면 서로 덮어쓴다.
    /// 새 커맨드는 이 클래스를 상속해서 Parse(칸 형식)와 Run을 구현하고 BossCommandFactory에 이름을 등록한다. Param1~2를 어떻게 쓰는지는 각 클래스 summary에 적는다
    /// </summary>
    public abstract class BossCommand
    {
        #region Fields
        //BossCommand 표의 줄 번호. 에러 로그에서 어느 줄인지 찾을 때 쓴다
        public long Id { get; private set; }
        //표에 적힌 커맨드 이름 (Approach, Attack ...)
        public string Command { get; private set; }
        #endregion

        #region Init
        /// <summary>표 한 줄을 읽어 칸을 해석한다. 형식이 틀리거나 쓰지 않는 칸에 값이 있으면 에러를 남기고 false를 돌려준다</summary>
        public bool Init(BossCommandData data)
        {
            Id = data.Id;
            Command = data.Command;

            var reader = new BossParamReader(data);
            bool parsed = Parse(reader);
            //형식이 틀린 칸과 쓰지 않는 칸을 한 번에 다 보이도록 &로 둘 다 확인한다
            return reader.CheckUnused() & parsed;
        }
        #endregion

        #region Public
        /// <summary>
        /// 다른 표와 맞춰봐야 아는 것을 확인한다 (누를 공격 커맨드가 그 보스의 AttackCommand 표에 있는지 등). 틀리면 에러를 남기고 false.
        /// 보스의 설계는 이름으로 찾으므로 어느 오브젝트가 쓰는지는 보스전이 시작될 때 알 수 있다. 그래서 칸 해석(Parse)과 따로 둔다
        /// </summary>
        public virtual bool Validate(ObjectData bossObject, TableManager tableManager) => true;

        /// <summary>
        /// 동작을 끝까지 하면 끝난다. 패턴이 끊기면(맞아서 굳음, 페이즈 전환, 쓰러짐) cancellationToken이 취소되고
        /// OperationCanceledException으로 끝나야 한다. 누르고 있던 이동 입력과 예고는 패턴 상태가 끝날 때 거두므로 여기서 되돌리지 않아도 된다
        /// </summary>
        public abstract UniTask Run(ControllerBossAI boss, CancellationToken cancellationToken);

        public override string ToString() => $"BossCommand {Id} ({Command})";
        #endregion

        #region Protected
        /// <summary>Param 칸을 읽는다. 쓰지 않는 칸은 읽지 않는다 (읽지 않은 칸에 값이 있으면 에러가 된다)</summary>
        protected abstract bool Parse(BossParamReader reader);
        #endregion
    }
}
