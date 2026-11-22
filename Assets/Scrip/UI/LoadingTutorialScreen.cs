using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class LoadingTutorialScreen : MonoBehaviour
{
    public static LoadingTutorialScreen Instance { get; private set; }

    [Header("UI Panels & CanvasGroup")]
    public GameObject screenPanel;
    public CanvasGroup canvasGroup;

    [Header("Progress Bar (Thanh Tiến Trình)")]
    public Slider progressBar;
    public TextMeshProUGUI progressText;

    [Header("OK / Battle Button")]
    public Button okButton;
    public TextMeshProUGUI okButtonText;

    [Header("Team Selection Popup (Chọn Phe)")]
    public string teamAName = "Tào Ngụy";
    public string teamBName = "Thục Hán";
    public GameObject teamSelectPopup;
    public Button teamAButton;
    public Button teamBButton;

    [Header("Testing & Simulation")]
    public bool autoSimulateLoading = true; 
    public float simulatedDuration = 2.5f;

    [Header("Fade Out Settings")]
    public float fadeOutSpeed = 2.5f;

    [Header("Audio Settings")]
    [Tooltip("Tắt toàn bộ âm thanh trong lúc đang mở màn hình Loading/Hướng dẫn, chỉ bật khi người chơi bấm nút Đã hiểu")]
    public bool muteAudioUntilDismissed = true;

    [Header("Events")]
    public UnityEvent onBattleStarted;

    private bool isLoaded = false;
    private bool isFading = false;
    private Coroutine simulationCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        if (screenPanel != null) screenPanel.SetActive(true);

        if (canvasGroup == null && screenPanel != null)
        {
            canvasGroup = screenPanel.GetComponent<CanvasGroup>();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        // [ĐÃ SỬA] Ban đầu ẨN HOÀN TOÀN nút OK, chờ đến khi load xong mới hiện
        if (okButton != null)
        {
            okButton.gameObject.SetActive(false); 
            okButton.interactable = true; // Đảm bảo nút ở trạng thái có thể bấm được khi hiện ra

            if (okButton.onClick.GetPersistentEventCount() == 0)
            {
                okButton.onClick.AddListener(OnOKButtonClicked);
            }
        }

        if (progressBar != null)
        {
            progressBar.value = 0f;
        }

        if (teamSelectPopup != null)
        {
            teamSelectPopup.SetActive(false);
        }

        if (teamAButton != null)
        {
            teamAButton.onClick.AddListener(() => OnTeamSelected(Team.TeamA));
        }

        if (teamBButton != null)
        {
            teamBButton.onClick.AddListener(() => OnTeamSelected(Team.TeamB));
        }

        // Tắt toàn bộ âm thanh trong game cho đến khi bấm nút Đã hiểu
        if (muteAudioUntilDismissed)
        {
            AudioListener.pause = true;
        }
    }

    void OnDisable()
    {
        if (muteAudioUntilDismissed)
        {
            AudioListener.pause = false;
        }
    }

    void OnDestroy()
    {
        if (muteAudioUntilDismissed)
        {
            AudioListener.pause = false;
        }
    }

    void Start()
    {
        if (autoSimulateLoading)
        {
            simulationCoroutine = StartCoroutine(SimulateLoadingRoutine());
        }
    }

    private IEnumerator SimulateLoadingRoutine()
    {
        float timer = 0f;
        while (timer < simulatedDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / simulatedDuration);
            SetProgressInternal(progress, $"Đang dàn trận... {Mathf.RoundToInt(progress * 100)}%");
            yield return null;
        }

        CompleteLoading();
    }

    public void SetProgress(float progress, string customMessage = "")
    {
        // Khi Spawner thực tế nạp thì dừng tiến trình giả lập (nếu đang chạy)
        if (simulationCoroutine != null)
        {
            StopCoroutine(simulationCoroutine);
            simulationCoroutine = null;
        }

        SetProgressInternal(progress, customMessage);
    }

    private void SetProgressInternal(float progress, string customMessage = "")
    {
        progress = Mathf.Clamp01(progress);

        if (progressBar != null)
        {
            progressBar.value = progress;
        }

        if (progressText != null)
        {
            if (!string.IsNullOrEmpty(customMessage))
            {
                progressText.text = customMessage;
            }
            else
            {
                progressText.text = $"Đang tải chiến trường... {Mathf.RoundToInt(progress * 100)}%";
            }
        }

        if (progress >= 1f && !isLoaded)
        {
            CompleteLoading();
        }
    }

    public void CompleteLoading()
    {
        isLoaded = true;

        if (progressBar != null) progressBar.value = 1f;

        // [ĐÃ SỬA] Hiển thị lại nút OK khi tiến trình tải đạt 100%
        if (okButton != null)
        {
            okButton.gameObject.SetActive(true); 
        }
    }

    public void OnOKButtonClicked()
    {
        if (!isLoaded || isFading) return;

        // Ẩn nút OK để người chơi chọn phe
        if (okButton != null)
        {
            okButton.gameObject.SetActive(false);
        }

        // Hiện popup chọn phe đã tạo trên Canvas
        if (teamSelectPopup != null)
        {
            teamSelectPopup.SetActive(true);
        }
        else
        {
            // Dự phòng nếu chưa gán popup thì vào thẳng trận
            OnTeamSelected(Team.Neutral);
        }
    }

    public void OnTeamSelected(Team chosenTeam)
    {
        if (isFading) return;

        ApplyTeamToPlayer(chosenTeam);

        if (teamSelectPopup != null)
        {
            teamSelectPopup.SetActive(false);
        }

        // Bật lại toàn bộ âm thanh khi người chơi đã chọn phe và vào trận
        if (muteAudioUntilDismissed)
        {
            AudioListener.pause = false;
        }

        StartCoroutine(DismissRoutine());
    }

    private void ApplyTeamToPlayer(Team chosenTeam)
    {
        HealthAndTeam playerHealth = null;

        if (PlayerMovement.PlayerTransform != null)
        {
            playerHealth = PlayerMovement.PlayerTransform.GetComponent<HealthAndTeam>();
        }

        if (playerHealth == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerHealth = playerObj.GetComponent<HealthAndTeam>();
            }
        }

        if (playerHealth == null)
        {
            PlayerMovement pm = FindObjectOfType<PlayerMovement>();
            if (pm != null)
            {
                playerHealth = pm.GetComponent<HealthAndTeam>();
            }
        }

        if (playerHealth != null)
        {
            playerHealth.teamCurrent = chosenTeam;
            string teamName = chosenTeam == Team.TeamA ? teamAName : teamBName;
            Debug.Log($"<color=cyan>[LoadingTutorialScreen]</color> Người chơi đã gia nhập phe: <b>{chosenTeam} ({teamName})</b>!");
        }
    }

    private IEnumerator DismissRoutine()
    {
        isFading = true;

        // Hiệu ứng mờ dần mượt mà
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false; // Bỏ chặn chuột để người chơi tương tác với game
            while (canvasGroup.alpha > 0f)
            {
                canvasGroup.alpha -= fadeOutSpeed * Time.deltaTime;
                yield return null;
            }
        }

        if (screenPanel != null)
        {
            screenPanel.SetActive(false);
        }

        // Bắn tín hiệu bắt đầu trận chiến cho toàn bộ hệ thống
        onBattleStarted?.Invoke();
    }
}