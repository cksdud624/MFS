using System;
using System.IO;
using System.Text;
using Mori.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 게임 설정(이 PC 에만 — 화면·프레임은 기기마다 다르므로 세이브 폴더 밖 settings.json 에 둔다, 스팀 클라우드로 옮기지 않음).
    /// 웹 버전 팝업의 언어 · 알람(토스트) · 연출 설정에 PC 게임 설정(화면 모드·해상도·수직 동기화·프레임·UI 크기·자동 저장)을 더했다.
    /// </summary>
    public sealed class GameSettings
    {
        public int V = 1;
        // 게임
        public string Lang = "ko";
        public bool Toasts = true;          // 거래·레벨·은행 알림(청산·위험 알림은 끌 수 없음)
        public bool Fx = true;              // 화면 연출(살롱 문·엘리베이터·소장, 은행 입장, 레벨 업)
        public bool RunInBackground = true; // 창이 비활성이어도 계속(끄면 다른 창을 보는 동안 장이 닫힘)
        // 화면
        public int Mode = 0;                // 0 테두리 없는 전체 화면 · 1 창 모드 · 2 전용 전체 화면
        public int ResW, ResH;              // 0 = 모니터 해상도
        public bool VSync = true;
        public int Fps = 60;                // 수직 동기화를 끈 경우의 상한(0 = 제한 없음)
        public float UiScale = 1f;
        // 소리 · 리듬
        public float VolMaster = 0.5f, VolMusic = 0.5f, VolSfx = 0.5f;      // 50% = 기준 크기(예전 기본 소리) · 100% = 더 크게(AudioHub 배율)
        public int VolV = 2;                // 2 = 50% 기준 음량
        public float VolAtkMagic = 0.5f, VolAtkMech = 0.5f;
        public bool AtkSoundOnPress = true;  // 공격 소리: true = 누를 때마다(예전 연타 소리) · false = 공격이 실제로 나갈 때      // 공격 소리 따로(50% = 기준) — 효과음 음량에 한 번 더 곱함
        public int RhythmOffsetMs;          // 판정 보정(+ 면 늦게 눌러도 맞게)
        public float NoteSpeed = 1f;        // 노트 내려오는 속도 배수
        public bool RhythmOnFill = true;    // 주문이 체결되어 포지션이 생기거나 늘면 바로 리듬 시작
        public bool RhythmPickChart = true; // 리듬을 시작할 때 곡(차트) 고르기 창을 띄운다
        public bool StageGuide = true;      // 리듬 액션: 결투자 타격이 닿을 자리(위험 구역)를 미리 칠함
        public bool StageCue = true;        // 리듬 액션: 결투자 타격 1박 전 예고음
        public string RhythmChart = "";     // 지난번에 고른 차트 파일 이름(고르기를 끄면 이 곡으로)
        // 저장
        public int AutosaveSec = 60;
        public int LastSlot = -1;

        public static readonly string[] LANGS = { "ko", "en", "ja", "zh-CN", "zh-TW", "ru" };
        public static readonly string[] LANG_NAMES = { "한국어", "English", "日本語", "简体中文", "繁體中文", "Русский" };
        public static readonly float[] SCALES = { 0.8f, 0.9f, 1f, 1.1f, 1.25f, 1.5f };
        public static readonly int[] FPS = { 30, 60, 120, 144, 0 };
        public static readonly int[] AUTOSAVE = { 30, 60, 180, 300 };
        public static readonly float[] VOLS = { 0f, 0.25f, 0.5f, 0.65f, 0.8f, 1f };
        public static readonly float[] SPEEDS = { 0.8f, 1f, 1.2f, 1.5f, 1.8f, 2.2f };

        public static string FilePath => Path.Combine(KitPaths.Data, "settings.json");

        public static GameSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath, Encoding.UTF8);
                    var s = MoriJson.Deserialize<GameSettings>(json);
                    if (s != null)
                    {
                        // 예전 음량(0 ~ 1 그대로) → 50% 기준으로: 예전 기본 소리 = 50%
                        if (json.IndexOf("\"VolV\"", StringComparison.Ordinal) < 0) { s.VolMaster = s.VolMusic = s.VolSfx = 0.5f; s.VolV = 2; }
                        return s.Clamp();
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Mori] 설정 파일을 읽지 못해 기본값을 씁니다: " + e.Message); }
            return new GameSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                KitBackup.Before(FilePath);
                File.WriteAllText(FilePath, MoriJson.Serialize(this, true), Encoding.UTF8);
            }
            catch (Exception e) { Debug.LogWarning("[Mori] 설정 저장 실패: " + e.Message); }
        }

        GameSettings Clamp()
        {
            Mode = Mathf.Clamp(Mode, 0, 2);
            UiScale = Mathf.Clamp(UiScale <= 0 ? 1 : UiScale, 0.7f, 1.6f);
            if (Array.IndexOf(AUTOSAVE, AutosaveSec) < 0) AutosaveSec = 60;
            if (Fps < 0) Fps = 60;
            if (Array.IndexOf(LANGS, Lang) < 0) Lang = "ko";
            VolMaster = Mathf.Clamp01(VolMaster); VolMusic = Mathf.Clamp01(VolMusic); VolSfx = Mathf.Clamp01(VolSfx);
            VolAtkMagic = Mathf.Clamp01(VolAtkMagic); VolAtkMech = Mathf.Clamp01(VolAtkMech);
            RhythmOffsetMs = Mathf.Clamp(RhythmOffsetMs, -300, 300);
            NoteSpeed = Mathf.Clamp(NoteSpeed <= 0 ? 1 : NoteSpeed, 0.5f, 3f);
            return this;
        }

        public GameSettings Defaults()
        {
            var d = new GameSettings { LastSlot = LastSlot };
            return d;
        }

        public static FullScreenMode ModeOf(int m) => m == 1 ? FullScreenMode.Windowed : m == 2 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow;

        /// <summary>화면·프레임·UI 크기를 실제로 적용한다(에디터에서는 해상도·화면 모드가 Game 창에 따라 무시된다).</summary>
        public void Apply(PanelSettings panel)
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : (Fps <= 0 ? -1 : Fps);
            Application.runInBackground = RunInBackground;
            if (panel != null)
                panel.referenceResolution = new Vector2Int(Mathf.RoundToInt(1600 / UiScale), Mathf.RoundToInt(900 / UiScale));
#if !UNITY_EDITOR
            var mode = ModeOf(Mode);
            int w = ResW, h = ResH;
            if (w <= 0 || h <= 0)
            {
                var cur = Screen.currentResolution;
                if (mode == FullScreenMode.Windowed) { w = Mathf.Min(1600, cur.width); h = Mathf.Min(900, cur.height); }
                else { w = cur.width; h = cur.height; }
            }
            if (Screen.width != w || Screen.height != h || Screen.fullScreenMode != mode) Screen.SetResolution(w, h, mode);
#endif
        }
    }
}
