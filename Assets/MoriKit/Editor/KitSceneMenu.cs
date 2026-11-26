using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mori.EditorTools
{
    /// <summary>메뉴 Mori ▸ 킷 장면 열기: Assets/MoriKit/MoriKit.unity 를 열고(없으면 만들고) — 이 장면에서 Play 를 누르면 킷이 뜬다.</summary>
    public static class KitSceneMenu
    {
        const string PATH = "Assets/MoriKit/MoriKit.unity";

        const string PREF_PLAY = "MoriKit.PlayWithKit", PREF_BLOCK = "MoriKit.BlockGameInput";

        /// <summary>켜 두면 어느 장면에서 Play 하든 킷이 게임 위에 뜬다(MFS 처럼 늘 Main 으로 가는 게임에서도).</summary>
        [MenuItem("Mori/킷 켜고 플레이", false, 0)]
        static void TogglePlay() => EditorPrefs.SetBool(PREF_PLAY, !EditorPrefs.GetBool(PREF_PLAY, false));

        [MenuItem("Mori/킷 켜고 플레이", true)]
        static bool TogglePlayCheck() { Menu.SetChecked("Mori/킷 켜고 플레이", EditorPrefs.GetBool(PREF_PLAY, false)); return !EditorApplication.isPlaying; }

        /// <summary>킷이 떠 있는 동안 게임 입력(InputAction) 막기 — 기본 끔(켜면 게임의 입력 흐름과 어긋날 수 있음 · MFS 로딩 멈춤).</summary>
        [MenuItem("Mori/킷: 게임 입력 막기", false, 1)]
        static void ToggleBlock() => EditorPrefs.SetBool(PREF_BLOCK, !EditorPrefs.GetBool(PREF_BLOCK, false));

        [MenuItem("Mori/킷: 게임 입력 막기", true)]
        static bool ToggleBlockCheck() { Menu.SetChecked("Mori/킷: 게임 입력 막기", EditorPrefs.GetBool(PREF_BLOCK, false)); return true; }

        /// <summary>킷을 켜고 바로 Play.</summary>
        [MenuItem("Mori/킷으로 바로 플레이 %#k", false, 2)]
        static void PlayNow()
        {
            if (EditorApplication.isPlaying) { Debug.Log("[Mori 킷] 이미 플레이 중이에요."); return; }
            EditorPrefs.SetBool(PREF_PLAY, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Mori/킷 장면 열기", false, 20)]
        static void Open()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Mori 킷", "플레이 중에는 장면을 열 수 없어요. 플레이를 멈추고 다시 눌러 주세요.", "확인"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(PATH)) { EditorSceneManager.OpenScene(PATH); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Kit Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.043f, 0.055f, 0.067f, 1f);
            cam.orthographic = true;
            cam.transform.position = new Vector3(0, 0, -10);
            EditorSceneManager.SaveScene(scene, PATH);
            AssetDatabase.Refresh();
            Debug.Log("[Mori 킷] 킷 장면을 만들었습니다: " + PATH + " — Play 를 누르면 킷이 뜹니다.");
        }
    }
}
