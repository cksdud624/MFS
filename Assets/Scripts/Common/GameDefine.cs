using UnityEngine;

namespace Common
{
    public static class GameDefine
    {
        #region Enum
        public enum SceneType
        {
            BootStrap = 0,
            Main = 1
        }

        public enum ObjectType
        {
            Object,
            Character
        }

        public enum ObjectState
        {
            Raw,
            Loading,
            Ready,
            Playing,
            Sleep,
            Destroyed,
            Error
        }

        public enum Direction
        {
            Right,
            Left,
        }

        public enum FSMState
        {
            Ground,
            Air,
            Action,
            Damage,
            Event,
        }

        //AI 행동 상태
        public enum AIState
        {
            Idle,
            Patrol,
            Chase,
        }
        #endregion

        #region Animation
        public enum AnimationType
        {
            Idle,
            Move,
            Jump,
            Fly,
            Dash,
            BackDash,
        }
        #endregion

        #region Asset Path
        public const string AssetPathCharacter = "Assets/AddressableAssets/Prefab/Character/";
        public const string AssetPathAnimation = "Assets/AddressableAssets/Animation/";
        public const string AssetExtensionPrefab = ".prefab";
        public const string AssetExtensionAnimation = ".anim";
        #endregion

        #region Tag / Layer
        public const string TagMap = "Map";
        public const string LayerHitBox = "HitBox";
        #endregion

        #region Physics
        //바닥으로 인정하는 접촉면의 기울기
        public const float GroundNormalThreshold = 0.5f;
        //낭떠러지 감지용
        public const float LedgeProbeMargin = 0.02f;
        public const float LedgeProbeDepth = 0.1f;
        //벽 감지용
        public const float WallProbeDistance = 0.05f;
        //콜라이더는 방향에 따라 좌우 대칭으로 뒤집는다
        public static readonly Vector2 ColliderSize = new(0.2f, 0.35f);
        public const float ColliderOffsetX = 0.07f;
        public const float ColliderOffsetY = -0.05f;
        #endregion

        #region Move
        //테이블로 차후 관리해야하는 부분
        //공중 관성: MoveSpeed까지 붙는 시간 / 입력을 뗐을 때 멈추는 시간 / 최고 속도 초과분을 깎는 시간
        public const float AirAccelerationTime = 0.25f;
        public const float AirDecelerationTime = 1.2f;
        public const float AirOverSpeedDecelerationTime = 0.3f;

        public const float JumpPower = 2.5f;
        //지상에서 얻는 점프 횟수 / 지상에서 떨어졌을 때 남는 점프 횟수
        public const int GroundJumpCount = 2;
        public const int FallJumpCount = 1;
        #endregion

        #region Dash
        //테이블로 차후 관리해야하는 부분
        public const float DashSpeed = 5f;
        public const float DashDuration = 0.2f;
        public const int DashMaxStack = 1;
        public const float DashChargeInterval = 0.5f;
        #endregion

        #region HitBox
        //한 번의 판정에서 감지할 수 있는 최대 대상 수
        public const int HitBoxMaxDetectCount = 16;
        //판정 범위를 눈으로 맞추기 위한 디버그 표시 시간
        public const float HitBoxGizmoDuration = 0.2f;
        #endregion

        #region AI
        //테이블로 차후 관리해야하는 부분
        public const float DetectRange = 5f;   //추격을 시작하는 거리
        public const float LoseRange = 8f;     //추격을 포기하는 거리
        public const float AttackRange = 0.5f; //멈춰서 공격하는 거리

        public const float IdleDuration = 1.5f;
        public const float PatrolDuration = 3f;
        //이 거리 안에서는 대상이 좌우 어느 쪽인지 따지지 않는다
        public const float TargetDirectionDeadZone = 0.05f;
        #endregion
    }
}
