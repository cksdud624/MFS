using System;
using System.Collections.Generic;
using Common;
using Generated.Table;
using UnityEngine;
using AttackType = Common.GameDefine.AttackType;
using ObjectType = Common.GameDefine.ObjectType;
using Direction = Common.GameDefine.Direction;
using AnimationType = Common.GameDefine.AnimationType;
using EffectType = Common.GameDefine.EffectType;
using ActionType = Common.GameDefine.ActionType;
using Team = Common.GameDefine.Team;
using DamageResult = Common.GameDefine.DamageResult;
using StatType = Common.GameDefine.StatType;
using StatusTrait = Common.GameDefine.StatusTrait;


namespace InGame.Context
{
    public class ObjectContext
    {
        #region Init
        public ObjectContext(ObjectData objectData, Team team)
        {
            //오브젝트 타입은 테이블 데이터에서 결정한다
            ObjectType = objectData.IsCharacter ? ObjectType.Character : ObjectType.Object;
            ObjectData = objectData;
            Team = team;
            //하이어라키의 오브젝트 이름과 같게 지어서 로그에서 같은 대상으로 읽히게 한다
            StatusEffects = new StatusEffects(Global.Instance.TableManager, $"{team}_{objectData.Id}");
            if (ObjectType is ObjectType.Character)
            {
                var character = Global.Instance.TableManager.CharacterRecord.GetRecord(objectData.Id);
                if (character == null)
                {
                    Debug.LogError($"Object {objectData.Id} has no character record");
                    return;
                }
                CharacterData = character;
                //Hp가 0이면 등장하자마자 쓰러진 캐릭터가 된다. 표의 칸을 비워둔 경우다
                if (character.Hp <= 0)
                    Debug.LogError($"Character {objectData.Id} : Hp must be greater than 0.");
                //전투는 Hp를 가득 채운 채로 시작한다
                Hp = character.Hp;

                //타고난 상태 효과(늘 경직 면역인 보스 등). 빈칸은 0 하나로 읽히므로 건너뛴다
                foreach (long effectId in character.StatusEffects)
                {
                    if (effectId != 0)
                        StatusEffects.Add(effectId);
                }
            }
        }
        #endregion

        #region Data
        public ObjectType ObjectType { get; private set; }
        public Team Team { get; private set; }

        public ObjectData ObjectData {get; private set;}
        public CharacterData CharacterData {get; private set;}
        #endregion

        #region Status
        //전투 수치는 캐릭터만 가진다 (Character 표). 캐릭터가 아닌 오브젝트는 모두 0이다.
        //공격력과 방어력은 표의 값에 걸려 있는 상태 효과를 반영해서 읽는다. 최대 Hp는 표의 값 그대로다
        public int MaxHp => CharacterData?.Hp ?? 0;
        public int Hp { get; private set; }
        //Hp가 바뀌었다 (지금 Hp, 최대 Hp)
        public event Action<int, int> OnHpChanged;
        //Hp가 0이 되었다. 0이 되는 순간에 한 번만 알린다. 그 뒤에 무엇을 할지(쓰러짐)는 오브젝트가 정한다
        public event Action OnHpDepleted;
        public void SetHp(int hp)
        {
            hp = Mathf.Clamp(hp, 0, MaxHp);
            if (Hp == hp) return;
            Hp = hp;
            OnHpChanged?.Invoke(Hp, MaxHp);
            if (Hp == 0)
                OnHpDepleted?.Invoke();
        }

        /// <summary>
        /// 공격 종류에 맞는 공격력. 공격이 마법과 기계로 나뉘어 나가므로(AttackCommand 표의 Type) 수치도 종류마다 따로 둔다.
        /// 종류가 없으면(None) 0이다
        /// </summary>
        public int GetAttack(AttackType attackType) => attackType switch
        {
            AttackType.Magic => GetStat(StatType.MagicAttack, CharacterData?.MagicAttack ?? 0),
            AttackType.Machine => GetStat(StatType.MachineAttack, CharacterData?.MachineAttack ?? 0),
            _ => 0,
        };

        /// <summary>공격 종류에 맞는 방어력. 마법 공격은 마법 방어력으로, 기계 공격은 기계 방어력으로 받는다</summary>
        public int GetDefense(AttackType attackType) => attackType switch
        {
            AttackType.Magic => GetStat(StatType.MagicDefense, CharacterData?.MagicDefense ?? 0),
            AttackType.Machine => GetStat(StatType.MachineDefense, CharacterData?.MachineDefense ?? 0),
            _ => 0,
        };

        //표의 기본값에 걸려 있는 상태 효과를 반영한다. 곱하기 보정으로 소수가 나오면 반올림한다
        private int GetStat(StatType stat, int baseValue) => Mathf.RoundToInt(StatusEffects.Apply(stat, baseValue));
        #endregion

        #region Status Effect
        //걸려 있는 상태 효과(버프, 디버프, 타고난 특성). 걸고 푸는 것도 이쪽으로 한다
        public StatusEffects StatusEffects { get; }
        #endregion

        #region Direction
        //캐릭터가 보고 있는 방향
        public Direction Direction { get; private set; } = Direction.Right;
        public event Action<Direction> OnDirectionChanged;
        public void SetDirection(Direction direction)
        {
            //공격 중에는 몸이 묶여 있으므로 보는 방향도 돌리지 않는다.
            //이 잠금은 대시로 캔슬해야만 풀린다. 피격 중에도 입력이 막히므로 돌리지 않는다
            if (IsAttacking || IsDamaged) return;
            if (Direction == direction) return;
            Direction = direction;
            OnDirectionChanged?.Invoke(direction);
        }

        /// <summary>
        /// 오른쪽을 보는 기준으로 적은 오프셋을 지금 보는 방향에 맞춰 뒤집는다.
        /// 테이블 값은 전부 오른쪽 기준으로 적으므로 쓰는 쪽에서 부호를 따지지 않는다
        /// </summary>
        public Vector2 GetDirectionalOffset(Vector2 offset)
        {
            if (Direction == Direction.Left)
                offset.x = -offset.x;
            return offset;
        }
        #endregion

        #region Collider
        //오브젝트 원점(발밑)에서 콜라이더 중점까지의 오프셋
        public Vector2 ColliderOffset => GetDirectionalOffset(ObjectData.ColliderOffset);

        //피격 판정의 크기와 오프셋. 지금은 본체 콜라이더와 같은 값을 쓴다.
        //본체와 따로 맞춰야 하는 캐릭터가 생기면 Object 테이블에 컬럼을 추가하고 여기만 바꾼다
        public Vector2 HurtBoxSize => ObjectData.ColliderSize;
        public Vector2 HurtBoxOffset => ColliderOffset;
        #endregion

        #region Move
        //시나리오가 정한 시간에 맞춰 걷게 할 때 쓰는 속도. 0이면 테이블의 MoveSpeed를 쓴다.
        //이동 입력이 들어올 때 속도를 정하므로, 이동 입력을 내기 전에 바꿔야 적용된다
        public float MoveSpeedOverride { get; private set; }
        //테이블 걸음 속도에 곱하는 배율 (이동 속도 테스트 키 등). 시나리오가 시간을 정한 이동(Override)에는 곱하지 않는다. 그 시간에 도착해야 하기 때문이다
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float MoveSpeed => MoveSpeedOverride > 0f ? MoveSpeedOverride : ObjectData.MoveSpeed * MoveSpeedMultiplier;
        public void SetMoveSpeedOverride(float speed) => MoveSpeedOverride = Mathf.Max(0f, speed);
        public void SetMoveSpeedMultiplier(float multiplier) => MoveSpeedMultiplier = Mathf.Max(0f, multiplier);

        //이동
        public event Action<float> OnMoveVelocityChanged;
        public void SetMoveVelocity(float velocityX) => OnMoveVelocityChanged?.Invoke(velocityX);

        //점프
        public event Action<float> OnJumpVelocityChanged;
        public void SetJumpVelocity(float velocityY) => OnJumpVelocityChanged?.Invoke(velocityY);

        //땅에 있는지
        public bool IsGrounded { get; private set; } = true;
        public event Action<bool> OnGroundedChanged;
        public void SetGrounded(bool grounded)
        {
            if (IsGrounded == grounded) return;
            IsGrounded = grounded;
            OnGroundedChanged?.Invoke(grounded);
        }

        //가능한 점프 횟수
        public int JumpCount { get; private set; }
        public event Action<int> OnJumpCountChanged;

        public bool SetJumpCount(int jumpCount)
        {
            if (JumpCount == jumpCount || jumpCount < 0)
                return false;
            JumpCount = jumpCount;
            OnJumpCountChanged?.Invoke(JumpCount);
            return true;
        }
        #endregion

        #region Dash
        //대시 중인지
        public bool IsDashing { get; private set; }
        public event Action<bool> OnDashingChanged;
        public void SetDashing(bool dashing)
        {
            if (IsDashing == dashing) return;
            IsDashing = dashing;
            OnDashingChanged?.Invoke(dashing);
        }

        //대시
        public event Action<Vector2> OnDashVelocityChanged;
        public void SetDashVelocity(Vector2 velocity) => OnDashVelocityChanged?.Invoke(velocity);

        //대시 중 대시 재입력 (연속 대시)
        public event Action OnDashRestart;
        public void RequestDashRestart() => OnDashRestart?.Invoke();
        #endregion

        #region Action
        //Action 상태에서 무엇을 할지. 상태를 바꾸기 전에 정해둔다
        public ActionType ActionType { get; private set; }
        public void SetActionType(ActionType actionType) => ActionType = actionType;
        #endregion

        #region Attack
        //공격 중인지. 공격이 끝날 때까지 제자리에 묶어둔다
        public bool IsAttacking { get; private set; }
        public event Action<bool> OnAttackingChanged;
        public void SetAttacking(bool attacking)
        {
            if (IsAttacking == attacking) return;
            IsAttacking = attacking;
            OnAttackingChanged?.Invoke(attacking);
        }

        //지금 나갈 공격. 어떤 애니메이션을 얼마나, 어떤 판정 박스로 휘두를지는 전부 여기 들어있다.
        //한 번의 커맨드에 마법과 기계가 같이 나가므로 타입별로 한 줄씩 들어온다.
        //Action 상태로 들어가기 전에 정해둔다
        public IReadOnlyList<AttackCommandData> AttackCommands { get; private set; }
        public void SetAttackCommands(IReadOnlyList<AttackCommandData> attackCommands) => AttackCommands = attackCommands;

        /// <summary>
        /// 지금 나가는 공격이 맞았을 때 맞은 쪽에 넘길 것을 만든다. 위력은 그 종류의 공격 줄에 적힌 Damage(AttackCommand 표)에
        /// 공격력을 더한 값이고, 경직 시간은 그 줄의 Stagger다. 반격은 카운터로 받아낼 수 없다.
        /// 지금 공격에 그 종류의 줄이 없으면 공격력만 있고 굳히지 않는 공격이 된다
        /// </summary>
        public AttackHit GetAttackHit(AttackType attackType)
        {
            int damage = 0;
            float stagger = 0f;
            if (AttackCommands != null)
            {
                for (int i = 0; i < AttackCommands.Count; i++)
                {
                    if ((AttackType)AttackCommands[i].Type != attackType) continue;
                    damage = AttackCommands[i].Damage;
                    stagger = AttackCommands[i].Stagger;
                    break;
                }
            }
            return new AttackHit(attackType, damage + GetAttack(attackType), stagger, ActionType is not ActionType.CounterAttack);
        }

        //공격 한 단계의 시작. 여기서 중복 히트 기록을 비운다
        public event Action OnAttackStart;
        public void StartAttack()
        {
            IsAttackHit = false;
            OnAttackStart?.Invoke();
        }

        //이번 단계가 무언가를 맞췄는지. 다음 커맨드가 히트를 요구할 때 본다
        public bool IsAttackHit { get; private set; }
        public void ReportAttackHit() => IsAttackHit = true;

        //판정 박스를 켜고 끄는 시점. 상태가 바뀔 때만 들어온다.
        //박스마다 어느 타입(마법/기계)의 판정인지 같이 넘긴다.
        //ownedByEffect면 그 타입의 이펙트가 판정을 들고 다니고, 아니면 캐릭터가 직접 들고 있는다
        public event Action<AttackHitBoxData, AttackType, bool, bool> OnAttackHitBoxActiveChanged;
        public void SetAttackHitBoxActive(AttackHitBoxData hitBox, AttackType attackType, bool active, bool ownedByEffect)
            => OnAttackHitBoxActiveChanged?.Invoke(hitBox, attackType, active, ownedByEffect);

        //공격 한 단계가 끝났다. 예약된 입력이 있으면 여기서 다음 단계를 이어붙인다
        public event Action OnAttackEnd;
        public void EndAttack() => OnAttackEnd?.Invoke();

        //공격 중 다음 단계로 이어붙이기 (연속 공격)
        public event Action OnAttackRestart;
        public void RequestAttackRestart() => OnAttackRestart?.Invoke();
        #endregion

        #region Counter
        //카운터 대기 중인지. 이 동안 맞으면 Damage로 가지 않고 카운터가 된다
        public bool IsCountering { get; private set; }
        public void SetCountering(bool countering) => IsCountering = countering;

        //Action 중 카운터 입력 (대시, 공격, 카운터 대기를 끊고 카운터 대기를 처음부터)
        public event Action OnCounterRestart;
        public void RequestCounterRestart() => OnCounterRestart?.Invoke();

        //카운터 대기 중에 공격을 받아냈다. 반격할 공격(AttackCommands)과 돌아설 방향은 알리기 전에 정해둔다
        public event Action OnCounterSuccess;
        public void NotifyCounterSuccess() => OnCounterSuccess?.Invoke();
        #endregion

        #region Damage
        //피격. 맞으면 Damage 상태로 들어가서 StaggerDuration만큼 굳는다.
        //attackerSide는 맞은 쪽에서 봤을 때 공격한 쪽이 있는 방향. 카운터에 성공하면 그쪽으로 돌아서서 반격한다.
        //counterable이 false면 카운터 대기 중이어도 받아내지 못하고 그대로 맞는다
        public event Action<Direction, bool> OnDamage;
        //이번 피격을 어떻게 받았는지. 피격을 처리하는 쪽이 이벤트 안에서 남긴다
        private DamageResult _damageResult;
        //이번 피격으로 굳는 시간(초). 피격을 알리기 전에 정해둔다.
        //0이면 굳지 않는다. 맞기는 해서 Hp는 깎이지만 Damage 상태에 들어가지 않고 하던 행동을 이어간다
        public float StaggerDuration { get; private set; }

        /// <summary>
        /// 피격을 알리고 맞은 쪽이 어떻게 받았는지 돌려준다.
        /// 아무도 결과를 남기지 않으면 받을 쪽이 없다는 뜻이므로 맞지 않은 것으로 본다.
        /// 맞았을 때(Hit)만 Hp를 깎는다. 카운터로 받아냈거나 무적이면 깎지 않는다
        /// </summary>
        public DamageResult RequestDamage(Direction attackerSide, AttackHit hit)
        {
            StaggerDuration = ResolveStagger(hit.Stagger);
            _damageResult = DamageResult.Ignored;
            OnDamage?.Invoke(attackerSide, hit.Counterable);
            if (_damageResult == DamageResult.Hit)
                SetHp(Hp - CalculateDamage(hit.Type, hit.Power));
            return _damageResult;
        }

        /// <summary>
        /// 공격의 경직 시간을 맞은 쪽의 상태 효과로 고친다. 경직 면역이면 공격이 얼마나 굳히든 0이고,
        /// 아니면 받는 경직 배율(StaggerTaken)을 곱한다
        /// </summary>
        private float ResolveStagger(float stagger)
        {
            if (StatusEffects.HasTrait(StatusTrait.StaggerImmune)) return 0f;
            return Mathf.Max(0f, stagger * StatusEffects.Apply(StatType.StaggerTaken, 1f));
        }

        /// <summary>
        /// 맞았을 때 깎이는 Hp. 공격한 쪽의 위력에서 맞은 쪽의 방어력을 뺀다 (마법은 마법 방어력, 기계는 기계 방어력).
        /// 방어력이 더 높으면 0이다. 음수가 되면 맞을수록 Hp가 차오른다
        /// </summary>
        public int CalculateDamage(AttackType attackType, int attackPower)
            => Mathf.Max(0, attackPower - GetDefense(attackType));

        public void ReportDamageResult(DamageResult damageResult) => _damageResult = damageResult;

        /// <summary>
        /// 이 오브젝트가 때린 대상에서 봤을 때 이 오브젝트가 있는 쪽. 맞은 쪽이 반격할 때 돌아설 방향이다.
        /// 거의 겹쳐서 좌우를 가릴 수 없으면 앞에 있는 대상을 때렸다고 보고 이 오브젝트가 보는 방향의 반대를 준다
        /// </summary>
        public Direction ResolveAttackerSide(Vector2 attackerPosition, Vector2 targetPosition)
        {
            float diffX = attackerPosition.x - targetPosition.x;
            if (Mathf.Abs(diffX) < GameDefine.TargetDirectionDeadZone)
                return Direction == Direction.Right ? Direction.Left : Direction.Right;
            return diffX > 0f ? Direction.Right : Direction.Left;
        }

        //피격 중인지. 피격 중에는 일반 입력이 막히고 땅에서도 공중처럼 관성을 받는다
        public bool IsDamaged { get; private set; }
        public event Action<bool> OnDamagedChanged;
        public void SetDamaged(bool damaged)
        {
            if (IsDamaged == damaged) return;
            IsDamaged = damaged;
            OnDamagedChanged?.Invoke(damaged);
        }

        /// <summary>
        /// 이 오브젝트의 공격이 대상을 맞출 수 있는지. 같은 진영은 때리지 않고, 중립(NPC)은 때리지도 맞지도 않는다.
        /// 자기 자신도 같은 진영이라 여기서 걸러진다
        /// </summary>
        public bool IsHostileTo(ObjectContext target) => IsHostileTo(target.Team);

        /// <summary>그 진영과 적대인지. 공격 판정과 AI의 추격 대상 고르기가 같은 기준을 쓴다</summary>
        public bool IsHostileTo(Team targetTeam)
            => Team != targetTeam && Team != Team.Neutral && targetTeam != Team.Neutral;
        #endregion

        #region Death
        //쓰러졌는지. 쓰러지면 Dead 상태에 묶여서 입력도 피격도 받지 않고, 쓰러지는 모션이 끝나면 사라진다
        public bool IsDead { get; private set; }
        public event Action OnDead;
        public void Die()
        {
            if (IsDead) return;
            IsDead = true;
            OnDead?.Invoke();
        }

        //쓰러지는 모션이 끝났다. 오브젝트를 없앨 때다
        public event Action OnDeathEnd;
        public void EndDeath() => OnDeathEnd?.Invoke();
        #endregion

        #region Event
        //대화 같은 연출 중인지. 연출 중에는 Event 상태에 묶여서 일반 입력을 받지 않고 맞지도 않는다
        public bool IsInEvent { get; private set; }
        public event Action<bool> OnInEventChanged;
        public void SetInEvent(bool inEvent)
        {
            if (IsInEvent == inEvent) return;
            IsInEvent = inEvent;
            OnInEventChanged?.Invoke(inEvent);
        }
        #endregion

        #region Effect
        //이펙트 재생.
        //variant는 같은 종류 안에서 몇 번째 프리팹인지를 가리킨다. Attack 1이면 Attack1 프리팹이고, 0이면 번호 없는 기본 프리팹.
        //offset은 콜라이더 중점에서 얼마나 띄울지. 오른쪽 기준으로 적은 값이면 GetDirectionalOffset을 거쳐서 넘긴다.
        //direction은 이펙트의 진행 방향이며 0이면 캐릭터가 보는 방향을 쓴다.
        //duration이 0 이하면 파티클 수명대로 재생한다.
        //attackType이 None이 아니면 그 타입의 공격 판정을 이 이펙트가 들고 다닌다
        public event Action<EffectType, int, Vector2, Vector2, float, AttackType> OnEffectPlay;
        public void PlayEffect(EffectType effectType, int variant, Vector2 offset, Vector2 direction = default,
            float duration = 0f, AttackType attackType = AttackType.None)
            => OnEffectPlay?.Invoke(effectType, variant, offset, direction, duration, attackType);
        #endregion

        #region Animation
        //현재 재생 중인 애니메이션.
        //variant는 같은 종류 안에서 몇 번째 클립인지를 가리킨다. Attack 1이면 Attack1 클립이고, 0이면 번호 없는 기본 클립
        public AnimationType Animation { get; private set; } = AnimationType.Idle;
        public event Action<AnimationType, int, Action> OnAnimationChanged;
        public void SetAnimation(AnimationType animationType, Action onAnimationEnd = null)
            => SetAnimation(animationType, 0, onAnimationEnd);
        public void SetAnimation(AnimationType animationType, int variant, Action onAnimationEnd = null)
        {
            Animation = animationType;
            OnAnimationChanged?.Invoke(animationType, variant, onAnimationEnd);
        }
        #endregion
    }
}
