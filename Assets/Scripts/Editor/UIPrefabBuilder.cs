using System;
using Common.UI;
using InGame.UI;
using Title.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Common.GameDefine;

/// <summary>
/// UI 프리팹을 코드로 조립해서 저장한다 (로딩 화면, 타이틀 화면, 말풍선, 화면 페이드, 목표와 대화 아이콘, 퀘스트 안내).
/// 레이아웃을 바꿀 때는 프리팹을 손으로 고치지 않고 여기를 고친 뒤 메뉴를 다시 실행한다.
/// 저장 위치(AssetPathUI)가 Addressables에 폴더째 등록된 곳이라 따로 등록하지 않아도 주소로 로드된다
/// </summary>
public static class UIPrefabBuilder
{
    #region Fields
    //기준 해상도. 화면 크기가 달라도 이 비율로 늘이고 줄인다
    private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

    //말풍선은 말하는 캐릭터의 머리 위에 뜨고, 크기는 안의 글자에 맞춰 늘어난다. 아래 길이는 모두 캔버스 단위(기준 해상도의 픽셀)다.
    //바탕은 불투명해야 한다. 꼬리가 말풍선과 겹쳐 있어서 반투명이면 겹친 곳만 두 번 칠해져 진하게 보인다
    private static readonly Color BubbleColor = new(0.98f, 0.98f, 0.96f, 1f);
    //Unity에 들어 있는 둥근 모서리 스프라이트. 따로 에셋을 만들지 않고 쓴다
    private const string BubbleSpritePath = "UI/Skin/Background.psd";
    //스프라이트의 모서리가 작아서 두 배로 키워 쓴다 (값이 작을수록 모서리가 커진다)
    private const float BubbleCornerScale = 0.5f;
    private const int BubblePaddingX = 28;
    private const int BubblePaddingY = 16;
    private const float BubbleSpacing = 4f;
    //화면 끝에서 떨어져 있어야 하는 간격
    private const float BubbleScreenMargin = 24f;

    //꼬리는 정사각형을 45도 돌려서 말풍선 아랫변에 반쯤 걸쳐 둔다. 아래로 나온 반쪽이 삼각형으로 보인다
    private const float TailSize = 26f;
    //머리 위에서 말풍선 아랫변까지. 꼬리가 아래로 나온 길이(대각선의 절반)보다 조금 길게 둬서 꼬리 끝이 머리에 닿지 않게 한다
    private const float HeadGap = 30f;
    //꼬리가 말풍선 양 끝에서 떨어져 있어야 하는 간격. 둥근 모서리 밖으로 나가지 않게 한다
    private const float TailInset = 40f;

    private const float NameFontSize = 24f;
    private static readonly Color NameColor = new(0.72f, 0.4f, 0.08f);

    private const float LineFontSize = 32f;
    private static readonly Color LineColor = new(0.12f, 0.12f, 0.14f);
    //대사 한 줄의 최대 폭. 넘으면 줄을 바꾼다
    private const float LineMaxWidth = 560f;

    private const string NextHintText = "[F] 다음";
    private const float NextHintFontSize = 20f;
    private static readonly Color NextHintColor = new(0f, 0f, 0f, 0.45f);

    //목표와 대화 아이콘. 그림이 없어서 원형 바탕에 글자 하나를 올린 임시 아이콘이다. 그림이 생기면 CreateMarkerIcon만 바꾼다.
    //Unity에 들어 있는 원 스프라이트를 쓴다
    private const string MarkerSpritePath = "UI/Skin/Knob.psd";
    //어두운 테두리를 둘러서 밝은 배경 위에서도 아이콘이 묻히지 않게 한다
    private static readonly Color MarkerRimColor = new(0.1f, 0.1f, 0.12f, 1f);
    private const float MarkerRimWidth = 4f;
    //가리키는 곳(머리 위)에서 아이콘 아랫변까지
    private const float MarkerGap = 14f;
    private const float MarkerScreenMargin = 24f;
    //퀘스트 : 노란 바탕에 느낌표
    private const float QuestMarkerSize = 56f;
    private const string QuestMarkerGlyph = "!";
    private const float QuestMarkerGlyphSize = 40f;
    private static readonly Color QuestMarkerColor = new(1f, 0.8f, 0.15f);
    private static readonly Color QuestMarkerGlyphColor = new(0.2f, 0.12f, 0f);
    //시나리오 : 보라 바탕에 재생 표시. 채우면 연출이 이어지는 목표다
    private const float ScenarioMarkerSize = 56f;
    private const string ScenarioMarkerGlyph = "▶";
    private const float ScenarioMarkerGlyphSize = 26f;
    private static readonly Color ScenarioMarkerColor = new(0.55f, 0.4f, 0.95f);
    private static readonly Color ScenarioMarkerGlyphColor = Color.white;
    //대화 : 흰 바탕에 말줄임표. 가까이 가야 뜨는 가벼운 표시라 조금 작게 둔다
    private const float TalkMarkerSize = 44f;
    private const string TalkMarkerGlyph = "…";
    private const float TalkMarkerGlyphSize = 30f;
    private static readonly Color TalkMarkerColor = new(0.98f, 0.98f, 0.96f);
    private static readonly Color TalkMarkerGlyphColor = new(0.12f, 0.12f, 0.14f);

    //퀘스트 안내는 화면 왼쪽 위에 둔다. 폭은 고정이고, 글이 길면 그 안에서 줄을 바꾸며 아래로 늘어난다.
    //게임 화면을 가리는 자리라서 바탕을 반투명하게 둔다
    private const float QuestGuideWidth = 440f;
    private static readonly Vector2 QuestGuideMargin = new(40f, 40f);
    private static readonly Color QuestGuideColor = new(0f, 0f, 0f, 0.6f);
    private const int QuestGuidePaddingX = 22;
    private const int QuestGuidePaddingY = 16;
    private const float QuestGuideSpacing = 8f;
    private const float QuestTitleFontSize = 28f;
    private static readonly Color QuestTitleColor = new(1f, 0.85f, 0.4f);
    private const float QuestGoalFontSize = 26f;

    //타이틀 화면. 화면 전체를 덮는 바탕 위에 게임 이름을 두고 그 아래에 메뉴를 세로로 쌓는다.
    //타이틀 씬에는 캔버스가 이것 하나라서 순서를 따질 것이 없다
    private const int TitleSortingOrder = 0;
    private static readonly Color TitleBackgroundColor = new(0.05f, 0.06f, 0.09f, 1f);
    //게임 이름이 정해지기 전이라 임시로 둔 글이다
    private const string TitleText = "테스트";
    private const float TitleFontSize = 140f;
    private static readonly Color TitleColor = new(1f, 0.85f, 0.4f);
    //게임 이름이 놓이는 높이와 메뉴의 윗변이 놓이는 높이 (화면 아래가 0, 위가 1)
    private const float TitleAnchorY = 0.68f;
    private const float TitleMenuAnchorY = 0.44f;
    private const float TitleHeight = 200f;
    //메뉴는 바탕 없이 글자만 둔다. 글자가 놓인 칸 전체가 클릭을 받는다
    private static readonly Vector2 TitleButtonSize = new(420f, 76f);
    private const float TitleButtonSpacing = 12f;
    private const float TitleButtonFontSize = 44f;
    private const string TitleStartText = "시작";
    private const string TitleContinueText = "이어하기";
    private const string TitleQuitText = "종료";
    //고른 메뉴는 글자 색으로 알린다. 마우스를 올린 것과 키보드로 고른 것을 같은 색으로 둔다
    private static readonly Color TitleButtonColor = new(0.82f, 0.84f, 0.88f, 1f);
    private static readonly Color TitleButtonSelectedColor = new(1f, 0.85f, 0.4f, 1f);
    private static readonly Color TitleButtonPressedColor = new(0.8f, 0.62f, 0.2f, 1f);
    private static readonly Color TitleButtonDisabledColor = new(0.82f, 0.84f, 0.88f, 0.3f);

    //로딩 화면. 화면 전체를 불투명하게 덮고 오른쪽 아래에 글을 둔다.
    //타이틀과 같은 바탕색을 써서 타이틀에서 넘어갈 때 화면이 튀지 않게 한다
    private static readonly Color LoadingBackgroundColor = TitleBackgroundColor;
    private const string LoadingText = "로딩 중";
    //글 끝에 붙이는 점의 수. LoadingView가 하나씩 보였다가 숨긴다
    private const int LoadingDotCount = 3;
    private const float LoadingFontSize = 40f;
    private static readonly Color LoadingTextColor = new(0.82f, 0.84f, 0.88f, 1f);
    //화면 오른쪽 아래 끝에서 떨어진 간격과 글이 놓이는 칸의 크기
    private static readonly Vector2 LoadingTextMargin = new(70f, 56f);
    private static readonly Vector2 LoadingTextSize = new(400f, 60f);

    //페이드는 검은색으로 덮는다. 투명도는 ScreenFadeView가 바꾸므로 색만 정한다
    private static readonly Color FadeColor = Color.black;

    //TMP 기본 폰트에는 한글이 없으므로 나눔고딕(OFL)으로 만든 폰트 에셋을 쓴다.
    //Dynamic이라 쓰는 글자가 나올 때마다 아틀라스에 채우므로 대사가 바뀌어도 다시 만들지 않아도 된다
    private const string KoreanFontPath = "Assets/Art/Font/NanumGothic/NanumGothic-Regular.ttf";
    private const string KoreanFontAssetPath = "Assets/Art/Font/NanumGothic/NanumGothic-Regular SDF.asset";

    //지금 조립하는 프리팹이 들어가는 보이지 않는 씬. Build가 열고 닫는다
    private static Scene _buildScene;
    #endregion

    #region Public
    [MenuItem("UI/Build All UI")]
    public static void BuildAll()
    {
        BuildLoadingView();
        BuildTitleView();
        BuildDialogueView();
        BuildScreenFadeView();
        BuildMarkerView();
        BuildQuestView();
    }

    [MenuItem("UI/Build Loading View")]
    public static void BuildLoadingView()
    {
        if (TryGetFontAsset(out var fontAsset))
            Build(UILoadingView, () => CreateLoadingView(fontAsset));
    }

    [MenuItem("UI/Build Title View")]
    public static void BuildTitleView()
    {
        if (TryGetFontAsset(out var fontAsset))
            Build(UITitleView, () => CreateTitleView(fontAsset));
    }

    [MenuItem("UI/Build Quest View")]
    public static void BuildQuestView()
    {
        if (TryGetFontAsset(out var fontAsset))
            Build(UIQuestView, () => CreateQuestView(fontAsset));
    }

    [MenuItem("UI/Build Dialogue View")]
    public static void BuildDialogueView()
    {
        if (TryGetFontAsset(out var fontAsset))
            Build(UIDialogueView, () => CreateDialogueView(fontAsset));
    }

    [MenuItem("UI/Build Screen Fade View")]
    public static void BuildScreenFadeView() => Build(UIScreenFadeView, CreateScreenFadeView);

    [MenuItem("UI/Build Marker View")]
    public static void BuildMarkerView()
    {
        if (TryGetFontAsset(out var fontAsset))
            Build(UIMarkerView, () => CreateMarkerView(fontAsset));
    }
    #endregion

    #region Private - View
    /// <summary>
    /// 캔버스 → 화면 전체를 덮는 바탕, 오른쪽 아래의 글 순으로 조립하고 LoadingView에 이어준다.
    /// 걷힐 때 바탕과 글이 같이 옅어지도록 캔버스에 CanvasGroup을 붙인다
    /// </summary>
    private static GameObject CreateLoadingView(TMP_FontAsset fontAsset)
    {
        var root = CreateCanvas(UILoadingView, SortingOrderLoading);
        var group = root.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        var background = CreateChild("Background", root.transform);
        SetRect(background, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;
        var backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = LoadingBackgroundColor;
        backgroundImage.raycastTarget = false;

        //오른쪽으로 맞춰서 점이 늘어나도 앞의 글이 움직이지 않게 한다
        var text = CreateText("Text", root.transform, fontAsset, LoadingFontSize, LoadingTextColor, TextAlignmentOptions.BottomRight);
        SetRect(text.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
        text.rectTransform.anchoredPosition = new Vector2(-LoadingTextMargin.x, LoadingTextMargin.y);
        text.rectTransform.sizeDelta = LoadingTextSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.text = LoadingText + new string('.', LoadingDotCount);

        var serializedView = new SerializedObject(root.AddComponent<LoadingView>());
        serializedView.FindProperty("_group").objectReferenceValue = group;
        serializedView.FindProperty("_text").objectReferenceValue = text;
        serializedView.FindProperty("_dotCount").intValue = LoadingDotCount;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    /// <summary>
    /// 캔버스 → 바탕, 게임 이름, 메뉴(시작, 이어하기, 종료), EventSystem 순으로 조립하고 TitleView에 이어준다
    /// </summary>
    private static GameObject CreateTitleView(TMP_FontAsset fontAsset)
    {
        var root = CreateCanvas(UITitleView, TitleSortingOrder);
        //메뉴를 마우스로도 고르므로 클릭을 받는다
        root.AddComponent<GraphicRaycaster>();

        var background = CreateChild("Background", root.transform);
        SetRect(background, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;
        var backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = TitleBackgroundColor;
        backgroundImage.raycastTarget = false;

        //화면 폭 전체를 쓰고 그 안에서 가운데로 맞춘다
        var title = CreateText("Title", root.transform, fontAsset, TitleFontSize, TitleColor, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0f, TitleAnchorY), new Vector2(1f, TitleAnchorY), new Vector2(0.5f, 0.5f));
        title.rectTransform.anchoredPosition = Vector2.zero;
        title.rectTransform.sizeDelta = new Vector2(0f, TitleHeight);
        title.fontStyle = FontStyles.Bold;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.text = TitleText;

        var menu = CreateChild("Menu", root.transform);
        SetRect(menu, new Vector2(0.5f, TitleMenuAnchorY), new Vector2(0.5f, TitleMenuAnchorY), new Vector2(0.5f, 1f));
        menu.anchoredPosition = Vector2.zero;
        menu.sizeDelta = new Vector2(TitleButtonSize.x, 0f);
        //버튼은 정해 둔 크기 그대로 위에서부터 쌓는다
        var layout = menu.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = TitleButtonSpacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var fitter = menu.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var startButton = CreateTitleButton("Start", menu, fontAsset, TitleStartText);
        //이어하기는 저장 기능이 생길 때까지 고를 수 없게 둔다. 고를 수 없는 버튼은 키보드로 오르내릴 때도 건너뛴다
        CreateTitleButton("Continue", menu, fontAsset, TitleContinueText).interactable = false;
        var quitButton = CreateTitleButton("Quit", menu, fontAsset, TitleQuitText);

        //버튼이 키보드와 마우스 입력을 받으려면 EventSystem이 있어야 한다. 타이틀 씬에 따로 두지 않고 화면과 같이 만든다.
        //입력 액션을 비워 두면 입력 모듈이 켜질 때 기본 UI 액션(방향키/WASD, Enter, 마우스)을 쓴다
        var eventSystem = new GameObject("EventSystem");
        eventSystem.transform.SetParent(root.transform, false);
        eventSystem.AddComponent<EventSystem>();
        //빈 곳을 클릭해도 선택을 풀지 않는다. 선택이 풀리면 키보드로 메뉴를 고를 수 없게 된다
        eventSystem.AddComponent<InputSystemUIInputModule>().deselectOnBackgroundClick = false;

        var serializedView = new SerializedObject(root.AddComponent<TitleView>());
        serializedView.FindProperty("_startButton").objectReferenceValue = startButton;
        serializedView.FindProperty("_quitButton").objectReferenceValue = quitButton;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    /// <summary>
    /// 타이틀 메뉴 하나. 글자가 곧 버튼이고, 상태(평소, 고름, 누름, 고를 수 없음)에 따라 글자 색을 바꾼다.
    /// 버튼의 색은 글자 색에 곱해지므로 글자는 흰색으로 둔다
    /// </summary>
    private static Button CreateTitleButton(string name, Transform parent, TMP_FontAsset fontAsset, string label)
    {
        var text = CreateText(name, parent, fontAsset, TitleButtonFontSize, Color.white, TextAlignmentOptions.Center);
        text.rectTransform.sizeDelta = TitleButtonSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.text = label;
        text.raycastTarget = true;

        var button = text.gameObject.AddComponent<Button>();
        button.targetGraphic = text;
        var colors = button.colors;
        colors.normalColor = TitleButtonColor;
        colors.highlightedColor = TitleButtonSelectedColor;
        colors.selectedColor = TitleButtonSelectedColor;
        colors.pressedColor = TitleButtonPressedColor;
        colors.disabledColor = TitleButtonDisabledColor;
        button.colors = colors;

        var serializedHover = new SerializedObject(text.gameObject.AddComponent<HoverSelector>());
        serializedHover.FindProperty("_selectable").objectReferenceValue = button;
        serializedHover.ApplyModifiedPropertiesWithoutUndo();

        return button;
    }

    /// <summary>
    /// 캔버스 → 말풍선 → 꼬리, 이름, 대사, 넘기기 표시 순으로 조립하고 DialogueView에 이어준다.
    /// 말풍선은 세로 레이아웃으로 안의 글자를 쌓고 그 크기에 맞춰 늘어난다. 위치는 DialogueView가 매 프레임 정한다
    /// </summary>
    private static GameObject CreateDialogueView(TMP_FontAsset fontAsset)
    {
        var root = CreateCanvas(UIDialogueView, SortingOrderDialogue);

        //캔버스의 왼쪽 아래를 기준으로 두고 아랫변 가운데를 축으로 삼는다. DialogueView가 화면 위치를 그대로 넣을 수 있다
        var bubble = CreateChild("Bubble", root.transform);
        SetRect(bubble, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f));
        var bubbleImage = bubble.gameObject.AddComponent<Image>();
        bubbleImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BubbleSpritePath);
        bubbleImage.type = Image.Type.Sliced;
        bubbleImage.pixelsPerUnitMultiplier = BubbleCornerScale;
        bubbleImage.color = BubbleColor;
        bubbleImage.raycastTarget = false;

        var layout = bubble.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(BubblePaddingX, BubblePaddingX, BubblePaddingY, BubblePaddingY);
        layout.spacing = BubbleSpacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        //폭은 가장 넓은 줄에 맞추고 나머지 줄도 그 폭으로 늘린다. 그래야 넘기기 표시를 오른쪽 끝에 붙일 수 있다
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = bubble.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        //꼬리는 글자보다 먼저 만들어서 글자 뒤에 그려지게 하고, 레이아웃이 줄로 세지 않게 뺀다
        var tail = CreateChild("Tail", bubble);
        SetRect(tail, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
        tail.sizeDelta = new Vector2(TailSize, TailSize);
        tail.anchoredPosition = Vector2.zero;
        tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tail.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var tailImage = tail.gameObject.AddComponent<Image>();
        tailImage.color = BubbleColor;
        tailImage.raycastTarget = false;

        var speakerName = CreateText("Name", bubble, fontAsset, NameFontSize, NameColor, TextAlignmentOptions.TopLeft);
        speakerName.fontStyle = FontStyles.Bold;
        speakerName.textWrappingMode = TextWrappingModes.NoWrap;

        //대사의 폭은 DialogueView가 대사마다 LayoutElement에 넣는다 (짧으면 글자 폭, 길면 최대 폭)
        var line = CreateText("Line", bubble, fontAsset, LineFontSize, LineColor, TextAlignmentOptions.TopLeft);
        line.textWrappingMode = TextWrappingModes.Normal;
        var lineLayout = line.gameObject.AddComponent<LayoutElement>();

        var nextHint = CreateText("NextHint", bubble, fontAsset, NextHintFontSize, NextHintColor, TextAlignmentOptions.TopRight);
        nextHint.textWrappingMode = TextWrappingModes.NoWrap;
        nextHint.text = NextHintText;

        //SerializeField는 private이라 SerializedObject로 넣는다. Undo 기록은 남기지 않는다
        var serializedView = new SerializedObject(root.AddComponent<DialogueView>());
        serializedView.FindProperty("_bubble").objectReferenceValue = bubble;
        serializedView.FindProperty("_tail").objectReferenceValue = tail;
        serializedView.FindProperty("_nameText").objectReferenceValue = speakerName;
        serializedView.FindProperty("_lineText").objectReferenceValue = line;
        serializedView.FindProperty("_lineLayout").objectReferenceValue = lineLayout;
        serializedView.FindProperty("_nextHint").objectReferenceValue = nextHint;
        serializedView.FindProperty("_maxLineWidth").floatValue = LineMaxWidth;
        serializedView.FindProperty("_headGap").floatValue = HeadGap;
        serializedView.FindProperty("_screenMargin").floatValue = BubbleScreenMargin;
        serializedView.FindProperty("_tailInset").floatValue = TailInset;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    /// <summary>
    /// 캔버스 → 패널 → 제목, 할 일 순으로 조립하고 QuestView에 이어준다.
    /// 패널은 화면 왼쪽 위에 붙이고 폭을 고정한다. 높이만 글의 줄 수에 맞춰 늘어난다
    /// </summary>
    private static GameObject CreateQuestView(TMP_FontAsset fontAsset)
    {
        var root = CreateCanvas(UIQuestView, SortingOrderHud);

        var panel = CreateChild("Panel", root.transform);
        SetRect(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        panel.anchoredPosition = new Vector2(QuestGuideMargin.x, -QuestGuideMargin.y);
        panel.sizeDelta = new Vector2(QuestGuideWidth, 0f);
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BubbleSpritePath);
        panelImage.type = Image.Type.Sliced;
        panelImage.color = QuestGuideColor;
        panelImage.raycastTarget = false;

        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(QuestGuidePaddingX, QuestGuidePaddingX, QuestGuidePaddingY, QuestGuidePaddingY);
        layout.spacing = QuestGuideSpacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        //폭은 위에서 정한 값을 그대로 두고 높이만 내용에 맞춘다
        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = CreateText("Title", panel, fontAsset, QuestTitleFontSize, QuestTitleColor, TextAlignmentOptions.TopLeft);
        title.fontStyle = FontStyles.Bold;
        title.textWrappingMode = TextWrappingModes.Normal;

        var goal = CreateText("Goal", panel, fontAsset, QuestGoalFontSize, Color.white, TextAlignmentOptions.TopLeft);
        goal.textWrappingMode = TextWrappingModes.Normal;

        var serializedView = new SerializedObject(root.AddComponent<QuestView>());
        serializedView.FindProperty("_panel").objectReferenceValue = panel.gameObject;
        serializedView.FindProperty("_titleText").objectReferenceValue = title;
        serializedView.FindProperty("_goalText").objectReferenceValue = goal;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    /// <summary>
    /// 캔버스 → 종류별 아이콘 원본 순으로 조립하고 MarkerView에 이어준다.
    /// 아이콘은 한 화면에 여러 개 뜰 수 있어서 원본만 넣어두고, MarkerView가 필요한 만큼 복사해서 쓴다
    /// </summary>
    private static GameObject CreateMarkerView(TMP_FontAsset fontAsset)
    {
        var root = CreateCanvas(UIMarkerView, SortingOrderHud);

        var quest = CreateMarkerIcon("Quest", root.transform, fontAsset, QuestMarkerSize, QuestMarkerColor,
            QuestMarkerGlyph, QuestMarkerGlyphSize, QuestMarkerGlyphColor);
        var scenario = CreateMarkerIcon("Scenario", root.transform, fontAsset, ScenarioMarkerSize, ScenarioMarkerColor,
            ScenarioMarkerGlyph, ScenarioMarkerGlyphSize, ScenarioMarkerGlyphColor);
        var talk = CreateMarkerIcon("Talk", root.transform, fontAsset, TalkMarkerSize, TalkMarkerColor,
            TalkMarkerGlyph, TalkMarkerGlyphSize, TalkMarkerGlyphColor);

        var serializedView = new SerializedObject(root.AddComponent<MarkerView>());
        serializedView.FindProperty("_questTemplate").objectReferenceValue = quest;
        serializedView.FindProperty("_scenarioTemplate").objectReferenceValue = scenario;
        serializedView.FindProperty("_talkTemplate").objectReferenceValue = talk;
        serializedView.FindProperty("_gap").floatValue = MarkerGap;
        serializedView.FindProperty("_screenMargin").floatValue = MarkerScreenMargin;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    /// <summary>
    /// 아이콘 하나. 어두운 원(테두리) 위에 색을 칠한 원을 조금 작게 올리고 가운데에 글자를 둔다.
    /// 캔버스의 왼쪽 아래를 기준으로 두고 아랫변 가운데를 축으로 삼아서, MarkerView가 화면 위치를 그대로 넣을 수 있게 한다
    /// </summary>
    private static RectTransform CreateMarkerIcon(string name, Transform parent, TMP_FontAsset fontAsset, float size, Color color,
        string glyph, float glyphSize, Color glyphColor)
    {
        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(MarkerSpritePath);

        var icon = CreateChild(name, parent);
        SetRect(icon, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f));
        icon.sizeDelta = new Vector2(size, size);
        var rimImage = icon.gameObject.AddComponent<Image>();
        rimImage.sprite = sprite;
        rimImage.color = MarkerRimColor;
        rimImage.raycastTarget = false;

        var fill = CreateChild("Fill", icon);
        SetRect(fill, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        fill.offsetMin = new Vector2(MarkerRimWidth, MarkerRimWidth);
        fill.offsetMax = new Vector2(-MarkerRimWidth, -MarkerRimWidth);
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = sprite;
        fillImage.color = color;
        fillImage.raycastTarget = false;

        //글자마다 높이가 달라서(느낌표와 말줄임표) 줄이 아니라 글자 모양의 가운데로 맞춘다
        var text = CreateText("Glyph", icon, fontAsset, glyphSize, glyphColor, TextAlignmentOptions.CenterGeoAligned);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.text = glyph;

        return icon;
    }

    /// <summary>캔버스 → 화면 전체를 덮는 검은 이미지 순으로 조립하고 ScreenFadeView에 이어준다</summary>
    private static GameObject CreateScreenFadeView()
    {
        var root = CreateCanvas(UIScreenFadeView, SortingOrderScreenFade);

        var cover = CreateChild("Cover", root.transform);
        SetRect(cover, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        cover.offsetMin = Vector2.zero;
        cover.offsetMax = Vector2.zero;
        var coverImage = cover.gameObject.AddComponent<Image>();
        coverImage.color = FadeColor;
        coverImage.raycastTarget = false;

        var serializedView = new SerializedObject(root.AddComponent<ScreenFadeView>());
        serializedView.FindProperty("_cover").objectReferenceValue = coverImage;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }
    #endregion

    #region Private - Common
    /// <summary>
    /// 열려 있는 씬을 건드리지 않도록 보이지 않는 별도 씬에서 조립하고 AssetPathUI 아래에 이름 그대로 저장한다.
    /// 조립용 씬은 CreateCanvas가 루트를 만들자마자 옮겨 넣는 데 쓴다
    /// </summary>
    private static void Build(string viewName, Func<GameObject> create)
    {
        _buildScene = EditorSceneManager.NewPreviewScene();
        try
        {
            Save(create(), viewName);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(_buildScene);
        }
    }

    /// <summary>화면 위에 그리는 캔버스. 순서가 클수록 위에 그려진다</summary>
    private static GameObject CreateCanvas(string name, int sortingOrder)
    {
        var root = new GameObject(name, typeof(RectTransform));
        //새 오브젝트는 열려 있는 씬에 생기므로 컴포넌트를 붙이기 전에 조립용 씬으로 옮긴다.
        //레이아웃 컴포넌트는 붙는 순간 자기가 있는 씬에 수정 표시를 남겨서, 다 조립한 뒤에 옮기면 열려 있던 씬이 바뀐 것이 없는데도 수정된 것으로 남는다
        SceneManager.MoveGameObjectToScene(root, _buildScene);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        //게임 중의 UI는 클릭을 받지 않으므로 GraphicRaycaster는 붙이지 않는다. 클릭을 받는 화면(타이틀)만 따로 붙인다
        return root;
    }

    /// <summary>글자가 들어가는 프리팹을 만들기 전에 폰트를 준비한다. 준비하지 못하면 에러를 남기고 false</summary>
    private static bool TryGetFontAsset(out TMP_FontAsset fontAsset)
    {
        fontAsset = null;
        //TMP 기본 폰트가 없으면 글자가 보이지 않는 프리팹이 만들어진다.
        //TMP Settings가 없으면 이 확인을 하는 순간 TMP가 가져오기 창을 띄운다
        if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
        {
            Debug.LogError("TMP default font is missing. Import it from Window > TextMeshPro > Import TMP Essential Resources.");
            return false;
        }

        fontAsset = GetOrCreateFontAsset();
        return fontAsset != null;
    }

    /// <summary>
    /// 한글 폰트 에셋을 찾고, 없으면 나눔고딕 원본으로 만든다.
    /// 메뉴(Create > TextMeshPro > Font Asset)로 만든 것과 같도록 아틀라스 텍스처와 머티리얼을 폰트 에셋 안에 같이 저장한다.
    /// 따로 두면 에셋을 다시 열었을 때 아틀라스가 끊긴다
    /// </summary>
    private static TMP_FontAsset GetOrCreateFontAsset()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath);
        if (fontAsset != null) return fontAsset;

        var font = AssetDatabase.LoadAssetAtPath<Font>(KoreanFontPath);
        if (font == null)
        {
            Debug.LogError($"Font {KoreanFontPath} is missing.");
            return null;
        }

        fontAsset = TMP_FontAsset.CreateFontAsset(font);
        if (fontAsset == null)
        {
            Debug.LogError($"Failed to create font asset from {KoreanFontPath}.");
            return null;
        }

        AssetDatabase.CreateAsset(fontAsset, KoreanFontAssetPath);
        fontAsset.atlasTextures[0].name = $"{font.name} Atlas";
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        fontAsset.material.name = $"{font.name} Atlas Material";
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[UI] 한글 폰트 에셋 생성 : {KoreanFontAssetPath}");
        return fontAsset;
    }

    private static RectTransform CreateChild(string name, Transform parent)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return (RectTransform)child.transform;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset fontAsset, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        var text = CreateChild(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = fontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
    }

    private static void Save(GameObject root, string viewName)
    {
        //Addressables 폴더 아래 UI 폴더가 처음이면 만든다
        string folder = AssetPathUI.TrimEnd('/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            int split = folder.LastIndexOf('/');
            AssetDatabase.CreateFolder(folder[..split], folder[(split + 1)..]);
        }

        string path = AssetPathUI + viewName + AssetExtensionPrefab;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        if (!success)
        {
            Debug.LogError($"Failed to save {path}.");
            return;
        }

        Debug.Log($"[UI] 프리팹 저장 : {path}");
        EditorGUIUtility.PingObject(prefab);
    }
    #endregion
}
