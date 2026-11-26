using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Mori.EditorTools
{
    /// <summary>
    /// 연출 재생기 내보내기: 다른 프로젝트(실제 게임)에 넣을 .unitypackage 를 만든다.
    ///  · 담는 것: Assets/MoriFx(재생기 코드 · 셰이더 · 설명서) + 연출 카드(json → Assets/MoriFx/Cards) + 카드에 쓰인 프리팹과 그 소재
    ///  · 담지 않음: 개발킷(Mori) 코드 · 스크립트(목록만 경고) · 그림 트랙의 그림(시트로 내보내 Clip 에 등록)
    /// </summary>
    public static class MoriFxExport
    {
        // 런타임이 킷 밖(Assets/MoriFx)으로 옮겨졌으면 그쪽
        static string ROOT => AssetDatabase.IsValidFolder("Assets/MoriFx") ? "Assets/MoriFx" : "Assets/MoriKit/Runtime";
        static string CARDS => ROOT + "/Cards";

        [MenuItem("Mori/연출 재생기 패키지 내보내기")]
        static void Export()
        {
            string src = Path.Combine(Application.dataPath, "MoriKit", "Data~", "Fight", "fx-cards");     // 킷 데이터 폴더(Data~)
            Directory.CreateDirectory(CARDS);
            int n = 0;
            if (Directory.Exists(src))
                foreach (var f in Directory.GetFiles(src, "*.json"))
                {
                    File.Copy(f, Path.Combine(CARDS, Path.GetFileName(f)), true);
                    n++;
                }
            AssetDatabase.Refresh();
            string path = EditorUtility.SaveFilePanel("연출 재생기 패키지 저장", "", "MoriFx.unitypackage", "unitypackage");
            if (string.IsNullOrEmpty(path)) return;
            // 카드에 쓰인 프리팹 + 그 소재(메시 · 머티리얼 · 텍스처 · 셰이더)를 같이 담음. 스크립트는 담지 않고 알려만 줌.
            var names = new HashSet<string>();
            var rx = new Regex("\"prefab\"\\s*:\\s*\"([^\"]+)\"");
            if (Directory.Exists(src))
                foreach (var f in Directory.GetFiles(src, "*.json"))
                    foreach (Match m in rx.Matches(File.ReadAllText(f))) names.Add(m.Groups[1].Value);
            var assets = new List<string> { ROOT };
            var scripts = new List<string>();
            var missing = new List<string>();
            foreach (var name in names)
            {
                var found = AssetDatabase.FindAssets("t:Prefab " + name).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(x => Path.GetFileNameWithoutExtension(x) == name).ToList();
                if (found.Count == 0) { missing.Add(name); continue; }
                foreach (var dep in AssetDatabase.GetDependencies(found[0], true))
                {
                    if (dep.EndsWith(".cs") || dep.EndsWith(".dll") || dep.EndsWith(".asmdef")) { if (!scripts.Contains(dep)) scripts.Add(dep); continue; }
                    if (!dep.StartsWith("Assets/")) continue;                     // 패키지(Packages/…) 소재는 게임 쪽에도 있음
                    if (!assets.Contains(dep)) assets.Add(dep);
                }
            }
            // Unity 6.6 부터 옛 함수로 표시됨(새 함수: AssetPackage.Package.Export). 6.5 에서도 같은 코드로 컴파일되게 옛 함수를 그대로 쓰고 경고만 끔
#pragma warning disable CS0618
            AssetDatabase.ExportPackage(assets.ToArray(), path, ExportPackageOptions.Recurse);
#pragma warning restore CS0618
            Debug.Log($"[Mori] 연출 재생기 패키지를 만들었습니다: {path} (카드 {n}장 · 프리팹 {names.Count - missing.Count}개 · 소재 {assets.Count - 1}개)");
            if (missing.Count > 0) Debug.LogWarning("[Mori] 프리팹을 찾지 못해 빠짐: " + string.Join(", ", missing));
            if (scripts.Count > 0) Debug.LogWarning("[Mori] 프리팹이 쓰는 스크립트는 담지 않았어요(게임 쪽에 있어야 함): " + string.Join(", ", scripts));
            EditorUtility.RevealInFinder(path);
        }
    }
}
