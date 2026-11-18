using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class NPCCombat : MonoBehaviour
{
    [Header("View and Targeting")]
    public float baseDetectionRange = 15f;
    public float chaseRange = 25f;
    public float attackRange = 1.5f;
    public float scanInterval = 0.5f;

    [Header("Expansion")]
    public float lostTargetTimeToExpand = 3f;
    public float expansionFactor = 1.5f;
    public float maxDetectionRange = 80f;

    [Header("Switch Target")]
    public float switchTargetCooldown = 2f;
    public float outnumberedCooldownMultiplier = 0.5f;
    public float commitLockTime = 1.2f;

    [Header("Duel & Retaliation")]
    public float duelDuration = 4.0f; // Thời gian giữ chân solo khi bị NPC khác đánh
    public float duelCooldown = 2.0f; // 2 giây thả cửa: phớt lờ đệ, lao thẳng vào Player!
    protected HealthAndTeam duelTarget;
    protected float duelTimer;
    protected float nextDuelAllowedTime = 0f;

    [Header("Debug")]
    public bool debugTargeting = false;

    [Header("Targeting Limits")]
    public int maxAttackersPerTarget = 3;      // Toi da so luong can chien bu vao 1 muc tieu
    public int maxTotalAttackersPerTarget = 5; // Toi da tong so nguoi tan cong (can chien + cung) vao 1 muc tieu

    [Header("Distance Culling & Optimization")]
    public bool enableDistanceAnimCulling = true;
    public float animCullDistance = 45f; // Qua 45m: tắt MeshRenderer + Animator + tăng scanInterval
    private bool isAnimCulled = false;
    [HideInInspector] public int cullingIndex = -1;
    protected Renderer[] cachedRenderers;
    public bool IsCulled => isAnimCulled;

    [Header("Scoring (Global)")]
    public float weightAttackerCount = 0.75f;
    public float weightDistance = 0.25f;
    public float hysteresisBonus = 0.15f;
    public float maxExpectedAttackers = 5f;
    public float weightThreat = 0.35f;
    public float maxThreatForNorm = 50f;

    [Header("Reactive")]
    public float reactiveBase = 0.5f;
    public float reactiveDamageScale = 0.3f;

    [Header("Fury")]
    public float furyDuration = 8f;

    [Header("Movement & Attack")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 5.0f;
    public float runDistanceRatio = 1.5f;
    public float attackCooldownDuration = 2f;
    public float rotationSpeed = 5f;
    public float attackAngle = 30f;
    public float attackCooldownTimer = 0f;

    [Header("Separation")]
    public float separationDistance = 2.5f;
    public float separationForce = 5f;

    [Header("Weapons (Kéo trực tiếp vào đây)")]
    public GameObject[] equippedWeapons;

    protected CharacterController controller;
    protected float verticalVelocity;

    protected NavMeshAgent agent;
    protected HealthAndTeam myHealth;
    protected NPCAnimatorHandler animHandler;
    protected Animator anim;

    // Public properties cho NPCAnimatorHandler đọc
    public HealthAndTeam MyHealth { get { return myHealth; } }
    public float VerticalVelocity { get { return verticalVelocity; } }
    public Transform CurrentTarget { get { return currentTarget; } }
    public HealthAndTeam CurrentTargetHealth { get { return currentTargetHealth; } }

    // target
    protected Transform currentTarget;
    protected HealthAndTeam currentTargetHealth;
    protected bool ignorePlayerPriority;

    // timer & range
    protected float lastSwitchTime = -999f;
    protected float commitTimer;
    protected float noTargetTimer;
    protected float currentDetectionRange;
    protected float currentChaseRange;
    protected float ignorePlayerPriorityTimer;
    public float ignoreDuration = 3f;
    public float rollCooldown = 5f;
    protected float rollTimer = 0f;
    protected bool isAttacking = false;
    protected bool isApproaching = true;

    // fury
    protected bool isFuryMode;
    protected float furyTimer;

    // targets and player
    public static GameObject CachedPlayerObject;
    public static HealthAndTeam CachedPlayerHealth;
    private static readonly Collider[] sharedScanColliders = new Collider[128];

    // player
    protected GameObject playerObject;
    protected HealthAndTeam playerHealth;
    protected LayerMask characterLayerMask;

    protected List<HealthAndTeam> visibleEnemies = new List<HealthAndTeam>();

    // Grid
    private Vector2Int currentGridCell;
    private bool isRegisteredInGrid = false;

    // random
    private float scanOffset;

    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        controller = GetComponent<CharacterController>();
        myHealth = GetComponent<HealthAndTeam>();
        animHandler = GetComponent<NPCAnimatorHandler>();
        anim = GetComponentInChildren<Animator>();

        characterLayerMask = LayerMask.GetMask("Character");

        // Tìm Player qua Static Cache: Ưu tiên lấy từ PlayerMovement Instance (0ms) thay vì duyệt Tag Scene
        if (CachedPlayerObject == null)
        {
            if (PlayerMovement.Instance != null)
                CachedPlayerObject = PlayerMovement.Instance.gameObject;
            else
                CachedPlayerObject = GameObject.FindGameObjectWithTag("Player");

            if (CachedPlayerObject != null)
                CachedPlayerHealth = CachedPlayerObject.GetComponent<HealthAndTeam>();
        }
        playerObject = CachedPlayerObject;
        playerHealth = (CachedPlayerHealth != null) ? CachedPlayerHealth : (playerObject != null ? playerObject.GetComponent<HealthAndTeam>() : null);

        myHealth.onDamaged += OnDamaged;
        myHealth.onDie += OnDie;

        currentDetectionRange = baseDetectionRange;
        currentChaseRange = chaseRange;
        agent.speed = walkSpeed;
        agent.updateRotation = false;
        agent.updatePosition = false;

        // Tự động Warp về vị trí NavMesh gần nhất nếu NPC spawn bị lệch khỏi sàn (trên không, dốc)
        if (agent != null && agent.isActiveAndEnabled && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 15.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        // Lưu cache toàn bộ Renderers (SkinnedMeshRenderer, MeshRenderer kiếm/khiên/tóc...)
        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        if (NPCCullingManager.Instance != null)
        {
            NPCCullingManager.Instance.RegisterNPC(this);
        }

        scanOffset = Random.Range(0f, scanInterval);
    }

    protected virtual void Update()
    {
        HandleFury();

        if (duelTarget != null)
        {
            duelTimer -= Time.deltaTime;
            if (duelTimer <= 0f || !duelTarget.isAlive)
            {
                duelTarget = null;
                nextDuelAllowedTime = Time.time + duelCooldown; // Bật 2s miễn nhiễm kiềm chân để kiên quyết dí Player!
                if (debugTargeting)
                    Debug.Log($"<color=orange>[NPC {name}] Hết 4s Duel -> Vào 2s THẢ CỬA: Kiên quyết dí Player!</color>");
            }
        }

        if (!isAnimCulled)
        {
            if (animHandler != null)
                animHandler.UpdateRootMotionState();

            if (isAttacking)
            {
                if (animHandler != null && !animHandler.IsAttackingCombo)
                {
                    isAttacking = false;
                }
            }

            UpdateAnimationSpeed();
            HandleAttack();
        }

        float scanTime = (Time.time + scanOffset) % scanInterval;
        if (scanTime < Time.deltaTime)
        {
            UpdatePerception();
            HandleDecisionAndAction();
        }

        HandleMovement();

        if (ignorePlayerPriority)
        {
            ignorePlayerPriorityTimer -= Time.deltaTime;

            if (ignorePlayerPriorityTimer <= 0)
            {
                ignorePlayerPriority = false;
            }
        }

        if (BattlefieldManager.Instance != null && myHealth.isAlive)
        {
            if (!isAnimCulled || scanTime < Time.deltaTime)
            {
                Vector2Int newCell = BattlefieldManager.Instance.GetCell(transform.position);
                if (!isRegisteredInGrid)
                {
                    BattlefieldManager.Instance.RegisterNPC(this, newCell);
                    currentGridCell = newCell;
                    isRegisteredInGrid = true;
                }
                else if (newCell != currentGridCell)
                {
                    BattlefieldManager.Instance.UpdateNPCPosition(this, currentGridCell, newCell);
                    currentGridCell = newCell;
                }
            }
        }

    }

    void UpdatePerception()
    {
        // Khi ở xa và đã có mục tiêu còn sống: Bỏ qua vòng quét Physics để tiết kiệm hàng nghìn phép dò va chạm mỗi giây
        if (isAnimCulled && currentTargetHealth != null && currentTargetHealth.isAlive)
        {
            return;
        }

        visibleEnemies.Clear();

        // Scan nhanh Physics Non-Alloc không sinh rác bộ nhớ heap (Zero GC Allocation)
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, currentDetectionRange, sharedScanColliders, characterLayerMask);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = sharedScanColliders[i];
            if (hit == null) continue;

            HealthAndTeam h = hit.GetComponent<HealthAndTeam>();
            if (IsValidEnemy(h))
            {
                // Neu o do bi qua tai, chi giu lai neu do la target hien tai
                if (BattlefieldManager.Instance != null && BattlefieldManager.Instance.IsZoneCrowded(h.transform.position))
                {
                    if (h != currentTargetHealth)
                    {
                        continue;
                    }
                }

                visibleEnemies.Add(h);
            }
        }

        HandleExpansion();
    }

    bool IsValidEnemy(HealthAndTeam h)
    {
        if (h == null) return false;
        if (h == myHealth) return false;
        if (!h.isAlive) return false;
        return myHealth.IsEnemy(h.teamCurrent);
    }

    // Expansion
    void HandleExpansion()
    {
        bool hasValidTarget = currentTargetHealth != null && currentTargetHealth.isAlive;

        if (hasValidTarget)
        {
            if (currentDetectionRange > baseDetectionRange && Vector3.Distance(transform.position, currentTarget.position) <= chaseRange)
            {
                currentDetectionRange = baseDetectionRange;
                currentChaseRange = chaseRange;
            }
            return;
        }

        if (visibleEnemies.Count == 0)
        {
            noTargetTimer += scanInterval;
            if (noTargetTimer >= lostTargetTimeToExpand && currentDetectionRange < maxDetectionRange)
            {
                currentDetectionRange = Mathf.Min(currentDetectionRange * expansionFactor, maxDetectionRange);
                currentChaseRange = Mathf.Min(currentChaseRange * expansionFactor, maxDetectionRange);
                noTargetTimer = 0;
            }
        }
        else
        {
            noTargetTimer = 0;
        }
    }

    void HandleDecisionAndAction()
    {
        if (Time.time < commitTimer)
        {
            if (debugTargeting)
                Debug.Log($"<color=orange>[NPC {name}] Bỏ qua xét target vì kẹt commitTimer còn: {(commitTimer - Time.time):F2}s</color>");
            return;
        }

        HealthAndTeam best = DecideTarget();

        if (best != null && best != currentTargetHealth)
        {
            float cooldown = GetEffectiveCooldown();
            float timeSinceSwitch = Time.time - lastSwitchTime;
            if (timeSinceSwitch >= cooldown)
            {
                if (debugTargeting)
                    Debug.Log($"<color=green>[NPC {name}] ĐỔI TARGET: {currentTarget?.name} -> {best.name}</color>");
                SwitchToTarget(best);
                commitTimer = Time.time + commitLockTime;
                lastSwitchTime = Time.time;
            }
            else
            {
                if (debugTargeting)
                    Debug.Log($"<color=yellow>[NPC {name}] Muốn đổi sang {best.name} nhưng KẸT COOLDOWN: còn {(cooldown - timeSinceSwitch):F2}s</color>");
            }
        }
    }

    protected virtual HealthAndTeam DecideTarget()
    {
        // 1. Ưu tiên số 1: Đang trong thời gian DUEL với kẻ vừa đánh mình -> Khóa mục tiêu vào kẻ đó!
        if (duelTarget != null && duelTarget.isAlive)
        {
            if (debugTargeting)
                Debug.Log($"<color=cyan>[NPC {name}] [DUEL] Đang bị giữ chân đánh {duelTarget.name} (còn {duelTimer:F1}s)</color>");
            return duelTarget;
        }

        // 2. Ưu tiên số 2: Fury Mode nếu bị Player đánh -> Dí Player
        if (isFuryMode && playerHealth != null && playerHealth.isAlive && myHealth.IsEnemy(playerHealth.teamCurrent))
        {
            float distToPlayer = Vector3.Distance(transform.position, playerObject.transform.position);
            if (distToPlayer <= currentDetectionRange)
            {
                if (debugTargeting)
                    Debug.Log($"<color=red>[NPC {name}] [FURY] Đang kích hoạt Fury lên Player!</color>");
                return playerHealth;
            }
        }

        // 3. Toàn cục:
        // - Nếu có Player trong tầm: Player có điểm -1.5f nên LUÔN ĐƯỢC ƯU TIÊN SỐ 1!
        // - Nếu KHÔNG CÓ Player (ngoài tầm hoặc đã chết): Tự động xét duyệt tất cả các NPC khác để chọn mục tiêu tốt nhất!
        float bestScore = float.MaxValue;
        HealthAndTeam best = null;

        foreach (var enemy in visibleEnemies)
        {
            float score = ComputeGlobalScore(enemy);

            // Ưu tiên target cũ một chút để tránh giật giật đổi mục tiêu liên tục
            if (enemy == currentTargetHealth)
                score -= hysteresisBonus;

            // Bỏ qua target nếu đã đầy người bu vào
            bool isMyCurrentTarget = (enemy == currentTargetHealth);
            bool isFull = IsTargetFull(enemy);

            if (isFull && !isMyCurrentTarget)
            {
                if (debugTargeting)
                    Debug.Log($"[NPC {name}] [Global] Bỏ qua {enemy.name} vì Target Full ({enemy.GetMeleeAttackerCount()}/{maxAttackersPerTarget})");
                continue;
            }

            // Phạt điểm nếu zone quá đông đúc
            if (BattlefieldManager.Instance != null && BattlefieldManager.Instance.IsZoneCrowded(enemy.transform.position))
            {
                if (!isMyCurrentTarget)
                {
                    score += 5.0f;
                    if (debugTargeting)
                        Debug.Log($"[NPC {name}] [Global] {enemy.name} bị phạt +5đ do Zone Crowded");
                }
            }

            if (debugTargeting)
            {
                string tagInfo = enemy.CompareTag("Player") ? "[PLAYER]" : "[NPC]";
                Debug.Log($"[NPC {name}] [Global] Xét {tagInfo} {enemy.name}: Score = {score:F2} (bestScore hiện tại = {bestScore:F2})");
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        if (debugTargeting)
            Debug.Log($"<color=magenta>[NPC {name}] => KẾT QUẢ CUỐI CÙNG: Target = {(best != null ? best.name : "NULL")} (bestScore = {bestScore:F2})</color>");

        return best;
    }

    // Kiem tra muc tieu da day cho chua (can chien: toi da 3 can chien VA tong khong qua 5)
    protected virtual bool IsTargetFull(HealthAndTeam enemy)
    {
        if (enemy == null) return false;
        return enemy.GetMeleeAttackerCount() >= maxAttackersPerTarget 
            || enemy.GetTotalAttackerCount() >= maxTotalAttackersPerTarget;
    }

    // tinh score (so attacker - khoang cach)
    protected virtual float ComputeGlobalScore(HealthAndTeam enemy)
    {
        float normAtt = Mathf.Clamp01((float)enemy.GetMeleeAttackerCount() / maxExpectedAttackers);
        float dist = Vector3.Distance(transform.position, enemy.transform.position);
        float normDist = Mathf.Clamp01(dist / currentDetectionRange);

        // threat: uu tien th dang danh minh
        float threat = myHealth.GetThreat(enemy.gameObject);
        float normThreat = Mathf.Clamp01(threat / maxThreatForNorm);

        float score =
        (normAtt * weightAttackerCount) +
        (normDist * weightDistance) -
        (normThreat * weightThreat);

        // Nếu là Player và không bị ignore -> Ưu tiên tuyệt đối (-1.5f)
        if (!ignorePlayerPriority && enemy.CompareTag("Player"))
        {
            score -= 1.5f;
        }

        return score;
    }

    // tinh do yeu duoi
    protected virtual float ComputeReactiveScore(HealthAndTeam attacker)
    {
        float healthPercent = attacker.currentHealth / attacker.maxHealth;
        float pressure = 1f - healthPercent;
        float reactivePriority = Mathf.Clamp01(reactiveBase + pressure * reactiveDamageScale);
        return 1f - reactivePriority;
    }

    protected void SwitchToTarget(HealthAndTeam newTarget)
    {
        if (currentTargetHealth != null)
            currentTargetHealth.RemoveAttacker(gameObject);

        currentTarget = newTarget.transform;
        currentTargetHealth = newTarget;
        newTarget.AddAttacker(gameObject);
    }

    protected void SafeResetPath()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }
    }

    protected void SafeSetDestination(Vector3 dest)
    {
        if (agent != null && agent.isActiveAndEnabled)
        {
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 15.0f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }

            if (agent.isOnNavMesh)
            {
                // Chỉ tính toán lại đường đi NavMesh nếu điểm đến di chuyển đáng kể (> 0.5m) hoặc chưa có đường
                // Cắt giảm 95% số lượng yêu cầu tìm đường NavMesh (Pathfinding Queue) bị nghẽn CPU mỗi frame
                if (!agent.hasPath || (dest - agent.destination).sqrMagnitude > 0.25f)
                {
                    agent.SetDestination(dest);
                }
            }
        }
    }

    protected void ClearTarget()
    {
        if (currentTargetHealth != null)
            currentTargetHealth.RemoveAttacker(gameObject);

        currentTarget = null;
        currentTargetHealth = null;
        isAttacking = false;
        isApproaching = true;
        SafeResetPath();
        if (agent != null)
        {
            agent.velocity = Vector3.zero;
        }
        agent.nextPosition = transform.position;
    }

    protected virtual void HandleMovement()
    {
        if (isAttacking || (animHandler != null && animHandler.IsUsingRootMotion))
        {
            SafeResetPath();
            if (agent != null)
            {
                agent.velocity = Vector3.zero;
                agent.nextPosition = transform.position;
            }
            return;
        }

        if (controller.isGrounded)
            verticalVelocity = -2f;
        else
            verticalVelocity -= 9.81f * Time.deltaTime;

        if (currentTarget == null || currentTargetHealth == null || !currentTargetHealth.isAlive)
        {
            SafeResetPath();
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            agent.nextPosition = transform.position;
            isApproaching = true;
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.position);

        if (dist > currentChaseRange && !isFuryMode)
        {
            ClearTarget();
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            agent.nextPosition = transform.position;
            return;
        }

        // Hysteresis chống khựng giật/chatter khi player đi vòng quanh ở rìa attackRange
        if (isApproaching)
        {
            if (dist <= attackRange)
                isApproaching = false;
        }
        else
        {
            if (dist > attackRange + 0.4f)
                isApproaching = true;
        }

        if (isApproaching)
        {
            Vector3 dest = currentTarget.position;
            SafeSetDestination(dest);

            Vector3 desiredVelocity = agent.desiredVelocity;
            desiredVelocity.y = verticalVelocity;
            controller.Move(desiredVelocity * Time.deltaTime);

            // vua xoay vua di
            Vector3 moveDir = agent.desiredVelocity;
            moveDir.y = 0;
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            // Đã vào cự ly tấn công: đứng yên và xoay mượt mà theo dõi đối thủ
            SafeResetPath();
            if (agent != null)
            {
                agent.velocity = Vector3.zero;
            }

            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);

            Vector3 dirToTarget = (currentTarget.position - transform.position);
            dirToTarget.y = 0;
            if (dirToTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToTarget.normalized);
                float trackRotSpeed = Mathf.Max(rotationSpeed, 10f);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, trackRotSpeed * Time.deltaTime);
            }
        }

        // thong bao cho agent
        agent.nextPosition = transform.position;
    }

    // fury
    void HandleFury()
    {
        if (!isFuryMode) return;
        furyTimer -= Time.deltaTime;
        if (furyTimer <= 0)
            isFuryMode = false;
    }

    // bi nhieu nguoi danh thi biet chay nhanh hon
    float GetEffectiveCooldown()
    {
        if (myHealth.currentAttackers.Count >= 2)
            return switchTargetCooldown * outnumberedCooldownMultiplier;
        return switchTargetCooldown;
    }

    // tim th yeu nhat
    Transform GetLowestHealthAttacker()
    {
        if (myHealth.currentAttackers.Count == 0 && myHealth.threatMap.Count == 0) return null;

        float minHealth = float.MaxValue;
        Transform lowest = null;

        // Tập hợp tất cả các kẻ thù đang nhắm vào mình hoặc đã gây sát thương lên mình
        HashSet<GameObject> candidateAttackers = new HashSet<GameObject>(myHealth.currentAttackers);
        foreach (var kvp in myHealth.threatMap)
        {
            if (kvp.Key != null && kvp.Value > 0f)
                candidateAttackers.Add(kvp.Key);
        }

        foreach (GameObject obj in candidateAttackers)
        {
            if (obj == null) continue;
            HealthAndTeam h = obj.GetComponent<HealthAndTeam>();
            if (h != null && h.isAlive && myHealth.IsEnemy(h.teamCurrent) && h.currentHealth < minHealth)
            {
                // Chỉ phản ứng trong reactive layer nếu kẻ địch nằm trong tầm phát hiện HOẶC đã từng gây sát thương lên mình
                float dist = Vector3.Distance(transform.position, obj.transform.position);
                bool hasDealtDamage = myHealth.GetThreat(obj) > 0f;
                if (dist <= currentDetectionRange || hasDealtDamage)
                {
                    minHealth = h.currentHealth;
                    lowest = obj.transform;
                }
            }
        }
        return lowest;
    }

    // event
    protected virtual void OnDamaged(GameObject attacker, float damage)
    {
        if (isAnimCulled && anim != null)
        {
            isAnimCulled = false;
            anim.enabled = true;
        }

        if (debugTargeting)
        {
            float remainingCommit = Mathf.Max(0, commitTimer - Time.time);
            float remainingCooldown = Mathf.Max(0, (lastSwitchTime + GetEffectiveCooldown()) - Time.time);
            Debug.Log($"<color=red>[NPC {name}] OnDamaged bởi {(attacker != null ? attacker.name : "NULL")}</color> | DMG: {damage} | Target hiện tại: {(currentTarget != null ? currentTarget.name : "NULL")}");
        }

        if (attacker != null)
        {
            HealthAndTeam attackerHealth = attacker.GetComponent<HealthAndTeam>();
            if (attackerHealth != null && attackerHealth.isAlive && myHealth.IsEnemy(attackerHealth.teamCurrent))
            {
                myHealth.AddAttacker(attacker);

                float dist = Vector3.Distance(transform.position, attacker.transform.position);
                if (dist > currentChaseRange)
                {
                    currentChaseRange = Mathf.Min(dist + 5f, maxDetectionRange);
                    currentDetectionRange = Mathf.Min(dist + 5f, maxDetectionRange);
                }

                if (attacker.CompareTag("Player"))
                {
                    isFuryMode = true;
                    furyTimer = furyDuration;
                    duelTarget = null; // Bị Player đánh -> Bỏ qua duel NPC để dí Player
                    SwitchToTarget(attackerHealth);
                    commitTimer = Time.time + commitLockTime;
                    lastSwitchTime = Time.time;
                    if (debugTargeting)
                        Debug.Log($"<color=red>[NPC {name}] BẬT FURY MODE vì bị Player đánh!</color>");
                }
                else
                {
                    // A. Nếu đang trong 4s Duel: Tiếp tục solo, KHÔNG reset timer để tránh bị kẹt vĩnh viễn
                    if (duelTarget != null && duelTarget.isAlive)
                    {
                        if (debugTargeting)
                            Debug.Log($"[NPC {name}] Đang trong 4s Duel với {duelTarget.name} -> Tiếp tục solo (không reset timer).");
                    }
                    // B. Nếu đang trong 2s Thả Cửa: MIỄN NHIỄM KIỀM CHÂN, TIẾP TỤC DÍ PLAYER!
                    else if (Time.time < nextDuelAllowedTime)
                    {
                        if (debugTargeting)
                            Debug.Log($"<color=orange>[NPC {name}] Bị {attackerHealth.name} chém nhưng đang trong 2s THẢ CỬA (còn {(nextDuelAllowedTime - Time.time):F2}s) -> Bỏ qua đệ, kiên quyết lao vào Player!</color>");
                    }
                    // C. Hết 2s thả cửa: Bị chém thì mới kích hoạt 4s Duel mới!
                    else
                    {
                        duelTarget = attackerHealth;
                        duelTimer = duelDuration;

                        SwitchToTarget(attackerHealth);
                        commitTimer = Time.time + commitLockTime;
                        lastSwitchTime = Time.time;

                        if (debugTargeting)
                            Debug.Log($"<color=yellow>[NPC {name}] Bị {attackerHealth.name} chém trúng! Bắt đầu DUEL {duelDuration}s với {attackerHealth.name}!</color>");
                    }
                }
            }
        }

        CheckRollOnDamaged(attacker, damage);
    }

    protected virtual void CheckRollOnDamaged(GameObject attacker, float damage)
    {
        // Nếu đã Block thành công (damage == 0): Không Roll
        if (damage <= 0f) return;

        // roll
        if (myHealth.currentAttackers.Count >= 2 && Time.time >= rollTimer)
        {
            float healthPercent = myHealth.currentHealth / myHealth.maxHealth;
            // mau 100% - 20%, mau 20% - 80% 
            float rollChance = Mathf.Lerp(0.8f, 0.3f, healthPercent);
            // float rollChance = 1.2f;
            if (Random.value < rollChance)
            {
                isAttacking = false;
                Vector3 escapeDir = FindBestRollDirection(attacker);
                if (escapeDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(escapeDir);
                }

                SafeResetPath();
                if (agent != null)
                {
                    agent.velocity = Vector3.zero;
                    agent.nextPosition = transform.position;
                }

                if (animHandler != null)
                    animHandler.PlayRoll();
                rollTimer = Time.time + rollCooldown;
            }
        }
    }

    protected virtual Vector3 FindBestRollDirection(GameObject attacker)
    {
        Vector3 baseAwayDir;
        if (attacker != null)
        {
            baseAwayDir = transform.position - attacker.transform.position;
            baseAwayDir.y = 0;
            if (baseAwayDir.sqrMagnitude < 0.001f)
            {
                baseAwayDir = -transform.forward;
            }
            else
            {
                baseAwayDir.Normalize();
            }
        }
        else if (currentTarget != null)
        {
            baseAwayDir = transform.position - currentTarget.position;
            baseAwayDir.y = 0;
            baseAwayDir = baseAwayDir.sqrMagnitude < 0.001f ? -transform.forward : baseAwayDir.normalized;
        }
        else
        {
            baseAwayDir = -transform.forward;
        }

        // Danh sách các góc thử nghiệm: 
        // 0 (lùi thẳng), +45 (lùi chếch phải), -45 (lùi chếch trái), 
        // +90 (né ngang phải), -90 (né ngang trái), +135 (vòng phải), -135 (vòng trái)
        float[] candidateAngles = new float[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f };

        Vector3 bestDir = baseAwayDir;
        float bestScore = -9999f;

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        float checkDistance = 4.0f;

        int characterLayer = LayerMask.NameToLayer("Character");
        int mask = Physics.DefaultRaycastLayers;
        if (characterLayer != -1)
        {
            mask |= (1 << characterLayer);
        }

        foreach (float angle in candidateAngles)
        {
            Vector3 testDir = Quaternion.Euler(0, angle, 0) * baseAwayDir;
            testDir.y = 0;
            testDir.Normalize();

            // Điểm góc: Ưu tiên thoát xa kẻ tấn công (lùi thẳng xa nhất)
            float angleScore = Mathf.Cos(angle * Mathf.Deg2Rad) * 40f + 10f;

            float clearDist = checkDistance;

            // Kiểm tra vật cản (tường, môi trường, hoặc Character khác)
            RaycastHit[] hits = Physics.SphereCastAll(rayOrigin, 0.35f, testDir, checkDistance, mask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.transform.IsChildOf(transform))
                {
                    if (hit.distance < clearDist)
                    {
                        clearDist = hit.distance;
                    }
                }
            }

            // Điểm không gian: Càng xa vật cản càng tốt
            float spaceScore = (clearDist / checkDistance) * 40f;

            // Phạt nặng nếu bị kẹt sát vật cản (< 1.8m)
            if (clearDist < 1.8f)
            {
                spaceScore -= 60f;
            }

            // Kiểm tra điểm đến có NavMesh hợp lệ không
            Vector3 targetPos = transform.position + testDir * Mathf.Min(clearDist, 3.5f);
            bool hasNavMesh = NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 1.2f, NavMesh.AllAreas);
            float navScore = hasNavMesh ? 20f : -50f;

            float totalScore = angleScore + spaceScore + navScore;

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestDir = testDir;
            }
        }

        return bestDir;
    }

    public void OnTargetDied(GameObject deadTarget)
    {
        if (currentTarget != null && currentTarget.gameObject == deadTarget)
            ClearTarget();
    }

    public void OnDie()
    {
        isAttacking = false;
        ClearTarget();
        myHealth.threatMap.Clear();

        if (NPCCullingManager.Instance != null)
        {
            NPCCullingManager.Instance.UnregisterNPC(this);
        }

        // Đảm bảo bật lại để chạy animation chết và hiển thị xác
        if (isAnimCulled)
        {
            SetCulled(false);
        }

        if (isRegisteredInGrid && BattlefieldManager.Instance != null)
        {
            BattlefieldManager.Instance.UnregisterNPC(this, currentGridCell);
            isRegisteredInGrid = false;
        }

        int deadIndex = Random.Range(0, 3);
        if (animHandler != null)
        {
            animHandler.PlayDeath(deadIndex);
        }
        agent.isStopped = true;
        enabled = false;

        // GIAI ĐOẠN 4: Lên lịch nướng xác và tách vũ khí nằm trên đất sau 1.8s
        if (CorpseManager.Instance != null)
        {
            CorpseManager.Instance.ScheduleCorpseBake(gameObject, deadIndex, equippedWeapons, 1.8f);
        }
    }

    public void DropWeapons()
    {
        if (equippedWeapons == null || equippedWeapons.Length == 0) return;

        foreach (var weapon in equippedWeapons)
        {
            if (weapon == null) continue;
            weapon.transform.SetParent(null);
        }
    }

    protected virtual void OnDestroy()
    {
        if (NPCCullingManager.Instance != null)
        {
            NPCCullingManager.Instance.UnregisterNPC(this);
        }
    }

    public void SetCulled(bool culled)
    {
        if (culled == isAnimCulled) return;

        isAnimCulled = culled;

        if (anim == null) anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.enabled = !culled;
        }

        // Tắt/bật toàn bộ Renderers (SkinnedMeshRenderer, MeshRenderer kiếm/khiên/tóc/mũi tên...)
        SetRenderersEnabled(!culled);

        if (controller == null) controller = GetComponent<CharacterController>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (culled)
        {
            // ==================== CHẾ ĐỘ NGỦ ĐÔNG HOÀN TOÀN (HIBERNATION) ====================
            // 1. Tắt CharacterController: gỡ capsule hoàn toàn khỏi PhysX (tiết kiệm ~220ms CPU)
            if (controller != null)
            {
                controller.enabled = false;
            }

            // 2. Tắt NavMeshAgent: gỡ khỏi mô phỏng Crowd NavMesh C++ (tiết kiệm ~80ms CPU)
            if (agent != null)
            {
                if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
                agent.enabled = false;
            }

            // 3. Tắt các script bổ trợ (ArcherAnimatorHandler LateUpdate, ActiveDefense)
            if (animHandler != null) animHandler.enabled = false;
            ActiveDefense defense = GetComponent<ActiveDefense>();
            if (defense != null) defense.enabled = false;

            // 4. Tắt chính script NPCCombat/ArcherCombat: ngừng hoàn toàn hàm Update() (tiết kiệm ~40ms CPU)
            this.enabled = false;
        }
        else
        {
            // ==================== ĐÁNH THỨC DẬY (WAKE UP) ====================
            // 1. Bật lại script NPCCombat / ArcherCombat
            this.enabled = true;

            // 2. Bật lại CharacterController và đảm bảo cờ va chạm vật lý hoạt động bình thường
            if (controller != null)
            {
                controller.detectCollisions = true; // Bật lại vật lý bình thường
                controller.enabled = true;
            }

            // 3. Bật lại NavMeshAgent
            if (agent != null)
            {
                agent.enabled = true;
                if (!agent.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                    {
                        agent.Warp(hit.position);
                    }
                }
            }

            // 4. Bật lại các script bổ trợ
            if (animHandler != null) animHandler.enabled = true;
            ActiveDefense defense = GetComponent<ActiveDefense>();
            if (defense != null) defense.enabled = true;

            // 5. Chu kỳ quét AI khi thức dậy
            scanInterval = 0.5f;
        }
    }

    public void SetRenderersEnabled(bool isEnabled)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = isEnabled;
            }
        }
    }

    public void RefreshRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    protected virtual void UpdateDistanceAnimationCulling()
    {
        // Quản lý culling tập trung đã được chuyển giao cho NPCCullingManager
    }

    protected virtual void UpdateAnimationSpeed()
    {
        if (animHandler == null) return;

        if (isAttacking || animHandler.IsUsingRootMotion)
        {
            animHandler.UpdateLocomotion(0f);
            return;
        }

        if (currentTarget == null || currentTargetHealth == null || !currentTargetHealth.isAlive)
        {
            animHandler.UpdateLocomotion(0f);
            return;
        }

        float distToTarget = Vector3.Distance(transform.position, currentTarget.position);
        float runThreshold = attackRange * runDistanceRatio;
        float targetAgentSpeed = (distToTarget > runThreshold) ? runSpeed : walkSpeed;

        if (Mathf.Abs(agent.speed - targetAgentSpeed) > 0.01f)
            agent.speed = targetAgentSpeed;

        Vector3 horizontalVel = controller.velocity;
        horizontalVel.y = 0;
        float animSpeed = horizontalVel.magnitude / runSpeed;
        if (horizontalVel.magnitude < 0.1f) animSpeed = 0f;
        animHandler.UpdateLocomotion(Mathf.Clamp01(animSpeed));
    }

    protected virtual void HandleAttack()
    {
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
            return;
        }

        if (currentTarget == null || currentTargetHealth == null || !currentTargetHealth.isAlive) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist <= attackRange)
        {
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                float angleToTarget = Vector3.Angle(transform.forward, direction);

                if (angleToTarget <= attackAngle)
                {
                    // Snap mặt về phía player dứt khoát trước khi ra đòn để chém chuẩn xác theo hướng player
                    transform.rotation = Quaternion.LookRotation(direction);
                    isAttacking = true;

                    SafeResetPath();
                    if (agent != null)
                    {
                        agent.velocity = Vector3.zero;
                        agent.nextPosition = transform.position;
                    }

                    int attackIndex = Random.Range(0, 5);
                    if (animHandler != null)
                        animHandler.PlayAttack(attackIndex);

                    attackCooldownTimer = attackCooldownDuration;
                }
            }
        }
    }

    // test
    void OnDrawGizmosSelected()
    {
        // Draw detection range (Scan circle)
        Gizmos.color = Color.yellow;
        float detectionRadius = Application.isPlaying ? currentDetectionRange : baseDetectionRange;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Draw chase range
        Gizmos.color = Color.cyan;
        float chaseRadius = Application.isPlaying ? currentChaseRange : chaseRange;
        Gizmos.DrawWireSphere(transform.position, chaseRadius);

        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Draw a line to the current target if it exists
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}