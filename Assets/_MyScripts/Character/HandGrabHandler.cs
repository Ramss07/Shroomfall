using UnityEngine;
using Fusion;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class HandGrabHandler : MonoBehaviour
{
    [SerializeField] Animator animator;

    // Networked prefab (must have NetworkObject + NetworkTransform)
    [SerializeField] NetworkObject grabIndicatorPrefab;

    FixedJoint fixedJoint;
    Rigidbody rigidbody3D;

    // Spawned networked indicator instance
    NetworkObject grabIndicatorNO;

    NetworkPlayer networkPlayer;
    public enum HandSide { Left, Right }
    [SerializeField] HandSide side = HandSide.Left;

    public bool IsLatched => fixedJoint != null;
    public HandSide Side => side;
    public Rigidbody ConnectedBody => fixedJoint ? fixedJoint.connectedBody : null;
    public bool IsLatchedToKinematic => fixedJoint && fixedJoint.connectedBody && fixedJoint.connectedBody.isKinematic;

    double nextAllowedGrabTime = -1;
    const double regrabDelay = 0.25;

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

            // Keep indicator following the grab point while holding (authority moves; NetworkTransform replicates)
            if (fixedJoint != null && grabIndicatorNO != null && fixedJoint.connectedBody != null)
            {
                grabIndicatorNO.transform.position =
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

            if (grabIndicatorNO != null)
            {
                networkPlayer.Runner.Despawn(grabIndicatorNO);
                grabIndicatorNO = null;
            }

            if (animator) animator.SetBool(grabParamHash, false);
        }
    }

    bool TryCarryObject(Collision collision)
    {
        if (!networkPlayer.Object.HasStateAuthority) return false;
        if (!networkPlayer.IsActiveRagdoll) return false;
        if (networkPlayer.Runner.SimulationTime < nextAllowedGrabTime) return false;
        if (networkPlayer.Stamina <= 0) return false;

        bool handWantsGrab = (side == HandSide.Left) ? networkPlayer.IsLeftGrab : networkPlayer.IsRightGrab;
        if (!handWantsGrab) return false;

        if (fixedJoint != null) return false;
        if (collision.transform.root == networkPlayer.transform) return false;

        if (!collision.collider.TryGetComponent(out Rigidbody otherObjectRigidbody)) return false;

        Vector3 contact = (collision.contactCount > 0)
            ? collision.GetContact(0).point
            : collision.collider.ClosestPoint(transform.position);

        fixedJoint = gameObject.AddComponent<FixedJoint>();
        fixedJoint.connectedBody = otherObjectRigidbody;
        fixedJoint.autoConfigureConnectedAnchor = false;

        fixedJoint.anchor          = transform.InverseTransformPoint(contact);
        fixedJoint.connectedAnchor = otherObjectRigidbody.transform.InverseTransformPoint(contact);

        fixedJoint.breakForce  = 500f;
        fixedJoint.breakTorque = 500f;

        ShowGrabIndicator(contact);

        if (animator) animator.SetBool(grabParamHash, true);
        return true;
    }

    void OnCollisionEnter(Collision collision) => TryCarryObject(collision);

    void ShowGrabIndicator(Vector3 worldPos)
    {
        if (grabIndicatorNO == null)
        {
            if (grabIndicatorPrefab == null) return;
            grabIndicatorNO = networkPlayer.Runner.Spawn(grabIndicatorPrefab, worldPos, Quaternion.identity);
        }
        else
        {
            grabIndicatorNO.transform.position = worldPos;
        }
    }

    public bool ReleaseIfLatched()
    {
        if (fixedJoint == null) return false;

        Destroy(fixedJoint);
        fixedJoint = null;
        nextAllowedGrabTime = networkPlayer.Runner.SimulationTime + regrabDelay;

        if (grabIndicatorNO)
        {
            networkPlayer.Runner.Despawn(grabIndicatorNO);
            grabIndicatorNO = null;
        }
        if (animator) animator.SetBool(grabParamHash, false);
        return true;
    }

    void OnJointBreak(float breakForce)
    {
        if (fixedJoint) { Destroy(fixedJoint); fixedJoint = null; }
        if (animator) animator.SetBool(grabParamHash, false);
        if (grabIndicatorNO)
        {
            networkPlayer.Runner.Despawn(grabIndicatorNO);
            grabIndicatorNO = null;
        }
    }
}
