using UnityEngine;

public class WindController : MonoBehaviour
{
    [Header("Volume & Audio Boost (Khuếch đại x1.5 chống rè)")]
    [Range(0f, 1f)] public float windVolume = 0.8f;
    [Range(1f, 4f)] public float windBoost = 1.5f;
    public float fadeInSpeed = 1.5f;

    [Header("Audio Clip")]
    public AudioClip windClip;

    [Header("Pitch Variation (Mô phỏng gió rít tự nhiên)")]
    public float minPitch = 0.8f;
    public float maxPitch = 1.2f;
    public float changeSpeed = 0.5f;

    private AudioSource windAudio;
    private WindAudioBooster booster;
    private bool hasStartedPlaying = false;

    void Awake()
    {
        // 1. Tắt AudioSource cũ trên Camera (nếu có) để tránh xung đột với AudioListener
        AudioSource cameraAudio = GetComponent<AudioSource>();
        if (cameraAudio != null)
        {
            if (windClip == null && cameraAudio.clip != null)
                windClip = cameraAudio.clip;

            cameraAudio.playOnAwake = false;
            cameraAudio.Stop();
            cameraAudio.enabled = false;
        }

        // 2. Tạo GameObject con riêng biệt [Audio_Wind] để DSP Booster không đè lên AudioListener
        Transform child = transform.Find("[Audio_Wind]");
        if (child == null)
        {
            GameObject go = new GameObject("[Audio_Wind]");
            go.transform.SetParent(transform, false);
            child = go.transform;
        }

        windAudio = child.GetComponent<AudioSource>();
        if (windAudio == null) windAudio = child.gameObject.AddComponent<AudioSource>();

        booster = child.GetComponent<WindAudioBooster>();
        if (booster == null) booster = child.gameObject.AddComponent<WindAudioBooster>();

        windAudio.playOnAwake = false;
        windAudio.loop = true;
        windAudio.spatialBlend = 0f; // 2D Stereo âm thanh vòm
        windAudio.volume = 0f;
        windAudio.Stop();

        if (windClip != null)
        {
            windAudio.clip = windClip;
            windClip.LoadAudioData(); // Ép nạp vào RAM ngay lập tức, không đợi streaming
        }
    }

    void Update()
    {
        if (windAudio == null) return;

        // Đồng bộ hệ số khuếch đại Boost (mặc định 1.5x)
        if (booster != null) booster.boostMultiplier = windBoost;

        // KIỂM TRA ĐIỀU KIỆN: Chưa chọn phe -> TUYỆT ĐỐI KHÔNG ĐƯỢC CÓ TIẾNG
        if (AudioListener.pause || IsWaitingForTeamSelection())
        {
            if (windAudio.isPlaying)
            {
                windAudio.Stop();
            }
            windAudio.volume = 0f;
            hasStartedPlaying = false;
            return;
        }

        // ĐÃ CHỌN PHE XONG: Bắt đầu phát tiếng gió môi trường (gọi Play đúng 1 lần, không gọi lặp)
        if (!hasStartedPlaying)
        {
            if (windAudio.clip != null)
            {
                windAudio.Play();
                hasStartedPlaying = true;
            }
        }

        // Tăng âm lượng mượt mà (Fade In) lên mức windVolume
        windAudio.volume = Mathf.MoveTowards(windAudio.volume, windVolume, fadeInSpeed * Time.deltaTime);

        // Hiệu ứng gió rít tự nhiên thay đổi pitch theo thời gian
        windAudio.pitch = Mathf.Lerp(minPitch, maxPitch, Mathf.PerlinNoise(Time.time * changeSpeed, 0.0f));
    }

    private bool IsWaitingForTeamSelection()
    {
        if (LoadingTutorialScreen.Instance != null && LoadingTutorialScreen.Instance.gameObject.activeInHierarchy)
        {
            if (LoadingTutorialScreen.Instance.screenPanel != null && LoadingTutorialScreen.Instance.screenPanel.activeInHierarchy)
                return true;

            if (LoadingTutorialScreen.Instance.teamSelectPopup != null && LoadingTutorialScreen.Instance.teamSelectPopup.activeInHierarchy)
                return true;
        }

        return false;
    }
}

public class WindAudioBooster : MonoBehaviour
{
    [Range(1f, 5f)] public float boostMultiplier = 1.5f;

    void OnAudioFilterRead(float[] data, int channels)
    {
        float boost = boostMultiplier;
        if (boost <= 1.001f) return;

        for (int i = 0; i < data.Length; i++)
        {
            float s = data[i] * boost;
            // Thuật toán Soft-Knee Limiter (Chống rè, chống vỡ tiếng loa khi khuếch đại)
            if (s > 0.95f)
            {
                s = 0.95f + (s - 0.95f) / (1f + (s - 0.95f) * 2f);
                if (s > 0.999f) s = 0.999f;
            }
            else if (s < -0.95f)
            {
                float neg = -s;
                neg = 0.95f + (neg - 0.95f) / (1f + (neg - 0.95f) * 2f);
                if (neg > 0.999f) neg = 0.999f;
                s = -neg;
            }
            data[i] = s;
        }
    }
}
