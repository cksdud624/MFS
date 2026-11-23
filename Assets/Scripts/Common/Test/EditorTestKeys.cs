#if UNITY_EDITOR
using System;
using Common.Template.Interface;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Common.Test
{
    /// <summary>
    /// 에디터 전용 테스트 키. 테스트용 단축키는 다른 곳에 흩어두지 않고 전부 여기에 모은다.
    /// Global이 만들어서 Update에 묶어주므로 씬에 따로 배치하지 않고, 빌드에는 들어가지 않는다.
    /// 프로젝트가 새 Input System 전용이라 예전 Input은 쓸 수 없으므로 InputAction 없이 키보드를 직접 읽는다.
    /// 테스트 키는 게임 조작 키(PlayerInputAction)와 겹치지 않게 고른다. 겹치면 테스트 키를 누를 때 게임 동작도 같이 나간다
    /// </summary>
    public class EditorTestKeys : IUpdateable
    {
        #region Fields
        //테스트 키가 눌렸다는 신호. 반응할 쪽이 직접 구독하고, 사라질 때 해제한다

        //AI 공격 (카운터 테스트). AI 오브젝트(ObjectBase)가 구독하고, 인스펙터에서 체크한 AI만 반응한다
        public static event Action OnAIAttack;
        //플레이어 이동 속도 빠르게 켜기/끄기 (넓은 맵을 빨리 오가며 Zone 컬링을 볼 때). 플레이어 컨트롤러가 구독한다
        public static event Action OnToggleFastMove;
        //퀵이동 키를 누르거나(true) 뗐다(false). 누르고 있는 동안만 아주 빠르게 간다. 플레이어 컨트롤러가 구독한다
        public static event Action<bool> OnQuickMoveChanged;
        //경직 면역 켜기/끄기. 싸움에 끼는 캐릭터(CharacterBase)가 모두 구독해서 플레이어와 적이 같이 바뀐다
        public static event Action OnToggleStaggerImmune;
        //공격력 증가 걸기. 캐릭터가 구독하고 플레이어만 반응한다. 누를 때마다 겹친다
        public static event Action OnAddAttackBuff;
        //보스의 지금 체력바 비우기 (페이즈 전환과 쓰러짐 확인). 보스 컨트롤러가 구독하고 전투 중인 보스만 반응한다
        public static event Action OnBossPhaseSkip;

        //빠르게 켰을 때의 이동 속도 배율
        public const float FastMoveMultiplier = 10f;
        //퀵이동 키를 누르고 있는 동안의 이동 속도 배율
        public const float QuickMoveMultiplier = 100f;
        //테스트 키가 거는 상태 효과 (StatusEffect 표의 번호)
        public const long StaggerImmuneEffectId = 1;
        public const long AttackBuffEffectId = 2;

        //누를 때 한 번 반응하는 테스트 키 목록. 키를 늘릴 때는 위에 이벤트를 하나 만들고 여기에 한 줄 추가한다
        private static readonly (Key key, string description, Action action)[] TestKeys =
        {
            (Key.Digit1, "AI 공격 (카운터 테스트)", () => OnAIAttack?.Invoke()),
            (Key.Digit2, "플레이어 이동 속도 빠르게 켜기/끄기", () => OnToggleFastMove?.Invoke()),
            (Key.Digit3, "경직 면역 켜기/끄기 (플레이어와 적 모두)", () => OnToggleStaggerImmune?.Invoke()),
            (Key.Digit4, "플레이어 공격력 증가 걸기 (10초, 3번까지 겹침)", () => OnAddAttackBuff?.Invoke()),
            (Key.Digit5, "보스 체력바 하나 비우기 (전투 중인 보스)", () => OnBossPhaseSkip?.Invoke()),
        };

        //누르고 있는 동안만 켜지는 테스트 키 목록. 누를 때 true, 뗄 때 false를 보낸다
        private static readonly (Key key, string description, Action<bool> action)[] HoldKeys =
        {
            //스페이스와 Shift는 둘 다 대시가 쓴다. 이동 키(WASD) 옆이라 누른 채로 움직이기 쉬운 Q에 둔다
            (Key.Q, "플레이어 퀵이동", held => OnQuickMoveChanged?.Invoke(held)),
        };
        #endregion

        #region LifeCycle
        public void OnUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            foreach (var testKey in TestKeys)
            {
                if (!keyboard[testKey.key].wasPressedThisFrame) continue;

                Debug.Log($"[테스트 키] {testKey.key} : {testKey.description}");
                testKey.action();
            }

            foreach (var holdKey in HoldKeys)
            {
                var control = keyboard[holdKey.key];
                if (control.wasPressedThisFrame)
                {
                    Debug.Log($"[테스트 키] {holdKey.key} : {holdKey.description} 켜짐");
                    holdKey.action(true);
                }
                else if (control.wasReleasedThisFrame)
                {
                    Debug.Log($"[테스트 키] {holdKey.key} : {holdKey.description} 꺼짐");
                    holdKey.action(false);
                }
            }
        }
        #endregion
    }
}
#endif
