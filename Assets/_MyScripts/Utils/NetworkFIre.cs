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
    public enum DissolveMode
    {
        FullHierarchy,   // dissolve + hide everything (parent + children)
        ParentOnly,      // only hide the parent's meshes/colliders
        VisualOnly       // just play FX, don't hide/destroy
    }

    [Header("Dissolve Settings")]
    [SerializeField] DissolveMode dissolveMode = DissolveMode.FullHierarchy;


    [Tooltip("If true, smoke & fire durations are derived from Rigidbody.mass.")]
    [Header("Auto Duration From Mass")]
    [SerializeField] bool autoConfigureFromMass = true;
    [SerializeField] float defaultMass = 5f;
    [SerializeField] float smokePerKg = 0.2f;         // seconds of smoke per 1 mass unit
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
    Rigidbody _rb;

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
        _rb = GetComponent<Rigidbody>();
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
        if (IsBurning)  return; // already burning

        IsSmoking    = true;
        SmokeEndTime = Runner.SimulationTime + smokeDuration;
    }

    /// <summary>Called only on StateAuthority to start fire directly.</summary>
    public void IgniteFire()
    {
        if (!Object.HasStateAuthority) return;

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
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        double now = Runner.SimulationTime;

        if (IsBurning)
        {
            // Check if this fire is on the player
            if (TryGetComponent<NetworkPlayer>(out var player))
            {
                player.TakeFireDamage(0.08f);
            }
        }

        // Smoking → Fire after duration
        if (IsSmoking && now >= SmokeEndTime)
        {
            IgniteFire();
        }

        // Fire finished
        if (IsBurning && now >= FireEndTime)
        {
            if (dissolvable)
                BeginDissolve();
        }
    }


    void BeginDissolve()
    {
        if (!Object.HasStateAuthority) return;
        if (dissolveMode == DissolveMode.VisualOnly)
        {
            IsBurning = false;
            IsSmoking = false;
            return;
        }
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

        // Tell any hands grabbing this rigidbody to release
        if (_rb != null)
        {
            var hands = FindObjectsOfType<HandGrabHandler>();
            for (int i = 0; i < hands.Length; i++)
            {
                if (hands[i].ConnectedBody == _rb)
                {
                    hands[i].ReleaseIfLatched();
                }
            }
        }

        if (fire != null)    fire.Stop();
        if (smoke != null)   smoke.Stop();
        if (dissolveMode != DissolveMode.VisualOnly && dissolve != null)
            dissolve.Play();

        // Decide what to actually hide based on dissolveMode
        switch (dissolveMode)
        {
            case DissolveMode.FullHierarchy:
            {
                MeshRenderer[] meshRends = GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < meshRends.Length; i++)
                    meshRends[i].enabled = false;

                SkinnedMeshRenderer[] skinnedRends = GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < skinnedRends.Length; i++)
                    skinnedRends[i].enabled = false;

                // Disable all colliders (root + children)
                Collider[] colliders = GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                    colliders[i].enabled = false;

                break;
            }

            case DissolveMode.ParentOnly:
            {
                var mr  = GetComponent<MeshRenderer>();
                var smr = GetComponent<SkinnedMeshRenderer>();

                if (mr)  mr.enabled  = false;
                if (smr) smr.enabled = false;

                // Disable colliders on the root only
                Collider[] rootColliders = GetComponents<Collider>();
                for (int i = 0; i < rootColliders.Length; i++)
                    rootColliders[i].enabled = false;

                // Children stay as-is
                break;
            }

            case DissolveMode.VisualOnly:
            {
                break;
            }
        }
    }

}
