using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Mori.Client
{
    /// <summary>
    /// 효과음 바꿔 끼우기: Data~/Sounds/&lt;이름&gt;.wav · .ogg · .mp3 가 있으면 코드로 만든 소리 대신 그 파일.
    /// 이름: atk-magic(마법 공격) · atk-mech(기계 공격). 파일을 넣거나 바꾼 뒤 F2 › 소리 › ‘효과음 파일 다시 읽기’.
    /// </summary>
    public static class SfxFiles
    {
        public static readonly (string key, string name)[] KEYS = { ("atk-magic", "마법 공격"), ("atk-mech", "기계 공격") };
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static bool loaded;

        public static string Dir => Path.Combine(KitPaths.Root, "Sounds");

        /// <summary>그 이름의 파일 소리(없으면 fallback).</summary>
        public static AudioClip Get(string key, AudioClip fallback)
        {
            if (!loaded) Reload();
            return clips.TryGetValue(key, out var c) && c != null ? c : fallback;
        }

        public static bool Has(string key) => clips.TryGetValue(key, out var c) && c != null;

        /// <summary>폴더를 다시 읽음(비동기 — 다 읽으면 done(읽은 개수)).</summary>
        public static void Reload(Action<int> done = null)
        {
            loaded = true;
            clips.Clear();
            try { Directory.CreateDirectory(Dir); } catch { }
            int pending = 0, ok = 0;
            foreach (var (key, _) in KEYS)
            {
                string path = null; AudioType type = AudioType.UNKNOWN;
                foreach (var (ext, t) in new[] { (".wav", AudioType.WAV), (".ogg", AudioType.OGGVORBIS), (".mp3", AudioType.MPEG) })
                {
                    string p = Path.Combine(Dir, key + ext);
                    if (File.Exists(p)) { path = p; type = t; break; }
                }
                if (path == null) continue;
                pending++;
                string k = key;
                var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
                ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;
                req.SendWebRequest().completed += _ =>
                {
                    try
                    {
                        if (req.result == UnityWebRequest.Result.Success)
                        {
                            var c = DownloadHandlerAudioClip.GetContent(req);
                            if (c != null) { c.name = k; clips[k] = c; ok++; }
                        }
                        else Debug.LogWarning($"[Mori 킷] 효과음 {k} 읽기 실패: {req.error}");
                    }
                    catch (Exception e) { Debug.LogWarning($"[Mori 킷] 효과음 {k} 읽기 실패: {e.Message}"); }
                    finally { req.Dispose(); if (--pending == 0) done?.Invoke(ok); }
                };
            }
            if (pending == 0) done?.Invoke(0);
        }
    }
}
