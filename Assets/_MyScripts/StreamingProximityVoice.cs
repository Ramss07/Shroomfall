using UnityEngine;
using Fusion;
using System.Collections.Generic;

/// <summary>
/// Memory-leak-free proximity voice chat using audio streaming.
/// This version uses OnAudioFilterRead to avoid creating/destroying AudioClips.
/// </summary>
public class StreamingProximityVoice : NetworkBehaviour
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
    private const int CHUNK_SIZE = 960;  // 60ms chunks = smoother audio, fits in 2 RPCs
    
    // Streaming playback buffer
    private Queue<float> audioStreamQueue = new Queue<float>();
    private readonly object queueLock = new object();
    private float lastVolume = 0f;
    private bool hasStartedPlayback = false;
    private const int MIN_BUFFER_SAMPLES = 4800; // 300ms buffer before starting playback

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
        audioSource.loop = true;
        
        // Create a dummy clip at the correct sample rate to force AudioSource to use it
        AudioClip dummyClip = AudioClip.Create("Dummy", SAMPLE_RATE, 1, SAMPLE_RATE, false);
        audioSource.clip = dummyClip;
        audioSource.Play(); // Start playing (OnAudioFilterRead will provide the actual data)
    }

    public override void Spawned()
    {
        if (speakingIndicator)
            speakingIndicator.SetActive(false);
        
        if (Object.HasInputAuthority)
        {
            StartMicrophone();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        StopMicrophone();
        lock (queueLock)
        {
            audioStreamQueue.Clear();
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

    void Update()
    {
        if (Object.HasInputAuthority)
        {
            HandleLocalVoice();
        }
        
        UpdateIndicator();
        UpdateVolume();
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
        
        // Split into safe RPC sizes
        const int maxBytesPerRpc = 400;
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
        
        // Decompress and add to stream
        lock (queueLock)
        {
            for (int i = 0; i < data.Length / 2; i++)
            {
                short sample = (short)(data[i * 2] | (data[i * 2 + 1] << 8));
                float floatSample = sample / 32767f;
                audioStreamQueue.Enqueue(floatSample);
            }
        }
        
        // Update target volume
        float normalizedDistance = Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
        lastVolume = 1f - normalizedDistance;
    }

    void UpdateVolume()
    {
        // Smoothly transition volume
        audioSource.volume = Mathf.Lerp(audioSource.volume, lastVolume, Time.deltaTime * 10f);
    }

    // This is called by Unity's audio system - streams audio directly
    void OnAudioFilterRead(float[] data, int channels)
    {
        // Only process for remote players
        if (Object == null || Object.HasInputAuthority)
        {
            // Fill with silence for local player
            for (int i = 0; i < data.Length; i++)
                data[i] = 0f;
            return;
        }
        
        lock (queueLock)
        {
            // Wait until we have enough buffered audio before starting
            if (!hasStartedPlayback)
            {
                if (audioStreamQueue.Count >= MIN_BUFFER_SAMPLES)
                {
                    hasStartedPlayback = true;
                    Debug.Log($"[Voice] Started playback with {audioStreamQueue.Count} samples buffered");
                }
                else
                {
                    // Not enough data yet, output silence
                    for (int i = 0; i < data.Length; i++)
                        data[i] = 0f;
                    return;
                }
            }
            
            // Play buffered audio
            for (int i = 0; i < data.Length; i += channels)
            {
                float sample = 0f;
                
                if (audioStreamQueue.Count > 0)
                {
                    sample = audioStreamQueue.Dequeue();
                }
                else
                {
                    // Buffer underrun - reset playback state
                    hasStartedPlayback = false;
                }
                
                // Write to all channels (mono to stereo/multichannel)
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = sample;
                }
            }
            
            // Prevent queue from growing too large
            if (audioStreamQueue.Count > SAMPLE_RATE * 2)
            {
                Debug.LogWarning($"[Voice] Queue overflow ({audioStreamQueue.Count}), clearing excess");
                while (audioStreamQueue.Count > SAMPLE_RATE)
                {
                    audioStreamQueue.Dequeue();
                }
            }
        }
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