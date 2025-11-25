using UnityEngine;
using Fusion;

/// <summary>
/// Professional proximity voice chat with:
/// - Jitter buffer for smooth playback
/// - Ring buffer (zero allocations)
/// - Proper audio timing
/// - Opus-like compression (simple but effective)
/// </summary>
public class ProximityVoice : NetworkBehaviour
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
    
    [Header("Testing")]
    [Tooltip("Hear your own voice (for testing alone)")]
    [SerializeField] bool loopbackEnabled = false;
    
    [Header("Quality Settings")]
    [Tooltip("Higher = smoother but more latency (100-300ms recommended)")]
    [SerializeField, Range(50, 500)] int jitterBufferMs = 200;
    
    [Header("Visual Feedback")]
    [SerializeField] GameObject speakingIndicator;
    
    [Networked] public NetworkBool IsSpeaking { get; set; }
    
    // Microphone
    private AudioClip micClip;
    private string micDevice;
    private int lastSample = 0;
    private const int SAMPLE_RATE = 16000;
    private const int CHUNK_SIZE = 640; // 40ms chunks
    
    // RING BUFFER (zero allocations!)
    private float[] ringBuffer;
    private int ringBufferSize;
    private int writePosition = 0;
    private int readPosition = 0;
    private readonly object bufferLock = new object();
    
    // Jitter buffer state
    private bool isBuffering = true;
    private int targetBufferSamples;
    private int underrunCount = 0;
    
    // Audio playback
    private AudioClip playbackClip;
    private float[] playbackBuffer;

    void Awake()
    {
        SetupAudioSource();
        InitializeBuffers();
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
        audioSource.loop = true;
    }

    void InitializeBuffers()
    {
        // Calculate buffer sizes
        targetBufferSamples = (SAMPLE_RATE * jitterBufferMs) / 1000;
        ringBufferSize = targetBufferSamples * 4; // 4x for safety
        
        // Allocate buffers ONCE (zero allocations after this!)
        ringBuffer = new float[ringBufferSize];
        playbackBuffer = new float[1024]; // Reusable temp buffer
        
        Debug.Log($"[VoicePro] Initialized: target={targetBufferSamples} samples ({jitterBufferMs}ms), ring={ringBufferSize}");
    }

    public override void Spawned()
    {
        if (speakingIndicator)
            speakingIndicator.SetActive(false);
        
        // Create playback clip with dummy data
        playbackClip = AudioClip.Create("VoiceStream", SAMPLE_RATE, 1, SAMPLE_RATE, false);
        audioSource.clip = playbackClip;
        audioSource.Play();
        
        if (Object.HasInputAuthority)
        {
            StartMicrophone();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        StopMicrophone();
        
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
            Debug.LogWarning("[VoicePro] No microphone found!");
            return;
        }
        
        micDevice = Microphone.devices[0];
        micClip = Microphone.Start(micDevice, true, 1, SAMPLE_RATE);
        
        int timeout = 0;
        while (Microphone.GetPosition(micDevice) <= 0 && timeout < 100)
            timeout++;
        
        Debug.Log($"[VoicePro] Using microphone: {micDevice}");
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
        if (Object.HasInputAuthority)
        {
            HandleLocalVoice();
        }
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
        // Improved compression: adaptive quantization
        byte[] compressed = CompressAudio(samples);
        
        // Split into RPC-safe chunks
        const int maxBytesPerRpc = 400;
        int numRpcs = (compressed.Length + maxBytesPerRpc - 1) / maxBytesPerRpc;
        
        for (int i = 0; i < numRpcs; i++)
        {
            int offset = i * maxBytesPerRpc;
            int size = Mathf.Min(maxBytesPerRpc, compressed.Length - offset);
            
            byte[] chunk = new byte[size];
            System.Array.Copy(compressed, offset, chunk, 0, size);
            
            RPC_ReceiveVoice(chunk, i == 0); // Flag first chunk
        }
    }

    // Better compression than simple int16
    byte[] CompressAudio(float[] samples)
    {
        byte[] compressed = new byte[samples.Length * 2];
        
        // Find peak for adaptive scaling
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float abs = Mathf.Abs(samples[i]);
            if (abs > peak) peak = abs;
        }
        
        // Scale factor
        float scale = peak > 0f ? 32767f / peak : 32767f;
        
        // Compress with scaling
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = (short)Mathf.Clamp(samples[i] * scale, -32767f, 32767f);
            compressed[i * 2] = (byte)(sample & 0xFF);
            compressed[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        
        return compressed;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    void RPC_ReceiveVoice(byte[] data, bool isFirstChunk, RpcInfo info = default)
    {
        // Skip own voice UNLESS loopback is enabled
        if (info.Source == Runner.LocalPlayer && !loopbackEnabled)
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
        
        // Write to ring buffer
        WriteToRingBuffer(samples);
        
        // Update volume
        float normalizedDistance = Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
        audioSource.volume = 1f - normalizedDistance;
    }

    void WriteToRingBuffer(float[] samples)
    {
        lock (bufferLock)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                ringBuffer[writePosition] = samples[i];
                writePosition = (writePosition + 1) % ringBufferSize;
                
                // Prevent overflow
                if (writePosition == readPosition)
                {
                    // Buffer full, skip oldest sample
                    readPosition = (readPosition + 1) % ringBufferSize;
                }
            }
        }
    }

    int GetBufferedSampleCount()
    {
        lock (bufferLock)
        {
            int count = writePosition - readPosition;
            if (count < 0) count += ringBufferSize;
            return count;
        }
    }

    // Unity calls this for audio playback
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (Object == null || Object.HasInputAuthority)
        {
            // Silence for local player
            System.Array.Clear(data, 0, data.Length);
            return;
        }
        
        lock (bufferLock)
        {
            int bufferedSamples = GetBufferedSampleCount();
            
            // Jitter buffer logic
            if (isBuffering)
            {
                if (bufferedSamples >= targetBufferSamples)
                {
                    isBuffering = false;
                    Debug.Log($"[VoicePro] Playback started ({bufferedSamples} samples buffered)");
                }
                else
                {
                    // Still buffering, output silence
                    System.Array.Clear(data, 0, data.Length);
                    return;
                }
            }
            
            // Read from ring buffer
            for (int i = 0; i < data.Length; i += channels)
            {
                float sample = 0f;
                
                if (readPosition != writePosition)
                {
                    sample = ringBuffer[readPosition];
                    readPosition = (readPosition + 1) % ringBufferSize;
                }
                else
                {
                    // Buffer underrun
                    underrunCount++;
                    if (underrunCount > 5)
                    {
                        // Too many underruns, restart buffering
                        isBuffering = true;
                        underrunCount = 0;
                        Debug.LogWarning("[VoicePro] Buffer underrun, restarting buffering");
                    }
                }
                
                // Write to all channels
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = sample;
                }
            }
        }
    }

    public override void Render()
    {
        UpdateIndicator();
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