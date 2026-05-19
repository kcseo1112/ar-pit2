using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
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
    public GameObject userInfoPanel;

    [Header("Thumbnails")]
    public Sprite[] upperThumbnails;
    public Sprite[] lowerThumbnails;
    public Sprite[] hatThumbnails;
    public Sprite[] shoesThumbnails;

    [Header("API")]
    public string apiBaseUrl = "http://127.0.0.1:5000";

    private const string CategoryUpper = "upper";
    private const string CategoryLower = "lower";
    private const string CategoryHat = "hat";
    private const string CategoryShoes = "shoes";

    private readonly List<OutfitCardButton> outfitCards = new List<OutfitCardButton>();
    private readonly HashSet<string> favoriteKeys = new HashSet<string>();
    private readonly HashSet<int> favoriteOutfitIds = new HashSet<int>();
    private readonly List<DbOutfit> wishlistOutfits = new List<DbOutfit>();
    private readonly Dictionary<string, DbOutfit> dbOutfitsByUnityKey = new Dictionary<string, DbOutfit>();
    private readonly Dictionary<string, Button> categoryButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Text> categoryButtonTexts = new Dictionary<string, Text>();
    private readonly Dictionary<string, Image> categoryButtonBorders = new Dictionary<string, Image>();
    private readonly Dictionary<string, Text> wishlistTabTexts = new Dictionary<string, Text>();
    private readonly Dictionary<string, Image> wishlistTabBorders = new Dictionary<string, Image>();

    private string activeCategory = CategoryUpper;
    private string activeWishlistCategory = "all";
    private Font runtimeFont;
    private Sprite roundedSprite;
    private Sprite circleSprite;

    private Transform carouselContent;
    private ScrollRect carouselScrollRect;
    private Image currentUpperThumbnailImage;
    private Image currentLowerThumbnailImage;
    private Image currentHatThumbnailImage;
    private Image currentShoesThumbnailImage;
    private Text currentUpperNameText;
    private Text currentLowerNameText;
    private Text currentHatNameText;
    private Text currentShoesNameText;
    private Image categoryInfoThumbnailImage;
    private Text categoryInfoTitleText;
    private Text categoryInfoBodyText;
    private Text categoryInfoIconText;
    private Text wishlistBodyText;
    private Transform wishlistContent;
    private Text userStatusText;
    private InputField loginPhoneInput;
    private InputField loginPasswordInput;
    private InputField registerNameInput;
    private InputField registerPhoneInput;
    private InputField registerPasswordInput;
    private InputField registerPasswordConfirmInput;
    private Text userInfoNameText;
    private Text userInfoPhoneText;
    private InputField currentPasswordInput;
    private InputField newPasswordInput;
    private InputField newPasswordConfirmInput;
    private int loggedInUserId;
    private string loggedInUserName;
    private string loggedInUserPhone;
    private bool runtimeLayoutBuilt;

    private Color glassColor = new Color(0.043f, 0.067f, 0.11f, 0.88f);
    private Color cardColor = new Color(0.082f, 0.106f, 0.145f, 0.94f);
    private Color blue = new Color(0.118f, 0.482f, 1f, 1f);
    private Color cyan = new Color(0f, 0.64f, 1f, 1f);
    private Color mutedText = new Color(0.667f, 0.706f, 0.769f, 1f);

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
        SelectCategory(CategoryUpper);
    }

    public void Rebuild()
    {
        EnsureRuntimeLayout();
        RebuildCarousel();
        RefreshCurrentOutfitPanel();
        RefreshCategoryTabs();
    }

    public void SelectCategory(string categoryCode)
    {
        activeCategory = categoryCode;
        RebuildCarousel();
        RefreshCategoryTabs();
        RefreshCategoryInfoPanel();
        StartCoroutine(LoadCategoryOutfitsRoutine(categoryCode));
    }

    public void SelectUpper(int index)
    {
        SelectOutfit(CategoryUpper, index);
    }

    public void SelectLower(int index)
    {
        SelectOutfit(CategoryLower, index);
    }

    public void ShowMainPanel()
    {
        SetPanel(mainPanel, true);
        SetPanel(loginPanel, false);
        SetPanel(registerPanel, false);
        SetPanel(wishlistPanel, false);
        SetPanel(userInfoPanel, false);
    }

    public void ShowLoginPanel()
    {
        SetPanel(mainPanel, true);
        SetPanel(loginPanel, true);
        SetPanel(registerPanel, false);
        SetPanel(wishlistPanel, false);
        SetPanel(userInfoPanel, false);
    }

    public void ShowUserInfoPanel()
    {
        if (loggedInUserId <= 0)
        {
            ShowLoginPanel();
            return;
        }

        SetPanel(mainPanel, true);
        SetPanel(loginPanel, false);
        SetPanel(registerPanel, false);
        SetPanel(wishlistPanel, false);
        SetPanel(userInfoPanel, true);
        StartCoroutine(LoadUserInfoRoutine());
    }

    public void ShowRegisterPanel()
    {
        SetPanel(mainPanel, true);
        SetPanel(loginPanel, false);
        SetPanel(registerPanel, true);
        SetPanel(wishlistPanel, false);
        SetPanel(userInfoPanel, false);
    }

    public void ShowWishlistPanel()
    {
        SetPanel(mainPanel, true);
        SetPanel(loginPanel, false);
        SetPanel(registerPanel, false);
        SetPanel(wishlistPanel, true);
        SetPanel(userInfoPanel, false);
        StartCoroutine(LoadWishlistRoutine());
    }

    private void SelectOutfit(string categoryCode, int index)
    {
        if (outfitManager == null)
            return;

        if (categoryCode == CategoryUpper)
            outfitManager.SelectUpper(index);
        else if (categoryCode == CategoryLower)
            outfitManager.SelectLower(index);
        else
        {
            Debug.Log("[FitRoomUI] " + categoryCode + " category is coming soon.");
            return;
        }

        RefreshSelectedCardState();
        RefreshCurrentOutfitPanel();
        RefreshCategoryInfoPanel();
    }

    private void ToggleFavorite(string categoryCode, int index)
    {
        if (loggedInUserId <= 0)
        {
            ShowLoginPanel();
            Debug.LogWarning("[FitRoomUI] 로그인 후 찜 기능을 사용할 수 있습니다.");
            return;
        }

        DbOutfit outfit = GetDbOutfit(categoryCode, index);
        if (outfit == null || outfit.outfit_id <= 0)
        {
            Debug.LogWarning("[FitRoomUI] DB 의상 정보가 없어 찜을 저장할 수 없습니다: " + GetFavoriteKey(categoryCode, index));
            return;
        }

        StartCoroutine(ToggleFavoriteRoutine(outfit));
    }

    private void RebuildCarousel()
    {
        if (carouselContent == null)
            return;

        ClearChildren(carouselContent);
        outfitCards.Clear();

        int count = GetCategoryCount(activeCategory);
        if (count == 0)
        {
            CreateComingSoonCard(carouselContent, activeCategory);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            int index = i;
            OutfitCardButton card = CreateOutfitCard(carouselContent, activeCategory, index);
            card.button.onClick.AddListener(() => SelectOutfit(activeCategory, index));
            card.favoriteButton.onClick.AddListener(() => ToggleFavorite(activeCategory, index));
            outfitCards.Add(card);
        }

        RefreshSelectedCardState();

        if (carouselScrollRect != null)
            carouselScrollRect.horizontalNormalizedPosition = 0f;
    }

    private void RefreshCurrentOutfitPanel()
    {
        if (currentUpperNameText != null)
            currentUpperNameText.text = GetOutfitName(CategoryUpper, outfitManager != null ? outfitManager.currentUpperIndex : 0);

        SetThumbnailImage(currentUpperThumbnailImage, GetThumbnail(CategoryUpper, outfitManager != null ? outfitManager.currentUpperIndex : 0));

        if (currentLowerNameText != null)
            currentLowerNameText.text = GetOutfitName(CategoryLower, outfitManager != null ? outfitManager.currentLowerIndex : 0);

        SetThumbnailImage(currentLowerThumbnailImage, GetThumbnail(CategoryLower, outfitManager != null ? outfitManager.currentLowerIndex : 0));

        if (currentHatNameText != null)
            currentHatNameText.text = "선택 없음";

        SetThumbnailImage(currentHatThumbnailImage, null);

        if (currentShoesNameText != null)
            currentShoesNameText.text = "선택 없음";

        SetThumbnailImage(currentShoesThumbnailImage, null);
    }

    private void RefreshCategoryTabs()
    {
        foreach (KeyValuePair<string, Image> pair in categoryButtonBorders)
        {
            bool selected = pair.Key == activeCategory;
            pair.Value.color = selected ? blue : new Color(1f, 1f, 1f, 0.16f);
        }

        foreach (KeyValuePair<string, Text> pair in categoryButtonTexts)
            pair.Value.color = pair.Key == activeCategory ? Color.white : mutedText;
    }

    private void RefreshSelectedCardState()
    {
        for (int i = 0; i < outfitCards.Count; i++)
        {
            OutfitCardButton card = outfitCards[i];
            bool selected = IsSelected(card.categoryCode, card.index);
            bool favorite = IsFavorite(card.categoryCode, card.index);
            card.SetSelected(selected, blue);
            card.SetFavorite(favorite, cyan);
        }
    }

    private void RefreshCategoryInfoPanel()
    {
        if (categoryInfoTitleText == null || categoryInfoBodyText == null)
            return;

        if (activeCategory == CategoryUpper)
        {
            categoryInfoTitleText.text = "상의 안내";
            categoryInfoBodyText.text = GetSelectedOutfitDescription(CategoryUpper, outfitManager != null ? outfitManager.currentUpperIndex : 0, "원하는 상의를 선택하면 AR로 착용해 볼 수 있습니다.");
            SetCategoryInfoThumbnail(GetThumbnail(CategoryUpper, outfitManager != null ? outfitManager.currentUpperIndex : 0), "T");
        }
        else if (activeCategory == CategoryLower)
        {
            categoryInfoTitleText.text = "하의 안내";
            categoryInfoBodyText.text = GetSelectedOutfitDescription(CategoryLower, outfitManager != null ? outfitManager.currentLowerIndex : 0, "원하는 하의를 선택하면 AR로 착용해 볼 수 있습니다.");
            SetCategoryInfoThumbnail(GetThumbnail(CategoryLower, outfitManager != null ? outfitManager.currentLowerIndex : 0), "P");
        }
        else if (activeCategory == CategoryHat)
        {
            categoryInfoTitleText.text = "모자 준비 중";
            categoryInfoBodyText.text = "모자 카테고리는 추후 OutfitManager 연동 예정입니다.";
            SetCategoryInfoThumbnail(null, "H");
        }
        else
        {
            categoryInfoTitleText.text = "신발 준비 중";
            categoryInfoBodyText.text = "신발 카테고리는 추후 OutfitManager 연동 예정입니다.";
            SetCategoryInfoThumbnail(null, "S");
        }
    }

    private void RefreshWishlistPanel()
    {
        if (wishlistContent == null)
            return;

        ClearChildren(wishlistContent);

        if (wishlistBodyText != null)
            wishlistBodyText.gameObject.SetActive(true);
    }

    private void SelectWishlistCategory(string categoryCode)
    {
        activeWishlistCategory = categoryCode;
        RebuildWishlistList();
        RefreshWishlistTabs();
    }

    private void RebuildWishlistList()
    {
        if (wishlistContent == null)
            return;

        ClearChildren(wishlistContent);

        int visibleCount = 0;
        for (int i = 0; i < wishlistOutfits.Count; i++)
        {
            DbOutfit outfit = wishlistOutfits[i];
            if (!ShouldShowWishlistOutfit(outfit))
                continue;

            CreateWishlistItem(wishlistContent, outfit);
            visibleCount++;
        }

        if (wishlistBodyText != null)
        {
            wishlistBodyText.gameObject.SetActive(visibleCount == 0);
            wishlistBodyText.text = wishlistOutfits.Count == 0
                ? "찜한 옷이 없습니다."
                : "선택한 카테고리에 찜한 옷이 없습니다.";
        }

        RefreshWishlistTabs();
    }

    private bool ShouldShowWishlistOutfit(DbOutfit outfit)
    {
        if (activeWishlistCategory == "all")
            return true;

        if (outfit == null)
            return false;

        return outfit.unity_category_code == activeWishlistCategory || outfit.category_code == activeWishlistCategory;
    }

    private void RefreshWishlistTabs()
    {
        foreach (KeyValuePair<string, Image> pair in wishlistTabBorders)
        {
            bool selected = pair.Key == activeWishlistCategory;
            pair.Value.color = selected ? blue : new Color(1f, 1f, 1f, 0.16f);
        }

        foreach (KeyValuePair<string, Text> pair in wishlistTabTexts)
            pair.Value.color = pair.Key == activeWishlistCategory ? Color.white : mutedText;
    }

    private void SetCategoryInfoThumbnail(Sprite sprite, string fallbackText)
    {
        SetThumbnailImage(categoryInfoThumbnailImage, sprite);

        if (categoryInfoIconText == null)
            return;

        categoryInfoIconText.text = sprite == null ? fallbackText : string.Empty;
        categoryInfoIconText.raycastTarget = false;
    }

    private void SetThumbnailImage(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite != null ? sprite : GetCircleSprite();
        image.color = sprite != null ? Color.white : new Color(0.18f, 0.21f, 0.26f, 0.95f);
        image.preserveAspect = true;
    }

    private IEnumerator LoginRoutine()
    {
        string phone = loginPhoneInput != null ? loginPhoneInput.text.Trim() : string.Empty;
        string password = loginPasswordInput != null ? loginPasswordInput.text : string.Empty;

        if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password))
        {
            Debug.LogWarning("[FitRoomUI] 전화번호와 비밀번호를 입력하세요.");
            yield break;
        }

        string json = "{\"phone\":\"" + EscapeJson(phone) + "\",\"password\":\"" + EscapeJson(password) + "\"}";
        yield return PostJson("/api/auth/login", json, response =>
        {
            AuthResponse parsed = JsonUtility.FromJson<AuthResponse>(response);
            if (parsed != null && parsed.ok && parsed.data != null)
            {
                loggedInUserId = parsed.data.user_id;
                loggedInUserName = parsed.data.name;
                loggedInUserPhone = parsed.data.phone;
                RefreshUserStatus();
                StartCoroutine(LoadFavoriteIdsRoutine());
                ShowMainPanel();
                Debug.Log("[FitRoomUI] login success: " + loggedInUserName);
            }
            else
            {
                Debug.LogWarning("[FitRoomUI] login failed: " + response);
            }
        });
    }

    private IEnumerator RegisterRoutine()
    {
        string name = registerNameInput != null ? registerNameInput.text.Trim() : string.Empty;
        string phone = registerPhoneInput != null ? registerPhoneInput.text.Trim() : string.Empty;
        string password = registerPasswordInput != null ? registerPasswordInput.text : string.Empty;
        string confirm = registerPasswordConfirmInput != null ? registerPasswordConfirmInput.text : string.Empty;

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password))
        {
            Debug.LogWarning("[FitRoomUI] 이름, 전화번호, 비밀번호를 입력하세요.");
            yield break;
        }

        if (password != confirm)
        {
            Debug.LogWarning("[FitRoomUI] 비밀번호 확인이 일치하지 않습니다.");
            yield break;
        }

        string json =
            "{\"name\":\"" + EscapeJson(name) +
            "\",\"phone\":\"" + EscapeJson(phone) +
            "\",\"password\":\"" + EscapeJson(password) + "\"}";

        yield return PostJson("/api/auth/register", json, response =>
        {
            AuthResponse parsed = JsonUtility.FromJson<AuthResponse>(response);
            if (parsed != null && parsed.ok && parsed.data != null)
            {
                loggedInUserId = parsed.data.user_id;
                loggedInUserName = parsed.data.name;
                loggedInUserPhone = parsed.data.phone;
                RefreshUserStatus();
                StartCoroutine(LoadFavoriteIdsRoutine());
                ShowMainPanel();
                Debug.Log("[FitRoomUI] register success: " + loggedInUserName);
            }
            else
            {
                Debug.LogWarning("[FitRoomUI] register failed: " + response);
            }
        });
    }

    private IEnumerator LoadCategoryOutfitsRoutine(string categoryCode)
    {
        yield return GetJson("/api/outfits?category_code=" + UnityWebRequest.EscapeURL(categoryCode), response =>
        {
            OutfitListResponse parsed = JsonUtility.FromJson<OutfitListResponse>(response);
            if (parsed == null || !parsed.ok || parsed.data == null)
            {
                Debug.LogWarning("[FitRoomUI] outfit metadata load failed: " + response);
                return;
            }

            for (int i = 0; i < parsed.data.Length; i++)
            {
                DbOutfit outfit = parsed.data[i];
                dbOutfitsByUnityKey[GetFavoriteKey(outfit.unity_category_code, outfit.unity_outfit_index)] = outfit;
            }

            RebuildCarousel();
            RefreshCurrentOutfitPanel();
            RefreshCategoryInfoPanel();
        });
    }

    private IEnumerator LoadFavoriteIdsRoutine()
    {
        if (loggedInUserId <= 0)
            yield break;

        yield return GetJson("/api/users/" + loggedInUserId + "/favorites", response =>
        {
            OutfitListResponse parsed = JsonUtility.FromJson<OutfitListResponse>(response);
            favoriteOutfitIds.Clear();

            if (parsed != null && parsed.ok && parsed.data != null)
            {
                for (int i = 0; i < parsed.data.Length; i++)
                    favoriteOutfitIds.Add(parsed.data[i].outfit_id);
            }

            RefreshSelectedCardState();
        });
    }

    private IEnumerator LoadWishlistRoutine()
    {
        if (wishlistContent == null)
            yield break;

        ClearChildren(wishlistContent);
        wishlistOutfits.Clear();

        if (loggedInUserId <= 0)
        {
            if (wishlistBodyText != null)
            {
                wishlistBodyText.gameObject.SetActive(true);
                wishlistBodyText.text = "로그인 후 찜 목록을 확인할 수 있습니다.";
            }
            yield break;
        }

        if (wishlistBodyText != null)
        {
            wishlistBodyText.gameObject.SetActive(true);
            wishlistBodyText.text = "찜 목록을 불러오는 중입니다.";
        }

        yield return GetJson("/api/users/" + loggedInUserId + "/favorites", response =>
        {
            OutfitListResponse parsed = JsonUtility.FromJson<OutfitListResponse>(response);
            ClearChildren(wishlistContent);
            favoriteOutfitIds.Clear();

            if (parsed == null || !parsed.ok || parsed.data == null || parsed.data.Length == 0)
            {
                if (wishlistBodyText != null)
                {
                    wishlistBodyText.gameObject.SetActive(true);
                    wishlistBodyText.text = "찜한 옷이 없습니다.";
                }
                return;
            }

            if (wishlistBodyText != null)
                wishlistBodyText.gameObject.SetActive(false);

            for (int i = 0; i < parsed.data.Length; i++)
            {
                DbOutfit outfit = parsed.data[i];
                favoriteOutfitIds.Add(outfit.outfit_id);
                dbOutfitsByUnityKey[GetFavoriteKey(outfit.unity_category_code, outfit.unity_outfit_index)] = outfit;
                wishlistOutfits.Add(outfit);
            }

            RebuildWishlistList();
            RefreshSelectedCardState();
        });
    }

    private IEnumerator ToggleFavoriteRoutine(DbOutfit outfit)
    {
        string json = "{\"user_id\":" + loggedInUserId + ",\"outfit_id\":" + outfit.outfit_id + "}";

        yield return PostJson("/api/favorites/toggle", json, response =>
        {
            FavoriteToggleResponse parsed = JsonUtility.FromJson<FavoriteToggleResponse>(response);
            if (parsed != null && parsed.ok && parsed.data != null)
            {
                if (parsed.data.is_favorite)
                    favoriteOutfitIds.Add(outfit.outfit_id);
                else
                    favoriteOutfitIds.Remove(outfit.outfit_id);

                RefreshSelectedCardState();

                if (wishlistPanel != null && wishlistPanel.activeSelf)
                    StartCoroutine(LoadWishlistRoutine());
            }
            else
            {
                Debug.LogWarning("[FitRoomUI] favorite toggle failed: " + response);
            }
        });
    }

    private IEnumerator RemoveFavoriteRoutine(DbOutfit outfit)
    {
        string json = "{\"user_id\":" + loggedInUserId + ",\"outfit_id\":" + outfit.outfit_id + "}";

        yield return PostJson("/api/favorites/remove", json, response =>
        {
            FavoriteToggleResponse parsed = JsonUtility.FromJson<FavoriteToggleResponse>(response);
            if (parsed != null && parsed.ok)
            {
                favoriteOutfitIds.Remove(outfit.outfit_id);
                RefreshSelectedCardState();
                StartCoroutine(LoadWishlistRoutine());
            }
            else
            {
                Debug.LogWarning("[FitRoomUI] favorite remove failed: " + response);
            }
        });
    }

    private IEnumerator LoadUserInfoRoutine()
    {
        if (loggedInUserId <= 0)
            yield break;

        yield return GetJson("/api/users/" + loggedInUserId, response =>
        {
            AuthResponse parsed = JsonUtility.FromJson<AuthResponse>(response);
            if (parsed != null && parsed.ok && parsed.data != null)
            {
                loggedInUserName = parsed.data.name;
                loggedInUserPhone = parsed.data.phone;
                RefreshUserStatus();
                RefreshUserInfoPanel();
            }
            else
            {
                Debug.LogWarning("[FitRoomUI] user info load failed: " + response);
            }
        });
    }

    private IEnumerator ChangePasswordRoutine()
    {
        if (loggedInUserId <= 0)
        {
            ShowLoginPanel();
            yield break;
        }

        string currentPassword = currentPasswordInput != null ? currentPasswordInput.text : string.Empty;
        string newPassword = newPasswordInput != null ? newPasswordInput.text : string.Empty;
        string confirm = newPasswordConfirmInput != null ? newPasswordConfirmInput.text : string.Empty;

        if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
        {
            Debug.LogWarning("[FitRoomUI] 현재 비밀번호와 새 비밀번호를 입력하세요.");
            yield break;
        }

        if (newPassword != confirm)
        {
            Debug.LogWarning("[FitRoomUI] 새 비밀번호 확인이 일치하지 않습니다.");
            yield break;
        }

        string json =
            "{\"current_password\":\"" + EscapeJson(currentPassword) +
            "\",\"new_password\":\"" + EscapeJson(newPassword) + "\"}";

        yield return PostJson("/api/users/" + loggedInUserId + "/password", json, response =>
        {
            ApiBasicResponse parsed = JsonUtility.FromJson<ApiBasicResponse>(response);
            if (parsed != null && parsed.ok)
            {
                ClearPasswordInputs();
                ShowMainPanel();
                Debug.Log("[FitRoomUI] password changed.");
            }
            else
            {
                Debug.LogWarning("[FitRoomUI] password change failed: " + response);
            }
        });
    }

    private void Logout()
    {
        loggedInUserId = 0;
        loggedInUserName = string.Empty;
        loggedInUserPhone = string.Empty;
        ClearPasswordInputs();
        RefreshUserStatus();
        ShowMainPanel();
        Debug.Log("[FitRoomUI] logged out.");
    }

    private IEnumerator PostJson(string path, string json, System.Action<string> onSuccess)
    {
        string url = apiBaseUrl.TrimEnd('/') + path;
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[FitRoomUI] API request failed: " + url + " / " + request.error);
                yield break;
            }

            if (onSuccess != null)
                onSuccess(request.downloadHandler.text);
        }
    }

    private IEnumerator GetJson(string path, System.Action<string> onSuccess)
    {
        string url = apiBaseUrl.TrimEnd('/') + path;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[FitRoomUI] API request failed: " + url + " / " + request.error);
                yield break;
            }

            if (onSuccess != null)
                onSuccess(request.downloadHandler.text);
        }
    }

    private void RefreshUserStatus()
    {
        if (userStatusText == null)
            return;

        if (loggedInUserId > 0)
            userStatusText.text = loggedInUserName + "님";
        else
            userStatusText.text = "로그인";
    }

    private void RefreshUserInfoPanel()
    {
        if (userInfoNameText != null)
            userInfoNameText.text = string.IsNullOrEmpty(loggedInUserName) ? "-" : loggedInUserName;

        if (userInfoPhoneText != null)
            userInfoPhoneText.text = string.IsNullOrEmpty(loggedInUserPhone) ? "-" : loggedInUserPhone;
    }

    private void ClearPasswordInputs()
    {
        if (currentPasswordInput != null)
            currentPasswordInput.text = string.Empty;

        if (newPasswordInput != null)
            newPasswordInput.text = string.Empty;

        if (newPasswordConfirmInput != null)
            newPasswordConfirmInput.text = string.Empty;
    }

    private string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }

    private void EnsureRuntimeLayout()
    {
        if (runtimeLayoutBuilt && mainPanel != null && carouselContent != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = CreateCanvas(transform);

        categoryButtons.Clear();
        categoryButtonTexts.Clear();
        categoryButtonBorders.Clear();
        outfitCards.Clear();

        ClearGeneratedMainPanel(canvas.transform);
        mainPanel = CreateMainPanel(canvas.transform);

        CreateHeaderPanel(mainPanel.transform);
        CreateCurrentOutfitPanel(mainPanel.transform);
        CreatePreviewFrame(mainPanel.transform);
        CreateRightControlPanel(mainPanel.transform);
        CreateOutfitCarouselPanel(mainPanel.transform);
        wishlistPanel = CreateWishlistPanel(mainPanel.transform);
        loginPanel = CreateLoginPanel(mainPanel.transform);
        registerPanel = CreateRegisterPanel(mainPanel.transform);
        userInfoPanel = CreateUserInfoPanel(mainPanel.transform);

        EnsureEventSystem();
        RefreshCurrentOutfitPanel();
        RefreshCategoryInfoPanel();
        runtimeLayoutBuilt = true;
    }

    private Canvas CreateCanvas(Transform controller)
    {
        GameObject canvasObject = new GameObject("FitRoom Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        controller.SetParent(canvasObject.transform, false);
        return canvas;
    }

    private GameObject CreateMainPanel(Transform parent)
    {
        GameObject panel = CreateRect("MainPanel", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Stretch(panel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.02f, 0.039f, 0.071f, 0.16f);
        image.raycastTarget = false;
        return panel;
    }

    private void CreateHeaderPanel(Transform parent)
    {
        GameObject header = CreateRect("HeaderPanel", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        header.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 108f);
        header.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        AddGlass(header, new Color(0.027f, 0.043f, 0.071f, 0.9f), 0.18f);

        GameObject logo = CreateRoundImage("LogoIcon", header.transform, new Color(0.04f, 0.18f, 0.36f, 0.95f), cyan, 54f);
        SetRect(logo.GetComponent<RectTransform>(), new Vector2(36f, -27f), new Vector2(54f, 54f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        Text logoText = CreateText("LogoText", logo.transform, "F", 28, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(logoText.rectTransform, 0f, 0f, 0f, 0f);

        Text title = CreateText("Title", header.transform, "FitRoom", 34, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(title.rectTransform, new Vector2(108f, -30f), new Vector2(260f, 42f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        Text subtitle = CreateText("Subtitle", header.transform, "AR 가상 피팅룸", 18, FontStyle.Normal, TextAnchor.MiddleLeft);
        subtitle.color = mutedText;
        SetRect(subtitle.rectTransform, new Vector2(110f, -70f), new Vector2(320f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        Button userButton = CreateIconTextButton("UserStatusButton", header.transform, "로그인", "U", new Vector2(-340f, -25f), new Vector2(190f, 58f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        userStatusText = userButton.GetComponentInChildren<Text>();
        userButton.onClick.AddListener(() =>
        {
            if (loggedInUserId > 0)
                ShowUserInfoPanel();
            else
                ShowLoginPanel();
        });

        Button wishlistButton = CreateIconTextButton("WishlistButton", header.transform, "찜 목록", "♡", new Vector2(-126f, -25f), new Vector2(170f, 58f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        wishlistButton.onClick.AddListener(ShowWishlistPanel);

        GameObject line = CreateRect("BottomDivider", header.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        line.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 1f);
        Image lineImage = line.AddComponent<Image>();
        lineImage.color = new Color(0.118f, 0.482f, 1f, 0.36f);
        lineImage.raycastTarget = false;
    }

    private void CreateCurrentOutfitPanel(Transform parent)
    {
        GameObject panel = CreateAnchoredPanel("CurrentOutfitPanel", parent, new Vector2(40f, -150f), new Vector2(360f, 570f), new Vector2(0f, 1f), glassColor);
        Text title = CreateText("Title", panel.transform, "현재 착용 정보", 24, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(title.rectTransform, new Vector2(24f, -24f), new Vector2(280f, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        currentUpperNameText = CreateCurrentOutfitItem(panel.transform, "CurrentUpperItem", "상의", GetUpperName(outfitManager != null ? outfitManager.currentUpperIndex : 0), 82f, out currentUpperThumbnailImage);
        currentLowerNameText = CreateCurrentOutfitItem(panel.transform, "CurrentLowerItem", "하의", GetLowerName(outfitManager != null ? outfitManager.currentLowerIndex : 0), 184f, out currentLowerThumbnailImage);
        currentHatNameText = CreateCurrentOutfitItem(panel.transform, "CurrentHatItem", "모자", "선택 없음", 286f, out currentHatThumbnailImage);
        currentShoesNameText = CreateCurrentOutfitItem(panel.transform, "CurrentShoesItem", "신발", "선택 없음", 388f, out currentShoesThumbnailImage);

        Button saveButton = CreateNeonButton("SaveCurrentOutfitButton", panel.transform, "♡ 현재 코디 저장하기", new Vector2(24f, -506f), new Vector2(312f, 46f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        saveButton.onClick.AddListener(() => Debug.Log("[FitRoomUI] current outfit saved locally."));
    }

    private Text CreateCurrentOutfitItem(Transform parent, string objectName, string category, string outfitName, float top, out Image thumbnailImage)
    {
        GameObject item = CreateAnchoredPanel(objectName, parent, new Vector2(24f, -top), new Vector2(312f, 82f), new Vector2(0f, 1f), new Color(0.082f, 0.106f, 0.145f, 0.88f));
        GameObject thumb = CreateRoundImage("Thumbnail", item.transform, new Color(0.18f, 0.21f, 0.26f, 0.95f), new Color(1f, 1f, 1f, 0.14f), 50f);
        SetRect(thumb.GetComponent<RectTransform>(), new Vector2(16f, -16f), new Vector2(50f, 50f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        thumbnailImage = thumb.GetComponent<Image>();
        thumbnailImage.preserveAspect = true;

        Text categoryText = CreateText("Category", item.transform, category, 15, FontStyle.Normal, TextAnchor.MiddleLeft);
        categoryText.color = mutedText;
        SetRect(categoryText.rectTransform, new Vector2(82f, -14f), new Vector2(190f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        Text nameText = CreateText("OutfitName", item.transform, outfitName, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(nameText.rectTransform, new Vector2(82f, -42f), new Vector2(200f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        return nameText;
    }

    private void CreatePreviewFrame(Transform parent)
    {
        GameObject frame = CreateRect("PreviewFrame", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        frame.GetComponent<RectTransform>().anchoredPosition = new Vector2(-200f, -120f);
        frame.GetComponent<RectTransform>().sizeDelta = new Vector2(860f, 630f);

        Image image = frame.AddComponent<Image>();
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = new Color(0f, 0f, 0f, 0.02f);
        image.raycastTarget = false;

        Outline outline = frame.AddComponent<Outline>();
        outline.effectColor = new Color(0.118f, 0.482f, 1f, 0.65f);
        outline.effectDistance = new Vector2(2f, 2f);

        GameObject border = CreateRect("BorderOnly", frame.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Stretch(border.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Image borderImage = border.AddComponent<Image>();
        borderImage.sprite = GetRoundedSprite();
        borderImage.type = Image.Type.Sliced;
        borderImage.color = new Color(1f, 1f, 1f, 0.035f);
        borderImage.raycastTarget = false;
    }

    private void CreateRightControlPanel(Transform parent)
    {
        GameObject panel = CreateAnchoredPanel("RightControlPanel", parent, new Vector2(-40f, -145f), new Vector2(540f, 550f), new Vector2(1f, 1f), glassColor);
        CreateCategoryTabPanel(panel.transform);
        CreateCategoryInfoPanel(panel.transform);
        CreateFilterSortPanel(panel.transform);
    }

    private void CreateCategoryTabPanel(Transform parent)
    {
        GameObject tabs = CreateRect("CategoryTabPanel", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        tabs.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -24f);
        tabs.GetComponent<RectTransform>().sizeDelta = new Vector2(-48f, 78f);

        HorizontalLayoutGroup layout = tabs.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;

        CreateCategoryTabButton(tabs.transform, "UpperTabButton", CategoryUpper, "상의", "T");
        CreateCategoryTabButton(tabs.transform, "LowerTabButton", CategoryLower, "하의", "P");
        CreateCategoryTabButton(tabs.transform, "HatTabButton", CategoryHat, "모자", "H");
        CreateCategoryTabButton(tabs.transform, "ShoesTabButton", CategoryShoes, "신발", "S");
    }

    private void CreateCategoryTabButton(Transform parent, string objectName, string categoryCode, string label, string icon)
    {
        GameObject obj = CreatePanelObject(objectName, parent, new Color(0.057f, 0.078f, 0.114f, 0.92f), new Color(1f, 1f, 1f, 0.16f));
        Button button = obj.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = obj.GetComponent<Image>();
        button.onClick.AddListener(() => SelectCategory(categoryCode));
        obj.AddComponent<LayoutElement>().preferredHeight = 68f;

        Text text = CreateText("Label", obj.transform, icon + "\n" + label, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.lineSpacing = 1.05f;
        Stretch(text.rectTransform, 4f, 4f, -4f, -4f);

        categoryButtons[categoryCode] = button;
        categoryButtonTexts[categoryCode] = text;
        categoryButtonBorders[categoryCode] = obj.GetComponent<OutlineHolder>().outlineImage;
    }

    private void CreateCategoryInfoPanel(Transform parent)
    {
        GameObject panel = CreateAnchoredPanel("CategoryInfoPanel", parent, new Vector2(24f, -128f), new Vector2(492f, 250f), new Vector2(0f, 1f), new Color(0.047f, 0.07f, 0.11f, 0.82f));

        categoryInfoTitleText = CreateText("Title", panel.transform, "상의 안내", 25, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(categoryInfoTitleText.rectTransform, new Vector2(24f, -24f), new Vector2(260f, 36f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        categoryInfoBodyText = CreateText("Description", panel.transform, "원하는 상의를 선택하면 AR로 착용해 볼 수 있습니다.", 18, FontStyle.Normal, TextAnchor.UpperLeft);
        categoryInfoBodyText.color = mutedText;
        SetRect(categoryInfoBodyText.rectTransform, new Vector2(24f, -76f), new Vector2(300f, 112f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        GameObject iconBack = CreateRoundImage("SelectedOutfitImage", panel.transform, new Color(0.02f, 0.14f, 0.32f, 0.82f), blue, 116f);
        SetRect(iconBack.GetComponent<RectTransform>(), new Vector2(-150f, -62f), new Vector2(116f, 116f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        categoryInfoThumbnailImage = iconBack.GetComponent<Image>();
        categoryInfoThumbnailImage.preserveAspect = true;
        categoryInfoIconText = CreateText("IconText", iconBack.transform, "T", 48, FontStyle.Bold, TextAnchor.MiddleCenter);
        categoryInfoIconText.color = cyan;
        Stretch(categoryInfoIconText.rectTransform, 0f, 0f, 0f, 0f);
    }

    private void CreateFilterSortPanel(Transform parent)
    {
        GameObject panel = CreateRect("FilterSortPanel", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        panel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 28f);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(-48f, 64f);

        HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;

        Button filter = CreateNeonButton("FilterButton", panel.transform, "필터", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        filter.onClick.AddListener(() => Debug.Log("[FitRoomUI] filter coming soon."));
        Button sort = CreateNeonButton("SortButton", panel.transform, "정렬", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        sort.onClick.AddListener(() => Debug.Log("[FitRoomUI] sort coming soon."));
    }

    private void CreateOutfitCarouselPanel(Transform parent)
    {
        GameObject panel = CreateAnchoredPanel("OutfitCarouselPanel", parent, new Vector2(40f, 80f), new Vector2(1565f, 220f), new Vector2(0f, 0f), new Color(0.043f, 0.067f, 0.11f, 0.86f));

        Button left = CreateNeonButton("LeftArrowButton", panel.transform, "<", new Vector2(18f, 70f), new Vector2(48f, 80f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        left.onClick.AddListener(() => NudgeCarousel(-0.22f));

        Button right = CreateNeonButton("RightArrowButton", panel.transform, ">", new Vector2(-20f, 70f), new Vector2(48f, 80f), new Vector2(1f, 0f), new Vector2(1f, 0f));
        right.onClick.AddListener(() => NudgeCarousel(0.22f));

        GameObject scroll = CreateRect("ScrollView", panel.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Stretch(scroll.GetComponent<RectTransform>(), 82f, 18f, -82f, -18f);
        carouselScrollRect = scroll.AddComponent<ScrollRect>();
        carouselScrollRect.horizontal = true;
        carouselScrollRect.vertical = false;
        carouselScrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CreateRect("Viewport", scroll.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Stretch(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        GameObject content = CreateRect("Content", viewport.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        content.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        HorizontalLayoutGroup layout = content.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 0, 0);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        carouselContent = content.transform;
        carouselScrollRect.viewport = viewport.GetComponent<RectTransform>();
        carouselScrollRect.content = content.GetComponent<RectTransform>();
    }

    private OutfitCardButton CreateOutfitCard(Transform parent, string categoryCode, int index)
    {
        GameObject card = CreatePanelObject("OutfitCard_" + categoryCode + "_" + index, parent, cardColor, new Color(1f, 1f, 1f, 0.18f));
        card.GetComponent<RectTransform>().sizeDelta = new Vector2(205f, 180f);
        LayoutElement layout = card.AddComponent<LayoutElement>();
        layout.preferredWidth = 205f;
        layout.preferredHeight = 180f;

        Button button = card.AddComponent<Button>();
        button.targetGraphic = card.GetComponent<Image>();

        GameObject thumbnail = CreateRect("Thumbnail", card.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        thumbnail.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -18f);
        thumbnail.GetComponent<RectTransform>().sizeDelta = new Vector2(-34f, 104f);
        Image thumbImage = thumbnail.AddComponent<Image>();
        thumbImage.sprite = GetThumbnail(categoryCode, index);
        thumbImage.color = thumbImage.sprite != null ? Color.white : new Color(0.22f, 0.25f, 0.31f, 1f);
        thumbImage.preserveAspect = true;

        Button heart = CreateSmallIconButton("FavoriteButton", card.transform, "♡", new Vector2(-34f, -18f));
        Text name = CreateText("NameText", card.transform, GetOutfitName(categoryCode, index), 17, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(name.rectTransform, new Vector2(12f, 12f), new Vector2(181f, 34f), new Vector2(0f, 0f), new Vector2(0f, 0f));

        Text check = CreateText("CheckMark", card.transform, "✓", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        check.color = cyan;
        SetRect(check.rectTransform, new Vector2(14f, -16f), new Vector2(34f, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        OutfitCardButton cardButton = card.AddComponent<OutfitCardButton>();
        cardButton.categoryCode = categoryCode;
        cardButton.index = index;
        cardButton.button = button;
        cardButton.favoriteButton = heart;
        cardButton.borderImage = card.GetComponent<OutlineHolder>().outlineImage;
        cardButton.favoriteText = heart.GetComponentInChildren<Text>();
        cardButton.nameText = name;
        cardButton.checkMark = check.gameObject;
        return cardButton;
    }

    private void CreateComingSoonCard(Transform parent, string categoryCode)
    {
        GameObject card = CreatePanelObject("ComingSoonCard_" + categoryCode, parent, cardColor, new Color(1f, 1f, 1f, 0.18f));
        card.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 180f);
        LayoutElement layout = card.AddComponent<LayoutElement>();
        layout.preferredWidth = 360f;
        layout.preferredHeight = 180f;

        string label = categoryCode == CategoryHat || categoryCode == CategoryShoes ? "Coming Soon" : "등록된 의상이 없습니다.";
        Text text = CreateText("Message", card.transform, label, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.color = mutedText;
        Stretch(text.rectTransform, 20f, 20f, -20f, -20f);
    }

    private GameObject CreateWishlistPanel(Transform parent)
    {
        GameObject overlay = CreateModalPanel("WishlistPanel", parent, "찜 목록");
        Transform dialog = overlay.transform.Find("Dialog");
        Transform target = dialog != null ? dialog : overlay.transform;
        wishlistBodyText = CreateText("WishlistPlaceholder", target, "찜한 옷이 없습니다.", 20, FontStyle.Normal, TextAnchor.UpperLeft);
        wishlistBodyText.color = mutedText;
        SetRect(wishlistBodyText.rectTransform, new Vector2(56f, -150f), new Vector2(488f, 42f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        CreateWishlistCategoryTabPanel(target);

        GameObject scroll = CreateRect("WishlistScrollView", target, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Stretch(scroll.GetComponent<RectTransform>(), 46f, 34f, -46f, -142f);
        ScrollRect scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CreateRect("Viewport", scroll.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Stretch(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        GameObject content = CreateRect("Content", viewport.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        content.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 0f);
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        wishlistContent = content.transform;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content.GetComponent<RectTransform>();
        RefreshWishlistTabs();
        overlay.SetActive(false);
        return overlay;
    }

    private void CreateWishlistCategoryTabPanel(Transform parent)
    {
        GameObject tabs = CreateRect("WishlistCategoryTabPanel", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        tabs.GetComponent<RectTransform>().anchoredPosition = new Vector2(56f, -86f);
        tabs.GetComponent<RectTransform>().sizeDelta = new Vector2(488f, 42f);

        HorizontalLayoutGroup layout = tabs.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;

        CreateWishlistTabButton(tabs.transform, "WishlistAllTabButton", "all", "전체");
        CreateWishlistTabButton(tabs.transform, "WishlistUpperTabButton", CategoryUpper, "상의");
        CreateWishlistTabButton(tabs.transform, "WishlistLowerTabButton", CategoryLower, "하의");
        CreateWishlistTabButton(tabs.transform, "WishlistHatTabButton", CategoryHat, "모자");
        CreateWishlistTabButton(tabs.transform, "WishlistShoesTabButton", CategoryShoes, "신발");
    }

    private void CreateWishlistTabButton(Transform parent, string objectName, string categoryCode, string label)
    {
        GameObject obj = CreatePanelObject(objectName, parent, new Color(0.057f, 0.078f, 0.114f, 0.92f), new Color(1f, 1f, 1f, 0.16f));
        Button button = obj.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = obj.GetComponent<Image>();
        button.onClick.AddListener(() => SelectWishlistCategory(categoryCode));

        Text text = CreateText("Label", obj.transform, label, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, 4f, 2f, -4f, -2f);

        wishlistTabTexts[categoryCode] = text;
        wishlistTabBorders[categoryCode] = obj.GetComponent<OutlineHolder>().outlineImage;
    }

    private void CreateWishlistItem(Transform parent, DbOutfit outfit)
    {
        GameObject item = CreatePanelObject("WishlistItem_" + outfit.outfit_id, parent, new Color(0.082f, 0.106f, 0.145f, 0.92f), new Color(1f, 1f, 1f, 0.16f));
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 92f);

        LayoutElement layout = item.AddComponent<LayoutElement>();
        layout.preferredHeight = 92f;
        layout.minHeight = 92f;
        layout.flexibleWidth = 1f;

        GameObject thumb = CreateRoundImage("Thumbnail", item.transform, new Color(0.18f, 0.21f, 0.26f, 0.95f), new Color(1f, 1f, 1f, 0.14f), 58f);
        SetRect(thumb.GetComponent<RectTransform>(), new Vector2(16f, -17f), new Vector2(58f, 58f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        Image thumbnailImage = thumb.GetComponent<Image>();
        SetThumbnailImage(thumbnailImage, GetThumbnail(outfit.unity_category_code, outfit.unity_outfit_index));

        Text category = CreateText("Category", item.transform, GetCategoryDisplayName(outfit), 15, FontStyle.Normal, TextAnchor.MiddleLeft);
        category.color = mutedText;
        SetRect(category.rectTransform, new Vector2(92f, -16f), new Vector2(190f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        Text name = CreateText("OutfitName", item.transform, GetDbOutfitName(outfit), 18, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(name.rectTransform, new Vector2(92f, -46f), new Vector2(210f, 30f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        Button wear = CreateNeonButton("WearButton", item.transform, "착용", new Vector2(-150f, -24f), new Vector2(82f, 44f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        wear.onClick.AddListener(() =>
        {
            SelectOutfit(outfit.unity_category_code, outfit.unity_outfit_index);
            SelectCategory(outfit.unity_category_code);
            ShowMainPanel();
        });

        Button delete = CreateGrayButton("DeleteButton", item.transform, "삭제", new Vector2(-54f, -24f), new Vector2(82f, 44f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        delete.onClick.AddListener(() => StartCoroutine(RemoveFavoriteRoutine(outfit)));
    }

    private GameObject CreateLoginPanel(Transform parent)
    {
        GameObject overlay = CreateModalPanel("LoginPanel", parent, "로그인");
        Transform dialog = overlay.transform.Find("Dialog");
        Transform target = dialog != null ? dialog : overlay.transform;
        loginPhoneInput = CreateInputField(overlay.transform, "PhoneInput", "전화번호", 116f, false);
        loginPasswordInput = CreateInputField(overlay.transform, "PasswordInput", "비밀번호", 180f, true);
        Button login = CreateNeonButton("LoginButton", target, "로그인", new Vector2(56f, -258f), new Vector2(220f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        login.onClick.AddListener(() => StartCoroutine(LoginRoutine()));
        Button register = CreateNeonButton("GoRegisterButton", target, "회원가입", new Vector2(300f, -258f), new Vector2(220f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        register.onClick.AddListener(ShowRegisterPanel);
        overlay.SetActive(false);
        return overlay;
    }

    private GameObject CreateRegisterPanel(Transform parent)
    {
        GameObject overlay = CreateModalPanel("RegisterPanel", parent, "회원가입");
        Transform dialog = overlay.transform.Find("Dialog");
        Transform target = dialog != null ? dialog : overlay.transform;
        registerNameInput = CreateInputField(overlay.transform, "NameInput", "이름", 104f, false);
        registerPhoneInput = CreateInputField(overlay.transform, "PhoneInput", "전화번호", 158f, false);
        registerPasswordInput = CreateInputField(overlay.transform, "PasswordInput", "비밀번호", 212f, true);
        registerPasswordConfirmInput = CreateInputField(overlay.transform, "PasswordConfirmInput", "비밀번호 확인", 266f, true);
        Button register = CreateNeonButton("RegisterButton", target, "회원가입", new Vector2(56f, -330f), new Vector2(220f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        register.onClick.AddListener(() => StartCoroutine(RegisterRoutine()));
        Button back = CreateGrayButton("BackToLoginButton", target, "이전으로", new Vector2(300f, -330f), new Vector2(220f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        back.onClick.AddListener(ShowLoginPanel);
        overlay.SetActive(false);
        return overlay;
    }

    private GameObject CreateUserInfoPanel(Transform parent)
    {
        GameObject overlay = CreateModalPanel("UserInfoPanel", parent, "사용자 정보");
        Transform dialog = overlay.transform.Find("Dialog");
        Transform target = dialog != null ? dialog : overlay.transform;

        Text nameLabel = CreateText("NameLabel", target, "이름", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
        nameLabel.color = mutedText;
        SetRect(nameLabel.rectTransform, new Vector2(56f, -100f), new Vector2(100f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        userInfoNameText = CreateText("NameValue", target, "-", 19, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(userInfoNameText.rectTransform, new Vector2(170f, -100f), new Vector2(350f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        Text phoneLabel = CreateText("PhoneLabel", target, "전화번호", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
        phoneLabel.color = mutedText;
        SetRect(phoneLabel.rectTransform, new Vector2(56f, -142f), new Vector2(100f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        userInfoPhoneText = CreateText("PhoneValue", target, "-", 19, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(userInfoPhoneText.rectTransform, new Vector2(170f, -142f), new Vector2(350f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        currentPasswordInput = CreateInputField(overlay.transform, "CurrentPasswordInput", "현재 비밀번호", 194f, true);
        newPasswordInput = CreateInputField(overlay.transform, "NewPasswordInput", "새 비밀번호", 248f, true);
        newPasswordConfirmInput = CreateInputField(overlay.transform, "NewPasswordConfirmInput", "새 비밀번호 확인", 302f, true);

        Button change = CreateNeonButton("ChangePasswordButton", target, "변경완료", new Vector2(56f, -366f), new Vector2(220f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        change.onClick.AddListener(() => StartCoroutine(ChangePasswordRoutine()));

        Button logout = CreateGrayButton("LogoutButton", target, "로그아웃", new Vector2(300f, -366f), new Vector2(220f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        logout.onClick.AddListener(Logout);

        overlay.SetActive(false);
        return overlay;
    }

    private GameObject CreateModalPanel(string objectName, Transform parent, string titleText)
    {
        GameObject panel = CreateRect(objectName, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Stretch(panel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Image dim = panel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.48f);

        GameObject box = CreateAnchoredPanel("Dialog", panel.transform, Vector2.zero, new Vector2(600f, 430f), new Vector2(0.5f, 0.5f), new Color(0.043f, 0.067f, 0.11f, 0.96f));
        Text title = CreateText("Title", box.transform, titleText, 28, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(title.rectTransform, new Vector2(32f, -28f), new Vector2(400f, 42f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        Button close = CreateSmallIconButton("CloseButton", box.transform, "×", new Vector2(-44f, -28f));
        close.onClick.AddListener(ShowMainPanel);
        return panel;
    }

    private void CreateInputPlaceholder(Transform parent, string objectName, string placeholder, float top)
    {
        Transform dialog = parent.Find("Dialog");
        Transform target = dialog != null ? dialog : parent;
        GameObject input = CreateAnchoredPanel(objectName, target, new Vector2(56f, -top), new Vector2(464f, 44f), new Vector2(0f, 1f), new Color(0.082f, 0.106f, 0.145f, 0.95f));
        Text text = CreateText("Placeholder", input.transform, placeholder, 17, FontStyle.Normal, TextAnchor.MiddleLeft);
        text.color = mutedText;
        Stretch(text.rectTransform, 18f, 0f, -18f, 0f);
    }

    private InputField CreateInputField(Transform parent, string objectName, string placeholder, float top, bool isPassword)
    {
        Transform dialog = parent.Find("Dialog");
        Transform target = dialog != null ? dialog : parent;
        GameObject input = CreateAnchoredPanel(objectName, target, new Vector2(56f, -top), new Vector2(464f, 44f), new Vector2(0f, 1f), new Color(0.082f, 0.106f, 0.145f, 0.95f));

        InputField field = input.AddComponent<InputField>();
        field.transition = Selectable.Transition.ColorTint;
        field.targetGraphic = input.GetComponent<Image>();
        field.contentType = isPassword ? InputField.ContentType.Password : InputField.ContentType.Standard;
        field.lineType = InputField.LineType.SingleLine;

        Text text = CreateText("Text", input.transform, string.Empty, 17, FontStyle.Normal, TextAnchor.MiddleLeft);
        text.color = Color.white;
        Stretch(text.rectTransform, 18f, 0f, -18f, 0f);

        Text placeholderText = CreateText("Placeholder", input.transform, placeholder, 17, FontStyle.Normal, TextAnchor.MiddleLeft);
        placeholderText.color = mutedText;
        Stretch(placeholderText.rectTransform, 18f, 0f, -18f, 0f);

        field.textComponent = text;
        field.placeholder = placeholderText;
        return field;
    }

    private Button CreateIconTextButton(string objectName, Transform parent, string label, string icon, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 pivot)
    {
        Button button = CreateNeonButton(objectName, parent, label, position, size, anchorMin, pivot);
        Text iconText = CreateText("Icon", button.transform, icon, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
        iconText.color = cyan;
        SetRect(iconText.rectTransform, new Vector2(14f, 0f), new Vector2(34f, 50f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        Text labelText = button.GetComponentInChildren<Text>();
        if (labelText != null && labelText.name == "Label")
            Stretch(labelText.rectTransform, 46f, 4f, -12f, -4f);
        return button;
    }

    private Button CreateNeonButton(string objectName, Transform parent, string label, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 pivot)
    {
        GameObject obj = CreatePanelObject(objectName, parent, new Color(0.047f, 0.07f, 0.11f, 0.92f), new Color(0.118f, 0.482f, 1f, 0.58f));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMin;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Button button = obj.AddComponent<Button>();
        button.targetGraphic = obj.GetComponent<Image>();

        Text text = CreateText("Label", obj.transform, label, 17, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, 8f, 2f, -8f, -2f);
        return button;
    }

    private Button CreateGrayButton(string objectName, Transform parent, string label, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 pivot)
    {
        GameObject obj = CreatePanelObject(objectName, parent, new Color(0.12f, 0.13f, 0.15f, 0.92f), new Color(1f, 1f, 1f, 0.2f));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMin;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Button button = obj.AddComponent<Button>();
        button.targetGraphic = obj.GetComponent<Image>();

        Text text = CreateText("Label", obj.transform, label, 17, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.color = new Color(0.82f, 0.84f, 0.88f, 1f);
        Stretch(text.rectTransform, 8f, 2f, -8f, -2f);
        return button;
    }

    private Button CreateSmallIconButton(string objectName, Transform parent, string label, Vector2 topRight)
    {
        GameObject obj = CreateRoundImage(objectName, parent, new Color(0.047f, 0.07f, 0.11f, 0.95f), new Color(1f, 1f, 1f, 0.18f), 38f);
        SetRect(obj.GetComponent<RectTransform>(), topRight, new Vector2(38f, 38f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        Button button = obj.AddComponent<Button>();
        button.targetGraphic = obj.GetComponent<Image>();
        Text text = CreateText("Icon", obj.transform, label, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        return button;
    }

    private GameObject CreateAnchoredPanel(string objectName, Transform parent, Vector2 position, Vector2 size, Vector2 anchor, Color color)
    {
        GameObject panel = CreatePanelObject(objectName, parent, color, new Color(1f, 1f, 1f, 0.18f));
        SetRect(panel.GetComponent<RectTransform>(), position, size, anchor, anchor);
        return panel;
    }

    private GameObject CreatePanelObject(string objectName, Transform parent, Color color, Color borderColor)
    {
        GameObject obj = CreateRect(objectName, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        AddGlass(obj, color, 0f);

        GameObject border = CreateRect("Border", obj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Stretch(border.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Image borderImage = border.AddComponent<Image>();
        borderImage.sprite = GetRoundedSprite();
        borderImage.type = Image.Type.Sliced;
        borderImage.color = borderColor;
        borderImage.raycastTarget = false;

        OutlineHolder holder = obj.AddComponent<OutlineHolder>();
        holder.outlineImage = borderImage;
        return obj;
    }

    private void AddGlass(GameObject target, Color color, float outlineAlpha)
    {
        Image image = target.GetComponent<Image>();
        if (image == null)
            image = target.AddComponent<Image>();

        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = color;

        if (outlineAlpha > 0f)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = new Color(0.118f, 0.482f, 1f, outlineAlpha);
            outline.effectDistance = new Vector2(1.5f, 1.5f);
        }
    }

    private GameObject CreateRoundImage(string objectName, Transform parent, Color color, Color outlineColor, float size)
    {
        GameObject obj = CreateRect(objectName, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);

        Image image = obj.AddComponent<Image>();
        image.sprite = GetCircleSprite();
        image.color = color;

        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(1f, 1f);
        return obj;
    }

    private Text CreateText(string objectName, Transform parent, string text, int size, FontStyle style, TextAnchor alignment)
    {
        GameObject obj = CreateRect(objectName, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
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

    private GameObject CreateRect(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return obj;
    }

    private void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    private void ClearGeneratedMainPanel(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find("MainPanel");
        if (existing != null)
            DestroyRuntimeObject(existing.gameObject);
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            DestroyRuntimeObject(parent.GetChild(i).gameObject);
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    private void NudgeCarousel(float delta)
    {
        if (carouselScrollRect == null)
            return;

        carouselScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(carouselScrollRect.horizontalNormalizedPosition + delta);
    }

    private int GetCategoryCount(string categoryCode)
    {
        if (categoryCode == CategoryUpper)
            return outfitManager != null && outfitManager.upperOutfits != null ? outfitManager.upperOutfits.Length : 0;

        if (categoryCode == CategoryLower)
            return outfitManager != null && outfitManager.lowerOutfits != null ? outfitManager.lowerOutfits.Length : 0;

        return 0;
    }

    private bool IsSelected(string categoryCode, int index)
    {
        if (outfitManager == null)
            return false;

        if (categoryCode == CategoryUpper)
            return outfitManager.currentUpperIndex == index;

        if (categoryCode == CategoryLower)
            return outfitManager.currentLowerIndex == index;

        return false;
    }

    private string GetOutfitName(string categoryCode, int index)
    {
        DbOutfit dbOutfit = GetDbOutfit(categoryCode, index);
        if (dbOutfit != null && !string.IsNullOrEmpty(dbOutfit.outfit_name))
            return dbOutfit.outfit_name;

        if (categoryCode == CategoryUpper)
            return GetUpperName(index);

        if (categoryCode == CategoryLower)
            return GetLowerName(index);

        return "Coming Soon";
    }

    private string GetSelectedOutfitDescription(string categoryCode, int index, string fallback)
    {
        DbOutfit dbOutfit = GetDbOutfit(categoryCode, index);
        if (dbOutfit != null && !string.IsNullOrEmpty(dbOutfit.description))
            return dbOutfit.description;

        return fallback;
    }

    private DbOutfit GetDbOutfit(string categoryCode, int index)
    {
        DbOutfit outfit;
        dbOutfitsByUnityKey.TryGetValue(GetFavoriteKey(categoryCode, index), out outfit);
        return outfit;
    }

    private bool IsFavorite(string categoryCode, int index)
    {
        DbOutfit outfit = GetDbOutfit(categoryCode, index);
        if (outfit != null && outfit.outfit_id > 0)
            return favoriteOutfitIds.Contains(outfit.outfit_id);

        return favoriteKeys.Contains(GetFavoriteKey(categoryCode, index));
    }

    private string GetDbOutfitName(DbOutfit outfit)
    {
        if (outfit != null && !string.IsNullOrEmpty(outfit.outfit_name))
            return outfit.outfit_name;

        return "의상";
    }

    private string GetCategoryDisplayName(DbOutfit outfit)
    {
        if (outfit != null && !string.IsNullOrEmpty(outfit.category_name))
            return outfit.category_name;

        if (outfit != null && outfit.unity_category_code == CategoryUpper)
            return "상의";

        if (outfit != null && outfit.unity_category_code == CategoryLower)
            return "하의";

        if (outfit != null && outfit.unity_category_code == CategoryHat)
            return "모자";

        if (outfit != null && outfit.unity_category_code == CategoryShoes)
            return "신발";

        return "의상";
    }

    private string GetUpperName(int index)
    {
        OutfitManager.OutfitSlot slot = GetSlot(outfitManager != null ? outfitManager.upperOutfits : null, index);
        return slot != null && !string.IsNullOrEmpty(slot.name) ? slot.name : "상의 " + (index + 1);
    }

    private string GetLowerName(int index)
    {
        OutfitManager.OutfitSlot slot = GetSlot(outfitManager != null ? outfitManager.lowerOutfits : null, index);
        return slot != null && !string.IsNullOrEmpty(slot.name) ? slot.name : "하의 " + (index + 1);
    }

    private OutfitManager.OutfitSlot GetSlot(OutfitManager.OutfitSlot[] slots, int index)
    {
        if (slots == null || index < 0 || index >= slots.Length)
            return null;

        return slots[index];
    }

    private Sprite GetThumbnail(string categoryCode, int index)
    {
        if (categoryCode == CategoryUpper)
            return GetSprite(upperThumbnails, index);

        if (categoryCode == CategoryLower)
            return GetSprite(lowerThumbnails, index);

        if (categoryCode == CategoryHat)
            return GetSprite(hatThumbnails, index);

        return GetSprite(shoesThumbnails, index);
    }

    private Sprite GetSprite(Sprite[] sprites, int index)
    {
        if (sprites == null || index < 0 || index >= sprites.Length)
            return null;

        return sprites[index];
    }

    private string GetFavoriteKey(string categoryCode, int index)
    {
        return categoryCode + ":" + index;
    }

    private string GetFavoriteDisplayName(string key)
    {
        string[] parts = key.Split(':');
        if (parts.Length != 2)
            return key;

        int index;
        if (!int.TryParse(parts[1], out index))
            return key;

        return GetOutfitName(parts[0], index);
    }

    private Font GetRuntimeFont()
    {
        if (runtimeFont != null)
            return runtimeFont;

        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (runtimeFont == null)
            runtimeFont = Font.CreateDynamicFontFromOSFont(new string[] { "Malgun Gothic", "맑은 고딕", "Segoe UI" }, 16);

        return runtimeFont;
    }

    private Sprite GetRoundedSprite()
    {
        if (roundedSprite != null)
            return roundedSprite;

        roundedSprite = CreateRoundedSprite(48, 48, 12);
        return roundedSprite;
    }

    private Sprite GetCircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        circleSprite = CreateCircleSprite(64);
        return circleSprite;
    }

    private Sprite CreateRoundedSprite(int width, int height, int radius)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = IsInsideRoundedRect(x, y, width, height, radius);
                texture.SetPixel(x, y, inside ? solid : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float radius = center;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                texture.SetPixel(x, y, distance <= radius ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
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

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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
}

public class OutfitCardButton : MonoBehaviour
{
    public string categoryCode;
    public int index;
    public Button button;
    public Button favoriteButton;
    public Image borderImage;
    public Text favoriteText;
    public Text nameText;
    public GameObject checkMark;

    public void SetSelected(bool selected, Color selectedColor)
    {
        if (borderImage != null)
            borderImage.color = selected ? selectedColor : new Color(1f, 1f, 1f, 0.18f);

        if (checkMark != null)
            checkMark.SetActive(selected);
    }

    public void SetFavorite(bool favorite, Color favoriteColor)
    {
        if (favoriteText == null)
            return;

        favoriteText.text = favorite ? "♥" : "♡";
        favoriteText.color = favorite ? favoriteColor : Color.white;
    }
}

public class OutlineHolder : MonoBehaviour
{
    public Image outlineImage;
}

[System.Serializable]
public class AuthResponse
{
    public bool ok;
    public AuthUser data;
    public string message;
}

[System.Serializable]
public class AuthUser
{
    public int user_id;
    public string name;
    public string phone;
}

[System.Serializable]
public class ApiBasicResponse
{
    public bool ok;
    public string message;
}

[System.Serializable]
public class OutfitListResponse
{
    public bool ok;
    public DbOutfit[] data;
    public string message;
}

[System.Serializable]
public class DbOutfit
{
    public int favorite_id;
    public int outfit_id;
    public string outfit_name;
    public int category_id;
    public string category_name;
    public string category_code;
    public string gender;
    public string color;
    public string description;
    public string unity_category_code;
    public int unity_outfit_index;
    public string unity_outfit_key;
    public string thumbnail_url;
}

[System.Serializable]
public class FavoriteToggleResponse
{
    public bool ok;
    public FavoriteToggleData data;
    public string message;
}

[System.Serializable]
public class FavoriteToggleData
{
    public int user_id;
    public int outfit_id;
    public bool is_favorite;
}
