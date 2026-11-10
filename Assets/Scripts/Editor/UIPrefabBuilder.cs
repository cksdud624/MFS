using System;
using InGame.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Common.GameDefine;

/// <summary>
/// UI 프리팹을 코드로 조립해서 저장한다 (대화창, 화면 페이드).
/// 레이아웃을 바꿀 때는 프리팹을 손으로 고치지 않고 여기를 고친 뒤 메뉴를 다시 실행한다.
/// 저장 위치(AssetPathUI)가 Addressables에 폴더째 등록된 곳이라 따로 등록하지 않아도 주소로 로드된다
/// </summary>
public static class UIPrefabBuilder
{
    #region Fields
    //기준 해상도. 화면 크기가 달라도 이 비율로 늘이고 줄인다
    private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

    //대화창은 화면 아래쪽에 가로로 꽉 차게 둔다. 화면 가장자리와는 간격을 둔다
    private const float PanelHeight = 260f;
    private const float PanelMargin = 40f;
    private const float Padding = 32f;
    private static readonly Color PanelColor = new(0f, 0f, 0f, 0.75f);

    private const float SpeakerHeight = 48f;
    private const float SpeakerFontSize = 34f;
    private static readonly Color SpeakerColor = new(1f, 0.85f, 0.4f);

    private const float LineFontSize = 40f;

    private const string NextHintText = "[F] 다음";
    private static readonly Vector2 NextHintSize = new(240f, 40f);
    private const float NextHintFontSize = 28f;
    private static readonly Color NextHintColor = new(1f, 1f, 1f, 0.6f);

    //페이드는 검은색으로 덮는다. 투명도는 ScreenFadeView가 바꾸므로 색만 정한다
    private static readonly Color FadeColor = Color.black;

    //TMP 기본 폰트에는 한글이 없으므로 나눔고딕(OFL)으로 만든 폰트 에셋을 쓴다.
    //Dynamic이라 쓰는 글자가 나올 때마다 아틀라스에 채우므로 대사가 바뀌어도 다시 만들지 않아도 된다
    private const string KoreanFontPath = "Assets/Art/Font/NanumGothic/NanumGothic-Regular.ttf";
    private const string KoreanFontAssetPath = "Assets/Art/Font/NanumGothic/NanumGothic-Regular SDF.asset";
    #endregion

    #region Public
    [MenuItem("UI/Build All UI")]
    public static void BuildAll()
    {
        BuildDialogueView();
        BuildScreenFadeView();
    }

    [MenuItem("UI/Build Dialogue View")]
    public static void BuildDialogueView()
    {
        //TMP 기본 폰트가 없으면 글자가 보이지 않는 프리팹이 만들어진다.
        //TMP Settings가 없으면 이 확인을 하는 순간 TMP가 가져오기 창을 띄운다
        if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
        {
            Debug.LogError("TMP default font is missing. Import it from Window > TextMeshPro > Import TMP Essential Resources.");
            return;
        }

        var fontAsset = GetOrCreateFontAsset();
        if (fontAsset == null) return;

        Build(UIDialogueView, () => CreateDialogueView(fontAsset));
    }

    [MenuItem("UI/Build Screen Fade View")]
    public static void BuildScreenFadeView() => Build(UIScreenFadeView, CreateScreenFadeView);
    #endregion

    #region Private - View
    /// <summary>캔버스 → 패널 → 이름, 대사, 넘기기 표시 순으로 조립하고 DialogueView에 이어준다</summary>
    private static GameObject CreateDialogueView(TMP_FontAsset fontAsset)
    {
        var root = CreateCanvas(UIDialogueView, SortingOrderDialogue);

        var panel = CreateChild("Panel", root.transform);
        SetRect(panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        panel.sizeDelta = new Vector2(-PanelMargin * 2f, PanelHeight);
        panel.anchoredPosition = new Vector2(0f, PanelMargin);
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        var speaker = CreateText("Speaker", panel, fontAsset, SpeakerFontSize, SpeakerColor, TextAlignmentOptions.TopLeft);
        speaker.fontStyle = FontStyles.Bold;
        SetRect(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        speaker.rectTransform.sizeDelta = new Vector2(-Padding * 2f, SpeakerHeight);
        speaker.rectTransform.anchoredPosition = new Vector2(Padding, -Padding);

        //대사는 이름 아래부터 패널 안쪽을 채운다
        var line = CreateText("Line", panel, fontAsset, LineFontSize, Color.white, TextAlignmentOptions.TopLeft);
        SetRect(line.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        line.rectTransform.offsetMin = new Vector2(Padding, Padding);
        line.rectTransform.offsetMax = new Vector2(-Padding, -(Padding + SpeakerHeight));

        var nextHint = CreateText("NextHint", panel, fontAsset, NextHintFontSize, NextHintColor, TextAlignmentOptions.BottomRight);
        nextHint.text = NextHintText;
        SetRect(nextHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
        nextHint.rectTransform.sizeDelta = NextHintSize;
        nextHint.rectTransform.anchoredPosition = new Vector2(-Padding, Padding * 0.5f);

        //SerializeField는 private이라 SerializedObject로 넣는다. Undo 기록은 남기지 않는다
        var serializedView = new SerializedObject(root.AddComponent<DialogueView>());
        serializedView.FindProperty("_panel").objectReferenceValue = panel.gameObject;
        serializedView.FindProperty("_speakerText").objectReferenceValue = speaker;
        serializedView.FindProperty("_lineText").objectReferenceValue = line;
        serializedView.FindProperty("_nextHint").objectReferenceValue = nextHint.gameObject;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        return root;
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
    /// 열려 있는 씬을 건드리지 않도록 보이지 않는 별도 씬에서 조립하고 AssetPathUI 아래에 이름 그대로 저장한다
    /// </summary>
    private static void Build(string viewName, Func<GameObject> create)
    {
        var previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = create();
            SceneManager.MoveGameObjectToScene(root, previewScene);
            Save(root, viewName);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    /// <summary>화면 위에 그리는 캔버스. 순서가 클수록 위에 그려진다</summary>
    private static GameObject CreateCanvas(string name, int sortingOrder)
    {
        var root = new GameObject(name, typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        //입력은 키보드로만 받으므로 클릭을 받는 GraphicRaycaster는 붙이지 않는다
        return root;
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
