using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mori.Client
{
    /// <summary>
    /// 빌드에 담긴 기본 파일(StreamingAssets/Bundled). 빌드할 때 Mori.Editor 의 BundleOnBuild 가 내 PC 의 사용자 데이터 폴더
    /// (persistentDataPath)에서 노트 차트 · 음악 · 전투 튜닝 · 조작 키를 여기로 모아 담는다.
    /// 읽는 순서: 사용자 폴더에 있으면 그것(직접 고친 것이 이김), 없으면 빌드에 담긴 것. 저장은 언제나 사용자 폴더로.
    /// </summary>
    public static class Bundled
    {
        public static string Dir => Path.Combine(Application.streamingAssetsPath, "Bundled");

        /// <summary>사용자 파일이 있으면 그 경로, 없고 빌드에 담긴 파일이 있으면 그 경로, 둘 다 없으면 사용자 경로(= 없음).</summary>
        public static string Pick(string userPath, string rel)
        {
            try
            {
                if (File.Exists(userPath)) return userPath;
                string b = Path.Combine(Dir, rel);
                if (File.Exists(b)) return b;
            }
            catch (Exception) { }
            return userPath;
        }

        /// <summary>폴더 안 파일 이름: 사용자 폴더 + 빌드에 담긴 폴더(이름이 같으면 하나만).</summary>
        public static List<string> Files(string userDir, string rel, Func<string, bool> keep)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var res = new List<string>();
            foreach (var dir in new[] { userDir, Path.Combine(Dir, rel) })
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.GetFiles(dir))
                    {
                        var n = Path.GetFileName(f);
                        if (n.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || !keep(n) || !seen.Add(n)) continue;
                        res.Add(n);
                    }
                }
                catch (Exception e) { Debug.LogWarning("[Mori] 폴더를 읽지 못했습니다: " + dir + " · " + e.Message); }
            }
            res.Sort(StringComparer.OrdinalIgnoreCase);
            return res;
        }
    }
}
