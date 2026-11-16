using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("Player Health Source")]
    public HealthAndTeam playerHealth;

    [Header("UI Bars")]
    public Image healthFillImage;

    public Image healthEaseImage;

    [Header("Text")]
    public TextMeshProUGUI healthText;

    [Header("Animation Settings")]
    public float easeDelay = 0.4f;

    public float easeSpeed = 1f;

    private float targetFill = 1f;
    private float easeTimer = 0f;

    void Start()
    {
        // Tự tìm Player nếu chưa kéo thả vào Inspector
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
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.onDamaged -= HandleDamaged;
            playerHealth.onDie -= HandleDie;
        }
    }

    void Update()
    {
        if (playerHealth == null) return;

        // Hiệu ứng thanh tụt máu chậm phía sau
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

    private void HandleDamaged(GameObject attacker, float damage)
    {
        UpdateHealthUI(false);
        easeTimer = easeDelay; 
    }

    private void HandleDie()
    {
        UpdateHealthUI(false);
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
}
