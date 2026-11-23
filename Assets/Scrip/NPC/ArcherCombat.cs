using UnityEngine;
using UnityEngine.AI;

public class ArcherCombat : NPCCombat
{
    [Header("Melee")]
    public float meleeRange = 1.5f;
    public float meleeCooldown = 3.5f;

    private float meleeCooldownTimer = 0f;
    private float lastTimeDamaged = -999f;

    [Header("Aiming & Kiting")]
    public float kiteRange = 6.5f;

    public float kiteSafeDistance = 9.5f;

    public float aimDuration = 1.5f;
    [Header("Ranged Attack Cooldown (Thời gian nghỉ sau bắn)")]
    public float minShootCooldown = 1.8f;
    public float maxShootCooldown = 2.6f;
    public float sprintSpeed = 7.2f;
    public float sprintBurstDuration = 1.2f;
    public float executeHealthThreshold = 0.2f;

    [Header("Turnaround Strike (Hit & Run)")]
    public float maxKiteTimePlayer = 3.0f;
    public float maxKiteTimeNPC = 7.0f;
    public float turnaroundMeleeRange = 6f;
    public float snapAimDuration = 0.8f;

    private bool isAiming = false;
    private float aimTimer = 0f;
    private bool isKiting = false;
    private float sprintBurstTimer = 0f;
    private float kiteTimer = 0f;
    private bool isTurnaroundStriking = false;

    private enum TurnaroundAction { None, AimingSnapShot, ExecutingMelee, Recovering }
    private TurnaroundAction turnaroundAction = TurnaroundAction.None;
    private float turnaroundTimer = 0f;

    [Header("Chasing Fatigue (Đuối sức khi rượt đuổi quá lâu)")]
    public float maxChaseTime = 5.0f;

    public float chaseFatigueCooldown = 2.5f;

    public float maxFatigueSnipeDistance = 35f;

    public float maxAimShootDistance = 29f;

    private float chaseTimer = 0f;
    private float chaseFatigueTimer = 0f;
    private bool isFatigueSniping = false;

    [Header("Archer Evade Roll")]
    public float archerRollChance = 0.7f;

    [Header("Archer Skill Settings")]
    [Range(0f, 1f)]
    public float highVolleySkillChance = 0.35f;
    private bool isNextShotHighSkill = false;

    [Header("Archer Targeting Limits")]
    public int maxTotalAttackers = 5; 

    [Header("Archer Targeting Scoring")]
    public float weightLowHealth = 0.5f;
    public float weightArcherTarget = 0.35f;

    [Header("Debug")]
    public bool enableAnimDebug = false;
    private float debugTimer = 0f;

    private ArcherAnimatorHandler archerAnim;

    private void Reset()
    {
        attackRange = 25f;
        baseDetectionRange = 35f;
        chaseRange = 41f;
        maxDetectionRange = 118f;
        rollCooldown = 3.5f;
        maxTotalAttackers = 5;
    }

    protected override void Start()
    {
        if (maxTotalAttackers <= 0) maxTotalAttackers = 5;
        maxTotalAttackersPerTarget = maxTotalAttackers;
        base.Start();
        archerAnim = GetComponent<ArcherAnimatorHandler>();
        if (archerAnim != null && archerAnim.bowAnim != null)
        {
            equippedWeapons = new GameObject[] { archerAnim.bowAnim.gameObject };
        }
    }

    protected override void Update()
    {
        if (CinematicBattleDirector.Instance != null && CinematicBattleDirector.Instance.isActiveAndEnabled && CinematicBattleDirector.Instance.enableDirectorMode)
        {
            return;
        }

        if (meleeCooldownTimer > 0)
            meleeCooldownTimer -= Time.deltaTime;

        if (chaseFatigueTimer > 0)
            chaseFatigueTimer -= Time.deltaTime;

        base.Update();

        if (enableAnimDebug && CurrentTarget != null)
        {
            debugTimer += Time.deltaTime;
            if (debugTimer >= 0.4f)
            {
                debugTimer = 0f;
                float d = Vector3.Distance(transform.position, CurrentTarget.position);
                float curSpeed = archerAnim != null && archerAnim.GetComponent<Animator>() != null ? archerAnim.GetComponent<Animator>().GetFloat("Speed") : -1f;
                Vector3 horiz = controller.velocity;
                horiz.y = 0;
                Debug.Log($"[ARCHER DEBUG] Dist: {d:F1}m | Aim: {isAiming} | Kite: {isKiting} | ChaseTime: {chaseTimer:F1}s | Fatigue: {chaseFatigueTimer:F1}s | RealSpeed: {horiz.magnitude:F2}");
            }
        }
    }

    protected override void OnDamaged(GameObject attacker, float damage)
    {
        base.OnDamaged(attacker, damage);
        lastTimeDamaged = Time.time;

        if (attacker != null)
        {
            HealthAndTeam attackerHealth = attacker.GetComponentInParent<HealthAndTeam>();
            if (attackerHealth != null && attackerHealth.isAlive && myHealth.IsEnemy(attackerHealth.teamCurrent))
            {
                float distToAttacker = Vector3.Distance(transform.position, attacker.transform.position);
                // Nếu bị kẻ địch áp sát trong vòng nguy hiểm (hoặc cận chiến): Lập tức chuyển mục tiêu sang kẻ đó!
                if (distToAttacker <= kiteRange)
                {
                    SwitchToTarget(attackerHealth);
                    commitTimer = 0f; // Reset commit để không bị khóa cứng vào target ở xa
                }
            }
        }
    }

    protected override void CheckRollOnDamaged(GameObject attacker, float damage)
    {
        // 1. NẾU BLOCK THÀNH CÔNG (damage == 0):
        // Tuyệt đối KHÔNG Roll!
        if (damage <= 0f)
        {
            // Nếu đòn đá cận chiến sẵn sàng: lập tức tung cú đá phản kích (Parry Riposte)!
            if (meleeCooldownTimer <= 0)
            {
                if (attacker != null)
                {
                    Vector3 dir = (attacker.transform.position - transform.position);
                    dir.y = 0;
                    if (dir.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.LookRotation(dir);
                    }
                }

                if (archerAnim != null) archerAnim.PlayMeleeAttack();
                meleeCooldownTimer = meleeCooldown;
            }

            // Kích hoạt ngay chu kỳ thoát ly (bứt tốc chạy lấy lại cự ly an toàn)
            isTurnaroundStriking = false;
            turnaroundAction = TurnaroundAction.None;
            kiteTimer = 0f;
            isKiting = true;
            sprintBurstTimer = sprintBurstDuration;
            return;
        }

        // 2. NẾU BLOCK HỤT (bị chém mất máu damage > 0):
        // Cung thủ máu mỏng hoảng sợ: 85% cơ hội kích hoạt Roll lộn nhào thoát thân!
        if (myHealth.currentAttackers.Count >= 1 && Time.time >= rollTimer)
        {
            if (Random.value < archerRollChance)
            {
                // Tìm hướng trống và an toàn nhất để lộn nhào thoát thân (tránh đâm đầu vào người địch hoặc tường)
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

                if (archerAnim != null)
                {
                    archerAnim.PlayRoll();
                }

                rollTimer = Time.time + rollCooldown;

                // Khi Roll: Lập tức hủy giương cung và chuyển sang Kite bứt tốc
                if (isAiming)
                {
                    isAiming = false;
                    if (archerAnim != null) archerAnim.SetAiming(false);
                }

                isTurnaroundStriking = false;
                turnaroundAction = TurnaroundAction.None;
                kiteTimer = 0f;
                isKiting = true;
                sprintBurstTimer = sprintBurstDuration;
            }
        }
    }

    protected override void UpdateAnimationSpeed()
    {
        if (archerAnim == null) return;

        if (archerAnim.IsUsingRootMotion || isAiming)
        {
            archerAnim.UpdateLocomotion(0f);
            return;
        }

        Vector3 horizontalVel = controller.velocity;
        horizontalVel.y = 0;
        float animSpeed = horizontalVel.magnitude / runSpeed;
        if (horizontalVel.magnitude < 0.1f) animSpeed = 0f;
        archerAnim.UpdateLocomotion(Mathf.Clamp01(animSpeed));
    }

    // Archer chi kiem tra tong slot: khong quan tam melee hay archer, mien tong ke tan cong du maxTotalAttackers (5) chua
    protected override bool IsTargetFull(HealthAndTeam enemy)
    {
        if (enemy == null) return false;
        return enemy.GetTotalAttackerCount() >= maxTotalAttackers;
    }

    protected override float ComputeGlobalScore(HealthAndTeam enemy)
    {
        // Archer tinh score theo tong so ke tan cong (GetTotalAttackerCount) thay vi chi moi melee
        float dist = Vector3.Distance(transform.position, enemy.transform.position);
        float normDist = Mathf.Clamp01(dist / currentDetectionRange);
        float normTotalAtt = Mathf.Clamp01((float)enemy.GetTotalAttackerCount() / maxExpectedAttackers);

        float threat = myHealth.GetThreat(enemy.gameObject);
        float normThreat = Mathf.Clamp01(threat / maxThreatForNorm);

        float score = (normTotalAtt * weightAttackerCount) +
                      (normDist * weightDistance) -
                      (normThreat * weightThreat);

        if (!ignorePlayerPriority && enemy.CompareTag("Player"))
        {
            score -= 1.5f;
        }

        // 1. Ưu tiên mục tiêu thấp máu để tỉa kết liễu
        if (enemy.maxHealth > 0)
        {
            float hpPercent = enemy.currentHealth / enemy.maxHealth;
            if (hpPercent <= 0.35f)
            {
                score -= (1f - hpPercent) * weightLowHealth;
            }
        }

        // 2. Ưu tiên bắn Archer đối phương (Counter-Sniper)
        if (enemy.GetComponent<ArcherCombat>() != null)
        {
            score -= weightArcherTarget;
        }

        // 3. Phạt nếu mục tiêu đã có nhiều kẻ địch tấn công (tổng >= 3)
        int totalAtt = enemy.GetTotalAttackerCount();
        if (totalAtt >= 3)
        {
            score += 0.35f * (totalAtt - 2);
        }

        // 4. Nếu vừa rượt đuổi mục tiêu này quá lâu dẫn đến đuối sức: Tạm thời trừ điểm để ưu tiên mục tiêu khác ở gần hơn
        if (chaseFatigueTimer > 0f && CurrentTarget != null && enemy.transform == CurrentTarget)
        {
            score += 0.5f;
        }

        return score;
    }

    private bool IsTargetPlayer()
    {
        if (CurrentTarget != null)
        {
            return CurrentTarget.CompareTag("Player") || (playerObject != null && CurrentTarget.gameObject == playerObject);
        }
        return false;
    }

    protected override void HandleAttack()
    {
        if (CurrentTarget == null || !MyHealth.isAlive) return;

        float dist = Vector3.Distance(transform.position, CurrentTarget.position);
        Vector3 dirToTarget = (CurrentTarget.position - transform.position);
        dirToTarget.y = 0;

        // Kiểm tra xem có bất kỳ kẻ thù nào đang ở cự ly cận chiến nguy hiểm (< kiteRange) không
        if (!IsCulled)
        {
            float nearestEnemyDist;
            HealthAndTeam nearestEnemy = GetNearestThreatEnemy(out nearestEnemyDist);

            if (nearestEnemy != null && nearestEnemyDist < kiteRange)
            {
                // Nếu có kẻ địch nguy hiểm áp sát và target hiện tại đang ở xa: Chuyển mục tiêu sang kẻ áp sát để tập trung ứng phó
                if (nearestEnemy != currentTargetHealth && dist > kiteRange)
                {
                    SwitchToTarget(nearestEnemy);
                    dist = nearestEnemyDist;
                    dirToTarget = (CurrentTarget.position - transform.position);
                    dirToTarget.y = 0;
                }
            }
        }
        float angleToTarget = (dirToTarget != Vector3.zero) ? Vector3.Angle(transform.forward, dirToTarget.normalized) : 0f;

        // Kiểm tra đối thủ có đang hấp hối không (ngưỡng kết liễu <= executeHealthThreshold)
        bool targetIsLowHealth = currentTargetHealth != null && 
                                 currentTargetHealth.isAlive && 
                                 currentTargetHealth.maxHealth > 0 &&
                                 (currentTargetHealth.currentHealth / currentTargetHealth.maxHealth <= executeHealthThreshold);

        // 1. Kiểm tra xem còn tên trong bao để bắn không
        bool hasArrow = archerAnim == null || archerAnim.HasArrow();

        // Nếu hết tên trong bao: Chỉ đánh cận chiến
        if (!hasArrow)
        {
            if (isAiming)
            {
                isAiming = false;
                archerAnim.SetAiming(false);
            }

            LookAtTarget(dirToTarget);

            if (dist <= meleeRange && attackCooldownTimer <= 0 && angleToTarget <= 35f)
            {
                archerAnim.PlayMeleeAttack();
                attackCooldownTimer = attackCooldownDuration;
            }
            return;
        }

        // 2. XỬ LÝ PHẢN ĐÒN QUAY ĐẦU (Turnaround Strike)
        // Ưu tiên cao nhất khi tới ngưỡng thời gian: đứng vững tung đòn phản công, không để logic khác hủy ngang!
        if (isTurnaroundStriking)
        {
            LookAtTarget(dirToTarget);

            if (turnaroundAction == TurnaroundAction.None)
            {
                // Snap xoay mặt tức thì về phía địch ngay frame đầu tiên
                if (dirToTarget.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(dirToTarget);
                }

                // Nếu kẻ địch ở cự ly áp sát (<= turnaroundMeleeRange) HOẶC hết tên trong bao: Dùng Cú Đá Cận Chiến!
                if (dist <= turnaroundMeleeRange || !hasArrow)
                {
                    turnaroundAction = TurnaroundAction.ExecutingMelee;
                    turnaroundTimer = 0.75f; // Thời gian chờ đòn đá diễn ra

                    if (archerAnim != null) archerAnim.PlayMeleeAttack();
                    meleeCooldownTimer = meleeCooldown;
                    attackCooldownTimer = 1.2f;
                }
                else
                {
                    // Nếu kẻ địch ở khoảng cách xa hơn (> turnaroundMeleeRange) và còn tên: Bắn nhanh cấp tốc (Snap Shot)
                    turnaroundAction = TurnaroundAction.AimingSnapShot;
                    turnaroundTimer = snapAimDuration; // vd: 0.45s

                    isAiming = true;
                    if (archerAnim != null) archerAnim.SetAiming(true);
                }
                return;
            }
            else if (turnaroundAction == TurnaroundAction.AimingSnapShot)
            {
                // Snap hướng nhìn thẳng vào đối thủ trong suốt thời gian ngắm
                if (dirToTarget.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(dirToTarget);
                }

                // KHÓA CHẶT TRẠNG THÁI NGẮM BẮN (State Commitment):
                // Kẻ địch có áp sát lại gần cũng KHÔNG hủy ngang aim để đổi sang melee!
                turnaroundTimer -= Time.deltaTime;
                if (turnaroundTimer <= 0f)
                {
                    if (archerAnim != null) archerAnim.PlayRangedAttack();
                    attackCooldownTimer = attackCooldownDuration;
                    meleeCooldownTimer = 2.0f; // Vừa bắn xong: cooldown cận chiến 2s để tập trung chạy thoát thân
                    isAiming = false;
                    if (archerAnim != null) archerAnim.SetAiming(false);

                    // Chờ thêm 0.35s cho animation nhả tên diễn ra trọn vẹn trước khi quay đầu chạy
                    turnaroundAction = TurnaroundAction.Recovering;
                    turnaroundTimer = 0.35f;
                }
                return;
            }
            else if (turnaroundAction == TurnaroundAction.ExecutingMelee)
            {
                turnaroundTimer -= Time.deltaTime;
                if (turnaroundTimer <= 0f)
                {
                    turnaroundAction = TurnaroundAction.None;
                    isTurnaroundStriking = false;
                    kiteTimer = 0f;
                    isKiting = true;
                    sprintBurstTimer = sprintBurstDuration;
                }
                return;
            }
            else if (turnaroundAction == TurnaroundAction.Recovering)
            {
                turnaroundTimer -= Time.deltaTime;
                if (turnaroundTimer <= 0f)
                {
                    turnaroundAction = TurnaroundAction.None;
                    isTurnaroundStriking = false;
                    kiteTimer = 0f;
                    isKiting = true;
                    sprintBurstTimer = sprintBurstDuration;
                }
                return;
            }

            return;
        }

        // 3. Cận chiến tự vệ bình thường khi bị đối thủ áp sát quá gần (dưới meleeRange)
        if (dist <= meleeRange)
        {
            if (isAiming)
            {
                isAiming = false;
                if (archerAnim != null) archerAnim.SetAiming(false);
            }

            // Dừng lại xoay mặt đánh nếu đòn cận chiến sẵn sàng HOẶC đối thủ đang hấp hối
            if (meleeCooldownTimer <= 0 || targetIsLowHealth)
            {
                // Snap xoay mặt tức thì về phía địch để cú đá luôn trúng đích, không đá vào không khí
                if (dirToTarget.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(dirToTarget);
                }

                if (meleeCooldownTimer <= 0)
                {
                    if (archerAnim != null) archerAnim.PlayMeleeAttack();
                    meleeCooldownTimer = meleeCooldown;
                    attackCooldownTimer = 1.2f;
                }
                return;
            }
            // Nếu đòn cận chiến đang hồi chiêu: Không xoay mặt lại, để HandleMovement tiếp tục chạy thoát thân!
        }

        // 4. KHÓA CỨNG KHI ĐANG KITING: CẤM TUYỆT ĐỐI GIƯƠNG CUNG / BÓC TÊN!
        if (isKiting)
        {
            if (isAiming)
            {
                isAiming = false;
                if (archerAnim != null) archerAnim.SetAiming(false);
            }
            return;
        }

        // 5. Cơ chế giương cung và bắn tỉa
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
            return;
        }

        // Tầm bắn cho phép:
        // - Khi CHƯA ngắm: Phải vào tầm attackRange (25m) hoặc đang Fatigue Snipe (35m)
        // - Một khi ĐÃ RÚT TÊN NGẮM: Mở rộng vùng đệm ra maxAimShootDistance (29m) để cam kết bắn ra bằng được, chống hủy ngang giật cục/nhún!
        float currentMaxShootRange = isFatigueSniping ? maxFatigueSnipeDistance : (isAiming ? Mathf.Max(maxAimShootDistance, attackRange + 4f) : attackRange);
        float currentMinShootRange = isAiming ? (kiteRange - 1.2f) : kiteRange;

        bool canAimAndShoot = (dist <= currentMaxShootRange && dist >= currentMinShootRange) || 
                              (targetIsLowHealth && dist <= currentMaxShootRange && dist > meleeRange) ||
                              (isFatigueSniping && dist <= maxFatigueSnipeDistance);

        if (canAimAndShoot)
        {
            LookAtTarget(dirToTarget);

            if (!isAiming)
            {
                isAiming = true;
                // Nếu đang Fatigue Snipe: 100% dùng skill mưa tên 70 độ bắn lên trời!
                // Nếu bắn bình thường: dùng tỉ lệ ngẫu nhiên 50%
                isNextShotHighSkill = isFatigueSniping ? true : (Random.value < highVolleySkillChance);
                aimTimer = isFatigueSniping ? aimDuration * 0.85f : aimDuration;
                SafeResetPath();
                if (agent != null) agent.velocity = Vector3.zero;
                if (archerAnim != null) archerAnim.SetAiming(true, isNextShotHighSkill);
            }
            else
            {
                aimTimer -= Time.deltaTime;
                if (aimTimer <= 0 && angleToTarget <= 35f)
                {
                    if (archerAnim != null) archerAnim.PlayRangedAttack();
                    attackCooldownTimer = Random.Range(minShootCooldown, maxShootCooldown);
                    meleeCooldownTimer = 1.5f;
                    isAiming = false;
                    isFatigueSniping = false; // Hoàn tất cú bắn tỉa khi đuối sức
                    isNextShotHighSkill = false;
                    if (archerAnim != null) archerAnim.SetAiming(false);

                    // BẮN XONG: LẬP TỨC QUÉT VÀ TÌM LẠI MỤC TIÊU!
                    ReevaluateTargetAfterShot();
                }
            }
        }
        else
        {
            if (isAiming)
            {
                isAiming = false;
                isFatigueSniping = false;
                isNextShotHighSkill = false;
                if (archerAnim != null) archerAnim.SetAiming(false);
            }
        }
    }

    protected override void HandleMovement()
    {
        // 1. Khi đang Roll: Tuyệt đối KHÔNG ép nhìn vào địch! Để Root Motion tự do đưa nhân vật di chuyển theo hướng thoát hiểm đã chọn
        if (archerAnim != null && archerAnim.IsRolling)
        {
            SafeResetPath();
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            agent.nextPosition = transform.position;
            return;
        }

        // 2. Khi đang cận chiến (Kick/Punch) hoặc đang giơ khiên đỡ (Block): Luôn ép xoay mặt nhìn vào địch
        if (archerAnim != null && (archerAnim.IsMeleeAttacking || archerAnim.IsBlocked()))
        {
            if (CurrentTarget != null)
            {
                Vector3 dir = (CurrentTarget.position - transform.position);
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }
            SafeResetPath();
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            agent.nextPosition = transform.position;
            return;
        }

        if (controller.isGrounded) verticalVelocity = -2f;
        else verticalVelocity -= 9.81f * Time.deltaTime;

        if (CurrentTarget == null || !MyHealth.isAlive)
        {
            if (isAiming)
            {
                isAiming = false;
                if (archerAnim != null) archerAnim.SetAiming(false);
            }
            isTurnaroundStriking = false;
            turnaroundAction = TurnaroundAction.None;
            kiteTimer = 0f;
            chaseTimer = 0f;
            isFatigueSniping = false;
            SafeResetPath();
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            return;
        }

        float dist = Vector3.Distance(transform.position, CurrentTarget.position);
        Vector3 dirToTarget = (CurrentTarget.position - transform.position);
        dirToTarget.y = 0;

        // Kiểm tra xem có bất kỳ kẻ thù nào đang ở cự ly cận chiến nguy hiểm (< kiteRange) không
        if (!IsCulled)
        {
            float nearestEnemyDist;
            HealthAndTeam nearestEnemy = GetNearestThreatEnemy(out nearestEnemyDist);

            if (nearestEnemy != null && nearestEnemyDist < kiteRange)
            {
                // Nếu có kẻ địch nguy hiểm áp sát và target hiện tại đang ở xa: Chuyển mục tiêu sang kẻ áp sát để tập trung ứng phó
                if (nearestEnemy != currentTargetHealth && dist > kiteRange)
                {
                    SwitchToTarget(nearestEnemy);
                    dist = nearestEnemyDist;
                    dirToTarget = (CurrentTarget.position - transform.position);
                    dirToTarget.y = 0;
                }
            }
        }

        if (dist > currentChaseRange && !isFuryMode)
        {
            if (isAiming)
            {
                isAiming = false;
                if (archerAnim != null) archerAnim.SetAiming(false);
            }
            isTurnaroundStriking = false;
            turnaroundAction = TurnaroundAction.None;
            kiteTimer = 0f;
            chaseTimer = 0f;
            isFatigueSniping = false;
            ClearTarget();
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            return;
        }

        // Nếu hết tên trong bao: Chạy thẳng vào áp sát đối thủ để đấm đá
        bool hasArrow = archerAnim == null || archerAnim.HasArrow();
        if (!hasArrow)
        {
            SafeSetDestination(CurrentTarget.position);
            agent.speed = runSpeed;
            LookAtTarget(agent.desiredVelocity);

            Vector3 outVelocity = agent.desiredVelocity;
            outVelocity.y = verticalVelocity;
            controller.Move(outVelocity * Time.deltaTime);
            agent.nextPosition = transform.position;
            return;
        }

        bool isPlayer = IsTargetPlayer();

        // Kiểm tra đối thủ có đang hấp hối không (ngưỡng kết liễu <= executeHealthThreshold)
        bool targetIsLowHealth = currentTargetHealth != null && 
                                 currentTargetHealth.isAlive && 
                                 currentTargetHealth.maxHealth > 0 &&
                                 (currentTargetHealth.currentHealth / currentTargetHealth.maxHealth <= executeHealthThreshold);

        // 1. Kiểm tra điều kiện Phản đòn khi bị rượt đuổi quá lâu (Turnaround Strike)
        bool isOverwhelmed = myHealth.currentAttackers.Count >= 2;
        float maxKiteTime = isPlayer ? maxKiteTimePlayer : maxKiteTimeNPC;

        if (isKiting && !targetIsLowHealth)
        {
            kiteTimer += Time.deltaTime;

            // Nếu bị dí quá lâu mà không bị hội đồng: Kích hoạt Phản đòn quay đầu!
            if (kiteTimer >= maxKiteTime && !isOverwhelmed && !isTurnaroundStriking)
            {
                isTurnaroundStriking = true;
                turnaroundAction = TurnaroundAction.None;
                turnaroundTimer = 0f;
            }
        }
        else if (!isTurnaroundStriking)
        {
            kiteTimer = 0f;
        }

        // KHI ĐANG THỰC HIỆN CÚ PHẢN ĐÒN QUAY ĐẦU: Khóa cứng vị trí và xoay mặt đối đầu, CẤM TUYỆT ĐỐI di chuyển lùi!
        if (isTurnaroundStriking)
        {
            SafeResetPath();
            LookAtTarget(dirToTarget);
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            agent.nextPosition = transform.position;
            return;
        }

        // 2. Cận chiến khi sát rạt (<= meleeRange)
        if (dist <= meleeRange)
        {
            // Nếu kẻ địch sắp chết HOẶC Cung thủ vừa sẵn sàng đá cận chiến: đứng lại đá
            if (targetIsLowHealth || meleeCooldownTimer <= 0)
            {
                isKiting = false;
                kiteTimer = 0f;
                SafeResetPath();
                if (dirToTarget.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(dirToTarget);
                }
                controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
                agent.nextPosition = transform.position;
                return;
            }
            // Ngược lại, nếu đá đang cooldown và kẻ địch khỏe: cố gắng kite thoát thân
        }

        // 3. KHI ĐANG GIƯƠNG CUNG / NGẮM BẮN: KHÓA CỨNG VỊ TRÍ TẠI CHỖ, CẤM TUYỆT ĐỐI DI CHUYỂN HOẶC TRƯỢT CHÂN!
        if (isAiming)
        {
            SafeResetPath();
            if (agent != null) agent.velocity = Vector3.zero;
            LookAtTarget(dirToTarget);
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            agent.nextPosition = transform.position;
            return;
        }

        // Quyết định vào/ra trạng thái Thả diều (State Commitment)
        if (dist < kiteRange && !targetIsLowHealth)
        {
            if (!isKiting)
            {
                isKiting = true;
                kiteTimer = 0f;
                sprintBurstTimer = sprintBurstDuration; // Kích hoạt Sprint Burst bứt tốc!
            }
        }
        else if (isKiting && (dist >= kiteSafeDistance || targetIsLowHealth))
        {
            isKiting = false;
            isTurnaroundStriking = false;
            turnaroundAction = TurnaroundAction.None;
            kiteTimer = 0f;
            sprintBurstTimer = 0f;
        }

        // THỰC HIỆN THẢ DIỀU (Kiting)
        if (isKiting)
        {
            Vector3 fleeDirection = -dirToTarget.normalized;
            float fleeDistance = 5f;

            int obstacleMask = Physics.DefaultRaycastLayers & ~characterLayerMask;
            Vector3 rayOrigin = transform.position + Vector3.up;
            if (Physics.Raycast(rayOrigin, fleeDirection, fleeDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 rightFleeDir = Vector3.Cross(Vector3.up, fleeDirection).normalized;
                Vector3 leftFleeDir = -rightFleeDir;

                bool rightClear = !Physics.Raycast(rayOrigin, rightFleeDir, fleeDistance, obstacleMask, QueryTriggerInteraction.Ignore);
                bool leftClear = !Physics.Raycast(rayOrigin, leftFleeDir, fleeDistance, obstacleMask, QueryTriggerInteraction.Ignore);

                if (rightClear && !leftClear)
                {
                    fleeDirection = rightFleeDir;
                }
                else if (leftClear && !rightClear)
                {
                    fleeDirection = leftFleeDir;
                }
                else
                {
                    fleeDirection = Random.value > 0.5f ? rightFleeDir : leftFleeDir;
                }
            }

            Vector3 fleePos = transform.position + fleeDirection * fleeDistance;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(fleePos, out hit, 5f, NavMesh.AllAreas))
            {
                SafeSetDestination(hit.position);
            }
            else
            {
                SafeSetDestination(fleePos);
            }

            // Bứt tốc trong sprintBurstDuration đầu tiên của pha Kite
            if (sprintBurstTimer > 0f)
            {
                sprintBurstTimer -= Time.deltaTime;
                agent.speed = sprintSpeed;
            }
            else
            {
                agent.speed = runSpeed;
            }

            LookAtTarget(agent.desiredVelocity);
        }
        else if (dist > attackRange && (chaseFatigueTimer > 0f || isFatigueSniping))
        {
            // Đang bị đuối sức / ngắm phát Fatigue Snipe tầm xa: Dừng chân tại chỗ, không chạy tiếp
            SafeResetPath();
            LookAtTarget(dirToTarget);
        }
        else if (dist > attackRange)
        {
            // Cập nhật thời gian rượt đuổi ở cự ly xa (>25m)
            if (!isKiting && !isTurnaroundStriking && chaseFatigueTimer <= 0f)
            {
                chaseTimer += Time.deltaTime;
                if (chaseTimer >= maxChaseTime)
                {
                    // Đuổi liên tục quá 5s mà chưa vào được 25m: Archer bị đuối sức!
                    // Dừng chân tại chỗ, kích hoạt cú bắn tỉa cầu vồng tầm xa (Fatigue Snipe)
                    chaseTimer = 0f;
                    isFatigueSniping = true;
                    chaseFatigueTimer = chaseFatigueCooldown;
                    SafeResetPath();
                }
            }

            SafeSetDestination(CurrentTarget.position);
            agent.speed = runSpeed;
            LookAtTarget(agent.desiredVelocity);
        }
        else
        {
            // Vào trong tầm 25m: Reset thời gian đuổi và dừng lại bắn luôn!
            chaseTimer = 0f;
            SafeResetPath();
            if (agent != null) agent.velocity = Vector3.zero;
            LookAtTarget(dirToTarget);
        }

        Vector3 finalVelocity = agent.desiredVelocity;
        finalVelocity.y = verticalVelocity;
        controller.Move(finalVelocity * Time.deltaTime);
        agent.nextPosition = transform.position;
    }

    private void LookAtTarget(Vector3 lookDirection)
    {
        lookDirection.y = 0;
        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * 2.5f * Time.deltaTime);
        }
    }

    private void ReevaluateTargetAfterShot()
    {
        commitTimer = 0f;
        HealthAndTeam best = DecideTarget();
        if (best != null && best != currentTargetHealth)
        {
            SwitchToTarget(best);
        }
    }

    private HealthAndTeam GetNearestThreatEnemy(out float nearestDist)
    {
        nearestDist = float.MaxValue;
        HealthAndTeam nearest = null;

        // 1. Kiểm tra những kẻ đang tấn công mình
        if (myHealth != null && myHealth.currentAttackers != null)
        {
            foreach (var att in myHealth.currentAttackers)
            {
                if (att == null) continue;
                HealthAndTeam h = att.GetComponentInParent<HealthAndTeam>();
                if (h != null && h.isAlive && myHealth.IsEnemy(h.teamCurrent))
                {
                    float d = Vector3.Distance(transform.position, att.transform.position);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = h;
                    }
                }
            }
        }

        // 2. Kiểm tra những kẻ địch nhìn thấy xung quanh
        if (visibleEnemies != null)
        {
            foreach (var enemy in visibleEnemies)
            {
                if (enemy == null || !enemy.isAlive) continue;
                float d = Vector3.Distance(transform.position, enemy.transform.position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = enemy;
                }
            }
        }

        return nearest;
    }


}
