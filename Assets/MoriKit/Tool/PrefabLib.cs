using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mori.Client
{
    /// <summary>
    /// 프리팹 등록부: 다른 프로젝트에서 가져온 3D · 파티클 프리팹을 이름으로 쓰게 한다.
    /// 만들기: Project 창의 아무 폴더(Resources 밖 권장 — 예: Assets/MoriKit/Data)에서 오른쪽 클릭 ▸ Create ▸ Mori ▸ 프리팹 등록부 → 목록에 프리팹을 끌어다 넣기(원래 폴더에서 옮기지 않아도 됨).
    /// 또는 프리팹을 Resources/MoriFx 폴더에 넣어도 자동으로 등록된다. 이름이 비면 프리팹 파일 이름.
    /// </summary>
    [CreateAssetMenu(menuName = "Mori/프리팹 등록부", fileName = "MoriPrefabs")]
    public sealed class PrefabLib : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Name;
            public GameObject Prefab;
        }

        public List<Entry> Entries = new List<Entry>();

        static Dictionary<string, GameObject> map;
        public static int Version { get; private set; }

        /// <summary>등록된 프리팹(이름 → 프리팹). 처음 부를 때 Resources 에서 모은다.</summary>
        public static IReadOnlyDictionary<string, GameObject> All
        {
            get
            {
                if (map == null) Refresh();
                return map;
            }
        }

        /// <summary>등록부 · Resources/MoriFx 를 다시 읽는다(작업실 ‘다시 읽기’).</summary>
        public static void Refresh()
        {
            map = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var libs = new List<PrefabLib>(Resources.LoadAll<PrefabLib>(""));
#if UNITY_EDITOR
                // 킷(에디터): 프로젝트 어디에 둔 등록부든 찾음 — Resources 밖에 두면 게임 빌드에 섞이지 않음
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:PrefabLib"))
                {
                    var l = UnityEditor.AssetDatabase.LoadAssetAtPath<PrefabLib>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (l != null && !libs.Contains(l)) libs.Add(l);
                }
#endif
                foreach (var lib in libs)
                    foreach (var e in lib.Entries)
                    {
                        if (e == null || e.Prefab == null) continue;
                        string n = string.IsNullOrWhiteSpace(e.Name) ? e.Prefab.name : e.Name.Trim();
                        if (!map.ContainsKey(n)) map[n] = e.Prefab;
                    }
                foreach (var go in Resources.LoadAll<GameObject>("MoriFx"))
                    if (go != null && !map.ContainsKey(go.name)) map[go.name] = go;
            }
            catch (Exception ex) { Debug.LogWarning("[Mori] 프리팹 등록부를 읽지 못했습니다: " + ex.Message); }
            Version++;
        }

        public static GameObject Get(string name) => string.IsNullOrEmpty(name) ? null : All.TryGetValue(name, out var g) ? g : null;
    }
}
