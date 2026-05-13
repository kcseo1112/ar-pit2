using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class OutfitItemButton : MonoBehaviour
{
    [Header("UI")]
    public Button button;
    public Image thumbnailImage;
    public Image selectedMark;
    public Text nameText;
    public Text numberText;

    public void Setup(int displayNumber, string itemName, Sprite thumbnail, UnityAction onClick)
    {
        if (button == null)
            button = GetComponent<Button>();

        if (thumbnailImage != null)
        {
            thumbnailImage.sprite = thumbnail;
            thumbnailImage.preserveAspect = true;
            thumbnailImage.color = thumbnail == null
                ? new Color(0.86f, 0.88f, 0.9f, 1f)
                : Color.white;
        }

        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(itemName)
                ? "Item " + displayNumber
                : itemName;
        }

        if (numberText != null)
            numberText.text = displayNumber.ToString();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedMark != null)
            selectedMark.gameObject.SetActive(selected);
    }
}
