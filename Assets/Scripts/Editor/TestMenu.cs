using Common.Test;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 상단 메뉴 Test. Play를 시작할 때의 조건을 바꾸는 테스트 설정(EditorTestSettings)을 체크로 켜고 끈다.
/// 설정을 늘릴 때는 EditorTestSettings에 값을 하나 만들고, 여기에 메뉴 항목과 체크 표시를 맞추는 함수를 한 쌍 추가한다
/// </summary>
public static class TestMenu
{
    #region Fields
    private const string StartAtBossBattleMenu = "Test/Start At Boss Battle";
    #endregion

    #region Private
    [MenuItem(StartAtBossBattleMenu)]
    private static void ToggleStartAtBossBattle()
    {
        EditorTestSettings.StartAtBossBattle = !EditorTestSettings.StartAtBossBattle;
        Menu.SetChecked(StartAtBossBattleMenu, EditorTestSettings.StartAtBossBattle);
        Debug.Log($"[테스트 설정] 보스전부터 시작 : {(EditorTestSettings.StartAtBossBattle ? "켜짐" : "꺼짐")}");
    }

    //메뉴를 펼칠 때마다 불린다. 지금 설정대로 체크 표시를 맞춘다
    [MenuItem(StartAtBossBattleMenu, true)]
    private static bool ValidateStartAtBossBattle()
    {
        Menu.SetChecked(StartAtBossBattleMenu, EditorTestSettings.StartAtBossBattle);
        return true;
    }
    #endregion
}
