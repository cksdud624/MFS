using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Mori.Core.Art;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 게임 안 도트 애니메이션 편집기(클립 스튜디오 · Aseprite 식 타임라인).
    ///  · 타임라인 = 가로 프레임 × 세로 레이어(헤어 · 몸통 · 무기 …). 칸 하나 = 셀(그 프레임 그 레이어의 그림).
    ///    셀은 ‘새 그림(●)’ · ‘앞 그림 유지(―, 같은 그림을 이어 씀 — 고치면 이어진 프레임 모두 바뀜)’ · ‘비움’ 중 하나.
    ///  · 프레임 길이는 프레임 단위(기준 fps, 기본 60) — ms 도 같이 보인다.
    ///  · 파일 = PSD(StreamingAssets/Fight): 프레임 하나 = 폴더 하나(이름 ‘03 · 3f’), 그 안에 레이어. 포토샵 · 클립 스튜디오와 같은 파일.
    ///  · 그리는 즉시 결투 · 작업실 미리보기의 캐릭터에 반영(ArtLib.SetLive). 저장 Ctrl+S(닫을 때 자동).
    /// 결투 화면 F3(왼쪽에 붙음) · 타이틀 ‘작업실’(화면 전체).
    /// </summary>
    public sealed class SpriteEditor
    {
        public readonly VisualElement Root;
        public bool Open { get; private set; }
        public bool Freeze = true;
        public bool Pin = true;
        /// <summary>지금 고른 색.</summary>
        public Color32 PickColor => colors.Main;
        public string Key => key;
        public Action OnClosed;
        /// <summary>편집기를 누름(작업실: 미리보기에서 제어권을 가져옴).</summary>
        public Action OnFocus;

        internal static readonly string[] STATES =
        {
            "hero-idle", "hero-run", "hero-jump", "hero-fall", "hero-dash", "hero-attack1", "hero-attack2", "hero-attack3", "hero-counter", "hero-counter-hit", "hero-counter-crash", "hero-ult", "hero-hurt", "hero-down",
            "hero-doublejump", "hero-air", "hero-ready", "hero-ready-attack1", "hero-ready-attack2", "hero-ready-attack3", "hero-ready-end", "hero-idle-long", "hero-land", "hero-stop", "hero-start",
            "foe-idle", "foe-walk", "foe-walk-back", "foe-dash", "foe-jump", "foe-turn", "foe-windup", "foe-attack", "foe-charge", "foe-air", "foe-stance", "foe-stun", "foe-ko",
            "foe-ready", "foe-ready-end", "foe-idle-long", "foe-land", "foe-stop", "foe-start",
            "ally-idle", "ally-walk", "ally-walk-back", "ally-run", "ally-jump", "ally-fly", "ally-shoot", "ally-shoot-strong", "ally-turn", "ally-ult", "ally-cutin",
        };

        /// <summary>흐름 그림(준비 · 긴 대기 · 연결)은 한 번만 재생이 기본.</summary>
        internal static bool FlowOnce(string k) =>
            k.Contains("-ready") || k.Contains("-idle-long") || k.EndsWith("-land") || k.EndsWith("-stop") || k.EndsWith("-start") || k == "hero-doublejump" || k == "hero-counter-hit" || k == "hero-counter-crash" || k == "ally-shoot" || k == "ally-shoot-strong"
            || FlowTable.Rows.Any(r => r.Anim == k);

        /// <summary>흐름 그림 프리셋 설명(파일 목록 툴팁).</summary>
        static string FlowTip(string k) =>
            k == "hero-air" ? "공중 몸통 — ‘오름 → 정점 → 낙하’ 순서로 그리면 세로 속도로 프레임을 골라요(시간으로 재생 안 함). 그리면 점프 · 더블 점프는 도약 모션으로만 쓰여요"
            : k == "hero-jump" ? "점프 — hero-air 를 그렸으면 땅에서 박차는 짧은 도약(1회), 안 그렸으면 예전처럼 올라가는 동안"
            : k == "hero-counter" ? "카운터 — 누른 순간의 한 번 휘두름. 그 뒤 배리어 동안은 달리기 · 공격 등 실제 행동 그림(배리어는 둘레 효과)"
            : k == "hero-counter-hit" ? "카운터 성공 — 배리어 안에서 받아쳤을 때 1회, 끝까지 재생(맞음 · 다운만 끊음)"
            : k == "hero-counter-crash" ? "카운터 크래시 — 결투자 카운터 자세를 깼을 때 1회, 끝까지 재생 · 성공보다 우선"
            : k == "hero-ult" ? "궁극기 — 궤도 폭격을 요청하는 자세(시전 7초 동안 고정 · 반복). 없으면 대기 그림"
            : k == "ally-idle" ? "동료(여주인공) 서 있기 — 반복. 플레이어처럼 발 기준 · 발밑 선 적용. 없으면 코드 사람 그림"
            : k == "ally-walk" ? "동료 걷기 — 자리가 가까울 때(속도에 맞춰 빠르기가 바뀜) · 없으면 ally-idle"
            : k == "ally-run" ? "동료 뛰기 — 엄호 자리를 멀리 옮길 때(돌아서서 뜀) · 없으면 ally-walk"
            : k == "ally-walk-back" ? "동료 뒷걸음 — 적을 보면서 적 반대쪽으로 물러날 때(없으면 ally-walk 를 거꾸로)"
            : k == "ally-fly" ? "동료 드론 비행 — 작은 드론에 매달려 날아가 자리를 다시 잡는 동안(반복). 드론까지 같이 그리면 코드 드론은 안 나옴 · 없으면 ally-jump + 코드 드론"
            : k == "ally-jump" ? "동료 공중 — 기둥을 뛰어넘거나 발판으로 올라가는 · 떨어지는 동안(없으면 걷기 · 서 있기)"
            : k == "ally-shoot" ? "동료 사격 — 기계 공격 순간 1회, 플레이어 뒤에 서서 결투자 쪽으로 엄호(없으면 ally-idle)"
            : k == "ally-shoot-strong" ? "동료 강한 사격 — 3타 · 카운터 성공 · 크래시 때 뒤에서 버티고 서서 굵은 한 방(반동으로 살짝 밀림) · 없으면 ally-shoot"
            : k == "ally-turn" ? "동료 돌아서기 — 바라보는 쪽이 바뀔 때 짧게(F2 › 동료 › 돌아서기 시간) · 없으면 바로 뒤집힘"
            : k == "ally-ult" ? "동료 궁극기 — 위성 레이저를 부르는 자세(플레이어 궁극기 위치 고정 동안 반복) · 없으면 ally-shoot"
            : k == "ally-cutin" ? "동료 컷인 — 여주인공 얼굴 · 상반신. 카운터 크래시 · 궁극기 시작 때 화면 띠 위에(없으면 글자 띠)"
            : k == "foe-dash" ? "결투자 대쉬 · 백대쉬(자연 이동) — 없으면 걷기"
            : k == "foe-jump" ? "결투자 점프 오름 — 없으면 foe-air"
            : k == "foe-turn" ? "결투자 돌아서기 — 시선이 바뀔 때 짧게(F2 › 결투자 › 시선 › 돌아서기 그림 시간) · 없으면 바로 뒤집힘"
            : k == "foe-walk-back" ? "결투자 뒷걸음 — 플레이어를 보면서 멀어질 때(없으면 foe-walk 를 거꾸로 재생)"
            : k == "hero-fall" ? "낙하 — hero-air 를 그렸으면 오래 떨어질 때만 도는 긴 낙하 루프(펄럭임 2~4프레임), 안 그렸으면 예전처럼 떨어지는 동안"
            : k == "hero-doublejump" ? "더블 점프 도약(1회) — 낙하가 시작돼도 끝까지 재생한 뒤 공중 몸통으로. 없으면 hero-jump"
            : k.EndsWith("-ready-end") ? "연결: 준비 → 대기 — 준비 루프가 끝나면 대기(-idle)로 자연스럽게 넘어가는 동작(1회, 입력 오면 즉시 끊김). 연결 표의 ‘-ready > -idle’ 줄로 준비별 그림도 지정 가능"
            : k.StartsWith("hero-ready-attack") ? "공격 마무리 — 휘두른 뒤 무기를 거두는 여운(길이 제한 없음). 다음 공격 · 이동을 누르면 즉시 끊김. 타별(-attack1 ~ 3)이 없으면 hero-ready-attack → hero-ready"
            : k.EndsWith("-ready") ? "준비 — 행동이 끝나고 입력이 없을 때 정한 시간 동안 루프(F2 › 캐릭터 흐름 › 준비 시간), 그 뒤 -ready-end 로 대기에. 입력 오면 즉시 끊김. -ready-attack 처럼 이전 행동별로도 그릴 수 있어요"
            : k.EndsWith("-idle-long") ? "긴 대기 — 대기가 오래가면 한 번 재생(-idle-long2 … 로 여러 개)"
            : k.EndsWith("-land") ? "연결: 착지(공중 → 땅)"
            : k.EndsWith("-stop") ? "연결: 멈춤(이동 → 대기)"
            : k.EndsWith("-start") ? "연결: 출발(대기 → 이동)" : null;

        /// <summary>파일 목록에 보일 모든 이름(기본 상태 + 자유 프리셋 + 연결 표의 그림).</summary>
        internal static IEnumerable<string> KnownNames() =>
            STATES.Concat(LoadCustom()).Concat(FlowTable.Rows.Select(r => r.Anim)).Concat(ArtLib.Files().Select(f => f.key)).Distinct();

        enum Tool { Pen, Eraser, Picker, Fill, Line, Rect, SelRect, Lasso, Poly, Hand, LassoFill, LassoErase, Blend }

        // ───────── 문서 모델 ─────────
        sealed class Row
        {
            public string Name = "레이어";
            public bool Visible = true, Locked, Clip;
            public double Opacity = 1;
            public string Blend = "norm";
            public Row Clone() => (Row)MemberwiseClone();
        }
        sealed class Cel { public TileImage Img; }
        sealed class Frame { public int Dur = 6; public List<Cel> Cels = new List<Cel>(); }

        string key, path;
        bool once, readOnly, dirty;
        int W = 64, H = 64, fps = 60;
        /// <summary>발밑 선: 캔버스 맨 아래에서 몇 px 위가 바닥인지(그 아래 = 바닥을 뚫는 자리). 파일 이름 ‘발밑N’ 으로 저장.</summary>
        int foot;
        /// <summary>캔버스 기준 = 가운데(이펙트용 그림). 파일 이름 ‘가운데’ 로 저장. 아니면 발밑(캐릭터) 기준.</summary>
        bool centerPivot;
        Button pivotBtn;
        IntegerField footField;
        List<Row> rows = new List<Row>();              // 아래 → 위
        List<Frame> frames = new List<Frame>();
        int fi, ri;                                     // 고른 프레임 · 레이어
        readonly List<Texture2D> frameTex = new List<Texture2D>();

        Frame F => frames[Mathf.Clamp(fi, 0, frames.Count - 1)];
        Row R => rows.Count == 0 ? null : rows[Mathf.Clamp(ri, 0, rows.Count - 1)];
        Cel CurCel => frames.Count == 0 || rows.Count == 0 ? null : F.Cels[Mathf.Clamp(ri, 0, rows.Count - 1)];
        float MsOf(int f) => frames[f].Dur * 1000f / fps;

        // ───────── 도구 상태 ─────────
        Tool tool = Tool.Pen;
        int brush = 1;
        Button toneModeBtn, toneAmtBtn, toneHueBtn, toneSnapBtn;
        // 어니언 설정(기억함)
        int onionPrev = Mathf.Clamp(PlayerPrefs.GetInt("mori.editor.onionPrev", 1), 0, 3), onionNext = Mathf.Clamp(PlayerPrefs.GetInt("mori.editor.onionNext", 1), 0, 3);
        int onionStyle = Mathf.Clamp(PlayerPrefs.GetInt("mori.editor.onionStyle", 0), 0, 4), onionAlpha = Mathf.Clamp(PlayerPrefs.GetInt("mori.editor.onionAlpha", 70), 10, 100);
        bool onionOver = PlayerPrefs.GetInt("mori.editor.onionOver", 1) == 1;
        static readonly string[] ONION_STYLE = { "윤곽선", "실루엣", "단색 명암", "예전 합성", "명암 경계선" };
        static readonly int[] ONION_STYLE_ORDER = { 0, 4, 1, 2, 3 };   // 버튼 · 단축키로 넘기는 순서(윤곽선 → 명암 경계선 → 실루엣 → 단색 명암 → 예전 합성)
        const float SHADE_STEP = 0.06f;                                  // 명암 경계선: 옆 픽셀과 밝기가 이만큼(0 ~ 1) 넘게 다르면 선
        static readonly Color ONION_PREV = new Color(1f, 0.30f, 0.38f), ONION_NEXT = new Color(0.25f, 0.62f, 1f);   // 기본 색
        Color onionPrevCol = LoadCol("mori.editor.onionPrevCol", ONION_PREV), onionNextCol = LoadCol("mori.editor.onionNextCol", ONION_NEXT);
        VisualElement onionPrevSw, onionNextSw, onionPick;
        int onionPickSide;                  // 0 = 닫힘 · 1 = 이전 색 고르는 중 · 2 = 다음 색
        // 고르기 쉬운 진한 색(체크무늬 · 어두운 화면에서 잘 보이는 것)
        static readonly (string name, Color c)[] ONION_COLORS =
        {
            ("빨강", new Color(1f, 0.30f, 0.38f)), ("주황", new Color(1f, 0.55f, 0.15f)), ("노랑", new Color(1f, 0.88f, 0.2f)), ("연두", new Color(0.6f, 1f, 0.25f)),
            ("초록", new Color(0.2f, 0.85f, 0.45f)), ("청록", new Color(0.2f, 0.95f, 0.95f)), ("파랑", new Color(0.25f, 0.62f, 1f)), ("남보라", new Color(0.5f, 0.45f, 1f)),
            ("보라", new Color(0.8f, 0.4f, 1f)), ("분홍", new Color(1f, 0.4f, 0.8f)), ("흰색", Color.white),
        };

        static Color LoadCol(string key, Color def)
        {
            var hex = PlayerPrefs.GetString(key, "");
            return hex.Length > 0 && ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : def;
        }
        static readonly Color ONION_OLD_PREV = new Color(1f, 0.4f, 0.4f), ONION_OLD_NEXT = new Color(0.4f, 1f, 0.55f);   // 예전 합성(빨강 · 초록)
        IntegerField onionPrevField, onionAlphaField;
        Button onionStyleBtn, onionPosBtn, onionSoloPrevBtn, onionSoloNextBtn, onionHoldBtn;
        int onionSolo;                      // 0 = 둘 다 · 1 = 이전만 · 2 = 다음만
        // 어니언 보기 키(3 이전 · 4 다음 · 5 전부): 누르고 보기 = 누르는 동안만 보이고 떼면 원래대로 · 계속 보기 = 누를 때마다 켜고 끔(6 으로 바꿈)
        bool onionHold = PlayerPrefs.GetInt("mori.editor.onionHold", 1) == 1;
        KeyCode onionHoldKey = KeyCode.None;
        bool holdSavedOn; int holdSavedSolo;
        readonly List<Texture2D> silTex = new List<Texture2D>();       // 어니언용(윤곽선 · 실루엣 — 흰색, 색은 칠할 때 입힘)
        readonly List<bool> silStale = new List<bool>();
        byte[] silBuf = new byte[0];
        bool mirror, grid = PlayerPrefs.GetInt("mori.editor.grid", 1) == 1, onion = true, play, loop = true, spaceHeld, spaceUsed;
        float zoom = 8, playMs;
        // 작업 창 배경: 0 어두운 체크 · 1 밝은 체크 · 2 흰색 · 3 검정
        int bgMode = Mathf.Clamp(PlayerPrefs.GetInt("mori.editor.bg", 0), 0, 3);
        static readonly string[] BG = { "어두운 체크", "밝은 체크", "흰색", "검정" };
        Button gridBtn2, bgBtn;
        Vector2 pan;

        // ───────── 실행 취소 ─────────
        sealed class Snap
        {
            public List<Row> Rows;
            public List<(int dur, List<Cel> cels)> Frames;
            public int Fi, Ri;
            public Cel PxCel;
            public byte[][] Tiles;
            public List<(Cel cel, byte[][] tiles)> Many;     // 여러 칸을 한 번에(이동 · 정렬)
        }
        readonly List<Snap> undo = new List<Snap>(), redo = new List<Snap>();
        const int UNDO_MAX = 80;

        // ───────── 화면 ─────────
        readonly VisualElement view, canvasHost, checker, img, gridEl, overlay, toolCol, timeline, fileList, helpBox, rowProps;
        readonly List<VisualElement> onionEls = new List<VisualElement>();
        readonly Label title, status, info, timeInfo, brushL;
        readonly TextField keyField, rowName;
        readonly IntegerField durField, fpsField, wField, hField, rwField, rhField, rowOp, onionField;
        readonly Dictionary<Tool, Button> toolBtns = new Dictionary<Tool, Button>();
        readonly Button mirrorBtn, gridBtn, onionBtn, playBtn, loopBtn, freezeBtn, pinBtn, clipBtn, visBtn, lockBtn;
        readonly ColorPanel colors;
        readonly VisualElement importList;
        IntegerField cellW, cellH;
        Button fitBtn, lockCanvasBtn, lockCanvasBtn2;
        // 캔버스 크기 잠금(기본 켬): 가져오기가 캔버스를 키우지 않음 · ‘바꾸기’ 도 막음. 맞추기: 0 그대로 · 1 알맞게(칸이 크면 줄임) · 2 꽉 차게(늘리기도)
        bool canvasLock = PlayerPrefs.GetInt("mori.editor.canvasLock", 1) == 1;
        int fitMode = Mathf.Clamp(PlayerPrefs.GetInt("mori.editor.fitMode", 1), 0, 2);
        static readonly string[] FIT = { "그대로", "알맞게", "꽉 차게" };
        Button alignBtn, alignBtn2;
        VisualElement selEl;
        static readonly string[] ALIGN = { "원본", "발밑", "몸통" };
        SpriteSheet.Align alignMode = SpriteSheet.Align.Body;

        public SpriteEditor(VisualElement parent, bool full)
        {
            Root = new VisualElement { focusable = true };
            Root.AddToClassList("ae");
            Root.AddToClassList(full ? "ae--full" : "ae--dock");
            parent.Add(Root);
            Root.style.display = DisplayStyle.None;
            Root.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            Root.RegisterCallback<KeyUpEvent>(OnKeyUp, TrickleDown.TrickleDown);
            // 누르면 키를 편집기로(단축키용) — 단, 글 입력 칸을 누른 거면 그 칸이 키를 받게 그대로 둠
            Root.RegisterCallback<PointerDownEvent>(e => { OnFocus?.Invoke(); if (!IsTypingEl(e.target as VisualElement)) Root.Focus(); }, TrickleDown.TrickleDown);
            // 방향키가 버튼 사이로 포커스를 옮기지 않게(옮기는 중 멈춤 방지)
            Root.RegisterCallback<NavigationMoveEvent>(e => { if (Open) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            // 스페이스 · Enter 가 포커스된 편집기 버튼(재생 등)을 누르지 않게 — 버튼은 마우스로만
            Root.RegisterCallback<NavigationSubmitEvent>(e => { if (Open) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            // 포커스가 편집기 밖(또는 아무 데도 없음)이어도 단축키가 먹게 — 화면 전체에서 듣는다
            Root.RegisterCallback<AttachToPanelEvent>(e => HookPanel(e.destinationPanel));
            Root.RegisterCallback<DetachFromPanelEvent>(_ => UnhookPanel());

            // ── 머리
            var hd = El(Root, "ae-hd");
            title = Lbl(hd, "도트 편집기", "ae-title");
            gridBtn2 = Btn(hd, "격자 켬", ToggleGrid, "픽셀 격자 켜기 · 끄기(확대했을 때 보이는 칸 선)");
            bgBtn = Btn(hd, "배경: 어두운 체크", CycleBg, "작업 창 배경 바꾸기 — 어두운 체크 · 밝은 체크 · 흰색 · 검정");
            Btn(hd, "도움말 ?", () => helpBox.style.display = helpBox.resolvedStyle.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None, "단축키 · 작업 순서");
            Btn(hd, "단축키", ToggleKeys, "단축키 바꾸기 · 초기값 · 프리셋");
            Btn(hd, "내보내기", ToggleExport, "한 장짜리 스프라이트 시트(PNG + json) · 프레임별 PNG 로 내보내기 — PSD 저장과 따로");
            Btn(hd, "저장", OpenSaveDlg, "작업 원본(PSD) 저장 — 창에서 미리 보고 저장 · 저장 폴더 열기(Ctrl+S = 창 없이 바로 저장)", "ae-btn--main");
            if (!full) Btn(hd, "닫기 (F3)", Hide, "닫기(자동 저장)");
            helpBox = El(Root, "ae-help");
            helpBox.style.display = DisplayStyle.None;
            foreach (var line in HELP) Lbl(helpBox, line, "ae-help-l");

            var body = El(Root, "ae-body");

            // ── 왼쪽 세로 도구
            var toolSv = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            toolSv.AddToClassList("ae-tools");
            body.Add(toolSv);
            toolCol = toolSv;                              // 도구가 많아 세로가 모자라면 휠로 내림
            foreach (var (t, n, tip) in new[] {
                (Tool.Pen, "펜", "펜 (B) · Alt 누른 채 클릭 = 스포이드 · S/D = 도구 위아래"), (Tool.Eraser, "지우개", "지우개 (E)"), (Tool.Picker, "스포이드", "스포이드 (I)"),
                (Tool.Fill, "채우기", "채우기 (G)"),
                (Tool.Blend, "톤 잇기", "톤 잇기 (Shift+B) · 두 톤의 경계를 문지르면 중간색(또는 디더)으로 이어 줌 — 흐리게가 아니라 또렷한 픽셀로 · 브러시 안에서 가장 많은 두 색을 알아서 찾음"),
                (Tool.LassoFill, "올가미 칠", "올가미 칠하기 (Shift+A) · 끌어서 그린 모양을 지금 색으로 채움 · Alt 누른 채 클릭 = 스포이드"),
                (Tool.LassoErase, "올가미 지움", "올가미 지우기 (Shift+E) · 끌어서 그린 모양 안을 지움"),
                (Tool.Line, "직선", "직선 (L)"), (Tool.Rect, "사각", "사각 (U) · Shift = 채운 사각"),
                (Tool.SelRect, "사각선택", "사각 선택 (R) · Shift = 더하기 · Alt = 빼기 · 그냥 누르면 해제"),
                (Tool.Lasso, "올가미", "올가미 (A) · 끌어서 그린 모양대로 선택"),
                (Tool.Poly, "꺾은선", "꺾은선 선택 (Q) · 누를 때마다 꼭짓점 · 처음 점 · 두 번 누르기 · Enter = 닫기 · Backspace = 점 하나 빼기"),
                (Tool.Hand, "손", "손 (H) · Space+드래그 · 오른쪽 드래그") })
            {
                var tt = t;
                toolBtns[t] = Btn(toolCol, n, () => SetTool(tt), tip, "ae-tool");
            }
            Btn(toolCol, "변형", BeginTransform, "자유 변형 (Ctrl+T) — 선택한 그림(없으면 고른 칸 전체) · 안 = 옮기기 · 모서리 = 크기(Shift = 자유) · 변 = 한쪽 · Alt = 가운데 기준 · Enter 확정 · Esc 취소", "ae-tool");
            Lbl(toolCol, "굵기", "ae-k");
            var br = El(toolCol, "ae-tool-pair");
            Btn(br, "−", () => { brush = Math.Max(1, brush - 1); Refresh(); }, "굵기 줄이기 (Z)");
            Btn(br, "+", () => { brush = Math.Min(16, brush + 1); Refresh(); }, "굵기 늘리기 (X)");
            brushL = Lbl(toolCol, "1px", "ae-k");
            // 톤 잇기 설정
            Lbl(toolCol, "톤 잇기", "ae-k");
            toneModeBtn = Btn(toolCol, "", () => { ToneMix.Mode = ToneMix.Mode + 1; ToneUi(); }, "중간색(계단) → 디더(점 섞기) → 명도 잇기(색은 그대로 · 밝기만 넓게)", "ae-tool");
            toneAmtBtn = Btn(toolCol, "", () =>
            {
                if (ToneMix.Mode == 0) ToneMix.Steps = ToneMix.Steps % 3 + 1;
                else if (ToneMix.Mode == 1) ToneMix.Dither = ToneMix.Dither == 25 ? 50 : ToneMix.Dither == 50 ? 75 : 25;
                else ToneMix.LumLevels = ToneMix.LumLevels >= 5 ? 3 : ToneMix.LumLevels + 1;
                ToneUi();
            }, "중간색 단계(1 · 2 · 3) · 디더 농도(25 · 50 · 75%) · 명도 단계(3 · 4 · 5)", "ae-tool");
            toneHueBtn = Btn(toolCol, "", () =>
            {
                if (ToneMix.Mode == 2) ToneMix.LumDither = !ToneMix.LumDither;
                else ToneMix.HueShift = ToneMix.HueShift > 0 ? 0 : 12;
                ToneUi();
            }, "중간색 · 디더: 색조 이동(어두운 쪽 차갑게 · 밝은 쪽 따뜻하게) / 명도 잇기: 단계 사이 4×4 점 섞기", "ae-tool");
            toneSnapBtn = Btn(toolCol, "", () =>
            {
                if (ToneMix.Mode == 2) ToneMix.LumChroma = !ToneMix.LumChroma;
                else ToneMix.Snap = !ToneMix.Snap;
                ToneUi();
            }, "중간색 · 디더: 팔레트에 맞추기 / 명도 잇기: 중간 톤 채도 살리기", "ae-tool");
            ToneUi();
            mirrorBtn = Btn(toolCol, "대칭", () => { mirror = !mirror; Refresh(); }, "좌우 대칭 (M)", "ae-tool");
            gridBtn = Btn(toolCol, "격자", ToggleGrid, "픽셀 격자 켜기 · 끄기", "ae-tool");
            pickPenBtn = Btn(toolCol, "찍고→펜", () => { pickToPen = !pickToPen; PlayerPrefs.SetInt("mori.editor.pickToPen", pickToPen ? 1 : 0); Refresh(); },
                "켜면: 스포이드로 색을 찍거나 색을 고르면 펜으로 바뀜 · 끄면(기본): 쓰던 도구 그대로", "ae-tool");
            var zm = El(toolCol, "ae-tool-pair");
            Btn(zm, "−", () => Zoom(-1, null), "축소 (Ctrl+− · 휠)");
            Btn(zm, "+", () => Zoom(1, null), "확대 (Ctrl+= · 휠)");
            Btn(toolCol, "맞춤", () => { FitZoom(); Layout(); }, "화면에 맞추기", "ae-tool");
            Btn(toolCol, "되돌리기", Undo, "Ctrl+Z", "ae-tool");
            Btn(toolCol, "다시", Redo, "Ctrl+Y · Ctrl+Shift+Z", "ae-tool");

            // ── 가운데: 캔버스 + 타임라인
            var main = El(body, "ae-main");
            view = El(main, "ae-view");
            view.RegisterCallback<WheelEvent>(e => { Zoom(e.delta.y < 0 ? 1 : -1, e.localMousePosition); e.StopPropagation(); });
            view.RegisterCallback<PointerDownEvent>(OnDown);
            view.RegisterCallback<PointerMoveEvent>(OnMove);
            view.RegisterCallback<PointerUpEvent>(OnUp);
            view.RegisterCallback<GeometryChangedEvent>(_ => { if (Open) Layout(); });
            canvasHost = new VisualElement { pickingMode = PickingMode.Ignore };
            canvasHost.style.position = Position.Absolute;
            view.Add(canvasHost);
            checker = Abs(canvasHost);
            checker.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
            checker.style.backgroundSize = new BackgroundSize(new Length(128, LengthUnit.Pixel), new Length(128, LengthUnit.Pixel));
            checker.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left);
            checker.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
            ApplyBg();
            for (int i = 0; i < 6; i++) onionEls.Add(Abs(canvasHost));
            rigBackEl = Abs(canvasHost);
            img = Abs(canvasHost);
            rigFrontEl = Abs(canvasHost);
            foreach (var v in new[] { rigBackEl, rigFrontEl }) v.pickingMode = PickingMode.Ignore;      // 빈 칸 — 어니언을 그림 앞 · 뒤에 놓는 기준점
            ApplyOnionOrder();
            gridEl = Abs(canvasHost); gridEl.generateVisualContent += DrawGrid;
            selEl = Abs(canvasHost); selEl.generateVisualContent += DrawSel;
            overlay = Abs(canvasHost); overlay.generateVisualContent += DrawOverlay;
            foreach (var v in new[] { checker, img, gridEl, selEl, overlay }.Concat(onionEls)) v.pickingMode = PickingMode.Ignore;
            info = Lbl(main, "", "ae-note");
            BuildXfBar(main);

            // 타임라인 조작
            var pc = El(main, "ae-wrap");
            Btn(pc, "|◀", () => Go(0), "첫 프레임");
            Btn(pc, "◀", () => Go(fi - 1), "이전 프레임 ( , )");
            playBtn = Btn(pc, "▶ 재생", TogglePlay, "재생 · 정지 (Space)");
            Btn(pc, "▶", () => Go(fi + 1), "다음 프레임 ( . )");
            Btn(pc, "▶|", () => Go(frames.Count - 1), "마지막 프레임");
            loopBtn = Btn(pc, "반복", () => { loop = !loop; once = !loop; dirty = true; PushLive(); Refresh(); }, "반복 재생 / 한 번만(마지막에서 멈춤) — 게임에도 그대로");
            Lbl(pc, "길이", "ae-k");
            durField = Int(pc, 6);
            durField.tooltip = "이 프레임 길이(프레임 수)";
            durField.RegisterValueChangedCallback(e => { int v = e.newValue; SetDur(_ => v); });          // 고른 프레임 모두
            Lbl(pc, "f", "ae-k");
            Lbl(pc, "기준", "ae-k");
            fpsField = Int(pc, 60);
            fpsField.tooltip = "기준 fps — 1f 의 길이(60 = 1f ≈ 17ms)";
            fpsField.RegisterValueChangedCallback(e => { fps = Mathf.Clamp(e.newValue, 1, 240); dirty = true; PushLive(); RefreshTime(); RebuildTimeline(); });
            Lbl(pc, "fps", "ae-k");
            timeInfo = Lbl(pc, "", "ae-time");

            var fc = El(main, "ae-wrap");
            Lbl(fc, "프레임", "ae-k");
            Btn(fc, "+ 프레임", () => AddFrame(frames.Count - 1, false), "끝에 빈 프레임 추가");
            Btn(fc, "삽입", () => AddFrame(fi, false), "지금 프레임 뒤에 빈 프레임");
            Btn(fc, "복제", DupFrames, "고른 프레임을 복사해 뒤에");
            Btn(fc, "삭제", DelFrame, "고른 프레임 지우기");
            Btn(fc, "◀ 옮김", () => MoveFrame(-1), "고른 프레임을 앞으로");
            Btn(fc, "옮김 ▶", () => MoveFrame(1), "고른 프레임을 뒤로");
            Lbl(fc, "셀", "ae-k");
            Btn(fc, "새 그림", CelNew, "이 칸을 빈 새 그림으로(유지 연결 끊고 비움)");
            Btn(fc, "앞 그림 유지", CelHold, "앞 프레임 그림을 이어 씀(―) — 고치면 이어진 프레임 모두 바뀜");
            Btn(fc, "연결 끊기", CelUnlink, "이어 쓰던 그림을 이 프레임만의 복사본으로");
            Btn(fc, "비우기", CelClear, "이 칸 비우기");

            var lc = El(main, "ae-wrap");
            Lbl(lc, "레이어", "ae-k");
            Btn(lc, "+ 레이어", AddRow, "고른 레이어 위에 새 레이어(모든 프레임)");
            Btn(lc, "삭제", DelRow, "고른 레이어 지우기(모든 프레임) — 여러 개는 타임라인에서 Ctrl · Shift+누르기");
            Btn(lc, "▲", () => MoveRow(1), "위로(앞으로)");
            Btn(lc, "▼", () => MoveRow(-1), "아래로(뒤로)");
            Btn(lc, "모든 프레임 같은 그림", CelShareAll, "이 레이어를 모든 프레임에서 지금 그림으로(그림자 · 고정 부위)");
            Btn(lc, "레이어 → 프레임 나누기", SplitLayersToFrames, "프레임이 하나일 때: 레이어 하나하나를 프레임으로");
            alignBtn2 = Btn(lc, "몸통 정렬", AlignLayer, "이 레이어의 모든 프레임을 자동 정렬(지금 프레임 기준) — 방식(발밑 · 몸통)은 가져오기 칸의 ‘정렬:’ 버튼으로 고름 · 되돌리기 가능");
            // 어니언(이전 · 다음 프레임 비쳐 보기) — 이전 = 빨강 · 다음 = 파랑, 멀수록 옅게
            var oc = El(main, "ae-wrap");
            Lbl(oc, "어니언", "ae-k");
            onionBtn = Btn(oc, "켬", () => { onion = !onion; OnionChanged(); }, "이전 프레임(빨강) · 다음 프레임(파랑) 비쳐 보기");
            onionPrevSw = Swatch(oc, 1); Lbl(oc, "이전", "ae-k"); onionPrevField = Int(oc, onionPrev);
            onionPrevField.tooltip = "이전 프레임 몇 장(0 ~ 3) — 빨강";
            onionPrevField.RegisterValueChangedCallback(e => { onionPrev = Mathf.Clamp(e.newValue, 0, 3); OnionChanged(); });
            onionNextSw = Swatch(oc, 2); Lbl(oc, "다음", "ae-k"); onionField = Int(oc, onionNext);
            onionField.tooltip = "다음 프레임 몇 장(0 ~ 3) — 파랑";
            onionField.RegisterValueChangedCallback(e => { onionNext = Mathf.Clamp(e.newValue, 0, 3); OnionChanged(); });
            onionStyleBtn = Btn(oc, "윤곽선", NextOnionStyle,
                "보기 방식 — 윤곽선(테두리만 · 지금 그림을 거의 안 가림) · 명암 경계선(테두리 + 밝고 어두운 면이 나뉘는 곳의 선) · 실루엣(한 색으로 채움) · 단색 명암(원래 색 대신 빨강 · 파랑 한 색의 밝고 어두움 — 형태 · 명암은 보이고 지금 그림과 안 섞임) · 예전 합성(원래 색에 빨강 · 초록, 지금 그림 아래 — 처음 고르면 ‘아래에’ 로)");
            onionSoloPrevBtn = Btn(oc, "이전만", () => { onionSolo = onionSolo == 1 ? 0 : 1; OnionChanged(); }, "이전 프레임(빨강)만 보기 — 다시 누르면 둘 다");
            onionSoloNextBtn = Btn(oc, "다음만", () => { onionSolo = onionSolo == 2 ? 0 : 2; OnionChanged(); }, "다음 프레임(파랑)만 보기 — 다시 누르면 둘 다");
            onionHoldBtn = Btn(oc, "누르고 보기", ToggleOnionHold,
                "어니언 보기 키(기본 3 이전 · 4 다음 · 5 전부)의 방식 — 누르고 보기 = 누르는 동안만 보이고 떼면 원래대로 · 계속 보기 = 누를 때마다 켜고 끔. 단축키 기본 6");
            onionPosBtn = Btn(oc, "위에", () => { onionOver = !onionOver; OnionChanged(); }, "위에 = 지금 그림 위에 겹쳐(칠해도 안 가려짐) · 아래에 = 지금 그림 밑에 깔림");
            Lbl(oc, "진하기 %", "ae-k"); onionAlphaField = Int(oc, onionAlpha);
            onionAlphaField.tooltip = "가장 가까운 프레임의 진하기(10 ~ 100) — 멀수록 옅어져요";
            onionAlphaField.RegisterValueChangedCallback(e => { onionAlpha = Mathf.Clamp(e.newValue, 10, 100); OnionChanged(); });
            // 색 고르기 줄(색 견본을 누르면 열림)
            onionPick = El(main, "ae-wrap");
            onionPick.style.display = DisplayStyle.None;
            BuildOnionPick();

            tlScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Auto,
                verticalScrollerVisibility = ScrollerVisibility.Auto,
            };
            tlScroll.AddToClassList("ae-tl-sv");
            main.Add(tlScroll);
            timeline = new VisualElement();
            timeline.AddToClassList("ae-tl");
            tlScroll.Add(timeline);
            HookTimelinePan();

            var opt = El(main, "ae-wrap");
            pinBtn = Btn(opt, "캐릭터가 이 동작 고정", () => { Pin = !Pin; Refresh(); }, "결투 · 미리보기 캐릭터가 이 동작을 계속 재생");
            if (!full) freezeBtn = Btn(opt, "결투 멈춤", () => { Freeze = !Freeze; Refresh(); }, "편집하는 동안 결투를 멈춤");
            Btn(opt, "판정 보기", () => StageField.ShowBoxes = !StageField.ShowBoxes, "몸판정 · 공격 판정 겹쳐 보기");

            // ── 오른쪽: 색 · 레이어 속성 · 캔버스 · 파일
            var side = new ScrollView(ScrollViewMode.Vertical);
            side.AddToClassList("ae-side");
            body.Insert(1, side);                       // 도구 오른쪽 · 캔버스 왼쪽(클립 스튜디오처럼 색은 왼쪽)
            colors = new ColorPanel(side);
            // 색을 바꾸면 펜으로 — ‘찍고→펜’ 을 켰을 때만(기본 끔: 고른 도구 그대로)
            ToneUi();                                   // 색 패널이 생긴 뒤 색 계단 색조 이동을 맞춤
            colors.Changed += () => { if (pickToPen && (tool == Tool.Eraser || tool == Tool.Picker || tool == Tool.Hand || tool == Tool.LassoErase || IsSelTool(tool))) SetTool(Tool.Pen); };

            Sec(side, "레이어 속성", "고른 레이어(모든 프레임에 적용)");
            rowProps = El(side, "ae-col");
            var rn = El(rowProps, "ae-row");
            rowName = new TextField(); rowName.AddToClassList("ae-text"); rn.Add(rowName);
            rowName.RegisterCallback<FocusOutEvent>(_ => { if (R != null && !readOnly && rowName.value != R.Name && rowName.value.Trim().Length > 0) { SnapStruct(); R.Name = UniqueRowName(rowName.value.Trim(), R); rowName.SetValueWithoutNotify(R.Name); dirty = true; RebuildTimeline(); } });
            var rp = El(rowProps, "ae-wrap");
            visBtn = Btn(rp, "보임", () => { if (R == null) return; bool v = !R.Visible; RowProp(r => r.Visible = v); }, "보이기 · 숨기기(고른 레이어 모두)");
            lockBtn = Btn(rp, "잠금", () => { if (R == null) return; bool v = !R.Locked; RowProp(r => r.Locked = v); }, "잠그면 그려지지 않음(고른 레이어 모두)");
            clipBtn = Btn(rp, "클리핑", () => { if (R == null) return; bool v = !R.Clip; RowProp(r => r.Clip = v); }, "아래 레이어에 클리핑(고른 레이어 모두)");
            Lbl(rp, "불투명도", "ae-k");
            rowOp = Int(rp, 100);
            rowOp.RegisterValueChangedCallback(e => { if (R == null || readOnly) return; double v = Mathf.Clamp(e.newValue, 0, 100) / 100.0; RowProp(r => r.Opacity = v); });
            Lbl(rp, "%", "ae-k");
            // 레이어 통째 복사 · 끼워 넣기(오른쪽 칸 — 가운데 버튼 줄이 늘어나 화면이 눌리지 않게 여기에)
            var lcp = El(rowProps, "ae-wrap");
            Btn(lcp, "레이어 복사", CopyLayers, "고른 레이어들을 모든 프레임 그림까지 통째로 복사(Ctrl+Alt+C) — 여러 개는 타임라인 레이어 이름을 Ctrl · Shift+누르기");
            Btn(lcp, "레이어 붙여넣기", PasteLayers, "복사한 레이어들을 지금 레이어 위에 끼워 넣기(Ctrl+Alt+V) — 다른 동작에도. 프레임이 모자라면 보탬 · 이어 쓰던 칸도 그대로");

            Sec(side, "캔버스", "캔버스만 = 그림 크기 그대로 판만 넓히거나 자름(아래 가운데 = 발 기준) · 그림째 = 그림도 같이 늘이거나 줄임");
            var cs = El(side, "ae-row");
            rwField = Int(cs, 64); Lbl(cs, "×", "ae-k"); rhField = Int(cs, 64);
            lockCanvasBtn2 = Btn(cs, "잠금", ToggleCanvasLock, "캔버스 크기 잠금 켜기 · 끄기");
            var cs2 = El(side, "ae-wrap");
            Btn(cs2, "캔버스만 바꾸기", () => { if (CanvasLocked()) return; int w = rwField.value, h = rhField.value; AskCanvas($"캔버스만 {W}×{H} → {w}×{h}", () => Resize(w, h)); },
                "그림은 그대로 두고 판 크기만(넓히면 빈칸 · 줄이면 잘림) — 아래 가운데(발) 기준 · 잠금을 끈 뒤");
            Btn(cs2, "그림째 늘이기 · 줄이기", () => { if (CanvasLocked()) return; int w = rwField.value, h = rhField.value; AskCanvas($"그림째 {W}×{H} → {w}×{h}", () => ScaleCanvas(w, h)); },
                "그림을 새 크기에 맞게 같이 늘이거나 줄임(도트 그대로 — 가장 가까운 점) · 잠금을 끈 뒤");
            Btn(cs2, "½", () => { if (CanvasLocked()) return; int w = Math.Max(1, W / 2), h = Math.Max(1, H / 2); AskCanvas($"그림째 {W}×{H} → {w}×{h}", () => ScaleCanvas(w, h)); }, "그림째 반으로(예: 256 → 128)");
            Btn(cs2, "¼", () => { if (CanvasLocked()) return; int w = Math.Max(1, W / 4), h = Math.Max(1, H / 4); AskCanvas($"그림째 {W}×{H} → {w}×{h}", () => ScaleCanvas(w, h)); }, "그림째 4분의 1로(예: 256 → 64)");
            Btn(cs2, "×2", () => { if (CanvasLocked()) return; int w = W * 2, h = H * 2; AskCanvas($"그림째 {W}×{H} → {w}×{h}", () => ScaleCanvas(w, h)); }, "그림째 두 배로");

            Sec(side, "발밑 선", "발이 닿는 바닥 줄(캔버스 맨 아래에서 px). 그 아래 = 바닥을 뚫고 나가는 자리(참격 등) — 결투 · MFS 내보내기도 이 선을 바닥에 맞춤");
            var fr = El(side, "ae-row");
            Lbl(fr, "아래에서", "ae-k");
            footField = Int(fr, 0);
            footField.tooltip = "0 = 캔버스 맨 아래가 바닥(예전과 같음)";
            footField.RegisterValueChangedCallback(e => SetFoot(e.newValue));
            Lbl(fr, "px", "ae-k");
            var fr2 = El(side, "ae-wrap");
            Btn(fr2, "아래로 +16", () => ExtendDownAsk(16), "캔버스를 아래로만 16px 늘리고 발밑 선도 16 올림 — 캐릭터는 제자리, 밑에 빈 공간");
            Btn(fr2, "아래로 +32", () => ExtendDownAsk(32), "캔버스를 아래로만 32px 늘리고 발밑 선도 32 올림");
            Btn(fr2, "캐릭터 전체에 맞추기", ApplyFootAll, "같은 캐릭터(hero- 또는 foe-)의 모든 그림 파일을 이 발밑 선으로(파일 이름만 바뀜 — 상태가 바뀔 때 발이 흔들리지 않게)");
            // 캔버스 기준점: 발밑(캐릭터 — 기본) · 가운데(이펙트용 — 폭발 · 참격 · 섬광처럼 한 점에서 퍼지는 그림)
            Sec(side, "캔버스 기준", "이 그림의 기준점. 발밑 = 캐릭터(캔버스 아래 가운데 · 발밑 선) · 가운데 = 이펙트용(캔버스 정가운데). 이펙트 카드에 넣으면 기준이 자동으로 맞춰지고, MFS 내보내기 피벗도 같아요.");
            var pv = El(side, "ae-wrap");
            pivotBtn = Btn(pv, "기준: 발밑(캐릭터)", TogglePivot, "누를 때마다 발밑(캐릭터) ↔ 가운데(이펙트용) — 파일 이름에 ‘가운데’ 로 저장");

            Sec(side, "파일", "상태를 누르면 열려요(초록 = 파일 있음, 없으면 새로)");
            fileList = El(side, "ae-wrap");
            var nk = El(side, "ae-row");
            keyField = new TextField { value = "hero-idle" }; keyField.AddToClassList("ae-text"); nk.Add(keyField);
            Btn(nk, "열기", () => OpenKey(keyField.value.Trim().ToLowerInvariant()), "이름으로 열기 · 새로 만들기");
            Btn(nk, "폴더", OpenSaveFolder, "저장 폴더 열기(Assets/MoriKit/Data~/Fight)");
            // 자유 프리셋: 정해진 상태(idle · run …) 말고도 원하는 이름의 동작을 목록에 계속 추가
            // 이름은 편집기 밖의 대화 상자에서 적음 — 편집기 단축키와 절대 부딪히지 않게(입력 중엔 대화 상자만 키를 받음)
            var cp = El(side, "ae-row");
            Lbl(cp, "자유 프리셋(이름은 창에서)", "ae-k");
            var cb = El(side, "ae-wrap");
            Btn(cb, "+ 플레이어", () => AddCustom("hero-"), "이름 적는 창이 뜸 → hero-이름 으로 목록에 추가하고 열기 — 미리보기 ‘고정’ 이 켜져 있으면 플레이어 자리에 보여요");
            Btn(cb, "+ 결투자", () => AddCustom("foe-"), "이름 적는 창이 뜸 → foe-이름 으로 목록에 추가하고 열기 — 미리보기 ‘고정’ 이 켜져 있으면 결투자 자리에 보여요");
            Btn(cb, "+ 기타", () => AddCustom(""), "이름 적는 창이 뜸 → 이름 그대로 추가(이펙트 그림 등 캐릭터가 아닌 것)");
            Btn(cb, "목록에서 빼기", RemoveCustom, "지금 연 자유 프리셋을 목록에서 뺌(그린 파일은 지우지 않아요)");
            var ns = El(side, "ae-row");
            Lbl(ns, "새 그림 크기", "ae-k");
            wField = Int(ns, 64); Lbl(ns, "×", "ae-k"); hField = Int(ns, 64);
            var np = El(side, "ae-wrap");
            foreach (var (n, w, h) in new[] { ("64", 64, 64), ("128", 128, 128), ("256", 256, 256), ("512", 512, 512), ("FHD", 1920, 1080), ("4K", 3840, 2160) })
            {
                int ww = w, hh = h;
                Btn(np, n, () => { wField.value = ww; hField.value = hh; }, $"새 그림 크기 {ww}×{hh} — 그다음 위에 새 이름을 적고 ‘열기’ (최대 {MAX})");
            }

            // ── 가져오기: Fight/import 폴더의 PNG · JPG · PSD
            Sec(side, "가져오기", "import 폴더에 그림(PNG · JPG · PSD)을 넣고 ‘가져오기…’ — 창에서 넣는 방식 · 칸 · 맞추기 · 정렬을 고르고 미리 보며 가져와요");
            var ib = El(side, "ae-wrap");
            Btn(ib, "폴더 열기", () => { Directory.CreateDirectory(ImportDir); Application.OpenURL("file:///" + ImportDir.Replace('\\', '/')); BuildImports(); }, "탐색기에서 import 폴더 열기 — 여기에 파일을 넣으세요");
            Btn(ib, "새로 고침", BuildImports, "목록 다시 읽기");
            hiddenBtn = Btn(ib, "뺀 것 보기", () => { showHidden = !showHidden; BuildImports(); }, "목록에서 뺀 파일도 보기(다시 넣기)");
#if UNITY_EDITOR
            Btn(ib, "파일 고르기…", PickFile, "다른 곳의 파일을 골라 import 폴더로 복사(에디터에서만)");
#endif
            importList = El(side, "ae-col");
            BuildImportDialog();
            BuildExportDialog();
            BuildSaveDialog();

            status = Lbl(Root, "", "ae-status");
            LoadKeys();
        }

        static readonly string[] HELP =
        {
            "■ 순서: 오른쪽 ‘파일’ 에서 상태(hero-idle …)를 고름 → 가운데에 그림 → 아래 타임라인에서 프레임 추가 · 길이(f) → ▶ 재생 → Ctrl+S 저장",
            "■ 타임라인: 가로 = 프레임, 세로 = 레이어(헤어 · 몸통 · 무기 …). 칸을 누르면 그 프레임 · 레이어로 가서 그 칸에 그려요.",
            "   ● = 이 프레임의 그림 · ― = 앞 그림 유지(이어 씀, 고치면 함께 바뀜) · 빈 칸 = 그림 없음(그리면 새로 생김)",
            "■ 아래는 처음 단축키예요 — 머리의 ‘단축키’ 에서 모두 바꿀 수 있어요(초기값 · 프리셋 저장 · 불러오기).",
            "■ Ctrl+클릭 = 그 자리에 그려진 맨 위 레이어로 이동",
            "■ 도구: S 아래 · D 위로 도구 바꾸기 · 펜 B · 지우개 E · 스포이드 I(그리면서 Alt 누른 채 클릭) · 채우기 G · 직선 L · 사각 U(Shift = 채움) · 손 H(Space+드래그 · 오른쪽 드래그)",
            "   굵기 Z − · X + · F = 색을 검정으로 · 대칭 M · 확대 휠 · Ctrl+= / Ctrl+− · 메인/서브 색 바꾸기는 색 패널 ⇄",
            "■ 프레임: , 이전 · . 다음 · ↑↓ 레이어 고르기 · Space 재생 · 되돌리기 Ctrl+Z · 다시 Ctrl+Y · 저장 Ctrl+S",
            "■ 시트 가져오기: ‘자동 나눠 프레임’ = 칸 수를 스스로 찾아 등분(빈 줄 또는 잔상이 옅어지는 골짜기 · 443.5 같은 칸도 정확히). 발끝처럼 조금 넘친 건 원래 컷에, 잔상처럼 크게 이어진 건 칸 경계로. ‘정렬:’ = 원본 · 발밑 · 몸통.",
            "■ 캔버스 잠금(기본 켬): 가져와도 캔버스 크기가 안 바뀜. ‘맞추기:’ 그대로 · 알맞게(칸이 캔버스보다 크면 줄임 — 512 칸 → 512 캔버스 = 1:1) · 꽉 차게. 잠금을 끄면 넘치는 그림에 맞춰 캔버스를 키움.",
            "■ 여러 개 고르기: 타임라인의 프레임 번호 · 레이어 이름을 Ctrl+누르기(더하기/빼기) · Shift+누르기(범위). 칸을 Ctrl/Shift+누르면 같은 레이어에서 프레임 여러 개. 길이 · 삭제 · 복제 · 옮김 · 칸 버튼 · 레이어 속성이 고른 것 전부에.",
            "■ 단축키: 머리의 ‘단축키’ 에서 편집기의 모든 동작(도구 · 선택 · 프레임 · 칸 · 레이어 · 보기)을 바꿀 수 있어요. 가져오기 목록의 ✕ = 목록에서 빼기(파일은 그대로).",
            "■ 선택: 사각선택(R) · 올가미(A) · 꺾은선(Q, 점 찍고 처음 점 · 두 번 누르기 · Enter 로 닫기). Shift = 더하기 · Alt = 빼기 · Ctrl+A 전체 · Ctrl+D 해제 · Ctrl+Shift+I 반전 · Delete 지우기. 선택 안에만 칠해져요.",
            "■ 복사 · 붙여넣기: Ctrl+C(고른 영역, 없으면 칸 전체) · Ctrl+X 잘라내기 · Ctrl+V 지금 레이어에 · Ctrl+Shift+V 새 레이어에. 붙인 그림은 바로 변형 상태 — 끌어서 맞추고 Enter. 다른 레이어 · 프레임 · 동작으로도.",
            "■ 레이어 통째로: 타임라인 레이어 이름을 Ctrl+누르기(하나씩) · Shift+누르기(범위)로 여럿 고르고 Ctrl+Alt+C → 다른 동작을 열고 Ctrl+Alt+V. 모든 프레임 그림 · 이어 쓰는 칸 · 레이어 속성까지 그대로.",
            "■ 자유 변형(Ctrl+T): 선택한 그림(없으면 고른 칸 전부). 상자 안 = 옮기기 · 모서리 = 크기(비율 유지, Shift = 자유) · 변 = 한쪽만 · Alt = 가운데 기준 · 반대편 너머로 끌면 뒤집기 · 방향키(Shift = 10px). 아래 칸에서 % 입력 · 또렷하게/부드럽게. Enter · Ctrl+T 확정 · Esc 취소.",
            "■ 레이어 이름: 타임라인의 이름을 두 번 누르면 바꾸기(Enter 확정 · Esc 취소). 저장 폴더: 머리의 ‘저장 폴더’.",
            "■ 캔버스는 최대 4096×4096(4K) — ‘파일’ 아래 크기 버튼(64 … FHD · 4K). 1배보다 작게 보려면 휠 · Ctrl+−(50% · 25% …).",
            "■ 길이는 프레임 단위(기준 60fps → 1f ≈ 17ms). 파일의 폴더 이름 ‘03 · 3f’ — 포토샵에서 ‘100ms’ 로 적어도 읽혀요.",
        };

        // ───────── UI 도우미 ─────────
        static VisualElement El(VisualElement p, string cls) { var v = new VisualElement(); v.AddToClassList(cls); p.Add(v); return v; }
        static Label Lbl(VisualElement p, string t, string cls) { var l = new Label(t); l.AddToClassList(cls); p.Add(l); return l; }
        static Button Btn(VisualElement p, string t, Action a, string tip = null, string extra = null)
        {
            var b = new Button(a) { text = t };
            if (tip != null) b.tooltip = tip;
            b.AddToClassList("ae-btn");
            if (extra != null) b.AddToClassList(extra);
            p.Add(b);
            return b;
        }
        static IntegerField Int(VisualElement p, int v) { var f = new IntegerField { value = v }; f.AddToClassList("ae-int"); p.Add(f); return f; }
        static VisualElement Abs(VisualElement p)
        {
            var v = new VisualElement();
            v.style.position = Position.Absolute;
            v.style.left = 0; v.style.top = 0; v.style.right = 0; v.style.bottom = 0;
            p.Add(v);
            return v;
        }
        static void Sec(VisualElement p, string t, string d) { Lbl(p, t, "ae-sec"); if (d != null) Lbl(p, d, "ae-note"); }
        void Say(string s) { status.text = s; }

        // ───────── 열기 · 닫기 ─────────
        public void Show()
        {
            Open = true;
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
            BuildFiles();
            BuildImports();
            if (key == null) OpenKey("hero-idle");
            else { PushLive(); Refresh(); }
            Root.Focus();
        }

        public void Hide()
        {
            if (dirty) Save();
            play = false;
            Open = false;
            Root.style.display = DisplayStyle.None;
            ArtLib.ClearLive(key);
            StageField.ForceHero = StageField.ForceFoe = null;
            OnClosed?.Invoke();
        }

        public void Toggle() { if (Open) Hide(); else Show(); }

        /// <summary>지금 글 입력 칸(이름 · 숫자 · 검색 등 어디든)에 키가 가 있는지 — 그동안 단축키는 전부 쉼.</summary>
        public bool TextFocus => IsTypingEl(Root.focusController?.focusedElement as VisualElement);

        /// <summary>글을 적는 칸(또는 그 안쪽)인지: TextField · 숫자 칸 · 모든 글 입력 칸(unity-base-text-field).</summary>
        public static bool IsTypingEl(VisualElement v)
        {
            for (var x = v; x != null; x = x.parent)
            {
                if (x is TextField || x is IntegerField || x is FloatField || x is DoubleField || x is LongField) return true;
                if (x.ClassListContains("unity-base-text-field") || x.ClassListContains("unity-text-input") || x.ClassListContains("unity-base-text-field__input")) return true;
            }
            return false;
        }

        // ───────── 자유 프리셋(Fight/custom-states.txt — 한 줄에 하나) ─────────
        static string CustomPath => Path.Combine(ArtLib.Dir, "custom-states.txt");

        static List<string> LoadCustom()
        {
            try
            {
                if (File.Exists(CustomPath))
                    return File.ReadAllLines(CustomPath).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).Distinct().ToList();
            }
            catch (Exception e) { Debug.LogWarning("[Mori] 자유 프리셋 읽기 실패: " + e.Message); }
            return new List<string>();
        }

        static void SaveCustom(List<string> list)
        {
            try { Directory.CreateDirectory(ArtLib.Dir); KitBackup.Before(CustomPath); File.WriteAllLines(CustomPath, new[] { "# 작업실 자유 프리셋(한 줄에 하나)" }.Concat(list)); }
            catch (Exception e) { Debug.LogWarning("[Mori] 자유 프리셋 저장 실패: " + e.Message); }
        }

        /// <summary>자유 프리셋 추가: 칸에 이름이 있으면 그걸로, 비어 있으면 이름 적는 창(편집기 밖 대화 상자라 단축키 · 포커스와 안 부딪힘).</summary>
        void AddCustom(string prefix)
        {
            string who = prefix == "hero-" ? "플레이어(hero-)" : prefix == "foe-" ? "결투자(foe-)" : "기타(이름 그대로)";
            Dialog.Prompt("자유 프리셋 추가 — " + who, "새 동작 이름을 적어 주세요(영문 · 숫자 · - 권장). 예: slash · uppercut · skill1 — 띄어쓰기는 - 로 바뀌어요.", "", "추가",
                v => AddCustomName(prefix, v),
                v => CleanCustom(v).Length == 0 ? "이름을 적어 주세요." : null, 40);
        }

        static string CleanCustom(string raw)
        {
            string n = (raw ?? "").Trim().ToLowerInvariant();
            n = System.Text.RegularExpressions.Regex.Replace(n, @"[\s·.]+", "-");
            foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c.ToString(), "");
            return n.Trim('-');
        }

        void AddCustomName(string prefix, string raw)
        {
            string n = (raw ?? "").Trim().ToLowerInvariant();
            if (n.StartsWith("hero-") || n.StartsWith("foe-")) prefix = "";        // 접두사를 직접 적었으면 그대로
            n = System.Text.RegularExpressions.Regex.Replace(n, @"[\s·.]+", "-");
            foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c.ToString(), "");
            n = n.Trim('-');
            if (n.Length == 0) { Say("자유 프리셋 이름을 적어 주세요(예: slash)."); return; }
            string k = prefix + n;
            var list = LoadCustom();
            if (STATES.Contains(k) || list.Contains(k)) { Say($"{k} 는 이미 목록에 있어요 — 열어 둘게요."); OpenKey(k); BuildFiles(); return; }
            list.Add(k);
            SaveCustom(list);
            OpenKey(k);
            BuildFiles();
            Say($"자유 프리셋 {k} 추가 — 그린 뒤 저장(Ctrl+S)하면 파일이 생겨요.");
        }

        void RemoveCustom()
        {
            var list = LoadCustom();
            if (key == null || !list.Remove(key)) { Say("지금 연 그림은 자유 프리셋이 아니에요 — 목록에서 뺄 프리셋을 먼저 열어 주세요."); return; }
            SaveCustom(list);
            BuildFiles();
            Say($"{key} 를 자유 프리셋 목록에서 뺐어요(저장된 파일은 그대로 — 파일이 있으면 목록에 계속 보여요).");
        }

        void BuildFiles()
        {
            fileList.Clear();
            var files = ArtLib.Files();
            var custom = LoadCustom();
            foreach (var k in STATES.Concat(custom).Concat(FlowTable.Rows.Select(r => r.Anim)).Concat(files.Select(f => f.key)).Distinct())
            {
                var kk = k;
                var b = Btn(fileList, k, () => OpenKey(kk));
                if (!STATES.Contains(k)) b.tooltip = custom.Contains(k) ? "자유 프리셋" : FlowTable.Rows.Any(r => r.Anim == k) ? "연결 표의 그림(F2 › 화면 › 캐릭터 흐름)" : "목록에 없는 이름의 파일";
                else if (FlowTip(k) != null) b.tooltip = FlowTip(k);
                b.EnableInClassList("ae-btn--has", files.Any(f => f.key == k));
                b.EnableInClassList("ae-btn--on", k == key);
            }
        }

        void OpenKey(string k)
        {
            if (string.IsNullOrEmpty(k)) return;
            if (xf != null) CommitTransform();               // 떠 있는 붙여넣기 · 변형은 확정하고(다른 그림으로 넘어가며 사라지지 않게)
            if (dirty && !Save())
            {
                // 저장이 안 됐으면 넘어가지 않음 — 넘어가면 바꾼 내용이 사라짐
                keyField.SetValueWithoutNotify(key);
                Dialog.Info("저장 실패", $"‘{key}’ 를 저장하지 못해서 다른 그림으로 넘어가지 않았어요.\n바꾼 내용은 그대로 있어요. 잠시 뒤 다시 눌러 주세요(다른 프로그램이 파일을 열고 있으면 닫아 주세요).");
                return;
            }
            xf = null; moving = false;
            ArtLib.ClearLive(key);
            key = k; keyField.value = k;
            readOnly = false; play = false;
            undo.Clear(); redo.Clear();
            rows = new List<Row>(); frames = new List<Frame>();
            // 같은 동작 파일이 여러 개면(예전 위치에서 옮겨 온 것 등) 가장 최근에 저장된 것
            var f = ArtLib.Files().Where(x => x.key == k).OrderByDescending(x => { try { return File.GetLastWriteTimeUtc(x.path); } catch { return DateTime.MinValue; } }).FirstOrDefault();
            if (f.path != null)
            {
                path = f.path;
                var toks = ArtLib.Tokens(Path.GetFileNameWithoutExtension(path));
                once = toks.Any(t => t.ToLowerInvariant() == "once" || t == "1회");
                foot = ArtLib.FootOf(Path.GetFileNameWithoutExtension(path));
                centerPivot = toks.Any(ArtLib.IsCenterToken);
                fps = 60;
                foreach (var t in toks) if (t.ToLowerInvariant().EndsWith("fps") && int.TryParse(t.Substring(0, t.Length - 3), out var v)) fps = Mathf.Clamp(v, 1, 240);
                if (f.sheet) { readOnly = true; }
                try { LoadDoc(Psd.Read(File.ReadAllBytes(path))); }
                catch (Exception e) { Say("열기 실패: " + e.Message); NewDoc(); }
                Say(readOnly ? "시트식(칸 크기) 파일은 보기만 해요 — 고치려면 포토샵에서." : $"{Path.GetFileName(path)} 열림 — 프레임 {frames.Count} · 레이어 {rows.Count} · {W}×{H}");
            }
            else
            {
                path = Path.Combine(ArtLib.Dir, k + ".psd");
                once = k.StartsWith("hero-attack") || k == "foe-ko" || k == "hero-down" || FlowOnce(k);
                fps = 60;
                NewDoc();
                foot = SharedFoot(k);                 // 같은 캐릭터의 다른 그림과 같은 발밑 선으로 시작
                centerPivot = false;
                Say($"새 그림 {k} ({W}×{H}) — 저장하면 {Path.GetFileName(path)} 로 만들어져요.");
            }
            loop = !once;
            foot = Mathf.Clamp(foot, 0, Math.Max(0, H - 1));
            footField?.SetValueWithoutNotify(foot);
            PaintPivot();
            fi = 0; ri = rows.Count - 1;
            dirty = false;
            // 다른 그림의 텍스처는 버리고 새로(미뤄 합치기가 옛 그림을 잠깐 보여 주지 않게)
            foreach (var t in frameTex) if (t != null) TexGrave.Kill(t);
            frameTex.Clear(); stale.Clear();
            FitZoom();
            RecomposeAll();
            BuildFiles();
        }

        void NewDoc()
        {
            xf = null; poly = null; SetSel(null);
            W = Mathf.Clamp(wField.value, 4, MAX); H = Mathf.Clamp(hField.value, 4, MAX);
            rows = new List<Row> { new Row { Name = "레이어 1" } };
            frames = new List<Frame> { new Frame { Dur = 6, Cels = new List<Cel> { null } } };
        }

        const int MAX = 4096;          // 캔버스 최대(4K)
        byte[] Blank() => new byte[W * H * 4];
        Cel NewCel() => new Cel { Img = new TileImage(W, H) };
        Cel CelFrom(byte[] rgba) => new Cel { Img = TileImage.FromRgba(rgba, W, H) };

        // PSD → 모델
        void LoadDoc(PsdDoc d)
        {
            xf = null; poly = null; SetSel(null);
            W = d.W; H = d.H;
            var topNodes = d.Root.Kids;
            var groups = topNodes.Where(n => n.Group).ToList();
            var order = new List<string>();
            var props = new Dictionary<string, PsdNode>();
            void Merge(IEnumerable<(string path, PsdNode n)> leaves)
            {
                int last = -1;
                foreach (var (p, n) in leaves)
                {
                    int i = order.IndexOf(p);
                    if (i < 0) { order.Insert(last + 1, p); i = last + 1; props[p] = n; }
                    last = i;
                }
            }
            IEnumerable<(string, PsdNode)> LeavesRaw(PsdNode g, string prefix)
            {
                foreach (var k in g.Kids)
                {
                    string p = prefix.Length == 0 ? k.Name : prefix + "/" + k.Name;
                    if (k.Group) { foreach (var x in LeavesRaw(k, p)) yield return x; }
                    else yield return (p, k);
                }
            }
            // 한 프레임 안에 같은 이름 레이어가 여럿이면(예전 파일) 하나로 합쳐 사라지지 않게 ‘이름 (2)’ 로 나눠 읽음
            IEnumerable<(string, PsdNode)> LeavesOf(PsdNode g, string prefix)
            {
                var seen = new Dictionary<string, int>();
                foreach (var (p, n) in LeavesRaw(g, prefix))
                {
                    seen[p] = seen.TryGetValue(p, out var c) ? c + 1 : 1;
                    yield return (seen[p] == 1 ? p : $"{p} ({seen[p]})", n);
                }
            }
            if (groups.Count == 0)
            {
                // 폴더 없음 = 프레임 하나, 맨 위 레이어들이 레이어
                Merge(topNodes.Where(n => !n.Group).Select(n => (n.Name, n)));
                var fr = new Frame { Dur = 6 };
                foreach (var p in order) fr.Cels.Add(CelOf(props[p]));
                frames.Add(fr);
            }
            else
            {
                // 번호 순(읽는 쪽과 같게)
                int Num(PsdNode g) { var t = g.Name.Split(new[] { ' ', '·' }, StringSplitOptions.RemoveEmptyEntries); return t.Length > 0 && int.TryParse(t[0], out var n) ? n : int.MinValue; }
                if (groups.All(g => Num(g) != int.MinValue)) groups = groups.OrderBy(Num).ToList();
                // 레이어 순서: 맨 위 순서대로 — 폴더 밖(공통) 레이어 · 각 프레임 폴더의 레이어를 합침
                foreach (var n in topNodes)
                {
                    if (n.Group) Merge(LeavesOf(n, ""));
                    else Merge(new[] { (n.Name, n) });
                }
                var shared = new Dictionary<string, Cel>();
                foreach (var n in topNodes.Where(n => !n.Group)) if (!shared.ContainsKey(n.Name)) shared[n.Name] = CelOf(n);   // 같은 이름이 겹쳐도 열기 실패하지 않게
                foreach (var g in groups)
                {
                    var fr = new Frame { Dur = DurOf(g.Name) };
                    var mine = LeavesOf(g, "").GroupBy(x => x.Item1).ToDictionary(x => x.Key, x => x.First().Item2);
                    foreach (var p in order) fr.Cels.Add(mine.TryGetValue(p, out var lf) ? CelOf(lf) : shared.TryGetValue(p, out var sc) ? sc : null);
                    frames.Add(fr);
                }
            }
            foreach (var p in order)
            {
                var n = props[p];
                rows.Add(new Row { Name = p, Visible = n.Visible, Clip = n.Clip, Opacity = n.Opacity, Blend = n.Blend ?? "norm" });
            }
            if (rows.Count == 0) { rows.Add(new Row { Name = "레이어 1" }); foreach (var fr in frames) fr.Cels.Add(null); }
            if (frames.Count == 0) frames.Add(new Frame { Cels = rows.Select(_ => (Cel)null).ToList() });
            // 앞 프레임과 똑같은 그림 = 유지(이어 씀)
            for (int r = 0; r < rows.Count; r++)
                for (int f = 1; f < frames.Count; f++)
                {
                    var a = frames[f - 1].Cels[r]; var b = frames[f].Cels[r];
                    if (a != null && b != null && a != b && a.Img.SameAs(b.Img)) frames[f].Cels[r] = a;
                }
        }

        int DurOf(string name)
        {
            foreach (var t in name.Split(new[] { ' ', '·' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var lo = t.ToLowerInvariant();
                if (lo.EndsWith("ms") && float.TryParse(lo.Substring(0, lo.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms)) return Mathf.Max(1, Mathf.RoundToInt(ms * fps / 1000f));
                if (lo.EndsWith("f") && !lo.EndsWith("fps") && int.TryParse(lo.Substring(0, lo.Length - 1), out var fr)) return Mathf.Max(1, fr);
            }
            return Mathf.Max(1, Mathf.RoundToInt(100f * fps / 1000f));
        }

        /// <summary>레이어 → 캔버스 크기 셀.</summary>
        Cel CelOf(PsdNode n)
        {
            var c = NewCel();
            if (n.Rgba != null && n.W > 0 && n.H > 0) c.Img.Blit(n.Rgba, n.W, n.H, n.L, n.T);
            return c;
        }

        // ───────── 저장 ─────────
        /// <summary>바뀐 게 있으면 저장(킷을 닫거나 플레이를 멈출 때).</summary>
        public void SaveIfDirty() { if (dirty && key != null && !readOnly) Save(); }

        /// <summary>자동 저장: 마지막으로 바꾼 뒤 이만큼(초) 손을 떼면 저장(그리는 중 · 변형 중엔 기다림).</summary>
        const float AUTOSAVE_DELAY = 1.2f;
        float dirtyAt = -1, saveFailAt = -10;
        bool lastSaveFailed;
        static bool AutoSave { get => PlayerPrefs.GetInt("mori.editor.autosave", 1) == 1; set => PlayerPrefs.SetInt("mori.editor.autosave", value ? 1 : 0); }

        void TickAutoSave()
        {
            if (!dirty || key == null || readOnly) { dirtyAt = -1; return; }
            if (dirtyAt < 0) dirtyAt = Time.realtimeSinceStartup;
            if (!AutoSave || drawing || xf != null || moving || selecting || lassoDraw) { dirtyAt = Time.realtimeSinceStartup; return; }
            float now = Time.realtimeSinceStartup;
            if (now - dirtyAt < AUTOSAVE_DELAY || now - saveFailAt < 2f) return;
            Save(true);
        }

        /// <summary>파일 쓰기: 다른 곳(그림 다시 읽기 등)이 잠깐 붙잡고 있으면 잠시 뒤 다시(최대 약 1초).</summary>
        static void WriteRetry(string tmp, string target, byte[] bytes)
        {
            Exception last = null;
            for (int i = 0; i < 12; i++)
            {
                try
                {
                    File.WriteAllBytes(tmp, bytes);
                    File.Copy(tmp, target, true);
                    try { File.Delete(tmp); } catch { }
                    return;
                }
                catch (IOException e) { last = e; System.Threading.Thread.Sleep(80); }
                catch (UnauthorizedAccessException e) { last = e; System.Threading.Thread.Sleep(80); }
            }
            throw last ?? new IOException("파일을 쓸 수 없어요");
        }

        bool Save() => Save(false);

        /// <summary>저장 — 성공하면 true. 실패하면 바뀐 상태(dirty)를 그대로 두고 크게 알림.</summary>
        bool Save(bool auto)
        {
            if (key == null) return false;
            if (readOnly) { if (!auto) Say("시트식 파일은 저장하지 않아요."); return false; }
            // 붙여넣기 · 변형 중이면 먼저 확정 — 떠 있는 그림이 저장에서 빠지지 않게
            if (xf != null) CommitTransform();
            // 레이어 이름이 겹치면 다시 열 때 하나만 읽히므로 저장 전에 이름을 나눔(‘몸통’ · ‘몸통 2’)
            if (FixDupNames() > 0) RebuildTimeline();
            try
            {
                Directory.CreateDirectory(ArtLib.Dir);
                var doc = new PsdDoc { W = W, H = H };
                var used = new bool[rows.Count];
                for (int f = 0; f < frames.Count; f++)
                {
                    var g = new PsdNode { Group = true, Name = $"{f + 1:00} · {frames[f].Dur}f", Parent = doc.Root };
                    for (int r = 0; r < rows.Count; r++)
                    {
                        var c = frames[f].Cels[r];
                        if (c == null) continue;
                        used[r] = true;
                        AddLeaf(g, rows[r], c.Img);
                    }
                    doc.Root.Kids.Add(g);
                }
                // 어느 프레임에도 그림이 없는 레이어도 이름은 남긴다(첫 프레임에 빈 레이어)
                for (int r = 0; r < rows.Count; r++) if (!used[r] && doc.Root.Kids.Count > 0) AddLeaf(doc.Root.Kids[0], rows[r], null);
                var bytes = Psd.Write(doc);
                var keep = File.Exists(path)
                    ? ArtLib.Tokens(Path.GetFileNameWithoutExtension(path)).Skip(1).Where(t => { var lo = t.ToLowerInvariant(); return lo != "once" && t != "1회" && !lo.EndsWith("fps") && !lo.EndsWith("ms") && !ArtLib.IsFootToken(t) && !ArtLib.IsCenterToken(t); }).ToList()
                    : new List<string>();
                if (once) keep.Add("once");
                if (foot > 0) keep.Add("발밑" + foot);
                if (centerPivot) keep.Add("가운데");
                if (fps != 60) keep.Add(fps + "fps");
                string name = key + string.Concat(keep.Select(t => " · " + t)) + ".psd";
                string target = Path.Combine(ArtLib.Dir, name);
                string tmp = target + ".tmp";
                KitBackup.Before(target);                                 // 덮어쓰기 전 자동 백업(프로젝트 밖)
                if (!string.IsNullOrEmpty(path) && path != target) KitBackup.Before(path);
                WriteRetry(tmp, target, bytes);
                if (!string.Equals(Path.GetFullPath(target), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) && File.Exists(path)) { try { File.Delete(path); } catch (Exception de) { Debug.LogWarning("[Mori 킷] 옛 이름 파일 지우기 실패: " + de.Message); } }
                path = target;
                // 같은 이름(동작)의 다른 파일이 남아 있으면 열 때 옛 파일을 집을 수 있음 → 백업에 남기고 치움
                int dup = 0;
                foreach (var other in ArtLib.Files().Where(x => x.key == key && !x.sheet && !string.Equals(Path.GetFullPath(x.path), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    try { KitBackup.Before(other.path, true); File.Delete(other.path); dup++; }
                    catch (Exception de) { Debug.LogWarning("[Mori 킷] 겹친 파일 정리 실패: " + de.Message); }
                }
                dirty = false;
                Say($"{(auto ? "자동 저장" : "저장")} — {name} ({DateTime.Now:HH:mm:ss})" + (dup > 0 ? $" · 같은 동작의 옛 파일 {dup}개는 백업으로 옮겼어요" : ""));
                if (!auto || dup > 0 || lastSaveFailed) BuildFiles();
                lastSaveFailed = false;
                dirtyAt = -1;
                RefreshTitle();
                return true;
            }
            catch (Exception e)
            {
                lastSaveFailed = true;
                saveFailAt = Time.realtimeSinceStartup;
                Say("⚠ 저장 실패 — 바뀐 내용은 아직 화면에 있어요(곧 다시 시도): " + e.Message);
                Debug.LogError("[Mori 킷] 그림 저장 실패: " + key + " — " + e);
                return false;
            }
        }

        /// <summary>레이어 이름의 ‘/’ = 폴더(예: 옷/상의) — 프레임 폴더 안에 폴더를 만들어 넣는다(아래 → 위 순서).</summary>
        void AddLeaf(PsdNode frameGroup, Row row, TileImage im)
        {
            var parts = row.Name.Split('/');
            var g = frameGroup;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var sub = g.Kids.LastOrDefault();
                if (sub == null || !sub.Group || sub.Name != parts[i]) { sub = new PsdNode { Group = true, Name = parts[i], Parent = g }; g.Kids.Add(sub); }
                g = sub;
            }
            // 그려진 영역만 저장(빈 곳은 파일에 안 씀 — 큰 캔버스에서 파일이 작아짐)
            int l = 0, t = 0, r = 0, b = 0;
            byte[] px = new byte[0];
            if (im != null && im.Bounds(out l, out t, out r, out b)) { px = new byte[(r - l) * (b - t) * 4]; im.CopyTo(px, l, t, r - l, b - t); }
            else { l = t = r = b = 0; }
            g.Kids.Add(new PsdNode { Name = parts[parts.Length - 1], Visible = row.Visible, Clip = row.Clip, Opacity = row.Opacity, Blend = row.Blend, L = l, T = t, R = r, B = b, Rgba = px, Parent = g });
        }

        // ───────── 실행 취소 ─────────
        Snap Capture(Cel pxCel = null, IEnumerable<Cel> many = null) => new Snap
        {
            Rows = rows.Select(r => r.Clone()).ToList(),
            Frames = frames.Select(f => (f.Dur, f.Cels.ToList())).ToList(),
            Fi = fi, Ri = ri,
            PxCel = pxCel, Tiles = pxCel?.Img.Snapshot(),
            Many = many?.Select(c => (c, c.Img.Snapshot())).ToList(),
        };

        void Push(Snap s) { undo.Add(s); if (undo.Count > UNDO_MAX) undo.RemoveAt(0); redo.Clear(); }
        void SnapStruct() { if (xf != null) CommitTransform(); Push(Capture()); }
        void SnapPixels(Cel c) { if (xf != null) CommitTransform(); Push(Capture(c)); }

        void Restore(Snap s)
        {
            rows = s.Rows.Select(r => r.Clone()).ToList();
            frames = s.Frames.Select(x => new Frame { Dur = x.dur, Cels = x.cels.ToList() }).ToList();
            fi = s.Fi; ri = s.Ri;
            if (s.PxCel != null) s.PxCel.Img.Restore(s.Tiles);
            if (s.Many != null) foreach (var (c, t) in s.Many) if (c.Img.Count == t.Length) c.Img.Restore(t);
        }

        void Undo()
        {
            if (xf != null) { CancelTransform(); return; }
            if (undo.Count == 0 || readOnly) return;
            var s = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1);
            redo.Add(Capture(s.PxCel, s.Many?.Select(x => x.cel)));
            Restore(s);
            dirty = true;
            RecomposeAll();
        }

        void Redo()
        {
            if (redo.Count == 0 || readOnly) return;
            if (xf != null) CommitTransform();
            var s = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1);
            undo.Add(Capture(s.PxCel, s.Many?.Select(x => x.cel)));
            Restore(s);
            dirty = true;
            RecomposeAll();
        }

        // ───────── 합치기 · 텍스처 ─────────
        //  · 그림은 128×128 조각(TileImage). 펜이 지나간 조각만 다시 합쳐, 그 조각만 그래픽 카드로 올린다(CopyTexture).
        //  · 한 화면 갱신에 한 번만(Tick → Flush). 임시 버퍼는 재사용(끊김 없음).
        //  · 전체 다시 합치기는 조각을 여러 코어로 나눠(Parallel). 큰 그림이면 지금 프레임만 바로, 나머지는 조금씩.
        static bool canCopy => SystemInfo.copyTextureSupport != UnityEngine.Rendering.CopyTextureSupport.None;
        readonly List<bool> stale = new List<bool>();
        readonly HashSet<int> dirtyTiles = new HashSet<int>(), strokeTiles = new HashSet<int>();
        Cel dirtyCel;
        byte[] pickBuf = new byte[0];
        byte[] fullBuf = new byte[0];
        readonly TileComp.Scratch mainScratch = new TileComp.Scratch();
        TileComp.Layer[] mainLayers = new TileComp.Layer[0];
        readonly Dictionary<int, List<(Texture2D tex, byte[] buf)>> stagePool = new Dictionary<int, List<(Texture2D, byte[])>>();
        readonly Dictionary<int, int> stageUsed = new Dictionary<int, int>();
        const long BIG = 1024L * 1024;     // 이보다 큰 그림(픽셀 수 × 프레임 수)이면 나머지 프레임은 조금씩 합침

        int TilesX => (W + TileImage.T - 1) / TileImage.T;
        int TilesY => (H + TileImage.T - 1) / TileImage.T;

        void TileRect(int i, out int x0, out int y0, out int tw, out int th)
        {
            int tx = i % TilesX, ty = i / TilesX;
            x0 = tx * TileImage.T; y0 = ty * TileImage.T;
            tw = Math.Min(TileImage.T, W - x0); th = Math.Min(TileImage.T, H - y0);
        }

        void FillLayers(Frame fr, int tile, TileComp.Layer[] L)
        {
            for (int r = 0; r < rows.Count; r++)
            {
                var c = r < fr.Cels.Count ? fr.Cels[r] : null;
                var row = rows[r];
                L[r] = new TileComp.Layer
                {
                    Px = c != null && c.Img != null && tile < c.Img.Count ? c.Img.Tiles[tile] : null,
                    Visible = row.Visible, Clip = row.Clip, Opacity = (float)row.Opacity, Mode = TileComp.ModeOf(row.Blend),
                };
            }
        }

        TileComp.Layer[] MainLayers() { if (mainLayers.Length != rows.Count) mainLayers = new TileComp.Layer[rows.Count]; return mainLayers; }

        /// <summary>프레임 f 전체 → fullBuf(아래 → 위 줄, 텍스처용). 조각마다 다른 코어에서.</summary>
        void ComposeFull(int f)
        {
            if (fullBuf.Length != W * H * 4) fullBuf = new byte[W * H * 4];
            var fr = frames[f];
            int n = TilesX * TilesY, nr = rows.Count;
            var dst = fullBuf;
            if (n <= 2)
            {
                var L = MainLayers();
                for (int i = 0; i < n; i++) { TileRect(i, out int x0, out int y0, out int tw, out int th); FillLayers(fr, i, L); TileComp.Compose(L, tw, th, mainScratch, dst, x0, y0, W, H, true); }
                return;
            }
            System.Threading.Tasks.Parallel.For(0, n,
                () => (new TileComp.Scratch(), new TileComp.Layer[nr]),
                (i, st, loc) =>
                {
                    TileRect(i, out int x0, out int y0, out int tw, out int th);
                    FillLayers(fr, i, loc.Item2);
                    TileComp.Compose(loc.Item2, tw, th, loc.Item1, dst, x0, y0, W, H, true);
                    return loc;
                },
                _ => { });
        }

        Texture2D TexFor(int i)
        {
            while (frameTex.Count <= i) frameTex.Add(null);
            var t = frameTex[i];
            if (t == null || t.width != W || t.height != H)
            {
                if (t != null) TexGrave.Kill(t);
                t = frameTex[i] = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Mori edit " + key + " " + i };
            }
            return t;
        }

        /// <summary>프레임 f 를 통째로 다시 합쳐 텍스처에.</summary>
        void ComposeFrame(int f)
        {
            ComposeFull(f);
            var t = TexFor(f);
            t.SetPixelData(fullBuf, 0);
            t.Apply(false, false);
            while (stale.Count <= f) stale.Add(false);
            stale[f] = false;
            while (silStale.Count <= f) silStale.Add(true);
            if (onion && onionStyle != 3) MakeSil(f); else silStale[f] = true;
        }

        /// <summary>
        /// fullBuf(방금 합친 프레임) → 어니언 그림. 윤곽선 = 흰 테두리, 실루엣 = 흰 덩어리,
        /// 단색 명암 = 원래 색을 버리고 밝기만 회색(어두운 곳도 보이게 70 ~ 255) + 테두리는 흰색 — 칠할 때 빨강 · 파랑 한 색으로 입혀짐.
        /// </summary>
        void MakeSil(int f)
        {
            int n = W * H;
            if (silBuf.Length != n * 4) silBuf = new byte[n * 4];
            var src = fullBuf; var d = silBuf;
            bool outline = onionStyle == 0, mono = onionStyle == 2, shade = onionStyle == 4;
            for (int y = 0; y < H; y++)
            {
                int row = y * W;
                for (int x = 0; x < W; x++)
                {
                    int i = row + x, k = i * 4;
                    byte a = src[k + 3];
                    bool on = a > 24;
                    // 테두리 = 옆(4방향)에 빈 칸이 있는 픽셀
                    bool edge = on && (x == 0 || x == W - 1 || y == 0 || y == H - 1
                        || src[k - 4 + 3] <= 24 || src[k + 4 + 3] <= 24 || src[k - W * 4 + 3] <= 24 || src[k + W * 4 + 3] <= 24);
                    byte g = 255;
                    if (shade && on && !edge)
                    {
                        // 명암 경계선 = 옆(4방향) 픽셀보다 SHADE_STEP 넘게 밝은 쪽 픽셀(1px 선 — 밝은 면 가장자리에)
                        float me = Lum(src, k);
                        bool line = me - Lum(src, k - 4) > SHADE_STEP || me - Lum(src, k + 4) > SHADE_STEP
                            || me - Lum(src, k - W * 4) > SHADE_STEP || me - Lum(src, k + W * 4) > SHADE_STEP;
                        on = line;
                        g = 210;                                     // 바깥 윤곽선보다 살짝 어둡게 — 둘이 구분되게
                    }
                    if (outline) on = edge;
                    if (mono && on && !edge)
                    {
                        float lum = (0.299f * src[k] + 0.587f * src[k + 1] + 0.114f * src[k + 2]) / 255f;
                        g = (byte)(70 + 185 * lum);
                    }
                    d[k] = d[k + 1] = d[k + 2] = g;
                    d[k + 3] = on ? (byte)255 : (byte)0;
                }
            }
            while (silTex.Count <= f) silTex.Add(null);
            var t = silTex[f];
            if (t == null || t.width != W || t.height != H)
            {
                if (t != null) TexGrave.Kill(t);
                t = silTex[f] = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Mori onion " + f };
            }
            t.SetPixelData(silBuf, 0);
            t.Apply(false, false);
            silStale[f] = false;
        }

        /// <summary>
        /// 어니언 보기 키. want 0 = 전부 · 1 = 이전만 · 2 = 다음만.
        /// 누르고 보기: 누르는 동안만(키 반복은 무시) — 떼면 누르기 전 상태(켬 · 끔 · 한쪽만)로.
        /// 계속 보기: 이미 그 모습이면 끄고, 아니면 그 모습으로 켬.
        /// </summary>
        void OnionViewKey(int want, KeyCode key)
        {
            if (onionHold)
            {
                if (onionHoldKey == key) return;                       // 누르고 있는 중(키 반복)
                if (onionHoldKey == KeyCode.None) { holdSavedOn = onion; holdSavedSolo = onionSolo; }
                onionHoldKey = key;
                onion = true; onionSolo = want;
            }
            else if (onion && onionSolo == want) onion = false;
            else { onion = true; onionSolo = want; }
            OnionChanged();
        }

        void EndOnionHold()
        {
            onionHoldKey = KeyCode.None;
            onion = holdSavedOn; onionSolo = holdSavedSolo;
            OnionChanged();
        }

        void ToggleOnionHold()
        {
            if (onionHoldKey != KeyCode.None) EndOnionHold();
            onionHold = !onionHold;
            PlayerPrefs.SetInt("mori.editor.onionHold", onionHold ? 1 : 0);
            Say("어니언 보기 키: " + (onionHold ? "누르고 보기(누르는 동안만)" : "계속 보기(누를 때마다 켜고 끔)"));
            Refresh();
        }

        /// <summary>어니언 색 견본 — 이 색이 어느 쪽인지. 누르면 그 쪽 색 고르기 줄이 열림(다시 누르면 닫힘).</summary>
        VisualElement Swatch(VisualElement p, int side)
        {
            var sw = new VisualElement(); sw.AddToClassList("ae-onion-sw");
            sw.tooltip = (side == 1 ? "이전" : "다음") + " 프레임 색 — 눌러서 바꾸기";
            sw.RegisterCallback<ClickEvent>(_ => { onionPickSide = onionPickSide == side ? 0 : side; BuildOnionPick(); });
            p.Add(sw);
            return sw;
        }

        void BuildOnionPick()
        {
            onionPrevSw.style.backgroundColor = onionPrevCol;
            onionNextSw.style.backgroundColor = onionNextCol;
            onionPrevSw.EnableInClassList("ae-onion-sw--on", onionPickSide == 1);
            onionNextSw.EnableInClassList("ae-onion-sw--on", onionPickSide == 2);
            onionPick.Clear();
            onionPick.style.display = onionPickSide == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (onionPickSide == 0) return;
            int side = onionPickSide;
            Lbl(onionPick, side == 1 ? "이전 색" : "다음 색", "ae-k");
            foreach (var (name, c) in ONION_COLORS)
            {
                var cc = c;
                var b = new VisualElement(); b.AddToClassList("ae-onion-pc");
                b.style.backgroundColor = c;
                b.tooltip = name;
                b.EnableInClassList("ae-onion-pc--on", Same(c, side == 1 ? onionPrevCol : onionNextCol));
                b.RegisterCallback<ClickEvent>(_ => SetOnionCol(side, cc));
                onionPick.Add(b);
            }
            Btn(onionPick, "지금 색으로", () => { var m = (Color)colors.Main; m.a = 1; SetOnionCol(side, m); }, "편집기에서 고른 메인 색을 이 쪽 색으로");
            Btn(onionPick, "기본", () => SetOnionCol(side, side == 1 ? ONION_PREV : ONION_NEXT), "기본 색으로(이전 = 빨강 · 다음 = 파랑)");
            Btn(onionPick, "닫기", () => { onionPickSide = 0; BuildOnionPick(); }, "색 고르기 닫기");
            static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;
        }

        void SetOnionCol(int side, Color c)
        {
            if (side == 1) { onionPrevCol = c; PlayerPrefs.SetString("mori.editor.onionPrevCol", ColorUtility.ToHtmlStringRGB(c)); }
            else { onionNextCol = c; PlayerPrefs.SetString("mori.editor.onionNextCol", ColorUtility.ToHtmlStringRGB(c)); }
            if (!onion) onion = true;
            BuildOnionPick();
            OnionChanged();
        }

        static float Lum(byte[] p, int k) => (0.299f * p[k] + 0.587f * p[k + 1] + 0.114f * p[k + 2]) / 255f;

        void MarkSilAll() { for (int i = 0; i < silStale.Count; i++) silStale[i] = true; }

        void OnionChanged()
        {
            PlayerPrefs.SetInt("mori.editor.onionPrev", onionPrev); PlayerPrefs.SetInt("mori.editor.onionNext", onionNext);
            PlayerPrefs.SetInt("mori.editor.onionStyle", onionStyle); PlayerPrefs.SetInt("mori.editor.onionAlpha", onionAlpha);
            PlayerPrefs.SetInt("mori.editor.onionOver", onionOver ? 1 : 0);
            ApplyOnionOrder();
            Refresh();
        }

        /// <summary>보기 방식 다음으로. 예전 합성으로 넘어가면 예전처럼 ‘아래에’ 로(다시 ‘위에’ 로 바꿀 수 있음).</summary>
        void NextOnionStyle()
        {
            onionStyle = ONION_STYLE_ORDER[(Array.IndexOf(ONION_STYLE_ORDER, onionStyle) + 1) % ONION_STYLE_ORDER.Length];
            if (onionStyle == 3) onionOver = false;
            MarkSilAll();
            OnionChanged();
        }

        /// <summary>
        /// 위에 = 지금 그림 위, 아래에 = 밑. 어니언끼리는 이전 프레임(빨강)이 다음 프레임보다 항상 위,
        /// 같은 쪽에서는 가까운 프레임이 위. 아래 → 위: 뒤 3 · 2 · 1 → 앞 3 · 2 · 1.
        /// </summary>
        void ApplyOnionOrder()
        {
            if (rigFrontEl == null || rigBackEl == null || onionEls.Count < 6) return;
            var order = new[] { onionEls[5], onionEls[3], onionEls[1], onionEls[4], onionEls[2], onionEls[0] };   // [k*2] = 앞, [k*2+1] = 뒤
            if (onionOver)
            {
                var at = rigFrontEl;
                foreach (var el in order) { el.PlaceInFront(at); at = el; }
            }
            else
            {
                var at = rigBackEl;
                for (int i = order.Length - 1; i >= 0; i--) { order[i].PlaceBehind(at); at = order[i]; }
            }
        }

        /// <summary>어니언으로 보일 프레임의 윤곽선 · 실루엣이 낡았으면 새로(그 프레임을 다시 합침).</summary>
        void TickOnion()
        {
            if (!onion || play || onionStyle == 3 || frames.Count < 2) return;
            bool changed = false;
            foreach (int f in OnionFrames())
            {
                bool need = f >= silStale.Count || silStale[f] || f >= silTex.Count || silTex[f] == null || silTex[f].width != W || silTex[f].height != H;
                if (!need) continue;
                ComposeFull(f);
                MakeSil(f);
                changed = true;
            }
            if (changed) Layout();
        }

        IEnumerable<int> OnionFrames()
        {
            for (int k = 0; k < 3; k++)
            {
                int pf = fi - 1 - k, nf = fi + 1 + k;
                if (loop && frames.Count > 2) { pf = (pf + frames.Count) % frames.Count; nf %= frames.Count; }
                if (k < onionPrev && pf >= 0 && pf < frames.Count && pf != fi) yield return pf;
                if (k < onionNext && nf >= 0 && nf < frames.Count && nf != fi) yield return nf;
            }
        }

        /// <summary>구조가 바뀐 뒤(프레임 · 레이어 · 속성 · 되돌리기). 큰 그림이면 지금 프레임만 바로, 나머지는 Tick 에서.</summary>
        void RecomposeAll()
        {
            foreach (var fr in frames) { while (fr.Cels.Count < rows.Count) fr.Cels.Add(null); while (fr.Cels.Count > rows.Count) fr.Cels.RemoveAt(fr.Cels.Count - 1); }
            fi = Mathf.Clamp(fi, 0, Math.Max(0, frames.Count - 1));
            ri = Mathf.Clamp(ri, 0, Math.Max(0, rows.Count - 1));
            PruneSel();
            dirtyTiles.Clear(); dirtyCel = null;
            for (int i = frameTex.Count - 1; i >= frames.Count; i--) { if (frameTex[i] != null) TexGrave.Kill(frameTex[i]); frameTex.RemoveAt(i); }
            for (int i = silTex.Count - 1; i >= frames.Count; i--) { if (silTex[i] != null) TexGrave.Kill(silTex[i]); silTex.RemoveAt(i); }
            while (silStale.Count > frames.Count) silStale.RemoveAt(silStale.Count - 1);
            MarkSilAll();
            while (stale.Count < frames.Count) stale.Add(true);
            while (stale.Count > frames.Count) stale.RemoveAt(stale.Count - 1);
            bool lazy = (long)W * H * frames.Count > BIG;
            for (int i = 0; i < frames.Count; i++)
            {
                bool hasTex = i < frameTex.Count && frameTex[i] != null && frameTex[i].width == W && frameTex[i].height == H;
                if (!lazy || i == fi || !hasTex) ComposeFrame(i);
                else stale[i] = true;
            }
            PushLive();
            Refresh();
        }

        /// <summary>미뤄 둔 프레임을 조금씩(한 번에 약 백만 픽셀).</summary>
        void CatchUp()
        {
            long budget = BIG;
            for (int i = 0; i < frames.Count && budget > 0; i++)
            {
                int k = (fi + i) % frames.Count;          // 지금 프레임 가까이부터
                if (k >= stale.Count || !stale[k]) continue;
                ComposeFrame(k);
                budget -= (long)W * H;
            }
        }

        /// <summary>점 하나를 셀에 — 바뀐 조각을 기억(화면 갱신 때 그 조각만 다시 합침).</summary>
        void Touch(Cel cel, int tile)
        {
            if (tile < 0) return;
            if (dirtyCel != null && dirtyCel != cel) Flush();
            dirtyCel = cel;
            dirtyTiles.Add(tile);
            if (cel == strokeCel) strokeTiles.Add(tile);
        }

        (Texture2D tex, byte[] buf) Stage(int tw, int th)
        {
            int k = (tw << 16) | th;
            if (!stagePool.TryGetValue(k, out var list)) stagePool[k] = list = new List<(Texture2D, byte[])>();
            stageUsed.TryGetValue(k, out int used);
            stageUsed[k] = used + 1;
            if (used < list.Count) return list[used];
            var s = (new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Mori edit stage" }, new byte[tw * th * 4]);
            list.Add(s);
            return s;
        }

        /// <summary>기억해 둔 조각만 다시 합쳐 올린다(그 셀을 쓰는 모든 프레임).</summary>
        void Flush()
        {
            if (dirtyCel == null || dirtyTiles.Count == 0) { dirtyTiles.Clear(); return; }
            var c = dirtyCel;
            int n = TilesX * TilesY;
            stageUsed.Clear();
            for (int f = 0; f < frames.Count; f++)
            {
                if (!frames[f].Cels.Contains(c)) continue;
                bool hasTex = f < frameTex.Count && frameTex[f] != null && frameTex[f].width == W && frameTex[f].height == H;
                while (silStale.Count <= f) silStale.Add(true);
                silStale[f] = true;                                  // 어니언 윤곽선은 나중에(보일 때) 다시
                if (!hasTex || !canCopy || dirtyTiles.Count * 2 > n || (f < stale.Count && stale[f])) { ComposeFrame(f); continue; }
                var L = MainLayers();
                foreach (int i in dirtyTiles)
                {
                    if (i < 0 || i >= n) continue;
                    TileRect(i, out int x0, out int y0, out int tw, out int th);
                    FillLayers(frames[f], i, L);
                    var (st, buf) = Stage(tw, th);
                    TileComp.Compose(L, tw, th, mainScratch, buf, 0, 0, tw, th, true);
                    st.SetPixelData(buf, 0);
                    st.Apply(false, false);
                    UnityEngine.Graphics.CopyTexture(st, 0, 0, 0, 0, tw, th, frameTex[f], 0, 0, x0, H - y0 - th);
                }
            }
            dirtyTiles.Clear();
        }

        /// <summary>지금 프레임의 (x, y) 에 보이는 색(합친 결과).</summary>
        Color32 ComposedAt(int x, int y)
        {
            int ts = TileImage.T, i = (y / ts) * TilesX + x / ts;
            TileRect(i, out int x0, out int y0, out int tw, out int th);
            if (pickBuf.Length < tw * th * 4) pickBuf = new byte[TileImage.T * TileImage.T * 4];
            var buf = pickBuf;
            var L = MainLayers();
            FillLayers(F, i, L);
            TileComp.Compose(L, tw, th, mainScratch, buf, 0, 0, tw, th, false);
            int k = ((y - y0) * tw + (x - x0)) * 4;
            return new Color32(buf[k], buf[k + 1], buf[k + 2], buf[k + 3]);
        }

        void PushLive()
        {
            if (key == null || frameTex.Count == 0) return;
            ArtLib.SetLive(key, frameTex.ToArray(), frames.Select((f, i) => MsOf(i)).ToArray(), once, foot, centerPivot);
        }

        // 어니언을 그림 앞 · 뒤에 놓는 기준점(빈 칸)
        VisualElement rigBackEl, rigFrontEl;

        // ───────── 프레임 · 셀 · 레이어 조작 ─────────
        bool Edit() { if (readOnly) { Say("보기 전용 파일이에요."); return false; } return true; }

        // ───────── 여러 프레임 · 레이어 고르기 ─────────
        //  누르기 = 하나만 · Ctrl+누르기 = 더하기/빼기 · Shift+누르기 = 범위. 지금 프레임 · 레이어는 늘 포함.
        //  길이 · 삭제 · 복제 · 옮김 · 칸(새 그림 · 유지 · 연결 끊기 · 비우기) · 레이어 속성(보임 · 잠금 · 클리핑 · 불투명도) · 삭제 · 옮김 = 고른 것 전부.
        readonly HashSet<Frame> fSel = new HashSet<Frame>();
        readonly HashSet<Row> rSel = new HashSet<Row>();
        int fAnchor = -1, rAnchor = -1;

        List<int> SelFrames()
        {
            var l = new List<int>();
            for (int i = 0; i < frames.Count; i++) if (i == fi || fSel.Contains(frames[i])) l.Add(i);
            return l;
        }

        List<int> SelRows()
        {
            var l = new List<int>();
            for (int i = 0; i < rows.Count; i++) if (i == ri || rSel.Contains(rows[i])) l.Add(i);
            return l;
        }

        void PruneSel()
        {
            fSel.RemoveWhere(x => !frames.Contains(x));
            rSel.RemoveWhere(x => !rows.Contains(x));
        }

        void FrameClick(int f, bool ctrl, bool shift)
        {
            if (f < 0 || f >= frames.Count) return;
            if (xf != null) CommitTransform();
            if (shift)
            {
                int a = fAnchor >= 0 && fAnchor < frames.Count ? fAnchor : fi;
                fSel.Clear();
                for (int i = Math.Min(a, f); i <= Math.Max(a, f); i++) fSel.Add(frames[i]);
                fi = f;
            }
            else if (ctrl)
            {
                if (fi < frames.Count) fSel.Add(frames[fi]);
                if (fSel.Contains(frames[f]) && fSel.Count > 1)
                {
                    fSel.Remove(frames[f]);
                    if (f == fi) fi = frames.IndexOf(fSel.First(x => frames.Contains(x)));
                }
                else { fSel.Add(frames[f]); fi = f; }
                fAnchor = f;
            }
            else { fSel.Clear(); fAnchor = f; fi = f; }
            play = false;
            Refresh();
        }

        void RowClick(int r, bool ctrl, bool shift)
        {
            if (r < 0 || r >= rows.Count) return;
            if (xf != null) CommitTransform();
            if (shift)
            {
                int a = rAnchor >= 0 && rAnchor < rows.Count ? rAnchor : ri;
                rSel.Clear();
                for (int i = Math.Min(a, r); i <= Math.Max(a, r); i++) rSel.Add(rows[i]);
                ri = r;
            }
            else if (ctrl)
            {
                if (ri < rows.Count) rSel.Add(rows[ri]);
                if (rSel.Contains(rows[r]) && rSel.Count > 1)
                {
                    rSel.Remove(rows[r]);
                    if (r == ri) ri = rows.IndexOf(rSel.First(x => rows.Contains(x)));
                }
                else { rSel.Add(rows[r]); ri = r; }
                rAnchor = r;
            }
            else { rSel.Clear(); rAnchor = r; ri = r; }
            Refresh();
        }

        void SelAllFrames() { fSel.Clear(); foreach (var f in frames) fSel.Add(f); Refresh(); Say($"프레임 {frames.Count}개 모두 고름"); }
        void SelAllRows() { rSel.Clear(); foreach (var r in rows) rSel.Add(r); Refresh(); Say($"레이어 {rows.Count}개 모두 고름"); }
        void ClearTlSel() { fSel.Clear(); rSel.Clear(); Refresh(); }

        string SelText()
        {
            int nf = SelFrames().Count, nr = SelRows().Count;
            if (nf <= 1 && nr <= 1) return "";
            return $"   ·   고름: 프레임 {nf}개 · 레이어 {nr}개";
        }

        void SetDur(Func<int, int> f)
        {
            if (frames.Count == 0 || readOnly) return;
            SnapStruct();
            foreach (int i in SelFrames()) frames[i].Dur = Mathf.Clamp(f(frames[i].Dur), 1, 999);
            dirty = true; PushLive(); Refresh();
        }

        void Go(int f)
        {
            if (frames.Count == 0) return;
            fi = Mathf.Clamp(f, 0, frames.Count - 1);
            fSel.Clear(); fAnchor = fi;
            play = false;
            Refresh();
        }

        void AddFrame(int after, bool copy)
        {
            if (!Edit()) return;
            SnapStruct();
            var src = frames[Mathf.Clamp(after, 0, frames.Count - 1)];
            var fr = new Frame { Dur = src.Dur };
            var map = new Dictionary<Cel, Cel>();
            foreach (var c in src.Cels)
            {
                if (!copy || c == null) { fr.Cels.Add(null); continue; }
                if (!map.TryGetValue(c, out var cc)) map[c] = cc = new Cel { Img = c.Img.Clone() };
                fr.Cels.Add(cc);
            }
            frames.Insert(Mathf.Clamp(after + 1, 0, frames.Count), fr);
            fi = Mathf.Clamp(after + 1, 0, frames.Count - 1);
            fSel.Clear();
            dirty = true;
            RecomposeAll();
        }

        /// <summary>고른 프레임들을 복사해 마지막 고른 프레임 뒤에(순서대로). 하나면 AddFrame 과 같음.</summary>
        void DupFrames()
        {
            var idx = SelFrames();
            if (idx.Count <= 1) { AddFrame(fi, true); return; }
            if (!Edit()) return;
            SnapStruct();
            var map = new Dictionary<Cel, Cel>();
            var copies = new List<Frame>();
            foreach (int i in idx)
            {
                var src = frames[i];
                var fr = new Frame { Dur = src.Dur };
                foreach (var c in src.Cels)
                {
                    if (c == null) { fr.Cels.Add(null); continue; }
                    if (!map.TryGetValue(c, out var cc)) map[c] = cc = new Cel { Img = c.Img.Clone() };     // 이어 쓰던 칸은 사본끼리도 이어짐
                    fr.Cels.Add(cc);
                }
                copies.Add(fr);
            }
            int at = idx[idx.Count - 1] + 1;
            frames.InsertRange(at, copies);
            fSel.Clear(); foreach (var c in copies) fSel.Add(c);
            fi = at;
            dirty = true;
            RecomposeAll();
            Say($"프레임 {copies.Count}개 복제");
        }

        void DelFrame()
        {
            if (!Edit()) return;
            var idx = SelFrames();
            if (idx.Count >= frames.Count) { Say("프레임은 하나 이상 있어야 해요 — 전부 고르면 지울 수 없어요."); return; }
            SnapStruct();
            for (int k = idx.Count - 1; k >= 0; k--) frames.RemoveAt(idx[k]);
            fi = Mathf.Clamp(idx[0] - 1, 0, frames.Count - 1);
            fSel.Clear();
            dirty = true;
            RecomposeAll();
            if (idx.Count > 1) Say($"프레임 {idx.Count}개 지움");
        }

        void MoveFrame(int d)
        {
            var idx = SelFrames();
            if (!Edit() || idx.Count == 0 || (d < 0 && idx[0] == 0) || (d > 0 && idx[idx.Count - 1] == frames.Count - 1)) return;
            SnapStruct();
            var cur = frames[fi];
            var keep = idx.Select(i => frames[i]).ToList();
            if (d < 0) foreach (int i in idx) (frames[i], frames[i - 1]) = (frames[i - 1], frames[i]);
            else for (int k = idx.Count - 1; k >= 0; k--) { int i = idx[k]; (frames[i], frames[i + 1]) = (frames[i + 1], frames[i]); }
            fi = frames.IndexOf(cur);
            fSel.Clear(); foreach (var f in keep) fSel.Add(f);
            dirty = true;
            RecomposeAll();
        }

        /// <summary>고른 프레임 × 고른 레이어 칸마다(프레임 순서대로).</summary>
        void ForSelCels(Action<int, int> a)
        {
            var rs = SelRows();
            foreach (int f in SelFrames()) foreach (int r in rs) if (!rows[r].Locked) a(f, r);
        }

        void CelNew()
        {
            if (!Edit()) return;
            SnapStruct();
            ForSelCels((f, r) => frames[f].Cels[r] = NewCel());
            dirty = true;
            RecomposeAll();
        }

        void CelHold()
        {
            if (!Edit()) return;
            if (SelFrames().All(f => f == 0)) { Say("첫 프레임은 이어 쓸 앞 그림이 없어요."); return; }
            SnapStruct();
            ForSelCels((f, r) => { if (f > 0) frames[f].Cels[r] = frames[f - 1].Cels[r]; });
            dirty = true;
            RecomposeAll();
        }

        void CelUnlink()
        {
            if (!Edit()) return;
            SnapStruct();
            ForSelCels((f, r) => { var c = frames[f].Cels[r]; if (c != null) frames[f].Cels[r] = new Cel { Img = c.Img.Clone() }; });
            dirty = true;
            RecomposeAll();
        }

        void CelClear()
        {
            if (!Edit()) return;
            SnapStruct();
            ForSelCels((f, r) => frames[f].Cels[r] = null);
            dirty = true;
            RecomposeAll();
        }

        void CelShareAll()
        {
            if (!Edit() || CurCel == null) { Say("지금 칸에 그림이 있어야 해요."); return; }
            SnapStruct();
            var c = CurCel;
            foreach (var fr in frames) fr.Cels[ri] = c;
            dirty = true;
            RecomposeAll();
        }

        /// <summary>겹치지 않는 레이어 이름(있으면 뒤에 2 · 3 …).</summary>
        string UniqueRowName(string want, Row except = null)
        {
            want = string.IsNullOrWhiteSpace(want) ? "레이어" : want.Trim();
            bool Taken(string n) => rows.Any(r => r != except && r.Name == n);
            if (!Taken(want)) return want;
            for (int i = 2; i < 1000; i++) { string n = $"{want} {i}"; if (!Taken(n)) return n; }
            return want + " " + DateTime.Now.ToString("HHmmss");
        }

        /// <summary>겹치는 레이어 이름을 고침(뒤쪽 것에 번호) — 고친 개수.</summary>
        int FixDupNames()
        {
            int n = 0;
            for (int i = 0; i < rows.Count; i++)
                for (int j = 0; j < i; j++)
                    if (rows[j].Name == rows[i].Name) { rows[i].Name = UniqueRowName(rows[i].Name, rows[i]); n++; break; }
            if (n > 0) Say($"같은 이름 레이어 {n}개의 이름을 나눴어요(다시 열 때 하나로 합쳐지지 않게).");
            return n;
        }

        void AddRow()
        {
            if (!Edit()) return;
            SnapStruct();
            int at = rows.Count == 0 ? 0 : ri + 1;
            rows.Insert(at, new Row { Name = UniqueRowName("레이어 " + (rows.Count + 1)) });
            foreach (var fr in frames) fr.Cels.Insert(at, null);
            ri = at;
            dirty = true;
            RecomposeAll();
        }

        void DelRow()
        {
            if (!Edit()) return;
            var idx = SelRows();
            if (idx.Count >= rows.Count) { Say("레이어는 하나 이상 있어야 해요 — 전부 고르면 지울 수 없어요."); return; }
            SnapStruct();
            for (int k = idx.Count - 1; k >= 0; k--)
            {
                rows.RemoveAt(idx[k]);
                foreach (var fr in frames) fr.Cels.RemoveAt(idx[k]);
            }
            ri = Mathf.Clamp(idx[0] - 1, 0, rows.Count - 1);
            rSel.Clear();
            dirty = true;
            RecomposeAll();
            if (idx.Count > 1) Say($"레이어 {idx.Count}개 지움");
        }

        void MoveRow(int d)
        {
            var idx = SelRows();
            if (!Edit() || idx.Count == 0 || (d < 0 && idx[0] == 0) || (d > 0 && idx[idx.Count - 1] == rows.Count - 1)) return;
            SnapStruct();
            var cur = rows[ri];
            var keep = idx.Select(i => rows[i]).ToList();
            void Swap(int i, int j)
            {
                (rows[i], rows[j]) = (rows[j], rows[i]);
                foreach (var fr in frames) (fr.Cels[i], fr.Cels[j]) = (fr.Cels[j], fr.Cels[i]);
            }
            if (d < 0) foreach (int i in idx) Swap(i, i - 1);
            else for (int k = idx.Count - 1; k >= 0; k--) Swap(idx[k], idx[k] + 1);
            ri = rows.IndexOf(cur);
            rSel.Clear(); foreach (var r in keep) rSel.Add(r);
            dirty = true;
            RecomposeAll();
        }

        /// <summary>고른 레이어 전부에.</summary>
        void RowProp(Action<Row> a)
        {
            if (!Edit() || R == null) return;
            SnapStruct();
            foreach (int r in SelRows()) a(rows[r]);
            dirty = true;
            RecomposeAll();
        }

        /// <summary>레이어 하나에만(타임라인의 보 · 잠 버튼).</summary>
        void RowPropOne(int r, Action<Row> a)
        {
            if (!Edit() || r < 0 || r >= rows.Count) return;
            SnapStruct();
            a(rows[r]);
            dirty = true;
            RecomposeAll();
        }

        /// <summary>프레임이 하나이고 레이어가 여럿일 때: 레이어 하나 = 프레임 하나(아래 레이어부터).</summary>
        void SplitLayersToFrames()
        {
            if (!Edit()) return;
            if (frames.Count != 1 || rows.Count < 2) { Say("프레임이 하나이고 레이어가 둘 이상일 때 쓸 수 있어요."); return; }
            SnapStruct();
            var src = frames[0];
            var nf = new List<Frame>();
            for (int r = 0; r < rows.Count; r++)
            {
                if (src.Cels[r] == null) continue;
                nf.Add(new Frame { Dur = src.Dur, Cels = new List<Cel> { src.Cels[r] } });
            }
            if (nf.Count == 0) { undo.RemoveAt(undo.Count - 1); Say("나눌 그림이 없어요."); return; }
            rows = new List<Row> { new Row { Name = "그림" } };
            frames = nf;
            fi = 0; ri = 0;
            dirty = true;
            RecomposeAll();
            Say($"레이어 {nf.Count}장을 프레임 {nf.Count}개로 나눴어요 — ▶ 재생으로 확인(되돌리기 Ctrl+Z).");
        }

        /// <summary>되돌릴 수 없는 캔버스 작업: 한 번 묻고, 하기 직전 지금 파일을 백업(프로젝트 밖)에 남김.</summary>
        void AskCanvas(string what, Action go)
        {
            Dialog.Confirm("캔버스 크기 바꾸기", what + "\n되돌리기(Ctrl+Z)가 안 되는 작업이에요. 하기 직전에 지금 저장된 파일을 백업 폴더에 남겨 둘게요.", "바꾸기", true, () =>
            {
                if (dirty) Save();                      // 지금 그린 것까지 저장한 뒤
                KitBackup.Before(path, true);           // 그 파일을 백업
                go();
            });
        }

        bool CanvasLocked()
        {
            if (!canvasLock) return false;
            Say("캔버스 크기가 잠겨 있어요 — ‘잠금’ 을 끄고 바꿔 주세요.");
            return true;
        }

        /// <summary>그림째 크기 바꾸기: 모든 프레임 · 레이어를 새 크기로 늘이거나 줄임(가장 가까운 점 — 도트가 번지지 않음).</summary>
        void ScaleCanvas(int nw, int nh)
        {
            if (!Edit()) return;
            nw = Mathf.Clamp(nw, 1, MAX); nh = Mathf.Clamp(nh, 1, MAX);
            if (nw == W && nh == H) return;
            var done = new HashSet<Cel>();
            foreach (var fr in frames)
                foreach (var c in fr.Cels)
                {
                    if (c == null || !done.Add(c)) continue;
                    var src = c.Img.ToRgba();
                    var dst = new byte[nw * nh * 4];
                    for (int y = 0; y < nh; y++)
                    {
                        int sy = Math.Min(H - 1, (int)((y + 0.5) * H / nh));
                        for (int x = 0; x < nw; x++)
                        {
                            int sx = Math.Min(W - 1, (int)((x + 0.5) * W / nw));
                            Buffer.BlockCopy(src, (sy * W + sx) * 4, dst, (y * nw + x) * 4, 4);
                        }
                    }
                    c.Img = TileImage.FromRgba(dst, nw, nh);
                }
            if (xf != null) CommitTransform();
            int ow = W, oh = H;
            foot = Mathf.Clamp(Mathf.RoundToInt(foot * nh / (float)Mathf.Max(1, H)), 0, nh - 1);
            footField?.SetValueWithoutNotify(foot);
            W = nw; H = nh;
            rwField.SetValueWithoutNotify(W); rhField.SetValueWithoutNotify(H);
            poly = null; SetSel(null);
            undo.Clear(); redo.Clear();
            dirty = true;
            FitZoom();
            RecomposeAll();
            Say($"그림째 {ow}×{oh} → {W}×{H} — 되돌릴 수 없어요(저장 전이면 파일을 다시 열면 원래대로).");
        }

        // ───────── 캔버스 기준(발밑 · 가운데) ─────────
        void TogglePivot()
        {
            if (readOnly) { Say("시트식 파일은 보기만 해요."); return; }
            centerPivot = !centerPivot;
            dirty = true;
            PaintPivot();
            overlay.MarkDirtyRepaint();
            PushLive();
            Say(centerPivot ? "캔버스 기준 = 가운데(이펙트용) — 캔버스 정가운데가 기준점이에요(십자선)." : "캔버스 기준 = 발밑(캐릭터) — 캔버스 아래 가운데 · 발밑 선이 기준점이에요.");
        }

        void PaintPivot()
        {
            if (pivotBtn == null) return;
            pivotBtn.text = centerPivot ? "기준: 가운데(이펙트용)" : "기준: 발밑(캐릭터)";
            pivotBtn.EnableInClassList("ae-btn--on", centerPivot);
        }

        // ───────── 발밑 선 ─────────
        void SetFoot(int v)
        {
            v = Mathf.Clamp(v, 0, Math.Max(0, H - 1));
            footField.SetValueWithoutNotify(v);
            if (v == foot) return;
            if (readOnly) { footField.SetValueWithoutNotify(foot); Say("시트식 파일은 보기만 해요."); return; }
            foot = v;
            dirty = true;
            overlay.MarkDirtyRepaint();
            PushLive();
            Say($"발밑 선 = 아래에서 {foot}px" + (foot > 0 ? " — 그 아래는 바닥을 뚫는 자리" : " (캔버스 맨 아래)"));
        }

        /// <summary>같은 캐릭터의 다른 그림 파일에 쓰인 발밑 선(가장 많이 쓰인 값, 없으면 0).</summary>
        int SharedFoot(string k)
        {
            int dash = k.IndexOf('-');
            if (dash <= 0) return 0;
            string pre = k.Substring(0, dash + 1);
            var vals = ArtLib.Files().Where(f => f.key.StartsWith(pre) && f.key != k).Select(f => ArtLib.FootOf(Path.GetFileNameWithoutExtension(f.path))).ToList();
            return vals.Count == 0 ? 0 : vals.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;
        }

        void ExtendDownAsk(int n)
        {
            if (CanvasLocked()) return;
            AskCanvas($"캔버스를 아래로 {n}px 늘림({W}×{H} → {W}×{H + n}) · 발밑 선 {foot} → {foot + n}", () => ExtendDown(n));
        }

        /// <summary>캔버스를 아래로만 늘림: 그림은 제자리(위 기준), 아래에 빈 줄 · 발밑 선도 그만큼 올림 → 결투에서 캐릭터 위치 그대로.</summary>
        void ExtendDown(int n)
        {
            if (!Edit()) return;
            int nh = Mathf.Clamp(H + n, 4, MAX);
            if (nh == H) return;
            int add = nh - H;
            var done = new HashSet<Cel>();
            foreach (var fr in frames)
                foreach (var c in fr.Cels)
                {
                    if (c == null || !done.Add(c)) continue;
                    var old = c.Img;
                    var ni = new TileImage(W, nh);
                    for (int i = 0; i < old.Count; i++)
                    {
                        if (old.Tiles[i] == null) continue;
                        old.TileRect(i, out int x0, out int y0, out int tw, out int th);
                        ni.Blit(old.Tiles[i], tw, th, x0, y0);
                    }
                    c.Img = ni;
                }
            if (xf != null) CommitTransform();
            H = nh;
            foot = Mathf.Clamp(foot + add, 0, H - 1);
            footField.SetValueWithoutNotify(foot);
            rwField.SetValueWithoutNotify(W); rhField.SetValueWithoutNotify(H);
            poly = null; SetSel(null);
            undo.Clear(); redo.Clear();
            dirty = true;
            FitZoom();
            RecomposeAll();
            Say($"캔버스 아래로 +{add}px → {W}×{H} · 발밑 선 {foot}px — 캐릭터는 제자리예요(되돌릴 수 없음).");
        }

        /// <summary>같은 캐릭터 그림 파일 모두 이 발밑 선으로(파일 이름의 ‘발밑N’ 만 바꿈 — 그림 내용은 그대로).</summary>
        void ApplyFootAll()
        {
            if (key == null) return;
            int dash = key.IndexOf('-');
            if (dash <= 0) { Say("캐릭터 그림(hero- · foe-)에서만 쓸 수 있어요."); return; }
            string pre = key.Substring(0, dash + 1);
            Dialog.Confirm("발밑 선 맞추기", $"‘{pre}’ 로 시작하는 모든 그림 파일의 발밑 선을 {foot}px 로 맞출까요?\n파일 이름만 바뀌고 그림은 그대로예요. 캔버스 높이가 다른 그림도 아래에서 {foot}px 가 바닥이 돼요.", "맞추기", false, () =>
            {
                if (dirty) Save();
                int n = 0;
                foreach (var f in ArtLib.Files().Where(x => x.key.StartsWith(pre)).ToList())
                {
                    try
                    {
                        string nm = Path.GetFileNameWithoutExtension(f.path);
                        var toks = ArtLib.Tokens(nm).ToList();
                        if (toks.Count == 0) continue;
                        var rest = toks.Skip(1).Where(t => !ArtLib.IsFootToken(t)).ToList();
                        if (foot > 0) rest.Add("발밑" + foot);
                        string nn = toks[0] + string.Concat(rest.Select(t => " · " + t)) + Path.GetExtension(f.path);
                        string target = Path.Combine(Path.GetDirectoryName(f.path) ?? ArtLib.Dir, nn);
                        if (string.Equals(Path.GetFullPath(target), Path.GetFullPath(f.path), StringComparison.OrdinalIgnoreCase)) continue;
                        if (File.Exists(target)) continue;
                        KitBackup.Before(f.path, true);
                        File.Move(f.path, target);
                        if (string.Equals(Path.GetFullPath(f.path), Path.GetFullPath(path ?? ""), StringComparison.OrdinalIgnoreCase)) path = target;
                        n++;
                    }
                    catch (Exception e) { Debug.LogWarning("[Mori 킷] 발밑 선 맞추기 실패: " + e.Message); }
                }
                BuildFiles();
                Say(n > 0 ? $"‘{pre}’ 그림 {n}개의 발밑 선을 {foot}px 로 맞췄어요." : "바꿀 파일이 없었어요(이미 같은 값).");
            });
        }

        void Resize(int nw, int nh)
        {
            if (!Edit()) return;
            nw = Mathf.Clamp(nw, 4, MAX); nh = Mathf.Clamp(nh, 4, MAX);
            if (nw == W && nh == H) return;
            int dx = (nw - W) / 2, dy = nh - H;           // 아래 가운데 기준(발 위치 유지)
            var done = new HashSet<Cel>();
            foreach (var fr in frames)
                foreach (var c in fr.Cels)
                {
                    if (c == null || !done.Add(c)) continue;
                    var old = c.Img;
                    var ni = new TileImage(nw, nh);
                    for (int i = 0; i < old.Count; i++)
                    {
                        if (old.Tiles[i] == null) continue;
                        old.TileRect(i, out int x0, out int y0, out int tw, out int th);
                        ni.Blit(old.Tiles[i], tw, th, x0 + dx, y0 + dy);
                    }
                    c.Img = ni;
                }
            if (xf != null) CommitTransform();
            W = nw; H = nh;
            foot = Mathf.Clamp(foot, 0, H - 1); footField?.SetValueWithoutNotify(foot);
            poly = null; SetSel(null);
            undo.Clear(); redo.Clear();          // 크기가 바뀐 그림은 되돌릴 수 없음
            dirty = true;
            FitZoom();
            RecomposeAll();
            Say($"캔버스 {W}×{H} — 크기 바꾸기는 되돌릴 수 없어요(저장 전이면 파일을 다시 열면 원래대로).");
        }

        // ───────── 그리기 ─────────
        bool drawing, panning, picking, lassoDraw;
        bool pickToPen = PlayerPrefs.GetInt("mori.editor.pickToPen", 0) == 1;
        Button pickPenBtn;
        Vector2Int downPx, lastPx, hoverPx = new Vector2Int(-1, -1);
        Vector2 panStart, panMouse;
        Cel strokeCel;

        Vector2Int PxAt(Vector2 viewLocal)
        {
            var o = Origin();
            return new Vector2Int(Mathf.FloorToInt((viewLocal.x - o.x) / zoom), Mathf.FloorToInt((viewLocal.y - o.y) / zoom));
        }

        void OnDown(PointerDownEvent e)
        {
            Root.Focus();
            if (e.button == 1 || e.button == 2 || tool == Tool.Hand || spaceHeld)
            {
                panning = true; spaceUsed = true; panStart = pan; panMouse = (Vector2)e.position;
                view.CapturePointer(e.pointerId); e.StopPropagation(); return;
            }
            if (e.button != 0 || frames.Count == 0 || rows.Count == 0) return;
            var vl = VL(e.position);
            var p = PxAt(vl);
            var pf = PxAtF(vl);
            // 옮기는 중이면 어느 도구든 끌어서 옮김
            if (xf != null) { XfDown(e, pf); e.StopPropagation(); return; }
            if (IsSelTool(tool)) { SelDown(e, pf, p); e.StopPropagation(); return; }
            bool Mod(string m) => m == "Alt" ? e.altKey : m == "Ctrl" ? (e.ctrlKey || e.commandKey) : e.shiftKey;
            if (Mod(selMod)) { SelectLayerAt(p); e.StopPropagation(); return; }
            if (tool == Tool.Picker || Mod(pickMod)) { picking = true; view.CapturePointer(e.pointerId); Pick(p); e.StopPropagation(); return; }
            if (!Edit()) return;
            if (R.Locked) { Say("잠긴 레이어예요 — 레이어 속성에서 잠금을 풀어 주세요."); return; }
            if (play) { play = false; Refresh(); }
            if (tool == Tool.LassoFill || tool == Tool.LassoErase)
            {
                if (tool == Tool.LassoErase && CurCel == null) { Say("지울 그림이 없는 칸이에요."); return; }
                if (CurCel == null) { SnapStruct(); F.Cels[ri] = NewCel(); RebuildTimeline(); }
                else SnapPixels(CurCel);
                strokeCel = CurCel;
                strokeTiles.Clear();
                lassoDraw = true;
                lasso.Clear(); lasso.Add(pf);
                view.CapturePointer(e.pointerId);
                overlay.MarkDirtyRepaint();
                e.StopPropagation(); return;
            }
            downPx = lastPx = p;
            // 빈 칸이면 새 그림을 만들고 그린다
            if (CurCel == null) { SnapStruct(); F.Cels[ri] = NewCel(); RebuildTimeline(); }
            else SnapPixels(CurCel);
            strokeCel = CurCel;
            strokeTiles.Clear();
            drawing = true;
            view.CapturePointer(e.pointerId);
            if (tool == Tool.Pen || tool == Tool.Eraser) { Stamp(p); Flush(); }
            else if (tool == Tool.Blend) { BlendAt(p); Flush(); }
            else if (tool == Tool.Fill) { Fill(p); drawing = false; view.ReleasePointer(e.pointerId); EndStroke(); }
            dirty = true;
            RefreshTitle();
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e)
        {
            if (panning) { pan = panStart + ((Vector2)e.position - panMouse); Layout(); return; }
            var vl = VL(e.position);
            var p = PxAt(vl);
            hoverPx = p;
            if (picking) Pick(p);
            hoverF = PxAtF(vl);
            if (moving && xf != null) XfDrag(hoverF, e.shiftKey, e.altKey);
            if (selecting) SelMove(hoverF, p);
            if (lassoDraw && (lasso.Count == 0 || (hoverF - lasso[lasso.Count - 1]).magnitude >= 0.5f)) lasso.Add(hoverF);
            if (poly != null) overlay.MarkDirtyRepaint();
            if (drawing && (tool == Tool.Pen || tool == Tool.Eraser))
            {
                foreach (var q in LinePts(lastPx, p)) Stamp(q);
                lastPx = p;            // 다시 합치기는 Tick 에서 한 화면에 한 번
            }
            else if (drawing && tool == Tool.Blend)
            {
                if (p != lastPx) foreach (var q in LinePts(lastPx, p)) BlendAt(q);
                lastPx = p;
            }
            else if (drawing) lastPx = p;
            overlay.MarkDirtyRepaint();
            info.text = $"{p.x}, {p.y}  ·  {W}×{H}  ·  확대 {ZoomText}  ·  굵기 {brush}  ·  {(R != null ? R.Name : "")}{(xf != null ? "  ·  변형 중" : sel != null ? $"  ·  선택 {selR - selL}×{selB - selT}" : "")}";
        }

        void OnUp(PointerUpEvent e)
        {
            if (panning) { panning = false; view.ReleasePointer(e.pointerId); return; }
            if (picking) { picking = false; view.ReleasePointer(e.pointerId); return; }
            if (moving) { XfUp(); view.ReleasePointer(e.pointerId); return; }
            if (selecting) { SelUp(e); return; }
            if (lassoDraw) { lassoDraw = false; view.ReleasePointer(e.pointerId); LassoPaint(); return; }
            if (!drawing) return;
            drawing = false;
            view.ReleasePointer(e.pointerId);
            var p = PxAt(VL(e.position));
            if (tool == Tool.Line) foreach (var q in LinePts(downPx, p)) Stamp(q);
            else if (tool == Tool.Rect) RectPx(downPx, p, e.shiftKey);
            EndStroke();
            overlay.MarkDirtyRepaint();
        }

        /// <summary>올가미 칠하기 · 지우기: 끌어서 그린 모양 안을 지금 색(지우기면 투명)으로. 선택이 있으면 그 안에만, 좌우 대칭도 따름.</summary>
        void LassoPaint()
        {
            if (lasso.Count >= 3 && strokeCel != null)
            {
                var m = PolyMask(lasso);
                var c = tool == Tool.LassoErase ? new Color32(0, 0, 0, 0) : colors.Current;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (!m[y * W + x]) continue;
                        Put(x, y, c);
                        if (mirror) Put(W - 1 - x, y, c);
                    }
                dirty = true;
                RefreshTitle();
            }
            lasso.Clear();
            EndStroke();
            overlay.MarkDirtyRepaint();
        }

        /// <summary>획 끝: 바로 올리고, 다 지워진 조각은 버림.</summary>
        void EndStroke()
        {
            Flush();
            strokeCel?.Img.Trim(strokeTiles);
            strokeTiles.Clear();
        }

        void Put(int x, int y, Color32 c)
        {
            var cel = strokeCel;
            if (cel == null || x < 0 || y < 0 || x >= W || y >= H) return;
            if (sel != null && !sel[y * W + x]) return;          // 선택이 있으면 그 안에만
            Touch(cel, cel.Img.Set(x, y, c.r, c.g, c.b, c.a));
        }

        void Stamp(Vector2Int p)
        {
            var c = tool == Tool.Eraser ? new Color32(0, 0, 0, 0) : colors.Current;
            int r0 = -(brush - 1) / 2, r1 = brush / 2;
            for (int dy = r0; dy <= r1; dy++)
                for (int dx = r0; dx <= r1; dx++)
                {
                    Put(p.x + dx, p.y + dy, c);
                    if (mirror) Put(W - 1 - (p.x + dx), p.y + dy, c);
                }
        }

        // ───────── 톤 잇기 ─────────
        void ToneUi()
        {
            if (toneModeBtn == null) return;
            int md = ToneMix.Mode;
            toneModeBtn.text = md == 0 ? "방식: 중간색" : md == 1 ? "방식: 디더" : "방식: 명도";
            toneAmtBtn.text = md == 0 ? $"단계 {ToneMix.Steps}" : md == 1 ? $"농도 {ToneMix.Dither}%" : $"명도 {ToneMix.LumLevels}단계";
            toneHueBtn.text = md == 2 ? (ToneMix.LumDither ? "단계 디더 켬" : "단계 디더 끔") : ToneMix.HueShift > 0 ? "색조 이동 켬" : "색조 이동 끔";
            toneSnapBtn.text = md == 2 ? (ToneMix.LumChroma ? "채도 살리기 켬" : "채도 살리기 끔") : ToneMix.Snap ? "팔레트 맞춤 켬" : "팔레트 맞춤 끔";
            colors?.SetRampHue(ToneMix.HueShift);
        }

        /// <summary>
        /// 톤 잇기 한 번: 브러시 둘레에서 가장 많은 두 색(A · B)을 찾아, 브러시 안의 A · B 픽셀 중 상대 색과 맞닿은 줄만 바꿈.
        ///  · 중간색: A 쪽 ⌈n/2⌉ 줄 · B 쪽 ⌊n/2⌋ 줄에 A → B 중간색 n 개를 계단으로(가까울수록 상대 색에 가깝게)
        ///  · 디더: 맞닿은 첫 줄을 체커 · 바이어 무늬로 상대 색과 섞음
        /// 안쪽(한 색만 있는 곳)은 그대로라 번지지 않는다. 투명은 셈에서 뺌.
        /// </summary>
        void BlendAt(Vector2Int p)
        {
            var cel = strokeCel;
            if (cel == null) return;
            var im = cel.Img;
            int bs = Math.Max(1, brush), n = ToneMix.Steps;
            bool lum = ToneMix.Mode == 2;
            int lumHalf = Math.Max(1, (bs + 1) / 2);                  // 명도 잇기: 경계 양쪽으로 이만큼
            int r0 = -(bs - 1) / 2, r1 = bs / 2;
            int look = Math.Max(bs, 3) + Math.Max(n, lumHalf) + 1;    // 색 고르기 · 거리 재기는 브러시보다 넓게
            int lx0 = Math.Max(0, p.x - look), lx1 = Math.Min(W - 1, p.x + look), ly0 = Math.Max(0, p.y - look), ly1 = Math.Min(H - 1, p.y + look);
            if (lx0 > lx1 || ly0 > ly1) return;
            int ww = lx1 - lx0 + 1, hh = ly1 - ly0 + 1;
            var px = new Color32[ww * hh];
            var count = new Dictionary<uint, int>();
            for (int y = ly0; y <= ly1; y++)
                for (int x = lx0; x <= lx1; x++)
                {
                    im.Get(x, y, out byte cr, out byte cg, out byte cb, out byte ca);
                    var c = new Color32(cr, cg, cb, ca);
                    px[(y - ly0) * ww + (x - lx0)] = c;
                    if (ca < 8) continue;
                    // 브러시 가까운 곳만 셈(먼 색이 끼어들지 않게)
                    if (Math.Abs(x - p.x) > Math.Max(bs, 3) || Math.Abs(y - p.y) > Math.Max(bs, 3)) continue;
                    uint k = ToneKey(c);
                    count[k] = count.TryGetValue(k, out var v) ? v + 1 : 1;
                }
            if (count.Count < 2) return;
            uint ka = 0, kb = 0; int na = -1, nb = -1;
            foreach (var kv in count)
            {
                if (kv.Value > na) { kb = ka; nb = na; ka = kv.Key; na = kv.Value; }
                else if (kv.Value > nb) { kb = kv.Key; nb = kv.Value; }
            }
            Color32 A = ToneCol(ka), B = ToneCol(kb);
            // 상대 색까지 거리(체비셰프, 최대 n+1) — 안쪽은 바꾸지 않음
            int Dist(int x, int y, uint other, int max)
            {
                for (int d = 1; d <= max; d++)
                    for (int yy = y - d; yy <= y + d; yy++)
                        for (int xx = x - d; xx <= x + d; xx++)
                        {
                            if (Math.Max(Math.Abs(xx - x), Math.Abs(yy - y)) != d) continue;
                            if (xx < lx0 || xx > lx1 || yy < ly0 || yy > ly1) continue;
                            if (ToneKey(px[(yy - ly0) * ww + (xx - lx0)]) == other) return d;
                        }
                return int.MaxValue;
            }
            int nA = (n + 1) / 2, nB = n / 2;
            List<Color32> ramp = null;
            Vector3 labA = ToneMix.ToLab(A), labB = ToneMix.ToLab(B);
            int lv = ToneMix.LumLevels;
            if (ToneMix.Mode == 0)
            {
                ramp = ToneMix.Ramp(A, B, n, ToneMix.HueShift);
                if (ToneMix.Snap) for (int i = 0; i < ramp.Count; i++) ramp[i] = ToneMix.Nearest(ramp[i], colors.Palette);
            }
            int q = ToneMix.Dither;
            bool Pat(int x, int y) => q == 50 ? ((x + y) & 1) == 0 : q == 25 ? (x % 2 == 0 && y % 2 == 0) : !(x % 2 == 1 && y % 2 == 1);
            var changes = new List<(int x, int y, Color32 c)>();
            for (int dy = r0; dy <= r1; dy++)
                for (int dx = r0; dx <= r1; dx++)
                {
                    int x = p.x + dx, y = p.y + dy;
                    if (x < lx0 || x > lx1 || y < ly0 || y > ly1) continue;
                    uint k = ToneKey(px[(y - ly0) * ww + (x - lx0)]);
                    bool isA = k == ka, isB = k == kb;
                    if (!isA && !isB) continue;
                    if (lum)
                    {
                        // 명도 잇기: 경계에서 lumHalf 줄까지 밝기만 S자로 이어 단계로 끊음 — 색조 · 채도는 제 색 그대로
                        int d = Dist(x, y, isA ? kb : ka, lumHalf);
                        if (d == int.MaxValue) continue;
                        float tt = isA ? 0.5f - (d - 0.5f) / (2f * lumHalf) : 0.5f + (d - 0.5f) / (2f * lumHalf);
                        float s = tt * tt * (3 - 2 * tt);
                        float u = s * (lv - 1);
                        int kq = ToneMix.LumDither ? Mathf.FloorToInt(u) + ((u - Mathf.Floor(u)) > ToneMix.Bayer(x, y) ? 1 : 0) : Mathf.RoundToInt(u);
                        kq = Mathf.Clamp(kq, 0, lv - 1);
                        float L = Mathf.Lerp(labA.x, labB.x, kq / (float)(lv - 1));
                        var own = isA ? labA : labB;
                        float cm = ToneMix.LumChroma ? 1f + 0.35f * 4f * s * (1 - s) : 1f;
                        var nc = ToneMix.FromLab(new Vector3(L, own.y * cm, own.z * cm), (isA ? A : B).a);
                        if (ToneKey(nc) != k) changes.Add((x, y, nc));
                    }
                    else if (ramp != null)
                    {
                        int d = Dist(x, y, isA ? kb : ka, isA ? nA : nB);
                        if (d == int.MaxValue) continue;
                        int idx = isA ? nA - d + 1 : nA + d;               // 1 ~ n (A 쪽 → B 쪽)
                        if (idx < 1 || idx > n) continue;
                        changes.Add((x, y, ramp[idx - 1]));
                    }
                    else
                    {
                        if (Dist(x, y, isA ? kb : ka, 1) != 1) continue;
                        if (Pat(x, y)) changes.Add((x, y, isA ? B : A));
                    }
                }
            foreach (var (x, y, c) in changes)
            {
                Put(x, y, c);
                if (mirror) Put(W - 1 - x, y, c);
            }
        }

        static uint ToneKey(Color32 c) => ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;
        static Color32 ToneCol(uint k) => new Color32((byte)(k >> 24), (byte)(k >> 16), (byte)(k >> 8), (byte)k);

        void RectPx(Vector2Int a, Vector2Int b, bool fill)
        {
            int x0 = Math.Min(a.x, b.x), x1 = Math.Max(a.x, b.x), y0 = Math.Min(a.y, b.y), y1 = Math.Max(a.y, b.y);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (fill || x == x0 || x == x1 || y == y0 || y == y1) Stamp(new Vector2Int(x, y));
        }

        static IEnumerable<Vector2Int> LinePts(Vector2Int a, Vector2Int b)
        {
            int x = a.x, y = a.y, dx = Math.Abs(b.x - a.x), dy = -Math.Abs(b.y - a.y), sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1, err = dx + dy;
            for (int guard = 0; guard < 8192; guard++)
            {
                yield return new Vector2Int(x, y);
                if (x == b.x && y == b.y) yield break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x += sx; }
                if (e2 <= dx) { err += dx; y += sy; }
            }
        }

        void Fill(Vector2Int p)
        {
            var cel = strokeCel;
            if (cel == null || p.x < 0 || p.y < 0 || p.x >= W || p.y >= H) return;
            var im = cel.Img;
            im.Get(p.x, p.y, out byte fr, out byte fg, out byte fb, out byte fa);
            var to = colors.Current;
            if (fr == to.r && fg == to.g && fb == to.b && fa == to.a) return;
            var st = new Stack<int>();
            st.Push(p.y * W + p.x);
            var seen = new bool[W * H];
            while (st.Count > 0)
            {
                int k = st.Pop();
                if (seen[k]) continue;
                seen[k] = true;
                int x = k % W, y = k / W;
                if (!Sel(x, y)) continue;
                im.Get(x, y, out byte cr, out byte cg, out byte cb, out byte ca);
                if (cr != fr || cg != fg || cb != fb || ca != fa) continue;
                Put(x, y, to);
                if (x > 0) st.Push(k - 1); if (x < W - 1) st.Push(k + 1); if (y > 0) st.Push(k - W); if (y < H - 1) st.Push(k + W);
            }
        }

        /// <summary>Ctrl+클릭: 그 자리에 보이는 픽셀이 있는 맨 위 레이어(지금 프레임)로 이동.</summary>
        void SelectLayerAt(Vector2Int p)
        {
            if (p.x < 0 || p.y < 0 || p.x >= W || p.y >= H || frames.Count == 0) return;
            for (int r = rows.Count - 1; r >= 0; r--)
            {
                var c = F.Cels[r];
                if (!rows[r].Visible || rows[r].Opacity <= 0 || c == null || c.Img.Alpha(p.x, p.y) == 0) continue;
                ri = r;
                Refresh();
                Say($"레이어 ‘{rows[r].Name}’ 로 이동");
                return;
            }
            Say("여기에는 그려진 레이어가 없어요.");
        }

        void Pick(Vector2Int p)
        {
            if (p.x < 0 || p.y < 0 || p.x >= W || p.y >= H || frames.Count == 0) return;
            var c = ComposedAt(p.x, p.y);
            if (c.a == 0) { Say("투명한 곳이에요 — 지우려면 지우개(E)나 색 패널의 ‘투명’ 칩."); return; }
            colors.SetMain(c);
        }

        // ───────── 보기 ─────────
        const float ZMIN = 1f / 16;
        string ZoomText => zoom >= 1 ? $"{zoom:0}×" : $"{zoom * 100:0}%";
        void FitZoom()
        {
            float vw = view.resolvedStyle.width, vh = view.resolvedStyle.height;
            if (float.IsNaN(vw) || vw < 50) vw = 600;
            if (float.IsNaN(vh) || vh < 50) vh = 400;
            float fit = Mathf.Min(vw / W, vh / H) * 0.96f;
            // 1배보다 작으면 1/2 · 1/4 … (큰 캔버스 · 배경)
            if (fit >= 1) zoom = Mathf.Clamp(Mathf.Floor(fit), 1, 64);
            else { zoom = 1; while (zoom > fit && zoom > ZMIN) zoom *= 0.5f; }
            pan = Vector2.zero;
        }

        void Zoom(int d, Vector2? at)
        {
            float old = zoom;
            var o0 = Origin();
            if (zoom < 1 || (zoom == 1 && d < 0)) zoom = Mathf.Clamp(d > 0 ? zoom * 2 : zoom * 0.5f, ZMIN, 1);
            else zoom = Mathf.Clamp(zoom + d * Mathf.Max(1, Mathf.Floor(zoom / 6)), 1, 64);
            if (at.HasValue && old != zoom)
            {
                var rel = (at.Value - o0) / old;
                pan = (at.Value - rel * zoom) - Origin(false);
            }
            Layout();
        }

        Vector2 Origin(bool withPan = true)
        {
            float vw = view.resolvedStyle.width, vh = view.resolvedStyle.height;
            if (float.IsNaN(vw)) vw = 600;
            if (float.IsNaN(vh)) vh = 400;
            return new Vector2(Mathf.Round((vw - W * zoom) / 2), Mathf.Round((vh - H * zoom) / 2)) + (withPan ? pan : Vector2.zero);
        }

        void Layout()
        {
            var o = Origin();
            canvasHost.style.left = o.x; canvasHost.style.top = o.y;
            canvasHost.style.width = W * zoom; canvasHost.style.height = H * zoom;
            if (!play) Tex(img, fi < frameTex.Count ? frameTex[fi] : null, 1, Color.white);
            // 어니언: 이전(빨강) · 다음(파랑), 멀수록 옅게 — 윤곽선 · 실루엣 · 단색 명암은 흰 · 회색 그림에 색을 입힘(색이 섞이지 않음)
            for (int k = 0; k < 3; k++)
            {
                int pf = fi - 1 - k, nf = fi + 1 + k;
                if (loop && frames.Count > 2) { pf = (pf + frames.Count) % frames.Count; nf %= frames.Count; }
                // 예전 합성 = stage22c 까지의 모습(원래 색에 빨강 · 초록을 곱하고 0.35 / (k+1)) — 진하기 70% 일 때 똑같음
                bool old = onionStyle == 3;
                float a = old ? 0.35f / (k + 1) * onionAlpha / 70f : onionAlpha / 100f * (1f - 0.3f * k);
                Texture2D OnionTex(int f, bool want)
                {
                    if (!onion || play || !want || f < 0 || f == fi || f >= frames.Count) return null;
                    if (onionStyle == 3) return f < frameTex.Count ? frameTex[f] : null;
                    return f < silTex.Count && silTex[f] != null && silTex[f].width == W && silTex[f].height == H ? silTex[f] : null;
                }
                Color Tint(Color c) => c;
                Tex(onionEls[k * 2], OnionTex(pf, k < onionPrev && onionSolo != 2), Mathf.Min(1f, a), old ? ONION_OLD_PREV : Tint(onionPrevCol));
                Tex(onionEls[k * 2 + 1], OnionTex(nf, k < onionNext && onionSolo != 1), Mathf.Min(1f, a), old ? ONION_OLD_NEXT : Tint(onionNextCol));
            }
            // 체크무늬 = 반복 텍스처 한 장(보이는 부분만) — 다시 그릴 일이 없다
            float vw = view.resolvedStyle.width, vh = view.resolvedStyle.height;
            if (float.IsNaN(vw)) vw = 600;
            if (float.IsNaN(vh)) vh = 400;
            float cl = Mathf.Max(0, -o.x), ct = Mathf.Max(0, -o.y), cr = Mathf.Min(W * zoom, vw - o.x), cb = Mathf.Min(H * zoom, vh - o.y);
            checker.style.display = cr > cl && cb > ct ? DisplayStyle.Flex : DisplayStyle.None;
            checker.style.left = cl; checker.style.top = ct; checker.style.right = StyleKeyword.Auto; checker.style.bottom = StyleKeyword.Auto;
            checker.style.width = Mathf.Max(0, cr - cl); checker.style.height = Mathf.Max(0, cb - ct);
            gridEl.MarkDirtyRepaint();
            selEl.MarkDirtyRepaint();
            overlay.MarkDirtyRepaint();
        }

        static Texture2D checkerTex, checkerLightTex;
        static Texture2D CheckerTex(bool light)
        {
            ref Texture2D t = ref light ? ref checkerLightTex : ref checkerTex;
            if (t != null) return t;
            const int N = 128, S = 8;
            var a = light ? new Color32(255, 255, 255, 255) : new Color32(51, 54, 64, 255);
            var b = light ? new Color32(204, 204, 204, 255) : new Color32(66, 69, 82, 255);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) px[y * N + x] = ((x / S + y / S) & 1) == 0 ? a : b;
            t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = light ? "Mori checker light" : "Mori checker" };
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        /// <summary>밝은 배경(밝은 체크 · 흰색)이면 격자 · 테두리를 어두운 선으로.</summary>
        bool LightBg => bgMode == 1 || bgMode == 2;

        void ApplyBg()
        {
            if (bgMode <= 1)
            {
                checker.style.backgroundImage = new StyleBackground(CheckerTex(bgMode == 1));
                checker.style.backgroundColor = StyleKeyword.Null;
            }
            else
            {
                checker.style.backgroundImage = StyleKeyword.None;
                checker.style.backgroundColor = bgMode == 2 ? Color.white : Color.black;
            }
        }

        void CycleBg()
        {
            bgMode = (bgMode + 1) % BG.Length;
            PlayerPrefs.SetInt("mori.editor.bg", bgMode);
            ApplyBg();
            Refresh();
            gridEl.MarkDirtyRepaint(); overlay.MarkDirtyRepaint();
        }

        void ToggleGrid()
        {
            grid = !grid;
            PlayerPrefs.SetInt("mori.editor.grid", grid ? 1 : 0);
            Refresh();
            gridEl.MarkDirtyRepaint();
        }

        static void Tex(VisualElement v, Texture2D t, float a, Color tint)
        {
            v.style.display = t != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (t == null) return;
            v.style.backgroundImage = new StyleBackground(t);
            v.style.opacity = a;
            v.style.unityBackgroundImageTintColor = tint;
        }

        /// <summary>픽셀 격자 — 보이는 부분의 선만(확대 6배 이상). 펜이 움직일 때는 다시 그리지 않음.</summary>
        void DrawGrid(MeshGenerationContext ctx)
        {
            if (!grid || zoom < 6) return;
            var p = ctx.painter2D;
            var o = Origin();
            float vw = view.resolvedStyle.width, vh = view.resolvedStyle.height;
            if (float.IsNaN(vw)) vw = 600;
            if (float.IsNaN(vh)) vh = 400;
            int x0 = Mathf.Max(1, Mathf.FloorToInt(-o.x / zoom)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt((vw - o.x) / zoom));
            int y0 = Mathf.Max(1, Mathf.FloorToInt(-o.y / zoom)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt((vh - o.y) / zoom));
            float top = Mathf.Max(0, -o.y), bot = Mathf.Min(H * zoom, vh - o.y), lft = Mathf.Max(0, -o.x), rgt = Mathf.Min(W * zoom, vw - o.x);
            p.strokeColor = LightBg ? new Color(0, 0, 0, 0.12f) : new Color(1, 1, 1, 0.08f); p.lineWidth = 1;
            p.BeginPath();
            for (int x = x0; x <= x1; x++) { p.MoveTo(new Vector2(x * zoom, top)); p.LineTo(new Vector2(x * zoom, bot)); }
            for (int y = y0; y <= y1; y++) { p.MoveTo(new Vector2(lft, y * zoom)); p.LineTo(new Vector2(rgt, y * zoom)); }
            p.Stroke();
        }

        void DrawOverlay(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float cw = W * zoom, ch = H * zoom;
            // 발밑 선(바닥) — 그 아래는 바닥을 뚫는 자리(흙빛으로 살짝)
            float fy = (H - foot) * zoom;
            if (foot > 0 && !centerPivot)
            {
                p.fillColor = new Color(0.55f, 0.32f, 0.12f, 0.22f);
                p.BeginPath(); Rc(p, 0, fy, cw, ch - fy); p.Fill();
            }
            if (!centerPivot)
            {
                p.strokeColor = new Color(1f, 0.83f, 0.2f, 0.9f); p.lineWidth = 2;
                p.BeginPath(); p.MoveTo(new Vector2(0, Mathf.Min(fy, ch - 1))); p.LineTo(new Vector2(cw, Mathf.Min(fy, ch - 1))); p.Stroke();
            }
            else
            {
                // 가운데 기준(이펙트용): 캔버스 정가운데 십자선 + 작은 원
                float cx = cw / 2, cy = ch / 2, r = Mathf.Max(6, Mathf.Min(cw, ch) * 0.04f);
                p.strokeColor = new Color(1f, 0.35f, 0.85f, 0.85f); p.lineWidth = 1.5f;
                p.BeginPath(); p.MoveTo(new Vector2(cx, 0)); p.LineTo(new Vector2(cx, ch)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(0, cy)); p.LineTo(new Vector2(cw, cy)); p.Stroke();
                p.lineWidth = 2;
                p.BeginPath(); p.Arc(new Vector2(cx, cy), r, new Angle(0, AngleUnit.Degree), new Angle(360, AngleUnit.Degree)); p.Stroke();
            }
            if (mirror) { p.strokeColor = new Color(0.4f, 0.9f, 1f, 0.6f); p.BeginPath(); p.MoveTo(new Vector2(cw / 2, 0)); p.LineTo(new Vector2(cw / 2, ch)); p.Stroke(); }
            p.strokeColor = LightBg ? new Color(0, 0, 0, 0.45f) : new Color(1, 1, 1, 0.35f); p.lineWidth = 1;
            p.BeginPath(); Rc(p, 0, 0, cw, ch); p.Stroke();
            if (drawing && (tool == Tool.Line || tool == Tool.Rect))
            {
                var c = (Color)colors.Current; c.a = Mathf.Max(0.35f, c.a * 0.6f);
                p.fillColor = c;
                p.BeginPath();
                if (tool == Tool.Line) foreach (var q in LinePts(downPx, lastPx)) Rc(p, q.x * zoom, q.y * zoom, zoom, zoom);
                else
                {
                    int x0 = Math.Min(downPx.x, lastPx.x), x1 = Math.Max(downPx.x, lastPx.x), y0 = Math.Min(downPx.y, lastPx.y), y1 = Math.Max(downPx.y, lastPx.y);
                    Rc(p, x0 * zoom, y0 * zoom, (x1 - x0 + 1) * zoom, (y1 - y0 + 1) * zoom);
                }
                p.Fill();
            }
            // 선택 미리보기
            if (selecting && tool == Tool.SelRect)
            {
                int x0 = Math.Min(selA.x, selB2.x), x1 = Math.Max(selA.x, selB2.x) + 1, y0 = Math.Min(selA.y, selB2.y), y1 = Math.Max(selA.y, selB2.y) + 1;
                for (int pass = 0; pass < 2; pass++)
                {
                    p.strokeColor = pass == 0 ? new Color(0, 0, 0, 0.9f) : new Color(0.55f, 0.85f, 1f, 1f); p.lineWidth = pass == 0 ? 3 : 1;
                    p.BeginPath(); Rc(p, x0 * zoom, y0 * zoom, (x1 - x0) * zoom, (y1 - y0) * zoom); p.Stroke();
                }
            }
            var path = (selecting && tool == Tool.Lasso) || lassoDraw ? lasso : poly;
            if (path != null && path.Count > 0)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    p.strokeColor = pass == 0 ? new Color(0, 0, 0, 0.9f) : new Color(0.55f, 0.85f, 1f, 1f); p.lineWidth = pass == 0 ? 3 : 1;
                    p.BeginPath();
                    p.MoveTo(path[0] * zoom);
                    for (int i = 1; i < path.Count; i++) p.LineTo(path[i] * zoom);
                    if (path == poly) p.LineTo(hoverF * zoom);
                    p.Stroke();
                }
                if (path == poly)
                {
                    p.fillColor = new Color(1f, 0.83f, 0.2f, 1f);
                    foreach (var q in poly) { p.BeginPath(); Rc(p, q.x * zoom - 3, q.y * zoom - 3, 6, 6); p.Fill(); }
                }
            }
            // 변형 상자
            if (xf != null) DrawXfBox(p);
            if (hoverPx.x >= 0 && hoverPx.y >= 0 && hoverPx.x < W && hoverPx.y < H && tool != Tool.Hand && tool != Tool.LassoFill && tool != Tool.LassoErase && !IsSelTool(tool) && xf == null)
            {
                int r0 = -(brush - 1) / 2;
                p.strokeColor = new Color(1, 1, 1, 0.9f); p.lineWidth = 1.5f;
                p.BeginPath(); Rc(p, (hoverPx.x + r0) * zoom, (hoverPx.y + r0) * zoom, brush * zoom, brush * zoom); p.Stroke();
            }
        }

        static void Rc(Painter2D p, float x, float y, float w, float h)
        {
            p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w, y)); p.LineTo(new Vector2(x + w, y + h)); p.LineTo(new Vector2(x, y + h)); p.ClosePath();
        }

        // ───────── 타임라인 ─────────
        const int CELL = 30;

        // ───────── 타임라인 스크롤: 가로 · 세로 모두 ─────────
        ScrollView tlScroll;
        bool tlPan; Vector2 tlPanMouse, tlPanStart; int tlPanId = -1;
        VisualElement tlCurHead;

        /// <summary>
        /// 휠 = 세로(Shift+휠 · 가로 휠 = 가로) · 가운데 · 오른쪽 버튼 끌기, 또는 빈 곳을 왼쪽 버튼으로 끌기 = 가로 · 세로 같이 이동.
        /// 스크롤 막대는 굵게(눌러 끌기 쉽게).
        /// </summary>
        void TlPanEnd(int id) { if (!tlPan || id != tlPanId) return; tlPan = false; if (tlScroll.HasPointerCapture(id)) tlScroll.ReleasePointer(id); tlPanId = -1; }

        void HookTimelinePan()
        {
            tlScroll.RegisterCallback<WheelEvent>(e =>
            {
                var d = e.delta;
                bool horiz = e.shiftKey || Mathf.Abs(d.x) > Mathf.Abs(d.y);
                float amt = (horiz ? (Mathf.Abs(d.x) > 0 ? d.x : d.y) : d.y) * 36f;
                var o = tlScroll.scrollOffset;
                if (horiz) o.x += amt; else o.y += amt;
                tlScroll.scrollOffset = o;
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
            tlScroll.RegisterCallback<PointerDownEvent>(e =>
            {
                // 예전 끌기가 덜 끝난 채 남아 있으면 정리(손을 뗀 소식을 놓친 경우)
                if (tlPan) { if (tlPanId >= 0 && tlScroll.HasPointerCapture(tlPanId)) tlScroll.ReleasePointer(tlPanId); tlPan = false; tlPanId = -1; }
                bool empty = e.target == timeline || e.target == tlScroll.contentContainer || e.target == tlScroll.contentViewport
                             || (e.target is VisualElement ve && (ve.ClassListContains("ae-tl-row") || ve.ClassListContains("ae-tl-name")));
                // 가운데 · 오른쪽 버튼, 빈 곳 왼쪽 버튼, 스페이스를 누른 채 왼쪽 버튼 = 끌어서 이동
                if (e.button == 1 || e.button == 2 || (e.button == 0 && (empty || spaceHeld || (e.shiftKey && e.altKey))))
                {
                    tlPan = true; tlPanId = e.pointerId;
                    tlPanMouse = e.position; tlPanStart = tlScroll.scrollOffset;
                    tlScroll.CapturePointer(e.pointerId);
                    e.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
            tlScroll.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!tlPan || e.pointerId != tlPanId) return;
                if (e.pressedButtons == 0) { TlPanEnd(e.pointerId); return; }            // 버튼을 이미 뗐는데 놓친 경우
                tlScroll.scrollOffset = tlPanStart - ((Vector2)e.position - tlPanMouse);
                tlKeepPending = false;                                         // 끄는 중엔 자리 되돌리기 하지 않음
                e.StopPropagation();
            });
            tlScroll.RegisterCallback<PointerUpEvent>(e => TlPanEnd(e.pointerId));
            tlScroll.RegisterCallback<PointerCaptureOutEvent>(_ => { tlPan = false; tlPanId = -1; });
            // 오른쪽 버튼 끌기에 메뉴가 뜨지 않게
            tlScroll.RegisterCallback<ContextClickEvent>(e => e.StopPropagation(), TrickleDown.TrickleDown);
        }

        int followFi = -1, followRi = -1;
        VisualElement tlCurCell;

        /// <summary>
        /// 스크롤 자리 지키기 + 따라가기: 타임라인을 다시 만들어도 보던 자리 그대로.
        /// 지금 프레임 · 레이어가 바뀌었을 때만(단축키 이동 등) 그 칸이 화면 밖이면 가로 · 세로로 따라감.
        /// </summary>
        Vector2 tlKeep;
        bool tlKeepPending, tlFollowPending;

        void FollowCurFrame(Vector2 keep)
        {
            if (tlScroll == null) return;
            bool chg = fi != followFi || ri != followRi;
            followFi = fi; followRi = ri;
            tlKeep = keep;
            tlKeepPending = true;
            tlFollowPending |= chg;
            if (tlScroll.scrollOffset != keep) tlScroll.scrollOffset = keep;
            // 칸들의 위치가 실제로 정해진 뒤(레이아웃 끝)에 자리 되돌리기 · 따라가기 — 그 전엔 위치가 0 이라 맨 앞으로 끌려감
            timeline.UnregisterCallback<GeometryChangedEvent>(OnTimelineLaid);
            timeline.RegisterCallback<GeometryChangedEvent>(OnTimelineLaid);
            timeline.schedule.Execute(() => OnTimelineLaid(null)).StartingIn(30);   // 크기가 안 바뀌어 위 소식이 안 오는 경우
        }

        void OnTimelineLaid(GeometryChangedEvent _)
        {
            timeline.UnregisterCallback<GeometryChangedEvent>(OnTimelineLaid);
            if (!tlKeepPending) return;
            var cell = tlCurCell ?? tlCurHead;
            if (cell != null && (cell.panel == null || float.IsNaN(cell.layout.width) || cell.layout.width <= 0)) return;   // 아직 배치 전 — 다음 소식에서
            tlKeepPending = false;
            // 보던 자리로 — 다시 만들며 0 으로 밀린 축만(그사이 막대를 끌어 옮긴 건 건드리지 않음)
            var cur = tlScroll.scrollOffset;
            if (cur.x <= 0.5f && tlKeep.x > 0.5f) cur.x = tlKeep.x;
            if (cur.y <= 0.5f && tlKeep.y > 0.5f) cur.y = tlKeep.y;
            tlScroll.scrollOffset = cur;
            if (!tlFollowPending || cell == null) return;
            tlFollowPending = false;
            {
                var vp = tlScroll.contentViewport.worldBound;
                var cb = cell.worldBound;
                var o = tlScroll.scrollOffset;
                float nameW = 196f, headH = 36f;                           // 왼쪽 레이어 이름 칸 · 위 머리 줄만큼은 비워 둠
                if (cb.xMin < vp.xMin + nameW) o.x -= vp.xMin + nameW - cb.xMin;
                else if (cb.xMax > vp.xMax) o.x += cb.xMax - vp.xMax + 8;
                if (tlCurCell != null)
                {
                    if (cb.yMin < vp.yMin + headH) o.y -= vp.yMin + headH - cb.yMin;
                    else if (cb.yMax > vp.yMax) o.y += cb.yMax - vp.yMax + 4;
                }
                tlScroll.scrollOffset = o;
            }
        }

        void RebuildTimeline()
        {
            var keep = tlScroll != null ? tlScroll.scrollOffset : Vector2.zero;
            timeline.Clear();
            tlCurHead = null; tlCurCell = null;
            if (frames.Count == 0) return;
            // 머리 줄: 프레임 번호 · 길이
            var hr = El(timeline, "ae-tl-row");
            var corner = El(hr, "ae-tl-name"); Lbl(corner, "레이어 \\ 프레임", "ae-tl-cap");
            for (int f = 0; f < frames.Count; f++)
            {
                int ff = f;
                var h = new Button { text = $"{f + 1}\n{frames[f].Dur}f" };
                h.RegisterCallback<ClickEvent>(e => FrameClick(ff, e.ctrlKey || e.commandKey, e.shiftKey));
                h.AddToClassList("ae-tl-head");
                h.tooltip = $"프레임 {f + 1} · {frames[f].Dur}f = {MsOf(f):0}ms · Ctrl+누르기 = 더 고르기 · Shift+누르기 = 범위";
                h.EnableInClassList("ae-tl-head--on", f == fi);
                if (f == fi) tlCurHead = h;
                h.EnableInClassList("ae-tl-head--sel", f != fi && fSel.Contains(frames[f]));
                h.style.width = CELL + Mathf.Min(frames[f].Dur - 1, 6) * 4;      // 길수록 조금 넓게(길이 감)
                hr.Add(h);
            }
            // 레이어 줄(위 → 아래 = 앞 → 뒤)
            for (int r = rows.Count - 1; r >= 0; r--)
            {
                int rr = r;
                var row = rows[r];
                var line = El(timeline, "ae-tl-row");
                line.EnableInClassList("ae-tl-row--on", r == ri);
                line.EnableInClassList("ae-tl-row--sel", r != ri && rSel.Contains(row));
                var nm = El(line, "ae-tl-name");
                var vb = new Button(() => RowPropOne(rr, x => x.Visible = !x.Visible)) { text = row.Visible ? "보" : "숨", tooltip = "보이기 · 숨기기(이 레이어만)" };
                vb.AddToClassList("ae-tl-mini"); vb.EnableInClassList("ae-btn--on", row.Visible); nm.Add(vb);
                var lk = new Button(() => RowPropOne(rr, x => x.Locked = !x.Locked)) { text = row.Locked ? "잠" : "–", tooltip = "잠금(이 레이어만)" };
                lk.AddToClassList("ae-tl-mini"); lk.EnableInClassList("ae-btn--on", row.Locked); nm.Add(lk);
                var nb = new Button { text = row.Name + (row.Clip ? " ↳" : ""), tooltip = "누르면 고름 · Ctrl+누르기 = 더 고르기 · Shift+누르기 = 범위 · 두 번 누르면 이름 바꾸기" };
                nb.RegisterCallback<ClickEvent>(e => { if (e.clickCount <= 1) RowClick(rr, e.ctrlKey || e.commandKey, e.shiftKey); });
                nb.AddToClassList("ae-tl-label"); nm.Add(nb);
                nb.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0 && e.clickCount >= 2) { e.StopImmediatePropagation(); StartRename(nm, nb, rr); } }, TrickleDown.TrickleDown);
                if (renameReq && rr == ri) { renameReq = false; var n0 = nm; var b0 = nb; nb.schedule.Execute(() => StartRename(n0, b0, rr)); }
                for (int f = 0; f < frames.Count; f++)
                {
                    int ff = f;
                    var c = frames[f].Cels[r];
                    bool hold = c != null && f > 0 && frames[f - 1].Cels[r] == c;
                    var cell = new Button { text = c == null ? "" : hold ? "―" : "●" };
                    cell.RegisterCallback<ClickEvent>(e =>
                    {
                        bool ctrl = e.ctrlKey || e.commandKey;
                        if (ctrl || e.shiftKey) { if (rr != ri) RowClick(rr, false, false); FrameClick(ff, ctrl, e.shiftKey); }   // 같은 레이어에서 프레임 여러 개
                        else { fSel.Clear(); rSel.Clear(); fAnchor = ff; rAnchor = rr; fi = ff; ri = rr; play = false; Refresh(); }
                    });
                    cell.AddToClassList("ae-tl-cell");
                    cell.EnableInClassList("ae-tl-cell--has", c != null && !hold);
                    cell.EnableInClassList("ae-tl-cell--hold", hold);
                    cell.EnableInClassList("ae-tl-cell--col", f == fi);
                    cell.EnableInClassList("ae-tl-cell--sel", (f == fi || fSel.Contains(frames[f])) && (r == ri || rSel.Contains(row)) && !(f == fi && r == ri));
                    cell.EnableInClassList("ae-tl-cell--on", f == fi && r == ri);
                    if (f == fi && r == ri) tlCurCell = cell;
                    cell.style.width = CELL + Mathf.Min(frames[f].Dur - 1, 6) * 4;
                    line.Add(cell);
                }
            }
            FollowCurFrame(keep);
        }

        // ───────── 갱신 ─────────
        void RefreshTitle()
        {
            title.text = key == null ? "도트 편집기" : $"도트 편집기 · {key}{(dirty ? " •" : "")}{(readOnly ? " (보기만)" : "")}";
        }

        void RefreshTime()
        {
            if (frames.Count == 0) { timeInfo.text = ""; return; }
            int tot = frames.Sum(f => f.Dur);
            timeInfo.text = $"= {MsOf(fi):0}ms   ·   프레임 {fi + 1}/{frames.Count}   ·   총 {tot}f = {tot * 1000f / fps:0}ms{SelText()}";
            durField.SetValueWithoutNotify(F.Dur);
            fpsField.SetValueWithoutNotify(fps);
        }

        void Refresh()
        {
            RefreshTitle();
            foreach (var kv in toolBtns) kv.Value.EnableInClassList("ae-btn--on", kv.Key == tool);
            if (alignBtn != null) alignBtn.text = "정렬: " + ALIGN[(int)alignMode];
            if (fitBtn != null) RefreshFit();
            if (alignBtn2 != null) alignBtn2.text = (alignMode == SpriteSheet.Align.None ? "자동" : ALIGN[(int)alignMode]) + " 정렬";
            mirrorBtn.EnableInClassList("ae-btn--on", mirror);
            pickPenBtn.EnableInClassList("ae-btn--on", pickToPen);
            pickPenBtn.text = pickToPen ? "찍고→펜 켬" : "찍고→펜 끔";
            gridBtn.EnableInClassList("ae-btn--on", grid);
            if (gridBtn2 != null) { gridBtn2.EnableInClassList("ae-btn--on", grid); gridBtn2.text = grid ? "격자 켬" : "격자 끔"; }
            if (bgBtn != null) bgBtn.text = "배경: " + BG[bgMode];
            onionBtn.EnableInClassList("ae-btn--on", onion);
            onionBtn.text = onion ? "켬" : "끔";
            onionStyleBtn.text = ONION_STYLE[onionStyle];
            onionSoloPrevBtn.EnableInClassList("ae-btn--on", onionSolo == 1);
            onionSoloNextBtn.EnableInClassList("ae-btn--on", onionSolo == 2);
            onionHoldBtn.text = onionHold ? "누르고 보기" : "계속 보기";
            onionPosBtn.text = onionOver ? "위에" : "아래에";
            onionPosBtn.EnableInClassList("ae-btn--on", onionOver);
            loopBtn.text = loop ? "반복" : "한 번만";
            loopBtn.EnableInClassList("ae-btn--on", true);
            playBtn.text = play ? "■ 정지" : "▶ 재생";
            playBtn.EnableInClassList("ae-btn--on", play);
            pinBtn.EnableInClassList("ae-btn--on", Pin);
            freezeBtn?.EnableInClassList("ae-btn--on", Freeze);
            brushL.text = brush + "px";
            if (R != null)
            {
                rowName.SetValueWithoutNotify(R.Name);
                rowOp.SetValueWithoutNotify(Mathf.RoundToInt((float)R.Opacity * 100));
                visBtn.text = R.Visible ? "보임" : "숨김"; visBtn.EnableInClassList("ae-btn--on", R.Visible);
                lockBtn.EnableInClassList("ae-btn--on", R.Locked);
                clipBtn.EnableInClassList("ae-btn--on", R.Clip);
            }
            rwField.SetValueWithoutNotify(W); rhField.SetValueWithoutNotify(H);
            RefreshTime();
            RebuildTimeline();
            Layout();
        }

        void TogglePlay()
        {
            play = !play;
            playMs = 0;
            for (int i = 0; i < fi; i++) playMs += MsOf(i);       // 지금 프레임부터
            Refresh();
        }

        // ───────── 가져오기 ─────────
        static string ImportDir => Path.Combine(ArtLib.Dir, "import");

        void BuildImports()
        {
            importList.Clear();
            cellW.SetValueWithoutNotify(cellW.value > 0 ? cellW.value : W);
            if (!Directory.Exists(ImportDir)) { Lbl(importList, "import 폴더가 아직 없어요 — ‘폴더 열기’ 를 누르면 만들어져요.", "ae-note"); return; }
            var files = Directory.GetFiles(ImportDir).Where(f => { var e = Path.GetExtension(f).ToLowerInvariant(); return e == ".png" || e == ".jpg" || e == ".jpeg" || e == ".psd" || e == ".psb"; }).OrderBy(f => f).ToList();
            if (files.Count == 0) { Lbl(importList, "비어 있어요 — PNG · JPG · PSD 를 넣고 ‘새로 고침’.", "ae-note"); return; }
            var hidden = LoadHidden();
            int nHidden = files.Count(f => hidden.Contains(Path.GetFileName(f)));
            hiddenBtn.text = showHidden ? "뺀 것 숨기기" : $"뺀 것 보기 ({nHidden})";
            hiddenBtn.EnableInClassList("ae-btn--on", showHidden);
            hiddenBtn.SetEnabled(nHidden > 0 || showHidden);
            if (!showHidden && nHidden == files.Count) { Lbl(importList, $"모두 목록에서 뺐어요 — ‘뺀 것 보기 ({nHidden})’ 로 다시 넣을 수 있어요.", "ae-note"); return; }
            foreach (var f in files)
            {
                string ff = f, fname = Path.GetFileName(f);
                bool isHidden = hidden.Contains(fname);
                if (isHidden && !showHidden) continue;
                bool psd = Path.GetExtension(f).ToLowerInvariant().StartsWith(".ps");
                var box = El(importList, "ae-imp");
                box.EnableInClassList("ae-imp--hidden", isHidden);
                var head = El(box, "ae-imp-h");
                Lbl(head, fname, "ae-imp-n");
                if (isHidden) { Btn(head, "다시 넣기", () => { hidden.Remove(fname); SaveHidden(hidden); BuildImports(); }, "목록에 다시 보이기"); continue; }
                Btn(head, "가져오기…", () => OpenImport(ff), psd ? "PSD 가져오기 — 한 장 · 레이어째 · 컷 폴더째 · 새 동작으로" : "그림 가져오기 — 새 레이어 · 칸 자동 · 칸 크기로 잘라 프레임", "ae-btn--main");
                Btn(head, "✕", () => { hidden.Add(fname); SaveHidden(hidden); BuildImports(); Say($"‘{fname}’ 을 목록에서 뺐어요(파일은 그대로) — ‘뺀 것 보기’ 로 되돌리기"); }, "목록에서 빼기(파일은 지우지 않음)", "ae-imp-x");
            }
        }

        // ───────── 가져오기 창: 미리보기 + 잘릴 칸 + 넣는 방식 · 칸 · 맞추기 · 정렬 → 가져오기 ─────────
        VisualElement impDlg, impPrev, impGrid, impOpts, impCellRow, impAlignRow, impModeRow, impFitBox;
        Label impTitle, impInfo;
        readonly List<Button> fitSeg = new List<Button>(), alignSeg = new List<Button>(), impModeBtns = new List<Button>();
        string impPath;
        bool impPsd;
        int impMode, impW, impH, impCols, impRows, impLayers, impGroups;
        double impCw, impCh;
        float impScale = 1;
        byte[] impPx;
        Texture2D impTex;
        static readonly string[] IMP_PNG = { "새 레이어로", "칸 자동 → 프레임", "칸 크기 → 프레임" };
        static readonly string[] IMP_PNG_TIP = { "지금 프레임의 새 레이어로(밑그림 · 참고용)", "칸 수를 스스로 찾아 등분 → 뒤에 새 프레임들로(빈 칸 건너뜀)", "아래 ‘칸’ 크기로 잘라 → 뒤에 새 프레임들로(빈 칸 건너뜀)" };
        static readonly string[] IMP_PSD = { "한 장 → 레이어", "레이어째 → 레이어", "컷 폴더째 → 프레임", "새 동작으로 열기" };
        static readonly string[] IMP_PSD_TIP = { "PSD 전체를 합쳐 지금 프레임의 새 레이어로", "PSD 레이어 하나하나를 지금 프레임의 새 레이어로", "PSD 컷 폴더(없으면 레이어) 하나하나를 뒤에 새 프레임으로", "이 PSD 를 위 ‘파일’ 이름 칸의 동작으로 복사해서 열기" };

        void BuildImportDialog()
        {
            impDlg = El(Root, "ae-dlg");
            impDlg.style.display = DisplayStyle.None;
            var card = El(impDlg, "ae-dlg-card");
            impTitle = Lbl(card, "", "ae-dlg-t");
            impPrev = El(card, "ae-dlg-prev");
            impPrev.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            impGrid = new VisualElement { pickingMode = PickingMode.Ignore };
            impGrid.style.position = Position.Absolute;
            impGrid.style.left = 0; impGrid.style.top = 0; impGrid.style.right = 0; impGrid.style.bottom = 0;
            impGrid.generateVisualContent += DrawImpGrid;
            impPrev.Add(impGrid);
            Lbl(card, "넣는 방식", "ae-k");
            impModeRow = El(card, "ae-wrap");
            impOpts = El(card, "ae-col");
            impCellRow = El(impOpts, "ae-row");
            Lbl(impCellRow, "칸 크기", "ae-k");
            cellW = Int(impCellRow, 64); Lbl(impCellRow, "×", "ae-k"); cellH = Int(impCellRow, 64);
            cellW.RegisterValueChangedCallback(_ => ImpUpdate());
            cellH.RegisterValueChangedCallback(_ => ImpUpdate());
            impFitBox = El(impOpts, "ae-col");
            Lbl(impFitBox, "캔버스에 맞추기", "ae-k");
            var fr = El(impFitBox, "ae-wrap");
            string[] fitTip = { "1:1 그대로(넘치면 잘림 — 잠금을 끄면 캔버스를 키움)", "칸 · 그림이 캔버스보다 크면 비율대로 줄임, 작으면 그대로", "캔버스에 꽉 차게 늘리거나 줄임" };
            for (int i = 0; i < FIT.Length; i++)
            {
                int ii = i;
                fitSeg.Add(Btn(fr, FIT[i], () => { fitMode = ii; PlayerPrefs.SetInt("mori.editor.fitMode", fitMode); RefreshFit(); ImpUpdate(); }, fitTip[i]));
            }
            lockCanvasBtn = Btn(fr, "캔버스 잠금", () => { ToggleCanvasLock(); ImpUpdate(); }, "켜면 가져오기가 캔버스 크기를 바꾸지 않아요(기본). 끄면 넘치는 그림에 맞춰 캔버스를 키워 안 잘리게.");
            impAlignRow = El(impOpts, "ae-col");
            Lbl(impAlignRow, "컷 정렬", "ae-k");
            var ar = El(impAlignRow, "ae-wrap");
            string[] alTip = { "칸 그대로", "발을 아래 가운데에 맞춤", "컷끼리 몸통을 겹쳐 가로만 맞춤(위아래 움직임은 살림)" };
            for (int i = 0; i < ALIGN.Length; i++)
            {
                int ii = i;
                alignSeg.Add(Btn(ar, ALIGN[i], () => { alignMode = (SpriteSheet.Align)ii; Refresh(); ImpUpdate(); }, alTip[i]));
            }
            impInfo = Lbl(card, "", "ae-dlg-info");
            var br = El(card, "ae-wrap");
            Btn(br, "가져오기", DoImport, "고른 방식으로 가져오기", "ae-btn--main");
            Btn(br, "취소 (Esc)", CloseImport, "닫기");
        }

        void OpenImport(string file)
        {
            try
            {
                impPath = file;
                impPsd = Path.GetExtension(file).ToLowerInvariant().StartsWith(".ps");
                if (impPsd)
                {
                    var d = Psd.Read(File.ReadAllBytes(file));
                    impW = d.W; impH = d.H;
                    impPx = Psd.Compose(d.Root.Kids, 0, 0, d.W, d.H);
                    impGroups = d.Root.Kids.Count(n => n.Group);
                    impLayers = d.Root.Kids.Count(n => !n.Group);
                    impMode = impGroups > 1 ? 2 : 0;
                }
                else
                {
                    impPx = LoadImage(file, out impW, out impH);
                    impMode = 1;
                }
                if (impTex != null) TexGrave.Kill(impTex);
                impTex = new Texture2D(impW, impH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var flip = new byte[impPx.Length];
                int row = impW * 4;
                for (int y = 0; y < impH; y++) Buffer.BlockCopy(impPx, y * row, flip, (impH - 1 - y) * row, row);
                impTex.SetPixelData(flip, 0);
                impTex.Apply(false, false);
                impPrev.style.backgroundImage = new StyleBackground(impTex);
                impScale = Mathf.Min(440f / Mathf.Max(1, impW), 260f / Mathf.Max(1, impH), 8f);
                impPrev.style.width = impW * impScale; impPrev.style.height = impH * impScale;
                impTitle.text = $"가져오기 — {Path.GetFileName(file)}  ({impW}×{impH})";
                impModeRow.Clear(); impModeBtns.Clear();
                var names = impPsd ? IMP_PSD : IMP_PNG; var tips = impPsd ? IMP_PSD_TIP : IMP_PNG_TIP;
                for (int i = 0; i < names.Length; i++)
                {
                    int ii = i;
                    impModeBtns.Add(Btn(impModeRow, names[i], () => { impMode = ii; ImpUpdate(); }, tips[i]));
                }
                if (cellW.value <= 0) cellW.SetValueWithoutNotify(W);
                if (cellH.value <= 0) cellH.SetValueWithoutNotify(H);
                RefreshFit();
                impDlg.style.display = DisplayStyle.Flex;
                impDlg.BringToFront();
                ImpUpdate();
            }
            catch (Exception e) { Say("미리보기를 열지 못했어요: " + e.Message); CloseImport(); }
        }

        void CloseImport()
        {
            if (impDlg != null) impDlg.style.display = DisplayStyle.None;
            if (impTex != null) { TexGrave.Kill(impTex); impTex = null; }
            impPx = null;
        }

        /// <summary>고른 방식 · 칸에 맞춰 격자와 ‘무엇이 어떻게 들어오는지’ 를 미리 보여 줌.</summary>
        void ImpUpdate()
        {
            if (impPx == null) return;
            for (int i = 0; i < impModeBtns.Count; i++) impModeBtns[i].EnableInClassList("ae-btn--on", i == impMode);
            bool sheet = !impPsd && impMode >= 1;
            impCellRow.style.display = sheet && impMode == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            impAlignRow.style.display = sheet ? DisplayStyle.Flex : DisplayStyle.None;
            impFitBox.style.display = impPsd && impMode == 3 ? DisplayStyle.None : DisplayStyle.Flex;
            impCols = impRows = 0;
            string lockNote = canvasLock ? "캔버스 잠금 켬(크기 그대로)" : "잠금 끔(넘치면 캔버스를 키움)";
            if (sheet)
            {
                if (impMode == 1) SpriteSheet.Detect(impPx, impW, impH, out impCols, out impRows);
                else { int cw = Mathf.Clamp(cellW.value, 1, impW), ch = Mathf.Clamp(cellH.value, 1, impH); impCols = Math.Max(1, impW / cw); impRows = Math.Max(1, impH / ch); }
                if (impCols * impRows <= 1 && impMode == 1) { impInfo.text = "칸을 찾지 못했어요(컷 사이에 빈 줄이 없음) — ‘칸 크기 → 프레임’ 으로 칸 크기를 적어 주세요."; impGrid.MarkDirtyRepaint(); return; }
                impCw = (double)impW / impCols; impCh = (double)impH / impRows;
                if (impMode == 2) { impCw = Mathf.Clamp(cellW.value, 1, impW); impCh = Mathf.Clamp(cellH.value, 1, impH); }
                int n = 0;
                try { n = SpriteSheet.Split(impPx, impW, impH, impCw, impCh, impCols, impRows, alignMode).Count; } catch { }
                float k = FitScale((int)Math.Ceiling(impCw), (int)Math.Ceiling(impCh));
                impInfo.text = $"{impCols}×{impRows} 칸 · 칸 {impCw:0.#}×{impCh:0.#} → 컷 {n}개(빈 칸 제외)를 지금 그림 뒤에 새 프레임으로 · 정렬 {ALIGN[(int)alignMode]}\n캔버스 {W}×{H}{(k != 1 ? $" · {k * 100:0}% 로 맞춤" : " · 1:1")} · {lockNote}";
            }
            else if (!impPsd)
            {
                float k = FitScale(impW, impH);
                impInfo.text = $"지금 프레임({fi + 1})의 새 레이어로 · 그림 {impW}×{impH} → 캔버스 {W}×{H}{(k != 1 ? $" · {k * 100:0}% 로 맞춤" : " · 1:1")} · {lockNote}";
            }
            else
            {
                string what = impMode == 0 ? $"PSD 전체를 합쳐 지금 프레임({fi + 1})의 새 레이어 하나로"
                    : impMode == 1 ? $"레이어 {impLayers + impGroups}개를 지금 프레임의 새 레이어들로"
                    : impMode == 2 ? $"{(impGroups > 0 ? $"컷 폴더 {impGroups}개" : $"레이어 {impLayers}개")}를 뒤에 새 프레임들로"
                    : $"이 PSD 를 ‘{keyField.value}’ 동작으로 복사해서 열기(이미 있으면 안 함)";
                impInfo.text = what + (impMode == 3 ? "" : $"\n그림 {impW}×{impH} → 캔버스 {W}×{H} · {lockNote}");
            }
            impGrid.MarkDirtyRepaint();
        }

        void DrawImpGrid(MeshGenerationContext ctx)
        {
            if (impCols <= 0 || impRows <= 0 || impPx == null) return;
            var p = ctx.painter2D;
            p.strokeColor = new Color(1f, 0.83f, 0.2f, 0.85f);
            p.lineWidth = 1;
            p.BeginPath();
            float w = impW * impScale, h = impH * impScale;
            for (int i = 1; i < impCols; i++) { float x = (float)(i * impCw * impScale); p.MoveTo(new Vector2(x, 0)); p.LineTo(new Vector2(x, h)); }
            for (int j = 1; j < impRows; j++) { float y = (float)(j * impCh * impScale); p.MoveTo(new Vector2(0, y)); p.LineTo(new Vector2(w, y)); }
            p.Stroke();
        }

        void DoImport()
        {
            string f = impPath;
            bool psd = impPsd; int m = impMode;
            CloseImport();
            if (f == null) return;
            if (psd) ImportPsd(f, m);
            else if (m == 0) ImportImage(f, false);
            else ImportSheet(f, m == 1);
        }

        // 목록에서 뺀 파일 이름들(import 폴더의 숨김 파일 한 줄에 하나) — 파일 자체는 그대로
        static string HiddenFile => Path.Combine(ImportDir, ".목록에서-뺀-파일.txt");
        bool showHidden;
        Button hiddenBtn;

        static HashSet<string> LoadHidden()
        {
            try { if (File.Exists(HiddenFile)) return new HashSet<string>(File.ReadAllLines(HiddenFile).Select(l => l.Trim()).Where(l => l.Length > 0)); } catch { }
            return new HashSet<string>();
        }

        void SaveHidden(HashSet<string> h)
        {
            try { Directory.CreateDirectory(ImportDir); File.WriteAllLines(HiddenFile, h.OrderBy(x => x)); } catch (Exception e) { Say("목록 저장 실패: " + e.Message); }
        }

#if UNITY_EDITOR
        void PickFile()
        {
            var src = UnityEditor.EditorUtility.OpenFilePanel("가져올 그림", "", "png,jpg,jpeg,psd,psb");
            if (string.IsNullOrEmpty(src)) return;
            try { Directory.CreateDirectory(ImportDir); File.Copy(src, Path.Combine(ImportDir, Path.GetFileName(src)), true); Say("import 폴더로 복사했어요: " + Path.GetFileName(src)); }
            catch (Exception e) { Say("복사 실패: " + e.Message); }
            BuildImports();
        }
#endif

        /// <summary>PNG · JPG → 위에서 아래로 RGBA.</summary>
        static byte[] LoadImage(string path, out int w, out int h)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!t.LoadImage(File.ReadAllBytes(path))) throw new Exception("그림을 읽지 못했어요");
                w = t.width; h = t.height;
                var px = t.GetPixels32();
                var o = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var c = px[(h - 1 - y) * w + x];
                        int i = (y * w + x) * 4;
                        o[i] = c.r; o[i + 1] = c.g; o[i + 2] = c.b; o[i + 3] = c.a;
                    }
                return o;
            }
            finally { UnityEngine.Object.Destroy(t); }
        }

        /// <summary>그림(sw×sh) → 캔버스 크기: 아래 가운데 기준. 크면 줄이기가 켜져 있으면 비율 그대로(가장 가까운 픽셀) 줄여 맞춤.</summary>
        byte[] Place(byte[] src, int sw, int sh)
        {
            float k = FitScale(sw, sh);
            int tw = Mathf.Max(1, Mathf.RoundToInt(sw * k)), th = Mathf.Max(1, Mathf.RoundToInt(sh * k));
            int ox = (W - tw) / 2, oy = H - th;
            var o = Blank();
            for (int y = 0; y < th; y++)
            {
                int cy = y + oy; if (cy < 0 || cy >= H) continue;
                int sy = Mathf.Min(sh - 1, (int)(y / k));
                for (int x = 0; x < tw; x++)
                {
                    int cx = x + ox; if (cx < 0 || cx >= W) continue;
                    int sx = Mathf.Min(sw - 1, (int)(x / k));
                    Buffer.BlockCopy(src, (sy * sw + sx) * 4, o, (cy * W + cx) * 4, 4);
                }
            }
            return o;
        }

        static bool IsEmpty(byte[] px) { for (int i = 3; i < px.Length; i += 4) if (px[i] != 0) return false; return true; }

        /// <summary>지금 프레임에 새 레이어(고른 레이어 위)로.</summary>
        void AddAsLayers(List<(string name, byte[] px)> layers)
        {
            if (!Edit() || layers.Count == 0) return;
            SnapStruct();
            int at = rows.Count == 0 ? 0 : ri + 1;
            foreach (var (name, px) in layers)
            {
                rows.Insert(at, new Row { Name = UniqueRowName(name) });
                for (int f = 0; f < frames.Count; f++) frames[f].Cels.Insert(at, f == fi ? CelFrom(px) : null);
                ri = at;
                at++;
            }
            dirty = true;
            RecomposeAll();
        }

        /// <summary>뒤에 새 프레임들로(새 레이어 하나에).</summary>
        void AddAsFrames(string name, List<byte[]> pics)
        {
            if (!Edit() || pics.Count == 0) return;
            SnapStruct();
            int at = rows.Count == 0 ? 0 : ri + 1;
            rows.Insert(at, new Row { Name = name });
            foreach (var fr in frames) fr.Cels.Insert(at, null);
            int dur = frames.Count > 0 ? F.Dur : 6;
            int first = frames.Count;
            foreach (var px in pics)
            {
                var fr = new Frame { Dur = dur };
                for (int r = 0; r < rows.Count; r++) fr.Cels.Add(r == at ? CelFrom(px) : null);
                frames.Add(fr);
            }
            ri = at; fi = first;
            dirty = true;
            RecomposeAll();
        }

        void ImportImage(string path, bool sheet)
        {
            if (sheet) { ImportSheet(path, false); return; }
            try
            {
                var src = LoadImage(path, out int sw, out int sh);
                string nm = Path.GetFileNameWithoutExtension(path);
                bool grew = GrowFor(sw, sh);
                AddAsLayers(new List<(string, byte[])> { (nm, Place(src, sw, sh)) });
                Say($"{Path.GetFileName(path)} → 새 레이어 ‘{nm}’{(grew ? $" · 안 잘리게 캔버스를 {W}×{H} 로 키움" : "")}");
            }
            catch (Exception e) { Say("가져오기 실패: " + e.Message); }
        }

        void CycleAlign() { alignMode = (SpriteSheet.Align)(((int)alignMode + 1) % ALIGN.Length); Refresh(); }

        /// <summary>시트 → 새 프레임들. auto = 칸 수를 스스로 찾아 등분(443.5 같은 칸도 밀림 없이).</summary>
        void ImportSheet(string path, bool auto)
        {
            try
            {
                if (!Edit()) return;
                var src = LoadImage(path, out int sw, out int sh);
                string nm = Path.GetFileNameWithoutExtension(path);
                int cols, rws; double cwD, chD;
                if (auto)
                {
                    SpriteSheet.Detect(src, sw, sh, out cols, out rws);
                    if (cols * rws <= 1) { Say("칸을 찾지 못했어요(컷 사이에 빈 줄이 없음) — ‘칸’ 크기를 적고 ‘칸 크기로 잘라’."); return; }
                    cwD = (double)sw / cols; chD = (double)sh / rws;
                    cellW.SetValueWithoutNotify((int)Math.Round(cwD)); cellH.SetValueWithoutNotify((int)Math.Round(chD));
                }
                else
                {
                    int cw = Mathf.Clamp(cellW.value, 1, sw), ch = Mathf.Clamp(cellH.value, 1, sh);
                    cols = Math.Max(1, sw / cw); rws = Math.Max(1, sh / ch); cwD = cw; chD = ch;
                }
                var cells = SpriteSheet.Split(src, sw, sh, cwD, chD, cols, rws, alignMode);
                if (cells.Count == 0) { Say("잘라 보니 빈 칸뿐이에요 — 칸 크기를 확인해 주세요."); return; }
                // 기준점(바닥 가운데)에서 그림이 뻗은 거리 → 캔버스에 들어가는지
                double left = 0, right = 0, up = 0;
                foreach (var c in cells)
                    if (BBox(c.Px, c.W, c.H, out int l, out int t, out int r, out int b))
                    { left = Math.Max(left, c.Ax - l); right = Math.Max(right, r - c.Ax); up = Math.Max(up, c.Ay - t); }
                int needW = (int)Math.Ceiling(Math.Max(left, right) * 2), needH = (int)Math.Ceiling(up);
                bool grew = GrowFor(needW, needH);
                float k = FitScale((int)Math.Ceiling(cwD), (int)Math.Ceiling(chD));      // 칸 크기 기준(512 칸 → 512 캔버스 = 1:1)
                var pics = cells.Select(c => PlaceAt(c.Px, c.W, c.H, c.Ax, c.Ay, k)).ToList();
                AddAsFrames(nm, pics);
                Say($"{Path.GetFileName(path)} → {cols}×{rws} 칸(칸 {cwD:0.#}×{chD:0.#}) · {pics.Count}컷 · 정렬 {ALIGN[(int)alignMode]}{(grew ? $" · 안 잘리게 캔버스를 {W}×{H} 로 키움(크기 바꾸기는 되돌리기 안 됨)" : "")}{(k != 1 ? $" · {k * 100:0}% 로 맞춤" : "")}{(canvasLock && (needW > W || needH > H) && k >= 1 ? " · 칸 밖으로 나간 부분은 캔버스에서 잘림(잠금을 끄면 캔버스를 키움)" : "")}");
            }
            catch (Exception e) { Say("가져오기 실패: " + e.Message); }
        }

        /// <summary>
        /// 가져올 그림이 캔버스보다 크면(‘크면 줄이기’ 꺼짐) 캔버스를 키운다 — 잘려서 없어지는 픽셀이 없게.
        /// 아래 가운데 기준이라 그려 둔 그림은 발 위치 그대로. 최대 4096 을 넘으면 키울 수 있는 만큼만(나머지는 ‘크면 줄이기’).
        /// </summary>
        bool GrowFor(int needW, int needH)
        {
            if (canvasLock || fitMode == 2 || (needW <= W && needH <= H)) return false;
            int nw = Math.Min(MAX, Math.Max(W, (needW + 7) / 8 * 8)), nh = Math.Min(MAX, Math.Max(H, (needH + 7) / 8 * 8));
            if (nw == W && nh == H) return false;
            KitBackup.Before(path, true);           // 캔버스 키우기는 되돌리기가 지워짐 → 지금 파일을 백업
            Resize(nw, nh);
            return true;
        }

        /// <summary>w×h(칸 또는 그림)를 캔버스에 맞출 배율 — 맞추기 방식대로.</summary>
        float FitScale(int w, int h)
        {
            if (w <= 0 || h <= 0) return 1;
            float k = Mathf.Min((float)W / w, (float)H / h);
            if (fitMode == 2) return k;
            if (fitMode == 1) return k < 1 ? k : 1;
            return 1;
        }

        void ToggleCanvasLock()
        {
            canvasLock = !canvasLock;
            PlayerPrefs.SetInt("mori.editor.canvasLock", canvasLock ? 1 : 0);
            RefreshFit();
            Say(canvasLock ? "캔버스 크기 잠금 — 가져와도 캔버스가 안 바뀌어요." : "캔버스 잠금 풀림 — 넘치는 그림에 맞춰 캔버스를 키울 수 있어요.");
        }

        void RefreshFit()
        {
            if (fitBtn != null) { fitBtn.text = "맞추기: " + FIT[fitMode]; fitBtn.EnableInClassList("ae-btn--on", fitMode != 0); }
            for (int i = 0; i < fitSeg.Count; i++) fitSeg[i].EnableInClassList("ae-btn--on", i == fitMode);
            for (int i = 0; i < alignSeg.Count; i++) alignSeg[i].EnableInClassList("ae-btn--on", i == (int)alignMode);
            foreach (var b in new[] { lockCanvasBtn, lockCanvasBtn2 })
            {
                if (b == null) continue;
                b.text = b == lockCanvasBtn ? (canvasLock ? "캔버스 잠금 켬" : "캔버스 잠금 끔") : (canvasLock ? "잠금 켬" : "잠금 끔");
                b.EnableInClassList("ae-btn--on", canvasLock);
            }
        }

        bool DocEmpty() => frames.All(f => f.Cels.All(c => c == null || c.Img.Empty));

        static bool BBox(byte[] px, int w, int h, out int l, out int t, out int r, out int b)
        {
            l = w; t = h; r = 0; b = 0;
            for (int y = 0; y < h; y++)
            {
                int k = y * w * 4 + 3;
                for (int x = 0; x < w; x++, k += 4)
                {
                    if (px[k] == 0) continue;
                    if (x < l) l = x;
                    if (x + 1 > r) r = x + 1;
                    if (y < t) t = y;
                    b = y + 1;
                }
            }
            return r > l && b > t;
        }

        /// <summary>그림의 바닥 기준점 (ax, ay) 를 캔버스 아래 가운데에 — k 배(가장 가까운 픽셀).</summary>
        byte[] PlaceAt(byte[] src, int sw, int sh, double ax, double ay, float k)
        {
            var o = Blank();
            double cx0 = W / 2.0, cy0 = H;
            int xa = Math.Max(0, (int)Math.Floor(cx0 - ax * k)), xb = Math.Min(W, (int)Math.Ceiling(cx0 + (sw - ax) * k));
            int ya = Math.Max(0, (int)Math.Floor(cy0 - ay * k)), yb = Math.Min(H, (int)Math.Ceiling(cy0 + (sh - ay) * k));
            for (int y = ya; y < yb; y++)
            {
                int sy = (int)Math.Floor((y + 0.5 - cy0) / k + ay);
                if (sy < 0 || sy >= sh) continue;
                for (int x = xa; x < xb; x++)
                {
                    int sx = (int)Math.Floor((x + 0.5 - cx0) / k + ax);
                    if (sx < 0 || sx >= sw) continue;
                    int si = (sy * sw + sx) * 4;
                    if (src[si + 3] == 0) continue;
                    Buffer.BlockCopy(src, si, o, (y * W + x) * 4, 4);
                }
            }
            return o;
        }

        /// <summary>이 레이어의 모든 프레임을 자동 정렬(지금 프레임이 기준).</summary>
        void AlignLayer()
        {
            if (!Edit() || R == null) return;
            if (alignMode == SpriteSheet.Align.None) { Say("정렬 방식을 고르세요 — 가져오기 칸의 ‘정렬:’ 버튼(발밑 · 몸통)."); return; }
            if (R.Locked) { Say("잠긴 레이어예요."); return; }
            var cels = new List<Cel>();
            foreach (var fr in frames) { var c = fr.Cels[ri]; if (c != null && !c.Img.Empty && !cels.Contains(c)) cels.Add(c); }
            if (cels.Count == 0) { Say("이 레이어에 그림이 없어요."); return; }
            int refIdx = CurCel != null && cels.Contains(CurCel) ? cels.IndexOf(CurCel) : 0;
            var imgs = cels.Select(c => c.Img.ToRgba()).ToList();
            var sh = SpriteSheet.Shifts(imgs, W, H, alignMode, refIdx, W / 2.0, H);
            if (xf != null) CommitTransform();
            Push(Capture(null, cels));
            int moved = 0;
            for (int i = 0; i < cels.Count; i++)
            {
                if (sh[i].dx == 0 && sh[i].dy == 0) continue;
                cels[i].Img = Shifted(cels[i].Img, sh[i].dx, sh[i].dy);
                moved++;
            }
            dirty = true;
            RecomposeAll();
            Say($"‘{R.Name}’ {cels.Count}컷 중 {moved}컷을 {ALIGN[(int)alignMode]} 기준으로 맞춤(기준 = 프레임 {fi + 1}) — 마음에 안 들면 Ctrl+Z, 한 컷만 손볼 땐 이동(V).");
        }

        TileImage Shifted(TileImage src, int dx, int dy)
        {
            var ni = new TileImage(W, H);
            for (int i = 0; i < src.Count; i++)
            {
                var t = src.Tiles[i];
                if (t == null) continue;
                src.TileRect(i, out int x0, out int y0, out int tw, out int th);
                ni.Blit(t, tw, th, x0 + dx, y0 + dy);
            }
            return ni;
        }

        // ───────── 선택 영역(사각 · 올가미 · 꺾은선) ─────────
        //  Shift = 더하기 · Alt = 빼기. Ctrl+A 전체 · Ctrl+D 해제 · Ctrl+Shift+I 반전 · Delete 지우기.
        //  선택이 있으면 그림 도구는 선택 안에만 칠해짐.
        bool[] sel;                                  // W×H, null = 선택 없음(전체)
        int selL, selT, selR, selB;                  // 선택 영역 상자(끝 다음)
        readonly List<(Vector2 a, Vector2 b)> selSegs = new List<(Vector2, Vector2)>();
        int selMode;                                 // 0 새로 · 1 더하기 · 2 빼기
        bool selecting;
        Vector2Int selA, selB2;
        readonly List<Vector2> lasso = new List<Vector2>();
        List<Vector2> poly;                          // 꺾은선 선택 중인 점들
        Vector2 hoverF;

        static bool IsSelTool(Tool t) => t == Tool.SelRect || t == Tool.Lasso || t == Tool.Poly;

        /// <summary>화면 좌표 → 캔버스 칸(view) 기준 좌표. e.localPosition 은 누른 자식 요소 · 잡은 요소 기준일 수 있어 언제나 이걸로.</summary>
        Vector2 VL(Vector3 worldPos) => view.WorldToLocal((Vector2)worldPos);

        Vector2 PxAtF(Vector2 viewLocal) { var o = Origin(); return new Vector2((viewLocal.x - o.x) / zoom, (viewLocal.y - o.y) / zoom); }

        void SelDown(PointerDownEvent e, Vector2 pf, Vector2Int p)
        {
            int mode = e.shiftKey ? 1 : e.altKey ? 2 : 0;
            if (tool == Tool.Poly)
            {
                if (poly == null) { poly = new List<Vector2> { pf }; selMode = mode; }
                else if (poly.Count >= 3 && ((pf - poly[0]).magnitude * zoom < 10 || e.clickCount >= 2)) ClosePoly();
                else poly.Add(pf);
                overlay.MarkDirtyRepaint();
                return;
            }
            selMode = mode;
            selecting = true;
            view.CapturePointer(e.pointerId);
            if (tool == Tool.SelRect) { selA = selB2 = p; }
            else { lasso.Clear(); lasso.Add(pf); }
            overlay.MarkDirtyRepaint();
        }

        void SelMove(Vector2 pf, Vector2Int p)
        {
            if (tool == Tool.SelRect) selB2 = p;
            else if (lasso.Count == 0 || (pf - lasso[lasso.Count - 1]).magnitude >= 0.5f) lasso.Add(pf);
            overlay.MarkDirtyRepaint();
        }

        void SelUp(PointerUpEvent e)
        {
            selecting = false;
            view.ReleasePointer(e.pointerId);
            if (tool == Tool.SelRect)
            {
                if (selA == selB2 && selMode == 0) { SetSel(null); return; }        // 그냥 누르면 해제(포토샵처럼)
                int x0 = Mathf.Clamp(Math.Min(selA.x, selB2.x), 0, W), x1 = Mathf.Clamp(Math.Max(selA.x, selB2.x) + 1, 0, W);
                int y0 = Mathf.Clamp(Math.Min(selA.y, selB2.y), 0, H), y1 = Mathf.Clamp(Math.Max(selA.y, selB2.y) + 1, 0, H);
                var m = new bool[W * H];
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) m[y * W + x] = true;
                Combine(m);
            }
            else
            {
                if (lasso.Count < 3) { if (selMode == 0) SetSel(null); lasso.Clear(); overlay.MarkDirtyRepaint(); return; }
                Combine(PolyMask(lasso));
                lasso.Clear();
            }
            overlay.MarkDirtyRepaint();
        }

        void ClosePoly()
        {
            if (poly == null) return;
            if (poly.Count >= 3) Combine(PolyMask(poly));
            poly = null;
            overlay.MarkDirtyRepaint();
        }

        /// <summary>다각형(캔버스 픽셀 좌표) 안의 픽셀 — 픽셀 가운데 기준, 짝홀 규칙.</summary>
        bool[] PolyMask(List<Vector2> pts)
        {
            var m = new bool[W * H];
            float minY = pts.Min(q => q.y), maxY = pts.Max(q => q.y);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(minY), 0, H), y1 = Mathf.Clamp(Mathf.CeilToInt(maxY) + 1, 0, H);
            var xs = new List<float>();
            for (int y = y0; y < y1; y++)
            {
                float yc = y + 0.5f;
                xs.Clear();
                for (int i = 0; i < pts.Count; i++)
                {
                    var a = pts[i]; var b = pts[(i + 1) % pts.Count];
                    if ((a.y <= yc && b.y > yc) || (b.y <= yc && a.y > yc)) xs.Add(a.x + (yc - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int xa = Mathf.Clamp(Mathf.CeilToInt(xs[k] - 0.5f), 0, W), xb = Mathf.Clamp(Mathf.CeilToInt(xs[k + 1] - 0.5f), 0, W);
                    for (int x = xa; x < xb; x++) m[y * W + x] = true;
                }
            }
            return m;
        }

        void Combine(bool[] m)
        {
            if (selMode == 0 || sel == null)
            {
                if (selMode == 2) return;                // 선택 없음에서 빼기 = 그대로
                SetSel(m); return;
            }
            var n = (bool[])sel.Clone();
            for (int i = 0; i < n.Length; i++) n[i] = selMode == 1 ? n[i] || m[i] : n[i] && !m[i];
            SetSel(n);
        }

        /// <summary>선택 바꾸기(빈 선택 = 없음) — 상자 · 테두리 선 다시 계산.</summary>
        void SetSel(bool[] m)
        {
            sel = null; selSegs.Clear();
            if (m != null && m.Length == W * H)
            {
                int l = W, t = H, r = 0, b = 0;
                for (int y = 0; y < H; y++)
                {
                    int row = y * W;
                    for (int x = 0; x < W; x++) if (m[row + x]) { if (x < l) l = x; if (x + 1 > r) r = x + 1; if (y < t) t = y; b = y + 1; }
                }
                if (r > l) { sel = m; selL = l; selT = t; selR = r; selB = b; BuildSelSegs(); }
            }
            selEl?.MarkDirtyRepaint();
        }

        bool Sel(int x, int y) => sel == null || (x >= 0 && y >= 0 && x < W && y < H && sel[y * W + x]);

        /// <summary>선택 테두리: 선택 · 비선택 사이 경계를 이어진 선분으로.</summary>
        void BuildSelSegs()
        {
            selSegs.Clear();
            bool At(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && sel[y * W + x];
            for (int y = selT; y <= selB; y++)                     // 가로 경계(y 줄 위쪽)
            {
                int run = -1;
                for (int x = selL; x <= selR; x++)
                {
                    bool edge = x < selR && At(x, y) != At(x, y - 1);
                    if (edge && run < 0) run = x;
                    if (!edge && run >= 0) { selSegs.Add((new Vector2(run, y), new Vector2(x, y))); run = -1; }
                }
            }
            for (int x = selL; x <= selR; x++)                     // 세로 경계(x 칸 왼쪽)
            {
                int run = -1;
                for (int y = selT; y <= selB; y++)
                {
                    bool edge = y < selB && At(x, y) != At(x - 1, y);
                    if (edge && run < 0) run = y;
                    if (!edge && run >= 0) { selSegs.Add((new Vector2(x, run), new Vector2(x, y))); run = -1; }
                }
            }
        }

        void DrawSel(MeshGenerationContext ctx)
        {
            if (sel == null || selSegs.Count == 0) return;
            var p = ctx.painter2D;

            for (int pass = 0; pass < 2; pass++)
            {
                p.strokeColor = pass == 0 ? new Color(0, 0, 0, 0.9f) : new Color(1, 1, 1, 1);
                p.lineWidth = pass == 0 ? 3 : 1;
                p.BeginPath();
                foreach (var (a, b) in selSegs)
                {
                    var a2 = xf != null && xf.Sel != null ? XfMap(a) : a; var b2 = xf != null && xf.Sel != null ? XfMap(b) : b;
                    p.MoveTo(a2 * zoom); p.LineTo(b2 * zoom);
                }
                p.Stroke();
            }
        }

        void SelectAll() { var m = new bool[W * H]; for (int i = 0; i < m.Length; i++) m[i] = true; SetSel(m); Say("전체 선택 — Ctrl+T 로 옮기기 · Ctrl+D 해제"); }
        void Deselect() { poly = null; SetSel(null); overlay.MarkDirtyRepaint(); }
        void InvertSel()
        {
            var m = new bool[W * H];
            for (int i = 0; i < m.Length; i++) m[i] = sel == null ? false : !sel[i];
            SetSel(m);
        }

        /// <summary>선택 영역(없으면 칸 전체)의 그림 지우기.</summary>
        // ───────── 복사 · 잘라내기 · 붙여넣기 ─────────
        //  Ctrl+C = 고른 영역(없으면 이 칸 전체) 복사 · Ctrl+X = 잘라내기 · Ctrl+V = 지금 레이어에 붙여넣기 · Ctrl+Shift+V = 새 레이어에.
        //  붙여넣은 그림은 바로 자유 변형 상태(끌기 · 방향키 · 모서리 = 크기) — Enter 확정 · Esc 취소. 다른 레이어 · 프레임 · 동작(파일)으로도.
        static byte[] clipPx;
        static int clipL, clipT, clipW, clipH, clipCanvasW, clipCanvasH;
        static string clipFrom;

        void Copy(bool cut)
        {
            if (xf != null) CommitTransform();
            var c = CurCel;
            if (c == null || c.Img.Empty || R == null) { Say("이 칸에 그림이 없어요."); return; }
            int l, t, r, b;
            if (sel != null) { l = selL; t = selT; r = selR; b = selB; }
            else if (!c.Img.Bounds(out l, out t, out r, out b)) { Say("이 칸에 그림이 없어요."); return; }
            int w = r - l, h = b - t;
            var px = new byte[w * h * 4];
            c.Img.CopyTo(px, l, t, w, h);
            bool any = false;
            if (sel != null)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int k = (y * w + x) * 4;
                        if (!sel[(t + y) * W + l + x]) { px[k] = px[k + 1] = px[k + 2] = px[k + 3] = 0; continue; }
                        if (px[k + 3] != 0) any = true;
                    }
            else any = true;
            if (!any) { Say("고른 영역에 그림이 없어요."); return; }
            clipPx = px; clipL = l; clipT = t; clipW = w; clipH = h; clipCanvasW = W; clipCanvasH = H;
            clipFrom = $"{key} · {R.Name} · 프레임 {fi + 1}";
            if (cut) { DeleteSel(); Say($"잘라냄 {w}×{h} — Ctrl+V 로 붙여넣기"); }
            else Say($"복사 {w}×{h}{(sel != null ? "(고른 영역)" : "")} — 다른 레이어 · 프레임을 골라 Ctrl+V(새 레이어로 = Ctrl+Shift+V)");
        }

        void Paste(bool newLayer)
        {
            if (clipPx == null) { Say("복사한 그림이 없어요 — 먼저 Ctrl+C."); return; }
            if (!Edit() || frames.Count == 0) return;
            if (xf != null) CommitTransform();
            poly = null;
            if (newLayer || rows.Count == 0)
            {
                SnapStruct();
                int at = rows.Count == 0 ? 0 : ri + 1;
                rows.Insert(at, new Row { Name = UniqueRowName("붙여넣기 " + (rows.Count + 1)) });
                foreach (var fr in frames) fr.Cels.Insert(at, null);
                ri = at;
                RecomposeAll();
            }
            if (R.Locked) { Say("잠긴 레이어예요."); return; }
            if (play) { play = false; }
            // 원래 자리 그대로 — 캔버스 크기가 달라서 밖으로 나가면 가운데로
            int dx = clipL, dy = clipT;
            if (dx + clipW > W || dy + clipH > H || clipCanvasW != W || clipCanvasH != H)
            {
                if (dx + clipW > W || dy + clipH > H) { dx = Math.Max(0, (W - clipW) / 2); dy = Math.Max(0, (H - clipH) / 2); }
            }
            if (CurCel == null) { SnapStruct(); F.Cels[ri] = NewCel(); }
            var c = CurCel;
            var flt = new TileImage(W, H);
            flt.Blit(clipPx, clipW, clipH, dx, dy);
            if (!flt.Bounds(out int l, out int t, out int r, out int b)) { Say("붙일 그림이 캔버스 밖이에요."); return; }
            SetSel(null);
            var it = new XfItem { Cel = c, Orig = c.Img.Clone(), Base = c.Img.Clone(), Flt = flt, Stale = true };
            var x = new Xf { Fi = fi, Ri = ri, Sel = null, L = l, T = t, R = r, B = b, Pasted = true };
            x.Items.Add(it);
            it.Px = new byte[x.Sw * x.Sh * 4]; flt.CopyTo(it.Px, l, t, x.Sw, x.Sh);
            x.X0 = pX0 = l; x.Y0 = pY0 = t; x.X1 = pX1 = r; x.Y1 = pY1 = b;
            x.Snap = Capture(null, new[] { c });
            Push(x.Snap);
            xf = x;
            moveDirty = true;
            RefreshXfBar();
            Refresh();
            Say($"붙여넣음({clipFrom}) — 끌기 · 방향키로 맞추고 Enter(또는 Ctrl+T)로 확정 · Esc 취소");
        }

        // ───────── 레이어 통째 복사 · 붙여넣기(모든 프레임 포함, 다른 동작으로 옮기기) ─────────
        //  Ctrl+Alt+C = 고른 레이어들(타임라인에서 Ctrl · Shift+누르기로 여럿) 을 모든 프레임 그림까지 복사
        //  Ctrl+Alt+V = 지금 레이어 위에 그대로 끼워 넣기 — 프레임이 모자라면 원본 길이로 프레임을 보탬, 이어 쓰던 칸(앞 그림 유지)도 그대로.
        //  캔버스 크기가 다르면 아래 · 가운데를 맞춰서(발밑 기준).
        sealed class LayerClip
        {
            public string From;
            public int W, H;
            public readonly List<int> Durs = new List<int>();
            public readonly List<(Row row, List<(int l, int t, int w, int h, byte[] px)> imgs, int[] map)> Layers = new List<(Row, List<(int, int, int, int, byte[])>, int[])>();
        }
        static LayerClip layerClip;

        void CopyLayers()
        {
            if (xf != null) CommitTransform();
            if (rows.Count == 0 || frames.Count == 0) { Say("복사할 레이어가 없어요."); return; }
            var idx = SelRows();
            var clip = new LayerClip { W = W, H = H, From = key };
            foreach (var f in frames) clip.Durs.Add(f.Dur);
            int pics = 0;
            foreach (int r in idx)
            {
                var imgs = new List<(int, int, int, int, byte[])>();
                var map = new int[frames.Count];
                var seen = new Dictionary<Cel, int>();
                for (int f = 0; f < frames.Count; f++)
                {
                    var c = frames[f].Cels[r];
                    if (c == null || !c.Img.Bounds(out int l, out int t, out int rr, out int b)) { map[f] = -1; continue; }
                    if (!seen.TryGetValue(c, out int k))
                    {
                        var px = new byte[(rr - l) * (b - t) * 4];
                        c.Img.CopyTo(px, l, t, rr - l, b - t);
                        k = imgs.Count; imgs.Add((l, t, rr - l, b - t, px)); seen[c] = k;       // 같은 칸(이어 쓰는 그림)은 한 번만
                    }
                    map[f] = k;
                }
                pics += imgs.Count;
                clip.Layers.Add((rows[r].Clone(), imgs, map));
            }
            layerClip = clip;
            string names = string.Join(", ", idx.Select(i => rows[i].Name).Take(4)) + (idx.Count > 4 ? " …" : "");
            Say($"레이어 {idx.Count}개 복사({names}) — 프레임 {frames.Count}개 · 그림 {pics}장. 다른 동작을 열고 Ctrl+Alt+V 로 끼워 넣기");
        }

        void PasteLayers()
        {
            var lc = layerClip;
            if (lc == null || lc.Layers.Count == 0) { Say("복사한 레이어가 없어요 — 타임라인에서 레이어를 고르고 Ctrl+Alt+C."); return; }
            if (!Edit()) return;
            if (xf != null) CommitTransform();
            poly = null; play = false;
            SnapStruct();
            int added = 0;
            while (frames.Count < lc.Durs.Count)                               // 프레임이 모자라면 원본 길이로 보탬
            {
                var fr = new Frame { Dur = lc.Durs[frames.Count] };
                for (int i = 0; i < rows.Count; i++) fr.Cels.Add(null);
                frames.Add(fr); added++;
            }
            bool same = lc.W == W && lc.H == H;
            int dx = same ? 0 : (W - lc.W) / 2, dy = same ? 0 : H - lc.H;      // 크기가 다르면 아래 · 가운데 맞춤
            int at = rows.Count == 0 ? 0 : ri + 1;
            rSel.Clear();
            int lost = 0;
            for (int li = 0; li < lc.Layers.Count; li++)
            {
                var (row, imgs, map) = lc.Layers[li];
                var nr = row.Clone();
                nr.Name = UniqueRowName(row.Name);
                rows.Insert(at + li, nr);
                var cels = new List<Cel>();
                foreach (var im in imgs)
                {
                    var c = NewCel();
                    c.Img.Blit(im.px, im.w, im.h, im.l + dx, im.t + dy);
                    if (im.l + dx < 0 || im.t + dy < 0 || im.l + dx + im.w > W || im.t + dy + im.h > H) lost++;
                    cels.Add(c);
                }
                for (int f = 0; f < frames.Count; f++)
                    frames[f].Cels.Insert(at + li, f < map.Length && map[f] >= 0 ? cels[map[f]] : null);
                rSel.Add(nr);
            }
            ri = at + lc.Layers.Count - 1;
            rAnchor = at;
            dirty = true;
            RecomposeAll();
            Say($"레이어 {lc.Layers.Count}개 끼워 넣음({lc.From})"
                + (added > 0 ? $" · 프레임 {added}개 보탬" : "")
                + (lc.Durs.Count < frames.Count ? $" · 원본은 프레임 {lc.Durs.Count}개라 뒤쪽은 비어 있음" : "")
                + (!same ? $" · 캔버스 크기가 달라 아래 · 가운데 맞춤" : "")
                + (lost > 0 ? $" · 그림 {lost}장은 캔버스 밖으로 일부 잘림" : ""));
        }

        void DeleteSel()
        {
            if (!Edit() || CurCel == null || R.Locked) return;
            SnapPixels(CurCel);
            strokeCel = CurCel; strokeTiles.Clear();
            int x0 = sel != null ? selL : 0, x1 = sel != null ? selR : W, y0 = sel != null ? selT : 0, y1 = sel != null ? selB : H;
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) if (Sel(x, y)) Touch(strokeCel, strokeCel.Img.Set(x, y, 0, 0, 0, 0));
            EndStroke();
            dirty = true; RefreshTitle();
            Say(sel != null ? "선택 영역을 지웠어요" : "이 칸을 지웠어요");
        }

        // ───────── 자유 변형(Ctrl+T) — 포토샵 · 클립 스튜디오처럼 ─────────
        //  대상: 고른 프레임 × 고른 레이어의 칸(하나만 골랐으면 지금 칸). 선택 영역이 있으면 그 안의 그림만.
        //  상자 안 끌기 = 옮기기 · 모서리 = 크기(비율 유지, Shift = 자유) · 변 = 한쪽만 · Alt = 가운데 기준 · 반대편 너머로 끌면 뒤집기.
        //  방향키 1px · Shift 10px · 아래 칸에 가로 · 세로 % · Enter / Ctrl+T 확정 · Esc / Ctrl+Z 취소.
        //  여러 칸은 같은 상자로 함께 변형(서로 위치 관계 그대로). 끄는 동안은 지금 프레임만 그리고, 놓으면 나머지.
        sealed class XfItem { public Cel Cel; public TileImage Orig, Base, Flt; public byte[] Px; public bool Stale; }
        sealed class Xf
        {
            public List<XfItem> Items = new List<XfItem>();
            public bool[] Sel;
            public int L, T, R, B;                 // 원래 상자(끝 다음)
            public float X0, Y0, X1, Y1;           // 그려진 상자(X0 = 원래 왼쪽 변이 간 곳 — X1 보다 크면 좌우 뒤집힘)
            public int Fi, Ri;
            public Snap Snap;
            public bool Pasted;                    // 붙여넣기로 시작(안 옮겨도 기록 남김)
            public int Sw => R - L;
            public int Sh => B - T;
        }
        Xf xf;
        float pX0, pY0, pX1, pY1;                  // 바라는 상자(Tick 에서 반영)
        bool moving, moveDirty, xfSmooth, xfLock = true;
        int dragHx, dragHy;                        // 잡은 곳: −1 = 원래 왼쪽/위 변, +1 = 오른쪽/아래, 0 = 아님(둘 다 0 = 옮기기)
        Vector2 dragStart;
        float sX0, sY0, sX1, sY1;
        VisualElement xfBar;
        IntegerField xfWp, xfHp;
        Label xfInfo;
        Button xfSmoothBtn, xfLockBtn;

        void BuildXfBar(VisualElement main)
        {
            xfBar = El(main, "ae-xf");
            xfBar.style.display = DisplayStyle.None;
            Lbl(xfBar, "변형", "ae-k");
            Lbl(xfBar, "가로 %", "ae-k"); xfWp = Int(xfBar, 100);
            Lbl(xfBar, "세로 %", "ae-k"); xfHp = Int(xfBar, 100);
            xfLockBtn = Btn(xfBar, "비율 고정", () => { xfLock = !xfLock; RefreshXfBar(); }, "숫자로 바꿀 때 가로 · 세로를 같이(마우스는 모서리 = 비율 유지, Shift = 자유)");
            xfSmoothBtn = Btn(xfBar, "또렷하게", () => { xfSmooth = !xfSmooth; MarkXfAll(); RefreshXfBar(); }, "또렷하게 = 픽셀 그대로(도트) · 부드럽게 = 섞어서(고해상도 그림)");
            Btn(xfBar, "1:1", () => { if (xf == null) return; float cx = (pX0 + pX1) / 2, cy = (pY0 + pY1) / 2; pX0 = Mathf.Round(cx - xf.Sw / 2f); pX1 = pX0 + xf.Sw; pY0 = Mathf.Round(cy - xf.Sh / 2f); pY1 = pY0 + xf.Sh; }, "원래 크기로(위치는 그대로)");
            Btn(xfBar, "확정", CommitTransform, "Enter · Ctrl+T", "ae-btn--main");
            Btn(xfBar, "취소", CancelTransform, "Esc");
            xfInfo = Lbl(xfBar, "", "ae-k");
            xfWp.RegisterValueChangedCallback(e => SetPercent(e.newValue, xfLock ? e.newValue : (int?)null));
            xfHp.RegisterValueChangedCallback(e => SetPercent(xfLock ? e.newValue : (int?)null, e.newValue));
        }

        void SetPercent(int? wp, int? hp)
        {
            if (xf == null) return;
            float cx = (pX0 + pX1) / 2, cy = (pY0 + pY1) / 2;
            if (wp.HasValue)
            {
                float w = Mathf.Max(1, Mathf.Round(xf.Sw * Mathf.Clamp(wp.Value, 1, 2000) / 100f)) * Mathf.Sign(pX1 - pX0 == 0 ? 1 : pX1 - pX0);
                pX0 = Mathf.Round(cx - w / 2); pX1 = pX0 + w;
            }
            if (hp.HasValue)
            {
                float h = Mathf.Max(1, Mathf.Round(xf.Sh * Mathf.Clamp(hp.Value, 1, 2000) / 100f)) * Mathf.Sign(pY1 - pY0 == 0 ? 1 : pY1 - pY0);
                pY0 = Mathf.Round(cy - h / 2); pY1 = pY0 + h;
            }
        }

        void RefreshXfBar()
        {
            if (xfBar == null) return;
            xfBar.style.display = xf != null ? DisplayStyle.Flex : DisplayStyle.None;
            xfLockBtn.EnableInClassList("ae-btn--on", xfLock);
            xfSmoothBtn.text = xfSmooth ? "부드럽게" : "또렷하게";
            xfSmoothBtn.EnableInClassList("ae-btn--on", xfSmooth);
            if (xf == null) return;
            float w = xf.X1 - xf.X0, h = xf.Y1 - xf.Y0;
            xfWp.SetValueWithoutNotify(Mathf.RoundToInt(Mathf.Abs(w) / xf.Sw * 100));
            xfHp.SetValueWithoutNotify(Mathf.RoundToInt(Mathf.Abs(h) / xf.Sh * 100));
            xfInfo.text = $"{Mathf.Abs(w):0}×{Mathf.Abs(h):0}px{(w < 0 ? " · 좌우 뒤집힘" : "")}{(h < 0 ? " · 위아래 뒤집힘" : "")} · {xf.Items.Count}칸";
        }

        void BeginTransform()
        {
            if (xf != null) { CommitTransform(); return; }
            if (!Edit() || frames.Count == 0 || rows.Count == 0) return;
            poly = null;
            var cels = new List<Cel>();
            foreach (int f in SelFrames())
                foreach (int r in SelRows())
                {
                    if (rows[r].Locked) continue;
                    var c = frames[f].Cels[r];
                    if (c != null && !c.Img.Empty && !cels.Contains(c)) cels.Add(c);
                }
            if (cels.Count == 0) { Say(R != null && R.Locked ? "잠긴 레이어예요." : "고른 칸에 그림이 없어요."); return; }
            var t = new Xf { Fi = fi, Ri = ri, Sel = sel };
            int L = W, T = H, Rr = 0, Bb = 0;
            foreach (var c in cels)
            {
                var it = new XfItem { Cel = c, Orig = c.Img.Clone() };
                if (sel != null)
                {
                    it.Base = c.Img.Clone(); it.Flt = new TileImage(W, H);
                    for (int y = selT; y < selB; y++)
                        for (int x = selL; x < selR; x++)
                        {
                            if (!sel[y * W + x]) continue;
                            it.Orig.Get(x, y, out var r0, out var g0, out var b0, out var a0);
                            if (a0 == 0) continue;
                            it.Flt.Set(x, y, r0, g0, b0, a0);
                            it.Base.Set(x, y, 0, 0, 0, 0);
                        }
                }
                else it.Flt = it.Orig;
                if (!it.Flt.Bounds(out int l, out int tt, out int rr, out int bb)) continue;
                L = Math.Min(L, l); T = Math.Min(T, tt); Rr = Math.Max(Rr, rr); Bb = Math.Max(Bb, bb);
                t.Items.Add(it);
            }
            if (t.Items.Count == 0) { Say("선택 영역에 그림이 없어요."); return; }
            t.L = L; t.T = T; t.R = Rr; t.B = Bb;
            foreach (var it in t.Items) { it.Px = new byte[t.Sw * t.Sh * 4]; it.Flt.CopyTo(it.Px, L, T, t.Sw, t.Sh); }
            t.X0 = pX0 = L; t.Y0 = pY0 = T; t.X1 = pX1 = Rr; t.Y1 = pY1 = Bb;
            if (play) { play = false; Refresh(); }
            t.Snap = Capture(null, t.Items.Select(i => i.Cel));
            Push(t.Snap);
            xf = t;
            RefreshXfBar();
            Say((sel != null ? "선택한 그림" : t.Items.Count > 1 ? $"고른 칸 {t.Items.Count}개" : $"‘{R.Name}’ 전체") + " 변형 — 안 = 옮기기 · 모서리 = 크기(Shift = 자유) · 변 = 한쪽 · Alt = 가운데 기준 · Enter 확정 · Esc 취소");
            overlay.MarkDirtyRepaint();
        }

        void TickXf()
        {
            if (xf == null) return;
            if (xf.Fi != fi || xf.Ri != ri || frames.Count == 0) { CommitTransform(); return; }
            ApplyXf(!moving);
        }

        void MarkXfAll() { if (xf == null) return; foreach (var it in xf.Items) it.Stale = true; moveDirty = true; }

        /// <summary>바라는 상자를 그림에 반영. all = 지금 프레임 밖의 칸도(끄는 중이 아닐 때).</summary>
        void ApplyXf(bool all)
        {
            if (xf == null) return;
            if (pX0 != xf.X0 || pX1 != xf.X1 || pY0 != xf.Y0 || pY1 != xf.Y1)
            {
                xf.X0 = pX0; xf.X1 = pX1; xf.Y0 = pY0; xf.Y1 = pY1;
                foreach (var it in xf.Items) it.Stale = true;
                dirty = true;
                RefreshTitle(); RefreshXfBar();
                selEl.MarkDirtyRepaint(); overlay.MarkDirtyRepaint();
            }
            var done = new List<Cel>();
            var cur = frames.Count > 0 ? F : null;
            foreach (var it in xf.Items)
            {
                if (!it.Stale) continue;
                if (!all && (cur == null || !cur.Cels.Contains(it.Cel))) continue;
                it.Cel.Img = RenderXf(it);
                it.Stale = false; done.Add(it.Cel);
            }
            if (done.Count == 0 && !moveDirty) return;
            if (moveDirty && done.Count == 0) done.AddRange(xf.Items.Select(i => i.Cel));
            moveDirty = false;
            bool lazy = (long)W * H * frames.Count > BIG;
            for (int f = 0; f < frames.Count; f++)
            {
                if (!done.Any(c => frames[f].Cels.Contains(c))) continue;
                if (lazy && f != fi) { while (stale.Count <= f) stale.Add(true); stale[f] = true; }
                else ComposeFrame(f);
            }
        }

        /// <summary>뜬 그림을 지금 상자에 맞춰 그린 칸 그림.</summary>
        TileImage RenderXf(XfItem it)
        {
            var t = xf;
            float w = t.X1 - t.X0, h = t.Y1 - t.Y0;
            // 크기 그대로 · 정수 위치 = 빠른 길
            if (w == t.Sw && h == t.Sh && t.X0 == Mathf.Round(t.X0) && t.Y0 == Mathf.Round(t.Y0))
            {
                int dx = (int)t.X0 - t.L, dy = (int)t.Y0 - t.T;
                if (it.Base == null) return Shifted(it.Flt, dx, dy);
                var im = it.Base.Clone(); Paste(im, it.Flt, dx, dy); return im;
            }
            int x0 = Mathf.FloorToInt(Mathf.Min(t.X0, t.X1)), x1 = Mathf.CeilToInt(Mathf.Max(t.X0, t.X1));
            int y0 = Mathf.FloorToInt(Mathf.Min(t.Y0, t.Y1)), y1 = Mathf.CeilToInt(Mathf.Max(t.Y0, t.Y1));
            x0 = Math.Max(x0, 0); y0 = Math.Max(y0, 0); x1 = Math.Min(x1, W); y1 = Math.Min(y1, H);
            var res = it.Base != null ? it.Base.Clone() : new TileImage(W, H);
            if (x1 <= x0 || y1 <= y0) return res;
            int dw = x1 - x0, dh = y1 - y0, sw = t.Sw, sh = t.Sh;
            var buf = new byte[dw * dh * 4];
            var src = it.Px;
            for (int y = 0; y < dh; y++)
            {
                float v = (y0 + y + 0.5f - t.Y0) / h;              // 0~1(뒤집혀도 맞음)
                if (v < 0 || v >= 1) continue;
                float syf = v * sh;
                for (int x = 0; x < dw; x++)
                {
                    float u = (x0 + x + 0.5f - t.X0) / w;
                    if (u < 0 || u >= 1) continue;
                    float sxf = u * sw;
                    int o = (y * dw + x) * 4;
                    if (!xfSmooth)
                    {
                        int sx = Math.Min(sw - 1, (int)sxf), sy = Math.Min(sh - 1, (int)syf);
                        int k = (sy * sw + sx) * 4;
                        buf[o] = src[k]; buf[o + 1] = src[k + 1]; buf[o + 2] = src[k + 2]; buf[o + 3] = src[k + 3];
                    }
                    else Bilinear(src, sw, sh, sxf - 0.5f, syf - 0.5f, buf, o);
                }
            }
            if (it.Base == null) { res.Blit(buf, dw, dh, x0, y0); return res; }
            for (int y = 0; y < dh; y++)
                for (int x = 0; x < dw; x++)
                {
                    int o = (y * dw + x) * 4;
                    if (buf[o + 3] != 0) res.Set(x0 + x, y0 + y, buf[o], buf[o + 1], buf[o + 2], buf[o + 3]);
                }
            return res;
        }

        static void Bilinear(byte[] s, int w, int h, float fx, float fy, byte[] d, int o)
        {
            int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
            float ax = fx - ix, ay = fy - iy;
            float r = 0, g = 0, b = 0, a = 0;
            for (int j = 0; j < 2; j++)
                for (int i = 0; i < 2; i++)
                {
                    int x = ix + i, y = iy + j;
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    float wt = (i == 0 ? 1 - ax : ax) * (j == 0 ? 1 - ay : ay);
                    int k = (y * w + x) * 4;
                    float al = s[k + 3] * wt;
                    r += s[k] * al; g += s[k + 1] * al; b += s[k + 2] * al; a += al;
                }
            if (a <= 0.5f) { d[o] = d[o + 1] = d[o + 2] = d[o + 3] = 0; return; }
            d[o] = (byte)Mathf.Clamp(Mathf.RoundToInt(r / a), 0, 255);
            d[o + 1] = (byte)Mathf.Clamp(Mathf.RoundToInt(g / a), 0, 255);
            d[o + 2] = (byte)Mathf.Clamp(Mathf.RoundToInt(b / a), 0, 255);
            d[o + 3] = (byte)Mathf.Clamp(Mathf.RoundToInt(a), 0, 255);
        }

        /// <summary>src 의 그림(알파 &gt; 0)을 (dx, dy) 옮겨 dst 위에 덮어 씀.</summary>
        void Paste(TileImage dst, TileImage src, int dx, int dy)
        {
            for (int i = 0; i < src.Count; i++)
            {
                var t = src.Tiles[i];
                if (t == null) continue;
                src.TileRect(i, out int x0, out int y0, out int tw, out int th);
                for (int y = 0; y < th; y++)
                    for (int x = 0; x < tw; x++)
                    {
                        int k = (y * tw + x) * 4;
                        if (t[k + 3] == 0) continue;
                        dst.Set(x0 + x + dx, y0 + y + dy, t[k], t[k + 1], t[k + 2], t[k + 3]);
                    }
            }
        }

        /// <summary>원래 상자 좌표 → 지금 상자 좌표.</summary>
        Vector2 XfMap(Vector2 p)
        {
            var t = xf;
            return new Vector2(t.X0 + (p.x - t.L) / t.Sw * (t.X1 - t.X0), t.Y0 + (p.y - t.T) / t.Sh * (t.Y1 - t.Y0));
        }

        // ── 마우스로 잡기
        void XfDown(PointerDownEvent e, Vector2 pf)
        {
            // 크기 점: 지금 보이는 상자(pX · pY) 기준 · 화면에서 12px 안이면 잡힘(상자 안쪽이어도 점이 먼저)
            float tol = 12f / Mathf.Max(0.01f, zoom);
            float[] xs = { pX0, (pX0 + pX1) / 2, pX1 }, ys = { pY0, (pY0 + pY1) / 2, pY1 };
            dragHx = dragHy = 0;
            float best = float.MaxValue;
            for (int j = 0; j < 3; j++)
                for (int i = 0; i < 3; i++)
                {
                    if (i == 1 && j == 1) continue;
                    float d = Mathf.Max(Mathf.Abs(pf.x - xs[i]), Mathf.Abs(pf.y - ys[j]));
                    if (d <= tol && d < best) { best = d; dragHx = i - 1; dragHy = j - 1; }
                }
            dragStart = pf;
            sX0 = pX0; sY0 = pY0; sX1 = pX1; sY1 = pY1;
            moving = true;
            view.CapturePointer(e.pointerId);
        }

        void XfDrag(Vector2 pf, bool shift, bool alt)
        {
            var d = pf - dragStart;
            if (dragHx == 0 && dragHy == 0)
            {
                float dx = Mathf.Round(d.x), dy = Mathf.Round(d.y);
                pX0 = sX0 + dx; pX1 = sX1 + dx; pY0 = sY0 + dy; pY1 = sY1 + dy;
                return;
            }
            float cx = (sX0 + sX1) / 2, cy = (sY0 + sY1) / 2;
            // 잡은 변의 처음 위치와 기준점(반대편 또는 가운데)
            float gx = dragHx < 0 ? sX0 : sX1, gy = dragHy < 0 ? sY0 : sY1;
            float ax = alt ? cx : dragHx < 0 ? sX1 : sX0, ay = alt ? cy : dragHy < 0 ? sY1 : sY0;
            float nx = gx + d.x, ny = gy + d.y;
            bool corner = dragHx != 0 && dragHy != 0;
            if (corner && !shift)
            {
                float w0 = gx - ax, h0 = gy - ay;
                float kx = w0 != 0 ? (nx - ax) / w0 : 1, ky = h0 != 0 ? (ny - ay) / h0 : 1;
                float k = Mathf.Abs(kx) >= Mathf.Abs(ky) ? kx : ky;
                nx = ax + w0 * k; ny = ay + h0 * k;
            }
            float X0 = sX0, X1 = sX1, Y0 = sY0, Y1 = sY1;
            if (dragHx != 0)
            {
                float v = Mathf.Round(nx), opp = alt ? Mathf.Round(2 * cx - nx) : (dragHx < 0 ? sX1 : sX0);
                if (dragHx < 0) { X0 = v; X1 = opp; } else { X1 = v; X0 = opp; }
                if (Mathf.Abs(X1 - X0) < 1) X1 = X0 + (X1 >= X0 ? 1 : -1);
            }
            if (dragHy != 0)
            {
                float v = Mathf.Round(ny), opp = alt ? Mathf.Round(2 * cy - ny) : (dragHy < 0 ? sY1 : sY0);
                if (dragHy < 0) { Y0 = v; Y1 = opp; } else { Y1 = v; Y0 = opp; }
                if (Mathf.Abs(Y1 - Y0) < 1) Y1 = Y0 + (Y1 >= Y0 ? 1 : -1);
            }
            pX0 = X0; pX1 = X1; pY0 = Y0; pY1 = Y1;
        }

        void XfUp() { moving = false; MarkXfAll(); }

        void DrawXfBox(Painter2D p)
        {
            var t = xf;
            float x0 = Mathf.Min(t.X0, t.X1) * zoom, x1 = Mathf.Max(t.X0, t.X1) * zoom, y0 = Mathf.Min(t.Y0, t.Y1) * zoom, y1 = Mathf.Max(t.Y0, t.Y1) * zoom;
            p.strokeColor = new Color(0, 0, 0, 0.9f); p.lineWidth = 3; p.BeginPath(); Rc(p, x0, y0, x1 - x0, y1 - y0); p.Stroke();
            p.strokeColor = new Color(1f, 0.83f, 0.2f, 1f); p.lineWidth = 1.5f; p.BeginPath(); Rc(p, x0, y0, x1 - x0, y1 - y0); p.Stroke();
            float mx = (x0 + x1) / 2, my = (y0 + y1) / 2;
            foreach (var (qx, qy) in new[] { (x0, y0), (mx, y0), (x1, y0), (x0, my), (x1, my), (x0, y1), (mx, y1), (x1, y1) })
            {
                p.fillColor = new Color(0, 0, 0, 0.9f); p.BeginPath(); Rc(p, qx - 5, qy - 5, 10, 10); p.Fill();
                p.fillColor = new Color(1f, 0.83f, 0.2f, 1f); p.BeginPath(); Rc(p, qx - 4, qy - 4, 8, 8); p.Fill();
            }
        }

        void CommitTransform()
        {
            if (xf == null) return;
            var t = xf;
            ApplyXf(true);
            xf = null;
            moving = false;
            bool changed = t.X0 != t.L || t.Y0 != t.T || t.X1 != t.R || t.Y1 != t.B;
            if (t.Sel != null && changed)
            {
                // 선택 영역도 같이 변형(가장 가까운 픽셀)
                var m = new bool[W * H];
                float w = t.X1 - t.X0, h = t.Y1 - t.Y0;
                for (int y = 0; y < H; y++)
                {
                    float v = (y + 0.5f - t.Y0) / h; if (v < 0 || v >= 1) continue;
                    int sy = t.T + Math.Min(t.Sh - 1, (int)(v * t.Sh));
                    for (int x = 0; x < W; x++)
                    {
                        float u = (x + 0.5f - t.X0) / w; if (u < 0 || u >= 1) continue;
                        int sx = t.L + Math.Min(t.Sw - 1, (int)(u * t.Sw));
                        if (t.Sel[sy * W + sx]) m[y * W + x] = true;
                    }
                }
                SetSel(m);
            }
            foreach (var it in t.Items) it.Cel.Img.Trim();
            if (!changed && !t.Pasted && undo.Count > 0 && undo[undo.Count - 1] == t.Snap) undo.RemoveAt(undo.Count - 1);   // 안 바꿨으면 기록 안 남김
            RefreshXfBar();
            selEl.MarkDirtyRepaint(); overlay.MarkDirtyRepaint();
            if (changed) Say($"변형 확정 — {Mathf.Abs(t.X1 - t.X0):0}×{Mathf.Abs(t.Y1 - t.Y0):0}px ({Mathf.Abs(t.X1 - t.X0) / t.Sw * 100:0}% × {Mathf.Abs(t.Y1 - t.Y0) / t.Sh * 100:0}%)");
        }

        void CancelTransform()
        {
            if (xf == null) return;
            var t = xf;
            xf = null; moving = false;
            foreach (var it in t.Items) it.Cel.Img = it.Orig;
            if (undo.Count > 0 && undo[undo.Count - 1] == t.Snap) undo.RemoveAt(undo.Count - 1);
            for (int f = 0; f < frames.Count; f++) if (t.Items.Any(i => frames[f].Cels.Contains(i.Cel))) ComposeFrame(f);
            RefreshXfBar();
            selEl.MarkDirtyRepaint(); overlay.MarkDirtyRepaint();
            Say("변형 취소");
        }

        void Nudge(Vector2Int d) { if (xf != null) { pX0 += d.x; pX1 += d.x; pY0 += d.y; pY1 += d.y; } }

        void SetTool(Tool t)
        {
            if (xf != null) CommitTransform();
            if (!IsSelTool(t)) poly = null;
            tool = t;
            Refresh();
        }

        // ───────── 레이어 이름 바꾸기(타임라인에서 두 번 누르기) ─────────
        TextField renameTf;
        bool renameReq;                              // 단축키로 이름 바꾸기 — 타임라인을 다시 만들 때 그 줄에서 시작
        Action cancelRename;

        void StartRename(VisualElement host, Button nb, int r)
        {
            if (readOnly || r < 0 || r >= rows.Count) return;
            ri = r;
            var tf = new TextField { value = rows[r].Name };
            tf.AddToClassList("ae-text"); tf.AddToClassList("ae-tl-rename");
            int at = host.IndexOf(nb);
            host.Remove(nb);
            host.Insert(Math.Max(0, at), tf);
            renameTf = tf;
            bool done = false;
            void Finish(bool ok)
            {
                if (done) return;
                done = true;
                renameTf = null; cancelRename = null;
                var v = (tf.value ?? "").Trim();
                if (ok && v.Length > 0 && r < rows.Count && v != rows[r].Name) { SnapStruct(); rows[r].Name = v; dirty = true; Say($"레이어 이름 → ‘{v}’"); }
                Refresh();
                Root.Focus();
            }
            cancelRename = () => Finish(false);
            tf.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Finish(true); e.StopPropagation(); }
                else if (e.keyCode == KeyCode.Escape) { Finish(false); e.StopPropagation(); }
            }, TrickleDown.TrickleDown);
            tf.RegisterCallback<FocusOutEvent>(_ => Finish(true));
            tf.schedule.Execute(() => { tf.Focus(); tf.SelectAll(); });
        }

        // ───────── 저장 폴더 열기 ─────────
        // ───────── 시트 내보내기(PNG + json · 프레임별 PNG) — PSD 저장과 따로 ─────────
        //  · 레이어를 합친 한 장(칸 = 캔버스 크기, 발밑 위치 그대로). 숨긴 레이어는 뺌.
        //  · json = 칸 크기 · 배치 · 프레임별 길이(f · ms) · fps · 반복 — 다시 가져오거나 다른 툴로 옮길 때 타이밍 그대로.
        //  · 내보내는 곳: 사용자 데이터 폴더/sprite-export (빌드에 들어가지 않음).
        VisualElement exportOpts;
        static readonly string[] EXP_LAYOUT = { "자동", "가로 한 줄", "세로 한 줄", "격자" };
        int expLayout = Mathf.Clamp(PlayerPrefs.GetInt("mori.export.layout", 0), 0, 3);
        int expCols = Mathf.Clamp(PlayerPrefs.GetInt("mori.export.cols", 8), 1, 64);
        int expPad = Mathf.Clamp(PlayerPrefs.GetInt("mori.export.pad", 0), 0, 64);
        int expFormat = Mathf.Clamp(PlayerPrefs.GetInt("mori.export.format", 0), 0, 1);      // 0 PNG(합친 한 장) · 1 PSD(레이어 살림)
        int expScale = ScaleOk(PlayerPrefs.GetInt("mori.export.scale", 1));
        static int ScaleOk(int v) => v == 2 || v == 4 ? v : 1;
        bool expFrames = PlayerPrefs.GetInt("mori.export.frames", 0) == 1;

        public static string ExportDir => Path.Combine(KitPaths.Data, "sprite-export");

        void SaveExportPrefs()
        {
            PlayerPrefs.SetInt("mori.export.layout", expLayout); PlayerPrefs.SetInt("mori.export.cols", expCols);
            PlayerPrefs.SetInt("mori.export.pad", expPad); PlayerPrefs.SetInt("mori.export.scale", expScale);
            PlayerPrefs.SetInt("mori.export.frames", expFrames ? 1 : 0);
            PlayerPrefs.SetInt("mori.export.format", expFormat);
            ExpUpdate();
        }

        // ───────── 내보내기 창: 저장 방식 · 배치 · 여백 · 배율 · 넣기 → 미리보기 → 내보내기 ─────────
        VisualElement expDlg, expPrev, expGrid, expColsRow, expFramesRow;
        Label expTitle, expInfo;
        IntegerField expColsF, expPadF;
        readonly List<Button> expFmtB = new List<Button>(), expLayB = new List<Button>(), expPadB = new List<Button>(), expScaleB = new List<Button>();
        Button expFrOn, expFrOff;
        Texture2D expTex;
        float expPrevK = 1;
        int pvCols, pvRows, pvCw, pvCh, pvPad;

        void BuildExportDialog()
        {
            expDlg = El(Root, "ae-dlg");
            expDlg.style.display = DisplayStyle.None;
            var card = El(expDlg, "ae-dlg-card");
            expTitle = Lbl(card, "", "ae-dlg-t");
            var pr = El(card, "ae-wrap");
            Lbl(pr, "빠른 설정", "ae-k");
            Btn(pr, "게임용으로 채우기", () => { expFormat = 0; expLayout = 1; expPad = 2; expScale = 1; expFrames = true; SaveExportPrefs(); },
                "아래 값을 게임(MFS)에 넣기 좋게 채워요: PNG · 가로 한 줄 · 여백 2px · ×1 · 프레임별 PNG");
            Btn(pr, "보여주기용으로 채우기", () => { expFormat = 0; expLayout = 0; expPad = 0; expScale = 4; expFrames = false; SaveExportPrefs(); },
                "발표 · SNS: PNG · 자동 배치 · 여백 없음 · 도트 ×4");
            expPrev = El(card, "ae-dlg-prev");
            expPrev.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            expGrid = new VisualElement { pickingMode = PickingMode.Ignore };
            expGrid.style.position = Position.Absolute;
            expGrid.style.left = 0; expGrid.style.top = 0; expGrid.style.right = 0; expGrid.style.bottom = 0;
            expGrid.generateVisualContent += DrawExpGrid;
            expPrev.Add(expGrid);

            var r0 = El(card, "ae-wrap");
            Lbl(r0, "저장 방식", "ae-k");
            expFmtB.Add(Btn(r0, "PNG", () => { expFormat = 0; SaveExportPrefs(); }, "레이어를 합친 그림 한 장 + 설명(json) — 게임 · 공유용"));
            expFmtB.Add(Btn(r0, "PSD", () => { expFormat = 1; SaveExportPrefs(); }, "같은 배치로, 레이어를 살린 채 — 칸마다 폴더 하나(그 안에 레이어). 포토샵에서 시트째 고칠 때"));

            var r1 = El(card, "ae-wrap");
            Lbl(r1, "배치", "ae-k");
            string[] layTip = { "8프레임 이하면 가로 한 줄, 넘으면 한 줄 8칸 격자", "모든 프레임을 가로 한 줄로", "모든 프레임을 세로 한 줄로", "열 수를 정해 격자로" };
            for (int i = 0; i < EXP_LAYOUT.Length; i++) { int ii = i; expLayB.Add(Btn(r1, EXP_LAYOUT[i], () => { expLayout = ii; SaveExportPrefs(); }, layTip[i])); }
            expColsRow = El(r1, "ae-row");
            Lbl(expColsRow, "열", "ae-k");
            expColsF = Int(expColsRow, expCols);
            expColsF.tooltip = "격자 한 줄의 칸 수(1 ~ 64)";
            expColsF.RegisterValueChangedCallback(e => { expCols = Mathf.Clamp(e.newValue, 1, 64); SaveExportPrefs(); });

            var r2 = El(card, "ae-wrap");
            Lbl(r2, "여백", "ae-k");
            expPadB.Add(Btn(r2, "0px", () => { expPad = 0; SaveExportPrefs(); }, "칸 사이 간격 없음"));
            expPadB.Add(Btn(r2, "2px", () => { expPad = 2; SaveExportPrefs(); }, "유니티에서 옆 칸 색이 번질 때(게임용 추천)"));
            Lbl(r2, "직접", "ae-k");
            expPadF = Int(r2, expPad);
            expPadF.tooltip = "칸 사이 간격(0 ~ 64px)";
            expPadF.RegisterValueChangedCallback(e => { expPad = Mathf.Clamp(e.newValue, 0, 64); SaveExportPrefs(); });

            var r3 = El(card, "ae-wrap");
            Lbl(r3, "배율", "ae-k");
            foreach (var v in new[] { 1, 2, 4 }) { int vv = v; expScaleB.Add(Btn(r3, "×" + v, () => { expScale = vv; SaveExportPrefs(); }, v == 1 ? "도트 그대로(게임에 쓸 때)" : "도트를 깨짐 없이 확대 — 발표 · SNS 용")); }

            expFramesRow = El(card, "ae-wrap");
            Lbl(expFramesRow, "프레임별 낱장도 같이", "ae-k");
            expFrOn = Btn(expFramesRow, "켬", () => { expFrames = true; SaveExportPrefs(); }, "시트와 함께 프레임마다 한 장씩(이름_01.png …)");
            expFrOff = Btn(expFramesRow, "끔", () => { expFrames = false; SaveExportPrefs(); }, "시트 한 장만");

            expInfo = Lbl(card, "", "ae-dlg-info");
            var r5 = El(card, "ae-wrap");
            Btn(r5, "내보내기", ExportSheet, "위 설정대로 파일 만들기(저장 안 한 것도 화면 그대로)", "ae-btn--main");
            Btn(r5, "내보낸 폴더 열기", () => RevealPath(ExportDir, null), "킷 데이터/sprite-export");
            Btn(r5, "닫기 (Esc)", CloseExport, "내보내기 창 닫기");
        }

        void ToggleExport()
        {
            if (expDlg.style.display == DisplayStyle.Flex) { CloseExport(); return; }
            if (frames.Count == 0 || rows.Count == 0) { Say("내보낼 그림이 없어요."); return; }
            Flush();
            expDlg.style.display = DisplayStyle.Flex;
            expDlg.BringToFront();
            ExpUpdate();
        }

        void CloseExport()
        {
            if (expDlg != null) expDlg.style.display = DisplayStyle.None;
            if (expTex != null) { TexGrave.Kill(expTex); expTex = null; }
        }

        bool ExportOpen => expDlg != null && expDlg.style.display == DisplayStyle.Flex;

        /// <summary>배치 계산(칸 수 · 시트 크기).</summary>
        void ExpLayout(int s, int pad, out int cols, out int rowsN, out long sw, out long sh)
        {
            int n = Math.Max(1, frames.Count);
            cols = expLayout == 1 ? n : expLayout == 2 ? 1 : expLayout == 3 ? Math.Min(expCols, n) : (n <= 8 ? n : 8);
            rowsN = (n + cols - 1) / cols;
            sw = (long)cols * W * s + (cols - 1) * pad;
            sh = (long)rowsN * H * s + (rowsN - 1) * pad;
        }

        /// <summary>시트 픽셀(아래 → 위 줄 = 텍스처 순서). frameCb = 프레임마다 위 → 아래 RGBA(낱장 저장용).</summary>
        byte[] BuildSheet(int s, int pad, int cols, long sw, long sh, Action<int, byte[]> frameCb = null)
        {
            int n = frames.Count, cw = W * s, ch = H * s;
            var order = ExportOrder();
            var frameBuf = new byte[W * H * 4];
            var sheet = new byte[sw * sh * 4];
            for (int f = 0; f < n; f++)
            {
                ComposeExport(f, order, frameBuf);
                int cx = (f % cols) * (cw + pad), cy = (f / cols) * (ch + pad);
                for (int y = 0; y < ch; y++)
                {
                    int sy = y / s;
                    int ty = (int)sh - 1 - (cy + y);
                    long d = ((long)ty * sw + cx) * 4;
                    int srow = sy * W * 4;
                    for (int x = 0; x < cw; x++, d += 4)
                    {
                        int o = srow + (x / s) * 4;
                        sheet[d] = frameBuf[o]; sheet[d + 1] = frameBuf[o + 1]; sheet[d + 2] = frameBuf[o + 2]; sheet[d + 3] = frameBuf[o + 3];
                    }
                }
                frameCb?.Invoke(f, frameBuf);
            }
            return sheet;
        }

        /// <summary>창의 버튼 상태 · 미리보기(배율 ×1 로 만든 시트 — 크기 · 칸 모양은 같음) · 결과 한 줄.</summary>
        void ExpUpdate()
        {
            if (expDlg == null) return;
            for (int i = 0; i < expFmtB.Count; i++) expFmtB[i].EnableInClassList("ae-btn--on", i == expFormat);
            for (int i = 0; i < expLayB.Count; i++) expLayB[i].EnableInClassList("ae-btn--on", i == expLayout);
            expPadB[0].EnableInClassList("ae-btn--on", expPad == 0); expPadB[1].EnableInClassList("ae-btn--on", expPad == 2);
            expPadF.SetValueWithoutNotify(expPad);
            expColsF.SetValueWithoutNotify(expCols);
            expColsRow.style.display = expLayout == 3 ? DisplayStyle.Flex : DisplayStyle.None;
            int[] sv = { 1, 2, 4 };
            for (int i = 0; i < expScaleB.Count; i++) expScaleB[i].EnableInClassList("ae-btn--on", sv[i] == expScale);
            expFrOn.EnableInClassList("ae-btn--on", expFrames); expFrOff.EnableInClassList("ae-btn--on", !expFrames);
            expFramesRow.style.display = expFormat == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (!ExportOpen) return;
            string name = string.IsNullOrEmpty(key) ? "sprite" : key;
            expTitle.text = $"내보내기 — {name}  ({W}×{H} · 프레임 {frames.Count}개)";
            ExpLayout(expScale, expPad, out int cols, out int rowsN, out long sw, out long sh);
            bool big = sw > 16384 || sh > 16384;
            // 미리보기: ×1 시트(여백은 배율에 맞춰 줄여 모양을 같게)
            int ppad = Mathf.Max(0, Mathf.RoundToInt((float)expPad / expScale));
            ExpLayout(1, ppad, out int pc, out int pr, out long pw, out long ph);
            if (expTex != null) { TexGrave.Kill(expTex); expTex = null; }
            if (pw * ph <= 4096L * 4096L)
            {
                var px = BuildSheet(1, ppad, pc, pw, ph);
                expTex = new Texture2D((int)pw, (int)ph, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                expTex.SetPixelData(px, 0);
                expTex.Apply(false, false);
                expPrev.style.backgroundImage = new StyleBackground(expTex);
            }
            else expPrev.style.backgroundImage = StyleKeyword.None;
            expPrevK = Mathf.Min(440f / Mathf.Max(1, pw), 220f / Mathf.Max(1, ph), 8f);
            expPrev.style.width = pw * expPrevK; expPrev.style.height = ph * expPrevK;
            pvCols = pc; pvRows = pr; pvCw = W; pvCh = H; pvPad = ppad;
            expGrid.MarkDirtyRepaint();
            string unique = KitBackup.Unique(ExportDir, name, "_sheet.png", "_sheet.json", "_sheet.psd", "_01.png");
            string what = expFormat == 0
                ? $"{unique}_sheet.png + .json{(expFrames ? $" + 낱장 {frames.Count}장({unique}_01.png …)" : "")}"
                : $"{unique}_sheet.psd(칸마다 폴더 · 레이어 살림)";
            expInfo.text = $"시트 {sw}×{sh}px · {cols}열 × {rowsN}줄 · 칸 {W * expScale}×{H * expScale} · 여백 {expPad}px\n→ 킷 데이터/sprite-export/{what}" +
                (big ? "\n너무 커요(최대 16384) — 격자로 바꾸거나 배율을 낮춰 주세요." : "");
            expInfo.EnableInClassList("ae-dlg-info--bad", big);
        }

        void DrawExpGrid(MeshGenerationContext ctx)
        {
            if (pvCols <= 0 || pvRows <= 0 || !ExportOpen) return;
            var p = ctx.painter2D;
            p.strokeColor = new Color(1f, 0.83f, 0.2f, 0.85f);
            p.lineWidth = 1;
            int n = frames.Count;
            for (int f = 0; f < n; f++)
            {
                float x = (f % pvCols) * (pvCw + pvPad) * expPrevK, y = (f / pvCols) * (pvCh + pvPad) * expPrevK;
                float w = pvCw * expPrevK, h = pvCh * expPrevK;
                p.BeginPath();
                p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w, y)); p.LineTo(new Vector2(x + w, y + h)); p.LineTo(new Vector2(x, y + h)); p.ClosePath();
                p.Stroke();
            }
        }
        /// <summary>내보낼 레이어 순서(아래 → 위). 숨긴 레이어는 Compose 에서 빠짐.</summary>
        List<int> ExportOrder()
        {
            var order = new List<int>();
            for (int r = 0; r < rows.Count; r++) order.Add(r);
            return order;
        }

        /// <summary>프레임 f 를 위 → 아래 줄 순서 RGBA 로(PNG 용).</summary>
        void ComposeExport(int f, List<int> order, byte[] dst)
        {
            var fr = frames[f];
            var L = new TileComp.Layer[order.Count];
            var sc = new TileComp.Scratch();
            int n = TilesX * TilesY;
            for (int i = 0; i < n; i++)
            {
                TileRect(i, out int x0, out int y0, out int tw, out int th);
                for (int k = 0; k < order.Count; k++)
                {
                    int r = order[k];
                    var row = rows[r];
                    var c = r < fr.Cels.Count ? fr.Cels[r] : null;
                    L[k] = new TileComp.Layer
                    {
                        Px = c != null && c.Img != null && i < c.Img.Count ? c.Img.Tiles[i] : null,
                        Visible = row.Visible, Clip = row.Clip, Opacity = (float)row.Opacity, Mode = TileComp.ModeOf(row.Blend),
                    };
                }
                TileComp.Compose(L, tw, th, sc, dst, x0, y0, W, H, false);
            }
        }

        void ExportSheet()
        {
            if (frames.Count == 0 || rows.Count == 0) { Say("내보낼 그림이 없어요."); return; }
            Flush();
            int n = frames.Count, s = expScale, pad = expPad;
            ExpLayout(s, pad, out int cols, out int rowsN, out long sw, out long sh);
            int cw = W * s, ch = H * s;
            const int MAXT = 16384;
            if (sw > MAXT || sh > MAXT) { Say($"시트가 너무 커요({sw}×{sh}, 최대 {MAXT}) — 배치를 격자로 바꾸거나 배율을 낮춰 주세요."); return; }
            string name = string.IsNullOrEmpty(key) ? "sprite" : key;
            try
            {
                Directory.CreateDirectory(ExportDir);
                name = KitBackup.Unique(ExportDir, name, "_sheet.png", "_sheet.json", "_sheet.psd", "_01.png");     // 덮어쓰지 않음(있으면 이름_2 …)
                if (expFormat == 1)
                {
                    ExportPsdSheet(Path.Combine(ExportDir, name + "_sheet.psd"), s, pad, cols, (int)sw, (int)sh);
                    Say($"내보냄 — {name}_sheet.psd ({sw}×{sh}, {cols}열 × {rowsN}줄 · 칸마다 폴더 · 레이어 살림)");
                    RevealPath(ExportDir, Path.Combine(ExportDir, name + "_sheet.psd"));
                    CloseExport();
                    return;
                }
                string nm = name;
                var sheet = BuildSheet(s, pad, cols, sw, sh, expFrames ? (f, buf) => WritePng(Path.Combine(ExportDir, $"{nm}_{f + 1:00}.png"), buf, W, H, s) : (Action<int, byte[]>)null);
                var json = new System.Text.StringBuilder();
                json.Append("{\n");
                json.Append($"  \"name\": \"{Esc(name)}\",\n  \"image\": \"{Esc(name)}_sheet.png\",\n");
                json.Append($"  \"canvasW\": {W}, \"canvasH\": {H}, \"foot\": {foot * s}, \"scale\": {s}, \"cellW\": {cw}, \"cellH\": {ch}, \"padding\": {pad},\n");
                json.Append($"  \"columns\": {cols}, \"rows\": {rowsN}, \"sheetW\": {sw}, \"sheetH\": {sh},\n");
                json.Append($"  \"fps\": {fps}, \"loop\": {(once ? "false" : "true")},\n");
                json.Append("  \"note\": \"x, y = 칸 왼쪽 위(위 → 아래 좌표). durF = 기준 fps 의 프레임 수\",\n");
                json.Append("  \"frames\": [\n");
                for (int f = 0; f < n; f++)
                {
                    int cx = (f % cols) * (cw + pad), cy = (f / cols) * (ch + pad);
                    json.Append($"    {{ \"index\": {f}, \"x\": {cx}, \"y\": {cy}, \"w\": {cw}, \"h\": {ch}, \"durF\": {frames[f].Dur}, \"ms\": {MsOf(f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} }}{(f < n - 1 ? "," : "")}\n");
                }
                json.Append("  ]\n}\n");
                var tex = new Texture2D((int)sw, (int)sh, TextureFormat.RGBA32, false);
                try
                {
                    tex.SetPixelData(sheet, 0);
                    tex.Apply(false, false);
                    File.WriteAllBytes(Path.Combine(ExportDir, name + "_sheet.png"), tex.EncodeToPNG());
                }
                finally { UnityEngine.Object.Destroy(tex); }
                File.WriteAllText(Path.Combine(ExportDir, name + "_sheet.json"), json.ToString(), new System.Text.UTF8Encoding(false));
                Say($"내보냄 — {name}_sheet.png ({sw}×{sh}, {cols}열 × {rowsN}줄){(expFrames ? $" + 프레임 {n}장" : "")} + json");
                RevealPath(ExportDir, Path.Combine(ExportDir, name + "_sheet.png"));
                CloseExport();
            }
            catch (Exception e) { Say("내보내기 실패: " + e.Message); }
        }

        /// <summary>PSD 시트: 같은 배치로 칸마다 폴더(‘01 · 6f’) · 그 안에 레이어(배율만큼 도트 그대로 확대). 숨긴 레이어 · 불투명도 · 섞기 · 클리핑도 그대로.</summary>
        void ExportPsdSheet(string file, int s, int pad, int cols, int sw, int sh)
        {
            var doc = new PsdDoc { W = sw, H = sh };
            var order = ExportOrder();
            int cw = W * s, ch = H * s;
            for (int f = 0; f < frames.Count; f++)
            {
                int cx = (f % cols) * (cw + pad), cy = (f / cols) * (ch + pad);
                var g = new PsdNode { Group = true, Name = $"{f + 1:00} · {frames[f].Dur}f", Parent = doc.Root };
                foreach (int r in order)
                {
                    var row = rows[r];
                    var c = r < frames[f].Cels.Count ? frames[f].Cels[r] : null;
                    int l = 0, t = 0, rr = 0, b = 0;
                    byte[] px = new byte[0];
                    if (c != null && c.Img != null && c.Img.Bounds(out l, out t, out rr, out b))
                    {
                        int w0 = rr - l, h0 = b - t;
                        var src = new byte[w0 * h0 * 4];
                        c.Img.CopyTo(src, l, t, w0, h0);
                        px = new byte[w0 * s * h0 * s * 4];
                        for (int y = 0; y < h0 * s; y++)
                            for (int x = 0; x < w0 * s; x++)
                                Buffer.BlockCopy(src, ((y / s) * w0 + x / s) * 4, px, (y * w0 * s + x) * 4, 4);
                        l = cx + l * s; t = cy + t * s; rr = l + w0 * s; b = t + h0 * s;
                    }
                    else { l = t = rr = b = 0; }
                    g.Kids.Add(new PsdNode { Name = row.Name.Replace('/', '·'), Visible = row.Visible, Clip = row.Clip, Opacity = row.Opacity, Blend = row.Blend, L = l, T = t, R = rr, B = b, Rgba = px, Parent = g });
                }
                doc.Root.Kids.Add(g);
            }
            File.WriteAllBytes(file, Psd.Write(doc));
        }

        static string Esc(string t) => (t ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        /// <summary>위 → 아래 줄 RGBA → PNG(배율만큼 도트 그대로 확대).</summary>
        static void WritePng(string file, byte[] topDown, int w, int h, int s)
        {
            int ow = w * s, oh = h * s;
            var buf = new byte[ow * oh * 4];
            for (int y = 0; y < oh; y++)
            {
                int src = (y / s) * w * 4, dst = (oh - 1 - y) * ow * 4;
                for (int x = 0; x < ow; x++)
                {
                    int o = src + (x / s) * 4, d = dst + x * 4;
                    buf[d] = topDown[o]; buf[d + 1] = topDown[o + 1]; buf[d + 2] = topDown[o + 2]; buf[d + 3] = topDown[o + 3];
                }
            }
            var tex = new Texture2D(ow, oh, TextureFormat.RGBA32, false);
            try { tex.SetPixelData(buf, 0); tex.Apply(false, false); File.WriteAllBytes(file, tex.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(tex); }
        }

        /// <summary>탐색기 · Finder 로 폴더 열기(file 이 있으면 그 파일을 골라 둠).</summary>
        void RevealPath(string dir, string file)
        {
            try { Directory.CreateDirectory(dir); } catch { }
            if (file != null && !File.Exists(file)) file = null;
            try
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                System.Diagnostics.Process.Start("explorer.exe", file != null ? "/select,\"" + file.Replace('/', '\\') + "\"" : "\"" + dir.Replace('/', '\\') + "\"");
                return;
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                System.Diagnostics.Process.Start("open", file != null ? "-R \"" + file + "\"" : "\"" + dir + "\"");
                return;
#endif
            }
            catch { }
            Application.OpenURL("file:///" + dir.Replace('\\', '/'));
        }

        // ───────── 저장 창: 작업 원본(PSD) 미리보기(프레임 재생) · 구성(프레임 = 폴더 · 레이어) · 저장 위치 → 저장 ─────────
        VisualElement svDlg, svPrev;
        Label svTitle, svInfo, svTree, svPath;
        float svMs;
        bool SaveOpen => svDlg != null && svDlg.style.display == DisplayStyle.Flex;

        void BuildSaveDialog()
        {
            svDlg = El(Root, "ae-dlg");
            svDlg.style.display = DisplayStyle.None;
            var card = El(svDlg, "ae-dlg-card");
            svTitle = Lbl(card, "", "ae-dlg-t");
            Lbl(card, "작업 원본(PSD)을 저장해요 — 레이어 · 프레임이 그대로 남고, 미리보기 · 게임 화면이 이 파일을 읽어요. 캔버스 한 장에 프레임마다 폴더 하나가 겹쳐 쌓여요(포토샵 · 클립 스튜디오에서 폴더를 켜고 끄며 넘겨 보기).", "ae-help-l");
            svPrev = El(card, "ae-dlg-prev");
            svPrev.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            svInfo = Lbl(card, "", "ae-dlg-info");
            svTree = Lbl(card, "", "ae-dlg-tree");
            svPath = Lbl(card, "", "ae-help-l");
            var r = El(card, "ae-wrap");
            Btn(r, "저장", () => { Save(); RefreshSaveDlg(); }, "지금 그림을 원본 PSD 로 저장(덮기 전 자동 백업)", "ae-btn--main");
            Button asb = null;
            asb = Btn(r, AutoSave ? "자동 저장 켬" : "자동 저장 끔", () => { AutoSave = !AutoSave; asb.text = AutoSave ? "자동 저장 켬" : "자동 저장 끔"; Say(AutoSave ? "자동 저장 켬 — 손을 떼고 잠깐 뒤 저장해요." : "자동 저장 끔 — Ctrl+S 나 저장 버튼으로 저장해요."); }, "바꾼 뒤 손을 떼면 1초쯤 뒤 자동으로 저장(그리는 중 · 변형 중엔 기다림)");
            Btn(r, "저장 폴더 열기", OpenSaveFolder, "저장된 PSD 가 있는 폴더 열기(지금 파일을 골라 둠) — 포토샵 · 클립 스튜디오에서 열 때");
            Btn(r, "닫기 (Esc)", CloseSaveDlg, "저장 창 닫기");
        }

        void OpenSaveDlg()
        {
            if (key == null) { Say("열린 그림이 없어요."); return; }
            if (readOnly) { Say("시트식 파일은 저장하지 않아요."); return; }
            Flush();
            svMs = 0;
            svDlg.style.display = DisplayStyle.Flex;
            svDlg.BringToFront();
            RefreshSaveDlg();
        }

        void CloseSaveDlg() { if (svDlg != null) svDlg.style.display = DisplayStyle.None; }

        void RefreshSaveDlg()
        {
            if (!SaveOpen) return;
            svTitle.text = $"저장 — {key}{(dirty ? "  · 저장 안 됨" : "  · 저장됨")}";
            float k = Mathf.Min(300f / Mathf.Max(1, W), 220f / Mathf.Max(1, H), 8f);
            svPrev.style.width = W * k; svPrev.style.height = H * k;
            int nLayers = rows.Count;
            svInfo.text = $"캔버스 {W}×{H} · 프레임 {frames.Count}개 = 폴더 {frames.Count}개 · 레이어 {nLayers}개 · {fps}fps{(once ? " · 한 번만 재생" : " · 반복")}";
            // 구성: 폴더(프레임) 마다 그림이 있는 레이어 이름 — 길면 앞쪽만
            var sb = new System.Text.StringBuilder();
            int show = Math.Min(frames.Count, 6);
            for (int f = 0; f < show; f++)
            {
                var names = new List<string>();
                for (int r = 0; r < rows.Count; r++)
                {
                    var c = r < frames[f].Cels.Count ? frames[f].Cels[r] : null;
                    if (c != null && c.Img != null && !c.Img.Empty) names.Add(rows[r].Name);
                }
                sb.Append($"▸ 폴더 {f + 1:00} · {frames[f].Dur}f — {(names.Count == 0 ? "(비어 있음)" : string.Join(", ", names.Take(6)) + (names.Count > 6 ? $" 외 {names.Count - 6}개" : ""))}");
                if (f < show - 1) sb.Append('\n');
            }
            if (frames.Count > show) sb.Append($"\n… 폴더 {frames.Count - show}개 더");
            svTree.text = sb.ToString();
            string file = path != null ? Path.GetFileName(path) : key + ".psd";
            svPath.text = "저장 위치: " + Path.Combine(ArtLib.Dir, file);
        }

        /// <summary>저장 창 미리보기: 프레임을 실제 길이대로 재생.</summary>
        void SaveDlgTick(float dt)
        {
            if (frames.Count == 0 || frameTex.Count == 0) return;
            svMs += dt * 1000f;
            float total = 0;
            for (int i = 0; i < frames.Count; i++) total += MsOf(i);
            float t = total > 0 ? Mathf.Repeat(svMs, total) : 0;
            int idx = 0;
            for (int i = 0; i < frames.Count; i++) { if (t < MsOf(i)) { idx = i; break; } t -= MsOf(i); idx = i; }
            if (idx < frameTex.Count && frameTex[idx] != null) svPrev.style.backgroundImage = new StyleBackground(frameTex[idx]);
        }

        void OpenSaveFolder()
        {
            string dir = ArtLib.Dir;
            try { Directory.CreateDirectory(dir); } catch { }
            string file = path != null && File.Exists(path) ? path : null;
            try
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                System.Diagnostics.Process.Start("explorer.exe", file != null ? "/select,\"" + file.Replace('/', '\\') + "\"" : "\"" + dir.Replace('/', '\\') + "\"");
                Say("저장 폴더를 열었어요" + (file != null ? $" — {Path.GetFileName(file)} 선택됨" : ""));
                return;
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                System.Diagnostics.Process.Start("open", file != null ? "-R \"" + file + "\"" : "\"" + dir + "\"");
                Say("저장 폴더를 열었어요");
                return;
#endif
            }
            catch { }
            Application.OpenURL("file:///" + dir.Replace('\\', '/'));
            Say("저장 폴더: " + dir);
        }

        /// <summary>mode 0 한 장 → 레이어 · 1 레이어째 · 2 프레임째 · 3 새 상태로 열기.</summary>
        void ImportPsd(string path, int mode)
        {
            try
            {
                if (mode == 3)
                {
                    string k = keyField.value.Trim().ToLowerInvariant();
                    string fk = ArtLib.Tokens(Path.GetFileNameWithoutExtension(path)).FirstOrDefault()?.ToLowerInvariant();
                    if (STATES.Contains(fk)) k = fk;
                    if (string.IsNullOrEmpty(k)) { Say("위 ‘파일’ 이름 칸에 상태 이름을 적어 주세요."); return; }
                    if (ArtLib.Files().Any(f => f.key == k)) { Say($"{k} 는 이미 있어요 — 다른 이름을 적거나 지금 그림에 합쳐 주세요."); return; }
                    if (dirty) Save();
                    File.Copy(path, Path.Combine(ArtLib.Dir, k + Path.GetExtension(path).ToLowerInvariant()), false);
                    OpenKey(k);
                    Say($"{Path.GetFileName(path)} → 새 상태 {k}");
                    return;
                }
                var d = Psd.Read(File.ReadAllBytes(path));
                string nm = Path.GetFileNameWithoutExtension(path);
                var top = d.Root.Kids;
                if (mode == 0) { AddAsLayers(new List<(string, byte[])> { (nm, Place(Psd.Compose(top, 0, 0, d.W, d.H), d.W, d.H)) }); Say($"{Path.GetFileName(path)} → 한 장으로 새 레이어"); return; }
                if (mode == 1)
                {
                    var list = new List<(string, byte[])>();
                    foreach (var lf in d.Root.Leaves()) list.Add((lf.Name, Place(Psd.Compose(new[] { lf }, 0, 0, d.W, d.H), d.W, d.H)));
                    AddAsLayers(list);
                    Say($"{Path.GetFileName(path)} → 레이어 {list.Count}장");
                    return;
                }
                var groups = top.Where(n => n.Group).ToList();
                var pics = new List<byte[]>();
                if (groups.Count > 0)
                {
                    int Num(PsdNode g) { var t = g.Name.Split(new[] { ' ', '·' }, StringSplitOptions.RemoveEmptyEntries); return t.Length > 0 && int.TryParse(t[0], out var n) ? n : int.MinValue; }
                    if (groups.All(g => Num(g) != int.MinValue)) groups = groups.OrderBy(Num).ToList();
                    foreach (var g in groups) pics.Add(Place(Psd.Compose(top.Where(n => !n.Group || n == g).ToList(), 0, 0, d.W, d.H, true), d.W, d.H));
                }
                else foreach (var lf in top.Where(n => !n.Group)) pics.Add(Place(Psd.Compose(new[] { lf }, 0, 0, d.W, d.H, true), d.W, d.H));
                AddAsFrames(nm, pics);
                Say($"{Path.GetFileName(path)} → 새 프레임 {pics.Count}개(레이어 ‘{nm}’)");
            }
            catch (Exception e) { Say("가져오기 실패: " + e.Message); }
        }

        // ───────── 매 프레임 · 키 ─────────
        public void Tick(float dt)
        {
            if (SaveOpen) SaveDlgTick(dt);
            if (!Open) return;
            Flush();                 // 이번 화면에 그린 조각들을 한 번에
            TickAutoSave();          // 손을 떼고 잠깐 뒤 자동 저장
            TickArrows(dt);          // 방향키 연속 이동
            PollKeys();              // 화면 키 이벤트가 빠졌을 때(한글 입력기 등) 단축키 보조
            // 스페이스를 뗀 소식을 놓쳐 ‘손’ 상태로 굳으면 클릭이 전부 화면 이동이 됨 → 실제 키 상태로 바로잡음
            if (spaceHeld && Keys.CanPoll && !Keys.Held(Keys.K.Space)) spaceHeld = false;
            // 버튼을 눌러 타임라인을 다시 만들면 포커스가 사라짐 → 편집기로 되돌림(미리보기 조작 · 글 입력 중이 아닐 때)
            if (!KeysOff && Root.panel != null && Root.resolvedStyle.display != DisplayStyle.None)
            {
                var fe = Root.panel.focusController?.focusedElement as VisualElement;
                if (fe == null || fe.panel == null) Root.Focus();
            }
            TickXf();                // 옮기기(한 화면에 한 번)
            TickOnion();             // 어니언 윤곽선 · 실루엣
            CatchUp();               // 미뤄 둔 프레임
            if (play && frameTex.Count > 0)
            {
                playMs += dt * 1000f;
                float total = 0; for (int i = 0; i < frames.Count; i++) total += MsOf(i);
                float t = loop ? Mathf.Repeat(playMs, Mathf.Max(1, total)) : Mathf.Min(playMs, total - 0.001f);
                int f = 0;
                for (; f < frames.Count - 1; f++) { if (t < MsOf(f)) break; t -= MsOf(f); }
                if (f != fi) { fi = f; if (fi < stale.Count && stale[fi]) ComposeFrame(fi); Tex(img, frameTex[fi], 1, Color.white); RefreshTime(); RebuildTimeline(); }
                if (!loop && playMs >= total) { play = false; Refresh(); }
            }
            StageField.ForceHero = Pin && key != null && key.StartsWith("hero-") ? key : null;
            StageField.ForceFoe = Pin && key != null && key.StartsWith("foe-") ? key : null;
        }

        // ───────── 화면 전체 키 듣기 · 방향키 연속 이동 ─────────
        /// <summary>작업실 미리보기를 조작하는 중이면 true — 그동안 편집기는 키를 하나도 받지 않음(스페이스 = 대쉬가 편집기 재생이 되지 않게).</summary>
        public bool KeysOff
        {
            get => keysOff;
            set
            {
                if (keysOff == value) return;
                keysOff = value;
                spaceHeld = false; spaceUsed = true; heldArrows.Clear(); arrowPollOk.Clear();     // 누르던 키 상태도 버림
                if (onionHoldKey != KeyCode.None) EndOnionHold();
            }
        }
        bool keysOff;
        IPanel hookedPanel;
        readonly HashSet<KeyCode> heldArrows = new HashSet<KeyCode>();
        bool shiftDown;
        float arrowRep;
        readonly Dictionary<KeyCode, float> arrowSeen = new Dictionary<KeyCode, float>();     // 마지막으로 그 방향키 이벤트(반복 포함)가 온 때
        readonly HashSet<KeyCode> arrowPollOk = new HashSet<KeyCode>();                       // 직접 읽기로 눌림이 확인된 키
        readonly List<KeyCode> arrowGone = new List<KeyCode>();

        void HookPanel(IPanel p)
        {
            if (p == null || hookedPanel == p) return;
            UnhookPanel();
            hookedPanel = p;
            p.visualTree.RegisterCallback<KeyDownEvent>(PanelKeyDown, TrickleDown.TrickleDown);
            p.visualTree.RegisterCallback<KeyUpEvent>(PanelKeyUp, TrickleDown.TrickleDown);
            p.visualTree.RegisterCallback<NavigationMoveEvent>(PanelNav, TrickleDown.TrickleDown);
        }

        /// <summary>변형 중 방향키가 UI 포커스 이동(다른 버튼으로 넘어감)이 되지 않게 막음 — 포커스가 편집기를 떠나 방향키가 끊기던 원인.</summary>
        void PanelNav(NavigationMoveEvent e)
        {
            if (!Open || KeysOff || xf == null || TextFocus) return;
            e.StopPropagation();
            hookedPanel?.focusController?.IgnoreEvent(e);
        }

        void UnhookPanel()
        {
            if (hookedPanel == null) return;
            hookedPanel.visualTree.UnregisterCallback<KeyDownEvent>(PanelKeyDown, TrickleDown.TrickleDown);
            hookedPanel.visualTree.UnregisterCallback<KeyUpEvent>(PanelKeyUp, TrickleDown.TrickleDown);
            hookedPanel.visualTree.UnregisterCallback<NavigationMoveEvent>(PanelNav, TrickleDown.TrickleDown);
            hookedPanel = null;
        }

        /// <summary>편집기 안에서 일어난 키는 편집기 자체 처리기가 받음 — 여기선 밖(또는 포커스 없음)만.</summary>
        bool OutsideEditor(EventBase e)
        {
            if (!(e.target is VisualElement v)) return true;
            if (v == Root || Root.Contains(v)) return false;
            if (IsTypingEl(v)) return false;     // 다른 곳의 글 입력 칸(숫자 칸 포함)
            return true;
        }

        void PanelKeyDown(KeyDownEvent e) { if (Open && !KeysOff && OutsideEditor(e)) OnKey(e); }
        void PanelKeyUp(KeyUpEvent e) { if (Open && !KeysOff && OutsideEditor(e)) OnKeyUp(e); }

        static readonly (KeyCode, Keys.K)[] ARROWS = { (KeyCode.LeftArrow, Keys.K.Left), (KeyCode.RightArrow, Keys.K.Right), (KeyCode.UpArrow, Keys.K.Up), (KeyCode.DownArrow, Keys.K.Down) };
        static bool IsArrow(KeyCode k) => k == KeyCode.LeftArrow || k == KeyCode.RightArrow || k == KeyCode.UpArrow || k == KeyCode.DownArrow;

        /// <summary>눌려 있는 방향키를 합쳐 한 걸음(오른쪽+위 = 대각선, 반대끼리는 상쇄).</summary>
        void StepArrows()
        {
            int dx = (heldArrows.Contains(KeyCode.RightArrow) ? 1 : 0) - (heldArrows.Contains(KeyCode.LeftArrow) ? 1 : 0);
            int dy = (heldArrows.Contains(KeyCode.DownArrow) ? 1 : 0) - (heldArrows.Contains(KeyCode.UpArrow) ? 1 : 0);
            int s = shiftDown ? 10 : 1;
            if (dx != 0 || dy != 0) Nudge(new Vector2Int(dx * s, dy * s));
        }

        /// <summary>
        /// 방향키 연속 이동은 운영체제 키 반복을 쓰지 않고 직접 — Shift · 다른 방향키를 눌렀다 떼도 멈추지 않음.
        /// 처음 누르면 한 걸음, 0.28초 뒤부터 초당 30걸음.
        /// </summary>
        void TickArrows(float dt)
        {
            if (xf == null || KeysOff || TextFocus) { heldArrows.Clear(); arrowPollOk.Clear(); return; }
            // 키보드를 직접 읽을 수 있으면 방향키는 전부 직접 읽기로(화면 키 이벤트 · 키 반복 · 포커스 이동에 흔들리지 않음)
            if (Keys.CanPoll)
            {
                bool fresh = false;
                foreach (var (kc, kk) in ARROWS)
                {
                    bool h = Keys.Held(kk);
                    if (h && heldArrows.Add(kc)) fresh = true;
                    else if (!h) heldArrows.Remove(kc);
                }
                shiftDown = Keys.Held(Keys.K.Shift);
                if (heldArrows.Count == 0) return;
                if (fresh) { StepArrows(); arrowRep = 0.28f; return; }      // 처음 누름 = 한 걸음, 0.28초 뒤부터 연속
                arrowRep -= dt;
                int g = 0;
                while (arrowRep <= 0 && g++ < 10) { StepArrows(); arrowRep += 1f / 30f; }
                return;
            }
            if (heldArrows.Count == 0) return;
            // 떼었는지 판단 — 키보드 직접 읽기는 ‘한 번이라도 눌림을 읽은 키’ 만 믿는다.
            // (MFS 처럼 Input System 이 키 상태를 늦게 · 못 읽는 프로젝트에선 눌렀는데도 ‘안 눌림’ 으로 읽혀 한 칸만 가고 멈췄다)
            // 직접 읽기를 못 믿으면: 키 반복 이벤트가 0.7초 넘게 안 오면 뗀 것으로 봄(떼는 이벤트는 OnKeyUp 이 바로 처리).
            float now = Time.unscaledTime;
            arrowGone.Clear();
            foreach (var k in heldArrows)
            {
                bool polled = Keys.CanPoll && Keys.Held(k == KeyCode.LeftArrow ? Keys.K.Left : k == KeyCode.RightArrow ? Keys.K.Right : k == KeyCode.UpArrow ? Keys.K.Up : Keys.K.Down);
                if (polled) { arrowPollOk.Add(k); continue; }
                float seen = arrowSeen.TryGetValue(k, out var t) ? t : now;
                if (arrowPollOk.Contains(k) ? now - seen > 0.1f : now - seen > 0.7f) arrowGone.Add(k);
            }
            foreach (var k in arrowGone) { heldArrows.Remove(k); arrowPollOk.Remove(k); }
            if (Keys.CanPoll && arrowPollOk.Count > 0) shiftDown = Keys.Held(Keys.K.Shift);
            arrowRep -= dt;
            int guard = 0;
            while (arrowRep <= 0 && heldArrows.Count > 0 && guard++ < 10) { StepArrows(); arrowRep += 1f / 30f; }
        }

        void OnKey(KeyDownEvent e)
        {
            if (!Open || KeysOff) return;
            if (e.keyCode == KeyCode.LeftShift || e.keyCode == KeyCode.RightShift) shiftDown = true;
            else shiftDown = e.shiftKey;
            if (capId != null) { Capture(e); e.StopPropagation(); return; }     // 단축키 바꾸는 중: 다음 키를 그 동작에
            if (TextFocus || IsTypingEl(e.target as VisualElement)) return;     // 글 입력 중: 단축키는 하나도 안 받음(글자가 칸으로 감)
            if (xf != null && !e.altKey)
            {
                // Ctrl · Shift 를 같이 눌러도 옮김(Shift = 10px). 키 반복(같은 키가 또 옴)은 무시하고 TickArrows 가 굴림
                if (IsArrow(e.keyCode) && Keys.CanPoll) { e.StopPropagation(); return; }      // 직접 읽기(TickArrows)가 처리
                if (IsArrow(e.keyCode)) { arrowSeen[e.keyCode] = Time.unscaledTime; if (heldArrows.Add(e.keyCode)) { arrowPollOk.Remove(e.keyCode); StepArrows(); arrowRep = 0.28f; } e.StopPropagation(); return; }
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { CommitTransform(); e.StopPropagation(); return; }
            }
            if (poly != null)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { ClosePoly(); e.StopPropagation(); return; }
                if (e.keyCode == KeyCode.Backspace) { poly.RemoveAt(poly.Count - 1); if (poly.Count == 0) poly = null; overlay.MarkDirtyRepaint(); e.StopPropagation(); return; }
            }
            var combo = Combo(e.keyCode, e.ctrlKey || e.commandKey, e.shiftKey, e.altKey);
            if (combo == null || !lookup.TryGetValue(combo, out var id)) return;
            if (id == "play") { if (!spaceHeld) { spaceHeld = true; spaceUsed = false; playKey = e.keyCode; } e.StopPropagation(); return; }
            // 같은 누름을 키보드 직접 읽기(PollKeys)가 방금 처리했으면 건너뜀(두 번 실행 방지)
            float now = Time.unscaledTime;
            if (polledAt.TryGetValue(e.keyCode, out float pt) && now - pt < 0.15f) { e.StopPropagation(); return; }
            eventAt[e.keyCode] = now;
            Dispatch(id, e.keyCode);
            e.StopPropagation();
        }

        void Dispatch(string id, KeyCode code)
        {
            if (id == "onionViewPrev" || id == "onionViewNext" || id == "onionViewAll")
            {
                OnionViewKey(id == "onionViewPrev" ? 1 : id == "onionViewNext" ? 2 : 0, code);
                return;
            }
            Do(id);
        }

        // ───────── 키보드 직접 읽기(보조) ─────────
        // 한글 입력기가 켜져 있거나 포커스가 잠깐 비면 화면 키 이벤트가 빠지거나 키 이름 없이 와서(ㄴ · ㅇ 처럼) 단축키가 안 먹음.
        // 그래서 실제 키보드에서 이번 프레임에 눌린 키를 한 번 더 확인 — 화면 이벤트와 같은 누름은 0.15초 안에 한 번만 실행.
        readonly Dictionary<KeyCode, float> eventAt = new Dictionary<KeyCode, float>(), polledAt = new Dictionary<KeyCode, float>();
        readonly List<(string combo, string id, KeyCode code)> lookupList = new List<(string, string, KeyCode)>();

        static KeyCode CodeOf(string combo)
        {
            if (string.IsNullOrEmpty(combo)) return KeyCode.None;
            int i = combo.LastIndexOf('+');
            string k = i >= 0 && i < combo.Length - 1 ? combo.Substring(i + 1) : combo;
            return Enum.TryParse(k, out KeyCode c) ? c : KeyCode.None;
        }

        /// <summary>입력기와 상관없는 키만(방향키 · 스페이스 · Enter · Esc · 지우기는 화면 이벤트가 늘 옴, 특별 처리도 있음).</summary>
        static bool Pollable(KeyCode c) => c != KeyCode.None && !IsArrow(c) && c != KeyCode.Space && c != KeyCode.Return && c != KeyCode.KeypadEnter
            && c != KeyCode.Escape && c != KeyCode.Backspace && c != KeyCode.Delete && c != KeyCode.Tab;

        void PollKeys()
        {
            if (!Keys.CanPoll) return;
            // 누르고 보기(어니언) — 떼는 소식이 빠져도 풀리게
            if (onionHoldKey != KeyCode.None && !Keys.CodeHeld(onionHoldKey)) EndOnionHold();
            if (KeysOff || capId != null || TextFocus || renameTf != null) return;
            if (Root.panel == null || Root.resolvedStyle.display == DisplayStyle.None) return;
            bool ctrl = Keys.Held(Keys.K.Ctrl), shift = Keys.Held(Keys.K.Shift), alt = Keys.AltHeld();
            float now = Time.unscaledTime;
            (string id, KeyCode code) hit = (null, KeyCode.None);
            foreach (var (combo, id, code) in lookupList)
            {
                if (!Keys.CodeDown(code)) continue;
                if (Combo(code, ctrl, shift, alt) != combo) continue;
                if (eventAt.TryGetValue(code, out float et) && now - et < 0.15f) continue;     // 화면 이벤트가 이미 처리
                hit = (id, code);
                break;
            }
            if (hit.id == null) return;
            polledAt[hit.code] = now;
            Dispatch(hit.id, hit.code);
        }

        /// <summary>단축키 동작 실행.</summary>
        void Do(string id)
        {
            switch (id)
            {
                case "save": Save(); break;
                case "undo": Undo(); break;
                case "redo": Redo(); break;
                case "zoomIn": Zoom(1, null); break;
                case "zoomOut": Zoom(-1, null); break;
                case "fit": FitZoom(); Layout(); break;
                case "pen": SetTool(Tool.Pen); break;
                case "eraser": SetTool(Tool.Eraser); break;
                case "picker": SetTool(Tool.Picker); break;
                case "fill": SetTool(Tool.Fill); break;
                case "line": SetTool(Tool.Line); break;
                case "rect": SetTool(Tool.Rect); break;
                case "hand": SetTool(Tool.Hand); break;
                case "selRect": SetTool(Tool.SelRect); break;
                case "lasso": SetTool(Tool.Lasso); break;
                case "lassoFill": SetTool(Tool.LassoFill); break;
                case "lassoErase": SetTool(Tool.LassoErase); break;
                case "blend": SetTool(Tool.Blend); break;
                case "polyLasso": SetTool(Tool.Poly); break;
                case "selAll": SelectAll(); break;
                case "deselect": Deselect(); break;
                case "selInvert": InvertSel(); break;
                case "delSel": DeleteSel(); break;
                case "transform": BeginTransform(); break;
                case "copy": Copy(false); break;
                case "cut": Copy(true); break;
                case "paste": Paste(false); break;
                case "pasteNew": Paste(true); break;
                case "copyLayers": CopyLayers(); break;
                case "pasteLayers": PasteLayers(); break;
                case "alignLayer": AlignLayer(); break;
                case "saveFolder": OpenSaveFolder(); break;
                case "exportSheet": ExportSheet(); break;
                case "exportPanel": ToggleExport(); break;
                case "toolNext": StepTool(1); break;
                case "toolPrev": StepTool(-1); break;
                case "mirror": mirror = !mirror; Refresh(); break;
                case "grid": ToggleGrid(); break;
                case "onion": onion = !onion; OnionChanged(); break;
                case "onionStyle": NextOnionStyle(); break;
                case "onionPos": onionOver = !onionOver; OnionChanged(); break;
                case "onionHoldMode": ToggleOnionHold(); break;
                case "brushDown": brush = Math.Max(1, brush - 1); Refresh(); break;
                case "brushUp": brush = Math.Min(16, brush + 1); Refresh(); break;
                case "black": colors.SetMain(new Color32(0, 0, 0, 255)); Say("색 = 검정"); break;
                case "white": colors.SetMain(new Color32(255, 255, 255, 255)); Say("색 = 흰색"); break;
                case "swapColor": colors.Swap(); break;
                case "clearColor": colors.UseClear = !colors.UseClear; colors.SetMain(colors.Main, true); break;
                case "prevFrame": Go(fi > 0 ? fi - 1 : frames.Count - 1); break;
                case "nextFrame": Go(fi < frames.Count - 1 ? fi + 1 : 0); break;
                case "firstFrame": Go(0); break;
                case "lastFrame": Go(frames.Count - 1); break;
                case "rowUp": ri = Math.Min(rows.Count - 1, ri + 1); Refresh(); break;
                case "rowDown": ri = Math.Max(0, ri - 1); Refresh(); break;
                case "addFrame": AddFrame(fi, false); break;
                case "dupFrame": DupFrames(); break;
                case "delFrame": DelFrame(); break;
                case "addLayer": AddRow(); break;
                case "celHold": CelHold(); break;
                case "celClear": CelClear(); break;
                case "durDown": SetDur(d => d - 1); break;
                case "durUp": SetDur(d => d + 1); break;
                case "boxes": StageField.ShowBoxes = !StageField.ShowBoxes; break;
                case "keysPanel": ToggleKeys(); break;
                case "importRefresh": BuildImports(); break;
                case "onionMore": onionPrevField.value = Mathf.Clamp(onionPrev + 1, 0, 3); onionField.value = Mathf.Clamp(onionNext + 1, 0, 3); if (!onion) { onion = true; OnionChanged(); } break;
                case "onionLess": onionPrevField.value = Mathf.Clamp(onionPrev - 1, 0, 3); onionField.value = Mathf.Clamp(onionNext - 1, 0, 3); break;
                case "loop": loop = !loop; once = !loop; dirty = true; PushLive(); Refresh(); break;
                case "addFrameEnd": AddFrame(frames.Count - 1, false); break;
                case "frameLeft": MoveFrame(-1); break;
                case "frameRight": MoveFrame(1); break;
                case "selAllFrames": SelAllFrames(); break;
                case "selAllRows": SelAllRows(); break;
                case "tlSelClear": ClearTlSel(); break;
                case "celNew": CelNew(); break;
                case "celUnlink": CelUnlink(); break;
                case "shareAll": CelShareAll(); break;
                case "delLayer": DelRow(); break;
                case "layerUp": MoveRow(1); break;
                case "layerDown": MoveRow(-1); break;
                case "layerVis": if (R != null) { bool v = !R.Visible; RowProp(r => r.Visible = v); } break;
                case "layerLock": if (R != null) { bool v = !R.Locked; RowProp(r => r.Locked = v); } break;
                case "layerClip": if (R != null) { bool v = !R.Clip; RowProp(r => r.Clip = v); } break;
                case "renameLayer": if (rows.Count > 0 && !readOnly) { renameReq = true; RebuildTimeline(); } break;
                case "splitLayers": SplitLayersToFrames(); break;
                case "cycleAlign": CycleAlign(); Say("정렬 방식: " + ALIGN[(int)alignMode]); break;
                case "pin": Pin = !Pin; Refresh(); break;
                case "freeze": Freeze = !Freeze; Refresh(); break;
                case "help": helpBox.style.display = helpBox.resolvedStyle.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None; break;
            }
        }

        // ───────── 단축키 설정(작업실 · 편집기 공통) ─────────
        /// <summary>(id, 이름, 기본 키 1, 기본 키 2). 키 = ‘Ctrl+Shift+Alt+KeyCode’ 이름.</summary>
        static readonly (string id, string name, string k1, string k2)[] ACTIONS =
        {
            ("#", "파일 · 창", null, null),
            ("save", "저장", "Ctrl+S", null), ("saveFolder", "저장 폴더 열기", null, null), ("exportSheet", "시트 내보내기(PNG + json)", null, null), ("exportPanel", "내보내기 칸 열기 · 닫기", null, null), ("keysPanel", "단축키 창 열기 · 닫기", null, null),
            ("help", "도움말", null, null), ("importRefresh", "가져오기 목록 새로 고침", null, null),
            ("undo", "되돌리기", "Ctrl+Z", null), ("redo", "다시", "Ctrl+Y", "Ctrl+Shift+Z"),
            ("#", "그리기 도구", null, null),
            ("pen", "펜", "B", null), ("eraser", "지우개", "E", null), ("picker", "스포이드", "I", null), ("fill", "채우기", "G", null),
            ("line", "직선", "L", null), ("rect", "사각", "U", null), ("hand", "손", "H", null),
            ("lassoFill", "올가미 칠하기", "Shift+A", null), ("lassoErase", "올가미 지우기", "Shift+E", null), ("blend", "톤 잇기", "Shift+B", null),
            ("toolNext", "아래 도구로", "S", null), ("toolPrev", "위 도구로", "D", null),
            ("brushDown", "굵기 −", "Z", "LeftBracket"), ("brushUp", "굵기 +", "X", "RightBracket"), ("mirror", "좌우 대칭", "M", null),
            ("#", "선택 · 옮기기", null, null),
            ("selRect", "사각 선택", "R", null), ("lasso", "올가미", "A", null), ("polyLasso", "꺾은선 선택", "Q", null),
            ("selAll", "전체 선택", "Ctrl+A", null), ("deselect", "선택 해제", "Ctrl+D", null), ("selInvert", "선택 반전", "Ctrl+Shift+I", null),
            ("transform", "자유 변형(옮기기 · 크기 — 고른 칸 전부)", "Ctrl+T", null), ("delSel", "선택 영역 지우기", "Delete", null),
            ("copy", "복사(고른 영역 · 없으면 칸 전체)", "Ctrl+C", null), ("cut", "잘라내기", "Ctrl+X", null),
            ("paste", "붙여넣기(지금 레이어)", "Ctrl+V", null), ("pasteNew", "새 레이어로 붙여넣기", "Ctrl+Shift+V", null),
            ("copyLayers", "레이어 통째 복사(고른 레이어들 · 모든 프레임)", "Ctrl+Alt+C", null), ("pasteLayers", "레이어 통째 끼워 넣기(다른 동작에도)", "Ctrl+Alt+V", null),
            ("#", "색", null, null),
            ("black", "색 = 검정", "F", null), ("white", "색 = 흰색", null, null), ("swapColor", "메인 · 서브 바꾸기", null, null), ("clearColor", "투명색 켜기 · 끄기", null, null),
            ("#", "보기", null, null),
            ("zoomIn", "확대", "Ctrl+Equals", "Ctrl+KeypadPlus"), ("zoomOut", "축소", "Ctrl+Minus", "Ctrl+KeypadMinus"), ("fit", "화면에 맞추기", null, null),
            ("grid", "격자", null, null), ("onion", "어니언 켜기 · 끄기", "O", null), ("onionMore", "어니언 장 수 +(이전 · 다음)", null, null), ("onionLess", "어니언 장 수 −(이전 · 다음)", null, null),
            ("onionStyle", "어니언 보기 방식(윤곽선 · 명암 경계선 · 실루엣 · 단색 명암 · 예전 합성)", null, null), ("onionPos", "어니언 위에 · 아래에", null, null),
            ("onionViewPrev", "어니언 이전(빨강)만 보기", "Alpha3", null), ("onionViewNext", "어니언 다음(파랑)만 보기", "Alpha4", null),
            ("onionViewAll", "어니언 전부 보기", "Alpha5", null), ("onionHoldMode", "어니언 보기 키: 누르고 보기 · 계속 보기", "Alpha6", null),
            ("boxes", "판정 보기", null, null),
            ("#", "재생 · 프레임", null, null),
            ("play", "재생 · 정지(누른 채 끌면 손)", "Space", null), ("loop", "반복 · 한 번만", null, null),
            ("prevFrame", "이전 프레임", "LeftArrow", "Comma"), ("nextFrame", "다음 프레임", "RightArrow", "Period"), ("firstFrame", "첫 프레임", null, null), ("lastFrame", "마지막 프레임", null, null),
            ("durDown", "길이 −1f(고른 프레임)", null, null), ("durUp", "길이 +1f(고른 프레임)", null, null),
            ("addFrameEnd", "끝에 빈 프레임", null, null), ("addFrame", "지금 뒤에 빈 프레임 삽입", null, null), ("dupFrame", "프레임 복제(고른 것)", null, null), ("delFrame", "프레임 삭제(고른 것)", null, null),
            ("frameLeft", "프레임 앞으로 옮김", null, null), ("frameRight", "프레임 뒤로 옮김", null, null),
            ("selAllFrames", "프레임 모두 고르기", null, null), ("tlSelClear", "고른 프레임 · 레이어 풀기", null, null),
            ("#", "칸", null, null),
            ("celNew", "칸 = 새 그림", null, null), ("celHold", "앞 그림 유지", null, null), ("celUnlink", "연결 끊기", null, null), ("celClear", "칸 비우기", null, null),
            ("shareAll", "모든 프레임 같은 그림", null, null),
            ("#", "레이어", null, null),
            ("rowUp", "위 레이어 고르기", "UpArrow", null), ("rowDown", "아래 레이어 고르기", "DownArrow", null),
            ("addLayer", "레이어 추가", null, null), ("delLayer", "레이어 삭제(고른 것)", null, null),
            ("layerUp", "레이어 위로 옮김", null, null), ("layerDown", "레이어 아래로 옮김", null, null), ("selAllRows", "레이어 모두 고르기", null, null),
            ("layerVis", "보임 · 숨김(고른 것)", null, null), ("layerLock", "잠금(고른 것)", null, null), ("layerClip", "클리핑(고른 것)", null, null),
            ("renameLayer", "레이어 이름 바꾸기", null, null), ("splitLayers", "레이어 → 프레임 나누기", null, null),
            ("alignLayer", "레이어 자동 정렬", null, null), ("cycleAlign", "정렬 방식 바꾸기(원본 · 발밑 · 몸통)", null, null),
            ("#", "결투 · 미리보기", null, null),
            ("pin", "캐릭터가 이 동작 고정", null, null), ("freeze", "결투 멈춤", null, null),
        };
        static readonly string[] MODS = { "Alt", "Ctrl", "Shift" };

        readonly Dictionary<string, string[]> keys = new Dictionary<string, string[]>();
        readonly Dictionary<string, string> lookup = new Dictionary<string, string>();
        string pickMod = "Alt", selMod = "Ctrl";
        string capId; int capSlot;
        KeyCode playKey = KeyCode.Space;
        VisualElement keyPanel, keyList, presetList;
        TextField presetName;

        static string KeyDir => Path.Combine(KitPaths.Data, "editor-keys");
        static string KeyFile => Path.Combine(KeyDir, "current.txt");

        static string Combo(KeyCode k, bool ctrl, bool shift, bool alt)
        {
            if (k == KeyCode.None || k == KeyCode.LeftControl || k == KeyCode.RightControl || k == KeyCode.LeftShift || k == KeyCode.RightShift
                || k == KeyCode.LeftAlt || k == KeyCode.RightAlt || k == KeyCode.LeftCommand || k == KeyCode.RightCommand || k == KeyCode.AltGr) return null;
            return (ctrl ? "Ctrl+" : "") + (shift ? "Shift+" : "") + (alt ? "Alt+" : "") + k;
        }

        /// <summary>보기 좋은 이름(Comma → , 등).</summary>
        static string Pretty(string combo)
        {
            if (string.IsNullOrEmpty(combo)) return "—";
            return combo.Replace("Alpha", "").Replace("Keypad", "숫자판 ").Replace("LeftBracket", "[").Replace("RightBracket", "]").Replace("Comma", ",").Replace("Period", ".")
                .Replace("Equals", "=").Replace("Minus", "−").Replace("UpArrow", "↑").Replace("DownArrow", "↓").Replace("LeftArrow", "←").Replace("RightArrow", "→").Replace("Space", "Space");
        }

        void DefaultKeys()
        {
            keys.Clear();
            foreach (var a in ACTIONS) if (a.id != "#") keys[a.id] = new[] { a.k1, a.k2 };
            pickMod = "Alt"; selMod = "Ctrl";
        }

        void RebuildLookup()
        {
            lookup.Clear();
            foreach (var a in ACTIONS)
                if (keys.TryGetValue(a.id, out var ks))
                    foreach (var k in ks) if (!string.IsNullOrEmpty(k) && !lookup.ContainsKey(k)) lookup[k] = a.id;
            lookupList.Clear();
            foreach (var kv in lookup) { var c = CodeOf(kv.Key); if (Pollable(c)) lookupList.Add((kv.Key, kv.Value, c)); }
            // 도구 버튼 설명에 지금 키
            foreach (var (t, id) in new[] { (Tool.Pen, "pen"), (Tool.Eraser, "eraser"), (Tool.Picker, "picker"), (Tool.Fill, "fill"), (Tool.Line, "line"), (Tool.Rect, "rect"), (Tool.SelRect, "selRect"), (Tool.Lasso, "lasso"), (Tool.Poly, "polyLasso"), (Tool.Hand, "hand"), (Tool.LassoFill, "lassoFill"), (Tool.LassoErase, "lassoErase"), (Tool.Blend, "blend") })
                if (toolBtns.TryGetValue(t, out var b)) b.tooltip = $"{b.text} ({Pretty(keys[id][0])}) · 도구 위아래 {Pretty(keys["toolPrev"][0])}/{Pretty(keys["toolNext"][0])}";
        }

        static string Serialize(Dictionary<string, string[]> ks, string pick, string sel)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("@ver=").Append(KEYS_VER).Append('\n');
            sb.Append("@pick=").Append(pick).Append('\n').Append("@select=").Append(sel).Append('\n');
            foreach (var kv in ks) sb.Append(kv.Key).Append('=').Append(kv.Value[0] ?? "").Append('|').Append(kv.Value[1] ?? "").Append('\n');
            return sb.ToString();
        }

        // 단축키 파일 판. 새 기본 키를 예전 저장 파일에도 넣어 줄 때 올림(그 키가 다른 동작에 안 쓰였을 때만, 빈 칸에)
        const int KEYS_VER = 7;
        static readonly (int ver, string id, string key)[] KEY_MIGRATE =
        {
            (2, "prevFrame", "LeftArrow"), (2, "nextFrame", "RightArrow"),
            (3, "onion", "O"),
            (6, "lassoFill", "Shift+A"), (6, "lassoErase", "Shift+E"),
            (7, "blend", "Shift+B"),
            (5, "onionViewPrev", "Alpha3"), (5, "onionViewNext", "Alpha4"), (5, "onionViewAll", "Alpha5"), (5, "onionHoldMode", "Alpha6"),
        };

        void Deserialize(string text)
        {
            DefaultKeys();
            int ver = 1;
            var inFile = new HashSet<string>();
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                if (k == "@ver") { int.TryParse(v, out ver); continue; }
                if (k == "@pick") { if (MODS.Contains(v)) pickMod = v; continue; }
                if (k == "@select") { if (MODS.Contains(v)) selMod = v; continue; }
                if (!keys.ContainsKey(k)) continue;
                inFile.Add(k);
                var parts = v.Split('|');
                keys[k] = new[] { parts.Length > 0 && parts[0].Length > 0 ? parts[0] : null, parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null };
            }
            // 파일에 없던 동작(새로 생긴 것)의 기본 키가 파일 속 동작의 키와 겹치면 비움 — 원래 쓰던 키를 빼앗지 않게
            var used = new HashSet<string>();
            foreach (var id in inFile) foreach (var kk in keys[id]) if (!string.IsNullOrEmpty(kk)) used.Add(kk);
            foreach (var id in keys.Keys.ToList())
            {
                if (inFile.Contains(id)) continue;
                var cur = keys[id];
                for (int j = 0; j < 2; j++) if (cur[j] != null && used.Contains(cur[j])) cur[j] = null;
            }
            foreach (var (mv, id, key) in KEY_MIGRATE)
            {
                if (ver >= mv || !keys.TryGetValue(id, out var cur)) continue;
                if (cur[0] == key || cur[1] == key || keys.Any(kv => kv.Value[0] == key || kv.Value[1] == key)) continue;
                if (cur[0] == null) cur[0] = key;
                else if (cur[1] == null) cur[1] = key;
                // 두 칸 다 직접 정해 두었으면 건드리지 않음
            }
        }

        void LoadKeys()
        {
            DefaultKeys();
            try { if (File.Exists(KeyFile)) Deserialize(File.ReadAllText(KeyFile)); } catch { }
            RebuildLookup();
        }

        void SaveKeys()
        {
            try { Directory.CreateDirectory(KeyDir); KitBackup.Before(KeyFile); File.WriteAllText(KeyFile, Serialize(keys, pickMod, selMod)); } catch (Exception e) { Say("단축키 저장 실패: " + e.Message); }
            RebuildLookup();
        }

        void Capture(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Escape) { capId = null; Say("단축키 바꾸기 취소"); BuildKeys(); return; }
            var combo = Combo(e.keyCode, e.ctrlKey || e.commandKey, e.shiftKey, e.altKey);
            if (combo == null) return;                    // 조합 키만 누른 상태 — 다음 키를 기다림
            // 같은 키를 쓰던 다른 동작에서는 뺀다
            string taken = null;
            foreach (var kv in keys)
                for (int i = 0; i < 2; i++)
                    if (kv.Value[i] == combo && !(kv.Key == capId && i == capSlot)) { kv.Value[i] = null; taken = kv.Key; }
            keys[capId][capSlot] = combo;
            Say(taken != null ? $"{Pretty(combo)} → ‘{NameOf(capId)}’ (‘{NameOf(taken)}’ 에서는 뺐어요)" : $"{Pretty(combo)} → ‘{NameOf(capId)}’");
            capId = null;
            SaveKeys();
            BuildKeys();
        }

        static string NameOf(string id) => ACTIONS.FirstOrDefault(a => a.id == id).name ?? id;

        /// <summary>Esc 처리: 단축키 바꾸는 중이면 취소, 단축키 창이 열려 있으면 닫기. 처리했으면 true.</summary>
        public bool BackHandled()
        {
            if (renameTf != null) { cancelRename?.Invoke(); return true; }
            if (xf != null) { CancelTransform(); return true; }
            if (poly != null) { poly = null; overlay.MarkDirtyRepaint(); return true; }
            if (selecting) { selecting = false; lasso.Clear(); overlay.MarkDirtyRepaint(); return true; }
            if (capId != null) { capId = null; BuildKeys(); Say("단축키 바꾸기 취소"); return true; }
            if (keyPanel != null && keyPanel.style.display == DisplayStyle.Flex) { keyPanel.style.display = DisplayStyle.None; return true; }
            if (impDlg != null && impDlg.style.display == DisplayStyle.Flex) { CloseImport(); return true; }
            if (ExportOpen) { CloseExport(); return true; }
            if (SaveOpen) { CloseSaveDlg(); return true; }
            return false;
        }

        void ToggleKeys()
        {
            if (keyPanel == null) MakeKeyPanel();
            bool show = keyPanel.style.display != DisplayStyle.Flex;
            keyPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) { keyPanel.BringToFront(); BuildKeys(); BuildPresets(); }
        }

        void MakeKeyPanel()
        {
            keyPanel = new VisualElement();
            keyPanel.AddToClassList("ae-keys");
            Root.Add(keyPanel);
            var hd = El(keyPanel, "ae-hd");
            Lbl(hd, "단축키 설정", "ae-title");
            Btn(hd, "초기값으로", () => { DefaultKeys(); SaveKeys(); BuildKeys(); Say("단축키를 초기값으로 되돌렸어요."); }, "처음 단축키로");
            Btn(hd, "닫기", () => { capId = null; keyPanel.style.display = DisplayStyle.None; }, "닫기 (Esc)");
            Lbl(keyPanel, "칸을 누르고 원하는 키(Ctrl · Shift · Alt 조합 가능)를 누르세요. Esc = 취소 · ✕ = 비우기. 바로 저장돼요.", "ae-note");
            var body = El(keyPanel, "ae-keys-body");
            var sv = new ScrollView(ScrollViewMode.Vertical); sv.AddToClassList("ae-keys-sv"); body.Add(sv);
            keyList = sv.contentContainer;
            var right = El(body, "ae-keys-side");
            Sec(right, "마우스 조합", "누른 채 클릭");
            ModRow(right, "스포이드", () => pickMod, v => pickMod = v);
            ModRow(right, "레이어 고르기", () => selMod, v => selMod = v);
            Sec(right, "프리셋", "지금 단축키를 이름 붙여 저장 · 불러오기");
            var pr = El(right, "ae-row");
            presetName = new TextField { value = "내 단축키" }; presetName.AddToClassList("ae-text"); pr.Add(presetName);
            Btn(pr, "지금 값 저장", SavePreset, "지금 단축키를 이 이름의 프리셋으로");
            presetList = El(right, "ae-col");
        }

        void ModRow(VisualElement p, string name, Func<string> get, Action<string> set)
        {
            var r = El(p, "ae-row");
            Lbl(r, name, "ae-k");
            Button b = null;
            b = Btn(r, get(), () => { int i = Array.IndexOf(MODS, get()); set(MODS[(i + 1) % MODS.Length]); b.text = get(); SaveKeys(); }, "누를 때마다 Alt → Ctrl → Shift");
        }

        void BuildKeys()
        {
            if (keyList == null) return;
            keyList.Clear();
            foreach (var a in ACTIONS)
            {
                var id = a.id;
                if (id == "#") { Lbl(keyList, a.name, "ae-key-sec"); continue; }
                var r = El(keyList, "ae-key-row");
                Lbl(r, a.name, "ae-key-n");
                for (int slot = 0; slot < 2; slot++)
                {
                    int sl = slot;
                    bool cap = capId == id && capSlot == sl;
                    var b = Btn(r, cap ? "키를 누르세요…" : Pretty(keys[id][sl]), () => { capId = id; capSlot = sl; BuildKeys(); Root.Focus(); }, sl == 0 ? "주 키" : "보조 키", "ae-key-b");
                    b.EnableInClassList("ae-btn--on", cap);
                }
                Btn(r, "✕", () => { keys[id][0] = keys[id][1] = null; SaveKeys(); BuildKeys(); }, "이 동작의 키 비우기");
            }
        }

        void SavePreset()
        {
            var name = (presetName.value ?? "").Trim();
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            if (name.Length == 0) { Say("프리셋 이름을 적어 주세요."); return; }
            try { Directory.CreateDirectory(KeyDir); File.WriteAllText(Path.Combine(KeyDir, "preset · " + name + ".txt"), Serialize(keys, pickMod, selMod)); Say($"프리셋 ‘{name}’ 저장"); }
            catch (Exception e) { Say("프리셋 저장 실패: " + e.Message); }
            BuildPresets();
        }

        void BuildPresets()
        {
            if (presetList == null) return;
            presetList.Clear();
            if (!Directory.Exists(KeyDir)) return;
            foreach (var f in Directory.GetFiles(KeyDir, "preset · *.txt").OrderBy(x => x))
            {
                string ff = f, nm = Path.GetFileNameWithoutExtension(f).Substring("preset · ".Length);
                var r = El(presetList, "ae-row");
                Lbl(r, nm, "ae-key-n");
                Btn(r, "불러오기", () => { try { Deserialize(File.ReadAllText(ff)); SaveKeys(); BuildKeys(); Say($"프리셋 ‘{nm}’ 불러옴"); MakeModsFresh(); } catch (Exception e) { Say("불러오기 실패: " + e.Message); } }, "이 프리셋의 단축키로");
                Btn(r, "삭제", () => { try { File.Delete(ff); } catch { } BuildPresets(); }, "프리셋 지우기");
            }
        }

        /// <summary>프리셋을 불러온 뒤 마우스 조합 버튼 글자 갱신(창을 다시 만듦).</summary>
        void MakeModsFresh()
        {
            if (keyPanel == null) return;
            bool shown = keyPanel.style.display == DisplayStyle.Flex;
            keyPanel.RemoveFromHierarchy();
            keyPanel = null;
            MakeKeyPanel();
            keyPanel.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            BuildKeys(); BuildPresets();
        }

        static readonly Tool[] TOOL_ORDER = { Tool.Pen, Tool.Eraser, Tool.Picker, Tool.Fill, Tool.Blend, Tool.LassoFill, Tool.LassoErase, Tool.Line, Tool.Rect, Tool.SelRect, Tool.Lasso, Tool.Poly, Tool.Hand };

        /// <summary>도구 줄에서 위 · 아래로 옮겨 고르기(S 아래 · D 위).</summary>
        void StepTool(int d)
        {
            int i = Array.IndexOf(TOOL_ORDER, tool);
            SetTool(TOOL_ORDER[(i + d + TOOL_ORDER.Length) % TOOL_ORDER.Length]);
        }

        void OnKeyUp(KeyUpEvent e)
        {
            if (KeysOff) return;
            if (TextFocus || IsTypingEl(e.target as VisualElement)) return;
            if (IsArrow(e.keyCode)) heldArrows.Remove(e.keyCode);
            if (onionHoldKey != KeyCode.None && e.keyCode == onionHoldKey) EndOnionHold();
            if (e.keyCode == KeyCode.LeftShift || e.keyCode == KeyCode.RightShift) shiftDown = false;
            if (e.keyCode != playKey || !spaceHeld) return;
            spaceHeld = false;
            if (!spaceUsed && !TextFocus) TogglePlay();       // 누르고 끌지 않았으면 재생 · 정지
            e.StopPropagation();
        }
    }
}
