using UnityEngine;
using UnityEngine.AI;

public class ArcherCombat : NPCCombat
{
    [Header("AAA Archer FSM & Melee")]
    public float meleeRange = 2f;
    public float meleeReactionWindow = 1.5f; // Lưu trạng thái bị đánh trong 1.5s
    public float meleeCooldown = 5f;         // Tránh spam đánh cận chiến
    
    private float meleeCooldownTimer = 0f;
    private float lastTimeDamaged = -999f;   // Lưu thời điểm bị đánh cuối cùng

    [Header("AAA Archer Aiming")]
    public float aimDuration = 1.5f;
    private bool isAiming = false;
    private float aimTimer = 0f;

    private ArcherAnimatorHandler archerAnim;

    protected override void Start()
    {
        base.Start(); // Gọi lại các thiết lập cơ bản của NPCCombat
        archerAnim = GetComponent<ArcherAnimatorHandler>();
        
        // Lắng nghe sự kiện bị đánh để kích hoạt Melee Reaction
        if (MyHealth != null)
        {
            MyHealth.onDamaged += OnArcherDamaged;
        }
    }

    private void OnDestroy()
    {
        if (MyHealth != null)
        {
            MyHealth.onDamaged -= OnArcherDamaged;
        }
    }

    private void OnArcherDamaged(GameObject attacker, float damage)
    {
        lastTimeDamaged = Time.time; // Lưu lại thời điểm bị đánh
    }

    protected override void Update()
    {
        // Trừ thời gian hồi chiêu Melee
        if (meleeCooldownTimer > 0) 
            meleeCooldownTimer -= Time.deltaTime;

        base.Update(); // Vẫn chạy logic Perception, RootMotion của NPCCombat
    }

    // GHI ĐÈ LOGIC TẤN CÔNG (Bao gồm bắn cung và Melee)
    protected override void HandleAttack()
    {
        if (CurrentTarget == null || !MyHealth.isAlive) return;

        float dist = Vector3.Distance(transform.position, CurrentTarget.position);

        // 1. KIỂM TRA ĐÁNH CẬN CHIẾN (Ưu tiên cao nhất)
        if (dist <= meleeRange)
        {
            // Nếu bị đánh trong vòng 1.5s đổ lại VÀ đã hết hồi chiêu cận chiến
            if (Time.time - lastTimeDamaged <= meleeReactionWindow && meleeCooldownTimer <= 0)
            {
                isAiming = false;
                archerAnim.SetAiming(false);
                
                archerAnim.PlayMeleeAttack(); // Gọi Animator chém/đá
                meleeCooldownTimer = meleeCooldown; // Reset Cooldown
                attackCooldownTimer = attackCooldownDuration; // Khóa luôn bắn cung tạm thời
                return; 
            }
        }

        // 2. LOGIC BẮN CUNG
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
            return;
        }

        if (dist <= attackRange)
        {
            if (!isAiming) // Bắt đầu giương cung
            {
                isAiming = true;
                aimTimer = aimDuration;
                archerAnim.SetAiming(true);
            }
            else // Đang giương cung, chờ đủ thời gian thì bắn
            {
                aimTimer -= Time.deltaTime;
                if (aimTimer <= 0)
                {
                    archerAnim.PlayRangedAttack();
                    attackCooldownTimer = attackCooldownDuration;
                    isAiming = false; // Bắn xong hạ cung xuống
                    archerAnim.SetAiming(false);
                }
            }
        }
        else
        {
            // Nếu địch ra khỏi tầm ngắm, hạ cung xuống để chạy cho lẹ
            if (isAiming)
            {
                isAiming = false;
                archerAnim.SetAiming(false);
            }
        }
    }

    // GHI ĐÈ LOGIC DI CHUYỂN (Thả diều + Xoay mặt)
    protected override void HandleMovement()
    {
        if (archerAnim != null && archerAnim.IsUsingRootMotion) return;

        // Xử lý trọng lực
        if (controller.isGrounded) verticalVelocity = -2f; 
        else verticalVelocity -= 9.81f * Time.deltaTime;

        if (CurrentTarget == null || !MyHealth.isAlive)
        {
            agent.ResetPath();
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            UpdateAnimatorBlendTree();
            return;
        }

        float dist = Vector3.Distance(transform.position, CurrentTarget.position);
        Vector3 dirToTarget = (CurrentTarget.position - transform.position);
        dirToTarget.y = 0;

        // THẢ DIỀU (KITING) KHI ĐỊCH LẠI QUÁ GẦN
        if (dist < kiteRange)
        {
            Vector3 fleeDirection = -dirToTarget.normalized;
            Vector3 fleePos = transform.position + fleeDirection * 5f;
            
            // Dùng SamplePosition để tránh văng map như đã thảo luận
            NavMeshHit hit;
            if (NavMesh.SamplePosition(fleePos, out hit, 5f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
            else
            {
                agent.SetDestination(fleePos);
            }

            if (isAiming) 
            {
                // ĐÃ GIƯƠNG CUNG: Mặt luôn khóa vào Player, Agent đi lùi
                agent.speed = walkSpeed; 
                LookAtTarget(dirToTarget);
            }
            else
            {
                // CHƯA GIƯƠNG CUNG: Quay lưng cắm đầu chạy
                agent.speed = runSpeed;
                LookAtTarget(agent.desiredVelocity);
            }
        }
        else if (dist > attackRange)
        {
            // TIẾN LẠI GẦN
            agent.SetDestination(CurrentTarget.position);
            agent.speed = isAiming ? walkSpeed : runSpeed; // Vừa đi vừa ngắm thì chậm, hạ cung thì chạy nhanh
            
            if (isAiming) LookAtTarget(dirToTarget);
            else LookAtTarget(agent.desiredVelocity);
        }
        else
        {
            // TRONG TẦM BẮN (Đứng lại ngắm)
            agent.ResetPath();
            LookAtTarget(dirToTarget);
        }

        // Apply Movement
        Vector3 finalVelocity = agent.desiredVelocity;
        finalVelocity.y = verticalVelocity;
        controller.Move(finalVelocity * Time.deltaTime);
        agent.nextPosition = transform.position;

        // Truyền thông số trục X, Y cho Animator BlendTree
        UpdateAnimatorBlendTree();
    }

    private void LookAtTarget(Vector3 lookDirection)
    {
        lookDirection.y = 0;
        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    private void UpdateAnimatorBlendTree()
    {
        if (archerAnim == null) return;

        // Phép màu AAA: Chuyển Vận tốc Global sang Local, sử dụng controller.velocity thay vì agent.velocity để chính xác frame-by-frame
        Vector3 localVelocity = transform.InverseTransformDirection(controller.velocity);
        
        // Đưa về tỉ lệ 0 -> 1 dựa trên max speed (runSpeed)
        float speedNormalizedX = localVelocity.x / runSpeed;
        float speedNormalizedZ = localVelocity.z / runSpeed;

        archerAnim.UpdateBlendTree(speedNormalizedX, speedNormalizedZ);
    }
}
