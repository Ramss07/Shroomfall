using UnityEngine;
using Fusion;

public class SoundProfile : NetworkBehaviour
{
    public enum SoundEvent
    {
        Grab,
        Release,
        Impact
    }

    [System.Serializable]
    public class SoundSettings
    {
        public AudioClip clip;
        [Range(0f, 2f)] public float volume = 1f;
        [Range(0f, 0.5f)] public float pitchRandomness = 0.1f;
    }

    [Header("Grab / Release Sounds")]
    [SerializeField] SoundSettings grab;
    [SerializeField] SoundSettings release;

    [Header("Impact Sounds")]
    [SerializeField] SoundSettings impact;

    public void PlayLocal(SoundEvent soundEvent, Vector3 worldPos, float intensity = 1f)
    {
        var settings = GetSettings(soundEvent);
        PlayOneShot3D(settings, worldPos, intensity);
    }

    public void PlayNetworked(SoundEvent soundEvent, Vector3 worldPos, float intensity = 1f)
    {
        if (!Object.HasStateAuthority)
            return;

        RPC_PlaySound(soundEvent, worldPos, intensity);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_PlaySound(SoundEvent soundEvent, Vector3 worldPos, float intensity)
    {
        var settings = GetSettings(soundEvent);
        PlayOneShot3D(settings, worldPos, intensity);
    }

    SoundSettings GetSettings(SoundEvent e)
    {
        switch (e)
        {
            case SoundEvent.Grab:       return grab;
            case SoundEvent.Release:    return release;
            case SoundEvent.Impact:     return impact;
            default:                    return null;
        }
    }

    void PlayOneShot3D(SoundSettings settings, Vector3 worldPos, float intensity)
    {
        // Validate
        if (settings == null || settings.clip == null)
            return;
        
        // Calculate final volume
        float finalVolume = settings.volume * Mathf.Clamp01(intensity);
        if (finalVolume <= 0f)
            return;

        // Create temporary GameObject
        GameObject go = new GameObject("OneShotAudio");
        go.transform.position = worldPos;

        // Add AudioSource and configure
        var source = go.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.clip = settings.clip;
        source.volume = finalVolume;

        // Randomize pitch slightly
        float r = settings.pitchRandomness;
        float minPitch = 1f - r;
        float maxPitch = 1f + r;
        source.pitch = Random.Range(minPitch, maxPitch);

        source.Play();

        float life = settings.clip.length / Mathf.Max(0.01f, source.pitch);
        Destroy(go, life);
    }
}