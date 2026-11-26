# CardIdle

Unity 6 (6000.6.8f1) 2D 액션 프로젝트. URP 2D, 새 Input System 전용, Addressables, UniTask, R3, FMOD를 쓴다.
R3는 코어(`R3.dll`)를 NuGetForUnity로(`Assets/packages.config`, `Assets/Packages/`), Unity 확장(`com.cysharp.r3`)을 UPM git URL로 받는다. 올릴 때는 두 버전을 같이 맞춘다.
코드는 `Assets/Scripts` 아래에 있고, 공개 포트폴리오로 쓰이는 코드라서 읽히는 품질을 기능만큼 중요하게 본다.
씬은 `BootStrap`(전역 준비) → `Title`(타이틀 메뉴) → `Main`(게임) 순으로 넘어가고, Build Settings에도 이 순서로 둔다(빌드는 첫 씬에서 시작한다).
씬을 넘어갈 때는 `Global.Instance.SceneLoader.LoadScene`을 쓴다. 로딩 화면이 바로 덮고, 새 씬의 `InitScene`(에셋과 Zone 불러오기)이 끝나면 걷힌다.
**지금 Main은 이야기(Quest 표)를 돌리지 않고, 들어오자마자 보스전을 벌인다.** `GameController`가 맵과 카메라를 만들고 시작 Zone(`GameDefine.PlayerStartZoneId`)에 플레이어를 세운 뒤 `BossBattleController.StartStage`에 넘긴다.
플레이어가 선 Zone이 그대로 보스 스테이지가 되고, `BossBattleController`가 그 Zone에 보스를 세워서 싸움을 시작한다.
둘이 선 뒤에야 `InitScene`이 끝난다. 먼저 걷히면 아무도 없는 맵이 보인다.

## 폴더와 네임스페이스

네임스페이스는 폴더 경로를 그대로 따른다 (`Assets/Scripts/InGame/Component/State` → `InGame.Component.State`).

| 경로 | 역할 |
|---|---|
| `Common/` | 전역(`Global` 싱글톤), 씬 로더, `AssetManager`, `GameDefine`(상수/enum) |
| `Common/Template/` | `Singleton`, `StateMachine`/`IState`, `IUpdateable` 계열 인터페이스 |
| `Common/Scene/` | `SceneLoader`가 씬을 넘기고 넘어가는 동안 로딩 화면으로 덮는다. 씬마다 시작점(`SceneLoadBase`를 상속한 `SceneLoadXxx`)을 하나 두고, 그 씬의 준비를 `InitScene`에서 끝까지 기다린다. `InitScene`이 끝나야 로딩 화면이 걷힌다 |
| `Common/UI/` | 씬과 상관없이 쓰는 UI (`LoadingView`: 로딩 화면. 게임이 켜질 때 가장 먼저 만들어서 `SceneLoader` 아래에 두고 끝까지 쓴다) |
| `Common/Test/` | 에디터 전용 테스트 코드 (`EditorTestKeys`: 누르면 바로 반응하는 테스트 키, `EditorTestSettings`: Play를 시작할 때의 조건을 바꾸는 설정) |
| `Title/` | 타이틀 씬. `SceneLoadTitle`이 씬의 시작점이고, `TitleController`가 타이틀 화면을 띄우고 고른 메뉴(시작, 종료)를 처리한다 |
| `Title/UI/` | `TitleView`: 게임 이름과 메뉴(시작, 이어하기, 종료). 키보드(방향키/WASD, Enter)와 마우스로 고른다. 이어하기는 저장 기능이 생길 때까지 프리팹에서 꺼둔다. 프리팹은 `InGame/UI/`와 같이 `UIPrefabBuilder`가 만들고(UI > Build Title View), 버튼이 입력을 받는 데 필요한 EventSystem도 프리팹에 들어 있다. Title 씬에는 `SceneLoader`(`SceneLoadTitle`)와 `Main Camera`만 둔다 |
| `InGame/Object/` | `ObjectBase`/`CharacterBase`, `ObjectSpawner` |
| `InGame/Context/` | `InGameContext`, `ObjectContext`, `InputContext`: 컴포넌트 사이를 이어주는 상태와 이벤트 |
| `InGame/Component/` | 오브젝트에 붙는 기능 컴포넌트 (물리, 애니메이션, 이펙트, 판정) |
| `InGame/Component/State/` | 오브젝트 FSM 상태 (`Ground`, `Air`, `Action`, `Damage`, `Event`, `Dead`). 뒤로 갈수록 높은 단계라 앞의 상태를 끊고 들어간다 |
| `InGame/Component/Controller/` | 플레이어/AI 컨트롤러, AI 상태. `ControllerBossAI`는 `ControllerAI`를 상속해서 행동 상태 머신만 보스의 것으로 바꾼다. 모든 보스가 이 하나를 쓴다 |
| `InGame/Component/Controller/Boss/` | 보스 AI의 큰 상태(`Intro`, `Idle`, `Pattern`, `Groggy`, `PhaseTransition`, `Dead`), 보스의 설계(`BossDefinition`)와 그것을 표에서 만드는 `BossFactory`. 아래 "보스전" 참고 |
| `InGame/Component/Controller/Boss/Patterns/` | 패턴 실행 객체(`BossPattern`, 표에서 만든 `BossCommandPattern`), 패턴 설계(`BossPatternDefinition`, `BossPatternSettings`), 고르기(`BossPatternSelector`, `BossPatternContext`) |
| `InGame/Component/Controller/Boss/Commands/` | 보스 커맨드(패턴을 이루는 동작) 하나당 클래스 하나. `BossCommand`를 상속하고 `BossCommandFactory`에 이름을 등록한다. Param1~2를 어떻게 쓰는지는 각 클래스 summary에 적는다 |
| `InGame/Story/` | 인게임 흐름. `StoryPlayer`가 퀘스트를 번호 순으로 실행하고, `StoryValidator`가 실행 전에 표 전체를 검사해 `StoryBook`을 만든다. 월드에 세운 대상은 `StoryContext`가 들고, `TalkPlayer`가 일반 대화를, `DialoguePlayer`가 대사를 띄운다. `StoryHudPresenter`가 지금 띄울 퀘스트 안내와 목표/대화 아이콘을 정한다 |
| `InGame/Story/Steps/` | 시나리오와 퀘스트가 같이 쓰는 명령 (`DialogueStep`. 어디서 쓰는지에 따라 카메라를 쓸지 정한다) |
| `InGame/Story/Scenario/Steps/` | 시나리오(연출) 명령 하나당 클래스 하나. `ScenarioStepFactory`에 이름을 등록한다 |
| `InGame/Story/Quest/Steps/` | 퀘스트(목표, 시나리오 재생, 보스전) 명령 하나당 클래스 하나. `QuestStepFactory`에 이름을 등록한다. 새 명령은 시나리오/퀘스트 모두 `StoryStep`을 상속해서 `Parse`(칸 형식), `Validate`(앞뒤 정합성), `Run`을 구현하고, 시작/종료/목표 명령이면 `Kind`를 다시 정한다. Actor와 Param1~2를 어떻게 쓰는지는 각 클래스 summary에 적는다 |
| `InGame/UI/` | UI 뷰 (`DialogueView`: 말하는 캐릭터의 머리 위를 따라다니는 말풍선, `ScreenFadeView`, `MarkerView`: 대상이나 지점 위의 아이콘, `QuestView`: 왼쪽 위의 퀘스트 안내, `BossView`: 위쪽 가운데의 보스 체력바). 프리팹은 `Editor/UIPrefabBuilder`의 메뉴(UI > Build All UI)가 코드로 조립해서 `AddressableAssets/UI/`에 저장한다. 레이아웃은 프리팹이 아니라 빌더 코드를 고친다 |
| `InGame/Battle/` | 전투 진행. 보스와 관련된 것은 모두 `BossBattleController`가 맡는다: 스테이지(`StartStage`: 세워진 플레이어를 넘겨받아, 플레이어가 선 Zone을 보스 스테이지로 삼고 그 Zone에 보스를 세운다), 시작(`Begin`: `BossFactory`로 Boss 표들에서 그 보스의 `BossDefinition`을 만들고, 보스의 컨트롤러를 `ControllerBossAI`로 붙이거나 바꿔서(`ObjectBase.ReplaceController`) 설계를 넣은 뒤, 보스가 선 Zone을 잠그고 `Engage`를 부른다), 진행(보스 체력바 `BossView`를 띄운다), 끝(보스가 쓰러지면 잠금을 풀고 체력바를 숨긴다). 이야기의 `Boss` 명령도 `StoryContext`를 거쳐 이것을 쓴다 |
| `InGame/World/` | 맵과 Zone. `ZoneLayout`이 Map/Zone 표로 격자 배치를 만들고 로컬↔월드 좌표를 바꾼다. `ZoneStreamer`가 플레이어 주변 Zone과 시나리오의 무대 Zone만 불러두고(보스전이 벌어지는 Zone은 잠근다), `IZoneLoader`(지금은 `PrefabZoneLoader`)가 Zone 내용물을 불러오고 내린다 |
| `InGame/CameraDirector.cs` | 게임 카메라를 움직이는 유일한 곳. 평소에는 플레이어를 따라가고, 연출(시나리오 Zoom, 대화 확대) 동안에는 바라볼 곳으로 옮긴다. 화면은 Zone 밖을 보여주지 않는다. 씬 카메라의 처음 화각을 배율 1로 본다 |
| `Generated/Table/` | **자동 생성 코드. 직접 고치지 않는다** (`Custom/` 폴더의 partial만 손으로 작성) |
| `Editor/` | 에디터 툴 (`TableUpdater`, `StoryValidatorMenu`, `DefaultSceneSetter`, `UIPrefabBuilder`, `TableBytes`: 생성된 `.bytes`를 Play 없이 읽는다, `TestMenu`: 테스트 설정을 켜고 끄는 상단 메뉴 Test). 네임스페이스 없이 둔다 |
| `Editor/StoryTool/` | 시나리오 툴 (메뉴 Story > Story Tool, 네임스페이스 `StoryTool`). 아래 "시나리오 툴" 참고 |

## 코딩 컨벤션

### 이름 짓기
- private 필드: `_camelCase` (`_activeColliders`, `_hitBoxLayer`)
- protected 필드, 프로퍼티, 메서드, 상수: `PascalCase` (`protected ObjectContext ObjectContext;`)
- `[SerializeField]` 필드: `_camelCase` 사용 (기존 `SceneLoadMain.gameController`는 예외로 남아 있음)
- 이벤트는 `On` + 무엇이 일어났는지: `OnAttackStart`, `OnDirectionChanged`, `OnAttackHitBoxActiveChanged`
- 이벤트 핸들러: `Handle` + 이벤트 이름 (`HandleDashRestart`), 혹은 역할을 바로 드러내는 동사 (`BeginAttack`)
- 상수는 분류를 앞에 붙인다: `LayerHitBox`, `TagMap`, `AssetPathEffect`, `SortingOrderPlayer`

### 구조와 흐름
- **초기화는 `Awake`/`Start`가 아니라 명시적인 `Init(...)`으로 한다.** 에셋 로드처럼 비동기가 필요할 때만 `async UniTask Init(...)`을 쓰고,
  나머지는 `void Init(...)`으로 둔다. `await UniTask.CompletedTask`로 모양만 맞추지 않는다.
  컴포넌트는 `ObjectBase.Init`에서 `AddComponent` 후 순서대로 `Init`을 부른다(비동기인 것만 `await`). 순서에 의존성이 있으면 그 이유를 주석으로 단다.
- `Init` 안은 **참조 저장 → 자기 준비(컴포넌트, 콜라이더, 에셋 로드) → 이벤트 구독과 `Global.BindXxx`** 순으로 쓴다.
  준비가 끝나기 전에 구독하면 반쯤 만들어진 상태에서 핸들러가 불릴 수 있다.
- 붙어 있으면 쓰고 없으면 붙이는 컴포넌트는 `if (!TryGetComponent(out _xxx)) _xxx = gameObject.AddComponent<Xxx>();`로 쓴다.
- 레이어는 `GameDefine.TryGetLayer(...)`로 찾는다. 없을 때 에러 로그는 여기서 남기므로 부르는 쪽은 없을 때 어떻게 할지만 정한다.
- 컴포넌트끼리는 서로 직접 참조하지 않고 `ObjectContext`를 거친다.
  상태를 바꿀 때는 `SetXxx(...)`를 부르고, `SetXxx`가 값을 갱신하고 `OnXxxChanged` 이벤트를 쏜다.
- 이벤트 구독은 `Init`/`OnEnter`에서 하고 해제는 `OnDestroy`/`OnExit`에서 짝을 맞춰 한다. 구독을 추가했으면 해제도 반드시 함께 추가한다.
- 매 프레임 업데이트가 필요한 순수 C# 객체는 `IUpdateable`/`IFixedUpdateable`/`ILateUpdateable`을 구현하고 `Global.BindUpdate`로 묶는다.
- 프로퍼티는 `{ get; private set; }`로 쓰기를 막는다. 계산값은 `=>` 식 본문을 쓴다.
- 함수 앞부분에서 가드절로 일찍 return 한다 (`if (hitBox == null || ownedByEffect) return;`).
- 컬렉션은 `readonly`로 만들어두고 `Clear()`해서 재사용한다. 핫패스(`FixedUpdate`, 트리거 콜백)에서는 할당과 LINQ를 피하고 `for` 루프를 쓴다.
- 생성은 target-typed `new()`를 쓴다 (`private readonly List<bool> _hitBoxActive = new();`).

### region 분류
클래스 멤버는 아래 순서의 `#region`으로 나눈다. 해당하는 멤버가 없는 region은 만들지 않는다. 기준 파일은 `InGame/Component/ObjectStateController.cs`다.

| region | 들어가는 것 |
|---|---|
| `Types` | 중첩 클래스, 구조체, enum |
| `Fields` | 필드, `[SerializeField]`, 상태를 담는 프로퍼티, 계산 프로퍼티, 상수. 필드끼리의 순서는 바꾸지 않는다 (초기화 순서가 바뀐다) |
| `Init` | 생성자, `Init(...)` |
| `LifeCycle` | Unity 생명주기(`Awake`, `Start`, `OnEnable`, `OnDisable`, `OnDestroy`, `Update`, `FixedUpdate`, `LateUpdate`), `IUpdateable` 계열 콜백(`OnUpdate`, `OnFixedUpdate`, `OnLateUpdate`), FSM 상태의 `OnEnter`/`OnExit`/`OnFixedUpdate` |
| `Public` | 밖에서 부르는 public 메서드 (`Init`은 제외, `Dispose`는 포함). 많으면 `Public - 주제`로 나눠도 된다 |
| `Protected` | 상속받는 쪽이 쓰는 protected 메서드 |
| `Event Handlers` | 이벤트에 바인드되는 핸들러 (`OnXxx`, `HandleXxx`, 입력 콜백, 애니메이션 끝 콜백), Unity 물리 콜백(`OnTriggerEnter2D` 등). public/protected여도 이벤트에 묶이는 용도면 여기에 둔다 |
| `Private` | 내부 함수. 많으면 `Private - 주제`로 나눠도 된다 (`Private - Target`) |
| `Editor` | `#if UNITY_EDITOR`로 감싼 디버그/테스트 코드 (`OnDrawGizmos`, 테스트 키 핸들러). `#if`는 region 안쪽에 둔다 |

- 멤버가 한 분류뿐인 작은 클래스, 인터페이스, 자동 생성 코드에는 region을 두지 않는다.
- `ObjectContext`, `InputContext`, `GameDefine`처럼 상태/이벤트/Set 함수가 주제별로 묶여 있는 클래스는 위 분류 대신 주제로 나눈다 (`Direction`, `Attack`, `Counter` ...).

### GameDefine과 using
- enum과 상수는 `Common.GameDefine`에 `#region`으로 분류해서 모은다. 코드 곳곳에 매직 넘버를 두지 않는다.
  나중에 테이블로 옮길 값이면 그 사실을 주석으로 남긴다 (`//테이블로 차후 관리해야하는 부분`).
- 가져올 때는 `using static Common.GameDefine;` 또는 필요한 enum만 별칭으로 가져온다
  (`using Direction = Common.GameDefine.Direction;`). 파일 안에서 이미 쓰는 방식을 따른다.

### 좌우 방향
- 테이블과 상수의 오프셋은 **오른쪽을 보는 기준**으로 적는다. 사용하는 쪽에서는 `ObjectContext.GetDirectionalOffset(...)`으로 뒤집고, 부호를 직접 따지지 않는다.

### 주석
- 주석은 **한국어**로, `//` 뒤에 공백 없이 붙여 쓴다 (`//같은 진영은 때리지 않는다`).
- **무엇을 하는지보다 왜 그렇게 했는지**를 적는다. 다른 방식을 쓰면 무엇이 깨지는지 적는다
  (예: `//Rigidbody는 본체 것을 그대로 쓰므로 붙이지 않는다. 붙이면 판정이 본체와 따로 놀게 된다`).
- 클래스와 주요 public/핵심 메서드에는 `/// <summary>`를 한국어로 단다. 한 줄이면 한 줄로 쓴다.
- 필드 위에는 그 필드가 무엇을 기록하는지 한 줄 주석을 단다.
- 문장은 평서형(`~한다`, `~이다`)으로 쓴다.

### 로그
- 설정 오류나 데이터 누락은 `Debug.LogError`에 영어로 짧게 쓴다 (`$"Layer {LayerHitBox} is not defined."`).
- 동작 확인용 로그는 한국어나 `[태그]` 접두를 쓴다 (`$"[테스트 키] {key} : {description}"`). 디버깅이 끝나면 지울지 판단한다.

### 에디터 전용 코드
- 테스트/디버그 코드는 `#if UNITY_EDITOR`로 감싸서 빌드에 들어가지 않게 한다. `using`도 같이 감싼다.
- 테스트 단축키는 `Common/Test/EditorTestKeys.cs` 한 곳에 모은다. 게임 조작 키(이동 WASD/방향키, 점프 Alt, 대시 Space와 Shift, 공격 Ctrl, 카운터 Z, 상호작용 F)와 겹치지 않게 고른다. 키를 추가할 때는 static 이벤트를 하나 만들고 `TestKeys` 배열에 한 줄 추가한다.
  반응하는 쪽은 그 이벤트를 구독/해제한다.
- Play를 시작할 때의 조건을 바꾸는 테스트 설정은 `Common/Test/EditorTestSettings.cs`에 모으고, 상단 메뉴 **Test**(`Editor/TestMenu.cs`)에서 체크로 켜고 끈다.
  값은 EditorPrefs에 남는다(이 PC에만 저장되고 레포에 올라가지 않는다). 설정을 추가할 때는 `EditorTestSettings`에 값을 만들고 `TestMenu`에 메뉴 항목과 체크 표시 함수를 한 쌍 추가한다.
  - **Test > Start At Boss Battle** : 켜고 Play하면 타이틀을 건너뛰고(`Global.Load`), 이야기를 보스전(`Boss` 명령)이 있는 첫 퀘스트부터 시작한다(`StoryPlayer.FindTestStart`).
    플레이어는 보스가 설 자리에서 `BossBattleStartDistance`만큼 떨어진 Zone 안쪽에 선다. 앞 퀘스트가 세웠을 다른 대상은 월드에 없다.
    지금 Main은 이야기를 돌리지 않으므로 타이틀을 건너뛰는 것만 의미가 있다.
- 판정 범위 같은 시각 디버그는 `OnDrawGizmos`를 `#if UNITY_EDITOR`로 감싸서 쓴다.
- 입력은 새 Input System만 쓴다. `UnityEngine.Input`(구 입력)은 쓰지 않는다.

## 테이블 (데이터)

- 원본은 `Assets/.Table/*.tsv`. 첫 줄은 컬럼 이름, 둘째 줄은 타입 (`long`, `int`, `float`, `bool`, `string`, `Vector2`, `List<long>` ...).
- 챕터마다 길어지는 표는 챕터 폴더로 나눈다: `Assets/.Table/Chapter1/Scenario.tsv`, `Chapter2/Scenario.tsv` ...
  지금 나눠 둔 표는 Scenario, Quest, QuestInfo, Talk다. 그 챕터의 줄만 그 폴더에 둔다.
  Update Table이 같은 이름의 파일을 챕터 번호 순으로 이어 붙여 표 하나로 만들므로, 생성 코드와 `.bytes`는 나누지 않았을 때와 같다.
  파일마다 머리 두 줄이 같아야 하고, Id는 다른 챕터 파일과도 겹치면 안 된다. 한 표를 맨 위와 챕터 폴더에 같이 둘 수 없고, 폴더 이름은 `Chapter` + 번호만 받는다.
  새 챕터는 폴더를 만들고 위 표의 머리 두 줄을 복사해서 시작한다.
- 수정 후 Unity 메뉴 **Table > Update Table**을 실행하면 다음이 갱신된다:
  - `Assets/Generated/Table/*.bytes` (데이터)
  - `Assets/Scripts/Generated/Table/*Record.cs`, `TableManager.cs` (코드)
  - `Assets/.Table/TableManifest.json` (해시)
- 첫 칸은 Id이고 생성된 Record가 키로 쓴다. 타입은 `long`, `int`, `string` 중 하나다.
  Update Table은 표를 쓰기 전에 모든 표의 모양(헤더 칸 수, 줄마다 칸 수, 빈 Id, 숫자 Id 형식, 겹친 Id)을 확인하고, 하나라도 틀리면 아무것도 갱신하지 않는다.
  숫자 Id는 읽은 값으로 비교하고(`010`과 `10`은 같은 Id), 문자열 Id는 적힌 그대로(대소문자 구분) 비교한다.
- 생성된 Record에 조회 기능이 필요하면 `Generated/Table/Custom/XxxRecord.Custom.cs`에 partial로 추가하고 `partial void InitCustomRecord()`에서 색인을 만든다.
- 번호 컬럼의 `0`은 "사용 안 함"을 뜻한다.
- 그 줄에서 쓰지 않는 칸은 빈칸으로 둔다. 빈칸은 숫자면 `0`, `Vector2`/`Vector3`면 `zero`, 문자열이면 빈 문자열로 읽힌다 (에러 없음).
  `0,0` 같은 채움값을 적지 않는다. 실제 값과 구분이 안 된다. 형식이 틀린 값(`1,2,3`을 `Vector2`에)은 지금처럼 에러를 낸다.
- 테이블 변경은 `.tsv`, `.bytes`, `TableManifest.json`, 생성 코드가 한 세트로 같이 커밋되어야 한다.

### 오브젝트와 캐릭터 (Object, Character)

오브젝트 하나는 같은 번호로 여러 곳에 걸쳐 있다. 새 오브젝트를 만들 때는 아래를 모두 같은 번호로 맞춘다.

| 어디 | 무엇 |
|---|---|
| `Object.tsv` | `IsCharacter`, `MoveSpeed`, 콜라이더, `Tint`(스프라이트에 곱하는 색 r,g,b 0~1. 빈칸이면 원래 색. 같은 그림을 쓰는 오브젝트를 색으로 구분한다) |
| `Character.tsv` | 캐릭터의 전투 수치. `Hp`, `MachineAttack`/`MachineDefense`(기계), `MagicAttack`/`MagicDefense`(마법), `StatusEffects`(타고난 상태 효과 번호들), `Boss`(보스 이름. Boss 표의 Id. 빈칸이면 보스가 아니다. 아래 "보스전"). 캐릭터(`IsCharacter`)는 꼭 한 줄 있어야 한다 |
| `AttackCommand.tsv`, `AttackHitBox.tsv` | 공격 커맨드와 판정. Id가 오브젝트 번호로 시작한다 (1001 → 100101, 10010101). `Damage`는 그 공격의 위력, `Stagger`는 맞은 쪽을 굳히는 시간(초), `AttackTime`은 그 단계 동안 제자리에 묶이는 시간(초)이다. `AdvanceDistance`/`AdvanceTime`은 그 단계가 시작될 때 앞으로 나아가는 거리와 걸리는 시간(초. 빈칸이면 물리 스텝 한 번에 간다)이다. AttackHitBox의 `Shape`는 판정을 화면에 보여줄 임시 도형이다 (`Square`, `Triangle`, `Circle`. 빈칸이면 보여주지 않는다) |
| `AddressableAssets/Animation/(번호)/`, `Effect/(번호)/` | 애니메이션 클립(`Idle`, `Move`, `Jump`, `Fly`, `Damage`, `Death`, `Attack1`…)과 이펙트 프리팹 |

- 공격은 마법과 기계 두 종류로 나가고(`AttackCommand`의 `Type`) 공격력과 방어력도 종류마다 따로 둔다. 마법 공격은 마법 방어력으로, 기계 공격은 기계 방어력으로 받는다.
- 수치는 `ObjectContext`의 Status 구역에서 읽는다 (`Hp`, `MaxHp`, `GetAttack(종류)`, `GetDefense(종류)`). 캐릭터는 Hp를 가득 채운 채로 등장한다.
  공격력과 방어력은 표의 값에 걸려 있는 상태 효과를 반영한 값이다. 표의 값을 직접 읽지 않고 꼭 이 함수로 읽는다.
- **피해:** 깎이는 Hp = `공격의 Damage + 공격한 쪽의 공격력 − 맞은 쪽의 방어력` (0보다 작으면 0). 한 커맨드에 마법과 기계 판정이 따로 나가므로 각각 한 번씩 계산한다.
  때린 쪽이 `AttackHit`(종류, 위력, 경직 시간, 카운터로 받아낼 수 있는지)을 만들어 넘기고(`GetAttackHit`), 맞은 쪽이 `RequestDamage` 안에서 방어력을 빼고 깎는다.
  맞았을 때(`Hit`)만 깎고, 카운터로 받아냈거나 무적이면 깎지 않는다.
  무적은 연출 중(`Event`), 대시하는 동안, `ObjectContext.IsInvincible`(보스 AI가 페이즈 전환 중에 켠다)이다.
- **경직:** 맞으면 그 공격의 `Stagger`만큼 `Damage` 상태로 굳는다. 굳은 채로 또 맞으면 남은 시간과 새 경직 중 긴 쪽으로 다시 정한다.
  맞은 쪽이 고친 값(`ObjectContext.StaggerDuration`)이 0이면 **맞기는 하되 굳지 않는다**. Hp는 깎이지만 `Damage` 상태에 들어가지 않고 하던 행동과 콤보가 이어진다.
  경직이 없는 공격(`Stagger` 빈칸)과 경직 면역이 이 한 곳으로 모인다.
  **반격(`AttackHit.IsCounterAttack`)은 경직 면역을 뚫는다.** 늘 면역인 보스를 굳힐 수 있는 것은 보스의 공격을 받아낸 반격뿐이다.
- **전진:** 공격은 그 단계가 끝날 때까지 제자리에 묶지만, `AdvanceDistance`를 적은 단계는 시작할 때 보는 쪽으로 그만큼 나아간다.
  한 걸음 내딛는 베기(시간 빈칸)부터 몸으로 밀고 가는 돌진(시간을 `AttackTime`과 같게, 판정도 그 시간 동안)까지 같은 칸으로 적는다.
  마법과 기계 줄 중 거리를 적어둔 첫 줄의 것을 쓴다(`Animation`과 같다). 지상에서는 낭떠러지 끝에서 멈춘다.
- **쓰러짐:** Hp가 0이 되면 `Dead` 상태(가장 높은 단계. 입력도 피격도 받지 않는다)로 들어가 `Death` 클립을 틀고, 끝나면 스포너가 없앤다. 퀘스트의 `Clear`는 이렇게 사라지기를 기다린다.
  `Death` 클립은 반복하지 않게 만든다(Loop Time 끔). 반복하면 끝났다는 신호가 오지 않아서 사라지지 않는다. 클립이 없으면 바로 사라진다.
  플레이어는 쓰러진 뒤의 처리가 아직 없어서 `[전투] 플레이어 사망` 로그만 남기고 계속 움직인다.
- 지금 `Attack1~3`, `Damage`, `Death` 클립은 그림이 없어서 점프·낙하 그림을 다시 쓴 임시 클립이다.
- 1002(Npc, 녹색)와 1003(적, 붉은색)은 1001을 복사한 테스트용 오브젝트다. 이펙트는 1001 프리팹의 변형(Variant)이라 1001을 고치면 같이 바뀐다.
- 1004(보스 결투자, 보라색)도 1001의 클립을 복사해서 쓴다. 공격에 이펙트를 쓰지 않아서(판정을 몸이 직접 든다) 이펙트 프리팹은 없고, 판정을 도형으로 보여준다 (AttackHitBox의 `Shape`).

### 이동, 점프, 대시

수치는 mori-kit의 전투 명세를 옮긴 것이다(`GameDefine`의 `Move`, `Dash`. 그쪽의 원래 길이 단위가 이 프로젝트와 같아서 값을 그대로 쓴다).
몸을 움직이는 것은 `PhysicsController` 한 곳이고, 플레이어와 AI(보스 포함)가 같은 규칙으로 움직인다.

- **걷기:** 걸음 속도는 Object 표의 `MoveSpeed`다. 바로 붙지 않고 가속(`MoveAccelerationTime`)과 감속(`MoveDecelerationTime`)을 거친다.
  가던 방향과 반대로 누르면 `MoveTurnHoldTime` 동안은 보는 방향만 바뀌고, 그보다 길게 누르면 방향을 바꾼다. 공중과 피격 중에는 덜 감속한다.
  가속과 감속을 걸음 속도에 붙는 **시간**으로 둔 것은 걸음 속도가 오브젝트, 연출, 테스트 키로 달라져도 같은 박자로 붙고 멈추게 하려는 것이다.
  걸음 속도를 넘어선 속도(대시, 공중 점프가 남긴 것)는 서서히 풀고, 걸음 속도 자체가 느려지면(`OnMoveSpeedLowered`) 그만큼 바로 줄인다.
- **점프:** 중력은 프로젝트 설정이 아니라 `Gravity`를 쓴다(gravityScale로 맞춘다). 올라갈 때는 그대로, 정점 근처와 떨어질 때는 가볍게 받아서
  빠르게 뛰어올라 천천히 내려온다. 공중 점프는 누른 쪽으로 밀어주면서 뛴다(`RequestAirJump`).
- **대시:** `DashDistance`를 `DashDuration`에 간다. 물리 스텝 단위로만 움직이므로 시간을 스텝 수로 바꾸고 그 스텝 수에 거리를 맞춘다.
  **대시하는 동안에는 맞지 않는다.** 끝나면 속도를 조금만 남기고, 공중이면 잠깐 떠 있는다. 스택은 `DashMaxStack`개이고 하나가 차는 데 `DashChargeInterval`초다.
- 연출의 `Move`는 도착하면 가던 속도를 바로 지운다(`PhysicsController.StopHorizontal`). 감속하는 동안 정한 자리를 지나치지 않게 한다.

### 보스전 (Boss)

mori-kit의 결투(Duel)를 옮기는 중이다. **큰 상태는 FSM, 패턴은 커맨드 목록을 차례로 기다리며 실행하는 async 시퀀스, 패턴 고르기는 조건 + 우선순위 + 가중치다.**
행동 트리는 쓰지 않는다. 보스는 "쉰다 → 패턴을 고른다 → 정해진 동작을 차례로 한다"를 되풀이하는 정해진 안무라서 상태가 적고 전이가 뻔하다.

**보스는 코드가 아니라 공통 코드 + 표 데이터로 조립한다.** 모든 보스가 `ControllerBossAI` 하나와 같은 상태, 같은 커맨드를 쓰고, 보스마다 다른 것(페이즈, 패턴, 커맨드와 그 값)은 표에 있다.
보스 전용 클래스, 보스 전용 상태 enum, 보스 전용 패턴 클래스를 만들지 않는 것이 기본이다.
보스는 스폰될 때 다른 적처럼 일반 AI(`ControllerAI`)가 붙고, 보스전이 시작될 때 `BossBattleController.Begin`이 `BossFactory`로 설계(`BossDefinition`)를 만들어 `ControllerBossAI`로 바꾸고 넣는다(`SetDefinition`) → `Engage`.

| 층 | 공통 코드 | 보스마다 다른 데이터 |
|---|---|---|
| 큰 상태 | `ControllerBossAI` + 상태 6개(`GameDefine.BossState`) | `Boss.tsv` : 체력바 수, 쉬는 시간, 페이즈 전환 시간 |
| 패턴 | `BossCommandPattern`, `BossPatternSelector` | `BossPattern.tsv` : 이름, 쓸 페이즈, 가중치, 우선순위, 쿨타임, 거리, 끝난 뒤 쉬는 시간 |
| 동작 | `Boss/Commands/`의 커맨드 (`BossApproachCommand` …) | `BossCommand.tsv` : 패턴마다 커맨드 목록과 Param |

| 표 | 칸 |
|---|---|
| `Boss.tsv` | `Id`(보스 이름. Character 표의 `Boss` 칸에 적는 이름 `Duelist`), `PhaseCount`(체력바 수), `IdleDuration`(패턴이 끝나서 온 게 아닐 때 쉬는 초), `PhaseTransitionDuration`(전환 중 무적으로 멈춰 있는 초), `Note` |
| `BossPattern.tsv` | `Id`(보스마다 100 단위로 묶는다. 첫 보스 101~, 둘째 201~), `Boss`, `Name`(체력바 아래에 뜬다), `Phases`(1부터. `2,3`. 빈칸이면 모든 페이즈), `Weight`(0보다 커야 한다), `Priority`, `Cooldown`(초), `MinDistance`/`MaxDistance`(대상과의 **좌우** 거리. 빈칸이면 제한 없음), `RecoveryDuration`(끝까지 했을 때 다음 패턴까지 쉬는 초. 0이면 바로 잇는다), `Note` |
| `BossCommand.tsv` | `Id`(패턴 × 1000부터 10 단위. 101 → 101010, 101020 …. 끼울 때는 사이 번호), `Pattern`, `Command`, `Param1`, `Param2`, `Note`. 패턴은 Id 순으로 하나씩 끝나기를 기다리며 실행한다 |

| 커맨드 | 하는 일 | Param1 / Param2 |
|---|---|---|
| `Approach` | 대상 쪽으로 걷는다. 멈출 거리 안, 최대 시간, 벽이나 낭떠러지에서 끝난다 | 멈출 거리 / 최대 초 |
| `Retreat` | 대상에게서 멀어지는 쪽으로 걷는다 (등을 돌린다) | 벌릴 거리 / 최대 초 |
| `FaceTarget` | 대상 쪽으로 돌아선다. 공격은 시작한 방향으로 잠기므로 `Attack` 앞에 둔다 | |
| `Attack` | 공격 버튼을 눌러 콤보를 끝까지 잇는다. 앞 공격의 콤보가 풀릴 때까지(`AttackCommandResetTime`) 기다린 뒤 누른다 | AttackCommand 표의 `Command` (`1`, `22`, `333`) / |
| `Counter` | 카운터 대기. 받아내면 반격(`C`)까지 끝나야 끝난다. 카운터가 차지 않았으면 그냥 넘어간다 | |
| `Telegraph` | 예고 색을 건다. 패턴이 끝나거나 끊기면 저절로 지워진다 | `Melee`, `Low`, `Charge`, `Counter`, `None` / |
| `Wait` | 기다린다 (예고를 보여준 뒤의 틈) | 초 / |

- **상태:** `Intro`(Engage 전) → `Idle`(쉬는 동안 대상을 본다) ⇄ `Pattern`(하나 골라 끝까지). 끼어드는 것은 `ControllerBossAI`가 직접 바꾼다:
  반격을 맞고 굳으면 `Groggy`(굳는 시간은 오브젝트의 `Damage` 상태가 센다. 풀리면 `Idle`), 체력바가 넘어가면(`OnPhaseChanged`) `PhaseTransition`(무적, 끝나면 `Idle`), 쓰러지면(`OnDead`) `Dead`.
  상태가 바뀌면 `BossPatternState.OnExit`이 패턴을 취소하고 이동 입력, 예고, 패턴 이름을 거둔다. 시나리오가 보스를 붙잡을 때(`Suspend`)도 패턴을 내려놓는다.
  쉬는 시간은 패턴이 끝까지 갔을 때만 그 패턴의 `RecoveryDuration`이고, 그 밖(전투 시작, 굳음과 전환 뒤)은 `IdleDuration`이다.
- **고르기 (`BossPatternSelector.TrySelect`):** 강제 지정(`ForceNextPattern`)이 있으면 그것 → 지금 페이즈의 패턴 중 `CanStart`(가중치, 쿨타임, 거리)를 통과한 것 → 그중 우선순위가 가장 높은 것만 →
  둘 이상이면 바로 앞 패턴을 빼고 → 가중치로 뽑는다. 대상이 없으면 거리 제한이 있는 패턴은 뽑히지 않는다. 고를 것이 없으면 쉬기로 돌아가서 다시 고른다.
  모든 페이즈에 패턴이 하나는 있어야 한다(`BossFactory`가 확인한다).
- **설계와 실행 객체:** `BossDefinition`(`BossPatternDefinition`, 커맨드)은 바뀌지 않는 설계다. 쿨타임처럼 싸우는 동안 바뀌는 것은 보스마다 만드는 `BossCommandPattern`이 든다.
  그래서 **커맨드 클래스는 칸에서 읽은 값만 필드에 두고, 실행 중의 값은 `Run`의 지역 변수로만 둔다.**
- **표 검사:** 보스전이 시작될 때 `BossFactory.TryCreate`가 세 표를 읽으며 틀린 곳을 모두 에러로 남기고, 하나라도 틀리면 보스전을 시작하지 않는다
  (없는 보스 이름, 칸 형식, 쓰지 않는 칸에 값, 없는 커맨드, 페이즈 범위, 비어 있는 페이즈, 그 보스의 AttackCommand 표에 없는 콤보 단계와 반격). Update Table과 Validate Story는 이 내용을 검사하지 않는다.
- **새 보스 (코드 없음):** Object, Character(`Boss` 칸), AttackCommand, AttackHitBox에 줄을 만들고, Boss, BossPattern, BossCommand 표에 줄을 더한다.
- **코드가 필요할 때만:** 새 동작은 `BossCommand`를 상속해 `Commands`에 만들고(`Parse`, 다른 표를 봐야 하면 `Validate`, `Run`) `BossCommandFactory`에 이름을 등록한다. 모든 보스가 같이 쓸 수 있게 만든다.
  그 보스만 쓰는 상태는 `ControllerBossAI`를 상속해서 `RegisterStates`(base 뒤에, 키는 `BossState` 100번부터), 표로 나타낼 수 없는 패턴은 `BossPattern`을 상속해 `RegisterPatterns`에서 등록한다.
  패턴이 새 입력이나 규칙을 필요로 하면 `ObjectContext`를 거치게 한다.

| 무엇 | 어디 |
|---|---|
| 공격의 위력, 판정, 전진 | `AttackCommand`, `AttackHitBox` 표. `Attack` 커맨드의 Param1이 그 `Command`다 (`"2"` → `"22"`로 콤보가 이어진다) |
| 어디서 누구와 싸우는지 | 플레이어가 서 있는 Zone이 보스 스테이지다. 플레이어가 서는 곳은 `GameDefine`의 `PlayerStartZoneId`/`PlayerStartPosition`, 보스는 `BossBattleBossObjectId`, `BossBattleBossName`(체력바의 이름), `BossBattleBossPosition`(스테이지 Zone의 로컬 좌표). Main에 들어오면 바로 싸운다 |
| 전투 진행 | `BossBattleController` (`InGame/Battle/`). 스테이지(플레이어를 넘겨받아 그 Zone에 보스 스폰), 시작(설계 생성, `ControllerBossAI`로 교체, Zone 잠금, `Engage`), 체력바(`BossView`), 끝(보스가 쓰러지면(`InGameContext.OnObjectDefeated`) 잠금 해제)을 모두 맡는다. 보스 AI는 여기서만 붙이고 깨운다 |
| 이야기 안의 보스전 | Quest 표의 `Boss` 명령 (아래 "이야기 표"). 지금 Main은 이야기를 돌리지 않아서 쓰이지 않는다 |

보스 AI와 같이 쓰는 것:
- **입력만 낸다 (원칙):** 보스도 플레이어와 같은 입력(이동, 공격 버튼, 카운터, 대시)만 낸다. 판정, 피해, 받아내기, 경직은 플레이어의 공격과 같은 경로로 처리된다.
- **예고:** 스프라이트에 공격 종류의 색을 섞는다 (`ObjectContext.SetTelegraph` → `AnimationPlayer`). `Telegraph` 커맨드가 건다.
  근접(빨강)은 거리를 벌리고, 하단(노랑)은 뛰어넘고, 돌진(분홍)은 대시로 빠지고, 카운터 자세(하늘색)는 때리지 않는다. 그림이 없어서 색으로 대신한 것이다.
- **판정 도형 (임시 이펙트):** 공격 이펙트의 그림이 없어서, 판정이 있는 자리를 기본 도형으로 보여준다 (`AttackShapeView`. 캐릭터마다 붙는다).
  AttackHitBox 표의 `Shape` 칸에 도형을 적은 판정만 보인다. 판정이 켜져 있는 동안 그 크기로 흰 도형이 뜨고, 꺼지면 `AttackShapeFadeDuration` 동안 사라진다.
  곧 나갈 공격의 범위를 예고 색으로 옅게 미리 보여주는 기능(`ObjectContext.SetAttackPreview`)도 있다. 지금은 부르는 곳이 없다.
  전진하는 공격은 나아간 뒤의 자리에, 돌진처럼 전진하는 내내 판정이 켜져 있는 공격은 지나가는 길 전체에 그린다.
  도형의 스프라이트는 파일이 아니라 코드가 그린다 (`AttackShapeSprite`). 이펙트 그림이 생기면 `Shape` 칸을 비우고 `Effect` 칸을 쓴다.
  이펙트를 쓰는 공격(`Effect` 칸)은 판정을 이펙트가 들고 다니므로 도형을 띄우지 않는다.
- **패턴 이름 표시 (확인용):** 보스 체력바 아래에 지금 하는 것의 이름(BossPattern 표의 `Name`, 전환 중에는 `BossPhaseTransitionName`)을 띄운다 (`ObjectContext.SetPatternName` → `BossView`).
- **경직 면역과 반격:** 보스는 타고난 경직 면역(`StatusEffects`에 1)이라 맞아도 굳지 않는다. **보스의 공격을 카운터로 받아낸 반격만 보스를 굳힌다.**
- **체력바와 페이즈:** 체력바 하나가 페이즈 하나다 (`ObjectContext.Phase`, `PhaseCount`). 체력바가 다 깎이면 쓰러지지 않고 다음 체력바가 가득 찬다.
  체력바 수는 `SetDefinition`이 Boss 표의 `PhaseCount`로 정한다(`ObjectContext.SetPhaseCount`). 페이즈 번호는 체력바가 넘어갈 때 `ObjectContext`가 올리고, 보스 AI는 그 알림을 받아 전환 상태로 간다.
  전환 중에는 `ObjectContext.SetInvincible`로 맞지 않는다.
- 메뉴 **Test > Start At Boss Battle** : 체크하고 Play하면 타이틀과 앞 퀘스트를 건너뛰고 바로 보스전으로 들어간다.
- **옮긴 것:** 거리 재기와 패턴 고르기, 패턴(베기, 2연격, 3연격, 바닥 쓸기, 돌진, 카운터 자세, 물러나 돌진, 다가가기. 1004 결투자), 예고, 페이즈 전환(무적과 멈춤).
- **mori-kit에서 옮길 것:** 연타에 받아치기(밀쳐내기 `6`), 경직이 풀린 뒤의 회피(대시 커맨드), 페이즈마다 빨라지기와 공격력 오르기, 곧 나갈 공격 범위 미리 보기(`SetAttackPreview`),
  에너지탄, 내려찍기, 맵 패턴(미사일, 낙뢰, 레이저, 장벽), 넉백과 월 바운스, 카운터 크래시, 빈틈 찌르기와 습관 읽기, 구석 탈출, 페이즈 전환 때의 플레이어 회복.
  mori-kit의 박(곡의 박자에 맞추는 진행, 리듬 판정)은 옮기지 않는다. 보스의 시간은 초로 잰다.

### 상태 효과 (StatusEffect, StatusModifier)

버프, 디버프, 타고난 특성을 모두 상태 효과로 다룬다. 효과가 하는 일은 두 가지이고, 효과 하나가 여러 개를 가질 수 있어서 표를 둘로 나눈다.

| 표 | 칸 |
|---|---|
| `StatusEffect.tsv` | `Id`, `Duration`(초. 빈칸이면 무기한), `MaxStack`(겹치는 최대 수. 빈칸이면 1), `Note` |
| `StatusModifier.tsv` | `Id`(효과 × 100 + 순번), `StatusEffect`(어느 효과의 것인지), `Kind`, `Target`, `Operation`, `Value`, `Note` |

| `Kind` | 하는 일 | `Target` | `Operation` / `Value` |
|---|---|---|---|
| `Stat` | 수치를 보정한다 | `GameDefine.StatType`의 이름 (`MachineAttack`, `MagicDefense`, `StaggerTaken` …) | `Add` 또는 `Multiply`, 값 |
| `Trait` | 처리 흐름을 바꾼다 | `GameDefine.StatusTrait`의 이름 (`StaggerImmune`) | 비운다 |

- 수치는 `(기본값 + 더하기의 합) × 곱하기의 곱`으로 계산한다. 더하기를 먼저 모아서 효과가 걸린 순서에 따라 결과가 달라지지 않게 한다.
  겹친 효과는 더하기를 겹친 수만큼 더하고, 곱하기를 겹친 수만큼 거듭 곱한다.
- 수치로 표현할 수 없는 규칙은 `Trait`으로 둔다. 경직 면역(`StaggerImmune`)은 "받는 경직 × 0"이 아니라 특성이다. 공격이 얼마나 굳히든 무시한다는 뜻을 표에 그대로 적는다.
  받는 경직을 줄이기만 하려면 `Stat`의 `StaggerTaken`(기본값 1인 배율)에 `Multiply`를 건다.
- 같은 효과를 다시 걸면 새로 추가하지 않고 중첩을 올리고(`MaxStack`까지) 남은 시간을 처음으로 되돌린다.
- 걸고 푸는 것은 `ObjectContext.StatusEffects`(`Add`, `Remove`, `Has`)로 한다. 타고난 효과는 `Character.tsv`의 `StatusEffects`에 적으면 등장할 때 걸린다.
  남은 시간은 `StatusEffectController`가 물리 스텝마다 센다.
- 새 수치나 특성을 만들 때는 `GameDefine`의 `StatType`/`StatusTrait`에 이름을 추가하고, 그 수치를 읽는 곳이 `StatusEffects.Apply`나 `HasTrait`을 거치게 한다.
  표의 이름이 틀리면 표를 불러올 때 에러를 남기고 그 줄을 뺀다.
- 지금은 효과를 거는 스킬이나 아이템이 없어서, 타고난 효과와 테스트 키(`3` 경직 면역 켜기/끄기, `4` 플레이어 공격력 증가)로만 걸린다.

### 이야기 표 (Actor, Scenario, Quest, Talk)

지금 Main은 보스전만 벌이고 이야기를 돌리지 않는다(맨 위 참고). 아래 표와 코드, 시나리오 툴, Validate Story는 그대로 남아 있다.

인게임 흐름은 네 표가 나눠 맡는다. 시나리오와 퀘스트 번호는 표마다 따로 `챕터 × 100 + 순번`으로 매긴다 (1장 1번 → 101, 1장 2번 → 102, 2장 1번 → 201).
대상(Actor)은 `챕터 × 10000 + 순번`으로 매긴다 (1장 1번 → 10001). 다섯 자리라서 시나리오/퀘스트 번호, Object 번호, 대화 번호와 눈으로 구분된다.
시나리오와 퀘스트 표에서는 번호만 보이므로 누구인지는 Actor 표의 `Note`에 적고, 줄의 `Note`에도 필요하면 같이 적는다.

| 표 | 무엇 | 명령 / 칸 |
|---|---|---|
| `Actor.tsv` | 월드의 캐릭터 등록. 시나리오, 퀘스트, 대화는 대상을 이 번호로 가리킨다 | `Id`(10001), `ObjectId`, `Type`(Npc/Enemy), `Name`(말풍선에 띄우는 이름), `Resident`, `Zone`, `Position`, `Note`(누구인지) |
| `Scenario.tsv` | 대형 연출. 플레이어가 조작하지 않는 장면 | `Spawn`, `Despawn`, `Place`, `Move`, `Effect`, `Dialogue`, `Line`, `CloseDialogue`, `Wait`, `FadeOut`/`FadeIn`, `Zoom` |
| `Quest.tsv` | 진행. 미션(목표)과 그에 딸린 대화. 퀘스트 번호 순서가 곧 진행 순서이고, 이야기는 첫 퀘스트부터 시작한다 | `Scenario`(시나리오 재생), `Dialogue`, 목표 `Interact`, `Reach`, `Clear`, `Boss`(보스전). 목표 줄은 `Text`에 플레이어가 할 일을 적는다 |
| `QuestInfo.tsv` | 퀘스트 하나에 대한 것. 줄을 묶는 번호만 있는 Quest 표에는 적을 자리가 없어서 따로 둔다 | `Id`(퀘스트 번호, 101), `Title`(화면의 퀘스트 안내에 띄우는 제목) |
| `Talk.tsv` | 가벼운 일반 대화. 퀘스트 목표가 아닐 때 Npc에게 말을 걸면 나오는 대화 | `Id`(챕터 × 100 + 순번, 101. `Quest`와 같은 챕터), `Actor`, `Quest`(이 퀘스트가 시작된 뒤부터), `Dialogue` |

대사 텍스트는 `Dialogue.tsv`에만 두고 세 표가 가리킨다. 칸은 `Id`(대사 한 줄, 100101), `DialogueId`(대화 묶음, 1001), `Speaker`, `Text`다.
대사는 화면 아래 대화창이 아니라 **말하는 캐릭터의 머리 위 말풍선**으로 뜬다. 그래서 `Speaker`에는 이름이 아니라 그 대사를 말하는 대상을 적는다 (`Player` 또는 Actor 번호).
말풍선에 띄우는 이름은 Actor 표의 `Name`에서 오고(플레이어는 `GameDefine.PlayerDisplayName`), 대사를 말하는 대상은 `Name`이 있어야 한다.
일반 대화(Talk)의 대사는 말을 건 Npc나 플레이어만 말할 수 있다. 표마다 대화의 무게가 다르다.

| 무게 | 어디서 | 대화하는 동안 |
|---|---|---|
| 가벼움 | Talk | 플레이어만 묶고 월드는 움직인다 |
| 중간 | Quest의 `Dialogue` | 플레이어만 묶고 카메라는 그대로 둔다. 일반 대화 중이면 끝나기를 기다린다 |
| 대형 | Scenario의 `Dialogue`(대화 하나), `Line`(대사 한 줄) | 월드가 멈춘 연출 안이다. `Dialogue`는 배율을 적으면 카메라를 확대한다 |

- 대상은 `Actor` 한 칸에 `Player` 또는 Actor 표의 번호(`10001`)로 적는다. 같은 캐릭터를 여럿 세워도 번호가 달라서 위치나 순서로 추측하지 않는다.
  플레이어는 하나뿐이라 Actor 표에 두지 않고 `Player`로 적는다(번호 칸의 0은 "사용 안 함"이라 쓸 수 없다). 캐릭터는 `GameDefine.PlayerObjectId`를 쓴다.
- Actor 표에는 이야기가 개별로 가리키는 캐릭터만 둔다. 이름 없는 적 무리는 나중에 웨이브/스폰 표로 따로 다루고, 퀘스트는 `Clear`(대상 비움)로 기다린다.
- 대상은 시나리오나 퀘스트가 아니라 **월드가 갖는다**. `Spawn`으로 들어와서 `Despawn`이나 `Clear`로 나가기 전까지 뒤의 퀘스트와 시나리오가 같은 번호로 쓴다.
  `Resident`를 켠 대상은 이야기가 시작될 때 `Zone`의 `Position`에 서 있다. `Spawn`의 Zone과 위치를 둘 다 비우면 Actor 표의 자리에 선다(플레이어는 꼭 적는다).
- 표의 위치는 모두 **Zone 로컬 좌표**다 (아래 "맵과 Zone"). 위치를 쓰는 명령(`Spawn`, `Place`, `Move`, `Reach`)은 `Zone` 칸에 기준 Zone을 적고, 다른 명령은 `Zone` 칸을 비운다.
- 시나리오 하나가 통째로 연출 구간이다. 재생하는 동안 월드의 조작과 AI가 멈추고(도중에 등장한 대상 포함), 끝나면 카메라를 처음 상태로 되돌린 뒤 다시 켠다.
  시나리오가 끝나면 목표가 이어지므로 화면을 덮은 채(`FadeOut`) 끝날 수 없다.
- 시나리오는 월드에 이미 있는 대상을 번호로 가리키기만 하면 넘겨받는다(따로 넘겨받는 명령은 없다). 시나리오가 옮겨 놓은 대상은 끝난 뒤에도 그 자리에 남는다.
  돌아다니던 대상을 연출이 시작될 자리에 세울 때는 `Place`로 바로 옮긴다(걷지 않는다. 보통 `FadeOut` 뒤에 쓴다). 아직 월드에 없는 대상은 `Spawn`으로 세운다.
- **무대 Zone:** 시나리오는 시작할 때 자기가 무대로 쓰는 Zone을 다 올라올 때까지 기다리고, 끝나면 푼다.
  무대는 줄이 대상을 세우거나 옮기는 Zone(`Spawn`, `Place`, `Move`의 `Zone` 칸, 비운 `Spawn`은 Actor 표의 Zone)과 출연하는 대상이 그때 서 있는 Zone이다.
  그래서 컬링되어 꺼져 있던 대상도 연출에 쓸 수 있다. 새 위치 명령을 만들면 `GetStageZone`을 다시 정한다.
- 퀘스트 목표를 지나는 동안 월드에 있던 적은 플레이어와 싸워 쓰러졌을 수 있다. 그래서 목표를 지난 적은 시나리오가 다룰 수 없고 `Clear`로만 가리킨다.
  `Clear`는 대상을 적으면 그 적, 비우면 월드의 적 전부가 쓰러질 때까지 기다리고, 통과한 적은 월드에서 나간 것으로 본다.
- **보스전(`Boss`):** 대상(보스)이 서 있는 Zone이 보스 전투 Zone이다. 플레이어가 그 Zone에 들어오면(경계에서 `BossArenaEnterMargin` 안쪽)
  Zone을 잠그고 보스를 깨우고, 보스가 쓰러지면 잠금을 풀고 넘어간다. 잠긴 동안에는 다른 Zone으로 나갈 수 없고 카메라도 그 Zone 밖을 보여주지 않는다.
  전투 중에는 화면 위쪽에 보스의 이름(Actor 표의 `Name`)과 체력바, 페이즈를 띄운다 (`BossBattleController` → `BossView`). Zone 잠금과 시작 판정도 `BossBattleController`가 한다.
  대상은 적이고 Character 표의 `Boss` 칸이 적혀 있어야 한다(검사가 확인한다). `Clear`처럼 통과한 보스는 월드에서 나간 것으로 본다.
  등장 연출은 앞 줄에 `Scenario`로 둔다. Zone이나 위치 칸은 쓰지 않는다 (보스의 자리는 Actor 표와 `Spawn`이 정한다).
- 일반 대화는 시나리오 재생 중이나 다른 대화 중에는 나오지 않고, `Interact` 목표가 기다리는 Npc에게 건 말은 목표로 간다.
- **퀘스트 안내:** 기다리는 목표가 있는 동안 화면 왼쪽 위에 퀘스트 제목(QuestInfo의 `Title`)과 할 일(목표 줄의 `Text`)을 띄운다 (`StoryHudPresenter` → `QuestView`).
  목표 사이(대화, 연출)에는 숨긴다. 그래서 목표 줄은 `Text`를 꼭 적고, 목표가 아닌 줄은 비운다. 퀘스트는 모두 QuestInfo에 제목이 있어야 한다(검사가 확인한다).
- **화면 아이콘:** 표를 따로 두지 않고 Quest, Talk 표에서 정해지는 것을 그대로 띄운다 (`StoryHudPresenter` → `MarkerView`).
  - 퀘스트 아이콘 : 지금 기다리는 목표가 가리키는 곳. `Interact`의 Npc, `Reach`의 목적지, `Clear`의 적(대상을 비웠으면 적 전부), `Boss`의 보스.
  - 시나리오 아이콘 : 같은 자리에 퀘스트 아이콘 대신. 목표 바로 다음 줄이 `Scenario`라서 채우면 바로 연출이 이어질 때다.
  - 대화 아이콘 : 지금 할 일반 대화가 있는 Npc. 플레이어가 `TalkMarkerDistance`(2.5. 말을 걸 수 있는 거리 1.5보다 조금 넓다) 안에 왔을 때만 뜬다. 목표가 가리키는 Npc에는 뜨지 않는다.
  - 퀘스트와 시나리오 아이콘은 거리와 상관없이 뜨고, 대상이 화면 밖이면(Zone이 내려가 꺼진 대상 포함) 화면 가장자리에 붙어 방향을 알려준다.
  - 시나리오 재생 중과 대화 중에는 모두 숨긴다.
  - 새 목표 명령을 만들면 기다리는 동안 `StoryContext.SetGoal`로 가리키는 곳과 할 일(`Text`)을 걸고, 끝나면(취소 포함) `ClearGoal`로 지운다.
  - 아이콘은 그림이 없어서 원형 바탕에 글자를 올린 임시 아이콘이다 (`UIPrefabBuilder.CreateMarkerIcon`).
- 시나리오의 `Dialogue` Param2에 배율을 적으면 대화 상대(없으면 플레이어)를 확대했다가 끝나면 대화 직전 카메라로 되돌린다. 퀘스트의 `Dialogue`는 배율 칸이 없다.
- 대사 사이에 연출을 끼울 때는 시나리오에서 `Line`으로 한 줄씩 띄운다. Param1은 대사 한 줄의 Id(`100101`)이고, 대화 번호(`DialogueId`, `1001`)가 아니다.
  `Line` 뒤의 연출 동안 말풍선은 마지막 대사를 띄운 채 남아서 말한 캐릭터를 따라다닌다. `CloseDialogue`, `Dialogue`(대화 하나)가 끝날 때, 또는 시나리오가 끝날 때 닫힌다.
  플레이어는 대사를 읽는 동안만 묶이므로 대사 사이에 `Move Player`를 쓸 수 있다.
- 시나리오와 퀘스트 줄의 Id는 번호에서 만든다: `번호 × 1000`부터 10 단위 (102 → 102010, 102020, ...).
  줄을 끼울 때는 사이 번호(102015)를 쓰고 뒤를 밀지 않는다. 줄은 표에 적힌 순서가 아니라 Id 순으로 실행한다.
- `Note` 칸은 설계자 메모다. 장면 구분이나 목표를 적는다. 게임은 읽지 않는다. 플레이어에게 보여줄 글은 `Text`(목표가 시키는 일)와 QuestInfo의 `Title`에 적는다.
- 이야기 표를 고쳤으면 Update Table 뒤에 **Table > Validate Story**를 실행한다. Play 없이 퀘스트를 번호 순으로, 퀘스트가 부르는 시나리오까지 펼쳐서 따라가며 검사한다:
  칸 형식(타입, 쓰지 않는 칸에 값, 번호 형식, Id 범위와 중복, 표에 없는 명령, Talk Id와 Quest의 챕터),
  앞뒤 관계(세우기 전 사용, 나간 뒤 참조, 이미 있는 대상을 다시 세움, 플레이어 두 번 세움, 목표를 지난 적을 다룸, 덮인 화면으로 끝나는 시나리오, 열린 말풍선 없이 CloseDialogue,
  대사를 말하는 대상이 그 시점에 월드에 없음),
  표 참조(Actor 번호, 대상이 캐릭터인지 — Object의 IsCharacter와 Character 표, 없는 시나리오/퀘스트 번호, Dialogue 번호와 대사 Id, 대사의 Speaker와 그 대상의 Name,
  이펙트 프리팹, Talk 대상이 Npc인지, Talk 대사를 그 Npc나 플레이어가 말하는지, 없는 Zone, `Boss`의 대상이 보스인지 — Character 표의 Boss 칸이 Boss 표에 있는지. 보스의 패턴과 커맨드는 보스전이 시작될 때 `BossFactory`가 따로 검사한다).
  어느 퀘스트도 부르지 않는 시나리오는 경고로 알린다. Zone 배치(Map, Zone 표)도 같이 검사한다.
  같은 검사(`StoryValidator`)가 이야기 시작 직전에도 돌고, 틀리면 이야기를 시작하지 않는다.

### 시나리오 툴 (Story > Story Tool)

시나리오 작업자가 표를 대본처럼 읽고, Zone을 띄워 놓은 화면에서 대상의 자리를 끌어서 정하는 에디터 창이다. UI Toolkit으로 만들었다.
**원본은 tsv다.** 툴은 tsv를 읽어 고치고, 저장하면 tsv에 다시 쓴 뒤 Update Table과 Validate Story를 이어서 돌린다.

- **창:** 왼쪽 목차 → 가운데 대본 → 오른쪽 고른 줄. 목차는 챕터마다 대상, 연출(시나리오), 퀘스트, 일반 대화(Talk) 순으로 둔다(챕터 폴더의 표와 같은 순서. 대상은 셋이 모두 가리켜서 맨 앞).
  대상은 번호에 든 챕터로 나눈다(10001 → 1장). 퀘스트를 열면 그 퀘스트가 부르는 연출의 줄과 대화의 대사까지 펼쳐 보여준다.
  검사에 걸린 줄은 대본에 붉게 표시하고 이유를 그 아래에 적는다.
- **배치 화면:** `StoryToolStage`가 프리팹 편집 화면처럼 Scene 뷰를 저장되지 않는 임시 씬으로 바꾼다. 열려 있던 씬은 건드리지 않는다.
  고른 줄까지 진행한 모습(`StoryReplay`가 표만 읽어서 계산)대로 Zone 프리팹을 깔고 대상을 세운다(`Idle` 클립의 첫 그림 + Object 표의 `Tint`).
  조명 없이 그림 그대로 본다(전역 조명이 Main 씬에 있어서 조명을 켜면 검게 나온다). 닫으면 Scene 뷰의 조명 설정을 되돌린다.
- **끌어서 배치:** 위치를 쓰는 줄(`Spawn`, `Place`, `Move`, `Reach`, Actor 표의 자리)을 고르면 핸들이 뜬다. 끌어 놓은 자리가 들어 있는 Zone을 찾아 `Zone` 칸까지 같이 바꾸고,
  가까이에 바닥이 있으면 발밑을 바닥에 붙인다. 위치를 비운 `Spawn`을 옮기면 Actor 표의 자리가 바뀐다. Undo로 되돌릴 수 있다.
- **미리보기:** 줄을 고르면 그 줄까지 진행한 모습이 나온다. 툴바의 ◀ ▶로 한 줄씩 넘기고, 재생으로 일정 간격마다 넘긴다(펼친 대사도 한 줄로 친다).
  대사 줄(`Line`, `Dialogue`)은 말하는 대상의 머리 위에 말풍선을 띄우고, `Line`의 말풍선은 게임처럼 `CloseDialogue`나 시나리오 끝까지 남는다.
  하늘색 틀은 그 순간 게임 화면에 보이는 범위다(`Zoom`, 확대 대화, Zone 끝에서 멈추는 것까지 반영. `FadeOut`으로 덮인 동안은 어둡게 칠한다).
  화각은 Main 씬의 카메라에서 읽고(`GameCameraInfo`), 범위 계산은 `CameraDirector`의 static 함수(`GetHalfView`, `ClampToZone`)를 게임과 같이 쓴다. 화면 비율은 16:9로 본다.
  표만 읽어서 계산하므로 걷는 시간, 물리, 전투는 반영하지 않는다(대상은 표에 적힌 자리에 바로 선다).
- **고치기:** 고른 줄의 칸을 오른쪽에서 고친다. 무엇을 띄울지는 명령마다 다르고 `StoryCommandCatalog`에 적힌 쓰임새를 따른다.
  번호를 적는 칸은 목록에서 이름이나 내용으로 고른다(대상, 대화, 대사, 연출). 대화와 대사 목록의 `+ 새로 만들기`는 대사 한 줄짜리 새 대화를 만들어 적는다.
  명령을 바꾸면 앞 명령의 값(Zone, Param1~2, Text)은 지우고 새 명령의 기본값을 넣는다. 위치를 쓰는 명령이면 그 대상이 서 있던 자리를 넣어서 바로 끌어 옮길 수 있게 한다.
- **줄:** 대본 위의 `+ 줄`(고른 줄 다음에 끼움), `삭제`, `▲ ▼`. 끼운 줄의 Id는 앞뒤의 사이 번호이고, 사이가 없으면 그 묶음을 10 단위로 다시 매긴다.
  옮길 때는 이웃 줄과 내용을 맞바꾸고 Id는 제자리에 둬서 파일에서도 Id 순서가 유지된다. 퀘스트의 마지막 줄을 지우면 QuestInfo의 제목도 지운다.
- **묶음과 대상:** 목차 위의 `+ 퀘스트`, `+ 연출`은 열어 둔 묶음과 같은 챕터에 다음 번호로 만든다(챕터 폴더가 없으면 만들지 못한다. 폴더와 머리 두 줄짜리 파일을 먼저 만든다).
  대상 묶음에서 `+ 줄`은 새 대상을, 일반 대화 묶음에서는 새 일반 대화를 만든다. 퀘스트 제목은 대본 위에서 고친다.
- **일반 대화(Talk):** 줄마다 말을 걸 Npc, 어느 퀘스트가 시작된 뒤부터인지, 어떤 대화인지를 고르고, 그 아래에 펼친 대사를 고친다.
  배치 화면에는 그 대화를 할 수 있게 되는 때(가리키는 퀘스트의 첫 줄을 지난 모습)가 나오고 Npc 머리 위에 대사가 뜬다.
- **대사:** 대화 아래에 펼친 대사를 고르면 말하는 대상과 글을 고친다. 대사는 대화의 끝에만 붙인다. 대사의 Id를 `Line`이 가리키므로 사이에 끼우거나 순서를 바꾸지 않는다.
- 표를 고치는 코드는 `StoryEditor` 한 곳에 있다(Undo 기록, 번호 규칙). 화면(`StoryToolWindow`, `StoryToolInspector`)은 고르면 넘기기만 하고, `StoryEditor`의 이벤트를 받아 다시 그린다.
- **새 명령을 만들면** `StoryCommandCatalog`에 한 줄 추가한다(어느 표에서 쓰는지, Actor와 Param 칸의 쓰임새, 위치 칸의 모양 `PositionKind`, 대본에 띄울 글). 칸의 쓰임새는 명령 클래스의 `Parse`와 같게 적는다. 대상을 세우거나 옮기거나 없애는 명령, 말풍선이나 카메라나 화면 덮기를 바꾸는 명령이면 `StoryReplay.Apply`에도 넣는다.
  빠뜨려도 게임에는 영향이 없고, 툴에서 그 명령을 고를 수 없거나 표의 칸이 그대로 보이거나 배치 화면에 반영되지 않는다.
- 툴은 고친 표만, 고친 칸만 다시 쓴다(줄바꿈 방식과 BOM 유지). 그래서 저장한 뒤의 diff에는 고친 줄만 남는다.

### 맵과 Zone (Map, Zone)

**Zone은 맵을 이루는 한 칸이다.** Zone은 상하좌우로 맞닿아 이어져 있고, 경계를 넘으면 포탈이나 로딩 없이 그대로 옆 Zone으로 걸어 들어간다.
맵은 Zone을 격자로 이어 붙여 만든다. **칸 크기는 맵마다 다를 수 있고**(`Map` 표), 한 맵 안에서는 모두 같다. Zone마다 지형, 배경, 발판을 담은 프리팹이 있다.

| 표 | 칸 |
|---|---|
| `Map.tsv` | `Id`, `ZoneWidth`/`ZoneHeight`(그 맵의 칸 크기), `Origin`(Column 0, Row 0 칸의 왼쪽 아래 월드 좌표) |
| `Zone.tsv` | `Id`(맵 × 1000 + 순번, 1번 맵 1번 → 1001), `Map`, `Column`, `Row` |

- Zone 안의 위치는 그 칸의 **왼쪽 아래를 0,0으로 보는 로컬 좌표**다. Zone을 다른 칸으로 옮겨도 표에 적은 위치는 고치지 않는다.
- Zone 프리팹은 `Assets/AddressableAssets/Zone/(Zone 번호).prefab`이고, 루트를 Zone의 왼쪽 아래에 두고 만든다. 그래야 프리팹 편집 화면의 좌표가 표의 로컬 좌표와 같다.
  프리팹이 없는 Zone은 빈 Zone으로 보고 경고만 남긴다.
- 이웃 Zone끼리 경계에서 지형(바닥, 벽, 발판)이 이어지게 만든다. 경계의 바닥 콜라이더 높이가 어긋나면 넘어가는 순간 걸리거나 떨어진다.
- **Zone 컬링:** 플레이어가 있는 Zone을 기준으로 상하좌우와 대각선으로 `GameDefine.ZoneCullDistance`(2) 칸 이상 떨어진 Zone은 내린다.
  그래서 자기 Zone과 둘레 8칸(3×3)만 올라와 있고, 경계를 넘으면 기준이 바뀌어 다시 정한다. 넘어간 쪽의 이웃은 이미 올라와 있어서 빈 곳이 보이지 않는다.
  컬링 거리가 칸 수로 고정이라 **Zone은 카메라 화면 절반보다 커야 한다.** 더 작으면 컬링된 Zone이 화면 가장자리에 보일 수 있어서, 그런 맵에 들어가면 경고를 한 번 남긴다.
  플레이어가 없으면 카메라가 보는 곳을 기준으로 한다. 시작 Zone을 다 불러온 뒤 이야기를 시작한다.
- **맵 끝:** 불러온 Zone의 상하좌우 중 이웃 Zone이 없는 쪽에는 보이지 않는 벽(`Map` 태그, 두께 `ZoneWallThickness`)을 세운다. 그래서 이어서 갈 Zone이 없으면 나갈 수 없다.
  벽은 Zone 프리팹이 아니라 Zone 배치에서 자동으로 만들므로, Zone을 옮기거나 새로 붙여도 손으로 고칠 것이 없다. Zone을 내리면 벽도 같이 지운다.
- **Zone 잠금(보스전):** `ZoneStreamer.Lock`으로 잠근 Zone은 옆에 Zone이 있어도 사방에 벽을 세우고, 카메라도 그 Zone 안만 보여준다 (`ZoneStreamer.GetBoundary`).
  `Unlock`하면 옆에 Zone이 있는 쪽의 벽을 걷는다. 따로 "보스 Zone"을 표에 표시하지 않고, 퀘스트의 `Boss` 명령이 보스가 서 있는 Zone을 잠근다.
  보스 전투 Zone도 카메라 화면보다 커야 한다.
- **카메라:** 플레이어를 따라가고(`CameraFollowHeight`, `CameraFollowSmoothTime`), 플레이어가 있는 Zone의 상하좌우에 Zone이 없으면 화면이 그 Zone 끝을 넘지 않는다.
  이웃이 있는 쪽으로는 막지 않는다. 그래서 **Zone은 카메라 화면보다 커야 한다**(화면이 더 크면 그 축은 Zone 가운데에 고정된다). 대각선 칸이 비어 있는 경우는 보지 않는다.
- 내려간 Zone에 있는 캐릭터는 없애지 않고 꺼둔다(이야기가 계속 추적한다). Zone이 다시 올라오면 켠다.
  시나리오의 무대 Zone은 컬링 거리 밖이어도 연출 동안 올려둔다(`ZoneStreamer.Pin`). 다만 `Move`로 걸어가는 길 사이의 Zone까지 올려두지는 않으므로,
  멀리 있는 대상은 `Place`로 옮긴 뒤에 걷게 한다.
- Main 씬에는 시스템 오브젝트만 둔다: `SceneLoader`, `GameController`, `Main Camera`, `Global Light 2D`, `Global Volume`.
  배경, 지형, 배경 조명, 그 Zone 바닥 전용 반사 카메라는 Zone 프리팹에 넣는다.
  `MainCamera` 태그와 `AudioListener`는 게임 카메라 하나에만 둔다. `Camera.main`이 태그로 카메라를 찾아서, 다른 카메라에 태그가 있으면 카메라 연출과 Zone 스트리밍이 그 카메라를 잡을 수 있다.
- 2D Renderer를 쓰는 동안은 베이크 조명이 없어서 Zone을 프리팹으로 둬도 손해가 없다. 3D Renderer와 베이크 조명으로 바꾸게 되면 `IZoneLoader` 구현을 Additive 씬으로 바꾼다.

## 검증과 테스트 (Claude Code 작업 절차)

코드를 고쳤으면 아래 순서로 확인한다. 1~2단계는 매번 하고, 3단계 이후는 변경 범위에 맞춰 한다.

### 1. 컴파일 확인 (Unity 없이)
```bash
"/c/Program Files/dotnet/dotnet" build Assembly-CSharp.csproj -v quiet -nologo
```
- 약 40초 걸린다. **`오류 0개`(error 0)**이면 통과.
- `MSB3277` 어셈블리 버전 충돌 경고 약 24개는 원래 나는 것이니 무시한다. 새로 생긴 `CS` 경고만 본다.
- `Editor/` 스크립트를 고쳤으면 `Assembly-CSharp-Editor.csproj`도 빌드한다.
- `.csproj`는 gitignore 대상이며 Unity가 생성한다. 새 파일을 추가하면 Unity에서 한 번 refresh 해야 csproj에 들어간다.
  csproj에 없는 새 파일은 dotnet build로 검증되지 않으므로 이때는 2단계로 확인한다.

### 2. Unity 에디터 확인 (UnityMCP가 연결되어 있을 때)
1. `refresh_unity`로 에셋/스크립트를 다시 불러온다.
2. `mcpforunity://editor/state`의 `data.compilation.is_compiling`이 false가 될 때까지 기다린다.
3. `read_console`로 Error/Warning을 확인한다. 컴파일 에러가 0이어야 다음 단계로 간다.

### 3. 플레이 모드 동작 확인
- `DefaultSceneSetter`가 Play 시작 씬을 `Assets/Scenes/BootStrap.unity`로 고정해두므로 어떤 씬이 열려 있든 부트스트랩부터 시작한다.
  부트스트랩 다음은 타이틀이라서, 게임에 들어가려면 타이틀에서 시작을 골라야 한다(Enter).
  `manage_editor`로 Play 하고, `read_console`로 로그를 읽은 뒤 Stop 한다.
- 전투 기능(공격, 피격, 카운터)은 로그로 확인한다: `[Player] ... hit [Enemy] ...`, `카운터 대기 시작`, `카운터 성공` 등.
- AI 공격이 필요한 테스트는 인스펙터에서 AI 오브젝트의 `_aiAttackTestTarget`을 체크하고 테스트 키(`1`)를 쓴다.
  키 입력은 사람이 해야 하므로 이런 확인은 **사용자에게 무엇을 눌러서 어떤 로그가 나와야 하는지** 정리해서 요청한다.
- 판정 범위는 Scene 뷰의 Gizmos(노란 박스)로 확인한다.

### 4. 테이블을 바꿨을 때
- `execute_menu_item`으로 `Table/Update Table`을 실행한다 (또는 사용자에게 요청).
- `git diff --stat`으로 `.tsv` / `.bytes` / `TableManifest.json` / 생성 코드가 같이 바뀌었는지 확인한다.
- 컬럼을 추가/변경했으면 생성된 `XxxRecord.cs`의 필드 타입을 확인하고 1단계 빌드를 다시 돌린다.

### 완료 보고
- 어떤 검증을 실제로 했는지, 어떤 것은 못 했는지(예: 플레이 모드 키 입력 확인) 구분해서 적는다.
- 빌드/콘솔에 남은 에러가 있으면 숨기지 않고 출력을 그대로 보여준다.

## 작업 규칙

- **대답은 항상 한국어로 한다.** 설명, 완료 보고, 질문 모두 한국어로 쓴다. 코드 식별자, 명령어, 에러 원문은 그대로 둔다.
- 커밋과 푸시는 요청받았을 때만 한다. 커밋 메시지는 한국어로 짧게, 기존 형식을 따른다 (`카운터 판정 기능 구현 완료`, `피격 판정 개선 및 FMOD 2.03 버전 추가`).
- 새 에셋/스크립트를 만들면 `.meta` 파일도 같이 커밋되어야 한다. `.meta`는 Unity가 만들게 두고 직접 만들지 않는다.
- 씬(`.unity`), 프리팹, `ProjectSettings/`는 Unity가 직렬화하는 파일이다. 텍스트로 직접 고치지 말고 UnityMCP 도구나 에디터로 수정한다.
- `Assets/Plugins/`, `Assets/Packages/`(NuGetForUnity가 관리), `Library/`, 서드파티 에셋 폴더(`Sprite Shaders Ultimate`, `Warrior free set`)는 수정하지 않는다.
  예외는 에디터 버전을 올리며 생긴 경고를 없애는 최소한의 변경뿐이다. 지금까지 한 것: FMOD 플러그인 `.meta` 46개를 새 형식으로 다시 저장
  (`ForceReserializeAssets`), `Sprite Shaders Ultimate/Scripts/Editor/CodingHelper.cs`의 `prop`에 `[System.NonSerialized]`.
  에셋을 새 버전으로 덮어쓰면 되돌아갈 수 있으니 같은 경고가 다시 뜨면 같은 방법으로 처리한다.
