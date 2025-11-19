using UnityEngine;
using Fusion;

/// <summary>
/// Network-optimized proximity voice chat using Fusion's tick system.
/// Uses a single reusable AudioClip to prevent memory leaks.
/// </summary>
public class SimpleProximityVoice : NetworkBehaviour
{
    [Header("Audio Setup")]
    [SerializeField] AudioSource audioSource;
    
    [Header("Proximity Settings")]
    [SerializeField] float maxDistance = 20f;
    [SerializeField] float minDistance = 1f;
    
    [Header("Input Settings")]
    [SerializeField] KeyCode talkKey = KeyCode.V;
    [SerializeField] bool alwaysOn = true;
    [SerializeField, Range(0f, 0.1f)] float noiseGate = 0.01f;
    
    [Header("Visual Feedback")]
    [SerializeField] GameObject speakingIndicator;
    
    [Networked] public NetworkBool IsSpeaking { get; set; }
    
    // Microphone
    private AudioClip micClip;
    private string micDevice;
    private int lastSample = 0;
    private const int SAMPLE_RATE = 16000;
    private const int CHUNK_SIZE = 800;
    
    // Single reusable playback clip (prevents memory leak)
    private AudioClip playbackClip;
    private System.Collections.Generic.Queue<float[]> audioQueue = new System.Collections.Generic.Queue<float[]>();
    private const int MAX_QUEUE_SIZE = 10; // Prevent queue from growing too large

    void Awake()
    {
        SetupAudioSource();
    }

    void SetupAudioSource()
    {
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();
        
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
        
        // Create single reusable clip for playback
        playbackClip = AudioClip.Create("VoicePlayback", CHUNK_SIZE * 2, 1, SAMPLE_RATE, false);
        
        if (Object.HasInputAuthority)
        {
            StartMicrophone();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        StopMicrophone();
        audioQueue.Clear();
        
        if (playbackClip != null)
        {
            Destroy(playbackClip);
            playbackClip = null;
        }
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
        
        int timeout = 0;
        while (Microphone.GetPosition(micDevice) <= 0 && timeout < 100)
            timeout++;
        
        Debug.Log($"[Voice] Using microphone: {micDevice}");
    }

    void StopMicrophone()
    {
        if (!string.IsNullOrEmpty(micDevice))
        {
            Microphone.End(micDevice);
            micDevice = null;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Handle voice transmission on fixed network tick
        if (Object.HasInputAuthority)
        {
            HandleLocalVoice();
        }
    }

    public override void Render()
    {
        // Handle playback in Render for smooth audio (runs every visual frame)
        if (!Object.HasInputAuthority && audioQueue.Count > 0)
        {
            ProcessAudioQueue();
        }
        
        UpdateIndicator();
    }

    void HandleLocalVoice()
    {
        if (micClip == null) return;
        
        bool wantsToTalk = alwaysOn || Input.GetKey(talkKey);
        
        if (wantsToTalk)
        {
            int currentPos = Microphone.GetPosition(micDevice);
            if (currentPos < 0) return;
            
            int samplesToRead = currentPos - lastSample;
            if (samplesToRead < 0)
                samplesToRead += micClip.samples;
            
            if (samplesToRead >= CHUNK_SIZE)
            {
                float[] samples = new float[CHUNK_SIZE];
                micClip.GetData(samples, lastSample);
                
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
            sum += Mathf.Abs(samples[i]);
        return sum / samples.Length;
    }

    void TransmitAudio(float[] samples)
    {
        byte[] compressed = new byte[samples.Length * 2];
        
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = (short)(samples[i] * 32767f);
            compressed[i * 2] = (byte)(sample & 0xFF);
            compressed[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        
        // Split into RPC-safe chunks
        const int maxBytesPerRpc = 450;
        int numRpcs = (compressed.Length + maxBytesPerRpc - 1) / maxBytesPerRpc;
        
        for (int i = 0; i < numRpcs; i++)
        {
            int offset = i * maxBytesPerRpc;
            int size = Mathf.Min(maxBytesPerRpc, compressed.Length - offset);
            
            byte[] chunk = new byte[size];
            System.Array.Copy(compressed, offset, chunk, 0, size);
            
            RPC_ReceiveVoice(chunk);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    void RPC_ReceiveVoice(byte[] data, RpcInfo info = default)
    {
        if (info.Source == Runner.LocalPlayer)
            return;
        
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
        
        // Queue audio (limit queue size to prevent buildup)
        if (audioQueue.Count < MAX_QUEUE_SIZE)
        {
            audioQueue.Enqueue(samples);
        }
    }

    void ProcessAudioQueue()
    {
        // Only play if not currently playing
        if (audioSource.isPlaying || audioQueue.Count == 0)
            return;
        
        float[] samples = audioQueue.Dequeue();
        
        if (NetworkPlayer.Local != null)
        {
            float distance = Vector3.Distance(transform.position, NetworkPlayer.Local.transform.position);
            PlayVoice(samples, distance);
        }
    }

    void PlayVoice(float[] samples, float distance)
    {
        if (playbackClip == null) return;
        
        // Pad samples to match clip size
        float[] paddedSamples = new float[playbackClip.samples];
        int copyLength = Mathf.Min(samples.Length, paddedSamples.Length);
        System.Array.Copy(samples, 0, paddedSamples, 0, copyLength);
        
        playbackClip.SetData(paddedSamples, 0);
        
        // Calculate volume based on distance
        float volume = 1f - Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
        
        audioSource.volume = volume;
        audioSource.PlayOneShot(playbackClip);
    }

    void UpdateIndicator()
    {
        if (speakingIndicator)
            speakingIndicator.SetActive(IsSpeaking);
    }

    void OnDrawGizmosSelected()
    {
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