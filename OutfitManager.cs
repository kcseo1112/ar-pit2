using UnityEngine;

public class OutfitManager : MonoBehaviour
{
    [System.Serializable]
    public class OutfitSlot
    {
        public string name;
        public GameObject displayRoot;
        public AvatarRetarget avatar;
    }

    [Header("Outfits")]
    public OutfitSlot[] outfits;
    public int currentIndex = 0;
    public bool activateCurrentOnStart = true;

    [Header("Debug Controls")]
    public bool enableKeyboardSwitch = true;
    public KeyCode previousKey = KeyCode.LeftBracket;
    public KeyCode nextKey = KeyCode.RightBracket;

    public AvatarRetarget CurrentAvatar
    {
        get
        {
            if (outfits == null || outfits.Length == 0)
                return null;

            int index = Mathf.Clamp(currentIndex, 0, outfits.Length - 1);
            OutfitSlot slot = outfits[index];
            return slot != null ? slot.avatar : null;
        }
    }

    void Start()
    {
        if (activateCurrentOnStart)
            SelectOutfit(currentIndex);
    }

    void Update()
    {
        if (!enableKeyboardSwitch)
            return;

        if (Input.GetKeyDown(previousKey))
            SelectPrevious();

        if (Input.GetKeyDown(nextKey))
            SelectNext();
    }

    public void ApplyPose(Vector3[] joints, float screenShoulderWidth)
    {
        AvatarRetarget currentAvatar = CurrentAvatar;
        if (currentAvatar == null)
            return;

        currentAvatar.ApplyPose(joints);
        currentAvatar.ApplyBodyFit(joints, screenShoulderWidth);
    }

    public void SelectNext()
    {
        if (outfits == null || outfits.Length == 0)
            return;

        SelectOutfit((currentIndex + 1) % outfits.Length);
    }

    public void SelectPrevious()
    {
        if (outfits == null || outfits.Length == 0)
            return;

        int nextIndex = currentIndex - 1;
        if (nextIndex < 0)
            nextIndex = outfits.Length - 1;

        SelectOutfit(nextIndex);
    }

    public void SelectOutfit(int index)
    {
        if (outfits == null || outfits.Length == 0)
            return;

        currentIndex = Mathf.Clamp(index, 0, outfits.Length - 1);

        for (int i = 0; i < outfits.Length; i++)
        {
            OutfitSlot slot = outfits[i];
            if (slot == null)
                continue;

            bool isCurrent = i == currentIndex;

            if (slot.displayRoot != null)
                slot.displayRoot.SetActive(isCurrent);

            if (slot.avatar != null)
                slot.avatar.enabled = isCurrent;
        }
    }
}
