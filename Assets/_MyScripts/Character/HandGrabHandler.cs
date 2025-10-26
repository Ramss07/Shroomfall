using UnityEngine;

public class HandGrabHandler : MonoBehaviour
{
    [SerializeField] Animator animator;

    //Fixed joint for grabbing
    FixedJoint fixedJoint;

    //Our own rigidbody
    Rigidbody rigidbody3D;

    //References
    NetworkPlayer networkPlayer;

    public enum HandSide { Left, Right }
    [SerializeField] HandSide side;

    // Cached Animator param hash for this hand only
    int grabParamHash;

    void Awake()
    {
        networkPlayer = transform.root.GetComponent<NetworkPlayer>();
        rigidbody3D   = GetComponent<Rigidbody>();
        rigidbody3D.solverIterations = 255;

        // Decide which animator bool this hand drives
        string paramName = (side == HandSide.Left) ? "IsLeftGrabbing" : "IsRightGrabbing";
        grabParamHash = Animator.StringToHash(paramName);
    }

    public void UpdateState()
    {

        // Evaluate intent each tick
        bool handWantsGrab = (side == HandSide.Left) ? networkPlayer.IsLeftGrab : networkPlayer.IsRightGrab;

        if (handWantsGrab)
        {
            if (animator) animator.SetBool(grabParamHash, true);
        }
        else
        {
            // Releasing: if we were holding something, toss a bit and drop it
            if (fixedJoint != null)
            {
                if (fixedJoint.connectedBody != null)
                {
                    float forceAmountMultiplier = 0.1f;

                    if (fixedJoint.connectedBody.transform.root.TryGetComponent(out NetworkPlayer otherNetworkPlayer))
                    {
                        // If you want more force on ragdolls, put the larger value in the true branch.
                        forceAmountMultiplier = otherNetworkPlayer.IsActiveRagdoll ? 15f : 10f;
                    }

                    fixedJoint.connectedBody.AddForce(
                        (networkPlayer.transform.forward + Vector3.up * 0.25f) * forceAmountMultiplier,
                        ForceMode.Impulse
                    );
                }
                Destroy(fixedJoint);
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

        // Create the joint anchored at the first contact
        fixedJoint = gameObject.AddComponent<FixedJoint>();
        fixedJoint.connectedBody = otherObjectRigidbody;
        fixedJoint.autoConfigureConnectedAnchor = false;

        var contact = collision.GetContact(0).point;
        fixedJoint.anchor          = transform.InverseTransformPoint(contact);
        fixedJoint.connectedAnchor = collision.transform.InverseTransformPoint(contact);

        fixedJoint.breakForce  = 500f;
        fixedJoint.breakTorque = 500f;

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
        if (fixedJoint != null) Destroy(fixedJoint);
        if (animator) animator.SetBool(grabParamHash, false);
    }
}
