# Mori 킷 — 1단계(분리)

어느 유니티 프로젝트(URP)에나 넣어 쓰는 이펙트 · 연출 작업 도구. Mori 게임 코드(트레이딩 · 은행 · 살롱 · 리듬 화면 · 스팀)는 들어 있지 않다.

## 폴더
| 폴더 | 내용 | 게임 빌드 |
| --- | --- | --- |
| Runtime | 재생기 · 연출 카드 · 셰이더(asmdef MoriFx.Runtime) | 들어감 |
| Core | 그림(PSD · 시트 · 사슬) · 시험 결투 계산 · 공용(asmdef MoriKit.Core, UNITY_EDITOR 에서만) | 안 들어감 |
| Tool | 작업실 · 그림 도구 · 연출 탭 · 빠른 조율 · 시험 결투(asmdef MoriKit.Tool, UNITY_EDITOR 에서만) | 안 들어감 |
| Editor | 메뉴(킷 장면 · 내보내기 · 패널 설정) | 안 들어감 |

## 넣는 법(MFS)
1. MFS 를 Unity VCS 에 저장(백업).
2. 이 zip 의 `Assets/MoriKit` 폴더를 MFS 의 `Assets` 아래에 복사.
3. 지금까지의 작업을 옮기려면 Mori 프로젝트의 `Assets/StreamingAssets/Fight` 폴더(그림 PSD · fx-cards · fx-tweaks · fx-presets · prefabs.json · post.json · 사슬 · 팔레트)를
   MFS 의 `Assets/StreamingAssets/Fight` 로 복사.
4. 메뉴 **Mori ▸ 킷으로 바로 플레이**(Ctrl+Shift+K) — 킷을 켜고 Play. MFS 는 늘 Main 으로 가지만 킷은 지워지지 않고 그 위에 뜬다.
   - **Mori ▸ 킷 켜고 플레이**(체크): 켜 두면 그냥 Play 해도 킷이 뜸. 끄면 평소처럼 게임만.
   - **Mori ▸ 킷: 게임 입력 막기**(체크, 기본 켬): 킷이 떠 있는 동안 게임의 InputAction 을 꺼 둠(UI 입력은 그대로). 킷을 닫거나 플레이를 멈추면 되돌림.
   - **F10**: 킷 숨기기 / 보이기. 숨기면 게임을 그대로 플레이(마우스 · 입력도 게임에게).
   - 킷이 보이는 동안은 게임이 마우스를 잠그거나 숨겨도 매 프레임 풀어 둠(숨기면 게임 상태로 되돌림).
   - **Mori ▸ 킷 장면 열기**: 게임이 장면을 바꾸지 않는 프로젝트라면 이 장면에서 Play 해도 됨.
5. 킷의 설정 · 단축키 · 내보낸 시트는 `persistentDataPath/MoriKit` 에 따로 저장(게임 파일과 안 섞임).

## 2단계 — MFS 화면(URP)
- 작업실 연출 탭 ‘화면 기본값’ ▸ MFS 화면: **URP 카메라 · 볼륨 · 글로벌 라이트** 켜고 끄기, 무대(캐릭터) · 킷 이펙트 정렬 레이어 고르기.
- 켜면 결투 그림(무대 판)과 이펙트를 장면의 Main Camera 와 같은 URP 설정(후처리 · HDR · 볼륨 마스크 · 렌더러 · AA)으로 그린다.
  게임의 글로벌 볼륨(Bloom · 톤매핑 · 컬러 그레이딩)과 글로벌 라이트 2D 가 게임과 같은 코드 · 같은 값으로 먹는다.
- 무대 판은 고른 정렬 레이어의 Sprite-Lit-Default — 글로벌 라이트 2D 의 대상 레이어를 고를 것.
- 프리팹 등록부는 이제 Resources 밖 어디에 둬도 찾는다(게임 빌드에 섞이지 않게 Resources 밖 권장).

## 아직(다음 단계)
- 3단계: 화면 효과를 URP 렌더러 기능으로.
- 4 · 5단계: 켜고 끄기 스위치, 시험 결투장을 MFS 캐릭터로(Sandbox 폴더로 분리).
