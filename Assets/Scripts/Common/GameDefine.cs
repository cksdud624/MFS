using UnityEngine;

namespace Common
{
    public static class GameDefine
    {
        #region Enum
        //씬 이름과 같다. 게임은 BootStrap → Title → Main 순으로 넘어간다
        public enum SceneType
        {
            BootStrap = 0,
            Title = 1,
            Main = 2
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
            //쓰러졌다. 가장 높은 단계라 무엇을 하던 중이든 끊고 들어가고, 다시 나오지 않는다
            Dead,
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

        //보스 AI의 큰 상태. 공격 하나하나는 상태가 아니라 패턴(BossPattern)과 커맨드(BossCommand)로 둔다.
        //보스만 쓰는 상태가 필요하면 ControllerBossAI를 상속해서 RegisterStates에 더하고, 값은 100부터 쓴다. 있는 값의 숫자는 바꾸지 않는다
        public enum BossState
        {
            //전투가 시작되기 전. Engage를 기다린다
            Intro = 0,
            //패턴 사이에 쉰다
            Idle = 1,
            //패턴 하나를 골라 끝까지 한다
            Pattern = 2,
            //반격을 맞고 굳어 있다
            Groggy = 3,
            //체력바 하나가 다 깎여서 다음 체력바로 넘어가는 중이다
            PhaseTransition = 4,
            //쓰러졌다. 다른 상태로 넘어가지 않는다
            Dead = 5,
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
            //쓰러지는 모션. 반복하지 않는 클립이어야 한다. 끝나야 오브젝트가 사라진다
            Death,
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
        //공격 판정을 보여주는 임시 도형. 캐릭터에 가려지면 범위를 읽을 수 없어서 캐릭터보다 앞에 그린다.
        //타격은 예고보다 하나 앞에 그려서 같은 자리의 예고 위에 보인다
        public const int SortingOrderAttackShape = 3000;
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
        //이동, 점프, 대시의 수치는 mori-kit의 전투 명세(FightTune)를 옮긴 것이다.
        //그쪽의 원래 길이 단위(캐릭터 폭 0.7)가 이 프로젝트와 같아서 값을 그대로 쓴다

        //걸음 속도(Object 표의 MoveSpeed)까지 붙는 시간 / 입력을 뗐을 때 멈추는 시간 / 반대로 돌아설 때 걸음 속도만큼 바꾸는 시간.
        //명세는 가속도(50, 35, 90 U/s²)인데, 걸음 속도 4.475 기준의 시간으로 바꿔서 둔다.
        //걸음 속도가 오브젝트마다, 또 연출과 테스트 키로 달라져도 같은 박자로 붙고 멈추게 하려는 것이다
        public const float MoveAccelerationTime = 0.09f;
        public const float MoveDecelerationTime = 0.128f;
        public const float MoveTurnTime = 0.05f;
        //반대 방향을 이 시간(초)보다 길게 눌러야 방향 전환으로 본다. 그보다 짧으면 보는 방향만 바뀌고 가던 속도는 그대로다
        public const float MoveTurnHoldTime = 0.1f;
        //이보다 느리게 움직이고 있으면 반대 입력을 방향 전환이 아니라 출발로 본다
        public const float MoveTurnSpeedThreshold = 0.5f;
        //공중에서(피격 중에는 땅에서도) 입력을 뗐을 때는 감속을 이만큼만 받는다
        public const float AirDecelerationMultiplier = 0.5f;
        //대시나 공중 점프로 걸음 속도를 넘어선 만큼은 감속에 이 배율을 곱해서 서서히 푼다
        public const float OverSpeedDecelerationMultiplier = 0.6f;

        //중력 가속도(U/s²)와 최대 낙하 속도(U/s). 프로젝트 물리 설정의 중력과 상관없이 이 값이 되도록 Rigidbody의 gravityScale을 맞춘다
        public const float Gravity = 120f;
        public const float MaxFallSpeed = 18f;
        //올라가는 동안은 중력을 그대로 받고, 정점 근처(세로 속도가 이 값 안쪽)에서는 조금 가볍게, 떨어질 때는 훨씬 가볍게 받는다.
        //빠르게 뛰어올라서 천천히 내려오는 점프가 된다
        public const float ApexSpeedBand = 3f;
        public const float ApexGravityMultiplier = 0.9f;
        public const float FallGravityMultiplier = 0.34f;

        //지상 점프의 초속(U/s)
        public const float JumpPower = 24.5f;
        //지상에서 얻는 점프 횟수 / 지상에서 떨어졌을 때 남는 점프 횟수
        public const int GroundJumpCount = 2;
        public const int FallJumpCount = 1;
        //공중 점프. 누른 쪽으로 가로 속도를 더하고 위로 뛴다. 이 시간(초) 동안은 중력을 덜 받고 가로 속도 초과분도 깎지 않는다
        public const float AirJumpHorizontalSpeed = 10f;
        public const float AirJumpVerticalSpeed = 12f;
        public const float AirJumpDuration = 0.18f;
        public const float AirJumpGravityMultiplier = 0.5f;
        #endregion

        #region Dash
        //테이블로 차후 관리해야하는 부분
        //대시 한 번에 가는 거리(U)와 걸리는 시간(초). 속도는 이 둘에서 나온다. 대시하는 동안에는 맞지 않는다
        public const float DashDistance = 4.1f;
        public const float DashDuration = 0.07f;
        public const int DashMaxStack = 2;
        //스택 하나가 차는 시간(초). mori-kit의 값(10)은 이 게임의 전투에는 길어서 절반으로 둔다
        public const float DashChargeInterval = 5f;
        //대시하는 동안 받는 중력의 배율
        public const float DashGravityMultiplier = 0.3f;
        //대시가 끝났을 때 남기는 속도. 가로는 걸음 속도의 이 배율까지, 위로는 점프 초속의 이 배율까지, 아래로는 DashEndFallSpeed까지만 남긴다
        public const float DashCarryMultiplier = 1.35f;
        public const float DashEndUpMultiplier = 0.5f;
        public const float DashEndFallSpeed = 2f;
        //공중 대시가 끝난 뒤 잠깐 떠 있는 시간(초)과 그동안 받는 중력의 배율
        public const float DashHangDuration = 0.2f;
        public const float DashHangGravityMultiplier = 0.3f;
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
        public const float AttackCommandResetTime = 0.3f;
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
        #endregion

        #region Status
        //상태 효과(버프, 디버프, 타고난 특성)가 하는 일은 두 가지다. 수치를 보정하거나(Stat), 처리 흐름을 바꾼다(Trait).
        //이름은 StatusModifier 표의 Kind, Target, Operation 칸에 그대로 적는다
        public enum StatusModifierKind
        {
            Stat,
            Trait,
        }

        //상태 효과가 보정하는 수치. 계산은 (기본값 + 더하기의 합) × 곱하기의 곱이다
        public enum StatType
        {
            MachineAttack,
            MachineDefense,
            MagicAttack,
            MagicDefense,
            //받는 경직 시간의 배율. 기본값이 1이라 곱하기 0.5면 절반만 굳는다
            StaggerTaken,
        }

        public enum StatOperation
        {
            Add,
            Multiply,
        }

        //상태 효과가 켜는 특성. 수치로는 표현할 수 없는 규칙을 바꾼다
        public enum StatusTrait
        {
            //맞아도 피격 상태에 들어가지 않는다. Hp는 깎이지만 하던 행동이 끊기지 않는다
            StaggerImmune,
        }
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

        #region Boss
        //테이블로 차후 관리해야하는 부분
        //보스전의 규칙은 mori-kit의 결투(Duel)를 옮긴 것이다. 보스마다 다른 것(페이즈, 패턴, 커맨드)은 Boss, BossPattern, BossCommand 표에 있고,
        //여기는 보스전을 어디서 누구와 벌이는지와, 보스의 공격을 알리는 예고와 판정 도형의 값이다

        //Main 씬에 들어오면 바로 벌이는 보스전. 플레이어가 서 있는 Zone이 보스 스테이지가 되고, 그 Zone에 이 보스를 세운다.
        //이야기(Quest 표)를 거치지 않으므로 보스와 서는 자리를 여기서 정한다
        public const long BossBattleBossObjectId = 1004;
        //체력바에 띄우는 보스의 이름. 이야기의 보스전은 Actor 표의 Name을 쓴다
        public const string BossBattleBossName = "결투자";
        //보스가 서는 자리 (보스 스테이지 Zone의 로컬 좌표). Zone 가운데다
        public static readonly Vector2 BossBattleBossPosition = new(57.6f, 5.95f);

        //페이즈 전환 동안 체력바 아래에 띄우는 이름. 패턴이 아니어서 BossPattern 표에 이름이 없다
        public const string BossPhaseTransitionName = "페이즈 전환";

        //보스가 공격하기 전에 어떤 공격이 오는지 색으로 알리는 예고. 피하는 법이 종류마다 다르다
        public enum AttackTelegraph
        {
            None,
            //근접 베기. 거리를 벌리거나 카운터로 받아낸다
            Melee,
            //바닥 쓸기. 점프로 넘거나 카운터로 받아낸다
            Low,
            //돌진. 대시로 빠지거나 뛰어넘거나 카운터로 받아낸다
            Charge,
            //카운터 자세. 이때 때리면 반격당한다
            Counter,
        }
        //예고 색. 그림이 없어서 보스의 스프라이트에 이 색을 TelegraphTintStrength만큼 섞는다
        public static readonly Color TelegraphColorMelee = new(1f, 0.3f, 0.2f);
        public static readonly Color TelegraphColorLow = new(1f, 0.9f, 0.2f);
        public static readonly Color TelegraphColorCharge = new(1f, 0.3f, 0.95f);
        public static readonly Color TelegraphColorCounter = new(0.25f, 0.9f, 1f);
        public const float TelegraphTintStrength = 0.75f;

        /// <summary>예고의 색. 예고가 없으면 흰색이다</summary>
        public static Color GetTelegraphColor(AttackTelegraph telegraph) => telegraph switch
        {
            AttackTelegraph.Melee => TelegraphColorMelee,
            AttackTelegraph.Low => TelegraphColorLow,
            AttackTelegraph.Charge => TelegraphColorCharge,
            AttackTelegraph.Counter => TelegraphColorCounter,
            _ => Color.white,
        };

        //공격 판정을 화면에 보여주는 임시 도형. 공격 이펙트의 그림이 없는 동안 판정이 있는 자리에 그 크기로 띄운다.
        //이름은 AttackHitBox 표의 Shape 칸에 그대로 적는다. 빈칸이면 띄우지 않는다 (이펙트 그림이 있는 공격)
        public enum AttackShape
        {
            None,
            Square,
            //오른쪽을 보는 기준으로 뾰족한 끝이 앞을 향한다
            Triangle,
            Circle,
        }
        //판정이 켜져 있는 동안의 색. 판정이 꺼지면 AttackShapeFadeDuration(초)에 걸쳐 사라진다.
        //판정이 아주 짧게 켜지는 공격도 어디를 쳤는지 눈에 남게 한다
        public static readonly Color AttackShapeColor = new(1f, 1f, 1f, 0.9f);
        public const float AttackShapeFadeDuration = 0.25f;
        //곧 나갈 공격의 범위를 미리 보여줄 때의 투명도. 색은 예고 색을 쓴다
        public const float AttackShapePreviewAlpha = 0.3f;
        #endregion

        #region Title
        //타이틀 화면 프리팹. AssetPathUI 아래에 있다. 에디터 메뉴 UI > Build Title View가 만든다
        public const string UITitleView = "TitleView";
        #endregion

        #region Loading
        //씬을 넘어갈 때 화면을 덮는 로딩 화면 프리팹. AssetPathUI 아래에 있다. 에디터 메뉴 UI > Build Loading View가 만든다
        public const string UILoadingView = "LoadingView";
        //로딩 화면은 씬이 준비되는 동안 그 씬의 UI를 모두 덮어야 하므로 가장 위에 둔다
        public const int SortingOrderLoading = 200;
        //새 씬이 준비를 마친 뒤 로딩 화면이 걷히는 시간(초)
        public const float LoadingFadeDuration = 0.3f;
        //로딩 글 끝의 점이 하나씩 늘어나는 간격(초). 멈춘 것이 아니라 불러오는 중임을 알린다
        public const float LoadingDotInterval = 0.3f;
        #endregion

        #region Dialogue
        //말풍선 프리팹. AssetPathUI 아래에 있다. 에디터 메뉴 UI > Build Dialogue View가 만든다
        public const string UIDialogueView = "DialogueView";
        //대사가 한 글자씩 나오는 간격(초)
        public const float DialogueCharInterval = 0.03f;
        //이 거리(좌우) 안에서 Interact를 누르면 NPC에게 말을 건다
        public const float InteractDistance = 1.5f;
        //이 거리(좌우) 안에 들어오면 일반 대화를 할 수 있는 NPC 위에 대화 아이콘을 띄운다.
        //말을 걸 수 있는 거리보다 조금만 넓게 둔다. 바로 앞까지 다가갔을 때 뜨고, 뜬 뒤 한 걸음이면 말을 걸 수 있다
        public const float TalkMarkerDistance = 2.5f;
        #endregion

        #region Screen
        //화면을 덮는 페이드 프리팹. AssetPathUI 아래에 있다. 에디터 메뉴 UI > Build Screen Fade View가 만든다
        public const string UIScreenFadeView = "ScreenFadeView";
        //캔버스 순서. 페이드는 게임 화면을 덮되, 검은 화면 위에서도 대사는 읽혀야 하므로 말풍선이 그보다 위에 온다
        public const int SortingOrderScreenFade = 90;
        public const int SortingOrderDialogue = 100;
        //목표와 대화 아이콘 프리팹. AssetPathUI 아래에 있다. 에디터 메뉴 UI > Build Marker View가 만든다
        public const string UIMarkerView = "MarkerView";
        //화면 왼쪽 위의 퀘스트 안내 프리팹. AssetPathUI 아래에 있다. 에디터 메뉴 UI > Build Quest View가 만든다
        public const string UIQuestView = "QuestView";
        //화면 위쪽 가운데의 보스 체력바 프리팹. AssetPathUI 아래에 있다. 에디터 메뉴 UI > Build Boss View가 만든다
        public const string UIBossView = "BossView";
        //아이콘과 퀘스트 안내, 보스 체력바는 게임 화면의 일부라서 페이드가 덮을 때 같이 가려지게 페이드보다 아래에 둔다
        public const int SortingOrderHud = 80;
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
        //말풍선에 띄우는 플레이어 이름. 다른 대상의 이름은 Actor 표의 Name 칸에 적는다
        //테이블로 차후 관리해야하는 부분
        public const string PlayerDisplayName = "플레이어";
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
        //화면에 띄우는 아이콘의 종류. 퀘스트와 시나리오는 지금 목표가 가리키는 곳에 뜨고 멀리서도 보인다
        public enum StoryMarkerKind
        {
            //퀘스트 목표 (말을 걸 Npc, 가야 할 곳, 쓰러뜨릴 적)
            Quest,
            //퀘스트 목표인데, 채우면 바로 시나리오(연출)가 이어진다
            Scenario,
            //일반 대화를 할 수 있는 Npc. 가까이 가야 보인다
            Talk,
        }
        //Reach 목표의 목적지는 발밑 기준이라, 아이콘은 이만큼 위(캐릭터 머리 높이)에 띄운다
        public const float GoalPointMarkerHeight = 2.5f;
        //Move가 도착했다고 보는 거리. 시간을 정해 빠르게 걸으면 한 물리 스텝에 이보다 멀리 가므로, 목표를 지나친 것도 도착으로 본다
        public const float ScenarioArriveDistance = 0.1f;
        #endregion

        #region World
        //메인 씬에서 시작할 맵. 맵 이동이 생기면 씬 파라미터나 진행 저장으로 넘긴다
        //테이블로 차후 관리해야하는 부분
        public const long StartMapId = 1;
        //Main 씬에 들어오면 플레이어가 서는 Zone과 자리 (Zone 로컬 좌표). 지금은 이 Zone이 그대로 보스 스테이지가 되어서,
        //보스(BossBattleBossPosition, Zone 가운데)의 첫 공격이 바로 닿지 않게 왼쪽으로 떨어진 자리에 선다
        //테이블로 차후 관리해야하는 부분
        public const long PlayerStartZoneId = 1002;
        public static readonly Vector2 PlayerStartPosition = new(45.6f, 5.95f);
        //Zone 번호는 맵 × 이 값 + 순번이다 (1번 맵 1번 Zone → 1001). 한 맵에 999칸까지 둔다
        public const long ZoneMapRange = 1000;
        //Zone 컬링 거리. 플레이어가 있는 Zone에서 상하좌우와 대각선으로 이 칸 수 이상 떨어진 Zone은 내린다.
        //2면 자기 Zone과 둘레 8칸(3×3)만 올라와 있다. 경계를 넘는 순간 넘어간 쪽의 이웃이 이미 올라와 있어서 빈 곳이 보이지 않는다
        public const int ZoneCullDistance = 2;
        //옆에 Zone이 없는 쪽에 세우는 보이지 않는 벽의 두께. 빠르게 움직이는 대시가 한 물리 스텝에 뚫고 지나가지 않을 만큼 두껍게 둔다
        public const float ZoneWallThickness = 4f;
        //보스가 서 있는 Zone에 플레이어가 경계에서 이만큼 안쪽으로 들어와야 보스전이 시작된다.
        //경계에 걸친 채 Zone을 잠그면 막는 벽이 몸 위에 선다
        public const float BossArenaEnterMargin = 3f;
        #endregion
    }
}
