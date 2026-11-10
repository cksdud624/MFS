# CardIdle

Unity 6 (6000.6.8f1) 2D 액션 프로젝트. URP 2D, 새 Input System 전용, Addressables, UniTask, R3, FMOD를 쓴다.
R3는 코어(`R3.dll`)를 NuGetForUnity로(`Assets/packages.config`, `Assets/Packages/`), Unity 확장(`com.cysharp.r3`)을 UPM git URL로 받는다. 올릴 때는 두 버전을 같이 맞춘다.
코드는 `Assets/Scripts` 아래에 있고, 공개 포트폴리오로 쓰이는 코드라서 읽히는 품질을 기능만큼 중요하게 본다.

## 폴더와 네임스페이스

네임스페이스는 폴더 경로를 그대로 따른다 (`Assets/Scripts/InGame/Component/State` → `InGame.Component.State`).

| 경로 | 역할 |
|---|---|
| `Common/` | 전역(`Global` 싱글톤), 씬 로더, `AssetManager`, `GameDefine`(상수/enum), `ChapterNumber`(표의 `챕터-순번` 번호) |
| `Common/Template/` | `Singleton`, `StateMachine`/`IState`, `IUpdateable` 계열 인터페이스 |
| `Common/Test/` | 에디터 전용 테스트 코드 (`EditorTestKeys`) |
| `InGame/Object/` | `ObjectBase`/`CharacterBase`, `ObjectSpawner` |
| `InGame/Context/` | `InGameContext`, `ObjectContext`, `InputContext`: 컴포넌트 사이를 이어주는 상태와 이벤트 |
| `InGame/Component/` | 오브젝트에 붙는 기능 컴포넌트 (물리, 애니메이션, 이펙트, 판정) |
| `InGame/Component/State/` | 오브젝트 FSM 상태 (`Ground`, `Air`, `Action`, `Damage`, `Event`) |
| `InGame/Component/Controller/` | 플레이어/AI 컨트롤러, AI 상태 |
| `InGame/Story/` | 인게임 흐름. `StoryPlayer`가 퀘스트를 번호 순으로 실행하고, `StoryValidator`가 실행 전에 표 전체를 검사해 `StoryBook`을 만든다. 월드에 세운 대상은 `StoryContext`가 들고, `TalkPlayer`가 일반 대화를, `DialoguePlayer`가 대사를 띄운다 |
| `InGame/Story/Scenario/Steps/` | 시나리오(연출) 명령 하나당 클래스 하나. `ScenarioStepFactory`에 이름을 등록한다 |
| `InGame/Story/Quest/Steps/` | 퀘스트(목표, 시나리오 재생) 명령 하나당 클래스 하나. `QuestStepFactory`에 이름을 등록한다. 새 명령은 시나리오/퀘스트 모두 `StoryStep`을 상속해서 `Parse`(칸 형식), `Validate`(앞뒤 정합성), `Run`을 구현하고, 시작/종료/목표 명령이면 `Kind`를 다시 정한다. Actor와 Param1~2를 어떻게 쓰는지는 각 클래스 summary에 적는다 |
| `InGame/UI/` | UI 뷰 (`DialogueView`, `ScreenFadeView`). 프리팹은 `Editor/UIPrefabBuilder`의 메뉴(UI > Build All UI)가 코드로 조립해서 `AddressableAssets/UI/`에 저장한다. 레이아웃은 프리팹이 아니라 빌더 코드를 고친다 |
| `InGame/CameraDirector.cs` | 연출용 카메라 조작 (확대/축소, 바라볼 곳). 씬 카메라의 처음 상태를 배율 1로 본다 |
| `Generated/Table/` | **자동 생성 코드. 직접 고치지 않는다** (`Custom/` 폴더의 partial만 손으로 작성) |
| `Editor/` | 에디터 툴 (`TableUpdater`, `StoryValidatorMenu`, `DefaultSceneSetter`) |

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
- 테스트 단축키는 `Common/Test/EditorTestKeys.cs` 한 곳에 모은다. 키를 추가할 때는 static 이벤트를 하나 만들고 `TestKeys` 배열에 한 줄 추가한다.
  반응하는 쪽은 그 이벤트를 구독/해제한다.
- 판정 범위 같은 시각 디버그는 `OnDrawGizmos`를 `#if UNITY_EDITOR`로 감싸서 쓴다.
- 입력은 새 Input System만 쓴다. `UnityEngine.Input`(구 입력)은 쓰지 않는다.

## 테이블 (데이터)

- 원본은 `Assets/.Table/*.tsv`. 첫 줄은 컬럼 이름, 둘째 줄은 타입 (`long`, `int`, `float`, `bool`, `string`, `Vector2`, `List<long>` ...).
- 수정 후 Unity 메뉴 **Table > Update Table**을 실행하면 다음이 갱신된다:
  - `Assets/Generated/Table/*.bytes` (데이터)
  - `Assets/Scripts/Generated/Table/*Record.cs`, `TableManager.cs` (코드)
  - `Assets/.Table/TableManifest.json` (해시)
- 생성된 Record에 조회 기능이 필요하면 `Generated/Table/Custom/XxxRecord.Custom.cs`에 partial로 추가하고 `partial void InitCustomRecord()`에서 색인을 만든다.
- 번호 컬럼의 `0`은 "사용 안 함"을 뜻한다.
- 그 줄에서 쓰지 않는 칸은 빈칸으로 둔다. 빈칸은 숫자면 `0`, `Vector2`/`Vector3`면 `zero`, 문자열이면 빈 문자열로 읽힌다 (에러 없음).
  `0,0` 같은 채움값을 적지 않는다. 실제 값과 구분이 안 된다. 형식이 틀린 값(`1,2,3`을 `Vector2`에)은 지금처럼 에러를 낸다.
- 테이블 변경은 `.tsv`, `.bytes`, `TableManifest.json`, 생성 코드가 한 세트로 같이 커밋되어야 한다.
### 이야기 표 (Actor, Scenario, Quest, Talk)

인게임 흐름은 네 표가 나눠 맡는다. 번호는 표마다 따로 `챕터-순번`(1-1, 1-2, 2-1)으로 매기고, 문자열 칸에 그대로 적는다. 정렬은 숫자로 한다(1-10은 1-2 뒤).

| 표 | 무엇 | 명령 / 칸 |
|---|---|---|
| `Actor.tsv` | 월드의 캐릭터 등록. 시나리오, 퀘스트, 대화는 대상을 이 번호로 가리킨다 | `Id`(1-2), `ObjectId`, `Type`(Npc/Enemy), `Resident`, `Position` |
| `Scenario.tsv` | 연출. 플레이어가 조작하지 않는 장면 | `Spawn`, `Despawn`, `Move`, `Effect`, `Dialogue`, `Wait`, `FadeOut`/`FadeIn`, `Zoom` |
| `Quest.tsv` | 진행. 퀘스트 번호 순서가 곧 진행 순서이고, 이야기는 첫 퀘스트부터 시작한다 | `Scenario`(시나리오 재생), 목표 `Interact`, `Reach`, `Clear` |
| `Talk.tsv` | 일반 대화. 퀘스트 목표가 아닐 때 Npc에게 말을 걸면 나오는 대화 | `Actor`, `Quest`(이 퀘스트가 시작된 뒤부터), `Dialogue` |

- 대상은 `Actor` 한 칸에 `Player` 또는 Actor 번호(`1-2`)로 적는다. 같은 캐릭터를 여럿 세워도 번호가 달라서 위치나 순서로 추측하지 않는다.
  플레이어는 하나뿐이라 Actor 표에 두지 않고, 캐릭터는 `GameDefine.PlayerObjectId`를 쓴다.
- 대상은 시나리오나 퀘스트가 아니라 **월드가 갖는다**. `Spawn`으로 들어와서 `Despawn`이나 `Clear`로 나가기 전까지 뒤의 퀘스트와 시나리오가 같은 번호로 쓴다.
  `Resident`를 켠 대상은 이야기가 시작될 때 `Position`에 서 있다. `Spawn`의 위치를 비우면 `Position`에 선다(플레이어는 위치를 꼭 적는다).
- 시나리오 하나가 통째로 연출 구간이다. 재생하는 동안 월드의 조작과 AI가 멈추고(도중에 등장한 대상 포함), 끝나면 카메라를 처음 상태로 되돌린 뒤 다시 켠다.
  시나리오가 끝나면 목표가 이어지므로 화면을 덮은 채(`FadeOut`) 끝날 수 없다.
- 퀘스트 목표를 지나는 동안 월드에 있던 적은 플레이어와 싸워 쓰러졌을 수 있다. 그래서 목표를 지난 적은 시나리오가 다룰 수 없고 `Clear`로만 가리킨다.
  `Clear`는 대상을 적으면 그 적, 비우면 월드의 적 전부가 쓰러질 때까지 기다리고, 통과한 적은 월드에서 나간 것으로 본다.
- 일반 대화는 시나리오 재생 중이나 다른 대화 중에는 나오지 않고, `Interact` 목표가 기다리는 Npc에게 건 말은 목표로 간다.
- `Dialogue`의 Param2에 배율을 적으면 대화 상대(없으면 플레이어)를 확대했다가 끝나면 대화 직전 카메라로 되돌린다.
- 시나리오와 퀘스트 줄의 Id는 번호에서 만든다: `챕터 × 1000000 + 순번 × 1000`부터 10 단위 (1-2 → 1002010, 1002020, ...).
  줄을 끼울 때는 사이 번호(1002015)를 쓰고 뒤를 밀지 않는다. 줄은 표에 적힌 순서가 아니라 Id 순으로 실행한다.
- `Note` 칸은 설계자 메모다. 장면 구분이나 목표를 적는다. 게임은 읽지 않는다.
- 이야기 표를 고쳤으면 Update Table 뒤에 **Table > Validate Story**를 실행한다. Play 없이 퀘스트를 번호 순으로, 퀘스트가 부르는 시나리오까지 펼쳐서 따라가며 검사한다:
  칸 형식(타입, 쓰지 않는 칸에 값, 번호 형식, Id 범위와 중복, 표에 없는 명령),
  앞뒤 관계(세우기 전 사용, 나간 뒤 참조, 이미 있는 대상을 다시 세움, 플레이어 두 번 세움, 목표를 지난 적을 다룸, 덮인 화면으로 끝나는 시나리오),
  표 참조(Actor 번호, 대상이 캐릭터인지 — Object의 IsCharacter와 Character 표, 없는 시나리오/퀘스트 번호, Dialogue, 이펙트 프리팹, Talk 대상이 Npc인지).
  어느 퀘스트도 부르지 않는 시나리오는 경고로 알린다.
  같은 검사(`StoryValidator`)가 이야기 시작 직전에도 돌고, 틀리면 이야기를 시작하지 않는다.

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
