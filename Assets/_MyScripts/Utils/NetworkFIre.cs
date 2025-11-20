using UnityEngine;
using Fusion;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class NetworkFire : NetworkBehaviour
{
    [Header("Particles")]
    [SerializeField] ParticleSystem smoke;
    [SerializeField] ParticleSystem fire;
    [SerializeField] ParticleSystem dissolve;

    [Header("General Settings")]
    [SerializeField] bool smokeOnStart = false;
    [SerializeField] bool dissolvable  = true;

    [Tooltip("If true, smoke & fire durations are derived from Rigidbody.mass.")]
    [Header("Auto Duration From Mass")]
    [SerializeField] bool autoConfigureFromMass = true;
    [SerializeField] float defaultMass = 5f;
    [SerializeField] float smokePerKg = 0.5f;         // seconds of smoke per 1 mass unit
    [SerializeField] float firePerKg  = 2f;         // seconds of fire per 1 mass unit
    [SerializeField] Vector2 smokeDurationRange = new Vector2(0.3f, 5f);
    [SerializeField] Vector2 fireDurationRange  = new Vector2(3f, 30f);
    [SerializeField] float maxDissolvableMass   = 40f;

    [Tooltip("Used when autoConfigureFromMass = false.")]
    [SerializeField] float manualSmokeDuration = 3f;
    [Tooltip("Used when autoConfigureFromMass = false.")]
    [SerializeField] float manualFireDuration  = 5f;

    // Actual durations used at runtime (either auto or manual)
    float smokeDuration;
    float fireDuration;

    // Networked state (authoritative on StateAuthority)
    [Networked] public NetworkBool IsSmoking   { get; set; }
    [Networked] public NetworkBool IsBurning   { get; set; }
    [Networked] public NetworkBool IsDissolved { get; set; }

    [Networked] public double SmokeEndTime { get; set; }
    [Networked] public double FireEndTime  { get; set; }

    // Local cached state so we only toggle particles when something changes
    bool _lastSmoking;
    bool _lastBurning;
    bool _lastDissolved;
    bool _hasDissolvePlayed;

    public bool Smoking  => IsSmoking;
    public bool Burning  => IsBurning;

    void Awake()
    {
        AutoConfigureDurations();
    }

#if UNITY_EDITOR
    // Nice to see changes live in editor when tweaking masses/values
    void OnValidate()
    {
        // Avoid running in edit mode while playing
        if (Application.isPlaying) return;
        AutoConfigureDurations();
    }
#endif

    void AutoConfigureDurations()
    {
        float mass = defaultMass;
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
            mass = Mathf.Max(0.01f, rb.mass);

        if (autoConfigureFromMass)
        {
            smokeDuration = Mathf.Clamp(mass * smokePerKg, smokeDurationRange.x, smokeDurationRange.y);
            fireDuration  = Mathf.Clamp(mass * firePerKg,  fireDurationRange.x,  fireDurationRange.y);

            // Light stuff can fully dissolve, heavy stuff just burns / chars
            dissolvable = mass <= maxDissolvableMass;
        }
        else
        {
            smokeDuration = Mathf.Max(0.01f, manualSmokeDuration);
            fireDuration  = Mathf.Max(0.01f, manualFireDuration);
            // 'dissolvable' stays whatever you set in inspector
        }
    }

    public override void Spawned()
    {
        // Register with FireManager (if present in scene)
        if (FireManager.Instance != null)
            FireManager.Instance.Register(this);

        // State authority can initialize burn state
        if (Object.HasStateAuthority)
        {
            if (smokeOnStart && !IsSmoking && !IsBurning && !IsDissolved)
                IgniteSmoke();
        }

        // Ensure particles start in correct visual state on join
        SyncVisualsImmediate();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (FireManager.Instance != null)
            FireManager.Instance.Unregister(this);
    }

    // -------------------- Authority-side logic --------------------

    /// <summary>Called only on StateAuthority to start smoking.</summary>
    public void IgniteSmoke()
    {
        if (!Object.HasStateAuthority) return;
        if (IsBurning || IsDissolved)  return; // already burning/finished

        IsSmoking    = true;
        SmokeEndTime = Runner.SimulationTime + smokeDuration;
    }

    /// <summary>Called only on StateAuthority to start fire directly.</summary>
    public void IgniteFire()
    {
        if (!Object.HasStateAuthority) return;
        if (IsDissolved) return;

        IsSmoking  = false;
        IsBurning  = true;
        FireEndTime = Runner.SimulationTime + fireDuration;
    }

    /// <summary>Stop smoke/fire without dissolving (eg. puzzle logic).</summary>
    public void StopBurning()
    {
        if (!Object.HasStateAuthority) return;

        IsSmoking   = false;
        IsBurning   = false;
        // Don’t change IsDissolved here
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        double now = Runner.SimulationTime;

        // Smoking → Fire after duration
        if (IsSmoking && now >= SmokeEndTime)
        {
            IgniteFire();
        }

        // Fire → Dissolve (optional)
        if (IsBurning && dissolvable && now >= FireEndTime)
        {
            BeginDissolve();
        }
    }

    void BeginDissolve()
    {
        if (!Object.HasStateAuthority) return;
        if (IsDissolved) return;

        IsBurning   = false;
        IsSmoking   = false;
        IsDissolved = true;
    }

    // -------------------- Client visual sync --------------------

    public override void Render()
    {
        // This runs on all peers. Just follow the Networked state.
        UpdateVisualsIfChanged();
    }

    void SyncVisualsImmediate()
    {
        _lastSmoking   = !IsSmoking;   // force initial update
        _lastBurning   = !IsBurning;
        _lastDissolved = !IsDissolved;
        UpdateVisualsIfChanged();
    }

    void UpdateVisualsIfChanged()
    {
        if (IsSmoking != _lastSmoking)
        {
            _lastSmoking = IsSmoking;
            if (smoke != null)
            {
                if (IsSmoking) smoke.Play();
                else           smoke.Stop();
            }
        }

        if (IsBurning != _lastBurning)
        {
            _lastBurning = IsBurning;
            if (fire != null)
            {
                if (IsBurning) fire.Play();
                else           fire.Stop();
            }
        }

        if (IsDissolved != _lastDissolved)
        {
            _lastDissolved = IsDissolved;

            if (IsDissolved)
                HandleDissolvedVisuals();
        }
    }

    void HandleDissolvedVisuals()
    {
        if (_hasDissolvePlayed) return;
        _hasDissolvePlayed = true;

        if (fire != null)    fire.Stop();
        if (smoke != null)   smoke.Stop();
        if (dissolve != null) dissolve.Play();

        // Hide mesh & disable colliders locally
        var mr  = GetComponent<MeshRenderer>();
        var smr = GetComponentInChildren<SkinnedMeshRenderer>();

        if (mr)  mr.enabled  = false;
        if (smr) smr.enabled = false;

        foreach (var c in GetComponents<Collider>())
            c.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = false;
    }
}
