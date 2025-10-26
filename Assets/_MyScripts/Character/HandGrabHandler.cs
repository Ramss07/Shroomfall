using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class HandGrabHandler : MonoBehaviour
{
    [SerializeField] Animator animator;

    // Prefab for the persistent grab marker (set in inspector)
    [SerializeField] GameObject grabIndicatorPrefab;

    // Fixed joint created when grabbing
    FixedJoint fixedJoint;

    // Our own rigidbody
    Rigidbody rigidbody3D;

    // Spawned indicator instance (world-space, never parented)
    GameObject grabIndicator;

    // References
    NetworkPlayer networkPlayer;

    public enum HandSide { Left, Right }
    [SerializeField] HandSide side = HandSide.Left;

    // Animator param (per-hand)
    int grabParamHash;

    void Awake()
    {
        networkPlayer = transform.root.GetComponent<NetworkPlayer>();
        rigidbody3D   = GetComponent<Rigidbody>();
        rigidbody3D.solverIterations = 255;

        string paramName = (side == HandSide.Left) ? "IsLeftGrabbing" : "IsRightGrabbing";
        grabParamHash = Animator.StringToHash(paramName);
    }

    public void UpdateState()
    {
        bool handWantsGrab = (side == HandSide.Left) ? networkPlayer.IsLeftGrab : networkPlayer.IsRightGrab;

        if (handWantsGrab)
        {
            if (animator) animator.SetBool(grabParamHash, true);

            // Keep indicator following the grab point while holding (world space)
            if (fixedJoint != null && grabIndicator != null && fixedJoint.connectedBody != null)
            {
                grabIndicator.transform.position =
                    fixedJoint.connectedBody.transform.TransformPoint(fixedJoint.connectedAnchor);
            }
        }
        else
        {
            // Release: drop joint, toss a bit, remove indicator
            if (fixedJoint != null)
            {
                if (fixedJoint.connectedBody != null)
                {
                    float forceAmountMultiplier = 10f;
                    if (fixedJoint.connectedBody.transform.root.TryGetComponent(out NetworkPlayer otherNetworkPlayer))
                        forceAmountMultiplier = otherNetworkPlayer.IsActiveRagdoll ? 15f : 10f;

                    fixedJoint.connectedBody.AddForce(
                        (networkPlayer.transform.forward + Vector3.up * 0.25f) * forceAmountMultiplier,
                        ForceMode.Impulse
                    );
                }

                Destroy(fixedJoint);
                fixedJoint = null;
            }

            if (grabIndicator != null)
            {
                Destroy(grabIndicator);
                grabIndicator = null;
            }

            if (animator) animator.SetBool(grabParamHash, false);
        }
    }

    bool TryCarryObject(Collision collision)
    {
        if (!networkPlayer.Object.HasStateAuthority) return false;
        if (!networkPlayer.IsActiveRagdoll)          return false;

        bool handWantsGrab = (side == HandSide.Left) ? networkPlayer.IsLeftGrab : networkPlayer.IsRightGrab;
        if (!handWantsGrab) return false;

        if (fixedJoint != null) return false;
        if (collision.transform.root == networkPlayer.transform) return false;

        if (!collision.collider.TryGetComponent(out Rigidbody otherObjectRigidbody)) return false;

        // Use a safe contact point
        Vector3 contact = (collision.contactCount > 0)
            ? collision.GetContact(0).point
            : collision.collider.ClosestPoint(transform.position);

        // Create joint
        fixedJoint = gameObject.AddComponent<FixedJoint>();
        fixedJoint.connectedBody = otherObjectRigidbody;
        fixedJoint.autoConfigureConnectedAnchor = false;

        // Set anchors (hand/local and object/local)
        fixedJoint.anchor          = transform.InverseTransformPoint(contact);
        fixedJoint.connectedAnchor = collision.transform.InverseTransformPoint(contact);

        fixedJoint.breakForce  = 500f;
        fixedJoint.breakTorque = 500f;

        // Spawn/position a persistent indicator in world space (no parenting)
        ShowGrabIndicator(contact);

        if (animator) animator.SetBool(grabParamHash, true);
        return true;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Attempt to grab when we collide and intent is active
        TryCarryObject(collision);
    }

    void OnJointBreak(float breakForce)
    {
        if (fixedJoint != null)
        {
            Destroy(fixedJoint);
            fixedJoint = null;
        }

        if (animator) animator.SetBool(grabParamHash, false);

        if (grabIndicator != null)
        {
            Destroy(grabIndicator);
            grabIndicator = null;
        }
    }

    // Spawns (or reuses) the prefab indicator at a world position, unparented so scale stays uniform
    void ShowGrabIndicator(Vector3 worldPos)
    {
        if (grabIndicator == null)
        {
            if (grabIndicatorPrefab == null) return; // nothing to spawn
            grabIndicator = Instantiate(grabIndicatorPrefab, worldPos, Quaternion.identity);
        }
        else
        {
            grabIndicator.transform.position = worldPos;
        }

        // Ensure no parent so it never inherits scale
        grabIndicator.transform.SetParent(null, true);
        grabIndicator.SetActive(true);
    }
}
