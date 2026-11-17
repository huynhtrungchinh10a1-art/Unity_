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
    public string waitingText = "Đang chuẩn bị chiến trường...";
    public string readyText = "VÀO TRẬN NGAY (OK)";

    [Header("Testing & Simulation")]
    public bool autoSimulateLoading = true;
    public float simulatedDuration = 2.5f;

    [Header("Fade Out Settings")]
    public float fadeOutSpeed = 2.5f;

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

        // Ban đầu vô hiệu hóa nút OK, hiển thị trạng thái chờ
        if (okButton != null)
        {
            okButton.interactable = false;
            if (okButton.onClick.GetPersistentEventCount() == 0)
            {
                okButton.onClick.AddListener(OnOKButtonClicked);
            }
        }

        if (okButtonText != null)
        {
            okButtonText.text = waitingText;
        }

        if (progressBar != null)
        {
            progressBar.value = 0f;
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
        // Khi Spawner thực tế nạp thì dừng tiến trình giả lập
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

        if (progressText != null)
        {
            progressText.text = "Chiến trường đã sẵn sàng!";
        }

        if (okButton != null)
        {
            okButton.interactable = true;
        }

        if (okButtonText != null)
        {
            okButtonText.text = readyText;
        }
    }

    public void OnOKButtonClicked()
    {
        if (!isLoaded || isFading) return;

        StartCoroutine(DismissRoutine());
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
