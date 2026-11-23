#if UNITY_EDITOR
using UnityEditor;

namespace Common.Test
{
    /// <summary>
    /// 에디터 전용 테스트 설정. 상단 메뉴 Test에서 체크로 켜고 끄고(Editor/TestMenu), 게임은 Play할 때 여기서 읽는다.
    /// 값은 EditorPrefs에 둔다. Unity를 다시 켜도 남아 있고, 이 PC에만 저장되어서 레포나 다른 사람의 설정에 섞이지 않는다.
    /// 눌러서 바로 반응하는 테스트 키(EditorTestKeys)와 달리, Play를 시작할 때의 조건을 바꾸는 설정을 여기에 모은다
    /// </summary>
    public static class EditorTestSettings
    {
        #region Fields
        private const string StartAtBossBattleKey = "CardIdle.Test.StartAtBossBattle";

        //보스전부터 시작할 때 플레이어를 세우는 자리. 보스가 서는 자리에서 이만큼 떨어뜨린다.
        //보스의 첫 공격이 바로 닿지 않고, 보스가 화면 안에 보이는 거리다
        public const float BossBattleStartDistance = 12f;

        /// <summary>
        /// 보스전부터 시작할지. 켜면 Play했을 때 타이틀을 거치지 않고 바로 게임으로 들어가고,
        /// 이야기는 보스전(Quest 표의 Boss 명령)이 있는 첫 퀘스트부터 시작한다
        /// </summary>
        public static bool StartAtBossBattle
        {
            get => EditorPrefs.GetBool(StartAtBossBattleKey, false);
            set => EditorPrefs.SetBool(StartAtBossBattleKey, value);
        }
        #endregion
    }
}
#endif
