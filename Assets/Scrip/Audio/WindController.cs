using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class WindController : MonoBehaviour
{
    private AudioSource windAudio;
    public float minPitch = 0.8f;
    public float maxPitch = 1.2f;
    public float changeSpeed = 0.5f;

    void Start()
    {
        windAudio = GetComponent<AudioSource>();
    }

    void Update()
    {
        windAudio.pitch = Mathf.Lerp(minPitch, maxPitch, Mathf.PerlinNoise(Time.time * changeSpeed, 0.0f));
    }
}