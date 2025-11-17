using UnityEngine;
using Fusion;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class HandGrabHandler : MonoBehaviour
{
    [SerializeField] Animator animator;

    // Networked prefab (must have NetworkObject + NetworkTransform)
    [SerializeField] NetworkObject grabIndicatorPrefab;
    [SerializeField] float grabbedMassScale = 0.1f;
    [SerializeField] float liftableMaxMass = 5f;
    [SerializeField] float minGrabbedMass = 0.1f;

    Rigidbody grabbedBody;
    float grabbedBodyOriginalMass;
    bool hasMassOverride = false;
    
    static Dictionary<Rigidbody, (float originalMass, int grabCount)> massData = new Dictionary<Rigidbody, (float originalMass, int grabCount)>();

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
                    float forceAmountMultiplier = 5f;
                    if (fixedJoint.connectedBody.transform.root.TryGetComponent(out NetworkPlayer otherNetworkPlayer))
                        forceAmountMultiplier = otherNetworkPlayer.IsActiveRagdoll ? 15f : 10f;
                        
                    if (hasMassOverride && grabbedBody == fixedJoint.connectedBody)
                        RestoreGrabbedBodyMass();
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

        grabbedBody = otherObjectRigidbody;

        if (!grabbedBody.isKinematic)
        {
            if (grabbedBody.mass <= liftableMaxMass)
            {
                if (!massData.TryGetValue(grabbedBody, out var data))
                {
                    data.originalMass = grabbedBody.mass;
                    data.grabCount    = 0;
                }

                data.grabCount++;

                if (data.grabCount == 1)
                {
                    float targetMass = data.originalMass * grabbedMassScale;
                    grabbedBody.mass = Mathf.Max(targetMass, minGrabbedMass);
                }

                massData[grabbedBody] = data;
                hasMassOverride       = true;
            }
            else
            {
                hasMassOverride = false;
            }
        }
        else
        {
            grabbedBody     = null;
            hasMassOverride = false;
        }

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
        if (fixedJoint)
        {
            Destroy(fixedJoint);
            fixedJoint = null;
        }
        if (hasMassOverride)
            RestoreGrabbedBodyMass();

        if (animator) animator.SetBool(grabParamHash, false);
        if (grabIndicatorNO)
        {
            networkPlayer.Runner.Despawn(grabIndicatorNO);
            grabIndicatorNO = null;
        }
    }

    void RestoreGrabbedBodyMass()
    {
        if (!hasMassOverride || grabbedBody == null)
            return;

        if (massData.TryGetValue(grabbedBody, out var data))
        {
            data.grabCount--;

            if (data.grabCount <= 0)
            {
                grabbedBody.mass = data.originalMass;
                massData.Remove(grabbedBody);
            }
            else
            {
                massData[grabbedBody] = data;
            }
        }
        hasMassOverride = false;
        grabbedBody     = null;
    }
}
