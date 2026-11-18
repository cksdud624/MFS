using System.Collections.Generic;
using System.IO;
using System.Text;
using InGame.Story;
using InGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static Common.GameDefine;

namespace StoryTool
{
    /// <summary>
    /// 시나리오 툴 창 (메뉴 Story > Story Tool). 이야기 표를 대본처럼 읽고 고치고, 배치 화면에서 대상의 자리를 끌어서 정한다.
    /// 왼쪽에서 퀘스트나 시나리오를 고르면 가운데에 그 줄이 대본으로 뜨고, 줄을 고르면 오른쪽에서 그 줄의 칸을 고친다.
    /// 배치 화면을 열면 Scene 뷰에 그 줄까지 진행한 모습(Zone과 서 있는 대상)이 나오고, 위치를 쓰는 줄은 핸들로 옮길 수 있다.
    /// 원본은 tsv다. 저장하면 tsv에 다시 쓰고 Update Table과 Validate Story를 이어서 돌린다.
    /// 창은 무엇을 고를지와 화면을 맞추는 일만 한다. 표를 고치는 것은 StoryEditor가, 오른쪽 칸은 StoryToolInspector가 맡는다
    /// </summary>
    public class StoryToolWindow : EditorWindow
    {
        #region Types
        /// <summary>왼쪽 목록의 항목. 챕터 같은 묶음은 Doc이 없다</summary>
        private class DocItem
        {
            public string Label { get; }
            public StoryDoc Doc { get; }

            public DocItem(string label, StoryDoc doc)
            {
                Label = label;
                Doc = doc;
            }
        }

        /// <summary>대본의 한 줄. 표의 줄 하나가 여러 줄로 보일 수 있다 (대화는 그 아래에 대사를 펼친다)</summary>
        private class ScriptRow
        {
            public StoryLine Line { get; }
            public int Indent { get; }
            //펼쳐 보여주는 대사인지. 표의 줄이 아니라서 고르면 그 대사가 속한 줄(Line)을 고른 것으로 본다
            public bool IsSpeech { get; }
            public StoryToolDocument.DialogueLine Speech { get; }
            //그 대화의 몇 번째 대사인지. 대사가 아니면 NoSpeech다
            public int SpeechIndex { get; }

            public ScriptRow(StoryLine line, int indent)
            {
                Line = line;
                Indent = indent;
                SpeechIndex = NoSpeech;
            }

            public ScriptRow(StoryLine line, int indent, StoryToolDocument.DialogueLine speech, int speechIndex)
            {
                Line = line;
                Indent = indent;
                IsSpeech = true;
                Speech = speech;
                SpeechIndex = speechIndex;
            }
        }
        #endregion

        #region Fields
        private const string MenuPath = "Story/Story Tool";
        private const string WindowTitle = "Story Tool";
        private const string StyleSheetName = "StoryTool.uss";
        private const string SaveMessage = "저장하지 않은 변경이 있습니다. 표(tsv)에 저장할까요?";
        private const string StageOpenText = "배치 화면 열기";
        private const string StageCloseText = "배치 화면 닫기";

        //목차, 대본, 고른 줄 세 칸이 모두 보이는 가장 작은 창 크기
        private static readonly Vector2 MinWindowSize = new(900f, 420f);
        private const float DocPaneWidth = 250f;
        private const float InspectorPaneWidth = 320f;
        private const float DocItemHeight = 20f;
        //대본에서 한 단계 들여 쓰는 폭 (퀘스트가 부르는 시나리오의 줄, 대화의 대사)
        private const float IndentWidth = 18f;
        private const float RowPadding = 6f;
        //끌어 놓은 자리의 위아래 이 거리 안에 바닥이 있으면 발밑을 바닥에 붙인다
        private const float GroundSnapRange = 1f;
        //새 줄의 자리를 화면 가운데로 잡을 때는 발밑이 어디인지 모르므로 이만큼 넓게 바닥을 찾는다
        private const float GroundSearchRange = 15f;
        //새 대상을 고른 대상에서 이만큼 옆에 세운다. 겹쳐 있으면 새로 생긴 것이 보이지 않는다
        private const float NewActorOffset = 2f;
        //대본의 줄이 펼친 대사가 아닐 때의 대사 순번
        private const int NoSpeech = -1;
        //재생할 때 한 줄에 머무는 시간 (초). 실제 연출의 빠르기가 아니라 읽고 넘어갈 만한 간격이다
        private const double PlayInterval = 1.2;
        //고른 자리로 Scene 뷰를 옮길 때 보여주는 넓이와, 발밑에서 화면 가운데까지의 높이
        private static readonly Vector2 FrameSize = new(32f, 18f);
        private const float FrameHeight = 3f;
        //이야기가 하나도 없을 때 새 퀘스트와 연출을 만드는 챕터
        private const long FirstChapter = 1;

        private const string ClassPane = "story-pane";
        private const string ClassPaneTitle = "story-pane__title";
        private const string ClassPaneBar = "story-pane__bar";
        private const string ClassQuestTitle = "story-quest-title";
        private const string ClassDocGroup = "story-doc--group";
        private const string ClassRow = "story-row";
        private const string ClassRowBody = "story-row__body";
        private const string ClassRowId = "story-row__id";
        private const string ClassRowText = "story-row__text";
        private const string ClassRowNote = "story-row__note";
        private const string ClassRowError = "story-row__error";
        private const string ClassRowSpeech = "story-row--speech";
        private const string ClassRowHeader = "story-row--header";
        private const string ClassRowInvalid = "story-row--invalid";
        private const string ClassStatus = "story-status";

        [SerializeField] private StoryToolDocument _document;
        //고른 묶음과 줄. 도메인 리로드 뒤에 같은 것을 다시 고르려고 키로 기억한다
        [SerializeField] private string _docKey;
        [SerializeField] private string _lineKey;
        //고른 것이 대화 아래에 펼친 대사면 몇 번째 대사인지. 배치 화면이 그 대사를 말풍선으로 띄운다
        [SerializeField] private int _speechIndex = NoSpeech;
        [SerializeField] private bool _snapToGround = true;
        [SerializeField] private bool _showCamera = true;
        //도메인 리로드 전에 배치 화면이 열려 있었는지. 임시 씬은 리로드를 넘지 못해서 닫았다가 다시 연다
        [SerializeField] private bool _reopenStage;
        //마지막 저장 때 검사에 걸린 줄
        [SerializeField] private List<StoryToolError> _errors = new();
        [SerializeField] private string _status = string.Empty;

        private StoryEditor _editor;
        private StoryToolInspector _inspector;
        private StoryOutline _outline;
        private StoryDoc _doc;
        private StoryLine _line;
        //고른 줄까지 진행한 모습. 표나 고른 줄이 바뀌면 다시 계산한다
        private StoryReplay _replay;
        private int _replayVersion;
        private StoryToolStage _stage;
        //재생 중인지와 다음 줄로 넘어갈 시각 (EditorApplication.timeSinceStartup 기준)
        private bool _isPlaying;
        private double _nextStepTime;
        private readonly StoryToolSceneGui _sceneGui = new();
        private readonly List<ScriptRow> _rows = new();
        //대본에 보이는 표의 줄 (펼친 대사 제외). 배치 화면이 이 줄들의 표식을 그린다
        private readonly List<StoryLine> _visibleLines = new();
        private readonly Dictionary<string, int> _docItemIds = new();
        //목차에 띄운 글을 이어 붙인 것. 묶음이나 제목이 바뀌었을 때만 목차를 다시 만들려고 기억한다 (다시 만들면 스크롤이 처음으로 돌아간다)
        private string _docTreeSignature;
        private bool _isRebuildingDocTree;
        private readonly HashSet<ZoneLayout.ZoneEntry> _zoneBuffer = new();
        private readonly List<ZoneLayout.ZoneEntry> _neighborBuffer = new();
        private readonly List<StoryToolStage.ActorPreview> _actorBuffer = new();

        private TreeView _docTree;
        private ListView _rowList;
        private TextField _questTitleField;
        private Label _statusLabel;
        private ToolbarButton _stageButton;
        private ToolbarToggle _playToggle;

        private bool IsStageOpen => _stage != null && _stage.IsCurrent;
        #endregion

        #region LifeCycle
        private void OnEnable()
        {
            minSize = MinWindowSize;
            if (_document == null)
            {
                _document = CreateInstance<StoryToolDocument>();
                //에셋이 아닌 오브젝트는 다른 씬을 열 때 정리되어 사라진다. 창이 열려 있는 동안은 남아 있어야 한다
                _document.hideFlags = HideFlags.DontSave;
                _document.Load();
            }
            _outline = new StoryOutline(_document);
            _editor = new StoryEditor(_document) { DefaultPosition = TryGetDefaultPosition };
            _inspector = new StoryToolInspector(_document, _editor);
            //목차는 CreateGUI에서 다시 만든다. 도메인 리로드가 이 값을 넘겨주면 같은 목차라고 보고 만들지 않는다
            _docTreeSignature = null;

            _editor.OnChanged += HandleDocumentChanged;
            _editor.OnStructureChanged += HandleStructureEdited;
            Undo.undoRedoPerformed += HandleUndoRedo;
            SceneView.duringSceneGui += HandleSceneGui;
            StoryToolStage.OnClosed += HandleStageClosed;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            _sceneGui.OnLineClicked += HandleMarkerClicked;
            _sceneGui.OnPositionDragged += HandlePositionDragged;

            if (_reopenStage)
            {
                _reopenStage = false;
                //리로드 도중에는 Scene 뷰가 아직 준비되지 않아서 에디터가 다시 돌기 시작한 뒤에 연다
                EditorApplication.update += ReopenStage;
            }
        }

        private void OnDisable()
        {
            _editor.OnChanged -= HandleDocumentChanged;
            _editor.OnStructureChanged -= HandleStructureEdited;
            Undo.undoRedoPerformed -= HandleUndoRedo;
            SceneView.duringSceneGui -= HandleSceneGui;
            StoryToolStage.OnClosed -= HandleStageClosed;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            _sceneGui.OnLineClicked -= HandleMarkerClicked;
            _sceneGui.OnPositionDragged -= HandlePositionDragged;
            EditorApplication.update -= ReopenStage;
            SetPlaying(false);
        }

        private void OnDestroy()
        {
            CloseStage();
            if (_document != null)
                DestroyImmediate(_document);
        }

        private void OnFocus()
        {
            //툴 밖에서(엑셀, 텍스트 편집기) 표를 고쳤으면 다시 읽는다. 툴에서 고친 것이 있으면 버리지 않고 저장할 때 묻는다
            if (_document == null || _document.IsDirty || !_document.HasChangedOnDisk(false)) return;
            Reload();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(GetStyleSheetPath());
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            else
                Debug.LogError($"{StyleSheetName} is missing next to {nameof(StoryToolWindow)}.cs.");

            root.Add(CreateToolbar());

            var body = new TwoPaneSplitView(0, DocPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
            body.Add(CreateDocPane());
            var right = new TwoPaneSplitView(1, InspectorPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
            right.Add(CreateScriptPane());
            right.Add(CreateInspectorPane());
            body.Add(right);
            root.Add(body);

            _statusLabel = new Label();
            _statusLabel.AddToClassList(ClassStatus);
            root.Add(_statusLabel);

            RefreshStructure();
            RefreshStageButton();
        }
        #endregion

        #region Public
        [MenuItem(MenuPath)]
        public static void Open() => GetWindow<StoryToolWindow>(WindowTitle);

        public override void SaveChanges()
        {
            Save();
            base.SaveChanges();
        }
        #endregion

        #region Event Handlers
        //Undo는 문서의 칸과 줄을 통째로 되돌린다. 문서가 스스로 알 수 없으므로 여기서 바뀌었다고 알리고 화면을 목차부터 다시 맞춘다
        private void HandleUndoRedo()
        {
            if (_document == null || _docTree == null) return;

            _document.MarkChanged();
            RefreshStructure();
        }

        //칸의 값만 바뀌었다 (위치를 끌어 놓았다). 줄은 그대로이므로 대본의 글, 입력 칸, 배치 화면만 바뀐 값에 맞춘다
        private void HandleDocumentChanged()
        {
            _replay = null;
            _rowList?.RefreshItems();
            _inspector.RefreshValues();
            RefreshStage();
            RefreshStatus();
        }

        //줄이 늘거나 줄었거나 줄의 뜻이 바뀌었다 (명령, 대상, 가리키는 대화). 목차부터 다시 맞춘다
        private void HandleStructureEdited()
        {
            if (_docTree != null)
                RefreshStructure();
        }

        private void HandleSceneGui(SceneView sceneView)
        {
            if (!IsStageOpen || _doc == null) return;
            _sceneGui.Draw(_document, GetReplay(), _stage, _visibleLines, _line, _showCamera);
        }

        private void HandleStageClosed(StoryToolStage stage)
        {
            if (stage != _stage) return;

            _stage = null;
            RefreshStageButton();
        }

        private void HandleBeforeAssemblyReload()
        {
            if (!IsStageOpen) return;

            _reopenStage = true;
            CloseStage();
        }

        //Play에 들어가면 Scene 뷰가 게임 씬을 보여줘야 하므로 배치 화면을 닫는다
        private void HandlePlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                CloseStage();
        }

        private void HandleMarkerClicked(StoryLine line) => SelectLine(line, true);

        //재생 중에 정해진 간격마다 다음 줄로 넘긴다. 마지막 줄이면 멈춘다
        private void HandlePlayUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextStepTime) return;

            _nextStepTime = EditorApplication.timeSinceStartup + PlayInterval;
            if (!StepRow(1))
                SetPlaying(false);
        }

        /// <summary>
        /// 핸들을 끌어 놓은 자리를 표에 적는다. 표의 위치는 Zone 로컬 좌표라서 그 자리가 들어 있는 Zone을 찾아 Zone 칸까지 같이 바꾼다.
        /// 그래서 Zone 경계를 넘겨 끌어도 표에는 늘 그 Zone 안의 좌표가 남는다
        /// </summary>
        private void HandlePositionDragged(PositionSlot slot, Vector2 world)
        {
            //x만 적는 줄은 높이를 적지 않으므로 바닥에 붙일 것이 없다
            if (_snapToGround && !slot.IsXOnly && IsStageOpen && _stage.TryFindGround(world, GroundSnapRange, out float groundY))
                world.y = groundY;
            if (!_document.TryReadPosition(slot, out long zoneId, out _)) return;
            if (!TryToLocal(world, zoneId, out zoneId, out var local)) return;

            _editor.SetPosition(slot, zoneId, local);
        }
        #endregion

        #region Private - Build
        //스타일 시트는 이 스크립트와 같은 폴더에 둔다. 폴더를 옮겨도 따라오게 스크립트의 위치에서 찾는다
        private string GetStyleSheetPath()
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            return Path.Combine(Path.GetDirectoryName(scriptPath) ?? string.Empty, StyleSheetName).Replace('\\', '/');
        }

        private Toolbar CreateToolbar()
        {
            var toolbar = new Toolbar();
            _stageButton = new ToolbarButton(ToggleStage);
            toolbar.Add(_stageButton);
            toolbar.Add(new ToolbarButton(() => FrameSelection(true)) { text = "고른 자리 보기" });

            var snapToggle = new ToolbarToggle { text = "바닥에 붙이기", value = _snapToGround };
            snapToggle.tooltip = "끌어 놓은 자리 가까이에 바닥이 있으면 발밑을 바닥에 맞춘다";
            snapToggle.RegisterValueChangedCallback(change => _snapToGround = change.newValue);
            toolbar.Add(snapToggle);

            var cameraToggle = new ToolbarToggle { text = "카메라 범위", value = _showCamera };
            cameraToggle.tooltip = "고른 줄에서 게임 화면에 보이는 범위를 배치 화면에 그린다";
            cameraToggle.RegisterValueChangedCallback(change =>
            {
                _showCamera = change.newValue;
                SceneView.RepaintAll();
            });
            toolbar.Add(cameraToggle);

            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton(() => StepRow(-1)) { text = "◀", tooltip = "앞 줄로" });
            toolbar.Add(new ToolbarButton(() => StepRow(1)) { text = "▶", tooltip = "다음 줄로" });
            _playToggle = new ToolbarToggle { text = "재생", tooltip = "고른 줄부터 한 줄씩 넘기며 보여준다" };
            _playToggle.RegisterValueChangedCallback(change => SetPlaying(change.newValue));
            toolbar.Add(_playToggle);

            toolbar.Add(new ToolbarSpacer { flex = true });
            toolbar.Add(new ToolbarButton(ReloadWithConfirm) { text = "다시 읽기", tooltip = "표(tsv)를 다시 읽는다. 저장하지 않은 것은 버린다" });
            toolbar.Add(new ToolbarButton(Save) { text = "저장", tooltip = "표(tsv)에 저장하고 Update Table과 Validate Story를 돌린다" });
            return toolbar;
        }

        private VisualElement CreateDocPane()
        {
            var pane = CreatePane("목차");
            var bar = CreateBar(pane);
            bar.Add(new Button(() => AddDoc(StoryLineKind.Quest)) { text = "+ 퀘스트", tooltip = "열어 둔 묶음과 같은 챕터에 새 퀘스트를 만든다" });
            bar.Add(new Button(() => AddDoc(StoryLineKind.Scenario)) { text = "+ 연출", tooltip = "열어 둔 묶음과 같은 챕터에 새 연출(시나리오)을 만든다" });

            _docTree = new TreeView
            {
                fixedItemHeight = DocItemHeight,
                selectionType = SelectionType.Single,
                makeItem = () => new Label(),
                bindItem = BindDocItem,
            };
            _docTree.selectionChanged += items =>
            {
                foreach (var item in items)
                {
                    //묶음(챕터, 퀘스트/연출 구분)은 고를 것이 없다
                    if (!_isRebuildingDocTree && item is DocItem { Doc: not null } docItem)
                        SelectDoc(docItem.Doc);
                }
            };
            _docTree.style.flexGrow = 1f;
            pane.Add(_docTree);
            return pane;
        }

        private VisualElement CreateScriptPane()
        {
            var pane = CreatePane("대본");
            var bar = CreateBar(pane);
            bar.Add(new Button(AddLine) { text = "+ 줄", tooltip = "고른 줄 다음에 새 줄을 끼운다. 대상 묶음에서는 새 대상을, 일반 대화 묶음에서는 새 일반 대화를 만든다" });
            bar.Add(new Button(RemoveSelected) { text = "삭제", tooltip = "고른 줄(또는 고른 대사)을 지운다" });
            bar.Add(new Button(() => MoveSelected(-1)) { text = "▲", tooltip = "고른 줄을 한 줄 위로 옮긴다" });
            bar.Add(new Button(() => MoveSelected(1)) { text = "▼", tooltip = "고른 줄을 한 줄 아래로 옮긴다" });

            //퀘스트를 열었을 때만 보인다. 연출은 제목이 없다
            _questTitleField = new TextField("퀘스트 제목") { isDelayed = true };
            _questTitleField.AddToClassList(ClassQuestTitle);
            _questTitleField.RegisterValueChangedCallback(change =>
            {
                if (_doc != null && _doc.Kind == StoryLineKind.Quest)
                    _editor.SetQuestTitle(_doc.Number, change.newValue);
            });
            pane.Add(_questTitleField);

            _rowList = new ListView
            {
                itemsSource = _rows,
                selectionType = SelectionType.Single,
                //메모와 에러가 붙은 줄은 그만큼 길어진다
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                makeItem = CreateRowElement,
                bindItem = BindRow,
            };
            _rowList.selectionChanged += _ =>
            {
                int index = _rowList.selectedIndex;
                if (index >= 0 && index < _rows.Count)
                    Select(_rows[index].Line, _rows[index].SpeechIndex, false);
            };
            _rowList.style.flexGrow = 1f;
            pane.Add(_rowList);
            return pane;
        }

        private VisualElement CreateInspectorPane()
        {
            var pane = CreatePane("고른 줄");
            pane.Add(_inspector.Root);
            return pane;
        }

        private static VisualElement CreatePane(string title)
        {
            var pane = new VisualElement();
            pane.AddToClassList(ClassPane);
            var label = new Label(title);
            label.AddToClassList(ClassPaneTitle);
            pane.Add(label);
            return pane;
        }

        //칸의 제목 아래에 버튼을 놓는 줄
        private static VisualElement CreateBar(VisualElement pane)
        {
            var bar = new VisualElement();
            bar.AddToClassList(ClassPaneBar);
            pane.Add(bar);
            return bar;
        }

        private static VisualElement CreateRowElement()
        {
            var row = new VisualElement();
            row.AddToClassList(ClassRow);

            //메모와 에러는 글 아래에 둔다. 옆에 두면 창이 좁을 때 글과 자리를 다퉈서 둘 다 읽기 어려워진다
            var body = new VisualElement();
            body.AddToClassList(ClassRowBody);
            body.Add(CreateLabel(ClassRowText));
            body.Add(CreateLabel(ClassRowNote));
            body.Add(CreateLabel(ClassRowError));
            row.Add(CreateLabel(ClassRowId));
            row.Add(body);
            return row;
        }

        private static Label CreateLabel(string className)
        {
            var label = new Label();
            label.AddToClassList(className);
            return label;
        }
        #endregion

        #region Private - Refresh
        /// <summary>
        /// 화면을 목차부터 다시 맞춘다. 표를 읽었을 때, 줄이 늘거나 줄었을 때, Undo로 되돌렸을 때 부른다.
        /// 들고 있던 줄은 표의 몇 번째 줄인지로 찾아가므로 줄이 끼거나 빠지면 다른 줄을 가리킨다. 그래서 목차를 새로 만들고 고르던 것은 키로 다시 찾는다
        /// </summary>
        private void RefreshStructure()
        {
            _outline = new StoryOutline(_document);
            _replay = null;
            RebuildDocTree();

            //고르던 묶음이 없으면 첫 퀘스트를 연다. 이야기가 비어 있으면 열 것이 없다
            _doc = _outline.Find(_docKey) ?? (_outline.Quests.Count > 0 ? _outline.Quests[0] : null);
            _docKey = _doc?.Key;
            if (_docKey != null && _docItemIds.TryGetValue(_docKey, out int id))
                _docTree.SetSelectionByIdWithoutNotify(new[] { id });

            RebuildRows();
            RefreshStatus();
        }

        private void RebuildDocTree()
        {
            _docItemIds.Clear();
            int nextId = 0;
            var signature = new StringBuilder();
            var roots = new List<TreeViewItemData<DocItem>>();
            //챕터 폴더의 표와 같은 순서로 둔다 (시나리오, 퀘스트, 일반 대화). 대상은 그 셋이 모두 가리키는 것이라 맨 앞에 둔다
            foreach (long chapter in _outline.Chapters)
            {
                var actors = _outline.FindChapterDoc(StoryLineKind.Actor, chapter);
                var talks = _outline.FindChapterDoc(StoryLineKind.Talk, chapter);
                var groups = new List<TreeViewItemData<DocItem>>
                {
                    CreateDocItem(ref nextId, $"대상 ({actors.Lines.Count})", actors, null, signature),
                    CreateDocGroup(ref nextId, "연출 (시나리오)", "연출", _outline.Scenarios, chapter, signature),
                    CreateDocGroup(ref nextId, "퀘스트", "퀘스트", _outline.Quests, chapter, signature),
                    CreateDocItem(ref nextId, $"일반 대화 ({talks.Lines.Count})", talks, null, signature),
                };
                roots.Add(CreateDocItem(ref nextId, $"{chapter}장", null, groups, signature));
            }

            //항목의 번호(id)는 순서대로 매기므로 글이 같으면 번호도 같다. 다시 만들지 않아도 번호로 고를 수 있다
            string newSignature = signature.ToString();
            if (newSignature == _docTreeSignature) return;

            _docTreeSignature = newSignature;
            //다시 만들면 목차가 전에 고르던 항목을 다시 골랐다고 알려 온다. 그대로 받으면 방금 새로 만든 묶음 대신 전에 보던 묶음이 열린다
            _isRebuildingDocTree = true;
            _docTree.SetRootItems(roots);
            _docTree.Rebuild();
            _docTree.ExpandAll();
            _isRebuildingDocTree = false;
        }

        private TreeViewItemData<DocItem> CreateDocGroup(ref int nextId, string label, string prefix, IReadOnlyList<StoryDoc> docs, long chapter,
            StringBuilder signature)
        {
            var children = new List<TreeViewItemData<DocItem>>();
            foreach (var doc in docs)
            {
                if (doc.Chapter != chapter) continue;
                string title = string.IsNullOrEmpty(doc.Title) ? string.Empty : $" · {doc.Title}";
                children.Add(CreateDocItem(ref nextId, $"{prefix} {doc.Number}{title}", doc, null, signature));
            }
            return CreateDocItem(ref nextId, label, null, children, signature);
        }

        private TreeViewItemData<DocItem> CreateDocItem(ref int nextId, string label, StoryDoc doc, List<TreeViewItemData<DocItem>> children,
            StringBuilder signature)
        {
            int id = nextId++;
            if (doc != null)
                _docItemIds[doc.Key] = id;
            signature.Append(label).Append('\n');
            return new TreeViewItemData<DocItem>(id, new DocItem(label, doc), children);
        }

        private void BindDocItem(VisualElement element, int index)
        {
            var item = _docTree.GetItemDataForIndex<DocItem>(index);
            var label = (Label)element;
            label.text = item.Label;
            label.EnableInClassList(ClassDocGroup, item.Doc == null);
        }

        //고른 묶음의 줄을 대본으로 펼친다. 퀘스트가 부르는 시나리오는 그 줄 아래에, 대화의 대사는 그 대화 아래에 들여 쓴다
        private void RebuildRows()
        {
            _rows.Clear();
            _visibleLines.Clear();
            if (_doc != null)
            {
                foreach (var line in _doc.Lines)
                    AddRows(line, 0);
            }
            _rowList.RefreshItems();

            bool isQuest = _doc != null && _doc.Kind == StoryLineKind.Quest;
            _questTitleField.style.display = isQuest ? DisplayStyle.Flex : DisplayStyle.None;
            _questTitleField.SetValueWithoutNotify(isQuest ? _doc.Title : string.Empty);

            //고르던 줄이 이 묶음에 없으면 첫 줄을 고른다. 고른 줄이 있어야 배치 화면에 그 시점의 모습이 나온다
            int index = FindRowIndex(_lineKey, _speechIndex);
            //대사를 지워서 그 순번의 대사가 없어졌으면 그 대화 줄을 고른다
            if (index < 0)
                index = FindRowIndex(_lineKey, NoSpeech);
            if (index >= 0)
                Select(_rows[index].Line, _rows[index].SpeechIndex, true);
            else
                SelectLine(_visibleLines.Count > 0 ? _visibleLines[0] : null, true);
        }

        private void AddRows(StoryLine line, int indent)
        {
            _rows.Add(new ScriptRow(line, indent));
            _visibleLines.Add(line);

            if (line.Kind == StoryLineKind.Quest && line.Command == StoryCommandCatalog.CommandScenario)
            {
                var scenario = _outline.Find(StoryLineKind.Scenario, line.Param1);
                if (scenario == null) return;
                foreach (var scenarioLine in scenario.Lines)
                    AddRows(scenarioLine, indent + 1);
                return;
            }

            //대화를 하는 줄(Dialogue 명령, 일반 대화)은 그 대화의 대사를 아래에 펼친다
            string dialogueId = StoryToolDocument.GetDialogueId(line);
            if (dialogueId == null) return;

            var speeches = _document.GetDialogue(dialogueId);
            for (int i = 0; i < speeches.Count; i++)
                _rows.Add(new ScriptRow(line, indent + 1, speeches[i], i));
        }

        private void BindRow(VisualElement element, int index)
        {
            var row = _rows[index];
            var line = row.Line;
            string error = row.IsSpeech ? null : FindError(line);
            bool isHeader = !row.IsSpeech && line.Kind == StoryLineKind.Quest && line.Command == StoryCommandCatalog.CommandScenario;

            element.EnableInClassList(ClassRowSpeech, row.IsSpeech);
            element.EnableInClassList(ClassRowHeader, isHeader);
            element.EnableInClassList(ClassRowInvalid, error != null);
            element.style.paddingLeft = RowPadding + row.Indent * IndentWidth;

            element.Q<Label>(className: ClassRowId).text = row.IsSpeech ? string.Empty : line.IdText;
            element.Q<Label>(className: ClassRowText).text = row.IsSpeech
                ? StoryCommandCatalog.DescribeSpeech(row.Speech, _document)
                : StoryCommandCatalog.Describe(line, _document);
            SetOptionalText(element.Q<Label>(className: ClassRowNote), row.IsSpeech ? null : line.Note);
            SetOptionalText(element.Q<Label>(className: ClassRowError), error);
        }

        //글이 없으면 줄째로 감춘다. 빈 줄로 남으면 대본의 줄 간격이 들쭉날쭉해진다
        private static void SetOptionalText(Label label, string text)
        {
            bool hasText = !string.IsNullOrEmpty(text);
            label.text = hasText ? text : string.Empty;
            label.style.display = hasText ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshInspector()
            => _inspector.Show(_outline, _line, _speechIndex, _line != null ? FindError(_line) : null, IsStageOpen);

        private void RefreshStageButton()
        {
            if (_stageButton != null)
                _stageButton.text = IsStageOpen ? StageCloseText : StageOpenText;
        }

        private void RefreshStatus()
        {
            hasUnsavedChanges = _document != null && _document.IsDirty;
            saveChangesMessage = SaveMessage;
            if (_statusLabel != null)
                _statusLabel.text = hasUnsavedChanges ? $"저장하지 않은 변경이 있다. {_status}" : _status;
        }
        #endregion

        #region Private - Selection
        private void SelectDoc(StoryDoc doc)
        {
            _doc = doc;
            _docKey = doc.Key;
            _replay = null;
            RebuildRows();
        }

        private void SelectLine(StoryLine line, bool syncList) => Select(line, NoSpeech, syncList);

        /// <summary>
        /// 줄을 고른다. speechIndex는 그 줄(대화) 아래에 펼친 대사를 골랐을 때 몇 번째 대사인지다.
        /// syncList면 대본의 고른 줄도 맞춘다 (대본에서 고른 것이 아닐 때)
        /// </summary>
        private void Select(StoryLine line, int speechIndex, bool syncList)
        {
            _line = line;
            _lineKey = line?.Key;
            _speechIndex = speechIndex;
            _replay = null;

            if (syncList && _rowList != null)
            {
                int index = FindRowIndex(_lineKey, _speechIndex);
                if (index >= 0)
                {
                    _rowList.SetSelectionWithoutNotify(new[] { index });
                    _rowList.ScrollToItem(index);
                }
                else
                    _rowList.ClearSelection();
            }

            RefreshInspector();
            RefreshStage();
            FrameSelection(false);
        }

        //키로 줄을 고른다. 줄을 새로 만들거나 옮긴 뒤에 그 줄을 고를 때 쓴다. 대본에 없는 키면 그대로 둔다
        private void SelectKey(string key)
        {
            int index = FindRowIndex(key, NoSpeech);
            if (index >= 0)
                Select(_rows[index].Line, NoSpeech, true);
        }

        //대본에서 그 줄(또는 그 줄의 그 대사)이 몇 번째인지. 없으면 -1
        private int FindRowIndex(string key, int speechIndex)
            => key == null ? -1 : _rows.FindIndex(row => row.Line.Key == key && row.SpeechIndex == speechIndex);

        /// <summary>
        /// 대본의 앞 줄(-1)이나 다음 줄(1)로 넘어간다. 넘어갔으면 true. 펼친 대사도 한 줄로 쳐서, 대화는 대사마다 말풍선이 바뀐다.
        /// 진행 순서대로 보이게, 순서에서 벗어나는 줄은 건너뛴다
        /// </summary>
        private bool StepRow(int direction)
        {
            int index = FindRowIndex(_lineKey, _speechIndex) + direction;
            while (index >= 0 && index < _rows.Count && IsSkippedOnStep(index))
                index += direction;
            if (index < 0 || index >= _rows.Count) return false;

            Select(_rows[index].Line, _rows[index].SpeechIndex, true);
            return true;
        }

        private bool IsSkippedOnStep(int index)
        {
            var row = _rows[index];
            //대화 줄을 골랐을 때 이미 첫 대사를 띄우고 있어서 같은 모습이 두 번 나온다
            if (row.SpeechIndex == 0) return true;

            //시나리오를 부르는 줄은 그 시나리오가 끝난 모습을 보여준다. 아래에 펼친 줄보다 먼저 나오면 끝난 모습에서 처음으로 되돌아가 보인다
            bool hasChildren = index + 1 < _rows.Count && _rows[index + 1].Indent > row.Indent;
            return !row.IsSpeech && hasChildren && row.Line.Command == StoryCommandCatalog.CommandScenario;
        }

        private void SetPlaying(bool playing)
        {
            if (_isPlaying == playing) return;

            _isPlaying = playing;
            _playToggle?.SetValueWithoutNotify(playing);
            if (playing)
            {
                _nextStepTime = EditorApplication.timeSinceStartup + PlayInterval;
                EditorApplication.update += HandlePlayUpdate;
            }
            else
                EditorApplication.update -= HandlePlayUpdate;
        }

        private string FindError(StoryLine line)
        {
            foreach (var error in _errors)
            {
                if (error.LineKey == line.Key) return error.Message;
            }
            return null;
        }

        private StoryReplay GetReplay()
        {
            if (_replay == null || _replayVersion != _document.Version)
            {
                _replay = new StoryReplay(_document, _outline, _doc, _line, Mathf.Max(_speechIndex, 0));
                _replayVersion = _document.Version;
            }
            return _replay;
        }
        #endregion

        #region Private - Edit
        //새 퀘스트나 연출을 만들고 연다. 챕터는 지금 열어 둔 묶음의 챕터다
        private void AddDoc(StoryLineKind kind)
        {
            string key = _editor.AddDoc(_outline, kind, GetCurrentChapter());
            if (key == null) return;

            _docKey = key;
            _lineKey = null;
            RefreshStructure();
        }

        private long GetCurrentChapter()
        {
            if (_doc != null) return _doc.Chapter;
            return _outline.Chapters.Count > 0 ? _outline.Chapters[0] : FirstChapter;
        }

        //고른 줄 다음에 새 줄을 끼우고 그 줄을 고른다. 대상 묶음에서는 새 대상을, 일반 대화 묶음에서는 새 일반 대화를 만든다
        private void AddLine()
        {
            if (_doc == null) return;

            string key;
            if (_doc.Kind == StoryLineKind.Talk)
                key = _editor.AddTalk(_outline, _doc.Chapter);
            else if (_doc.Kind == StoryLineKind.Actor)
            {
                long chapter = _doc.Chapter;
                //고른 대상의 옆에 세운다. 같은 바닥 위라서 배치 화면을 열지 않고 만들어도 공중에 뜨지 않는다
                if (!TryGetDefaultPosition(_line?.Actor, out long zoneId, out var local)) return;
                if (_line != null)
                    local.x += NewActorOffset;
                key = _editor.AddActor(_outline, chapter, zoneId, local);
            }
            else
            {
                //퀘스트를 열어 둔 채로 그 아래에 펼친 시나리오의 줄을 골랐으면 그 시나리오에 끼운다
                var doc = FindOwnDoc(_line);
                if (doc == null) return;
                key = _editor.InsertLine(doc, _line);
            }

            if (key != null)
                SelectKey(key);
        }

        //고른 줄을 지운다. 펼친 대사를 골랐으면 그 대사만 지운다. 지운 뒤에는 그 자리에 올라온 줄을 고른다
        private void RemoveSelected()
        {
            if (_line == null) return;

            int index = FindRowIndex(_lineKey, _speechIndex);
            if (_speechIndex != NoSpeech)
            {
                if (index >= 0)
                    _editor.RemoveSpeech(_rows[index].Speech);
                return;
            }

            var doc = FindOwnDoc(_line);
            if (doc == null) return;
            _editor.RemoveLine(doc, _line);

            //지운 줄은 키로 찾을 수 없어서 첫 줄이 골라져 있다. 지운 자리로 다시 맞춘다
            if (_rows.Count == 0) return;
            index = Mathf.Clamp(index, 0, _rows.Count - 1);
            Select(_rows[index].Line, _rows[index].SpeechIndex, true);
        }

        private void MoveSelected(int direction)
        {
            //대사의 순서는 Id가 정하고, 그 Id를 다른 줄이 가리킨다. 옮기려면 가리키는 줄까지 고쳐야 해서 여기서는 옮기지 않는다
            if (_line == null || _speechIndex != NoSpeech) return;
            //대상과 일반 대화는 줄마다 따로라서 순서에 뜻이 없다
            if (_line.Kind == StoryLineKind.Actor || _line.Kind == StoryLineKind.Talk) return;

            var doc = FindOwnDoc(_line);
            if (doc == null) return;

            string key = _editor.MoveLine(doc, _line, direction);
            if (key != null)
                SelectKey(key);
        }

        //줄이 실제로 속한 묶음. 퀘스트를 열어 두고 그 아래에 펼친 시나리오의 줄을 골랐으면 그 시나리오다
        private StoryDoc FindOwnDoc(StoryLine line)
        {
            if (line == null) return null;
            //대상과 일반 대화는 Id에 든 챕터로 묶는다
            if (line.Kind == StoryLineKind.Actor) return _outline.FindChapterDoc(line.Kind, line.Id / StoryActorChapterRange);
            if (line.Kind == StoryLineKind.Talk) return _outline.FindChapterDoc(line.Kind, line.Id / StoryChapterRange);

            //번호 칸의 이름은 표 이름과 같다 (Quest 표의 Quest, Scenario 표의 Scenario)
            return _outline.Find(line.Kind, line.Get(line.TableName));
        }

        /// <summary>
        /// 새로 놓을 자리를 정한다 (StoryEditor가 위치가 필요한 줄을 채울 때와 새 대상을 만들 때 쓴다).
        /// 그 대상이 서 있으면 그 자리다. "제자리에서 시작해 끌어 옮기는" 흐름이 되게 하려는 것이다.
        /// 서 있지 않으면 배치 화면에 보이는 곳의 바닥, 배치 화면도 닫혀 있으면 첫 Zone의 가운데다. 대상이 없는 줄(Reach)은 플레이어를 기준으로 한다
        /// </summary>
        private bool TryGetDefaultPosition(string actor, out long zoneId, out Vector2 local)
        {
            if (actor == string.Empty)
                actor = StoryActor.PlayerName;

            Vector2 world;
            var sceneView = SceneView.lastActiveSceneView;
            if (actor != null && _doc != null && GetReplay().Standing.TryGetValue(actor, out var standing))
                world = standing;
            else if (IsStageOpen && sceneView != null)
            {
                world = sceneView.pivot;
                if (_stage.TryFindGround(world, GroundSearchRange, out float groundY))
                    world.y = groundY;
            }
            else
                world = _document.Zones.Count > 0 ? _document.Zones[0].Rect.center : Vector2.zero;
            return TryToLocal(world, 0, out zoneId, out local);
        }

        /// <summary>
        /// 월드 위치를 표에 적는 모양(Zone과 그 Zone 안의 위치)으로 바꾼다. 그 자리가 들어 있는 Zone을 쓰고,
        /// 어느 Zone에도 들어 있지 않으면 fallbackZone(0이면 첫 Zone)을 기준으로 적는다. Zone이 하나도 없으면 false
        /// </summary>
        private bool TryToLocal(Vector2 world, long fallbackZone, out long zoneId, out Vector2 local)
        {
            zoneId = fallbackZone;
            local = Vector2.zero;
            if (!_document.TryFindZoneAt(world, out var zone) && !_document.ZoneLayout.TryGetZone(fallbackZone, out zone))
            {
                if (_document.Zones.Count == 0) return false;
                zone = _document.Zones[0];
            }

            zoneId = zone.Id;
            local = world - zone.Rect.position;
            return true;
        }

        private void Save()
        {
            //툴에서 고친 표를 밖에서도 고쳤으면, 저장하는 순간 밖에서 고친 내용이 사라진다
            if (_document.HasChangedOnDisk(true) && !EditorUtility.DisplayDialog(WindowTitle,
                    "고친 표가 툴 밖에서도 바뀌었습니다. 저장하면 밖에서 바꾼 내용을 덮어씁니다.", "덮어쓰기", "취소"))
                return;

            _document.Save();
            //저장한 뒤에 Undo로 되돌리면 표와 파일이 어긋난 채로 '저장됨'으로 보인다
            Undo.ClearUndo(_document);

            if (EditorApplication.isPlaying)
                _status = "표(tsv)에 저장했다. Play 중이라 Update Table은 돌리지 않았다. 멈춘 뒤에 Table > Update Table을 실행한다.";
            else if (StoryToolValidation.Run(_errors))
                _status = "저장하고 검사를 통과했다.";
            else
                _status = $"저장했지만 검사에 걸렸다. 대본에 표시한 줄 {_errors.Count}개, 자세한 내용은 콘솔에서 확인한다.";

            _rowList?.RefreshItems();
            RefreshInspector();
            RefreshStatus();
        }

        private void ReloadWithConfirm()
        {
            if (_document.IsDirty && !EditorUtility.DisplayDialog(WindowTitle,
                    "저장하지 않은 변경을 버리고 표를 다시 읽습니다.", "다시 읽기", "취소"))
                return;
            Reload();
        }

        private void Reload()
        {
            Undo.ClearUndo(_document);
            _document.Load();
            _errors.Clear();
            _status = "표를 다시 읽었다.";
            if (_docTree != null)
                RefreshStructure();
        }
        #endregion

        #region Private - Stage
        private void ToggleStage()
        {
            if (IsStageOpen)
                CloseStage();
            else
                OpenStage();
        }

        private void OpenStage()
        {
            if (IsStageOpen || EditorApplication.isPlayingOrWillChangePlaymode) return;

            _stage = StoryToolStage.Open();
            RefreshStageButton();
            RefreshInspector();
            RefreshStage();
            FrameSelection(true);
        }

        private void ReopenStage()
        {
            EditorApplication.update -= ReopenStage;
            OpenStage();
        }

        private void CloseStage()
        {
            if (_stage == null) return;

            //닫히면 HandleStageClosed가 불려서 나머지를 정리한다
            if (_stage.IsCurrent)
                StageUtility.GoToMainStage();
            _stage = null;
            RefreshStageButton();
        }

        /// <summary>
        /// 배치 화면을 고른 줄의 모습에 맞춘다. Zone은 필요한 것만 깐다:
        /// 고른 자리의 Zone과 둘레(게임에서 같이 올라와 있는 범위), 대본에 보이는 줄이 가리키는 Zone, 대상이 서 있는 Zone
        /// </summary>
        private void RefreshStage()
        {
            if (!IsStageOpen || _doc == null) return;

            var replay = GetReplay();
            _zoneBuffer.Clear();
            foreach (var line in _visibleLines)
            {
                if (replay.TryGetMarker(line, out var marker) && _document.TryFindZoneAt(marker.World, out var zone))
                    _zoneBuffer.Add(zone);
            }
            foreach (var pair in replay.Standing)
            {
                if (_document.TryFindZoneAt(pair.Value, out var zone))
                    _zoneBuffer.Add(zone);
            }
            if (TryGetSelectedWorld(out var selected) && _document.TryFindZoneAt(selected, out var center))
            {
                _document.ZoneLayout.GetNeighbors(center, ZoneCullDistance - 1, _neighborBuffer);
                foreach (var zone in _neighborBuffer)
                    _zoneBuffer.Add(zone);
            }
            //아무도 없고 가리키는 곳도 없는 줄이면(이야기의 첫 줄 등) 빈 화면 대신 첫 Zone이라도 보여준다
            if (_zoneBuffer.Count == 0 && _document.Zones.Count > 0)
                _zoneBuffer.Add(_document.Zones[0]);
            _stage.ShowZones(_zoneBuffer);

            _actorBuffer.Clear();
            int order = 0;
            foreach (var pair in replay.Standing)
            {
                long objectId = _document.GetActorObjectId(pair.Key);
                var data = _document.GetObject(objectId);
                //Object 표의 Tint가 빈칸(0,0,0)이면 원래 색이다 (AnimationPlayer와 같다)
                var color = data != null && data.Tint != Vector3.zero ? new Color(data.Tint.x, data.Tint.y, data.Tint.z) : Color.white;
                int sortingOrder = StoryToolDocument.IsPlayer(pair.Key) ? SortingOrderPlayer : SortingOrderCharacter + order++;
                _actorBuffer.Add(new StoryToolStage.ActorPreview(pair.Key, objectId, color, pair.Value, sortingOrder));
            }
            _stage.ShowActors(_actorBuffer);
            SceneView.RepaintAll();
        }

        //고른 줄이 가리키는 곳. 위치를 쓰지 않는 줄이면 그 줄의 대상이 서 있는 곳이다
        private bool TryGetSelectedWorld(out Vector2 world)
        {
            world = Vector2.zero;
            if (_line == null || _doc == null) return false;

            var replay = GetReplay();
            if (replay.TryGetMarker(_line, out var marker))
            {
                world = marker.World;
                return true;
            }
            return replay.Standing.TryGetValue(_line.Actor, out world);
        }

        /// <summary>Scene 뷰를 고른 자리로 옮긴다. always가 아니면 그 자리가 화면 밖일 때만 옮긴다 (줄을 고를 때마다 화면이 움직이면 보던 곳을 놓친다)</summary>
        private void FrameSelection(bool always)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (!IsStageOpen || sceneView == null) return;

            Vector2 center;
            if (TryGetSelectedWorld(out var world))
            {
                if (!always)
                {
                    var viewport = sceneView.camera.WorldToViewportPoint(world);
                    if (viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f) return;
                }
                //위치는 발밑이라 그대로 가운데에 두면 화면의 아래 절반이 땅속이다
                center = world + new Vector2(0f, FrameHeight);
            }
            else
            {
                //가리키는 곳이 없으면 줄을 고를 때는 그대로 두고, 직접 눌렀을 때만 깔아 둔 Zone을 보여준다
                if (!always || _zoneBuffer.Count == 0) return;
                center = Vector2.zero;
                foreach (var zone in _zoneBuffer)
                {
                    center = zone.Rect.center;
                    break;
                }
            }

            sceneView.Frame(new Bounds(center, FrameSize), false);
        }
        #endregion
    }
}
