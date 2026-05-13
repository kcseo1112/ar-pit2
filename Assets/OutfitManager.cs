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
    public bool useSeparatedOutfits = false;
    public OutfitSlot[] upperOutfits;
    public OutfitSlot[] lowerOutfits;
    public int currentUpperIndex = 0;
    public int currentLowerIndex = 0;
    public bool activateCurrentOnStart = true;

    [Header("Display Offset")]
    public bool applyDisplayOffsetOnActivate = false;
    public Vector3 outfitDisplayLocalPosition = new Vector3(0f, -1.1f, 0f);

    [Header("Debug Controls")]
    public bool enableKeyboardSwitch = true;
    public KeyCode previousKey = KeyCode.LeftBracket;
    public KeyCode nextKey = KeyCode.RightBracket;
    public KeyCode previousUpperKey = KeyCode.Q;
    public KeyCode nextUpperKey = KeyCode.E;
    public KeyCode previousLowerKey = KeyCode.A;
    public KeyCode nextLowerKey = KeyCode.D;

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
            RefreshActiveOutfits();
    }

    void Update()
    {
        if (!enableKeyboardSwitch)
            return;

        if (useSeparatedOutfits)
        {
            if (Input.GetKeyDown(previousUpperKey))
                SelectPreviousUpper();

            if (Input.GetKeyDown(nextUpperKey))
                SelectNextUpper();

            if (Input.GetKeyDown(previousLowerKey))
                SelectPreviousLower();

            if (Input.GetKeyDown(nextLowerKey))
                SelectNextLower();
        }
        else
        {
            if (Input.GetKeyDown(previousKey))
                SelectPrevious();

            if (Input.GetKeyDown(nextKey))
                SelectNext();
        }
    }

    public void ApplyPose(Vector3[] joints, float screenShoulderWidth)
    {
        if (useSeparatedOutfits)
        {
            ApplyPoseToSlot(GetSlot(upperOutfits, currentUpperIndex), joints, screenShoulderWidth);
            ApplyPoseToSlot(GetSlot(lowerOutfits, currentLowerIndex), joints, screenShoulderWidth);
            return;
        }

        ApplyPoseToSlot(GetSlot(outfits, currentIndex), joints, screenShoulderWidth);
    }

    public void ApplyPose(
        Vector3[] joints,
        float screenShoulderWidth,
        Vector2 rootPixel,
        float rootDepthMeters,
        Vector2 frameSize)
    {
        if (useSeparatedOutfits)
        {
            ApplyPoseToSlot(
                GetSlot(upperOutfits, currentUpperIndex),
                joints,
                screenShoulderWidth,
                rootPixel,
                rootDepthMeters,
                frameSize);

            ApplyPoseToSlot(
                GetSlot(lowerOutfits, currentLowerIndex),
                joints,
                screenShoulderWidth,
                rootPixel,
                rootDepthMeters,
                frameSize);

            return;
        }

        ApplyPoseToSlot(
            GetSlot(outfits, currentIndex),
            joints,
            screenShoulderWidth,
            rootPixel,
            rootDepthMeters,
            frameSize);
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
        RefreshActiveOutfits();
    }

    public void SelectNextUpper()
    {
        if (upperOutfits == null || upperOutfits.Length == 0)
            return;

        SelectUpper((currentUpperIndex + 1) % upperOutfits.Length);
    }

    public void SelectPreviousUpper()
    {
        if (upperOutfits == null || upperOutfits.Length == 0)
            return;

        int nextIndex = currentUpperIndex - 1;
        if (nextIndex < 0)
            nextIndex = upperOutfits.Length - 1;

        SelectUpper(nextIndex);
    }

    public void SelectNextLower()
    {
        if (lowerOutfits == null || lowerOutfits.Length == 0)
            return;

        SelectLower((currentLowerIndex + 1) % lowerOutfits.Length);
    }

    public void SelectPreviousLower()
    {
        if (lowerOutfits == null || lowerOutfits.Length == 0)
            return;

        int nextIndex = currentLowerIndex - 1;
        if (nextIndex < 0)
            nextIndex = lowerOutfits.Length - 1;

        SelectLower(nextIndex);
    }

    public void SelectUpper(int index)
    {
        if (upperOutfits == null || upperOutfits.Length == 0)
            return;

        currentUpperIndex = Mathf.Clamp(index, 0, upperOutfits.Length - 1);
        RefreshActiveOutfits();
    }

    public void SelectLower(int index)
    {
        if (lowerOutfits == null || lowerOutfits.Length == 0)
            return;

        currentLowerIndex = Mathf.Clamp(index, 0, lowerOutfits.Length - 1);
        RefreshActiveOutfits();
    }

    public void RefreshActiveOutfits()
    {
        if (useSeparatedOutfits)
        {
            SetGroupActive(outfits, -1);
            SetGroupActive(upperOutfits, currentUpperIndex);
            SetGroupActive(lowerOutfits, currentLowerIndex);
        }
        else
        {
            SetGroupActive(outfits, currentIndex);
            SetGroupActive(upperOutfits, -1);
            SetGroupActive(lowerOutfits, -1);
        }
    }

    OutfitSlot GetSlot(OutfitSlot[] slots, int index)
    {
        if (slots == null || slots.Length == 0)
            return null;

        int safeIndex = Mathf.Clamp(index, 0, slots.Length - 1);
        return slots[safeIndex];
    }

    void ApplyPoseToSlot(OutfitSlot slot, Vector3[] joints, float screenShoulderWidth)
    {
        if (slot == null || slot.avatar == null)
            return;

        slot.avatar.ApplyPose(joints);
        slot.avatar.ApplyBodyFit(joints, screenShoulderWidth);
    }

    void ApplyPoseToSlot(
        OutfitSlot slot,
        Vector3[] joints,
        float screenShoulderWidth,
        Vector2 rootPixel,
        float rootDepthMeters,
        Vector2 frameSize)
    {
        if (slot == null || slot.avatar == null)
            return;

        slot.avatar.ApplyRootFollow(rootPixel, rootDepthMeters, frameSize);
        slot.avatar.ApplyPose(joints);
        slot.avatar.ApplyBodyFit(joints, screenShoulderWidth);
    }

    void SetGroupActive(OutfitSlot[] slots, int activeIndex)
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            OutfitSlot slot = slots[i];
            if (slot == null)
                continue;

            bool isActive = i == activeIndex;

            if (slot.displayRoot != null)
            {
                slot.displayRoot.SetActive(isActive);

                if (isActive && applyDisplayOffsetOnActivate)
                    slot.displayRoot.transform.localPosition = outfitDisplayLocalPosition;
            }

            if (slot.avatar != null)
                slot.avatar.enabled = isActive;
        }
    }
}
