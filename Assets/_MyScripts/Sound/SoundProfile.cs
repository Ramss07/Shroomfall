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
        [Range(0.1f, 3f)] public float basePitch = 1f;
    }
    [SerializeField] bool restricttToOwnerOnly = false;

    [Header("Grab / Release Sounds")]
    [SerializeField] SoundSettings grab;
    [SerializeField] SoundSettings release;

    [Header("Impact Sounds")]
    [SerializeField] SoundSettings impact;
    [SerializeField] float minImpactVelocity = 10f;
    [SerializeField] float maxImpactVelocity = 20f;
    [SerializeField] float impactCooldown = 0.2f;
    double nextAllowedImpactTime;


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
        source.minDistance = 1f;
        source.maxDistance = 30f;
        source.rolloffMode = AudioRolloffMode.Linear;

        // Randomize around base pitch
        float r = settings.pitchRandomness;
        float baseP = settings.basePitch;

        // Random range around base pitch
        float minPitch = baseP - r;
        float maxPitch = baseP + r;

        // Just in case, clamp to a sane range
        float pitch = Random.Range(minPitch, maxPitch);
        pitch = Mathf.Clamp(pitch, 0.1f, 3f);

        source.pitch = pitch;

        source.Play();

        float life = settings.clip.length / Mathf.Max(0.01f, source.pitch);
        Destroy(go, life);
    }

    void OnCollisionEnter(Collision c)
    {
        if (Runner == null)  return;
        if (!Object.HasStateAuthority)  return;
        if (impact == null || impact.clip == null)  return;

        float speed = c.relativeVelocity.magnitude;
        Debug.LogWarning($"[Impact] Speed = {c.relativeVelocity.magnitude:F2}");

        if (speed < minImpactVelocity)  return;
        if (Runner.SimulationTime < nextAllowedImpactTime)  return;
        nextAllowedImpactTime = Runner.SimulationTime + impactCooldown;

        Vector3 hitPoint = (c.contactCount > 0) ? c.GetContact(0).point : transform.position;

        float intensity = Mathf.InverseLerp(minImpactVelocity, maxImpactVelocity, speed);

        PlayNetworked(SoundEvent.Impact, hitPoint, intensity);
    }
}