using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Audio;
using UnityEngine.Experimental.Video;
using UnityEngine.Video;

namespace Mori.Client
{
    /// <summary>
    /// 동영상 파일의 소리를 곡으로 쓰기. Unity 는 mp4 등의 소리를 AudioClip 으로 바로 읽지 못하므로,
    /// VideoPlayer 로 영상을 (화면에 그리지 않고) 한 번 재생하며 소리 샘플만 받아 WAV 로 저장한다(music/.cache).
    /// 처음 한 번은 곡 길이만큼 걸리고(진행률 Progress), 그 뒤로는 저장된 WAV 를 바로 읽는다.
    /// 지원: mp4 · m4v · mov · webm · avi · wmv (운영체제 코덱에 따라 다를 수 있음).
    /// </summary>
    public sealed class VideoAudio : MonoBehaviour
    {
        public static readonly string[] EXTS = { ".mp4", ".m4v", ".mov", ".webm", ".avi", ".wmv" };
        public static bool IsVideo(string file) => Array.IndexOf(EXTS, Path.GetExtension(file ?? "").ToLowerInvariant()) >= 0;
        public static string CacheDir => Path.Combine(AudioHub.MusicDir, ".cache");

        /// <summary>변환 중인지 · 진행률(0~1) · 변환 중인 파일 이름.</summary>
        public static bool Busy => host != null && host.vp != null && host.src != null;
        public static float Progress { get; private set; }
        public static string Current => host?.src != null ? Path.GetFileName(host.src) : null;

        static VideoAudio host;

        /// <summary>원본이 바뀌면 다시 뽑도록 크기·수정 시각을 이름에 넣는다.</summary>
        public static string CachePath(string src)
        {
            var fi = new FileInfo(src);
            return Path.Combine(CacheDir, Path.GetFileNameWithoutExtension(src) + "-" + fi.Length + "-" + fi.LastWriteTimeUtc.Ticks + ".wav");
        }

        /// <summary>영상 파일 → (저장된) WAV 경로. 끝나면 done(wav 경로, 오류).</summary>
        public static void Extract(string path, Action<string, string> done)
        {
            string wav;
            try { wav = CachePath(path); }
            catch (Exception e) { done(null, "영상 파일을 읽지 못했어요: " + e.Message); return; }
            if (File.Exists(wav)) { done(wav, null); return; }
            if (host == null)
            {
                var go = new GameObject("Mori VideoAudio");
                DontDestroyOnLoad(go);
                host = go.AddComponent<VideoAudio>();
            }
            host.Begin(path, wav, done);
        }

        VideoPlayer vp;
        AudioSampleProvider prov;
        string src, dst;
        readonly List<Action<string, string>> waiters = new List<Action<string, string>>();
        readonly List<float> pcm = new List<float>(1 << 20);
        ushort channels;
        uint rate;
        float idle, age;
        bool ending;

        void Begin(string path, string wav, Action<string, string> done)
        {
            if (src == path) { waiters.Add(done); return; }   // 같은 영상을 이미 뽑는 중
            if (src != null) Finish(null, "다른 영상을 고르셔서 멈췄어요");
            src = path;
            dst = wav;
            waiters.Add(done);
            pcm.Clear();
            prov = null;
            idle = age = 0;
            ending = false;
            Progress = 0;
            if (vp == null)
            {
                vp = gameObject.AddComponent<VideoPlayer>();
                vp.errorReceived += (_, msg) => Finish(null, "영상을 재생하지 못했어요(코덱을 확인해 주세요): " + msg);
                vp.prepareCompleted += _ => OnPrepared();
                vp.loopPointReached += _ => ending = true;
            }
            vp.Stop();
            vp.playOnAwake = false;
            vp.isLooping = false;
            vp.source = VideoSource.Url;
            vp.url = path;
            vp.renderMode = VideoRenderMode.APIOnly;
            vp.audioOutputMode = VideoAudioOutputMode.APIOnly;
            vp.controlledAudioTrackCount = 1;
            vp.EnableAudioTrack(0, true);
            vp.skipOnDrop = true;
            vp.playbackSpeed = 1;
            vp.Prepare();
        }

        void OnPrepared()
        {
            if (src == null) return;
            if (vp.audioTrackCount == 0) { Finish(null, "이 영상에는 소리 트랙이 없어요"); return; }
            prov = vp.GetAudioSampleProvider(0);
            if (prov == null) { Finish(null, "영상의 소리를 읽을 수 없어요"); return; }
            channels = Math.Max((ushort)1, prov.channelCount);
            rate = prov.sampleRate > 0 ? prov.sampleRate : 48000;
            vp.Play();
        }

        void Update()
        {
            if (src == null) return;
            age += Time.unscaledDeltaTime;
            if (prov == null)
            {
                if (age > 20) Finish(null, "영상을 여는 데 너무 오래 걸려요(지원하지 않는 형식일 수 있어요)");
                return;
            }
            Drain();
            double len = vp.length;
            if (len > 0) Progress = Mathf.Clamp01((float)(vp.time / len));
            // 끝: 끝 지점에 닿았거나, 재생이 멈췄고 더 오는 소리가 없을 때
            idle += Time.unscaledDeltaTime;
            bool stalled = idle > 3f;
            if ((ending && idle > 0.4f) || (stalled && pcm.Count > 0 && !vp.isPlaying)) Write();
            else if (stalled && pcm.Count == 0 && age > 12) Finish(null, "영상에서 소리가 나오지 않아요(소리 코덱을 지원하지 않을 수 있어요)");
        }

        void Drain()
        {
            uint n = prov.availableSampleFrameCount;
            if (n == 0) return;
            var buf = new NativeArray<float>((int)(n * channels), Allocator.Temp);
            try
            {
                uint got = prov.ConsumeSampleFrames(buf);
                int cnt = (int)(got * channels);
                for (int i = 0; i < cnt; i++) pcm.Add(buf[i]);
                if (got > 0) idle = 0;
            }
            finally { buf.Dispose(); }
        }

        void Write()
        {
            try
            {
                Drain();
                vp.Stop();
                Directory.CreateDirectory(CacheDir);
                string tmp = dst + ".tmp";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                using (var w = new BinaryWriter(fs))
                {
                    int frames = pcm.Count / channels;
                    int bytes = frames * channels * 2;
                    w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                    w.Write(36 + bytes);
                    w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                    w.Write(16);
                    w.Write((short)1);
                    w.Write((short)channels);
                    w.Write((int)rate);
                    w.Write((int)(rate * channels * 2));
                    w.Write((short)(channels * 2));
                    w.Write((short)16);
                    w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                    w.Write(bytes);
                    for (int i = 0; i < frames * channels; i++)
                        w.Write((short)Mathf.Clamp(Mathf.RoundToInt(pcm[i] * 32767f), -32768, 32767));
                }
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(tmp, dst);
                Finish(dst, null);
            }
            catch (Exception e) { Finish(null, "영상의 소리를 저장하지 못했어요: " + e.Message); }
        }

        void Finish(string wav, string err)
        {
            var list = new List<Action<string, string>>(waiters);
            waiters.Clear();
            src = null;
            prov = null;
            pcm.Clear();
            Progress = 0;
            if (vp != null) vp.Stop();
            foreach (var cb in list)
            {
                try { cb(wav, err); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
