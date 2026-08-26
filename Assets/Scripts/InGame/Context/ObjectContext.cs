using System;
using Common;
using Generated.Table;
using UnityEngine;
using ObjectType = Common.GameDefine.ObjectType;
using Direction = Common.GameDefine.Direction;
using AnimationType = Common.GameDefine.AnimationType;
using EffectType = Common.GameDefine.EffectType;
using ActionType = Common.GameDefine.ActionType;


namespace InGame.Context
{
    public class ObjectContext
    {
        public ObjectContext(ObjectData objectData)
        {
            //오브젝트 타입은 테이블 데이터에서 결정한다
            ObjectType = objectData.IsCharacter ? ObjectType.Character : ObjectType.Object;
            ObjectData = objectData;
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
        public ObjectType ObjectType { get; private set; }

        public ObjectData ObjectData {get; private set;}
        public CharacterData CharacterData {get; private set;}

        //캐릭터가 보고 있는 방향
        public Direction Direction { get; private set; } = Direction.Right;
        public event Action<Direction> OnDirectionChanged;
        public void SetDirection(Direction direction)
        {
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

        //오브젝트 원점(발밑)에서 콜라이더 중점까지의 오프셋
        public Vector2 ColliderOffset => GetDirectionalOffset(ObjectData.ColliderOffset);

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

        //Action 상태에서 무엇을 할지. 상태를 바꾸기 전에 정해둔다
        public ActionType ActionType { get; private set; }
        public void SetActionType(ActionType actionType) => ActionType = actionType;

        //지금 나갈 공격. 어떤 애니메이션을 얼마나, 어떤 판정 박스로 휘두를지는 전부 여기 들어있다.
        //Action 상태로 들어가기 전에 정해둔다
        public AttackCommandData AttackCommand { get; private set; }
        public void SetAttackCommand(AttackCommandData attackCommand) => AttackCommand = attackCommand;

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

        //판정 박스를 켜는 시점. 유지시간 동안은 매 프레임 들어온다
        public event Action<AttackHitBoxData> OnAttackHit;
        public void RequestAttackHit(AttackHitBoxData hitBox) => OnAttackHit?.Invoke(hitBox);

        //공격 한 단계가 끝났다. 예약된 입력이 있으면 여기서 다음 단계를 이어붙인다
        public event Action OnAttackEnd;
        public void EndAttack() => OnAttackEnd?.Invoke();

        //공격 중 다음 단계로 이어붙이기 (연속 공격)
        public event Action OnAttackRestart;
        public void RequestAttackRestart() => OnAttackRestart?.Invoke();

        //이펙트 재생.
        //variant는 같은 종류 안에서 몇 번째 프리팹인지를 가리킨다. Attack 1이면 Attack1 프리팹이고, 0이면 번호 없는 기본 프리팹.
        //offset은 콜라이더 중점에서 얼마나 띄울지. 오른쪽 기준으로 적은 값이면 GetDirectionalOffset을 거쳐서 넘긴다.
        //direction은 이펙트의 진행 방향이며 0이면 캐릭터가 보는 방향을 쓴다.
        //duration이 0 이하면 파티클 수명대로 재생한다
        public event Action<EffectType, int, Vector2, Vector2, float> OnEffectPlay;
        public void PlayEffect(EffectType effectType, int variant, Vector2 offset, Vector2 direction = default, float duration = 0f)
            => OnEffectPlay?.Invoke(effectType, variant, offset, direction, duration);

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
    }
}
