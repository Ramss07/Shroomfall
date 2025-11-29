using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class NetworkPortal : MonoBehaviour
{
    [SerializeField] private string portalID; // Unique identifier for this portal
    [SerializeField] private string linkedPortalID; // ID of the portal to teleport to
    [SerializeField] private Vector3 teleportOffset = Vector3.zero;
    [SerializeField] private float teleportCooldown = 1f;
    
    private NetworkPortal linkedPortal;
    private double lastTeleportTime = -999;

    private void Start()
    {
        // find the linked portal by ID
        if (linkedPortal == null && !string.IsNullOrEmpty(linkedPortalID))
        {
            NetworkPortal[] allPortals = FindObjectsOfType<NetworkPortal>();
            foreach (NetworkPortal portal in allPortals)
            {
                if (portal.portalID == linkedPortalID)
                {
                    linkedPortal = portal;
                    Debug.Log($"[NetworkPortal] {gameObject.name} linked to {linkedPortal.gameObject.name}");
                    break;
                }
            }

            if (linkedPortal == null)
                Debug.LogError($"[NetworkPortal] Could not find portal with ID: {linkedPortalID}");
        }

        // verify setup
        if (GetComponent<Collider>() == null)
            Debug.LogError($"[NetworkPortal] {gameObject.name} has no Collider!");
        else if (!GetComponent<Collider>().isTrigger)
            Debug.LogError($"[NetworkPortal] {gameObject.name} Collider is not set to 'Is Trigger'!");
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[NetworkPortal] OnTriggerEnter called with {other.gameObject.name}");

        // get the NetworkRunner to check authority
        NetworkRunner runner = FindObjectOfType<NetworkRunner>();
        if (runner == null)
        {
            Debug.LogError("[NetworkPortal] No NetworkRunner found!");
            return;
        }

        if (!runner.IsServer)
        {
            Debug.Log("[NetworkPortal] Not server, ignoring collision");
            return;
        }

        // check if it's a NetworkPlayer
        NetworkPlayer player = other.GetComponent<NetworkPlayer>();
        if (player == null)
        {
            Debug.Log($"[NetworkPortal] {other.gameObject.name} is not a NetworkPlayer");
            return;
        }

        Debug.Log($"[NetworkPortal] Found NetworkPlayer: {player.gameObject.name}");

        // check if linked portal is set
        if (linkedPortal == null)
        {
            Debug.LogError($"[NetworkPortal] Portal on {gameObject.name} has no linked portal set!");
            return;
        }

        Debug.Log($"[NetworkPortal] Linked portal found: {linkedPortal.gameObject.name}");

        // check cooldown to prevent rapid re-teleportation
        if (runner.SimulationTime - lastTeleportTime < teleportCooldown)
        {
            Debug.Log($"[NetworkPortal] Cooldown active, skipping teleport");
            return;
        }

        lastTeleportTime = runner.SimulationTime;

        // teleport the player
        Vector3 targetPos = linkedPortal.transform.position + teleportOffset;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        
        if (rb != null)
        {
            rb.MovePosition(targetPos);
            Debug.Log($"[NetworkPortal] Teleported {player.gameObject.name} to {targetPos}");
        }
        else
        {
            Debug.LogError($"[NetworkPortal] Player has no Rigidbody!");
        }

        NetworkRigidbody3D networkRb = player.GetComponent<NetworkRigidbody3D>();
        if (networkRb != null)
        {
            networkRb.Teleport(targetPos, Quaternion.identity);
            Debug.Log($"[NetworkPortal] Also called NetworkRigidbody3D.Teleport");
        }
    }

#if UNITY_EDITOR
    // draw linked portal connection in editor
    private void OnDrawGizmos()
    {
        if (linkedPortal != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, linkedPortal.transform.position);
            Gizmos.DrawWireSphere(linkedPortal.transform.position + teleportOffset, 0.5f);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
#endif
}