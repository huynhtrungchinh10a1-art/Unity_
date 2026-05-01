using UnityEngine;

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

    private CharacterController controller;
    private float verticalVelocity;

    private bool isTurning = false;
    private float lastTurnTime = -10f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        combat = GetComponent<PlayerCombat>();

        if (anim == null)
            anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float speed = isRunning ? runSpeed : walkSpeed;

        Vector3 inputDir = new Vector3(h, 0, v);

        // CHẶN đi lùi trong free mode
        if (!cam.IsLocked() && inputDir.z < 0)
        {
            inputDir.z = 0;
        }

        // tránh chạy chéo vượt tốc độ
        float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);

        // tham vọng tay cầm
        bool isMoving = inputMagnitude > 0.1f;

        anim.SetBool("IsMoving", isMoving);

        bool isLocked = cam.IsLocked();

        // CANCEL TURN NGAY KHI PLAYER DI CHUYỂN
        if (isTurning && isMoving)
        {
            isTurning = false;

            if (turnCoroutine != null)
                StopCoroutine(turnCoroutine);
        }
        // TURN 180
        if (!isLocked && !isTurning && !combat.IsAttacking() && Time.time - lastTurnTime > turnCooldown)
        {
            if (Input.GetKeyDown(KeyCode.S) || (v < -0.7f && inputMagnitude > 0.5f))
            {
                StartTurn180();
                return;
            }
        }

        // MOVEMENT
        if (!combat.IsAttacking() && !isTurning && isMoving)
        {
            inputDir.Normalize();

            if (isLocked)
                HandleLockMovement(inputDir, speed);
            else
                HandleFreeMovement(inputDir, speed);
        }

        HandleGravity();
        UpdateAnimator(inputDir, isRunning);

        anim.SetBool("IsRunning", isRunning);
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
        if (controller.isGrounded)
            verticalVelocity = -2f;
        else
            verticalVelocity -= 9.81f * Time.deltaTime;

        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    void OnAnimatorMove()
    {
        Vector3 delta = anim.deltaPosition;
        delta.y = 0f;
        controller.Move(delta);
    }
}