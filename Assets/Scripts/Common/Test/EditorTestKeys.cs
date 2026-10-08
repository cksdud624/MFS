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
    /// 프로젝트가 새 Input System 전용이라 예전 Input은 쓸 수 없으므로 InputAction 없이 키보드를 직접 읽는다
    /// </summary>
    public class EditorTestKeys : IUpdateable
    {
        #region Events
        //테스트 키가 눌렸다는 신호. 반응할 쪽이 직접 구독하고, 사라질 때 해제한다

        //AI 공격 (카운터 테스트). AI 오브젝트(ObjectBase)가 구독하고, 인스펙터에서 체크한 AI만 반응한다
        public static event Action OnAIAttack;
        #endregion

        //테스트 키 목록. 키를 늘릴 때는 위에 이벤트를 하나 만들고 여기에 한 줄 추가한다
        private static readonly (Key key, string description, Action action)[] TestKeys =
        {
            (Key.Digit1, "AI 공격 (카운터 테스트)", () => OnAIAttack?.Invoke()),
        };

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
        }
    }
}
#endif
