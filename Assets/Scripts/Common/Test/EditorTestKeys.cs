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

        //빠르게 켰을 때의 이동 속도 배율
        public const float FastMoveMultiplier = 10f;
        //퀵이동 키를 누르고 있는 동안의 이동 속도 배율
        public const float QuickMoveMultiplier = 100f;

        //누를 때 한 번 반응하는 테스트 키 목록. 키를 늘릴 때는 위에 이벤트를 하나 만들고 여기에 한 줄 추가한다
        private static readonly (Key key, string description, Action action)[] TestKeys =
        {
            (Key.Digit1, "AI 공격 (카운터 테스트)", () => OnAIAttack?.Invoke()),
            (Key.Digit2, "플레이어 이동 속도 빠르게 켜기/끄기", () => OnToggleFastMove?.Invoke()),
        };

        //누르고 있는 동안만 켜지는 테스트 키 목록. 누를 때 true, 뗄 때 false를 보낸다
        private static readonly (Key key, string description, Action<bool> action)[] HoldKeys =
        {
            (Key.Space, "플레이어 퀵이동", held => OnQuickMoveChanged?.Invoke(held)),
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
