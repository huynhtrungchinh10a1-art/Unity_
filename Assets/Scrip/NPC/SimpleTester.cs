using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleTester : MonoBehaviour
{
    [Header("References")]
    public ThirdPersonCamera cam;
    public Animator anim;

    private CharacterController controller;

    [Header("Movement Speeds")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 5.0f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    [Header("Block Settings")] // Thêm settings cho block
    public bool isBlocking = false; // Có thể set từ Inspector hoặc code

    private float verticalVelocity;

    void Start()
    {
        if (anim == null) anim = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();

        anim.applyRootMotion = false;
    }

    void Update()
    {
        // ================= ATTACK TEST (0 → 5) =================
        for (int i = 0; i <= 5; i++)
        {
            if (Input.GetKeyDown(i.ToString()))
            {
                anim.SetInteger("AttackIndex", i);
                anim.SetTrigger("DoAttack");

                Debug.Log("Attack index: " + i);
            }
        }

        // ================= DEAD TEST (U, I, O) =================
        if (Input.GetKeyDown(KeyCode.U))
        {
            anim.SetInteger("DeadIndex", 0); // Sword And Shield Death
            anim.SetTrigger("DoDead");
            Debug.Log("Dead: Sword And Shield Death");
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            anim.SetInteger("DeadIndex", 1); // Flying Back Death
            anim.SetTrigger("DoDead");
            Debug.Log("Dead: Flying Back Death");
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            anim.SetInteger("DeadIndex", 2); // Sword And Shield Death2
            anim.SetTrigger("DoDead");
            Debug.Log("Dead: Sword And Shield Death2");
        }

        // ================= IMPACT TEST Z với IsBlocked check =================
        if (Input.GetKeyDown(KeyCode.Z))
        {
            // QUAN TRỌNG: Set IsBlocked TRƯỚC trigger
            anim.SetBool("IsBlocked", isBlocking);

            // Debug kiểm tra giá trị đã được set chưa
            Debug.Log($"Set IsBlocked = {isBlocking}");

            // Sau đó mới trigger
            anim.SetTrigger("DoBlock");
        }





        // ================= TOGGLE BLOCK MODE (thêm phím B để bật/tắt trạng thái blocking) =================
        if (Input.GetKeyDown(KeyCode.B))
        {
            isBlocking = !isBlocking;
            Debug.Log("Block mode: " + (isBlocking ? "ON" : "OFF"));
        }

        // ================= ROOT MOTION CONTROL =================
        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = anim.GetNextAnimatorStateInfo(0);

        bool isUsingRootMotion =
            state.IsTag("Combo") ||
            state.IsTag("Dead") ||    // Thêm tag Dead
            state.IsTag("Impact") ||  // Thêm tag Impact
            (anim.IsInTransition(0) &&
                (next.IsTag("Combo") || next.IsTag("Dead") || next.IsTag("Impact")));

        anim.applyRootMotion = isUsingRootMotion;

        // ================= CHẶN MOVEMENT KHI ATTACK/DEAD/IMPACT =================
        if (anim.applyRootMotion) return;

        // ================= MOVEMENT =================
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = new Vector3(h, 0, v).normalized;
        float inputMagnitude = inputDir.magnitude;

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float targetSpeed = inputMagnitude > 0.1f ? (isRunning ? runSpeed : walkSpeed) : 0f;

        Vector3 moveDir = Vector3.zero;

        if (cam != null)
        {
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;

            camForward.y = 0;
            camRight.y = 0;

            camForward.Normalize();
            camRight.Normalize();

            moveDir = camForward * inputDir.z + camRight * inputDir.x;

            if (moveDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
        else
        {
            moveDir = transform.right * inputDir.x + transform.forward * inputDir.z;
        }

        // gravity
        if (controller.isGrounded)
            verticalVelocity = -2f;
        else
            verticalVelocity -= 9.81f * Time.deltaTime;

        Vector3 finalMove = moveDir * targetSpeed;
        finalMove.y = verticalVelocity;

        controller.Move(finalMove * Time.deltaTime);

        anim.SetFloat("Speed", targetSpeed, 0.1f, Time.deltaTime);
    }

    void OnAnimatorMove()
    {
        if (!anim.applyRootMotion) return;

        Vector3 delta = anim.deltaPosition;
        controller.Move(delta);
        transform.rotation *= anim.deltaRotation;
    }
}