using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("Player Health Source")]
    public HealthAndTeam playerHealth;

    [Header("UI Bars (Health)")]
    public Image healthFillImage;
    public Image healthEaseImage;

    [Header("Health Text")]
    public TextMeshProUGUI healthText;

    [Header("Player Panic Source")]
    public PlayerPanicSystem panicSystem;

    [Header("UI Bars (Panic)")]
    public Image panicFillImage;
    public Image panicEaseImage;

    [Header("Panic Text")]
    public TextMeshProUGUI panicText;

    [Header("Animation Settings")]
    public float easeDelay = 0.4f;
    public float easeSpeed = 1f;

    private float targetFill = 1f;
    private float easeTimer = 0f;

    private float targetPanicFill = 0f;
    private float panicEaseTimer = 0f;

    void Start()
    {
        // 1. Tự tìm Player Health nếu chưa kéo thả vào Inspector
        if (playerHealth == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerHealth = playerObj.GetComponent<HealthAndTeam>();
            }
        }

        if (playerHealth != null)
        {
            playerHealth.onDamaged += HandleDamaged;
            playerHealth.onDie += HandleDie;
            UpdateHealthUI(true);
        }

        // 2. Tự tìm Player Panic System nếu chưa kéo thả
        if (panicSystem == null)
        {
            panicSystem = PlayerPanicSystem.Instance;
            if (panicSystem == null)
                panicSystem = FindObjectOfType<PlayerPanicSystem>();
        }

        if (panicSystem != null)
        {
            panicSystem.onPanicChanged += HandlePanicChanged;
            UpdatePanicUI(true);
        }
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.onDamaged -= HandleDamaged;
            playerHealth.onDie -= HandleDie;
        }

        if (panicSystem != null)
        {
            panicSystem.onPanicChanged -= HandlePanicChanged;
        }
    }

    void Update()
    {
        // 1. Hiệu ứng thanh tụt máu chậm phía sau
        if (playerHealth != null && healthEaseImage != null)
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

        // 2. Hiệu ứng thanh Sợ Hãi mượt
        if (panicSystem != null && panicEaseImage != null)
        {
            if (panicEaseTimer > 0f)
            {
                panicEaseTimer -= Time.deltaTime;
            }
            else if (Mathf.Abs(panicEaseImage.fillAmount - targetPanicFill) > 0.001f)
            {
                panicEaseImage.fillAmount = Mathf.MoveTowards(
                    panicEaseImage.fillAmount,
                    targetPanicFill,
                    easeSpeed * Time.deltaTime
                );
            }
        }
    }

    private void HandleDamaged(GameObject attacker, float damage)
    {
        UpdateHealthUI(false);
        easeTimer = easeDelay; 
    }

    private void HandleDie()
    {
        UpdateHealthUI(false);
    }

    private void HandlePanicChanged(float current, float max)
    {
        UpdatePanicUI(false);
        panicEaseTimer = easeDelay;
    }

    public void UpdateHealthUI(bool immediate)
    {
        if (playerHealth == null) return;

        float maxHp = Mathf.Max(playerHealth.maxHealth, 1f);
        float currentHp = Mathf.Clamp(playerHealth.currentHealth, 0f, maxHp);

        targetFill = currentHp / maxHp;

        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = targetFill;
        }

        if (healthEaseImage != null && immediate)
        {
            healthEaseImage.fillAmount = targetFill;
        }

        if (healthText != null)
        {
            float healthPercent = (currentHp / maxHp) * 100f;
            int displayHealth = Mathf.RoundToInt(healthPercent);
            if (currentHp > 0f && displayHealth == 0)
            {
                displayHealth = 1;
            }
            healthText.text = $"Health: {displayHealth} / 100";
        }
    }

    public void UpdatePanicUI(bool immediate)
    {
        if (panicSystem == null) return;

        float maxCap = Mathf.Max(panicSystem.maxCourageCap, 1f);
        float current = Mathf.Clamp(panicSystem.currentPanic, 0f, maxCap);

        targetPanicFill = current / maxCap;

        if (panicFillImage != null)
        {
            panicFillImage.fillAmount = targetPanicFill;
        }

        if (panicEaseImage != null && immediate)
        {
            panicEaseImage.fillAmount = targetPanicFill;
        }

        if (panicText != null)
        {
            int displayPanic = Mathf.RoundToInt(current);
            int displayMax = Mathf.RoundToInt(maxCap);
            panicText.text = $"Panic: {displayPanic} / {displayMax}";
        }
    }
}
