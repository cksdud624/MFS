using AttackType = Common.GameDefine.AttackType;

namespace InGame.Context
{
    /// <summary>
    /// 공격이 맞았을 때 때린 쪽이 맞은 쪽에 넘기는 것. 때린 쪽의 사정(어떤 공격이고 얼마나 센지)만 담는다.
    /// Hp를 얼마나 깎고 얼마나 굳을지는 맞은 쪽이 자기 방어력과 상태 효과로 정한다
    /// </summary>
    public readonly struct AttackHit
    {
        //공격의 종류. 맞은 쪽은 같은 종류의 방어력으로 받는다
        public AttackType Type { get; }
        //위력. 공격의 Damage(AttackCommand 표)에 때린 쪽의 공격력을 더한 값
        public int Power { get; }
        //맞은 쪽을 굳히는 시간(초). 공격마다 다르다 (AttackCommand 표의 Stagger)
        public float Stagger { get; }
        //카운터 대기에서 공격을 받아낸 뒤 나간 반격인지. 반격은 카운터로 받아낼 수 없고, 경직 면역인 대상도 굳힌다
        public bool IsCounterAttack { get; }
        //카운터 대기로 받아낼 수 있는지. 반격은 받아낼 수 없다
        public bool Counterable => !IsCounterAttack;

        public AttackHit(AttackType type, int power, float stagger, bool isCounterAttack)
        {
            Type = type;
            Power = power;
            Stagger = stagger;
            IsCounterAttack = isCounterAttack;
        }
    }
}
