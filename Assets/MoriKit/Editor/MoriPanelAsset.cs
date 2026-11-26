using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.EditorTools
{
    /// <summary>
    /// 빌드에서 글자가 안 그려지는(파란 화면) 문제 막기.
    /// Unity 6 의 UI Toolkit 글자 처리(Advanced Text)는 PanelSettings 에 ICU 데이터가 붙어 있어야 하는데,
    /// 코드로 만든 PanelSettings 에는 붙지 않는다(에디터에서는 자동으로 채워져 문제가 안 보임).
    /// 그래서 Resources/MoriPanel.asset 을 에셋으로 만들어 두고(에디터가 ICU 데이터를 붙임), 게임은 그것을 복제해 쓴다.
    /// 프로젝트를 열 때 · 빌드 직전에 자동으로 확인한다. 메뉴 Mori ▸ 빌드용 패널 설정 확인 으로도 실행.
    /// </summary>
    [InitializeOnLoad]
    public static class MoriPanelAsset
    {
        public const string PATH = "Assets/MoriKit/Tool/Resources/MoriPanel.asset";

        static MoriPanelAsset() { EditorApplication.delayCall += () => Ensure(false); }

        [MenuItem("Mori/빌드용 패널 설정 확인")]
        static void Menu() => Ensure(true);

        public static void Ensure(bool verbose)
        {
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(PATH);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, PATH);
                Debug.Log("[Mori] 빌드용 패널 설정을 만들었습니다: " + PATH);
            }
            // ICU 데이터(글자 처리용)가 비어 있으면 프로젝트 · 패키지에서 찾아 붙인다
            var so = new SerializedObject(ps);
            var icu = so.FindProperty("m_ICUDataAsset") ?? so.FindProperty("icuDataAsset");
            if (icu != null && icu.objectReferenceValue == null)
            {
                var guid = AssetDatabase.FindAssets("icudt t:TextAsset").FirstOrDefault()
                        ?? AssetDatabase.FindAssets("icudt").FirstOrDefault();
                if (guid != null)
                {
                    icu.objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            EditorUtility.SetDirty(ps);
            AssetDatabase.SaveAssets();
            bool ok = icu == null || icu.objectReferenceValue != null;
            if (verbose || !ok)
                Debug.Log(ok ? "[Mori] 빌드용 패널 설정 OK(ICU 데이터 연결됨)." :
                    "[Mori] 빌드용 패널 설정에 ICU 데이터가 비어 있습니다 — Project 창에서 " + PATH + " 를 한 번 클릭해 Inspector 를 열어 주세요(에디터가 채웁니다).");
        }
    }

    /// <summary>빌드 직전에 한 번 더 확인.</summary>
    public sealed class MoriPanelAssetBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => MoriPanelAsset.Ensure(true);
    }
}
