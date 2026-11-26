using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mori.Core.Art;
using UnityEngine;

namespace Mori.Client
{
    /// <summary>
    /// 결투 그림 창고: StreamingAssets/Fight 의 PSD 를 직접 읽어 캐릭터 애니메이션 · 배경 · 지형 그림으로 만든다.
    /// 0.5초마다 파일을 살펴 저장이 끝난(크기 · 시각이 두 번 같은) 파일만 뒤에서 다시 읽고, 다 읽으면 바꿔 끼운다 — 게임이 멈추지 않는다.
    /// 빌드에서도 같은 폴더(게임이름_Data/StreamingAssets/Fight)를 읽으므로 빌드된 게임에서도 PSD 를 고치면 바로 반영된다.
    ///
    /// 파일 이름(확장자 앞, ‘·’ · 띄어쓰기 · ‘.’ 로 나눔): 첫 낱말 = 이름, 나머지 = 설정
    ///   hero-run · 64x64 · 100ms.psd   → 한 판에 64×64 칸으로 나란히(시트식), 컷마다 100ms
    ///   hero-attack1.once.psd          → 컷 폴더(겹쳐 놓기), 한 번만 재생
    ///   설정 낱말: once/1회 · 64x64(칸 크기) · 100ms 또는 100,100,80ms(컷 길이) · 도트/pixel(선명) · 부드럽게/smooth
    ///             32px(1U 당 픽셀, 배경 · 지형) · 바닥1500(배경의 바닥선 픽셀 줄) · cap12/끝12(지형 끝 조각 픽셀)
    /// 이름: hero-* · foe-* = 캐릭터 상태, bg = 배경, pillar/기둥 = 기둥, platform/발판 = 발판.
    /// </summary>
    public static class ArtLib
    {
        public static string Dir => KitPaths.Art;          // Assets/MoriKit/Data~/Fight(예전: StreamingAssets/Fight — 처음 한 번 옮겨 옴)

        /// <summary>그림이 바뀔 때마다 +1.</summary>
        public static int Version { get; private set; }
        /// <summary>마지막 읽기 오류(패널 · 로그용).</summary>
        public static string LastError { get; private set; }

        // ───────── 결과물 ─────────
        public sealed class Anim
        {
            public string Key;
            public Texture2D[] Frames;
            public float[] Ms;
            public bool Once;
            public int W, H;
            /// <summary>발밑 선: 캔버스 맨 아래에서 몇 px 위가 발이 닿는 바닥인지(그 아래 = 바닥을 뚫고 나가는 자리). 파일 이름 ‘발밑32’ · ‘foot32’.</summary>
            public int Foot;
            /// <summary>캔버스 기준 = 가운데(이펙트용 그림 — 파일 이름 ‘가운데’). 아니면 발밑 기준.</summary>
            public bool Center;
            public float TotalMs;
            /// <summary>사슬 기준점(@이름 레이어) — 컷마다 캔버스 좌표.</summary>
            public List<Track> Tracks = new List<Track>();
            /// <summary>시작부터 지난 시간(ms) → 컷.</summary>
            public Texture2D At(float ms) => Frames[IndexAt(ms)];
            public int IndexAt(float ms)
            {
                if (Frames.Length == 1 || TotalMs <= 0) return 0;
                float t = Once ? Mathf.Min(ms, TotalMs - 0.001f) : Mathf.Repeat(ms, TotalMs);
                for (int i = 0; i < Frames.Length; i++) { if (t < Ms[i]) return i; t -= Ms[i]; }
                return Frames.Length - 1;
            }
        }

        /// <summary>휘는 그림 한 장(캔버스의 (L, T) 에 놓인 W×H RGBA, 위 → 아래).</summary>
        public sealed class ArtPx { public byte[] Rgba; public int L, T, W, H; public double HalfW = -1; }

        /// <summary>컷 하나의 사슬: 뼈 마디(캔버스 좌표, 첫 점 = 뿌리) · 휘는 그림(없을 수 있음) · 몸 뒤인지.</summary>
        public sealed class FrameRig { public List<(double x, double y)> Joints; public ArtPx Art; public bool Back = true; }

        /// <summary>사슬 하나(@이름 뼈 + ~이름 그림) — 컷마다. 빈 컷은 가까운 컷 것으로 채움.</summary>
        public sealed class Track
        {
            public string Name;
            public bool Back = true;
            public List<(double x, double y)>[] Joints;
            public ArtPx[] Art;
        }

        /// <summary>컷별 사슬(없으면 null) → Track 들. 뼈가 한 컷에도 없으면 만들지 않음.</summary>
        public static List<Track> MakeTracks(List<Dictionary<string, FrameRig>> perFrame)
        {
            var o = new List<Track>();
            var names = new List<string>();
            foreach (var d in perFrame) if (d != null) foreach (var kv in d) if (kv.Value.Joints != null && kv.Value.Joints.Count > 0 && !names.Contains(kv.Key)) names.Add(kv.Key);
            int n = perFrame.Count;
            foreach (var nm in names)
            {
                var t = new Track { Name = nm, Joints = new List<(double, double)>[n], Art = new ArtPx[n] };
                for (int i = 0; i < n; i++)
                    if (perFrame[i] != null && perFrame[i].TryGetValue(nm, out var fr))
                    {
                        if (fr.Joints != null && fr.Joints.Count > 0) t.Joints[i] = fr.Joints;
                        t.Art[i] = fr.Art;
                        t.Back = fr.Back;
                    }
                Fill(t.Joints); Fill(t.Art);
                o.Add(t);
            }
            return o;
        }

        static void Fill<T>(T[] a) where T : class
        {
            int first = Array.FindIndex(a, x => x != null);
            if (first < 0) return;
            for (int i = 0; i < first; i++) a[i] = a[first];
            for (int i = first + 1; i < a.Length; i++) if (a[i] == null) a[i] = a[i - 1];
        }

        public sealed class Tile { public Texture2D Tex; public int X, Y, W, H; }
        public sealed class BgLayer
        {
            public string Name;
            public float Par = 1;
            public bool Fixed, Front;
            public List<Tile>[] Frames;
            public float[] Ms;
            public float TotalMs;
            public List<Tile> At(float ms)
            {
                if (Frames.Length == 1 || TotalMs <= 0) return Frames[0];
                float t = Mathf.Repeat(ms, TotalMs);
                for (int i = 0; i < Frames.Length; i++) { if (t < Ms[i]) return Frames[i]; t -= Ms[i]; }
                return Frames[Frames.Length - 1];
            }
        }
        public sealed class Background { public int W, H, GroundY; public float PxPerU = 64; public readonly List<BgLayer> Layers = new List<BgLayer>(); }
        /// <summary>지형 조각: 기둥 = 윗면(Cap) + 몸통(Body, 아래로 반복), 발판 = 왼끝 + 가운데(반복) + 오른끝.</summary>
        public sealed class Slice { public Texture2D Cap, Body, Left, Mid, Right; public float PxPerU = 64; }


        static readonly Dictionary<string, Anim> anims = new Dictionary<string, Anim>();
        public static Background Bg { get; private set; }
        public static Slice Pillar { get; private set; }
        public static Slice Platform { get; private set; }
        /// <summary>읽어 둔 애니메이션 전부(편집 중인 것 포함).</summary>
        public static IEnumerable<Anim> AllAnims => anims.Values.Concat(live.Values);
        public static Anim Get(string key) => key == null ? null : live.TryGetValue(key, out var l) ? l : anims.TryGetValue(key, out var a) ? a : null;

        // ───────── 편집기 실시간 그림(저장 전에도 결투에 바로 보이게) ─────────
        static readonly Dictionary<string, Anim> live = new Dictionary<string, Anim>();
        /// <summary>편집 중인 그림을 그 이름으로 덮어 보인다(텍스처는 편집기 것 — 여기서 지우지 않음).</summary>
        public static void SetLive(string key, Texture2D[] frames, float[] ms, bool once, List<Track> tracks = null, int foot = 0, bool center = false)
        {
            if (string.IsNullOrEmpty(key) || frames == null || frames.Length == 0) return;
            var a = new Anim { Key = key, Frames = frames, Ms = ms.Select(x => Mathf.Max(1, x)).ToArray(), Once = once, W = frames[0].width, H = frames[0].height, Tracks = tracks ?? new List<Track>(), Foot = Mathf.Clamp(foot, 0, Mathf.Max(0, frames[0].height - 1)), Center = center };
            a.TotalMs = a.Ms.Sum();
            live[key] = a;
        }
        public static void ClearLive(string key) { if (key != null && live.Remove(key)) Version++; }

        /// <summary>폴더의 PSD 목록: (경로, 이름, 시트식인지).</summary>
        public static List<(string path, string key, bool sheet)> Files()
        {
            var o = new List<(string, string, bool)>();
            if (!Directory.Exists(Dir)) return o;
            foreach (var p in Directory.GetFiles(Dir))
            {
                var ext = Path.GetExtension(p).ToLowerInvariant();
                if (ext != ".psd" && ext != ".psb") continue;
                var sp = Spec.Parse(Path.GetFileNameWithoutExtension(p), true);
                o.Add((p, sp.Key, sp.CellW > 0));
            }
            return o;
        }
        /// <summary>파일 이름의 설정 중 ‘한 번만 재생’.</summary>
        public static bool OnceOf(string path) => Spec.Parse(Path.GetFileNameWithoutExtension(path), true).Once;
        /// <summary>파일 이름(확장자 앞)의 낱말들.</summary>
        /// <summary>파일 이름의 발밑 선(px, 없으면 0).</summary>
        public static int FootOf(string fileNameNoExt)
        {
            foreach (var t in Tokens(fileNameNoExt))
            {
                var m = Regex.Match(t.ToLowerInvariant(), @"^(?:발밑|foot)(\d+)$");
                if (m.Success) return int.Parse(m.Groups[1].Value);
            }
            return 0;
        }
        public static bool IsFootToken(string t) => Regex.IsMatch(t.ToLowerInvariant(), @"^(?:발밑|foot)\d+$");
        /// <summary>파일 이름 표시 ‘가운데’(center) = 이펙트용 그림, 캔버스 가운데가 기준점.</summary>
        public static bool IsCenterToken(string t) { var lo = (t ?? "").Trim().ToLowerInvariant(); return lo == "가운데" || lo == "center"; }

        public static string[] Tokens(string fileNameNoExt) => Regex.Split(fileNameNoExt.Trim(), @"[\s·.]+").Where(t => t.Length > 0).ToArray();
        public static int Count => anims.Count + (Bg != null ? 1 : 0) + (Pillar != null ? 1 : 0) + (Platform != null ? 1 : 0);

        // ───────── 파일 살피기 ─────────
        sealed class Watch { public DateTime T; public long Size; public bool Dirty = true, Loading; public string Key; }
        static readonly Dictionary<string, Watch> files = new Dictionary<string, Watch>();
        static readonly ConcurrentQueue<Action> done = new ConcurrentQueue<Action>();
        static float pollT;

        /// <summary>매 프레임(메인 스레드). 파일 살피기 · 다 읽은 결과 바꿔 끼우기.</summary>
        public static void Tick(float dt)
        {
            while (done.TryDequeue(out var a))
            {
                try { a(); } catch (Exception e) { LastError = e.Message; Debug.LogWarning("[Mori] 그림 적용 실패: " + e); }
            }
            pollT -= dt;
            if (pollT > 0) return;
            pollT = 0.5f;
            try { Poll(); } catch (Exception e) { LastError = e.Message; }
        }

        static void Poll()
        {
            var dir = Dir;
            if (!Directory.Exists(dir)) return;
            var seen = new HashSet<string>();
            foreach (var path in Directory.GetFiles(dir))
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".psd" && ext != ".psb") continue;
                seen.Add(path);
                var fi = new FileInfo(path);
                if (!files.TryGetValue(path, out var w)) { files[path] = w = new Watch { T = fi.LastWriteTimeUtc, Size = fi.Length }; continue; }   // 다음 살피기에서 그대로면 읽음
                if (fi.LastWriteTimeUtc != w.T || fi.Length != w.Size) { w.T = fi.LastWriteTimeUtc; w.Size = fi.Length; w.Dirty = true; continue; }   // 저장 중일 수 있음 — 한 번 더 기다림
                if (w.Dirty && !w.Loading) { w.Dirty = false; w.Loading = true; Load(path, w); }
            }
            foreach (var gone in files.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                var w = files[gone];
                files.Remove(gone);
                if (w.Key != null) done.Enqueue(() => Remove(w.Key));
            }
        }

        static void Load(string path, Watch w)
        {
            string fname = Path.GetFileNameWithoutExtension(path);
            Task.Run(() =>
            {
                try
                {
                    var bytes = File.ReadAllBytes(path);
                    var doc = Psd.Read(bytes);
                    var spec = Spec.Parse(fname, true);
                    w.Key = spec.Key;
                    var job = Build(doc, spec);
                    done.Enqueue(() => { job(); w.Loading = false; LastError = null; Version++; Debug.Log($"[Mori] 그림 읽음: {fname}"); });
                }
                catch (IOException) { w.Loading = false; w.Dirty = true; }     // 아직 저장 중 — 다음에 다시
                catch (Exception e)
                {
                    done.Enqueue(() => { w.Loading = false; LastError = $"{fname}: {e.Message}"; Debug.LogWarning($"[Mori] 그림 읽기 실패({fname}): {e.Message}"); });
                }
            });
        }

        // ───────── 이름 · 설정 ─────────
        sealed class Spec
        {
            public string Key = "";
            public bool Once, Point, Smooth, HasNum;
            public int CellW, CellH, Ground = -1, Cap = -1, Num = -1, Frames = -1, Fps = 60, Foot = 0;
            public bool Center;
            public float[] Ms;
            public float Px = -1, Par = float.NaN;
            public bool Fixed, Front, Anim;

            static readonly Regex Cell = new Regex(@"^(\d+)[x×](\d+)$"), MsRx = new Regex(@"^(\d+(?:,\d+)*)ms$"), PxRx = new Regex(@"^(\d+(?:\.\d+)?)px$"),
                GroundRx = new Regex(@"^(?:바닥|ground)(\d+)$"), FootRx = new Regex(@"^(?:발밑|foot)(\d+)$"), CapRx = new Regex(@"^(?:cap|끝)(\d+)$"), NumRx = new Regex(@"^-?\d+(?:\.\d+)?$"),
                FRx = new Regex(@"^(\d+)(?:f|프레임)$"), FpsRx = new Regex(@"^(\d+)fps$");

            /// <summary>file = 파일 이름(‘.’ 도 나눔), 아니면 레이어 · 폴더 이름(‘.’ 은 소수점).</summary>
            public static Spec Parse(string name, bool file)
            {
                var s = new Spec();
                var toks = Regex.Split(name.Trim(), file ? @"[\s·.]+" : @"[\s·]+").Where(t => t.Length > 0).ToArray();
                for (int i = 0; i < toks.Length; i++)
                {
                    string t = toks[i], lo = t.ToLowerInvariant();
                    if (i == 0 && file) { s.Key = lo; continue; }
                    Match m;
                    if (lo == "once" || lo == "1회") s.Once = true;
                    else if (lo == "도트" || lo == "pixel" || lo == "point") s.Point = true;
                    else if (lo == "부드럽게" || lo == "smooth") s.Smooth = true;
                    else if (lo == "고정" || lo == "fixed") s.Fixed = true;
                    else if (lo == "앞" || lo == "front") s.Front = true;
                    else if (lo == "애니" || lo == "anim") s.Anim = true;
                    else if ((m = Cell.Match(lo)).Success) { s.CellW = int.Parse(m.Groups[1].Value); s.CellH = int.Parse(m.Groups[2].Value); }
                    else if ((m = FpsRx.Match(lo)).Success) s.Fps = Math.Max(1, int.Parse(m.Groups[1].Value));
                    else if ((m = FRx.Match(lo)).Success) s.Frames = Math.Max(1, int.Parse(m.Groups[1].Value));
                    else if ((m = MsRx.Match(lo)).Success) s.Ms = m.Groups[1].Value.Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                    else if ((m = PxRx.Match(lo)).Success) s.Px = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    else if ((m = GroundRx.Match(lo)).Success) s.Ground = int.Parse(m.Groups[1].Value);
                    else if ((m = FootRx.Match(lo)).Success) s.Foot = int.Parse(m.Groups[1].Value);
                    else if (lo == "가운데" || lo == "center") s.Center = true;
                    else if ((m = CapRx.Match(lo)).Success) s.Cap = int.Parse(m.Groups[1].Value);
                    else if (NumRx.IsMatch(lo))
                    {
                        float v = float.Parse(lo, CultureInfo.InvariantCulture);
                        if (!file && i == 0 && !lo.Contains(".")) { s.Num = (int)v; s.HasNum = true; }   // 컷 폴더 번호 "03"
                        else s.Par = v;                                                              // 배경 층 시차 "0.2"
                    }
                }
                if (!file && toks.Length > 0 && NumRx.IsMatch(toks[0]) && !toks[0].Contains(".")) { s.Num = int.Parse(toks[0]); s.HasNum = true; }
                return s;
            }

            public float MsAt(int i) => Ms == null || Ms.Length == 0 ? 100 : Ms[Math.Min(i, Ms.Length - 1)];
        }

        // ───────── 만들기(뒤 스레드: 픽셀만) → 바꿔 끼우기(메인 스레드: 텍스처) ─────────
        sealed class Px { public byte[] Rgba; public int W, H; }

        static Action Build(PsdDoc doc, Spec spec)
        {
            string k = spec.Key;
            if (k == "bg" || k == "배경") return BuildBg(doc, spec);
            if (k == "pillar" || k == "기둥") return BuildSlice(doc, spec, true);
            if (k == "platform" || k == "발판") return BuildSlice(doc, spec, false);
            return BuildAnim(doc, spec);
        }

        static bool Empty(byte[] rgba) { for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] != 0) return false; return true; }

        static byte[] Crop(byte[] src, int sw, int x, int y, int w, int h)
        {
            var o = new byte[w * h * 4];
            for (int yy = 0; yy < h; yy++) Buffer.BlockCopy(src, ((y + yy) * sw + x) * 4, o, yy * w * 4, w * 4);
            return o;
        }

        static Action BuildAnim(PsdDoc doc, Spec spec)
        {
            var frames = new List<Px>();
            var ms = new List<float>();
            int W = doc.W, H = doc.H;
            // 사슬 레이어(@이름 = 뼈 마디 · ~이름 = 휘는 그림)는 몸 그림에서 빼고 따로
            var anchors = ChainAnchors.Strip(doc);
            var perFrame = new List<Dictionary<string, FrameRig>>();
            Dictionary<string, FrameRig> AnchorsOf(PsdNode frameGroup)
            {
                Dictionary<string, FrameRig> d = null;
                // 컷 폴더 안 것을 먼저, 공통(폴더 밖)은 비어 있는 것만 채움
                foreach (bool common in new[] { false, true })
                    foreach (var (leaf, topG) in anchors)
                    {
                        if (common ? topG != null : topG != frameGroup) continue;
                        if (!ChainAnchors.ParseAny(leaf.Name, out bool deform, out var nm, out bool back) || !leaf.Visible) continue;
                        if (d == null) d = new Dictionary<string, FrameRig>();
                        if (!d.TryGetValue(nm, out var fr)) d[nm] = fr = new FrameRig { Back = back };
                        if (!deform)
                        {
                            if (fr.Joints != null) continue;
                            var j = ChainAnchors.Joints(leaf);
                            if (j.Count > 0) { fr.Joints = j; fr.Back = back; }
                        }
                        else if (fr.Art == null && leaf.Rgba != null && leaf.W > 0 && leaf.H > 0)
                        {
                            fr.Art = new ArtPx { Rgba = leaf.Rgba, L = leaf.L, T = leaf.T, W = leaf.W, H = leaf.H };
                            fr.Back = back;
                        }
                    }
                return d;
            }
            var top = doc.Root.Kids;
            if (spec.CellW > 0 && spec.CellH > 0)
            {
                // 시트식: 한 판에 칸칸이 — 칸 크기대로 잘라 왼쪽 위부터 오른쪽으로, 줄 바꿈
                var all = Psd.Compose(top, 0, 0, W, H);
                int cols = W / spec.CellW, rows = H / spec.CellH;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        var cell = Crop(all, W, c * spec.CellW, r * spec.CellH, spec.CellW, spec.CellH);
                        if (Empty(cell)) continue;
                        frames.Add(new Px { Rgba = cell, W = spec.CellW, H = spec.CellH });
                        ms.Add(spec.MsAt(frames.Count - 1));
                    }
                W = spec.CellW; H = spec.CellH;
            }
            else if (top.Any(n => n.Group))
            {
                // 겹쳐 놓기: 맨 위 폴더 하나 = 컷 하나(번호가 있으면 번호 순, 없으면 아래 → 위). 폴더 밖 레이어는 모든 컷에 공통.
                var groups = top.Where(n => n.Group).ToList();
                var specs = groups.ToDictionary(g => g, g => Spec.Parse(g.Name, false));
                if (groups.All(g => specs[g].HasNum)) groups = groups.OrderBy(g => specs[g].Num).ToList();
                for (int i = 0; i < groups.Count; i++)
                {
                    var g = groups[i];
                    var list = top.Where(n => !n.Group || n == g).ToList();
                    frames.Add(new Px { Rgba = Psd.Compose(list, 0, 0, W, H, true), W = W, H = H });
                    perFrame.Add(AnchorsOf(g));
                    var gs = specs[g];
                    // 컷 길이: 폴더 이름의 프레임(3f — 파일 이름의 fps, 기본 60) > ms > 파일 이름의 ms
                    ms.Add(gs.Frames > 0 ? gs.Frames * 1000f / spec.Fps : gs.Ms != null && gs.Ms.Length > 0 ? gs.Ms[0] : spec.MsAt(i));
                }
            }
            else
            {
                frames.Add(new Px { Rgba = Psd.Compose(top, 0, 0, W, H), W = W, H = H });
                perFrame.Add(AnchorsOf(null));
                ms.Add(spec.MsAt(0));
            }
            if (frames.Count == 0) frames.Add(new Px { Rgba = new byte[W * H * 4], W = W, H = H });
            var tracks = perFrame.Count == frames.Count ? MakeTracks(perFrame) : new List<Track>();
            bool point = !spec.Smooth;          // 캐릭터는 도트가 기본
            int fw = W, fh = H;
            return () =>
            {
                var a = new Anim { Key = spec.Key, Once = spec.Once, W = fw, H = fh, Frames = frames.Select(f => MakeTex(f, point, spec.Key)).ToArray(), Ms = ms.Select(x => Mathf.Max(1, x)).ToArray(), Tracks = tracks, Foot = Mathf.Clamp(spec.Foot, 0, Mathf.Max(0, fh - 1)), Center = spec.Center };
                a.TotalMs = a.Ms.Sum();
                if (anims.TryGetValue(spec.Key, out var old)) Destroy(old);
                anims[spec.Key] = a;
            };
        }

        const int TILE = 1024;

        static Action BuildBg(PsdDoc doc, Spec spec)
        {
            var layers = new List<(string name, Spec s, List<List<(Px px, int x, int y)>> frames, List<float> ms)>();
            foreach (var n in doc.Root.Kids)
            {
                var ls = Spec.Parse(n.Name, false);
                var frameNodes = new List<(PsdNode node, float ms)>();
                if (ls.Anim && n.Group && n.Kids.Count > 0)
                {
                    var kids = n.Kids.ToList();
                    var ks = kids.ToDictionary(x => x, x => Spec.Parse(x.Name, false));
                    if (kids.All(x => ks[x].HasNum)) kids = kids.OrderBy(x => ks[x].Num).ToList();
                    foreach (var x in kids) frameNodes.Add((x, ks[x].Ms?.FirstOrDefault() ?? ls.MsAt(0)));
                }
                else frameNodes.Add((n, 100));
                var tf = new List<List<(Px, int, int)>>();
                var tms = new List<float>();
                foreach (var (node, fms) in frameNodes)
                {
                    // 그려진 범위만 타일로(빈 캔버스는 건너뜀)
                    int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
                    foreach (var lf in node.Leaves()) { x0 = Math.Min(x0, lf.L); y0 = Math.Min(y0, lf.T); x1 = Math.Max(x1, lf.R); y1 = Math.Max(y1, lf.B); }
                    x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(doc.W, x1); y1 = Math.Min(doc.H, y1);
                    var tiles = new List<(Px, int, int)>();
                    var one = new List<PsdNode> { node };
                    for (int ty = y0; ty < y1; ty += TILE)
                        for (int tx = x0; tx < x1; tx += TILE)
                        {
                            int tw = Math.Min(TILE, x1 - tx), th = Math.Min(TILE, y1 - ty);
                            var px = Psd.Compose(one, tx, ty, tw, th, ls.Anim && node != n);     // 애니 컷 폴더는 숨겨 둬도 읽음
                            if (Empty(px)) continue;
                            tiles.Add((new Px { Rgba = px, W = tw, H = th }, tx, ty));
                        }
                    tf.Add(tiles);
                    tms.Add(fms);
                }
                layers.Add((n.Name, ls, tf, tms));
            }
            bool point = spec.Point;
            int W = doc.W, H = doc.H;
            return () =>
            {
                var bg = new Background { W = W, H = H, PxPerU = spec.Px > 0 ? spec.Px : 64, GroundY = spec.Ground >= 0 ? spec.Ground : H };
                foreach (var (name, ls, tf, tms) in layers)
                {
                    var L = new BgLayer
                    {
                        Name = name, Fixed = ls.Fixed, Front = ls.Front, Par = float.IsNaN(ls.Par) ? 1 : ls.Par,
                        Frames = tf.Select(fr => fr.Select(t => new Tile { Tex = MakeTex(t.px, point || ls.Point, "bg"), X = t.x, Y = t.y, W = t.px.W, H = t.px.H }).ToList()).ToArray(),
                        Ms = tms.Select(x => Mathf.Max(1, x)).ToArray(),
                    };
                    L.TotalMs = L.Ms.Sum();
                    bg.Layers.Add(L);
                }
                if (Bg != null) Destroy(Bg);
                Bg = bg;
            };
        }

        static Action BuildSlice(PsdDoc doc, Spec spec, bool pillar)
        {
            int W = doc.W, H = doc.H;
            var all = Psd.Compose(doc.Root.Kids, 0, 0, W, H);
            Px a, b, c = null;
            if (pillar)
            {
                int cap = Mathf.Clamp(spec.Cap >= 0 ? spec.Cap : H / 3, 0, H - 1);
                a = new Px { Rgba = cap > 0 ? Crop(all, W, 0, 0, W, cap) : null, W = W, H = cap };
                b = new Px { Rgba = Crop(all, W, 0, cap, W, H - cap), W = W, H = H - cap };
            }
            else
            {
                int cap = Mathf.Clamp(spec.Cap >= 0 ? spec.Cap : W / 4, 0, (W - 1) / 2);
                a = new Px { Rgba = cap > 0 ? Crop(all, W, 0, 0, cap, H) : null, W = cap, H = H };
                b = new Px { Rgba = Crop(all, W, cap, 0, W - 2 * cap, H), W = W - 2 * cap, H = H };
                c = new Px { Rgba = cap > 0 ? Crop(all, W, W - cap, 0, cap, H) : null, W = cap, H = H };
            }
            bool point = !spec.Smooth;
            return () =>
            {
                Texture2D T(Px p) => p?.Rgba == null || p.W <= 0 || p.H <= 0 ? null : MakeTex(p, point, spec.Key);
                var s = new Slice { PxPerU = spec.Px > 0 ? spec.Px : 64 };
                if (pillar) { s.Cap = T(a); s.Body = T(b); if (Pillar != null) Destroy(Pillar); Pillar = s; }
                else { s.Left = T(a); s.Mid = T(b); s.Right = T(c); if (Platform != null) Destroy(Platform); Platform = s; }
            };
        }

        static Texture2D MakeTex(Px p, bool point, string name)
        {
            var t = new Texture2D(p.W, p.H, TextureFormat.RGBA32, false) { name = "Mori " + name, wrapMode = TextureWrapMode.Clamp, filterMode = point ? FilterMode.Point : FilterMode.Bilinear };
            // PSD 는 위 → 아래, 텍스처는 아래 → 위
            var flip = new byte[p.Rgba.Length];
            int row = p.W * 4;
            for (int y = 0; y < p.H; y++) Buffer.BlockCopy(p.Rgba, y * row, flip, (p.H - 1 - y) * row, row);
            t.SetPixelData(flip, 0);
            t.Apply(false, true);
            return t;
        }

        static void Kill(Texture2D t) { if (t != null) TexGrave.Kill(t); }      // 화면이 새 그림으로 바뀐 뒤에 지움
        static void Destroy(Anim a) { foreach (var f in a.Frames) Kill(f); }
        static void Destroy(Background b) { foreach (var l in b.Layers) foreach (var fr in l.Frames) foreach (var t in fr) Kill(t.Tex); }
        static void Destroy(Slice s) { Kill(s.Cap); Kill(s.Body); Kill(s.Left); Kill(s.Mid); Kill(s.Right); }

        static void Remove(string key)
        {
            if (key == "bg" || key == "배경") { if (Bg != null) Destroy(Bg); Bg = null; }
            else if (key == "pillar" || key == "기둥") { if (Pillar != null) Destroy(Pillar); Pillar = null; }
            else if (key == "platform" || key == "발판") { if (Platform != null) Destroy(Platform); Platform = null; }
            else if (anims.TryGetValue(key, out var a)) { Destroy(a); anims.Remove(key); }
            Version++;
        }
    }
}
