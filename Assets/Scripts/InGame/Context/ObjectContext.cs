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

        //콜라이더 오프셋. 테이블 값은 오른쪽을 보는 기준이라 왼쪽을 보면 X만 뒤집는다
        public Vector2 ColliderOffset
        {
            get
            {
                var offset = ObjectData.ColliderOffset;
                if (Direction == Direction.Left)
                    offset.x = -offset.x;
                return offset;
            }
        }

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

        //공격 시작. 여기서 중복 히트 기록을 비운다
        public event Action OnAttackStart;
        public void StartAttack() => OnAttackStart?.Invoke();

        //공격 판정이 나가는 시점
        public event Action OnAttackHit;
        public void RequestAttackHit() => OnAttackHit?.Invoke();

        //이펙트 재생. direction이 0이면 캐릭터가 보는 방향을 쓰고, duration이 0 이하면 파티클 수명대로 재생한다
        public event Action<EffectType, Vector2, float> OnEffectPlay;
        public void PlayEffect(EffectType effectType, Vector2 direction = default, float duration = 0f)
            => OnEffectPlay?.Invoke(effectType, direction, duration);

        //현재 재생 중인 애니메이션
        public AnimationType Animation { get; private set; } = AnimationType.Idle;
        public event Action<AnimationType, Action> OnAnimationChanged;
        public void SetAnimation(AnimationType animationType, Action onAnimationEnd = null)
        {
            Animation = animationType;
            OnAnimationChanged?.Invoke(animationType, onAnimationEnd);
        }
    }
}
