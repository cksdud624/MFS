# Mori 킷

어느 유니티 프로젝트(URP)에나 넣어 쓰는 이펙트 · 연출 작업 도구. Mori 게임 코드(트레이딩 · 은행 · 살롱 · 리듬 화면 · 스팀)는 들어 있지 않다.

## 폴더
| 폴더 | 내용 | 게임 빌드 |
| --- | --- | --- |
| Runtime | 재생기 · 연출 카드 · 셰이더(asmdef MoriFx.Runtime) | 들어감 |
| Core | 그림(PSD · 시트) · 시험 결투 계산 · 공용(asmdef MoriKit.Core, UNITY_EDITOR 에서만) | 안 들어감 |
| Tool | 작업실 · 그림 도구 · 연출 탭 · 빠른 조율 · 시험 결투(asmdef MoriKit.Tool, UNITY_EDITOR 에서만) | 안 들어감 |
| Editor | 메뉴(킷 장면 · 내보내기 · 패널 설정) | 안 들어감 |

## 원칙
- 킷은 `Assets/MoriKit` 안에서만 작업한다. MFS 의 스크립트 · 씬 · 테이블 · 설정 · 폴더는 고치지 않는다(읽기만).
- 킷 결과물은 MFS 가 쓰는 형식 그대로 제공한다. MFS 코드를 고쳐야만 들어가는 것은 사양서로만 넘기고, 옮기는 것은 프로그래머가 한다.
- 킷 작업 규칙(Claude 용)은 `Assets/MoriKit/CLAUDE.md` 에 있다.

## 넣는 법(MFS)
MFS 는 Git(GitHub, Git LFS) · Unity 6000.6.8f1 프로젝트다.
1. 최신 main 을 받고(Pull), 킷 전용 브랜치를 만든다(예: `feature/mori-kit`). main 에서 직접 작업하지 않는다.
2. `Assets/MoriKit` 폴더와 `Assets/MoriKit.meta` 만 MFS 의 `Assets` 아래에 복사한다. 그 밖의 파일은 복사하지 않는다.
3. 킷의 작업 데이터(그림 PSD · fx-cards · 튜닝 · 설정 등)는 모두 `Assets/MoriKit/Data~` 안에 있어서 2번에서 같이 옮겨진다.
   MFS 의 `Assets/StreamingAssets` 에는 아무것도 넣지 않는다(게임 빌드에 들어가는 메인 폴더).
4. 커밋할 때는 `Assets/MoriKit` 관련 파일만 담는다. 유니티가 저절로 바꾼 파일(폰트 SDF, `ProjectSettings` 등)은 담지 않는다.
5. 메뉴 **Mori ▸ 킷으로 바로 플레이**(Ctrl+Shift+K) — 킷을 켜고 Play. MFS 는 Play 를 늘 BootStrap 부터 시작하지만(BootStrap → Title → Main) 킷은 지워지지 않고 그 위에 뜬다.
   - **Mori ▸ 킷 켜고 플레이**(체크): 켜 두면 그냥 Play 해도 킷이 뜸. 끄면 평소처럼 게임만.
   - **Mori ▸ 킷: 게임 입력 막기**(체크, 기본 끔): 킷이 떠 있는 동안 게임의 InputAction 을 꺼 둠(UI 입력은 그대로). 게임 상태를 직접 바꾸는 방식이라 MFS 최신 빌드에서는 켜면 로딩이 끝나지 않으므로 켜지 않는다.
   - **F10**: 킷 숨기기 / 보이기. 숨기면 게임을 그대로 플레이(마우스 · 입력도 게임에게).
   - 킷이 보이는 동안은 게임이 마우스를 잠그거나 숨겨도 매 프레임 풀어 둠(숨기면 게임 상태로 되돌림).
   - **Mori ▸ 킷 장면 열기**: 게임이 장면을 바꾸지 않는 프로젝트라면 이 장면에서 Play 해도 됨.
6. 킷의 작업 데이터 · 설정은 `Assets/MoriKit/Data~` 에 저장된다(끝의 ~ 때문에 유니티가 에셋으로 가져오지 않고, 게임 빌드에도 안 들어감).
   자동 백업은 프로젝트 밖 `문서/MoriKitBackup/<프로젝트 폴더 이름>` 에 쌓인다.

## 내보내기(잠금)
- 지금 내보내기(그림 · 연출 카드 · 런타임 옮기기)는 잠겨 있다. 예전 방식은 메인 폴더(`StreamingAssets` · `MoriFxGame` · `MoriFx`)에 킷 전용 형식으로 썼는데, MFS 는 그 형식을 쓰지 않는다.
- MFS 형식: 애니메이션 = `Assets/AddressableAssets/Animation/(번호)/` 의 `.anim` 클립(`idle`, `move`, `jump`, `fly`, `attack1…`, `damage`, `death`), 이펙트 = `Assets/AddressableAssets/Effect/(번호)/` 프리팹.
- 애니메이션 종류와 전환 규칙을 프로그래머와 정한 뒤, 그 형식으로 내보내기를 새로 만든다. 잠금은 `KitExport.Locked`.

## 2단계 — MFS 화면(URP)
- 작업실 연출 탭 ‘화면 기본값’ ▸ MFS 화면: **URP 카메라 · 볼륨 · 글로벌 라이트** 켜고 끄기, 무대(캐릭터) · 킷 이펙트 정렬 레이어 고르기.
- 켜면 결투 그림(무대 판)과 이펙트를 장면의 Main Camera 와 같은 URP 설정(후처리 · HDR · 볼륨 마스크 · 렌더러 · AA)으로 그린다.
  게임의 글로벌 볼륨(Bloom · 톤매핑 · 컬러 그레이딩)과 글로벌 라이트 2D 가 게임과 같은 코드 · 같은 값으로 먹는다.
- 무대 판은 고른 정렬 레이어의 Sprite-Lit-Default — 글로벌 라이트 2D 의 대상 레이어를 고를 것.
- 프리팹 등록부는 이제 Resources 밖 어디에 둬도 찾는다(게임 빌드에 섞이지 않게 Resources 밖 권장).

## 아직(다음 단계)
- 킷 미리보기에 MFS 의 Zone 프리팹(`AddressableAssets/Zone/(번호).prefab`)을 띄워 게임 화면(원근 카메라 · 조명 · 볼륨)과 똑같이 보기. 메인은 읽기만 한다.
- 킷의 동작 흐름(레디 · 여운 · 연결 동작)을 완성하고, 애니메이션 사양을 프로그래머와 정하기.
- 메인 형식 내보내기(`.anim` 클립 · 이펙트 프리팹 · 테이블 제안 TSV).
