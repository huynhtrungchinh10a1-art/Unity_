using UnityEngine;


public class CinematicFlyCam : MonoBehaviour
{
    public static CinematicFlyCam Instance { get; private set; }

    [Header("1. Kích Hoạt (Toggle)")]
    [Tooltip("Phím bật/tắt chế độ Camera quay phim")]
    public KeyCode toggleKey = KeyCode.F1;
    public bool isFlyCamActive = false;

    [Header("2. Tốc Độ Bay (Movement Speed)")]
    public float normalSpeed = 25f;
    public float fastSpeedMultiplier = 3.5f;  // Giữ Shift để bay lướt nhanh qua chiến trường 600m
    public float slowSpeedMultiplier = 0.25f; // Giữ Alt/Ctrl để lướt chậm cận cảnh từng đường kiếm
    public float smoothMoveTime = 0.12f;      // Độ mượt quán tính khi di chuyển

    [Header("4. Xoay Góc Nhìn (Look & Rotation)")]
    public float lookSensitivity = 2.5f;
    public float smoothRotateTime = 0.08f;    // Độ mượt quán tính xoay cam điện ảnh

    [Header("5. Ống Kính & Zoom FOV")]
    public float minFOV = 20f;
    public float maxFOV = 85f;
    public float zoomSensitivity = 5f;

    [Header("6. Ẩn/Hiện Giao Diện Hướng Dẫn")]
    public KeyCode toggleUIKey = KeyCode.H;
    public bool showControlsUI = true;

    // Internal tracking
    private Camera targetCam;
    private ThirdPersonCamera playerCam;
    private PlayerMovement playerMovement;
    private Transform originalCullingTransform;

    private Vector3 currentVelocity;
    private Vector3 targetPosition;
    private float targetYaw;
    private float targetPitch;
    private float currentYaw;
    private float currentPitch;
    private float yawVelocity;
    private float pitchVelocity;
    private float targetFOV = 60f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        targetCam = GetComponent<Camera>();
        if (targetCam == null) targetCam = Camera.main;
    }

    void Start()
    {
        // Luôn đảm bảo tốc độ thời gian chuẩn 1.0x không bao giờ bị slow-motion ngầm
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        playerCam = FindObjectOfType<ThirdPersonCamera>();
        playerMovement = FindObjectOfType<PlayerMovement>();

        if (targetCam != null)
        {
            targetFOV = targetCam.fieldOfView;
            Vector3 euler = targetCam.transform.eulerAngles;
            currentYaw = targetYaw = euler.y;
            currentPitch = targetPitch = euler.x;
            targetPosition = targetCam.transform.position;
        }
    }

    void Update()
    {
        // 1. Phím tắt chuyển đổi giữa Cam Người Chơi và FlyCam Quay Phim
        if (Input.GetKeyDown(toggleKey))
        {
            SetFlyCamActive(!isFlyCamActive);
        }

        // 2. Phím ẩn/hiện bảng hướng dẫn phím
        if (Input.GetKeyDown(toggleUIKey))
        {
            showControlsUI = !showControlsUI;
        }

        if (!isFlyCamActive) return;

        // 4. Xử lý xoay góc nhìn chuột (Chuột phải hoặc khi khóa con trỏ)
        HandleRotation();

        // 5. Xử lý di chuyển bàn phím (WASD + Q/E)
        HandleMovement();

        // 6. Xử lý Zoom ống kính lăn chuột (FOV Zoom)
        HandleZoom();
    }

    public void SetFlyCamActive(bool active)
    {
        isFlyCamActive = active;

        if (targetCam == null) targetCam = Camera.main;
        if (targetCam == null) return;

        if (isFlyCamActive)
        {
            // Tắt camera người chơi để tránh xung đột vị trí
            if (playerCam != null) playerCam.enabled = false;
            if (playerMovement != null) playerMovement.enabled = false;

            // Đồng bộ vị trí & góc nhìn hiện tại của camera
            targetPosition = targetCam.transform.position;
            Vector3 euler = targetCam.transform.eulerAngles;
            currentYaw = targetYaw = euler.y;
            currentPitch = targetPitch = euler.x;
            targetFOV = targetCam.fieldOfView;

            // BẬT CULLING THEO CAMERA: Cam bay tới đâu, lính xung quanh tự thức dậy!
            if (NPCCullingManager.Instance != null)
            {
                originalCullingTransform = NPCCullingManager.Instance.playerTransform;
                NPCCullingManager.Instance.playerTransform = targetCam.transform;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("<color=cyan>[CinematicFlyCam]</color> ĐÃ BẬT FLYCAM QUAY PHIM! (Nhấn F1 để trở lại Player)");
        }
        else
        {
            // Trả lại quyền điều khiển cho Player
            if (playerCam != null) playerCam.enabled = true;
            if (playerMovement != null) playerMovement.enabled = true;

            // Trả lại Culling theo Player
            if (NPCCullingManager.Instance != null && originalCullingTransform != null)
            {
                NPCCullingManager.Instance.playerTransform = originalCullingTransform;
            }

            Time.timeScale = 1.0f; // Trả lại tốc độ game chuẩn
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("<color=yellow>[CinematicFlyCam]</color> ĐÃ TẮT FLYCAM - Trở về góc nhìn Player.");
        }
    }

    private void HandleRotation()
    {
        bool allowRotate = Input.GetMouseButton(1) || Cursor.lockState == CursorLockMode.Locked;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = (Cursor.lockState != CursorLockMode.Locked);
        }

        if (allowRotate)
        {
            float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

            targetYaw += mouseX;
            targetPitch -= mouseY;
            targetPitch = Mathf.Clamp(targetPitch, -89f, 89f);
        }

        currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, smoothRotateTime);
        currentPitch = Mathf.SmoothDampAngle(currentPitch, targetPitch, ref pitchVelocity, smoothRotateTime);

        targetCam.transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    private void HandleMovement()
    {
        float speed = normalSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            speed *= fastSpeedMultiplier;
        }
        else if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.LeftControl))
        {
            speed *= slowSpeedMultiplier;
        }

        Vector3 moveInput = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) moveInput += targetCam.transform.forward;
        if (Input.GetKey(KeyCode.S)) moveInput -= targetCam.transform.forward;
        if (Input.GetKey(KeyCode.D)) moveInput += targetCam.transform.right;
        if (Input.GetKey(KeyCode.A)) moveInput -= targetCam.transform.right;

        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) moveInput += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) moveInput -= Vector3.up;

        if (moveInput.sqrMagnitude > 0.001f)
        {
            moveInput.Normalize();
        }

        targetPosition += moveInput * (speed * Time.unscaledDeltaTime);
        targetCam.transform.position = Vector3.SmoothDamp(targetCam.transform.position, targetPosition, ref currentVelocity, smoothMoveTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetFOV -= scroll * zoomSensitivity * 10f;
            targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
        }

        if (Mathf.Abs(targetCam.fieldOfView - targetFOV) > 0.05f)
        {
            targetCam.fieldOfView = Mathf.Lerp(targetCam.fieldOfView, targetFOV, Time.unscaledDeltaTime * 10f);
        }
    }

    private void HandleSlowMotion()
    {
        // Đã khóa cứng tốc độ 1.0x bình thường - không còn tự động bị Slow-Motion do gõ phím tiếng Việt Unikey
        if (Time.timeScale != 1.0f)
        {
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    void OnGUI()
    {
        if (!showControlsUI) return;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 13;
        boxStyle.alignment = TextAnchor.UpperLeft;
        boxStyle.normal.textColor = Color.white;

        GUILayout.BeginArea(new Rect(15, 15, 380, 270), boxStyle);
        GUILayout.Label("<b>🎬 BẢNG ĐIỀU KHIỂN QUAY PHIM ĐẠI CHIẾN</b>");

        bool isCharging = CinematicBattleDirector.Instance != null && CinematicBattleDirector.Instance.isCinematicActive;
        string chargeStatus = isCharging ? "<color=green>ĐANG XUNG PHONG</color>" : "<color=yellow>ĐANG ĐỨNG YÊN CHỜ</color>";
        GUILayout.Label($"• Trạng thái quân: <b>{chargeStatus}</b>");

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(isCharging ? "⏹ DỪNG LẠI (F2)" : "▶ BẮT ĐẦU XUNG PHONG (F2)", GUILayout.Height(30)))
        {
            if (CinematicBattleDirector.Instance != null)
            {
                if (!isCharging) CinematicBattleDirector.Instance.StartCinematicCharge();
                else CinematicBattleDirector.Instance.StopCinematicCharge();
            }
        }

        if (GUILayout.Button("🔄 RESET VỊ TRÍ GỐC (F3)", GUILayout.Height(30)))
        {
            if (CinematicBattleDirector.Instance != null)
            {
                CinematicBattleDirector.Instance.ResetBattlefield();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("• <b>F1:</b> Bật / Tắt FlyCam (" + (isFlyCamActive ? "<color=cyan>ĐANG BẬT</color>" : "TẮT") + ")");
        GUILayout.Label("• <b>WASD:</b> Bay ngang / tiến lùi | <b>Q/E:</b> Lên/Xuống");
        GUILayout.Label("• <b>Chuột phải:</b> Xoay góc nhìn điện ảnh");
        GUILayout.Label("• <b>Shift:</b> Bay siêu tốc (3.5x) | <b>Alt:</b> Cận cảnh (0.25x)");
        GUILayout.Label("• <b>Lăn chuột:</b> Zoom ống kính FOV (" + (int)targetFOV + "°)");
        GUILayout.Label("• <b>H:</b> Ẩn/Hiện bảng này để quay màn hình sạch");
        GUILayout.EndArea();
    }
}

