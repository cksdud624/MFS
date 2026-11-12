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

        //진영. 공격은 다른 진영의 피격 판정만 맞춘다
        public enum Team
        {
            Player,
            Enemy,
            //NPC. 누구도 때리지 않고 누구에게도 맞지 않는다
            Neutral,
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
            Counter,
            //카운터 대기에서 공격을 받아낸 뒤 나가는 반격. 휘두르는 방식은 Attack과 같고 끝날 때까지 맞지 않는다.
            //대시나 카운터로 끊을 수 없고, 상대도 이 공격은 카운터로 받아낼 수 없다
            CounterAttack,
        }

        //AI 행동 상태
        public enum AIState
        {
            Idle,
            Patrol,
            Chase,
            Attack,
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
            Damage,
        }
        #endregion

        #region Effect
        //Effects/(오브젝트 id)/(EffectType) 프리팹을 찾는다
        public enum EffectType
        {
            Dash,
            Attack,
        }

        //콜라이더 중점에서 대시 방향으로 얼마나 더 띄울지.
        //공격 이펙트는 AttackCommand 테이블의 EffectOffset이 자리를 정하지만 대시는 아직 테이블이 없다
        public const float DashEffectDistance = -1f;
        #endregion

        #region Asset Path
        public const string AssetPathEffect = "Assets/AddressableAssets/Effect/";
        public const string AssetPathAnimation = "Assets/AddressableAssets/Animation/";
        public const string AssetPathUI = "Assets/AddressableAssets/UI/";
        //Zone 프리팹. 이 아래에 Zone 번호로 둔다 (Zone/1001.prefab)
        public const string AssetPathZone = "Assets/AddressableAssets/Zone/";
        public const string AssetExtensionPrefab = ".prefab";
        public const string AssetExtensionAnimation = ".anim";
        #endregion

        #region Tag / Layer
        public const string TagMap = "Map";
        public const string LayerHitBox = "HitBox";
        //캐릭터끼리는 서로 밀지 않도록 이 레이어에 둔다. 충돌 매트릭스에서 자기 자신과의 충돌을 꺼둔 상태
        public const string LayerIndependent = "Independent";

        /// <summary>레이어 이름으로 번호를 찾는다. 프로젝트 설정에 없으면 에러를 남기고 false를 돌려준다</summary>
        public static bool TryGetLayer(string layerName, out int layer)
        {
            layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) return true;

            Debug.LogError($"Layer {layerName} is not defined.");
            return false;
        }
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
        //공격 커맨드는 누른 공격 버튼 번호를 순서대로 이어붙인 문자열이다.
        //버튼이 1번뿐이면 "1" → "11" → "111"로 쌓이고, 이 문자열로 AttackCommand 테이블에서 공격을 찾는다.
        //버튼이 늘어나면 2, 3을 넘겨서 "12", "121"처럼 쌓인다
        public const int AttackButtonDefault = 1;

        //공격의 종류. 한 번의 커맨드에 마법과 기계가 같이 나가며, 테이블은 타입마다 한 줄씩 들고 있다.
        //번호는 AttackCommand 테이블의 Type 컬럼에 그대로 적는다
        public enum AttackType
        {
            None = 0,
            Magic = 1,
            Machine = 2,
        }

        //한 단계가 끝난 뒤 다음 입력을 기다려주는 시간. 이 시간을 넘기면 커맨드가 처음으로 돌아간다
        public const float AttackCommandResetTime = 0.4f;
        #endregion

        #region Counter
        //테이블로 차후 관리해야하는 부분
        //카운터는 한 번 쓸 때마다 스택을 하나 쓰고, 모자란 만큼 일정 시간마다 하나씩 다시 찬다
        public const int CounterMaxStack = 1;
        public const float CounterChargeInterval = 1f;
        //카운터 키를 누른 뒤 공격을 받아낼 수 있는 시간. 이 안에 맞으면 피격 대신 카운터가 된다
        public const float CounterWaitDuration = 0.7f;
        //카운터에 성공하면 나가는 반격. AttackCommand 테이블에서 이 커맨드로 찾는다.
        //공격 버튼 번호는 숫자뿐이라 콤보로 쌓이는 커맨드와 겹치지 않는다
        public const string CounterAttackCommand = "C";
        #endregion

        #region Damage
        //피격을 알렸을 때 맞은 쪽이 어떻게 받았는지. 때린 쪽은 Hit일 때만 맞힌 것으로 센다
        public enum DamageResult
        {
            //맞았다
            Hit,
            //카운터 대기에 받아내졌다
            Countered,
            //무적이라 받지 않았다 (반격 중, Event 중)
            Ignored,
        }

        //테이블로 차후 관리해야하는 부분
        //피격 후 Damage 상태에 묶여 있는 시간. 이 시간이 지나면 Ground나 Air로 돌아간다
        public const float DamageDuration = 5f;
        #endregion

        #region AI
        //테이블로 차후 관리해야하는 부분
        public const float DetectRange = 5f;   //추격을 시작하는 거리
        public const float LoseRange = 8f;     //추격을 포기하는 거리
        public const float AttackRange = 0.5f; //멈춰서 공격하는 거리

        public const float IdleDuration = 1.5f;
        public const float PatrolDuration = 3f;
        //콤보를 다 쓰고 다음 공격을 시작하기까지 쉬는 시간
        public const float AIAttackInterval = 1f;
        //AI가 공격까지 하는지. 끄면 공격 거리에서 멈춰 서기만 한다.
        //const로 두면 꺼진 쪽 코드가 도달 불가 경고를 내므로 readonly로 둔다
        public static readonly bool AIAttackEnabled = false;
        //이 거리 안에서는 대상이 좌우 어느 쪽인지 따지지 않는다
        public const float TargetDirectionDeadZone = 0.05f;
        #endregion

        #region Dialogue
        //대화창 프리팹. AssetPathUI 아래에 있다. 에디터 메뉴 UI > Build Dialogue View가 만든다
        public const string UIDialogueView = "DialogueView";
        //대사가 한 글자씩 나오는 간격(초)
        public const float DialogueCharInterval = 0.03f;
        //이 거리(좌우) 안에서 Interact를 누르면 NPC에게 말을 건다
        public const float InteractDistance = 1.5f;
        #endregion

        #region Screen
        //화면을 덮는 페이드 프리팹. AssetPathUI 아래에 있다. 에디터 메뉴 UI > Build Screen Fade View가 만든다
        public const string UIScreenFadeView = "ScreenFadeView";
        //캔버스 순서. 페이드는 게임 화면을 덮되, 검은 화면 위에서도 대사는 읽혀야 하므로 대화창이 그보다 위에 온다
        public const int SortingOrderScreenFade = 90;
        public const int SortingOrderDialogue = 100;
        //시나리오 FadeIn/FadeOut이 늘 쓰는 시간(초). 표에서는 정하지 않는다
        public const float ScreenFadeDuration = 0.5f;
        //시나리오 Zoom에서 시간을 적지 않았을 때, 그리고 대화 확대와 연출 구간 끝의 카메라 복귀에 쓰는 시간(초)
        public const float CameraZoomDuration = 0.5f;
        //카메라가 따라갈 때 플레이어 발밑에서 화면 가운데까지의 높이. 발밑이 화면 아래쪽에 오게 해서 위쪽 발판과 하늘이 보이게 한다
        public const float CameraFollowHeight = 8.2f;
        //카메라가 플레이어를 따라잡는 데 걸리는 시간(초). 0이면 딱 붙어서 움직이고, 클수록 늦게 따라온다
        public const float CameraFollowSmoothTime = 0.15f;
        #endregion

        #region Story
        //플레이어 캐릭터의 Object 번호. 플레이어는 하나뿐이라 Actor 표에 두지 않고 표에는 Player로만 적는다
        //테이블로 차후 관리해야하는 부분
        public const long PlayerObjectId = 1001;
        //시나리오와 퀘스트 명령의 분류. 대상은 Spawn으로 월드에 들어와서 Despawn이나 Clear로 나가고, 그 사이의 줄은 월드에 있는 대상에만 쓸 수 있다
        public enum StoryStepKind
        {
            //대상을 월드에 세운다 (Spawn)
            Start,
            //월드에 있는 대상을 다루거나 대상 없이 연출한다
            Content,
            //대상을 월드에서 없앤다 (Despawn)
            End,
            //플레이어가 해내야 넘어가는 퀘스트 목표 (Interact, Reach, Clear)
            Goal,
        }
        //대상의 종류. 플레이어는 표에 Player로 적고, 나머지는 Actor 표의 Type 칸에 적는다
        public enum StoryActorType
        {
            //대상이 없는 줄 (Wait, Reach 등)
            None,
            Player,
            //컨트롤러가 없어서 Move로 움직이지 않으면 제자리에 서 있다. 중립이라 싸움에 끼지 않는다
            Npc,
            //AI가 붙는다. Clear가 기다리는 대상이다
            Enemy,
        }
        //시나리오와 퀘스트 번호는 챕터 × 이 값 + 순번이다 (1장 2번 → 102). 한 챕터에 99개까지 둔다
        public const long StoryChapterRange = 100;
        //시나리오와 퀘스트 줄의 Id는 번호 × 이 값 + 줄 번호다 (102 → 102010, 102020, ...).
        //10 단위로 띄워 적어서 줄을 끼울 때 뒤 번호를 밀지 않고 사이 번호(102015)를 쓴다
        public const long StoryLineIdRange = 1000;
        //Actor 표의 번호는 챕터 × 이 값 + 순번이다 (1장 1번 → 10001). 한 챕터에 9999명까지 둔다.
        //다섯 자리라서 시나리오/퀘스트 번호(101), Object 번호(1001), 대화 번호(1001)와 눈으로 구분된다
        public const long StoryActorChapterRange = 10000;
        //Move가 도착했다고 보는 거리. 시간을 정해 빠르게 걸으면 한 물리 스텝에 이보다 멀리 가므로, 목표를 지나친 것도 도착으로 본다
        public const float ScenarioArriveDistance = 0.1f;
        #endregion

        #region World
        //메인 씬에서 시작할 맵. 맵 이동이 생기면 씬 파라미터나 진행 저장으로 넘긴다
        //테이블로 차후 관리해야하는 부분
        public const long StartMapId = 1;
        //Zone 번호는 맵 × 이 값 + 순번이다 (1번 맵 1번 Zone → 1001). 한 맵에 999칸까지 둔다
        public const long ZoneMapRange = 1000;
        //Zone 컬링 거리. 플레이어가 있는 Zone에서 상하좌우와 대각선으로 이 칸 수 이상 떨어진 Zone은 내린다.
        //2면 자기 Zone과 둘레 8칸(3×3)만 올라와 있다. 경계를 넘는 순간 넘어간 쪽의 이웃이 이미 올라와 있어서 빈 곳이 보이지 않는다
        public const int ZoneCullDistance = 2;
        //옆에 Zone이 없는 쪽에 세우는 보이지 않는 벽의 두께. 빠르게 움직이는 대시가 한 물리 스텝에 뚫고 지나가지 않을 만큼 두껍게 둔다
        public const float ZoneWallThickness = 4f;
        #endregion
    }
}
