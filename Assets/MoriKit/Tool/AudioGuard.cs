using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// Unity 오디오 엔진 버그 감시 · 대처.
    /// 증상: 에디터 로그에 “Access version should be odd when acquiring lock” 이 끝없이 찍히다가(수 GB) 메모리가 바닥나 Unity 가 튕김
    ///       (Unity 이슈 트래커: audio::DualThreadManager 잠금 검사 실패 — 킷 코드가 아니라 엔진 쪽).
    /// 감지: ① 그 메시지가 로그로 들어오는 횟수  ② 에디터 로그 파일이 비정상적으로 빨리 커지는지(메시지가 C# 쪽으로 안 와도 잡히게).
    /// 대처(작업을 잃지 않는 게 최우선):
    ///   1단계 — 바로 모두 저장 → 오디오 엔진만 다시 시작(AudioSettings.Reset) → 위에 알림. 대부분 여기서 멈춤.
    ///   2단계 — 1분 안에 또 나면: 다시 저장 → 오디오 다시 시작 + 소리 끄기(AudioListener.pause) → ‘한가할 때 Unity 다시 켜기’ 알림.
    ///   계속되면 30초마다 자동 저장만 되풀이(튕겨도 잃는 건 최대 30초 · 그림은 원래 1.2초 자동 저장).
    /// </summary>
    public static class AudioGuard
    {
        const string SIG = "Access version should be odd";
        const long FLOOD_BYTES_PER_SEC = 8L * 1024 * 1024;       // 로그가 1초에 8MB 넘게 커지면 이상
        const int FLOOD_HITS_PER_SEC = 30;

        static int hits;
        static bool hooked;
        static string logPath;
        static long lastLen = -1;
        static float nextCheck, lastActAt = -1000, lastSaveAt = -1000;
        static int floodSecs, stage;
        static VisualElement root, banner;
        static Label bannerText;
        static Button unmuteBtn;
        static float bannerUntil;

        /// <summary>지금까지 대처한 횟수(작업실 표시용).</summary>
        public static int Handled { get; private set; }

        public static void Hook(VisualElement r)
        {
            root = r;
            if (!hooked) { Application.logMessageReceivedThreaded += OnLog; hooked = true; }
            logPath = FindLog();
            lastLen = LogLen();
            BuildBanner();
        }

        public static void Unhook()
        {
            if (hooked) { Application.logMessageReceivedThreaded -= OnLog; hooked = false; }
            if (AudioListener.pause && stage >= 2) AudioListener.pause = false;
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (msg != null && msg.StartsWith(SIG, StringComparison.Ordinal)) Interlocked.Increment(ref hits);
        }

        /// <summary>에디터 로그 파일: 프로젝트 Logs/Editor.log(로그가 옮겨진 경우)와 기본 위치 중 최근에 쓰인 쪽.</summary>
        static string FindLog()
        {
            string best = null;
            DateTime bestT = DateTime.MinValue;
            try
            {
                var proj = Directory.GetParent(Application.dataPath)?.FullName;
                foreach (var p in new[] { proj != null ? Path.Combine(proj, "Logs", "Editor.log") : null, Application.consoleLogPath })
                {
                    if (string.IsNullOrEmpty(p) || !File.Exists(p)) continue;
                    var t = File.GetLastWriteTimeUtc(p);
                    if (t > bestT) { bestT = t; best = p; }
                }
            }
            catch { }
            return best;
        }

        static long LogLen()
        {
            try { return logPath != null && File.Exists(logPath) ? new FileInfo(logPath).Length : -1; }
            catch { return -1; }
        }

        /// <summary>KitRoot.Update 에서 매 프레임(실제 검사는 1초에 한 번).</summary>
        public static void Tick(Action saveAll)
        {
            float now = Time.realtimeSinceStartup;
            if (banner != null && bannerUntil > 0 && now > bannerUntil) { banner.style.display = DisplayStyle.None; bannerUntil = 0; }
            if (now < nextCheck) return;
            nextCheck = now + 1f;

            int h = Interlocked.Exchange(ref hits, 0);
            long len = LogLen(), grow = lastLen >= 0 && len >= lastLen ? len - lastLen : 0;
            if (len >= 0 && len < lastLen) grow = 0;                  // 로그가 새로 시작됨
            lastLen = len;
            bool flood = h >= FLOOD_HITS_PER_SEC || grow >= FLOOD_BYTES_PER_SEC;
            floodSecs = flood ? floodSecs + 1 : 0;
            // 메시지를 직접 봤으면 1초, 로그 크기로만 알면 3초 이어질 때(큰 저장 · 가져오기로 잠깐 커지는 건 무시)
            bool trigger = h >= FLOOD_HITS_PER_SEC || floodSecs >= 3;
            if (!trigger) { if (now - lastActAt > 60) stage = Math.Min(stage, 1); return; }

            if (now - lastActAt < 4) return;                          // 방금 대처함 — 효과를 볼 시간
            if (stage >= 2 && now - lastSaveAt < 30) return;          // 이미 소리를 껐는데 계속됨 — 30초마다 저장만
            Respond(saveAll, now);
        }

        static void Respond(Action saveAll, float now)
        {
            Handled++;
            SafeSave(saveAll, now);
            if (stage >= 2)
            {
                Show("Unity 오디오 버그가 계속되고 있어요 — 작업은 방금 또 저장했어요(30초마다 자동 저장). 지금 Unity 를 다시 켜는 게 안전해요.", 0, true);
                lastActAt = now;
                return;
            }
            bool again = now - lastActAt < 60;
            ResetAudio();
            if (!again)
            {
                stage = 1;
                Show("Unity 오디오 버그를 감지해서 작업을 모두 저장하고 오디오만 다시 시작했어요. 음악이 멈췄다면 다시 틀어 주세요.", 10, false);
            }
            else
            {
                stage = 2;
                AudioListener.pause = true;
                Show("오디오 버그가 다시 났어요 — 작업은 저장했고, 튕기지 않게 소리를 껐어요. 계속 작업해도 되지만 한가할 때 Unity 를 다시 켜 주세요.", 0, true);
            }
            lastActAt = now;
        }

        static void SafeSave(Action saveAll, float now)
        {
            try { saveAll?.Invoke(); }
            catch (Exception e) { Debug.LogWarning("[Mori 오디오 지킴] 저장 실패: " + e.Message); }
            try { TuneStore.Save(out _); } catch { }
            lastSaveAt = now;
        }

        static void ResetAudio()
        {
            try
            {
                var cfg = AudioSettings.GetConfiguration();
                AudioSettings.Reset(cfg);                               // 오디오 엔진만 다시 시작(스레드 · 잠금 상태 초기화)
            }
            catch (Exception e) { Debug.LogWarning("[Mori 오디오 지킴] 오디오 다시 시작 실패: " + e.Message); }
        }

        // ───────── 알림 띠(맨 위 가운데) ─────────
        static void BuildBanner()
        {
            if (root == null || banner != null && banner.parent == root) return;
            banner = new VisualElement { pickingMode = PickingMode.Position };
            var s = banner.style;
            s.position = Position.Absolute;
            s.top = 8; s.left = Length.Percent(15); s.right = Length.Percent(15);
            s.flexDirection = FlexDirection.Row;
            s.alignItems = Align.Center;
            s.backgroundColor = new Color(0.32f, 0.07f, 0.07f, 0.96f);     // 짙은 빨강 바탕 + 흰 글씨(대비 충분)
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 2;
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = new Color(1f, 0.55f, 0.5f);
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 6;
            s.paddingLeft = s.paddingRight = 14; s.paddingTop = s.paddingBottom = 8;
            s.display = DisplayStyle.None;
            bannerText = new Label();
            bannerText.style.color = Color.white;
            bannerText.style.fontSize = 16;
            bannerText.style.whiteSpace = WhiteSpace.Normal;
            bannerText.style.flexGrow = 1; bannerText.style.flexShrink = 1;
            bannerText.style.marginRight = 10;
            banner.Add(bannerText);
            unmuteBtn = MkBtn("소리 다시 켜기", () => { AudioListener.pause = false; stage = 1; unmuteBtn.style.display = DisplayStyle.None; });
            banner.Add(unmuteBtn);
            banner.Add(MkBtn("닫기", () => banner.style.display = DisplayStyle.None));
            root.Add(banner);
        }

        static Button MkBtn(string t, Action a)
        {
            var b = new Button(a) { text = t };
            var s = b.style;
            s.minHeight = 30; s.minWidth = 64; s.flexShrink = 0;
            s.marginLeft = 6;
            s.fontSize = 15;
            s.color = Color.white;
            s.backgroundColor = new Color(0.55f, 0.15f, 0.14f);
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 1;
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = new Color(1f, 0.7f, 0.65f);
            return b;
        }

        static void Show(string msg, float secs, bool unmute)
        {
            Debug.LogWarning("[Mori 오디오 지킴] " + msg);
            if (banner == null) BuildBanner();
            if (banner == null) return;
            bannerText.text = msg;
            unmuteBtn.style.display = unmute && AudioListener.pause ? DisplayStyle.Flex : DisplayStyle.None;
            banner.style.display = DisplayStyle.Flex;
            banner.BringToFront();
            bannerUntil = secs > 0 ? Time.realtimeSinceStartup + secs : 0;
        }
    }
}
