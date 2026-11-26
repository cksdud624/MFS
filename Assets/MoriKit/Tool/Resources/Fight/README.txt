※ 이제 PSD 를 바로 읽습니다 — Assets/StreamingAssets/Fight 에 넣으세요(그쪽 README 참고). 이 폴더의 png 는 PSD 가 없을 때만 쓰여요.

이 폴더에 png 를 넣으면 결투 화면의 코드 그림 대신 그 그림을 씁니다(이름 그대로, 확장자 .png).
없는 그림은 -idle 그림으로, 그것도 없으면 코드 그림으로 그려요.

플레이어  hero-idle  hero-run  hero-jump  hero-fall  hero-dash
          hero-attack1  hero-attack2  hero-attack3  (또는 하나로 hero-attack)
          hero-counter  hero-hurt  hero-down(경직 · 넉백)
결투자    foe-idle  foe-walk  foe-windup(공격 예고)  foe-attack(타격 순간)
          foe-charge(돌진)  foe-air(공중)  foe-stance(카운터 자세)  foe-stun  foe-ko
배경      bg  (하늘 · 원경 자리, 화면을 덮게 늘어남)

· 캐릭터 그림은 투명 배경, 발이 그림 맨 아래에 오게. 한 캐릭터의 그림들은 같은 캔버스 크기로.
· 기본은 오른쪽을 보는 그림. 왼쪽을 보게 그렸다면 튜닝 패널 화면 › 캐릭터 그림 › 그림이 보는 방향 = 0.
· 크기 · 발 위치: 튜닝 패널 화면 › 캐릭터 그림.
