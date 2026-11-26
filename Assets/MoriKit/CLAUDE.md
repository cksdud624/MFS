# Mori 킷 (Assets/MoriKit)

이 폴더는 MFS 게임과 따로 움직이는 이펙트 · 연출 · 도트 작업 도구(킷)다. 게임 코드가 아니다.
프로젝트 맨 위의 `CLAUDE.md`는 프로그래머가 쓴 **메인 게임 코드의 규칙**이고, 이 파일은 **킷 작업의 규칙**이다.
킷을 작업할 때는 이 파일이 우선한다. 메인 코드를 읽을 때는 맨 위 `CLAUDE.md`로 구조를 이해한다.

## 가장 중요한 원칙: 킷과 메인의 철저한 독립

- **킷 작업은 `Assets/MoriKit` 안에서만 한다.** 그 밖의 파일은 읽기만 한다.
- **메인(MFS)의 스크립트, 씬(`.unity`), 프리팹, 테이블(`Assets/.Table`, `Generated`), `ProjectSettings/`, `Packages/`, 폴더 구조는 고치지도, 옮기지도, 이름을 바꾸지도 않는다.**
  고치면 더 좋아 보이는 곳이 있어도 고치지 않고 사용자에게 말만 한다.
- **킷은 메인 없이도 돌아가야 한다.** 킷 어셈블리(`MoriKit.Core`, `MoriKit.Tool`, `MoriKit.Editor`, `MoriFx.Runtime`)는 메인 코드를 참조하지 않는다. 메인이 바뀌어도 킷이 깨지지 않고, 킷이 무엇을 하든 메인이 깨지지 않는다.
- **킷은 게임 상태를 직접 바꾸지 않는다.** 게임의 InputAction을 껐다 켜거나, `Time.timeScale`, 물리 설정, 카메라, 프로젝트 설정을 바꾸지 않는다.
  (예: "게임 입력 막기"를 켜 두면 MFS 최신 빌드에서 타이틀 → 게임 로딩이 끝나지 않았다. 그래서 기본값을 끔으로 두었다.)
- **킷 결과물은 메인이 지금 쓰는 형식 그대로 낸다.** 메인 코드를 고쳐야만 들어가는 것은 만들지 않고, 사양서로만 정리해서 사용자에게 준다. 메인에 옮기는 것은 프로그래머가 한다.
  프로그래머는 이미 킷의 이동·대시 수치와 결투(Duel)를 메인의 방식으로 옮겼다(맨 위 `CLAUDE.md`의 "이동, 점프, 대시", "보스전").
- 메인 테이블(`.tsv`)은 `.tsv` · `.bytes` · `TableManifest.json` · 생성 코드가 한 세트라 프로그래머의 영역이다. 킷은 "이 줄을 넣으면 된다"는 **제안 TSV**까지만 만든다.

## 킷이 지키는 메인 쪽 규칙

- 킷 카메라에 `MainCamera` 태그를 붙이지 않는다. MFS는 `Camera.main`으로 게임 카메라를 찾고, Zone 스트리밍과 카메라 연출이 그것을 쓴다.
- `Assets/StreamingAssets`, `Assets/MoriFxGame`, `Assets/MoriFx`, `Assets/_backup` 같은 메인 폴더에 파일을 만들지 않는다.
- 킷 데이터는 `Assets/MoriKit/Data~`에만 둔다. 끝의 `~` 때문에 유니티가 에셋으로 가져오지 않고 빌드에도 안 들어간다.
  **`Data~`는 사용자의 도트 원본(PSD)과 작업 데이터다. 지우거나 통째로 덮어쓰지 않는다.**
- 텍스처 임포트 규칙(`Editor/MoriTextureImport.cs`)은 킷 폴더 안의 파일에만 적용한다.
- MFS는 새 Input System 전용이다. 킷 입력은 `MORI_INPUTSYS`(asmdef의 versionDefines)로 새 Input System을 쓴다.
- MFS의 Play는 항상 `BootStrap`에서 시작한다(`DefaultSceneSetter`). 킷은 메뉴 **Mori ▸ 킷 켜고 플레이**로 어느 장면 위에서든 뜬다.

## 메인 구조 요약 (읽기 전용 참고)

- Unity 6000.6.8f1, URP 2D, Addressables, UniTask, R3, FMOD.
- 씬 흐름: `BootStrap` → `Title` → `Main`. `Main.unity`에는 시스템 오브젝트만 있고, 맵은 Zone 프리팹(`Assets/AddressableAssets/Zone/(번호).prefab`)을 격자로 잇는다.
- 카메라: 원근(Perspective). `CameraDirector`가 플레이어를 따라가고 Zone 밖은 보여 주지 않는다.
- 애니메이션: `AnimationPlayer`가 `Assets/AddressableAssets/Animation/(번호)/`의 `.anim` 클립을 상태에 들어갈 때 바로 바꿔 튼다.
  종류는 7개(`Idle`, `Move`, `Jump`, `Fly`, `Attack`(번호), `Damage`, `Death`), 클립 이름은 소문자(`1001/idle`, `1001/attack1`). 좌우는 `SpriteRenderer.flipX`.
- 이펙트: `Assets/AddressableAssets/Effect/(번호)/` 프리팹. 좌우는 Y축 180도 회전(`EffectDirection`).
- 킷의 동작 흐름(연결 동작, 레디 → 레디 엔드, 착지, 뒤돌기 등)은 지금 메인 구조로는 재생할 곳이 없다. 애니메이션 사양은 프로그래머와 정하기 전까지 내보내기 형식을 정하지 않는다.

## 내보내기

- 지금 내보내기는 잠겨 있다(`Tool/KitExport.cs`의 `KitExport.Locked`). 예전 방식이 메인 폴더에 킷 전용 형식으로 썼기 때문이다.
- 애니메이션 사양이 정해지면 메인 형식(`.anim` 클립, 이펙트 프리팹, 테이블 제안 TSV)으로 새로 만든다.

## 사용자와 일하는 방식

- 대답, 설명, 보고는 **한국어**로 한다.
- **질문("어디서 조절해?", "맞아?" 등)에는 답만 한다. 요청 없이 작업을 시작하거나 수정하지 않는다.**
- 사용자가 의견을 물으면("너의 생각은?") 제안하고, "ㅇㅇ / 진행 / ok" 같은 승인을 받은 뒤에 작업한다.
- 커밋과 푸시는 요청받았을 때만 한다. 킷은 `feature/mori-kit` 같은 킷 브랜치에서 작업하고, main에 직접 넣지 않는다.
  커밋에는 `Assets/MoriKit` 관련 파일만 담는다. 유니티가 저절로 바꾼 파일(폰트 SDF, `ProjectSettings` 등)은 담지 않는다.

## UI 규칙 (킷 화면을 고칠 때 항상)

- **대비:** 글자와 배경의 색상·명암 대비를 충분히 둔다(흰 배경에 흰 글씨 같은 문제 금지). 입력 칸은 배경 #1C2333, 테두리 #7C8AA8, 흰 글씨, 포커스 시 노란 테두리를 쓴다.
- **크기와 간격:** 글씨 잘림, 요소 겹침, 눌리지 않는 버튼이 생기지 않게 한다. 버튼은 최소 28px.
- **이미 꽉 찬 줄에 버튼을 더하지 않는다.** 예전에 꽉 찬 편집기 줄에 버튼을 더했다가 레이아웃이 넘쳐 캔버스와 타임라인이 사라진 적이 있다. 자리가 없으면 옆 패널 등 여유 있는 곳에 둔다.
- **입력 칸에서 단축키가 먼저 반응하지 않게 한다.** 글을 입력하는 동안 킷 단축키가 입력을 가로채면 안 된다(`SpriteEditor.IsTypingEl` 같은 가드를 모든 단축키 처리에 둔다).

## 검증

- 코드를 고쳤으면 컴파일 오류가 0개인지 확인한다. 유니티 콘솔 또는 프로젝트 맨 위 `CLAUDE.md`의 "1. 컴파일 확인" 방식(`dotnet build`)을 쓴다.
  킷은 별도 어셈블리라서 킷 csproj(`MoriKit.Tool.csproj` 등)도 빌드 대상이다.
- 메인 쪽 경고(xNode, FMOD 등)는 메인의 것이라 고치지 않는다. 킷 쪽 경고만 정리한다.
- 확인하지 못한 것(예: 플레이 모드에서 키 입력)은 숨기지 않고 사용자에게 무엇을 눌러 무엇을 보면 되는지 알려 준다.
