using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(HealthAndTeam))]
[RequireComponent(typeof(CharacterController))]
public class PlayerFeignDeath : MonoBehaviour
{
    public static PlayerFeignDeath Instance { get; private set; }

    [Header("State (Trạng Thái Giả Chết)")]
    [SerializeField] private bool isFeignDead = false;
    [SerializeField] private bool isDetected = false;
    public bool IsFeignDead => isFeignDead;
    public bool IsDetected => isDetected;

    [Header("Key Bindings")]
    public KeyCode toggleKey = KeyCode.C;

    [Header("Line Of Sight Check (Không cho giả chết nếu đang bị nhìn)")]
    public float sightCheckDistance = 12f;
    public float sightFovAngle = 80f; 
    public LayerMask sightObstructionMask;

    [Header("Horizontal Prone Capsule (Kéo CapsuleCollider đã chỉnh ở Inspector vào đây)")]
    public CapsuleCollider proneCapsule;

    [Header("Detection Conditions (Quy tắc bị phát hiện)")]
    public float corpseCheckRadius = 2.0f; 
    public float corpseNearDist = 3.0f;
    public float corpseNearPanicThreshold = 70f;

    public float openGroundDist = 7.0f;
    public float openGroundPanicThreshold = 30f;

    public float detectionScanInterval = 0.25f;

    [Header("Cooldown")]
    public float feignCooldown = 2.0f;
    private float lastExitTime = -10f;

    private HealthAndTeam myHealth;
    private CharacterController controller;
    private Animator anim;
    private PlayerMovement movement;
    private PlayerCombat combat;

    private float detectionTimer = 0f;
    private readonly Collider[] scanColliders = new Collider[64];
    private LayerMask characterLayerMask;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }

        myHealth = GetComponent<HealthAndTeam>();
        controller = GetComponent<CharacterController>();
        anim = GetComponentInChildren<Animator>();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();

        characterLayerMask = LayerMask.GetMask("Character");
        if (sightObstructionMask.value == 0)
        {
            sightObstructionMask = ~LayerMask.GetMask("Character", "Ignore Raycast");
        }

        if (proneCapsule == null)
        {
            proneCapsule = GetComponent<CapsuleCollider>();
        }
        if (proneCapsule != null)
        {
            proneCapsule.enabled = false; // Mặc định tắt khi đứng
        }
    }

    void Start()
    {
        if (myHealth != null)
        {
            myHealth.onDamaged += OnDamagedWhileFeign;
            myHealth.onDie += OnDieWhileFeign;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (myHealth != null)
        {
            myHealth.onDamaged -= OnDamagedWhileFeign;
            myHealth.onDie -= OnDieWhileFeign;
        }
    }

    void Update()
    {
        if (myHealth != null && !myHealth.isAlive)
        {
            if (isFeignDead) ExitFeignDeath(false);
            return;
        }

        if (!isFeignDead)
        {
            // Bấm C để bắt đầu giả chết
            if (Input.GetKeyDown(toggleKey))
            {
                if (Time.time - lastExitTime >= feignCooldown)
                {
                    TryStartFeignDeath();
                }
            }
        }
        else
        {
            // ĐANG GIẢ CHẾT:
            // 1. Phím Space: Lăn người (Roll) bật dậy né đòn
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ExitFeignDeathWithRoll();
                return;
            }

            // 2. Phím C hoặc di chuyển WASD: Đứng dậy bình thường
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            bool isMovingInput = (Mathf.Abs(h) > 0.2f || Mathf.Abs(v) > 0.2f);

            if (Input.GetKeyDown(toggleKey) || isMovingInput)
            {
                ExitFeignDeathNormal();
                return;
            }

            // 3. Quét kiểm tra bị phát hiện (chỉ kiểm tra nếu chưa bị lộ)
            if (!isDetected)
            {
                detectionTimer -= Time.deltaTime;
                if (detectionTimer <= 0f)
                {
                    detectionTimer = detectionScanInterval;
                    CheckDetectionRules();
                }
            }
        }
    }


    public bool TryStartFeignDeath()
    {
        if (anim == null || myHealth == null || !myHealth.isAlive) return false;

        // Không cho giả chết nếu đang Roll hoặc dính đòn (Impact)
        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        if (state.IsTag("Roll") || state.IsTag("Impact") || state.IsTag("Dead"))
        {
            return false;
        }

        // 1. Kiểm tra tầm nhìn trực diện của kẻ địch (Line of Sight)
        if (IsWatchedByEnemy(out HealthAndTeam watchingEnemy))
        {
            Debug.Log($"<color=yellow>[FeignDeath] Không thể giả chết! Kẻ địch {watchingEnemy.gameObject.name} đang nhìn chằm chằm vào bạn!</color>");
            return false;
        }

        // 2. Kích hoạt giả chết thành công!
        isFeignDead = true;
        isDetected = false;
        detectionTimer = detectionScanInterval;

        // Chọn ngẫu nhiên 1 trong 3 thế chết
        int deadIndex = Random.Range(0, 3);
        anim.SetInteger("DeadIndex", deadIndex);
        anim.ResetTrigger("DoDead");
        anim.SetTrigger("DoDead");

        // 1. Tắt CharacterController đứng khi giả chết
        if (controller != null)
        {
            controller.enabled = false;
        }

        // 2. Bật Capsule nằm ngang đã setup sẵn ở Inspector
        if (proneCapsule != null)
        {
            proneCapsule.enabled = true;
        }

        // Bỏ toàn bộ kẻ thù đang nhắm vào mình (Drop Aggro)
        DropAllCurrentAttackers();

        Debug.Log("<color=green>[FeignDeath] Đã nằm giả chết thành công! Kẻ địch hủy mục tiêu.</color>");
        return true;
    }

    private bool IsWatchedByEnemy(out HealthAndTeam watchingEnemy)
    {
        watchingEnemy = null;
        Vector3 playerHeadPos = transform.position + Vector3.up * 1.0f;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, sightCheckDistance, scanColliders, characterLayerMask);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = scanColliders[i];
            if (col == null) continue;

            HealthAndTeam h = col.GetComponentInParent<HealthAndTeam>();
            if (h == null || h == myHealth || !h.isAlive || !myHealth.IsEnemy(h.teamCurrent)) continue;

            Vector3 enemyEyePos = h.transform.position + Vector3.up * 1.4f;
            Vector3 toPlayer = playerHeadPos - enemyEyePos;
            float dist = toPlayer.magnitude;
            if (dist < 0.1f) continue;

            Vector3 dirToPlayer = toPlayer.normalized;

            // Kiểm tra góc nhìn phía trước của địch (FOV)
            float angle = Vector3.Angle(h.transform.forward, dirToPlayer);
            if (angle <= sightFovAngle * 0.5f)
            {
                // Bắn Raycast kiểm tra đường nhìn có bị tường/địa hình cản không
                if (!Physics.Raycast(enemyEyePos, dirToPlayer, dist, sightObstructionMask, QueryTriggerInteraction.Ignore))
                {
                    watchingEnemy = h;
                    return true; // Có địch đang nhìn thấy rõ ràng!
                }
            }
        }

        return false;
    }

    private void DropAllCurrentAttackers()
    {
        if (myHealth == null) return;

        // Báo cho toàn bộ NPC đang nhắm vào mình hủy target
        var attackersCopy = myHealth.currentAttackers.ToArray();
        foreach (var attacker in attackersCopy)
        {
            if (attacker != null)
            {
                NPCCombat npc = attacker.GetComponent<NPCCombat>();
                if (npc != null)
                {
                    npc.OnTargetDied(gameObject);
                }
            }
        }
        myHealth.currentAttackers.Clear();
        myHealth.attackerCount = 0;
    }

    private void CheckDetectionRules()
    {
        bool isNearCorpse = (CorpseManager.Instance != null && CorpseManager.Instance.IsNearAnyCorpse(transform.position, corpseCheckRadius));
        float currentPanic = (PlayerPanicSystem.Instance != null) ? PlayerPanicSystem.Instance.currentPanic : 0f;

        float maxScanRadius = openGroundDist + 1f;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, maxScanRadius, scanColliders, characterLayerMask);

        Vector3 myPos = transform.position;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = scanColliders[i];
            if (col == null) continue;

            HealthAndTeam h = col.GetComponentInParent<HealthAndTeam>();
            if (h == null || h == myHealth || !h.isAlive || !myHealth.IsEnemy(h.teamCurrent)) continue;

            float dist = Vector3.Distance(myPos, h.transform.position);

            // QUY TẮC PHÁT HIỆN ĐỘNG (Dựa trên trần Panic và khoảng cách):
            // 1. Nằm gần xác (bất kể phe ta hay phe địch): Địch áp sát < 3m
            //    - Trần: Panic >= 70% -> 100% chắc chắn bị phát hiện!
            //    - Dưới 70%: Nguy cơ bị phát hiện tăng dần tỉ lệ thuận theo Panic và khoảng cách
            if (isNearCorpse)
            {
                if (dist <= corpseNearDist)
                {
                    if (currentPanic >= corpseNearPanicThreshold)
                    {
                        TriggerDetected(h);
                        break;
                    }
                    else if (currentPanic > 0f)
                    {
                        float panicRisk = currentPanic / corpseNearPanicThreshold; // 0.0 -> 1.0
                        float distFactor = 1f - (dist / corpseNearDist); // Càng sát người càng dễ lộ (0 ở 3m, 1 ở 0m)
                        float detectionChance = panicRisk * (0.15f + 0.35f * distFactor);
                        if (Random.value < detectionChance)
                        {
                            TriggerDetected(h);
                            break;
                        }
                    }
                }
            }
            // 2. Nằm trơ trọi (không gần xác): Địch áp sát < 7m
            //    - Trần: Panic >= 30% -> 100% chắc chắn bị phát hiện!
            //    - Dưới 30%: Nguy cơ bị phát hiện tăng dần tỉ lệ thuận theo Panic và khoảng cách
            else
            {
                if (dist <= openGroundDist)
                {
                    if (currentPanic >= openGroundPanicThreshold)
                    {
                        TriggerDetected(h);
                        break;
                    }
                    else if (currentPanic > 0f)
                    {
                        float panicRisk = currentPanic / openGroundPanicThreshold; // 0.0 -> 1.0
                        float distFactor = 1f - (dist / openGroundDist); // Càng sát người càng dễ lộ (0 ở 7m, 1 ở 0m)
                        float detectionChance = panicRisk * (0.20f + 0.40f * distFactor);
                        if (Random.value < detectionChance)
                        {
                            TriggerDetected(h);
                            break;
                        }
                    }
                }
            }
        }
    }

    private void TriggerDetected(HealthAndTeam spotter)
    {
        isDetected = true;
        Debug.Log($"<color=red>[FeignDeath] BỊ PHÁT HIỆN! Kẻ địch {spotter.gameObject.name} nhận ra bạn còn sống và lao vào chém!</color>");

        // Kẻ địch phát hiện đổi target sang Player
        NPCCombat spotterCombat = spotter.GetComponent<NPCCombat>();
        if (spotterCombat != null)
        {
            spotterCombat.OnTargetResurrected(gameObject);
        }

        // Đồng thời báo động cho các kẻ địch khác trong bán kính 6m
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, 6f, scanColliders, characterLayerMask);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = scanColliders[i];
            if (col == null) continue;
            HealthAndTeam h = col.GetComponentInParent<HealthAndTeam>();
            if (h != null && h != myHealth && h.isAlive && myHealth.IsEnemy(h.teamCurrent))
            {
                NPCCombat npc = h.GetComponent<NPCCombat>();
                if (npc != null)
                {
                    npc.OnTargetResurrected(gameObject);
                }
            }
        }
    }

    [Header("Animation Triggers (Điều khiển bằng Animator Parameters)")]
    public string standUpTriggerName = "StandUp";

    public void ExitFeignDeathWithRoll()
    {
        if (!isFeignDead) return;

        ExitFeignDeath(false);

        // Kích hoạt Any State -> Roll thông qua Trigger DoRoll của PlayerMovement
        if (movement != null)
        {
            movement.TriggerRollManual();
        }
    }

    public void ExitFeignDeathNormal()
    {
        if (!isFeignDead) return;

        ExitFeignDeath(false);

        // Kích hoạt transition đứng dậy thông qua Trigger Animator
        if (anim != null && !string.IsNullOrEmpty(standUpTriggerName))
        {
            anim.ResetTrigger(standUpTriggerName);
            anim.SetTrigger(standUpTriggerName);
        }
    }

    public void ExitFeignDeath(bool interruptedByDamage)
    {
        if (!isFeignDead) return;

        isFeignDead = false;
        isDetected = false;
        lastExitTime = Time.time;

        // Khôi phục và Bật lại CharacterController khi đứng dậy
        if (controller != null)
        {
            controller.enabled = true;
        }

        // Tắt Capsule nằm ngang khi đứng dậy
        if (proneCapsule != null)
        {
            proneCapsule.enabled = false;
        }

        if (interruptedByDamage && anim != null)
        {
            // Kích hoạt Any State -> Impact thông qua Trigger TakeAttack
            anim.ResetTrigger("TakeAttack");
            anim.SetTrigger("TakeAttack");
        }
    }

    private void OnDamagedWhileFeign(GameObject attacker, float damage)
    {
        if (isFeignDead)
        {
            Debug.Log($"<color=red>[FeignDeath] Bị dính đòn ({damage} DMG)! Thoát giả chết và bật dậy!</color>");
            ExitFeignDeath(true);
        }
    }

    private void OnDieWhileFeign()
    {
        if (isFeignDead)
        {
            ExitFeignDeath(false);
        }
    }
}
