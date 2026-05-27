using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VirtualKeyboard : MonoBehaviour
{
    public enum KeyboardMode
    {
        Text,
        Number,
        Password
    }

    public Action<InputField, KeyboardMode> OnEnterPressed;

    private readonly List<Button> keyButtons = new List<Button>();
    private readonly Color panelColor = new Color(0.035f, 0.051f, 0.078f, 0.94f);
    private readonly Color keyColor = new Color(0.082f, 0.106f, 0.145f, 0.98f);
    private readonly Color specialKeyColor = new Color(0.11f, 0.145f, 0.2f, 0.98f);
    private readonly Color blue = new Color(0.118f, 0.482f, 1f, 1f);
    private readonly Color closeColor = new Color(0.33f, 0.08f, 0.1f, 0.98f);

    private InputField activeInputField;
    private RectTransform panelRect;
    private Transform rowsRoot;
    private Font runtimeFont;
    private Sprite roundedSprite;
    private KeyboardMode currentMode = KeyboardMode.Text;
    private bool isShift = true;
    private int focusedKeyIndex = -1;
    private int caretIndex;

    private void Awake()
    {
        BuildKeyboard();
        Hide();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    public void RegisterInputField(InputField target, KeyboardMode mode)
    {
        if (target == null)
            return;

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = target.gameObject.AddComponent<EventTrigger>();

        AddTrigger(trigger, EventTriggerType.Select, _ => Show(target, mode));
        AddTrigger(trigger, EventTriggerType.PointerClick, _ => Show(target, mode));
    }

    public void Show(InputField target, KeyboardMode mode)
    {
        if (target == null)
            return;

        activeInputField = target;
        SetMode(mode);

        if (panelRect != null)
            panelRect.gameObject.SetActive(true);

        activeInputField.ActivateInputField();
        activeInputField.MoveTextEnd(false);
        caretIndex = activeInputField.text != null ? activeInputField.text.Length : 0;
    }

    public void Hide()
    {
        if (panelRect != null)
            panelRect.gameObject.SetActive(false);

        focusedKeyIndex = -1;
    }

    public void PressKey(string value)
    {
        if (activeInputField == null || string.IsNullOrEmpty(value))
            return;

        if (currentMode != KeyboardMode.Number && value.Length == 1 && char.IsLetter(value[0]))
            value = isShift ? value.ToUpperInvariant() : value.ToLowerInvariant();

        InsertText(value);
    }

    public void PressBackspace()
    {
        if (activeInputField == null || string.IsNullOrEmpty(activeInputField.text))
            return;

        caretIndex = Mathf.Clamp(caretIndex, 0, activeInputField.text.Length);
        if (caretIndex <= 0)
            caretIndex = activeInputField.text.Length;

        activeInputField.text = activeInputField.text.Remove(caretIndex - 1, 1);
        SetCaret(caretIndex - 1);
    }

    public void PressSpace()
    {
        InsertText(" ");
    }

    public void PressClear()
    {
        if (activeInputField == null)
            return;

        activeInputField.text = string.Empty;
        SetCaret(0);
    }

    public void PressEnter()
    {
        if (activeInputField != null)
            activeInputField.DeactivateInputField();

        if (OnEnterPressed != null)
            OnEnterPressed(activeInputField, currentMode);
    }

    public void ToggleShift()
    {
        isShift = !isShift;
        RebuildKeys();
    }

    public void SetMode(KeyboardMode mode)
    {
        currentMode = mode;
        RebuildKeys();
    }

    public void PressFocusedKey()
    {
        if (focusedKeyIndex < 0 || focusedKeyIndex >= keyButtons.Count)
            return;

        keyButtons[focusedKeyIndex].onClick.Invoke();
    }

    public void MoveKeyFocus(Vector2 direction)
    {
        if (keyButtons.Count == 0)
            return;

        int delta = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
            ? (direction.x >= 0f ? 1 : -1)
            : (direction.y >= 0f ? -10 : 10);

        focusedKeyIndex = Mathf.Clamp(focusedKeyIndex < 0 ? 0 : focusedKeyIndex + delta, 0, keyButtons.Count - 1);
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
            eventSystem.SetSelectedGameObject(keyButtons[focusedKeyIndex].gameObject);
    }

    public void SetPointerPosition(Vector2 screenPosition)
    {
        for (int i = 0; i < keyButtons.Count; i++)
        {
            RectTransform rect = keyButtons[i].transform as RectTransform;
            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition))
            {
                focusedKeyIndex = i;
                EventSystem eventSystem = EventSystem.current;
                if (eventSystem != null)
                    eventSystem.SetSelectedGameObject(keyButtons[i].gameObject);
                return;
            }
        }
    }

    private void InsertText(string value)
    {
        if (activeInputField == null)
            return;

        caretIndex = Mathf.Clamp(caretIndex, 0, activeInputField.text.Length);
        activeInputField.text = activeInputField.text.Insert(caretIndex, value);
        SetCaret(caretIndex + value.Length);
    }

    private void SetCaret(int position)
    {
        if (activeInputField == null)
            return;

        int safePosition = Mathf.Clamp(position, 0, activeInputField.text.Length);
        caretIndex = safePosition;
        activeInputField.caretPosition = safePosition;
        activeInputField.selectionAnchorPosition = safePosition;
        activeInputField.selectionFocusPosition = safePosition;
    }

    private void BuildKeyboard()
    {
        if (panelRect != null)
            return;

        GameObject panel = new GameObject("VirtualKeyboardPanel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 22f);
        panelRect.sizeDelta = new Vector2(1460f, 320f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.sprite = GetRoundedSprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.color = panelColor;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.118f, 0.482f, 1f, 0.35f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        GameObject rows = new GameObject("Rows", typeof(RectTransform));
        rows.transform.SetParent(panel.transform, false);
        rowsRoot = rows.transform;
        RectTransform rowsRect = rows.GetComponent<RectTransform>();
        rowsRect.anchorMin = Vector2.zero;
        rowsRect.anchorMax = Vector2.one;
        rowsRect.offsetMin = new Vector2(24f, 18f);
        rowsRect.offsetMax = new Vector2(-24f, -18f);

        VerticalLayoutGroup layout = rows.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;
    }

    private void AddTrigger(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = eventType;
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private void RebuildKeys()
    {
        if (rowsRoot == null)
            return;

        for (int i = rowsRoot.childCount - 1; i >= 0; i--)
            Destroy(rowsRoot.GetChild(i).gameObject);

        keyButtons.Clear();

        if (currentMode == KeyboardMode.Number)
            BuildNumberKeys();
        else
            BuildTextKeys();
    }

    private void BuildTextKeys()
    {
        AddRow(new string[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" });
        AddRow(new string[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" });
        AddRow(new string[] { "A", "S", "D", "F", "G", "H", "J", "K", "L" });
        AddRow(new string[] { "Shift", "Z", "X", "C", "V", "B", "N", "M", "Backspace" });
        AddRow(new string[] { "123", "Space", "Clear", "Enter", "Close" });
    }

    private void BuildNumberKeys()
    {
        AddRow(new string[] { "1", "2", "3" });
        AddRow(new string[] { "4", "5", "6" });
        AddRow(new string[] { "7", "8", "9" });
        AddRow(new string[] { "Clear", "0", "Backspace" });
        AddRow(new string[] { "ABC", "Enter", "Close" });
    }

    private void AddRow(string[] keys)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(rowsRoot, false);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;

        for (int i = 0; i < keys.Length; i++)
            AddKey(row.transform, keys[i]);
    }

    private void AddKey(Transform parent, string value)
    {
        GameObject obj = new GameObject("Key_" + value, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        LayoutElement layout = obj.AddComponent<LayoutElement>();
        layout.minHeight = 50f;
        layout.preferredHeight = 56f;
        layout.flexibleWidth = value == "Space" ? 3f : IsSpecialKey(value) ? 1.55f : 1f;

        Image image = obj.AddComponent<Image>();
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = GetKeyColor(value);

        Button button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = GetKeyColor(value);
        colors.highlightedColor = blue;
        colors.pressedColor = new Color(0.05f, 0.32f, 0.82f, 1f);
        colors.selectedColor = blue;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        button.colors = colors;

        Text label = CreateText(obj.transform, GetDisplayValue(value), IsSpecialKey(value) ? 18 : 21, FontStyle.Bold);
        label.raycastTarget = false;

        string captured = value;
        button.onClick.AddListener(() => HandleKey(captured));
        keyButtons.Add(button);
    }

    private void HandleKey(string value)
    {
        if (value == "Shift")
            ToggleShift();
        else if (value == "Backspace")
            PressBackspace();
        else if (value == "Space")
            PressSpace();
        else if (value == "Clear")
            PressClear();
        else if (value == "Enter")
            PressEnter();
        else if (value == "Close")
            Hide();
        else if (value == "123")
            SetMode(KeyboardMode.Number);
        else if (value == "ABC")
            SetMode(activeInputField != null && activeInputField.contentType == InputField.ContentType.Password ? KeyboardMode.Password : KeyboardMode.Text);
        else
            PressKey(value);
    }

    private string GetDisplayValue(string value)
    {
        if (value.Length == 1 && char.IsLetter(value[0]))
            return isShift ? value.ToUpperInvariant() : value.ToLowerInvariant();

        return value;
    }

    private bool IsSpecialKey(string value)
    {
        return value == "Shift" || value == "Backspace" || value == "Space" || value == "Clear" || value == "Enter" || value == "Close" || value == "123" || value == "ABC";
    }

    private Color GetKeyColor(string value)
    {
        if (value == "Enter")
            return blue;

        if (value == "Close")
            return closeColor;

        return IsSpecialKey(value) ? specialKeyColor : keyColor;
    }

    private Text CreateText(Transform parent, string value, int size, FontStyle style)
    {
        GameObject obj = new GameObject("Label", typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = obj.AddComponent<Text>();
        text.text = value;
        text.font = GetRuntimeFont();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Font GetRuntimeFont()
    {
        if (runtimeFont != null)
            return runtimeFont;

        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (runtimeFont == null)
            runtimeFont = Font.CreateDynamicFontFromOSFont(new string[] { "Malgun Gothic", "Segoe UI", "Arial" }, 16);

        return runtimeFont;
    }

    private Sprite GetRoundedSprite()
    {
        if (roundedSprite != null)
            return roundedSprite;

        Texture2D texture = new Texture2D(48, 48, TextureFormat.ARGB32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < 48; y++)
        {
            for (int x = 0; x < 48; x++)
            {
                bool inside = IsInsideRoundedRect(x, y, 48, 48, 10);
                texture.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        }

        texture.Apply();
        roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, 48f, 48f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10f, 10f, 10f, 10f));
        return roundedSprite;
    }

    private bool IsInsideRoundedRect(int x, int y, int width, int height, int radius)
    {
        int left = radius;
        int right = width - radius - 1;
        int bottom = radius;
        int top = height - radius - 1;

        if ((x >= left && x <= right) || (y >= bottom && y <= top))
            return true;

        int cx = x < left ? left : right;
        int cy = y < bottom ? bottom : top;
        int dx = x - cx;
        int dy = y - cy;
        return dx * dx + dy * dy <= radius * radius;
    }
}
