using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
using UnityEngine.InputSystem;
#endif

namespace Mori.Client
{
    /// <summary>
    /// Mori 킷 진입점. 두 경우에만 생긴다:
    ///  · 메뉴 Mori ▸ 킷 켜고 플레이(체크)가 켜져 있을 때 — 어느 장면에서 Play 하든. 게임이 장면을 바꿔도(MFS 는 늘 Main 으로) 지워지지 않고 그 위에 남는다.
    ///  · ‘MoriKit’ 이라는 이름의 장면에서 Play 할 때.
    /// 킷이 떠 있는 동안 게임의 Input System 동작(InputAction)을 잠시 꺼서, 킷에서 누른 키에 게임 캐릭터가 반응하지 않게 한다(킷을 닫으면 되돌림).
    /// 이 어셈블리는 UNITY_EDITOR 에서만 컴파일되어 게임 빌드에 들어가지 않는다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class KitRoot : MonoBehaviour
    {
        public const string SCENE = "MoriKit";
        public static GameSettings Settings = new GameSettings();
        public static KitRoot Main { get; private set; }

        UIDocument doc;
        PanelSettings panel;
        VisualElement root;
        public AudioHub Audio { get; private set; }
        public WorkshopScreen Workshop { get; private set; }
        float escTime = -10;

        public const string PREF_PLAY = "MoriKit.PlayWithKit", PREF_BLOCK = "MoriKit.BlockGameInput";

        /// <summary>메뉴 ‘킷 켜고 플레이’ 가 켜져 있는지(에디터 설정에 저장).</summary>
        public static bool PlayWithKit
        {
            get => UnityEditor.EditorPrefs.GetBool(PREF_PLAY, false);
            set => UnityEditor.EditorPrefs.SetBool(PREF_PLAY, value);
        }

        /// <summary>킷이 떠 있는 동안 게임 입력 막기(기본 끔).
        /// 게임의 InputAction 을 직접 껐다 켜는 방식이라 게임 쪽 입력 흐름과 어긋날 수 있음
        /// (MFS 최신 빌드: 켜 두면 타이틀 → 게임 로딩이 끝나지 않음). 킷은 게임 상태를 건드리지 않는 것이 원칙이라 기본은 끔.</summary>
        public static bool BlockGameInput
        {
            get => UnityEditor.EditorPrefs.GetBool(PREF_BLOCK, false);
            set => UnityEditor.EditorPrefs.SetBool(PREF_BLOCK, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!PlayWithKit && SceneManager.GetActiveScene().name != SCENE) return;
            if (FindAnyObjectByType<KitRoot>() != null) return;
            var go = new GameObject("Mori Kit");
            DontDestroyOnLoad(go);                 // 게임이 장면을 바꿔도 남음
            go.AddComponent<KitRoot>();
        }

        void Awake()
        {
            Main = this;
            Settings = GameSettings.Load();
            var asset = Resources.Load<PanelSettings>("MoriPanel");
            panel = asset != null ? Instantiate(asset) : ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "Mori Kit Panel";
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("MoriTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.clearColor = false;          // 게임 화면을 지우지 않음(킷은 그 위에 얹힘)
            panel.sortingOrder = 1000;         // 게임 UI 보다 위에
            Settings.Apply(panel);

            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            root = doc.rootVisualElement;
            foreach (var name in new[] { "MoriUI", "MoriMeta", "MoriStage", "MoriScroll" })
            {
                var sh = Resources.Load<StyleSheet>(name);
                if (sh != null) root.styleSheets.Add(sh);
                else Debug.LogWarning($"[Mori 킷] Resources/{name}.uss 를 찾지 못했습니다.");
            }
            Audio = new AudioHub(gameObject);
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic", "Arial Unicode MS", "Arial" }, 16);
            if (font != null) root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
            Dialog.Init(root);
            Workshop = new WorkshopScreen(root, Audio);
            root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape) { Back(); e.StopPropagation(); }
            }, TrickleDown.TrickleDown);
            Keys.Hook();
            ActInput.Hook();
            LayoutGuard.Watch(root);
            AudioGuard.Hook(root);              // Unity 오디오 엔진 버그(로그 폭주 → 튕김) 감시 · 저장 · 오디오 다시 시작
            Workshop.Show();
        }

        void Back()
        {
            if (Time.realtimeSinceStartup - escTime < 0.25f) return;
            escTime = Time.realtimeSinceStartup;
            if (Dialog.CloseTop()) return;
            Workshop.Back();
            if (!Workshop.IsOpen) Workshop.Show();             // 킷은 작업실이 곧 전부 — 닫히지 않음
        }

        // ───────── 게임 입력 막기 ─────────
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
        readonly List<InputAction> paused = new List<InputAction>();
        readonly HashSet<InputAction> uiActions = new HashSet<InputAction>();
#endif

        /// <summary>켜져 있는 게임의 InputAction 을 모두 꺼 둠(게임이 나중에 켠 것도 매 프레임 다시 끔).</summary>
        void BlockInput()
        {
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            if (!BlockGameInput) { RestoreInput(); return; }
            // UI 입력(EventSystem 의 UI 모듈 · ‘UI’ 동작 묶음)은 그대로 둠 — 끄면 킷 화면도 눌리지 않음
            uiActions.Clear();
            foreach (var m in FindObjectsByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>())
                if (m != null && m.actionsAsset != null) foreach (var ua in m.actionsAsset) uiActions.Add(ua);
            var on = InputSystem.ListEnabledActions();
            foreach (var a in on)
            {
                if (a == null || uiActions.Contains(a) || (a.actionMap != null && a.actionMap.name == "UI")) continue;
                a.Disable();
                if (!paused.Contains(a)) paused.Add(a);
            }
#endif
        }

        void RestoreInput()
        {
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            foreach (var a in paused) if (a != null) { try { a.Enable(); } catch { } }
            paused.Clear();
#endif
        }

        // ───────── 마우스 · 숨기기 ─────────
        /// <summary>킷이 보이는 중(F10 으로 숨기면 게임을 그대로 플레이 — 마우스 · 입력도 게임에게).</summary>
        public bool Visible { get; private set; } = true;

        public void SetVisible(bool on)
        {
            Visible = on;
            // display:none 은 크기가 0 이 되어 칸 배치 보정(LayoutGuard)이 망가뜨림 → 크기는 그대로 두고 안 보이게 · 안 눌리게만
            root.style.visibility = on ? Visibility.Visible : Visibility.Hidden;
            SetPicking(root, on);
            if (!on)
            {
                root.focusController?.focusedElement?.Blur();
                RestoreInput(); RestoreCursor();
            }
        }

        // 숨긴 동안 킷의 칸들이 마우스를 가로채지 않게(원래 설정을 기억했다가 되돌림)
        readonly System.Collections.Generic.Dictionary<VisualElement, PickingMode> pickSaved = new System.Collections.Generic.Dictionary<VisualElement, PickingMode>();

        void SetPicking(VisualElement e, bool on)
        {
            if (!on)
            {
                pickSaved.Clear();
                e.Query<VisualElement>().ForEach(v => { pickSaved[v] = v.pickingMode; v.pickingMode = PickingMode.Ignore; });
            }
            else
            {
                foreach (var kv in pickSaved) if (kv.Key != null) kv.Key.pickingMode = kv.Value;
                pickSaved.Clear();
            }
        }

        bool cursorSaved; CursorLockMode savedLock; bool savedVisible;

        /// <summary>게임이 마우스를 잠그거나 숨겨도 킷이 보이는 동안은 풀어 둠(매 프레임 — 게임이 다시 잠가도).</summary>
        void FreeCursor()
        {
            if (!cursorSaved) { cursorSaved = true; savedLock = UnityEngine.Cursor.lockState; savedVisible = UnityEngine.Cursor.visible; }
            if (UnityEngine.Cursor.lockState != CursorLockMode.None) UnityEngine.Cursor.lockState = CursorLockMode.None;
            if (!UnityEngine.Cursor.visible) UnityEngine.Cursor.visible = true;
        }

        void RestoreCursor()
        {
            if (!cursorSaved) return;
            cursorSaved = false;
            UnityEngine.Cursor.lockState = savedLock; UnityEngine.Cursor.visible = savedVisible;
        }

        void LateUpdate() { if (Visible) FreeCursor(); }

        static bool F10Pressed()
        {
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            return Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F10);
#else
            return false;
#endif
        }

        void Update()
        {
            TexGrave.Tick();                                                // 다 쓴 텍스처는 몇 프레임 뒤에 지움
            AudioGuard.Tick(() => Workshop?.SaveAll());                     // 숨겨 둔 동안에도 감시
            if (F10Pressed()) SetVisible(!Visible);                         // F10 = 킷 숨기기 / 보이기
            if (!Visible) return;
            BlockInput();
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) Back();
#endif
            if (Workshop != null && !Workshop.IsOpen) Workshop.Show();     // ‘나가기’ 를 눌러도 다시 작업실로
            Workshop?.OnUpdate(Time.unscaledDeltaTime);
        }

        void OnApplicationQuit() => Workshop?.SaveAll();

        void OnDestroy()
        {
            Workshop?.SaveAll();                // 플레이를 멈추거나 킷을 닫을 때 편집 중인 그림 저장
            TexGrave.Flush();
            AudioGuard.Unhook();
            RestoreInput();
            RestoreCursor();
            if (Main == this) Main = null;
        }
    }
}
