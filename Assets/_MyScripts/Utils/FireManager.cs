using System.Collections.Generic;
using UnityEngine;
using Fusion;

public class FireManager : NetworkBehaviour
{
    public static FireManager Instance { get; private set; }

    [Header("Spread Settings")]
    [SerializeField] float burnDistance = 0.6f;   // in world units
    float burnDistanceSqr;

    readonly List<NetworkFire> _fires = new List<NetworkFire>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        burnDistanceSqr = burnDistance * burnDistance;
    }

    public override void Spawned()
    {
        // No special network setup needed. Only StateAuthority will simulate.
    }

    // Called by NetworkFire.Spawned/Despawned
    public void Register(NetworkFire fire)
    {
        if (!_fires.Contains(fire))
            _fires.Add(fire);
    }

    public void Unregister(NetworkFire fire)
    {
        _fires.Remove(fire);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        SpreadFire();
        // Optional: Extinguish logic here if you want
    }

    void SpreadFire()
    {
        int count = _fires.Count;
        if (count == 0) return;

        // Simple O(N^2) for now, but only over burnables (not whole scene)
        for (int i = 0; i < count; i++)
        {
            var a = _fires[i];
            if (a == null || !a.Burning) continue;

            Vector3 aPos = a.transform.position;

            for (int j = 0; j < count; j++)
            {
                if (i == j) continue;
                var b = _fires[j];
                if (b == null) continue;

                // Already burning or smoking → skip
                if (b.Burning || b.Smoking || b.IsDissolved) continue;

                Vector3 bPos = b.transform.position;
                float distSqr = (aPos - bPos).sqrMagnitude;
                if (distSqr <= burnDistanceSqr)
                {
                    // Start smoke on neighbor
                    b.IgniteSmoke();
                }
            }
        }
    }
}
