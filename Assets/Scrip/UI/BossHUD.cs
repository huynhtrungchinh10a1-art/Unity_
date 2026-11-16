using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHUD : MonoBehaviour
{
    public static BossHUD Instance { get; private set; }

    [Header("Current Target")]
    public HealthAndTeam currentTarget;

    [Header("UI Bars & Name")]
    public Image healthFillImage;
    public Image healthEaseImage;

    public TextMeshProUGUI healthText;

    [Header("Behavior")]
    public float autoHideDelay = 6f;

    public GameObject bossBarContainer;

    [Header("Animation Settings")]
    public float easeDelay = 0.4f;

    public float easeSpeed = 1f;
    private float targetFill = 1f;
    private float easeTimer = 0f;
    private float hideTimer = 0f;
    private bool isShown = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        if (bossBarContainer == null)
            bossBarContainer = gameObject;

        // Mặc định ẩn lúc vào game
        HideBar();
    }

    void Start()
    {
        if (currentTarget != null)
        {
            SetTarget(currentTarget);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeTarget();
    }

    void Update()
    {
        if (!isShown || currentTarget == null) return;

        // Đếm ngược thời gian tự ẩn nếu không gây thêm sát thương
        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f)
            {
                HideBar();
            }
        }

        // Hiệu ứng thanh vàng tụt chậm (Ease Bar)
        if (healthEaseImage != null)
        {
            if (easeTimer > 0f)
            {
                easeTimer -= Time.deltaTime;
            }
            else if (healthEaseImage.fillAmount > targetFill)
            {
                healthEaseImage.fillAmount = Mathf.MoveTowards(
                    healthEaseImage.fillAmount,
                    targetFill,
                    easeSpeed * Time.deltaTime
                );
            }
            else if (healthEaseImage.fillAmount < targetFill)
            {
                healthEaseImage.fillAmount = targetFill;
            }
        }
    }


    public void SetTarget(HealthAndTeam newTarget)
    {
        if (newTarget == null || !newTarget.isAlive) return;

        bool isDifferentTarget = (currentTarget != newTarget);

        if (isDifferentTarget)
        {
            UnsubscribeTarget();
            currentTarget = newTarget;

            if (currentTarget != null)
            {
                currentTarget.onDamaged += HandleDamaged;
                currentTarget.onDie += HandleDie;
            }

            // Cập nhật tên của NPC vừa bị đánh
            if (healthText != null && currentTarget != null)
            {
                healthText.text = CleanName(currentTarget.gameObject.name);
            }

            // Reset thanh máu về trạng thái mục tiêu mới
            UpdateHealthUI(true);
        }

        // Hiện thanh máu và gia hạn thời gian hiển thị
        ShowBar();
        hideTimer = autoHideDelay;
        easeTimer = easeDelay;

        // Cập nhật lượng máu sau khi dính đòn
        UpdateHealthUI(false);
    }

    private void UnsubscribeTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.onDamaged -= HandleDamaged;
            currentTarget.onDie -= HandleDie;
        }
    }

    public void ShowBar()
    {
        isShown = true;
        if (bossBarContainer != null && !bossBarContainer.activeSelf)
        {
            bossBarContainer.SetActive(true);
        }
    }

    public void HideBar()
    {
        isShown = false;
        if (bossBarContainer != null && bossBarContainer.activeSelf)
        {
            bossBarContainer.SetActive(false);
        }
    }

    private void HandleDamaged(GameObject attacker, float damage)
    {
        ShowBar();
        hideTimer = autoHideDelay;
        UpdateHealthUI(false);
        easeTimer = easeDelay;
    }

    private void HandleDie()
    {
        UpdateHealthUI(false);
        hideTimer = 2.0f; 
    }

    public void UpdateHealthUI(bool immediate)
    {
        if (currentTarget == null) return;

        float maxHp = Mathf.Max(currentTarget.maxHealth, 1f);
        float currentHp = Mathf.Clamp(currentTarget.currentHealth, 0f, maxHp);

        targetFill = currentHp / maxHp;

        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = targetFill;
        }

        if (healthEaseImage != null)
        {
            if (immediate)
                healthEaseImage.fillAmount = targetFill;
        }
    }

    private string CleanName(string rawName)
    {
        return rawName.Replace("(Clone)", "").Trim();
    }
}
