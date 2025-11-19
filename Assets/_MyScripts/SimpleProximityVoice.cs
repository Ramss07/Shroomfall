using UnityEngine;
using Fusion;

/// <summary>
/// Simple proximity voice chat for multiplayer games.
/// 
/// SETUP:
/// 1. Add this component to your NetworkPlayer prefab
/// 2. Assign an AudioSource (will be created automatically if missing)
/// 3. Configure hearing distances and push-to-talk key
/// 4. Done!
/// 
/// REQUIREMENTS:
/// - Microphone permission must be granted
/// - Audio Listener on local player camera
/// </summary>
public class SimpleProximityVoice : NetworkBehaviour
{
    [Header("Audio Setup")]
    [Tooltip("Audio source for playing received voice (created automatically if null)")]
    [SerializeField] AudioSource audioSource;
    
    [Header("Proximity Settings")]
    [Tooltip("Maximum distance to hear other players")]
    [SerializeField] float maxDistance = 20f;
    
    [Tooltip("Distance at which volume starts to fall off")]
    [SerializeField] float minDistance = 1f;
    
    [Header("Input Settings")]
    [Tooltip("Key to hold for push-to-talk (V by default)")]
    [SerializeField] KeyCode talkKey = KeyCode.V;
    
    [Tooltip("If true, voice is always transmitted (no push-to-talk)")]
    [SerializeField] bool alwaysOn = true;
    
    [Tooltip("Minimum volume to start transmitting (noise gate)")]
    [SerializeField, Range(0f, 0.1f)] float noiseGate = 0.01f;
    
    [Header("Visual Feedback (Optional)")]
    [Tooltip("GameObject to show when player is speaking")]
    [SerializeField] GameObject speakingIndicator;
    
    // Network state
    [Networked] public NetworkBool IsSpeaking { get; set; }
    
    // Microphone
    private AudioClip micClip;
    private string micDevice;
    private int lastSample = 0;
    private const int SAMPLE_RATE = 16000;
    private const int CHUNK_SIZE = 1024;
    
    // Playback
    private AudioClip playbackClip;

    void Awake()
    {
        SetupAudioSource();
    }

    void SetupAudioSource()
    {
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();
        
        // Configure for 3D voice
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.dopplerLevel = 0f;
        audioSource.loop = false;
    }

    public override void Spawned()
    {
        if (speakingIndicator)
            speakingIndicator.SetActive(false);
        
        // Only local player records audio
        if (Object.HasInputAuthority)
        {
            StartMicrophone();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        StopMicrophone();
    }

    void StartMicrophone()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[Voice] No microphone found!");
            return;
        }
        
        micDevice = Microphone.devices[0];
        micClip = Microphone.Start(micDevice, true, 1, SAMPLE_RATE);
        
        // Wait for mic to initialize
        int timeout = 0;
        while (Microphone.GetPosition(micDevice) <= 0 && timeout < 100)
        {
            timeout++;
        }
        
        Debug.Log($"[Voice] Using microphone: {micDevice}");
    }

    void StopMicrophone()
    {
        if (!string.IsNullOrEmpty(micDevice))
        {
            Microphone.End(micDevice);
        }
    }

    void Update()
    {
        if (Object.HasInputAuthority)
        {
            HandleLocalVoice();
        }
        
        UpdateIndicator();
    }

    void HandleLocalVoice()
    {
        if (micClip == null) return;
        
        // Check if should transmit
        bool wantsToTalk = alwaysOn || Input.GetKey(talkKey);
        
        if (wantsToTalk)
        {
            int currentPos = Microphone.GetPosition(micDevice);
            if (currentPos < 0) return;
            
            // Calculate samples to read
            int samplesToRead = currentPos - lastSample;
            if (samplesToRead < 0)
                samplesToRead += micClip.samples;
            
            if (samplesToRead >= CHUNK_SIZE)
            {
                float[] samples = new float[CHUNK_SIZE];
                micClip.GetData(samples, lastSample);
                
                // Check volume (noise gate)
                float volume = CalculateVolume(samples);
                
                if (volume > noiseGate)
                {
                    TransmitAudio(samples);
                    IsSpeaking = true;
                }
                else
                {
                    IsSpeaking = false;
                }
                
                lastSample = (lastSample + CHUNK_SIZE) % micClip.samples;
            }
        }
        else
        {
            IsSpeaking = false;
        }
    }

    float CalculateVolume(float[] samples)
    {
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += Mathf.Abs(samples[i]);
        }
        return sum / samples.Length;
    }

    void TransmitAudio(float[] samples)
    {
        // Compress to bytes (simple int16 encoding)
        byte[] compressed = new byte[samples.Length * 2];
        
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = (short)(samples[i] * 32767f);
            compressed[i * 2] = (byte)(sample & 0xFF);
            compressed[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        
        // Send via RPC
        RPC_ReceiveVoice(compressed);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    void RPC_ReceiveVoice(byte[] data, RpcInfo info = default)
    {
        // Don't play own voice
        if (info.Source == Runner.LocalPlayer)
            return;
        
        // Check distance
        if (NetworkPlayer.Local == null)
            return;
        
        float distance = Vector3.Distance(transform.position, NetworkPlayer.Local.transform.position);
        if (distance > maxDistance)
            return;
        
        // Decompress
        float[] samples = new float[data.Length / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = (short)(data[i * 2] | (data[i * 2 + 1] << 8));
            samples[i] = sample / 32767f;
        }
        
        // Play
        PlayVoice(samples, distance);
    }

    void PlayVoice(float[] samples, float distance)
    {
        // Create clip
        if (playbackClip == null || playbackClip.samples != samples.Length)
        {
            playbackClip = AudioClip.Create("Voice", samples.Length, 1, SAMPLE_RATE, false);
        }
        
        playbackClip.SetData(samples, 0);
        
        // Calculate volume based on distance
        float volume = 1f - Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
        
        audioSource.volume = volume;
        audioSource.PlayOneShot(playbackClip);
    }

    void UpdateIndicator()
    {
        if (speakingIndicator)
        {
            speakingIndicator.SetActive(IsSpeaking);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Show hearing ranges
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, minDistance);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxDistance);
        
        if (IsSpeaking)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}