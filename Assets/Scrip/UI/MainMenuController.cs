using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Configuration")]
    public string gameplaySceneName = "SampleScene";

    [Header("Panels")]
    public GameObject mainPanel;

    public GameObject settingsPanel;

    [Header("BGM / Nhạc Nền Menu")]
    public AudioSource bgmAudioSource;
    public AudioClip menuBGMClip;
    [Range(0f, 1f)]
    public float bgmVolume = 0.5f;

    public Slider volumeSlider;
    public Toggle fullscreenToggle;

    private void Start()
    {
        // Đảm bảo mở Menu chính và ẩn Settings khi mới vào
        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Khôi phục mức âm lượng đã lưu
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume = savedVolume;
        if (volumeSlider != null)
        {
            volumeSlider.value = savedVolume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        // Khôi phục trạng thái toàn màn hình
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreen;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }

        // Bật nhạc nền Menu
        SetupMenuBGM();
    }

    private void SetupMenuBGM()
    {
        if (bgmAudioSource == null)
        {
            bgmAudioSource = GetComponent<AudioSource>();
            if (bgmAudioSource == null)
            {
                bgmAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        bgmAudioSource.playOnAwake = false;
        bgmAudioSource.loop = true;
        bgmAudioSource.spatialBlend = 0f; // 2D Stereo bao trùm

        if (menuBGMClip != null && bgmAudioSource.clip == null)
        {
            bgmAudioSource.clip = menuBGMClip;
        }

        if (bgmAudioSource.clip != null)
        {
            bgmAudioSource.volume = bgmVolume;
            if (!bgmAudioSource.isPlaying)
            {
                bgmAudioSource.Play();
            }
        }
    }



    public void PlayGame()
    {
        if (bgmAudioSource != null && bgmAudioSource.isPlaying)
        {
            bgmAudioSource.Stop();
        }

        if (!string.IsNullOrEmpty(gameplaySceneName))
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
        else
        {
            Debug.LogWarning("[MainMenu] gameplaySceneName đang để trống!");
        }
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("MasterVolume", volume);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    public void QuitGame()
    {
        Debug.Log("[MainMenu] Thoát Game!");
        Application.Quit();

        // Nếu đang test trong Unity Editor thì dừng Play Mode
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
