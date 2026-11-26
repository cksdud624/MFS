# MoriFx — 연출 카드 재생기

Mori 개발킷의 작업실 ‘연출’ 칸에서 만든 연출 카드(json)를 실제 게임에서 그대로 재생하는 작은 패키지입니다.
개발킷 코드(Mori)에 기대지 않으며 이 폴더만 옮기면 됩니다.

## 넣는 법
1. 개발킷에서 메뉴 **Mori ▸ 연출 재생기 패키지 내보내기** → `MoriFx.unitypackage` 저장
2. 게임 프로젝트에서 Assets ▸ Import Package ▸ Custom Package… 로 가져오기
3. 장면에 빈 오브젝트를 만들고 **MoriFxPlayer** 컴포넌트를 붙인다
   - `cam` : 게임 카메라(직교). 흔들림 · 줌이 여기에 적용됨
   - `prefabs` : 카드의 ‘이펙트’ 트랙이 쓰는 프리팹(이름은 개발킷 등록부 이름과 같게)
   - `clips` : 카드의 ‘그림’ 트랙이 쓰는 스프라이트 컷들(이름 = 개발킷 애니메이션 이름)
   - `cards` : `Assets/MoriFx/Cards` 의 json 을 끌어다 넣기(또는 StreamingAssets/fx-cards 폴더에 두기)

## 코드
```csharp
// 공격이 맞은 순간
MoriFxPlayer.Main.Play("hero.slash1", heroPos, enemyPos, facing);

// 따라가기(follow) 트랙을 쓰려면 번호 → Transform 등록
MoriFxPlayer.Main.Follow[1] = hero.transform;
MoriFxPlayer.Main.Follow[2] = enemy.transform;
MoriFxPlayer.Main.Play("hero.dash", new FxCtx { self = heroPos, target = enemyPos, hit = hitPos, facing = 1, owner = 1, targetOwner = 2 });

// 히트스톱: 값만 알려 줌 → 게임이 원하는 곳에 적용
float k = MoriFxPlayer.Main.Screen.timeScale;   // 1 = 보통, 0 = 멈춤
```
`useTimeScale` 를 켜면 Time.timeScale 을 직접 바꿉니다(간단한 게임용).

```csharp
// 방향(대쉬 8방향 · 위아래): dir 을 채우면 ‘움직이는 방향’ 트랙이 그쪽으로 돌아감
MoriFxPlayer.Main.Play("hero.dash", new FxCtx { self = heroPos, target = enemyPos, facing = 1, dir = dashVelocity });

// 히트스톱 ‘맞는 쪽만’ · ‘쓰는 쪽만’: 캐릭터 번호(owner)별 시간 배율
float enemyK = MoriFxPlayer.Main.Screen.ScaleOf(2);  // 그 캐릭터 애니메이터 speed 에 곱하기
// 줌 중심: Screen.zoomAnchored 이면 Screen.zoomAt(게임 단위) 이 화면에서 제자리에 있게 카메라를 옮기기
```

## 단위
- 위치 1U = 유니티 1 단위. 시간은 프레임(60 = 1초).
- 흔들림은 1080p 기준 px → 카메라 높이에 맞춰 바뀜.
- 섬광은 화면 전체를 OnGUI 로 덮음(게임에 자체 화면 효과가 있으면 `Screen.flash` 를 읽어 직접 그리기).

## 직접 재생기를 만들 때
`IFxHost` 를 구현하고 `new FxDirector(host)` 로 감독을 만든 뒤 매 프레임 `Tick(dt)` · `Screen` 을 읽으면 됩니다.

## 화면 효과 · 잔상 · 몸 효과
- `Screen.glow / chroma / blur(At) / wave* / sat / invert / tint / tilt` 은 값만 계산해 줍니다. 게임의 후처리(URP Volume 등)에 연결하세요.
- 몸 효과는 `Follow[번호]` 캐릭터의 SpriteRenderer 색을 바꿨다가 끝나면 되돌립니다(게임이 같은 프레임에 색을 바꾸면 덮어씀).
- 잔상은 `Follow[번호]` 의 SpriteRenderer 를 몇 프레임마다 복사해 남깁니다.
- 늘이기(to)를 쓰는 스프라이트는 피벗을 왼쪽 가운데로 두세요(폭 = 길이).

## 셰이더 이펙트(MoriFx/Erode)
- 파티클 · 메시 · 스프라이트 머티리얼의 셰이더를 **MoriFx/Erode** 로 두면 연출 카드의 ‘셰이더 값 곡선’ 으로
  침식(_Erode) · 발광(_Glow, HDR) · 가장자리 빛(_EdgeGlow) · 일렁임(_Wobble) · 흐름(_Scroll)을 시간에 따라 바꿀 수 있습니다.
- 노이즈는 FxNoise 가 코드로 만들어 전역(_MoriNoise)으로 넣습니다.
- 빛(주변 밝히기) · 일렁임은 `Screen.LightAt(i) / LightCol(i) / lightN` · `Screen.haze*` 값으로 — 게임의 2D 라이트나 후처리에 연결하세요.
