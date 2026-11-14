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

    [Header("Targeting Limits")]
    public int maxAttackersPerTarget = 3;      // Toi da so luong can chien bu vao 1 muc tieu
    public int maxTotalAttackersPerTarget = 5; // Toi da tong so nguoi tan cong (can chien + cung) vao 1 muc tieu

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

    protected CharacterController controller;
    protected float verticalVelocity;

    protected NavMeshAgent agent;
    protected HealthAndTeam myHealth;
    protected NPCAnimatorHandler animHandler;

    // Public properties cho NPCAnimatorHandler đọc
    public HealthAndTeam MyHealth { get { return myHealth; } }
    public float VerticalVelocity { get { return verticalVelocity; } }
    public Transform CurrentTarget { get { return currentTarget; } }

    // target
    protected Transform currentTarget;
    protected HealthAndTeam currentTargetHealth;
    protected bool ignorePlayerPriority;

    // timer & range
    protected float lastSwitchTime;
    protected float commitTimer;
    protected float noTargetTimer;
    protected float currentDetectionRange;
    protected float currentChaseRange;
    protected float ignorePlayerPriorityTimer;
    public float ignoreDuration = 3f;
    public float rollCooldown = 5f;
    protected float rollTimer = 0f;

    // fury
    protected bool isFuryMode;
    protected float furyTimer;

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

        characterLayerMask = LayerMask.GetMask("Character");

        playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            playerHealth = playerObject.GetComponent<HealthAndTeam>();

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

        scanOffset = Random.Range(0f, scanInterval);
    }

    protected virtual void Update()
    {
        HandleFury();

        if (animHandler != null)
            animHandler.UpdateRootMotionState();

        UpdateAnimationSpeed();
        HandleAttack();

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

    void UpdatePerception()
    {
        visibleEnemies.Clear();

        // scan nhanh
        Collider[] hits = Physics.OverlapSphere(transform.position, currentDetectionRange, characterLayerMask);
        foreach (var hit in hits)
        {
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
        if (Time.time < commitTimer) return;

        HealthAndTeam best = DecideTarget();

        if (best != null && best != currentTargetHealth)
        {
            float cooldown = GetEffectiveCooldown();
            if (Time.time - lastSwitchTime >= cooldown)
            {
                SwitchToTarget(best);
                commitTimer = Time.time + commitLockTime;
                lastSwitchTime = Time.time;
            }
        }
    }

    protected virtual HealthAndTeam DecideTarget()
    {
        float bestScore = float.MaxValue;
        HealthAndTeam best = null;

        // Reactive layer
        if (myHealth.currentAttackers.Count > 0)
        {
            Transform lowestAttacker = GetLowestHealthAttacker();
            if (lowestAttacker != null)
            {
                HealthAndTeam h = lowestAttacker.GetComponent<HealthAndTeam>();
                float reactiveScore = ComputeReactiveScore(h);
                best = h;
                bestScore = reactiveScore;
            }
        }

        // Global layer
        foreach (var enemy in visibleEnemies)
        {
            float score = ComputeGlobalScore(enemy);

            // uu tien target cu 1 ti
            if (enemy == currentTargetHealth)
                score -= hysteresisBonus;

            // skip target da day nguoi
            bool isMyCurrentTarget = (enemy == currentTargetHealth);
            bool isFull = IsTargetFull(enemy);

            if (isFull && !isMyCurrentTarget)
                continue;

            // neu zone day, cong them diem phat lon de uu tien cac zone trong hon
            if (BattlefieldManager.Instance != null && BattlefieldManager.Instance.IsZoneCrowded(enemy.transform.position))
            {
                if (!isMyCurrentTarget)
                {
                    score += 5.0f;
                }
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        // Fury
        if (isFuryMode && playerHealth != null && playerHealth.isAlive && myHealth.IsEnemy(playerHealth.teamCurrent))
        {
            float distToPlayer = Vector3.Distance(transform.position, playerObject.transform.position);
            if (distToPlayer <= currentDetectionRange)
            {
                float furyScore = ComputeGlobalScore(playerHealth) - 999f;
                if (furyScore < bestScore)
                    best = playerHealth;
            }
        }

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
                agent.SetDestination(dest);
            }
        }
    }

    protected void ClearTarget()
    {
        if (currentTargetHealth != null)
            currentTargetHealth.RemoveAttacker(gameObject);

        currentTarget = null;
        currentTargetHealth = null;
        SafeResetPath();
        agent.nextPosition = transform.position;
    }

    protected virtual void HandleMovement()
    {
        if (animHandler != null && animHandler.IsUsingRootMotion) return;

        if (controller.isGrounded)
            verticalVelocity = -2f;
        else
            verticalVelocity -= 9.81f * Time.deltaTime;

        if (currentTarget == null || currentTargetHealth == null || !currentTargetHealth.isAlive)
        {
            SafeResetPath();
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            agent.nextPosition = transform.position;
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

        if (dist > attackRange)
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
            // dung yen quay mat
            SafeResetPath();

            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);

            Vector3 dirToTarget = (currentTarget.position - transform.position);
            dirToTarget.y = 0;
            if (dirToTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToTarget.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
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
        if (attacker != null)
        {
            HealthAndTeam attackerHealth = attacker.GetComponent<HealthAndTeam>();
            if (attackerHealth != null && attackerHealth.isAlive && myHealth.IsEnemy(attackerHealth.teamCurrent))
            {
                myHealth.AddAttacker(attacker);

                // Nếu đang rảnh rỗi chưa có target (hoặc target cũ đã chết): Phản xạ lập tức quay lại target kẻ đã đánh mình!
                if (currentTarget == null || currentTargetHealth == null || !currentTargetHealth.isAlive)
                {
                    float dist = Vector3.Distance(transform.position, attacker.transform.position);
                    // Nới rộng tầm rượt đuổi nếu bị bắn tỉa từ ngoài tầm chase cơ bản
                    if (dist > currentChaseRange)
                    {
                        currentChaseRange = Mathf.Min(dist + 5f, maxDetectionRange);
                        currentDetectionRange = Mathf.Min(dist + 5f, maxDetectionRange);
                    }

                    SwitchToTarget(attackerHealth);
                    commitTimer = Time.time + commitLockTime;
                    lastSwitchTime = Time.time;
                }
            }

            if (attacker.CompareTag("Player"))
            {
                isFuryMode = true;
                furyTimer = furyDuration;
            }
            else
            {
                ignorePlayerPriority = true;
                ignorePlayerPriorityTimer = ignoreDuration;
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
        ClearTarget();
        myHealth.threatMap.Clear();

        if (isRegisteredInGrid && BattlefieldManager.Instance != null)
        {
            BattlefieldManager.Instance.UnregisterNPC(this, currentGridCell);
            isRegisteredInGrid = false;
        }

        if (animHandler != null)
        {
            int deadIndex = Random.Range(0, 3);
            animHandler.PlayDeath(deadIndex);
        }
        agent.isStopped = true;
        enabled = false;
    }

    protected virtual void UpdateAnimationSpeed()
    {
        if (animHandler == null) return;

        if (animHandler.IsUsingRootMotion)
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