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
            if (ObjectType is ObjectType.Character)
            {
                var character = Global.Instance.TableManager.CharacterRecord.GetRecord(objectData.Id);
                if (character == null)
                {
                    Debug.LogError($"Object {objectData.Id} has no character record");
                    return;
                }
                CharacterData = character;
            }
        }
        #endregion

        #region Data
        public ObjectType ObjectType { get; private set; }
        public Team Team { get; private set; }

        public ObjectData ObjectData {get; private set;}
        public CharacterData CharacterData {get; private set;}
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
        public float MoveSpeed => MoveSpeedOverride > 0f ? MoveSpeedOverride : ObjectData.MoveSpeed;
        public void SetMoveSpeedOverride(float speed) => MoveSpeedOverride = Mathf.Max(0f, speed);

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
        //피격. 맞으면 Damage 상태로 들어가고, 이미 Damage 상태면 시간을 처음부터 다시 센다.
        //attackerSide는 맞은 쪽에서 봤을 때 공격한 쪽이 있는 방향. 카운터에 성공하면 그쪽으로 돌아서서 반격한다.
        //counterable이 false면 카운터 대기 중이어도 받아내지 못하고 그대로 맞는다
        public event Action<Direction, bool> OnDamage;
        //이번 피격을 어떻게 받았는지. 피격을 처리하는 쪽이 이벤트 안에서 남긴다
        private DamageResult _damageResult;

        /// <summary>
        /// 피격을 알리고 맞은 쪽이 어떻게 받았는지 돌려준다.
        /// 아무도 결과를 남기지 않으면 받을 쪽이 없다는 뜻이므로 맞지 않은 것으로 본다
        /// </summary>
        public DamageResult RequestDamage(Direction attackerSide, bool counterable)
        {
            _damageResult = DamageResult.Ignored;
            OnDamage?.Invoke(attackerSide, counterable);
            return _damageResult;
        }

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
