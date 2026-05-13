using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FitRoomMainUI : MonoBehaviour
{
    [Header("References")]
    public OutfitManager outfitManager;

    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject loginPanel;
    public GameObject registerPanel;
    public GameObject wishlistPanel;

    [Header("Scroll Contents")]
    public Transform upperContent;
    public Transform lowerContent;

    [Header("Prefab")]
    public OutfitItemButton itemPrefab;

    [Header("Thumbnails")]
    public Sprite[] upperThumbnails;
    public Sprite[] lowerThumbnails;

    [Header("Selected Text")]
    public Text selectedUpperText;
    public Text selectedLowerText;

    private Font runtimeFont;
    private readonly List<OutfitItemButton> upperButtons = new List<OutfitItemButton>();
    private readonly List<OutfitItemButton> lowerButtons = new List<OutfitItemButton>();

    void Awake()
    {
        if (outfitManager == null)
            outfitManager = FindObjectOfType<OutfitManager>();

        EnsureRuntimeLayout();
    }

    void Start()
    {
        EnsureRuntimeLayout();

        if (outfitManager != null)
        {
            outfitManager.useSeparatedOutfits = true;
            outfitManager.enableKeyboardSwitch = false;
            outfitManager.activateCurrentOnStart = true;
            outfitManager.RefreshActiveOutfits();
        }

        ShowMainPanel();
        Rebuild();
    }

    public void Rebuild()
    {
        EnsureRuntimeLayout();

        if (!IsRuntimeLayoutReady())
        {
            Debug.LogError(
                "[FitRoomUI] Rebuild aborted. Runtime layout is not ready. " +
                $"mainPanel={mainPanel != null}, " +
                $"upperContent={upperContent != null}, " +
                $"lowerContent={lowerContent != null}, " +
                $"itemPrefab={itemPrefab != null}"
            );
            return;
        }

        ClearContent(upperContent);
        ClearContent(lowerContent);

        upperButtons.Clear();
        lowerButtons.Clear();

        BuildUpperList();
        BuildLowerList();
        RefreshSelectionUI();
    }

    private void BuildUpperList()
    {
        if (itemPrefab == null || upperContent == null)
        {
            Debug.LogError("[FitRoomUI] Cannot build upper list. itemPrefab or upperContent is null.");
            return;
        }

        int count = outfitManager != null && outfitManager.upperOutfits != null
            ? outfitManager.upperOutfits.Length
            : 0;

        if (count == 0)
        {
            CreateEmptyMessage(upperContent, "No upper outfits");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            int index = i;
            OutfitItemButton item = Instantiate(itemPrefab, upperContent);
            item.gameObject.SetActive(true);
            item.Setup(index + 1, GetUpperName(index), GetSprite(upperThumbnails, index), () => SelectUpper(index));
            upperButtons.Add(item);
        }
    }

    private void BuildLowerList()
    {
        if (itemPrefab == null || lowerContent == null)
        {
            Debug.LogError("[FitRoomUI] Cannot build lower list. itemPrefab or lowerContent is null.");
            return;
        }

        int count = outfitManager != null && outfitManager.lowerOutfits != null
            ? outfitManager.lowerOutfits.Length
            : 0;

        if (count == 0)
        {
            CreateEmptyMessage(lowerContent, "No lower outfits");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            int index = i;
            OutfitItemButton item = Instantiate(itemPrefab, lowerContent);
            item.gameObject.SetActive(true);
            item.Setup(index + 1, GetLowerName(index), GetSprite(lowerThumbnails, index), () => SelectLower(index));
            lowerButtons.Add(item);
        }
    }

    public void SelectUpper(int index)
    {
        if (outfitManager == null)
            return;

        outfitManager.SelectUpper(index);
        RefreshSelectionUI();
    }

    public void SelectLower(int index)
    {
        if (outfitManager == null)
            return;

        outfitManager.SelectLower(index);
        RefreshSelectionUI();
    }

    private void RefreshSelectionUI()
    {
        if (outfitManager == null)
            return;

        for (int i = 0; i < upperButtons.Count; i++)
            upperButtons[i].SetSelected(i == outfitManager.currentUpperIndex);

        for (int i = 0; i < lowerButtons.Count; i++)
            lowerButtons[i].SetSelected(i == outfitManager.currentLowerIndex);

        if (selectedUpperText != null)
            selectedUpperText.text = "Upper: " + (outfitManager.currentUpperIndex + 1);

        if (selectedLowerText != null)
            selectedLowerText.text = "Lower: " + (outfitManager.currentLowerIndex + 1);
    }

    private string GetUpperName(int index)
    {
        OutfitManager.OutfitSlot slot = GetSlot(outfitManager != null ? outfitManager.upperOutfits : null, index);
        return slot != null && !string.IsNullOrEmpty(slot.name)
            ? slot.name
            : "Upper " + (index + 1);
    }

    private string GetLowerName(int index)
    {
        OutfitManager.OutfitSlot slot = GetSlot(outfitManager != null ? outfitManager.lowerOutfits : null, index);
        return slot != null && !string.IsNullOrEmpty(slot.name)
            ? slot.name
            : "Lower " + (index + 1);
    }

    private OutfitManager.OutfitSlot GetSlot(OutfitManager.OutfitSlot[] slots, int index)
    {
        if (slots == null || index < 0 || index >= slots.Length)
            return null;

        return slots[index];
    }

    private Sprite GetSprite(Sprite[] sprites, int index)
    {
        if (sprites == null || index < 0 || index >= sprites.Length)
            return null;

        return sprites[index];
    }

    private void ClearContent(Transform content)
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    public void ShowMainPanel()
    {
        SetPanel(mainPanel, true);
        SetPanel(loginPanel, false);
        SetPanel(registerPanel, false);
        SetPanel(wishlistPanel, false);
    }

    public void ShowLoginPanel()
    {
        SetPanel(mainPanel, false);
        SetPanel(loginPanel, true);
        SetPanel(registerPanel, false);
        SetPanel(wishlistPanel, false);
    }

    public void ShowRegisterPanel()
    {
        SetPanel(mainPanel, false);
        SetPanel(loginPanel, false);
        SetPanel(registerPanel, true);
        SetPanel(wishlistPanel, false);
    }

    public void ShowWishlistPanel()
    {
        SetPanel(mainPanel, false);
        SetPanel(loginPanel, false);
        SetPanel(registerPanel, false);
        SetPanel(wishlistPanel, true);
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    private void EnsureRuntimeLayout()
    {
        try
        {
            Debug.Log("[FitRoomUI] EnsureRuntimeLayout start");

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.Log("[FitRoomUI] CreateCanvas");
                canvas = CreateCanvas(transform);
            }

            if (mainPanel == null)
                mainPanel = FindDirectChild(canvas.transform, "MainPanel");

            if (mainPanel == null)
            {
                Debug.Log("[FitRoomUI] CreateMainPanel");
                mainPanel = CreateMainPanel(canvas.transform);
            }

            ResolveHeaderReferences();
            if (selectedUpperText == null || selectedLowerText == null)
            {
                Debug.Log("[FitRoomUI] CreateHeader");
                GameObject existingHeader = FindDirectChild(mainPanel.transform, "Header");
                if (existingHeader != null)
                {
                    existingHeader.name = "Header_Broken";
                    DestroyRuntimeObject(existingHeader);
                }

                CreateHeader(mainPanel.transform);
            }

            if (upperContent == null)
                upperContent = FindScrollContent(mainPanel.transform, "UpperScrollView");

            if (upperContent == null)
            {
                Debug.Log("[FitRoomUI] CreateUpperScroll");
                upperContent = CreateUpperScroll(mainPanel.transform);
            }

            if (lowerContent == null)
                lowerContent = FindScrollContent(mainPanel.transform, "LowerScrollView");

            if (lowerContent == null)
            {
                Debug.Log("[FitRoomUI] CreateLowerScroll");
                lowerContent = CreateLowerScroll(mainPanel.transform);
            }

            if (itemPrefab == null)
                itemPrefab = FindItemPrefab(canvas.transform);

            if (itemPrefab == null)
            {
                Debug.Log("[FitRoomUI] CreateItemPrefab");
                itemPrefab = CreateItemPrefab(canvas.transform);
            }

            EnsureEventSystem();

            Debug.Log("[FitRoomUI] runtime layout ready");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[FitRoomUI] EnsureRuntimeLayout failed\n" + e);
        }
    }

    private Canvas CreateCanvas(Transform controller)
    {
        GameObject canvasObject = new GameObject("FitRoom Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        controller.SetParent(canvasObject.transform, false);
        return canvas;
    }

    private GameObject CreateMainPanel(Transform parent)
    {
        GameObject panel = CreateRect(
            "MainPanel",
            parent,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero
        );

        RectTransform rect = panel.GetComponent<RectTransform>();
        SetOffsets(rect, 0f, 0f, 0f, 0f);

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);

        return panel;
    }

    private void CreateHeader(Transform parent)
    {
        GameObject header = CreateRect("Header", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -72f));
        SetOffsets(header.GetComponent<RectTransform>(), 0f, -72f, 0f, 0f);
        Image background = header.AddComponent<Image>();
        background.color = new Color(0.02f, 0.025f, 0.03f, 0.92f);

        Text title = CreateText("Title", header.transform, "FitRoom", 30, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetOffsets(title.rectTransform, 24f, 0f, -320f, 0f);

        selectedUpperText = CreateText("SelectedUpperText", header.transform, "Upper: 1", 20, FontStyle.Normal, TextAnchor.MiddleRight);
        SetOffsets(selectedUpperText.rectTransform, 520f, 0f, -190f, 0f);

        selectedLowerText = CreateText("SelectedLowerText", header.transform, "Lower: 1", 20, FontStyle.Normal, TextAnchor.MiddleRight);
        SetOffsets(selectedLowerText.rectTransform, 710f, 0f, -24f, 0f);
    }

    private Transform CreateUpperScroll(Transform parent)
    {
        GameObject scroll = CreateRect("UpperScrollView", parent, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero);
        SetOffsets(scroll.GetComponent<RectTransform>(), -250f, 170f, -16f, -84f);
        AddPanelBackground(scroll, new Color(0.02f, 0.025f, 0.03f, 0.94f));
        CreatePanelLabel(scroll.transform, "UPPER", TextAnchor.UpperCenter);

        ScrollRect scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CreateRect("Viewport", scroll.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        SetOffsets(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, -34f);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        GameObject content = CreateRect("Content", viewport.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero);
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 12, 10);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content.GetComponent<RectTransform>();
        return content.transform;
    }

    private Transform CreateLowerScroll(Transform parent)
    {
        GameObject scroll = CreateRect("LowerScrollView", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero);
        SetOffsets(scroll.GetComponent<RectTransform>(), 16f, 16f, -282f, 154f);
        AddPanelBackground(scroll, new Color(0.02f, 0.025f, 0.03f, 0.94f));
        CreatePanelLabel(scroll.transform, "LOWER", TextAnchor.UpperLeft);

        ScrollRect scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CreateRect("Viewport", scroll.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        SetOffsets(viewport.GetComponent<RectTransform>(), 74f, 0f, 0f, 0f);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        GameObject content = CreateRect("Content", viewport.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero);
        HorizontalLayoutGroup layout = content.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content.GetComponent<RectTransform>();
        return content.transform;
    }

    private OutfitItemButton CreateItemPrefab(Transform parent)
    {
        GameObject root = new GameObject("OutfitItemButtonTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(OutfitItemButton));
        root.transform.SetParent(parent, false);
        root.SetActive(false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(170f, 116f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.96f, 0.97f, 0.98f, 0.94f);

        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = 170f;
        layout.preferredHeight = 116f;
        layout.minWidth = 160f;
        layout.minHeight = 108f;

        OutfitItemButton item = root.GetComponent<OutfitItemButton>();
        item.button = root.GetComponent<Button>();

        GameObject thumbnail = CreateRect("Thumbnail", root.transform, new Vector2(0f, 0.34f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero);
        SetOffsets(thumbnail.GetComponent<RectTransform>(), 10f, -10f, -10f, -42f);
        item.thumbnailImage = thumbnail.AddComponent<Image>();
        item.thumbnailImage.color = new Color(0.86f, 0.88f, 0.9f, 1f);

        Text number = CreateText("NumberText", root.transform, "1", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
        number.color = new Color(0.05f, 0.08f, 0.1f, 1f);
        number.rectTransform.anchorMin = new Vector2(0f, 1f);
        number.rectTransform.anchorMax = new Vector2(0f, 1f);
        number.rectTransform.pivot = new Vector2(0f, 1f);
        number.rectTransform.sizeDelta = new Vector2(32f, 32f);
        number.rectTransform.anchoredPosition = new Vector2(10f, -10f);
        item.numberText = number;

        Text name = CreateText("NameText", root.transform, "Upper 1", 16, FontStyle.Bold, TextAnchor.MiddleCenter);
        name.color = new Color(0.05f, 0.08f, 0.1f, 1f);
        SetOffsets(name.rectTransform, 8f, -82f, -8f, -8f);
        item.nameText = name;

        GameObject selected = CreateRect("SelectedMark", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image selectedImage = selected.AddComponent<Image>();
        selectedImage.color = new Color(0.1f, 0.42f, 0.95f, 0.28f);
        selected.SetActive(false);
        item.selectedMark = selectedImage;

        return item;
    }

    private void CreatePanelLabel(Transform parent, string labelText, TextAnchor alignment)
    {
        Text label = CreateText("PanelLabel", parent, labelText, 18, FontStyle.Bold, alignment);
        label.color = new Color(0.75f, 0.84f, 0.95f, 1f);

        if (alignment == TextAnchor.UpperLeft)
            SetOffsets(label.rectTransform, 14f, -36f, -8f, -8f);
        else
            SetOffsets(label.rectTransform, 8f, -36f, -8f, -8f);
    }

    private void CreateEmptyMessage(Transform parent, string message)
    {
        if (parent == null)
            return;

        Text label = CreateText("EmptyMessage", parent, message, 16, FontStyle.Normal, TextAnchor.MiddleCenter);
        label.color = new Color(1f, 1f, 1f, 0.82f);

        LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 170f;
        layout.preferredHeight = 80f;
    }

    private GameObject CreateRect(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return obj;
    }

    private Text CreateText(string objectName, Transform parent, string text, int size, FontStyle style, TextAnchor alignment)
    {
        GameObject obj = CreateRect(
            objectName,
            parent,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero
        );

        Text label = obj.AddComponent<Text>();
        label.text = text;
        label.font = GetRuntimeFont();
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    private Font GetRuntimeFont()
    {
        if (runtimeFont != null)
            return runtimeFont;

        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (runtimeFont == null)
            runtimeFont = Font.CreateDynamicFontFromOSFont(new string[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 16);

        return runtimeFont;
    }

    private void AddPanelBackground(GameObject target, Color color)
    {
        Image image = target.AddComponent<Image>();
        image.color = color;
    }

    private void SetOffsets(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private bool IsRuntimeLayoutReady()
    {
        return mainPanel != null
            && upperContent != null
            && lowerContent != null
            && itemPrefab != null;
    }

    private void DestroyRuntimeObject(GameObject obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private void ResolveHeaderReferences()
    {
        if (mainPanel == null)
            return;

        Transform header = FindDirectChild(mainPanel.transform, "Header") != null
            ? FindDirectChild(mainPanel.transform, "Header").transform
            : null;

        if (header == null)
            return;

        if (selectedUpperText == null)
        {
            GameObject upper = FindDirectChild(header, "SelectedUpperText");
            selectedUpperText = upper != null ? upper.GetComponent<Text>() : null;
        }

        if (selectedLowerText == null)
        {
            GameObject lower = FindDirectChild(header, "SelectedLowerText");
            selectedLowerText = lower != null ? lower.GetComponent<Text>() : null;
        }
    }

    private GameObject FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child.gameObject;
        }

        return null;
    }

    private Transform FindScrollContent(Transform parent, string scrollName)
    {
        GameObject scroll = FindDirectChild(parent, scrollName);
        if (scroll == null)
            return null;

        Transform viewport = scroll.transform.Find("Viewport");
        if (viewport == null)
            return null;

        return viewport.Find("Content");
    }

    private OutfitItemButton FindItemPrefab(Transform canvasTransform)
    {
        GameObject prefabObject = FindDirectChild(canvasTransform, "OutfitItemButtonTemplate");
        if (prefabObject == null)
        {
            prefabObject = FindDirectChild(transform, "OutfitItemButtonTemplate");
            if (prefabObject != null)
                prefabObject.transform.SetParent(canvasTransform, false);
        }

        return prefabObject != null ? prefabObject.GetComponent<OutfitItemButton>() : null;
    }
}

public static class FitRoomMainUIBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateMainUI()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildForCurrentScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BuildForCurrentScene();
    }

    private static void BuildForCurrentScene()
    {
        if (Object.FindObjectOfType<FitRoomMainUI>() != null)
            return;

        OutfitManager outfitManager = Object.FindObjectOfType<OutfitManager>();
        if (outfitManager == null)
            return;

        GameObject controller = new GameObject("FitRoomMainUI");
        FitRoomMainUI ui = controller.AddComponent<FitRoomMainUI>();
        ui.outfitManager = outfitManager;
        Debug.Log("FitRoomMainUI bootstrap created");
    }
}
