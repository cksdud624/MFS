using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Mori.Client
{
    /// <summary>
    /// 소리 재생: 음악 1채널 + 효과음 여러 채널. 음량은 설정(전체 · 음악 · 효과음)을 따른다.
    /// 음악 파일(wav · ogg · mp3, 그리고 mp4 등 동영상의 소리)은 persistentDataPath/music 에서 읽는다(노트 에디터에서 내 곡을 쓸 때).
    /// </summary>
    public sealed class AudioHub
    {
        readonly GameObject go;
        public readonly AudioSource Music;
        /// <summary>게이트 슬로모션 때 마지막 마디를 느리게 되풀이하는 채널(음악과 같은 저역 필터를 지난다).</summary>
        public readonly AudioSource Loop;
        readonly AudioLowPassFilter lp;
        readonly List<AudioSource> sfx = new List<AudioSource>();
        int next;
        static AudioClip testClip;

        public static string MusicDir => Path.Combine(KitPaths.Data, "music");
        public static readonly string[] EXTS = { ".ogg", ".wav", ".mp3", ".mp4", ".m4v", ".mov", ".webm", ".avi", ".wmv" };

        public AudioHub(GameObject host)
        {
            go = new GameObject("Mori Audio");
            go.transform.SetParent(host.transform, false);
            // 음악 채널은 따로 둔다 — 저역 필터(슬로모션의 먹먹함)가 효과음에는 걸리지 않게
            var mgo = new GameObject("Mori Music");
            mgo.transform.SetParent(go.transform, false);
            Music = mgo.AddComponent<AudioSource>();
            Music.playOnAwake = false;
            Music.loop = false;
            Loop = mgo.AddComponent<AudioSource>();
            Loop.playOnAwake = false;
            Loop.loop = true;
            lp = mgo.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = 22000;
            lp.lowpassResonanceQ = 1.2f;
            lp.enabled = false;
            for (int i = 0; i < 12; i++)
            {
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                sfx.Add(s);
            }
            Synth.WarmUp();
            Apply();
        }

        static GameSettings S => KitRoot.Settings;
        // 음량 슬라이더 50% = 기준 크기(예전 기본: 마스터 0.8 · 음악 0.8 · 효과음 0.9), 100% = 그 두 배까지
        const float MASTER_K = 1.6f, MUSIC_K = 1.6f, SFX_K = 1.8f;
        public float MusicVol => S.VolMaster * MASTER_K * S.VolMusic * MUSIC_K;
        public float SfxVol => S.VolMaster * MASTER_K * S.VolSfx * SFX_K;

        public void Apply() { Music.volume = MusicVol; Loop.volume = MusicVol; }

        /// <summary>음악 저역 필터(Hz). 21 kHz 이상이면 끈다.</summary>
        public void SetCutoff(float hz)
        {
            lp.enabled = hz < 21000;
            lp.cutoffFrequency = Mathf.Clamp(hz, 150, 22000);
        }

        /// <summary>음악 두 채널 모두 멈추고 원래대로(속도·필터).</summary>
        public void StopMusic()
        {
            Music.Stop(); Loop.Stop();
            Music.pitch = 1; Loop.pitch = 1;
            Music.volume = Loop.volume = MusicVol;
            SetCutoff(22000);
        }

        static readonly Dictionary<string, AudioClip> slices = new Dictionary<string, AudioClip>();

        /// <summary>곡의 [t0, t1) 구간을 따로 떼어 낸 클립(되풀이용). 읽을 수 없는 형식이면 null.</summary>
        public static AudioClip Slice(AudioClip src, double t0, double t1)
        {
            if (src == null || t1 - t0 < 0.05) return null;
            string key = src.name + "|" + src.samples + "|" + src.frequency + "|" + Math.Round(t0, 3) + ":" + Math.Round(t1, 3);   // GetInstanceID 는 Unity 6.5 에서 쓸 수 없음
            if (slices.TryGetValue(key, out var c) && c != null) return c;
            try
            {
                int fr = src.frequency, ch = Math.Max(1, src.channels);
                int i0 = Mathf.Clamp((int)(t0 * fr), 0, src.samples - 1);
                int n = Mathf.Clamp((int)((t1 - t0) * fr), 1, src.samples - i0);
                var data = new float[n * ch];
                if (!src.GetData(data, i0)) return null;
                // 이음매 딸깍 소리 없게 끝 5ms 를 살짝 줄인다
                int fade = Math.Min(n / 4, fr / 200);
                for (int i = 0; i < fade; i++)
                {
                    float k = i / (float)fade;
                    for (int q = 0; q < ch; q++) { data[i * ch + q] *= k; data[(n - 1 - i) * ch + q] *= k; }
                }
                c = AudioClip.Create(src.name + "-loop", n, ch, fr, false);
                c.SetData(data, 0);
                slices[key] = c;
                return c;
            }
            catch (Exception e) { Debug.LogWarning("[Mori] 구간을 떼어 내지 못했어요: " + e.Message); return null; }
        }

        public void Play(AudioClip c, float vol = 1, float pitch = 1)
        {
            if (c == null) return;
            var s = sfx[next];
            next = (next + 1) % sfx.Count;
            s.pitch = pitch;
            s.PlayOneShot(c, Mathf.Max(0, vol * SfxVol));       // 1 을 넘으면 더 크게(슬라이더 50% 넘김)
        }

        /// <summary>내장 테스트 곡 AudioClip.</summary>
        public static AudioClip TestClip()
        {
            if (testClip == null) testClip = Synth.Clip("Mori Pulse (test)", Synth.TestSong());
            return testClip;
        }

        /// <summary>음악 폴더의 파일 목록(이름만).</summary>
        public static List<string> MusicFiles()
        {
            // 내 음악 폴더 + 빌드에 담긴 음악(StreamingAssets/Bundled/music — 이름이 같으면 내 것)
            try { Directory.CreateDirectory(MusicDir); } catch (Exception e) { Debug.LogWarning("[Mori] 음악 폴더를 만들지 못했습니다: " + e.Message); }
            return Bundled.Files(MusicDir, "music", n => Array.IndexOf(EXTS, Path.GetExtension(n).ToLowerInvariant()) >= 0);
        }

        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        /// <summary>
        /// 차트의 Audio 값으로 곡을 불러온다("synth:test" 또는 음악 폴더의 파일 이름). 끝나면 done(clip, 오류).
        /// </summary>
        public static void Load(string audio, Action<AudioClip, string> done)
        {
            if (string.IsNullOrEmpty(audio) || audio.StartsWith("synth:")) { done(TestClip(), null); return; }
            if (cache.TryGetValue(audio, out var c) && c != null) { done(c, null); return; }
            string path = Bundled.Pick(Path.Combine(MusicDir, audio), Path.Combine("music", audio));
            if (!File.Exists(path)) { done(null, $"음악 파일을 찾지 못했어요: {audio}\n(음악 폴더: {MusicDir})"); return; }
            // 동영상: 소리만 뽑아 WAV 로 저장한 뒤(처음 한 번) 그 WAV 를 읽는다
            if (VideoAudio.IsVideo(path))
            {
                VideoAudio.Extract(path, (wav, err) =>
                {
                    if (wav == null) { done(null, err); return; }
                    LoadFile(wav, AudioType.WAV, audio, done);
                });
                return;
            }
            string ext = Path.GetExtension(path).ToLowerInvariant();
            LoadFile(path, ext == ".wav" ? AudioType.WAV : ext == ".mp3" ? AudioType.MPEG : AudioType.OGGVORBIS, audio, done);
        }

        static void LoadFile(string path, AudioType type, string audio, Action<AudioClip, string> done)
        {
            var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
            ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;
            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                try
                {
                    if (req.result != UnityWebRequest.Result.Success) { done(null, "음악을 불러오지 못했어요: " + req.error); return; }
                    var clip = DownloadHandlerAudioClip.GetContent(req);
                    if (clip == null || clip.length <= 0) { done(null, "음악 파일을 읽을 수 없어요(형식 확인: ogg · wav · mp3 · mp4 등 동영상)"); return; }
                    clip.name = audio;
                    cache[audio] = clip;
                    done(clip, null);
                }
                catch (Exception e) { done(null, "음악을 불러오지 못했어요: " + e.Message); }
                finally { req.Dispose(); }
            };
        }
    }
}
