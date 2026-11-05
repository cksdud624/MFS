using Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DefaultSceneSetter
{
    #region Fields
    private const GameDefine.SceneType DefaultScene = GameDefine.SceneType.BootStrap;
    #endregion

    #region Init
    static DefaultSceneSetter()
    {
        string scenePath = $"Assets/Scenes/{DefaultScene}.unity";

        var currentStartScene = EditorSceneManager.playModeStartScene;

        if (currentStartScene != null && AssetDatabase.GetAssetPath(currentStartScene) == scenePath)
            return;

        SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        if (scene != null)
        {
            EditorSceneManager.playModeStartScene = scene;
            Debug.Log($"[DefaultSceneSetter] Start scene updated to: {DefaultScene}");
        }
    }
    #endregion
}
