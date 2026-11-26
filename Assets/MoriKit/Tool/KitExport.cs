using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mori.Client
{
    /// <summary>
    /// MFS 로 내보내기(킷을 빼기 전에): 게임이 쓸 것만 킷 밖(게임 프로젝트 폴더)으로.
    ///  · 그림 → &lt;게임 폴더&gt;/Sprites/&lt;이름&gt;/&lt;이름&gt;_00.png … (스프라이트 · PPU = 킷의 1U px · 도트 그대로 · 발밑 가운데 기준) + &lt;이름&gt;.json(컷 길이)
    ///  · 연출 카드 → Assets/StreamingAssets/fx-cards(MoriFxPlayer 가 기본으로 읽는 곳)
    ///  · 런타임(MoriFx 재생기 코드 · 셰이더) → Assets/MoriKit/Runtime 을 Assets/MoriFx 로 옮김(GUID 그대로 — 프리팹 · 씬 연결이 안 끊김)
    /// 게임 쪽 파일은 덮어쓴다(같은 파일을 고쳐야 Animator 등의 연결이 유지됨 — 게임 프로젝트의 버전 관리로 지킴).
    /// </summary>
    public static class KitExport
    {
        const string PREF_DIR = "MoriKit.MfsDir";
        /// <summary>내보내기 잠금. 지금 내보내기는 메인 폴더(StreamingAssets · MoriFxGame · MoriFx)에 킷 전용 형식으로 파일을 쓴다.
        /// MFS 는 그 형식을 쓰지 않으므로(애니메이션 = Animation/(번호)/ .anim 클립, 이펙트 = Effect/(번호)/ 프리팹),
        /// 메인 형식이 프로그래머와 정해질 때까지 메인 폴더에 아무것도 쓰지 않게 막아 둔다.
        /// const 로 두면 아래 코드가 ‘닿지 않는 코드’ 경고(CS0162)를 내므로 readonly 로 둔다.</summary>
        public static readonly bool Locked = true;
        public const string LOCKED_MSG = "내보내기는 잠겨 있어요 — 메인(MFS) 형식(.anim 클립 · 이펙트 프리팹)으로 바꾸는 중이에요. 메인 폴더에는 아무것도 쓰지 않았어요.";

        public const string RUNTIME_IN = "Assets/MoriKit/Runtime", RUNTIME_OUT = "Assets/MoriFx", CARDS_OUT = "Assets/StreamingAssets/fx-cards";

        /// <summary>게임 쪽 폴더(Assets 아래, 기본 Assets/MoriFxGame).</summary>
        public static string GameDir
        {
            get { var d = EditorPrefs.GetString(PREF_DIR, ""); return string.IsNullOrEmpty(d) ? "Assets/MoriFxGame" : d; }
            set => EditorPrefs.SetString(PREF_DIR, value ?? "");
        }

        static string Full(string assetPath) => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", assetPath);

        /// <summary>폴더 고르기(프로젝트 Assets 안만).</summary>
        public static bool PickGameDir(out string msg)
        {
            if (Locked) { msg = LOCKED_MSG; return false; }
            string abs = EditorUtility.OpenFolderPanel("게임 쪽 폴더 고르기(Assets 안)", Full(GameDir), "");
            msg = null;
            if (string.IsNullOrEmpty(abs)) return false;
            string proj = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath) ?? "").Replace('\\', '/').TrimEnd('/') + "/";
            string a = Path.GetFullPath(abs).Replace('\\', '/');
            if (!a.StartsWith(proj + "Assets", StringComparison.OrdinalIgnoreCase)) { msg = "프로젝트의 Assets 폴더 안에서 골라 주세요."; return false; }
            string rel = a.Substring(proj.Length).TrimEnd('/');
            if (rel.StartsWith("Assets/MoriKit", StringComparison.OrdinalIgnoreCase)) { msg = "킷 폴더(Assets/MoriKit) 밖에서 골라 주세요 — 킷을 빼면 같이 지워져요."; return false; }
            GameDir = rel;
            msg = "게임 쪽 폴더: " + rel;
            return true;
        }

        // ───────── 그림 → 스프라이트 PNG ─────────
        public static string Sprites()
        {
            if (Locked) return LOCKED_MSG;
            var anims = ArtLib.AllAnims.Where(a => a != null && !string.IsNullOrEmpty(a.Key) && a.Frames != null && a.Frames.Length > 0)
                .GroupBy(a => a.Key).Select(g => g.Last()).ToList();
            if (anims.Count == 0) return "내보낼 그림이 없어요(그림 폴더가 비었거나 아직 읽는 중).";
            string root = GameDir + "/Sprites";
            var written = new List<string>();
            var footOf = new Dictionary<string, float>();          // 파일 → 발밑 선(캔버스 높이 대비)
            var centerOf = new HashSet<string>();                  // 파일 → 가운데 기준(이펙트용 그림 — ‘가운데’)
            foreach (var a in anims)
            {
                string safe = Safe(a.Key);
                string dirA = root + "/" + safe;
                Directory.CreateDirectory(Full(dirA));
                for (int i = 0; i < a.Frames.Length; i++)
                {
                    var png = Png(a.Frames[i]);
                    if (png == null) continue;
                    string p = $"{dirA}/{safe}_{i:00}.png";
                    File.WriteAllBytes(Full(p), png);
                    written.Add(p);
                    footOf[p] = a.H > 0 ? a.Foot / (float)a.H : 0;
                    if (a.Center) centerOf.Add(p);
                }
                // 컷 길이(ms) · 한 번만 재생 여부 — 게임에서 AnimationClip 을 만들 때 참고
                var ms = string.Join(", ", (a.Ms ?? new float[0]).Select(x => x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
                File.WriteAllText(Full($"{dirA}/{safe}.json"),
                    $"{{\n  \"key\": \"{a.Key}\",\n  \"frames\": {a.Frames.Length},\n  \"ms\": [{ms}],\n  \"once\": {(a.Once ? "true" : "false")},\n  \"ppu\": {PostSettings.Ppu},\n  \"pivot\": \"{(a.Center ? "center" : "foot-center")}\",\n  \"foot\": {a.Foot}\n}}\n");
            }
            AssetDatabase.Refresh();
            float ppu = PostSettings.Ppu;
            foreach (var p in written)
            {
                if (!(AssetImporter.GetAtPath(p) is TextureImporter ti)) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = ppu;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                float fv = footOf.TryGetValue(p, out var fq) ? fq : 0;
                if (centerOf.Contains(p)) st.spriteAlignment = (int)SpriteAlignment.Center;      // 이펙트용 = 캔버스 가운데
                else if (fv > 0) { st.spriteAlignment = (int)SpriteAlignment.Custom; st.spritePivot = new Vector2(0.5f, fv); }   // 발밑 선 가운데(바닥 밑으로 뚫는 그림)
                else st.spriteAlignment = (int)SpriteAlignment.BottomCenter;     // 발밑 가운데 = 킷과 같은 기준
                st.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(st);
                ti.SaveAndReimport();
            }
            return $"그림 {anims.Count}개 · 컷 {written.Count}장 → {root} (PPU {ppu:0} · 도트 그대로 · 발밑 선 가운데)";
        }

        static string Safe(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        /// <summary>GPU 에만 있는 텍스처도 PNG 로(렌더 텍스처로 옮겨 읽음).</summary>
        static byte[] Png(Texture2D t)
        {
            if (t == null) return null;
            if (t.isReadable) return t.EncodeToPNG();
            var rt = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            var tmp = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(t, rt);
                RenderTexture.active = rt;
                tmp.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0, false);
                tmp.Apply(false, false);
                return tmp.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(tmp);
            }
        }

        // ───────── 연출 카드 ─────────
        public static string Cards()
        {
            if (Locked) return LOCKED_MSG;
            string src = FxCardStore.Dir;
            if (!Directory.Exists(src)) return "연출 카드가 없어요.";
            Directory.CreateDirectory(Full(CARDS_OUT));
            int n = 0;
            foreach (var f in Directory.GetFiles(src, "*.json"))
            {
                File.Copy(f, Full(CARDS_OUT + "/" + Path.GetFileName(f)), true);
                n++;
            }
            AssetDatabase.Refresh();
            return $"연출 카드 {n}장 → {CARDS_OUT} (게임의 MoriFxPlayer 가 ‘fx-cards’ 로 읽음)";
        }

        // ───────── 런타임을 킷 밖으로 ─────────
        public static bool RuntimeInside => AssetDatabase.IsValidFolder(RUNTIME_IN);

        public static string MoveRuntime()
        {
            if (Locked) return LOCKED_MSG;
            if (!RuntimeInside) return AssetDatabase.IsValidFolder(RUNTIME_OUT) ? $"런타임은 이미 {RUNTIME_OUT} 에 있어요." : "런타임 폴더를 찾지 못했어요.";
            if (AssetDatabase.IsValidFolder(RUNTIME_OUT)) return $"{RUNTIME_OUT} 가 이미 있어요 — 비우거나 이름을 바꾼 뒤 다시 해 주세요.";
            string err = AssetDatabase.MoveAsset(RUNTIME_IN, RUNTIME_OUT);
            if (!string.IsNullOrEmpty(err)) return "옮기지 못했어요: " + err;
            AssetDatabase.Refresh();
            return $"런타임을 {RUNTIME_OUT} 로 옮겼어요(GUID 그대로). 이제 Assets/MoriKit 을 지워도 게임은 연출 카드를 재생할 수 있어요.";
        }

        public static string All()
        {
            if (Locked) return LOCKED_MSG;
            var a = Sprites();
            var b = Cards();
            return a + "\n" + b;
        }
    }
}
