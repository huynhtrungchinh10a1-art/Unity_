using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleTester : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Kéo cục CameraPivot (chứa script ThirdPersonCamera) vào đây")]
    public ThirdPersonCamera cam;
    public Animator anim;

    private CharacterController controller;

    [Header("Movement Speeds")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 5.0f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    [Header("Combat Test")]
    [Tooltip("Đổi số này để test các đòn chém khác nhau (0 = Slash 1, 1 = Slash 2...)")]
    public int defaultAttackIndex = 0;

    void Start()
    {
        if (anim == null) anim = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();

        // FIX LỖI SETUP 1: Ép tắt Root Motion ngay khi bắt đầu game để chắc chắn có thể đi bộ
        anim.applyRootMotion = false;
    }

    void Update()
    {
        // FIX LỖI CODE 2: Đưa lệnh check chuột trái LÊN TRÊN CÙNG.
        // Để dù nhân vật có đang lướt chém, bác vẫn có thể bấm phím để nối Combo tiếp.
        if (Input.GetMouseButtonDown(0))
        {
            anim.SetInteger("AttackIndex", defaultAttackIndex);
            anim.SetTrigger("DoAttack");
            Debug.Log("Đã bấm chém đòn số: " + defaultAttackIndex);
        }

        // CHẶN DI CHUYỂN: Nằm ở vị trí này mới chuẩn! 
        // Bấm chuột xong rồi thì mới khóa WASD.
        if (anim.applyRootMotion) return;

        // ---------------- 1. TEST DI CHUYỂN THEO CAMERA ----------------
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = new Vector3(h, 0, v).normalized;
        float inputMagnitude = inputDir.magnitude;

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float targetSpeed = 0f;

        if (inputMagnitude > 0.1f)
        {
            targetSpeed = isRunning ? runSpeed : walkSpeed;
        }

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
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            moveDir = transform.right * inputDir.x + transform.forward * inputDir.z;
        }

        // Apply di chuyển bằng phím (Chỉ chạy khi không chém)
        controller.SimpleMove(moveDir * targetSpeed);
        anim.SetFloat("Speed", targetSpeed, 0.1f, Time.deltaTime);
    }

    void OnAnimatorMove()
    {
        // Chỉ áp dụng Root Motion khi Animator cho phép (Script RootMotionToggle ở bài trước bật lên)
        if (anim.applyRootMotion)
        {
            Vector3 delta = anim.deltaPosition;
            // delta.y = 0f; // Bỏ comment dòng này ra nếu bác không muốn nhân vật bay nhảy lên trời
            controller.Move(delta);
            transform.rotation *= anim.deltaRotation;
        }
    }
}