using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public ThirdPersonCamera cam;
    public Animator anim;
    public PlayerCombat combat;

    [Header("Speed")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 5f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    [Header("Turn")]
    public float turnCooldown = 0.8f;

    [Header("Roll")]
    public float rollCooldown = 5f;
    private float lastRollTime = -10f;
    [Header("Roll UI")]
    public Image rollCooldownImage;
    public Text rollCooldownText;
    private List<Collider> ignoredColliders = new List<Collider>();
    private bool wasRollingLastFrame = false;

    private CharacterController controller;
    private float verticalVelocity;

    private bool isTurning = false;
    private float lastTurnTime = -10f;
    private bool isDead = false;

    public static PlayerMovement Instance { get; private set; }
    public static Transform PlayerTransform { get; private set; }

    void Awake()
    {
        Instance = this;
        PlayerTransform = transform;
        if (!CompareTag("Player"))
        {
            gameObject.tag = "Player";
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        combat = GetComponent<PlayerCombat>();

        if (anim == null)
            anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        UpdateRollCooldownUI();

        if (isDead)
        {
            anim.applyRootMotion = true;
            return;
        }

        // Khi đang giả chết: Khóa di chuyển thường bằng code, nhưng VẪN PHẢI CHẠY TRỌNG LỰC ĐỂ HÚT SÁT MẶT ĐẤT
        if (PlayerFeignDeath.Instance != null && PlayerFeignDeath.Instance.IsFeignDead)
        {
            HandleGravity();
            anim.applyRootMotion = true;
            return;
        }

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = anim.GetNextAnimatorStateInfo(0);

        bool isRolling = state.IsTag("Roll") || (anim.IsInTransition(0) && next.IsTag("Roll"));
        bool isImpact = state.IsTag("Impact") || (anim.IsInTransition(0) && next.IsTag("Impact"));

        // ROLL INPUT (Phím Space)
        if (Input.GetKeyDown(KeyCode.Space) && !isRolling && !isImpact && Time.time - lastRollTime > rollCooldown)
        {
            PerformRoll();
            return;
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 inputDir = new Vector3(h, 0, v);

        // CHẶN đi lùi trong free mode
        if (!cam.IsLocked() && inputDir.z < 0)
        {
            inputDir.z = 0;
        }

        // tránh chạy chéo vượt tốc độ
        float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);
        bool isMoving = inputMagnitude > 0.1f;

        anim.SetBool("IsMoving", isMoving);

        // Chỉ tính là chạy khi: bấm Shift + có di chuyển + không đấm + không lăn + không dính đòn
        bool isAttacking = (combat != null && combat.IsAttacking()) || state.IsTag("Combo");
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving && !isAttacking && !isRolling && !isImpact;
        float currentRunSpeed = runSpeed;

        if (PlayerPanicSystem.Instance != null)
        {
            float panic = PlayerPanicSystem.Instance.currentPanic;
            if (panic >= 80f)
            {
                isRunning = false; // >= 80: Chân run lẩy bẩy, cấm chạy, chỉ cho đi bộ
            }
            else if (panic >= 50f)
            {
                currentRunSpeed *= 0.8f; // 50 -> 80: Giảm 20% tốc độ chạy
            }
        }

        float speed = isRunning ? currentRunSpeed : walkSpeed;

        bool isLocked = cam.IsLocked();

        // CANCEL TURN NGAY KHI PLAYER DI CHUYỂN
        if (isTurning && isMoving)
        {
            isTurning = false;
            if (turnCoroutine != null)
                StopCoroutine(turnCoroutine);
        }

        // TURN 180
        if (!isLocked && !isTurning && (combat == null || !combat.IsAttacking()) && !isRolling && !isImpact && Time.time - lastTurnTime > turnCooldown)
        {
            if (Input.GetKeyDown(KeyCode.S) || (v < -0.7f && inputMagnitude > 0.5f))
            {
                StartTurn180();
                return;
            }
        }

        // MOVEMENT
        if (!anim.applyRootMotion && isMoving && !isRolling && !isImpact)
        {
            inputDir.Normalize();

            if (isLocked)
                HandleLockMovement(inputDir, speed);
            else
                HandleFreeMovement(inputDir, speed);
        }

        if (!anim.applyRootMotion) HandleGravity();

        UpdateAnimator(inputDir, isRunning);

        anim.SetBool("IsRunning", isRunning);

        state = anim.GetCurrentAnimatorStateInfo(0);
        next = anim.GetNextAnimatorStateInfo(0);

        bool isUsingRootMotion =
            (combat != null && combat.IsAttacking()) ||
            isTurning ||
            state.IsTag("Combo") ||
            state.IsTag("Roll") ||
            state.IsTag("Impact") ||
            state.IsTag("Dead") ||
            (anim.IsInTransition(0) &&
                (next.IsTag("Combo") || next.IsTag("Roll") || next.IsTag("Impact") || next.IsTag("Dead")));

        anim.applyRootMotion = isUsingRootMotion;
    }

    void PerformRoll()
    {
        lastRollTime = Time.time;

        // Cancel combo đấm nếu đang ra đòn
        if (combat != null)
        {
            combat.ForceStopAttack();
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(h, 0, v);

        // Nếu đang bấm phím di chuyển thì xoay mặt về hướng đó trước khi lăn
        if (inputDir.sqrMagnitude > 0.01f)
        {
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 rollDir = (camForward * inputDir.z + camRight * inputDir.x).normalized;
            if (rollDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(rollDir);
            }
        }

        anim.SetTrigger("DoRoll");
    }

    public void TriggerRollManual()
    {
        PerformRoll();
    }

    private void UpdateRollCooldownUI()
    {
        if (rollCooldownImage == null && rollCooldownText == null) return;

        float remaining = rollCooldown - (Time.time - lastRollTime);
        if (remaining > 0f)
        {
            if (rollCooldownImage != null)
                rollCooldownImage.fillAmount = remaining / rollCooldown;

            if (rollCooldownText != null)
            {
                if (!rollCooldownText.gameObject.activeSelf) rollCooldownText.gameObject.SetActive(true);
                rollCooldownText.text = remaining.ToString("F1");
            }
        }
        else
        {
            if (rollCooldownImage != null && rollCooldownImage.fillAmount > 0f)
                rollCooldownImage.fillAmount = 0f;

            if (rollCooldownText != null && rollCooldownText.gameObject.activeSelf)
                rollCooldownText.gameObject.SetActive(false);
        }
    }

    // FREE MODE
    void HandleFreeMovement(Vector3 inputDir, float speed)
    {
        Vector3 camForward = cam.transform.forward;
        Vector3 camRight = cam.transform.right;

        camForward.y = 0;
        camRight.y = 0;

        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camForward * inputDir.z + camRight * inputDir.x;

        // Xoay theo camera (TPS chuẩn)
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(camForward),
            rotationSpeed * Time.deltaTime
        );

        controller.Move(moveDir * speed * Time.deltaTime);
    }

    // LOCK MODE
    void HandleLockMovement(Vector3 inputDir, float speed)
    {
        Vector3 move =
            transform.right * inputDir.x +
            transform.forward * inputDir.z;

        controller.Move(move * speed * Time.deltaTime);
    }

    Coroutine turnCoroutine;

    void StartTurn180()
    {
        isTurning = true;
        lastTurnTime = Time.time;

        anim.SetTrigger("Turn180");

        if (turnCoroutine != null)
            StopCoroutine(turnCoroutine);

        turnCoroutine = StartCoroutine(DoTurn());
    }

    System.Collections.IEnumerator DoTurn()
    {
        float duration = 0.35f;
        float time = 0f;

        Quaternion startRot = transform.rotation;
        Quaternion targetRot = startRot * Quaternion.Euler(0, 180, 0);

        cam.AddYaw(180f);

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        isTurning = false;
    }

    // ANIMATOR
    void UpdateAnimator(Vector3 inputDir, bool isRunning)
    {
        Vector3 camForward = cam.transform.forward;
        Vector3 camRight = cam.transform.right;

        camForward.y = 0;
        camRight.y = 0;

        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camForward * inputDir.z + camRight * inputDir.x;
        Vector3 localMove = transform.InverseTransformDirection(moveDir);

        if (!cam.IsLocked() && !isTurning)
        {
            if (inputDir.z < 0)
                localMove.z = 0;
        }

        anim.SetFloat("MoveX", localMove.x, 0.1f, Time.deltaTime);
        anim.SetFloat("MoveY", localMove.z, 0.1f, Time.deltaTime);
    }

    // GRAVITY
    void HandleGravity()
    {
        if (controller == null || !controller.enabled) return;

        if (controller.isGrounded)
            verticalVelocity = -2f;
        else
            verticalVelocity -= 9.81f * Time.deltaTime;

        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    void OnAnimatorMove()
    {
        if (!anim.applyRootMotion) return;

        Vector3 delta = anim.deltaPosition;
        delta.y += verticalVelocity * Time.deltaTime;

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        bool rolling = state.IsTag("Roll");

        if (rolling)
        {
            HandleRollCollision();
            if (controller != null && controller.enabled) controller.Move(delta);
            wasRollingLastFrame = true;
        }
        else
        {
            if (wasRollingLastFrame)
            {
                RestoreRollCollision();
            }
            if (controller != null && controller.enabled) controller.Move(delta);
        }

        transform.rotation *= anim.deltaRotation;
    }

    private void HandleRollCollision()
    {
        int characterLayer = LayerMask.NameToLayer("Character");
        if (characterLayer != -1)
        {
            Collider[] nearby = Physics.OverlapSphere(transform.position, 3f, 1 << characterLayer);
            foreach (var col in nearby)
            {
                if (col != controller && col.gameObject != gameObject)
                {
                    Physics.IgnoreCollision(controller, col, true);
                    if (!ignoredColliders.Contains(col))
                        ignoredColliders.Add(col);
                }
            }
        }
    }

    private void RestoreRollCollision()
    {
        foreach (var col in ignoredColliders)
        {
            if (col != null)
                Physics.IgnoreCollision(controller, col, false);
        }
        ignoredColliders.Clear();
        wasRollingLastFrame = false;
    }

    public void SetDead(bool dead)
    {
        isDead = dead;
        if (!dead)
        {
            RestoreRollCollision();
        }
    }

    public void SetDeadState(bool dead)
    {
        SetDead(dead);
    }
}
