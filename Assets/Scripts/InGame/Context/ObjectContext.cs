using System;
using Common;
using Generated.Table;
using UnityEngine;
using static Common.GameDefine;
using AnimationType = Common.GameDefine.AnimationType;
using Direction = Common.GameDefine.Direction;
using ObjectType = Common.GameDefine.ObjectType;


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

        //현재 재생 중인 애니메이션
        public AnimationType Animation { get; private set; } = AnimationType.Idle;
        public event Action<AnimationType, Action> OnAnimationChanged;
        public void SetAnimation(AnimationType animationType, Action onAnimationEnd = null)
        {
            Animation = animationType;
            OnAnimationChanged?.Invoke(animationType, onAnimationEnd);
        }

        //이펙트
        public event Action<EffectType, Vector2> OnEffectRequested;

        public void PlayEffect(EffectType effectType, Vector2 direction)
        {
            Vector2 normalizedDirection =
                direction.sqrMagnitude > 0f
                    ? direction.normalized
                    : (Direction == Direction.Right ? Vector2.right : Vector2.left);

            OnEffectRequested?.Invoke(effectType, normalizedDirection);
        }
    }
}
