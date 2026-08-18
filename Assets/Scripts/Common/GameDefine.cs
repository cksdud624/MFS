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

        //Action 상태에서 무엇을 하는지
        public enum ActionType
        {
            Dash,
            Attack,
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
            Attack,
        }
        #endregion

        #region Effect
        //Effects/(오브젝트 id)/(EffectType) 프리팹을 찾는다
        public enum EffectType
        {
            Dash,
        }

        //중점에서 진행 방향으로 얼마나 띄울지. 프리팹을 고치지 않고 여기서 거리만 맞춘다
        public const float DashEffectDistance = 1f;
        #endregion

        #region Asset Path
        public const string AssetPathEffect = "Assets/AddressableAssets/Effects/";
        public const string AssetPathAnimation = "Assets/AddressableAssets/Animation/";
        public const string AssetExtensionPrefab = ".prefab";
        public const string AssetExtensionAnimation = ".anim";
        #endregion

        #region Tag / Layer
        public const string TagMap = "Map";
        public const string LayerHitBox = "HitBox";
        //캐릭터끼리는 서로 밀지 않도록 이 레이어에 둔다. 충돌 매트릭스에서 자기 자신과의 충돌을 꺼둔 상태
        public const string LayerIndependent = "Independent";
        #endregion

        #region Sorting
        //Order in Layer 구간. 일반 오브젝트 → 캐릭터 → 플레이어 순으로 앞에 그린다
        public const int SortingOrderObject = 0;       //0 ~ 999
        public const int SortingOrderCharacter = 1000; //1000 ~ 1999
        public const int SortingOrderPlayer = 2000;
        //한 구간에 들어갈 수 있는 개수. 넘어가면 다음 구간을 침범하므로 처음으로 돌린다
        public const int SortingOrderRange = 1000;
        #endregion

        #region Physics
        //바닥으로 인정하는 접촉면의 기울기
        public const float GroundNormalThreshold = 0.5f;
        //낭떠러지 감지용
        public const float LedgeProbeMargin = 0.02f;
        public const float LedgeProbeDepth = 0.1f;
        //벽 감지용
        public const float WallProbeDistance = 0.05f;
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

        #region Attack
        //테이블로 차후 관리해야하는 부분
        //공격 상태를 유지하는 시간 / 그 중 판정이 나가는 시점
        public const float AttackDuration = 0.4f;
        public const float AttackHitDelay = 0.15f;
        //판정 박스. offset.x는 캐릭터가 보는 방향 기준이라 왼쪽을 볼 때 자동으로 뒤집힌다
        public static readonly Vector2 AttackHitBoxOffset = new(0.25f, 0f);
        public static readonly Vector2 AttackHitBoxSize = new(0.4f, 0.3f);
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
