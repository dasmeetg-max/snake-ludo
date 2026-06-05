using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Controls all home screen panel switching and button logic
// Attach to a HomeScreenManager GameObject in HomeScene
public class HomeScreenManager : MonoBehaviour
{
    // ── Panels ────────────────────────────────────────────────
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject friendRoomPanel;
    public GameObject waitingPanel;
    public GameObject rulesPanel;

    // ── Main Menu ─────────────────────────────────────────────
    [Header("Main Menu")]
    public TMP_InputField playerNameInput;
    public Button playOnlineButton;
    public Button playWithFriendButton;
    public Button playBotButton;
    public Button rulesButton;
    public Button soundToggleButton;
    public TextMeshProUGUI soundToggleLabel;

    // ── Friend Room Panel ─────────────────────────────────────
    [Header("Friend Room — Tabs")]
    public Button createTabButton;
    public Button joinTabButton;
    public GameObject createContent;
    public GameObject joinContent;
    public Image createTabUnderline; // highlight under active tab
    public Image joinTabUnderline;

    [Header("Friend Room — Create")]
    public Button createRoomButton;

    [Header("Friend Room — Join")]
    public TMP_InputField roomCodeInput;
    public Button joinRoomButton;
    public Button friendRoomBackButton;

    // ── Waiting Panel ─────────────────────────────────────────
    [Header("Waiting Panel")]
    public TextMeshProUGUI waitingTitleText;
    public TextMeshProUGUI roomCodeDisplay;  // shown for private rooms, hidden for online
    public Button copyCodeButton;
    public Button cancelWaitButton;

    // ── Rules Panel ───────────────────────────────────────────
    [Header("Rules Panel")]
    public Button rulesCloseButton;

    // ── Colors ────────────────────────────────────────────────
    [Header("Tab Colors")]
    public Color activeTabColor = Color.white;
    public Color inactiveTabColor = new Color(1f, 1f, 1f, 0.35f);

    // ── State ─────────────────────────────────────────────────
    private bool soundEnabled = true;
    private const string PLAYER_NAME_KEY = "PlayerName";
    private const string SOUND_KEY = "SoundEnabled";

    // ─────────────────────────────────────────────────────────

    void Start()
    {
        LoadPlayerPrefs();
        WireButtons();
        ShowPanel(mainMenuPanel);
        ShowCreateTab(); // default tab
    }

    // ── PlayerPrefs ───────────────────────────────────────────

    void LoadPlayerPrefs()
    {
        // Restore saved player name
        if (playerNameInput != null)
            playerNameInput.text = PlayerPrefs.GetString(PLAYER_NAME_KEY, "");

        // Restore sound setting
        soundEnabled = PlayerPrefs.GetInt(SOUND_KEY, 1) == 1;
        UpdateSoundToggleLabel();
    }

    void SavePlayerName()
    {
        if (playerNameInput != null)
            PlayerPrefs.SetString(PLAYER_NAME_KEY, playerNameInput.text);
        PlayerPrefs.Save();
    }

    // ── Button wiring ─────────────────────────────────────────

    void WireButtons()
    {
        // Main menu
        playOnlineButton?.onClick.AddListener(OnPlayOnlineClicked);
        playWithFriendButton?.onClick.AddListener(OnPlayWithFriendClicked);
        playBotButton?.onClick.AddListener(OnPlayBotClicked);
        rulesButton?.onClick.AddListener(OnRulesClicked);
        soundToggleButton?.onClick.AddListener(OnSoundToggleClicked);

        // Save name when field is edited
        playerNameInput?.onEndEdit.AddListener(_ => SavePlayerName());

        // Friend room tabs
        createTabButton?.onClick.AddListener(ShowCreateTab);
        joinTabButton?.onClick.AddListener(ShowJoinTab);
        friendRoomBackButton?.onClick.AddListener(() => ShowPanel(mainMenuPanel));

        // Create room
        createRoomButton?.onClick.AddListener(OnCreateRoomClicked);

        // Join room
        joinRoomButton?.onClick.AddListener(OnJoinRoomClicked);

        // Waiting panel
        cancelWaitButton?.onClick.AddListener(() => ShowPanel(mainMenuPanel));
        copyCodeButton?.onClick.AddListener(OnCopyCodeClicked);

        // Rules
        rulesCloseButton?.onClick.AddListener(() => ShowPanel(mainMenuPanel));
    }

    // ── Panel switching ───────────────────────────────────────

    void ShowPanel(GameObject panel)
    {
        mainMenuPanel?.SetActive(false);
        friendRoomPanel?.SetActive(false);
        waitingPanel?.SetActive(false);
        rulesPanel?.SetActive(false);

        panel?.SetActive(true);
    }

    // ── Tab switching ─────────────────────────────────────────

    void ShowCreateTab()
    {
        createContent?.SetActive(true);
        joinContent?.SetActive(false);

        // Highlight create tab
        if (createTabUnderline != null) createTabUnderline.color = activeTabColor;
        if (joinTabUnderline != null) joinTabUnderline.color = inactiveTabColor;

        var createLabel = createTabButton?.GetComponentInChildren<TextMeshProUGUI>();
        var joinLabel = joinTabButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (createLabel != null) createLabel.color = activeTabColor;
        if (joinLabel != null) joinLabel.color = inactiveTabColor;
    }

    void ShowJoinTab()
    {
        createContent?.SetActive(false);
        joinContent?.SetActive(true);

        // Highlight join tab
        if (createTabUnderline != null) createTabUnderline.color = inactiveTabColor;
        if (joinTabUnderline != null) joinTabUnderline.color = activeTabColor;

        var createLabel = createTabButton?.GetComponentInChildren<TextMeshProUGUI>();
        var joinLabel = joinTabButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (createLabel != null) createLabel.color = inactiveTabColor;
        if (joinLabel != null) joinLabel.color = activeTabColor;
    }

    // ── Button handlers ───────────────────────────────────────

    void OnPlayOnlineClicked()
    {
        // Placeholder — room system not built yet
        ShowComingSoon("Online matchmaking coming soon!");
    }

    void OnPlayWithFriendClicked()
    {
        ShowPanel(friendRoomPanel);
    }

    void OnPlayBotClicked()
    {
        // Placeholder — bot not built yet
        ShowComingSoon("Play vs Bot coming soon!");
    }

    void OnRulesClicked()
    {
        ShowPanel(rulesPanel);
    }

    void OnSoundToggleClicked()
    {
        soundEnabled = !soundEnabled;
        PlayerPrefs.SetInt(SOUND_KEY, soundEnabled ? 1 : 0);
        PlayerPrefs.Save();
        UpdateSoundToggleLabel();
        // TODO: wire to AudioManager when sounds are added
    }

    void OnCreateRoomClicked()
    {
        // Placeholder — room system not built yet
        // Will generate a room code and show waiting panel
        ShowPanel(waitingPanel);

        if (waitingTitleText != null) waitingTitleText.text = "Waiting for friend...";
        if (roomCodeDisplay != null) roomCodeDisplay.text = "Room Code: ----";
        if (copyCodeButton != null) copyCodeButton.gameObject.SetActive(true);
    }

    void OnJoinRoomClicked()
    {
        string code = roomCodeInput != null ? roomCodeInput.text.Trim() : "";
        if (code.Length == 0)
        {
            // TODO: show validation message
            Debug.Log("⚠️ Please enter a room code");
            return;
        }

        // Placeholder — room system not built yet
        ShowPanel(waitingPanel);
        if (waitingTitleText != null) waitingTitleText.text = "Joining room...";
        if (roomCodeDisplay != null) roomCodeDisplay.gameObject.SetActive(false);
        if (copyCodeButton != null) copyCodeButton.gameObject.SetActive(false);
    }

    void OnCopyCodeClicked()
    {
        // Copy room code to clipboard
        string code = roomCodeDisplay != null ? roomCodeDisplay.text : "";
        GUIUtility.systemCopyBuffer = code;
        Debug.Log($"📋 Copied: {code}");
        // TODO: show brief "Copied!" feedback toast
    }

    void ShowComingSoon(string message)
    {
        // TODO: replace with a proper toast/overlay
        Debug.Log($"🚧 {message}");
    }

    void UpdateSoundToggleLabel()
    {
        if (soundToggleLabel != null)
            soundToggleLabel.text = soundEnabled ? "🔊" : "🔇";
    }

    // ── Scene transition ──────────────────────────────────────

    // Called when room is ready (both players connected)
    // Will be called by NetworkManager once room system is built
    public void OnRoomReady()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene("GameWorld");
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameWorld");
    }
}