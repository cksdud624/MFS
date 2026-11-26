using UnityEditor;
using UnityEngine;

namespace Mori.EditorTools
{
    /// <summary>
    /// UI 그림(Assets/MoriKit/Tool/Resources 아래 png) 가져오기 규칙.
    /// Unity 기본값은 2의 거듭제곱이 아닌 그림을 가까운 크기로 늘이거나 줄여서(240×320 → 256×256)
    /// 인물·배경이 찌그러지고 잘려 보인다. 여기서 원래 크기 그대로, 밉맵 없이, 투명 테두리가 번지지 않게 가져온다.
    /// 처음 한 번은 메뉴 Mori ▸ UI 그림 다시 가져오기 를 누르거나, 프로젝트를 열 때 자동으로 고친다.
    /// </summary>
    public sealed class MoriTextureImport : AssetPostprocessor
    {
        const string ROOT = "Assets/MoriKit/Tool/Resources";

        static bool Mine(string path) => path.Replace('\\', '/').StartsWith(ROOT) && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessTexture()
        {
            if (!Mine(assetPath)) return;
            Apply((TextureImporter)assetImporter);
        }

        static bool Apply(TextureImporter ti)
        {
            bool changed = ti.textureType != TextureImporterType.Default || ti.npotScale != TextureImporterNPOTScale.None
                || ti.mipmapEnabled || !ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp
                || ti.maxTextureSize < 4096 || ti.textureCompression != TextureImporterCompression.CompressedHQ;
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.sRGBTexture = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            return changed;
        }

        [MenuItem("Mori/UI 그림 다시 가져오기")]
        public static void ReimportAll() => Fix(true);

        [InitializeOnLoadMethod]
        static void OnLoad() => EditorApplication.delayCall += () => Fix(false);

        static void Fix(bool force)
        {
            int n = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ROOT }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!Mine(path) || !(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                    if (Apply(ti) || force) { ti.SaveAndReimport(); n++; }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            if (n > 0) Debug.Log($"[Mori] UI 그림 {n}개를 원래 크기로 다시 가져왔습니다.");
        }
    }
}
