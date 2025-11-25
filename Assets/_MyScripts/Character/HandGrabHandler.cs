using UnityEngine;
using Fusion;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class HandGrabHandler : MonoBehaviour
{
    [Header("Visuals / Feedback")]
    [SerializeField] Animator animator;
    [SerializeField] NetworkObject grabIndicatorPrefab;

    [Header("Mass Tweaks")]
    [SerializeField] float grabbedMassScale = 0.1f;
    [SerializeField] float liftableMaxMass = 10f;
    [SerializeField] float minGrabbedMass = 0.1f;

    [Header("Arm Stiffness")]
    [SerializeField] ConfigurableJoint armJoint;   // drag the correct arm joint in the Inspector
    [SerializeField] float grabSpring = 500f;      // stiffer while grabbing
    float defaultSpring;

    // Mass management (shared across hands)
    static Dictionary<Rigidbody, (float originalMass, int grabCount)> massData =
        new Dictionary<Rigidbody, (float originalMass, int grabCount)>();

    // Per-hand runtime state
    Rigidbody grabbedBody;
    bool hasMassOverride = false;
    FixedJoint fixedJoint;
    Rigidbody rigidbody3D;

    NetworkObject grabIndicatorNO;
    NetworkPlayer networkPlayer;

    public enum HandSide { Left, Right }
    [SerializeField] HandSide side = HandSide.Left;

    public bool IsLatched => fixedJoint != null;
    public HandSide Side => side;
    public Rigidbody ConnectedBody => fixedJoint ? fixedJoint.connectedBody : null;
    public bool IsLatchedToKinematic =>
        fixedJoint && fixedJoint.connectedBody && fixedJoint.connectedBody.isKinematic;

    double nextAllowedGrabTime = -1;
    const double regrabDelay = 0.25;

    int grabParamHash;

    void Awake()
    {
        networkPlayer = transform.root.GetComponent<NetworkPlayer>();
        rigidbody3D = GetComponent<Rigidbody>();
        rigidbody3D.solverIterations = 255;

        // Cache default spring from the assigned arm joint
        if (armJoint != null)
        {
            var d = armJoint.slerpDrive;
            defaultSpring = d.positionSpring;
        }
        else
        {
            Debug.LogWarning($"[HandGrabHandler] No armJoint assigned on {name}. Arm stiffness won't change.");
        }

        string paramName = (side == HandSide.Left) ? "IsLeftGrabbing" : "IsRightGrabbing";
        grabParamHash = Animator.StringToHash(paramName);
    }

    void SetArmStiff(bool grabbing)
    {
        if (armJoint == null) return;

        float targetSpring = grabbing ? grabSpring : defaultSpring;

        var drive = armJoint.slerpDrive;
        drive.positionSpring = targetSpring;
        armJoint.slerpDrive = drive;
    }

    public void UpdateState()
    {
        bool handWantsGrab =
            (side == HandSide.Left) ? networkPlayer.IsLeftGrab : networkPlayer.IsRightGrab;

        if (handWantsGrab)
        {
            if (animator) animator.SetBool(grabParamHash, true);

            // Keep indicator following the grab point while holding
            if (fixedJoint != null && grabIndicatorNO != null && fixedJoint.connectedBody != null)
            {
                grabIndicatorNO.transform.position =
                    fixedJoint.connectedBody.transform.TransformPoint(fixedJoint.connectedAnchor);
            }
        }
        else
        {
            
            // Released grab intent: drop joint, restore mass, despawn indicator, relax arm
            if (fixedJoint != null)
            {
                PlayReleaseSound();

                if (hasMassOverride && grabbedBody == fixedJoint.connectedBody)
                    RestoreGrabbedBodyMass();

                Destroy(fixedJoint);
                fixedJoint = null;
            }

            if (grabIndicatorNO != null)
            {
                networkPlayer.Runner.Despawn(grabIndicatorNO);
                grabIndicatorNO = null;
            }

            SetArmStiff(false);

            if (animator) animator.SetBool(grabParamHash, false);
        }
    }

    bool TryCarryObject(Collision collision)
    {
        // Only state authority actually attaches stuff
        if (!networkPlayer.Object.HasStateAuthority) return false;
        if (!networkPlayer.IsActiveRagdoll) return false;
        if (networkPlayer.Runner.SimulationTime < nextAllowedGrabTime) return false;
        if (networkPlayer.Stamina <= 0) return false;

        bool handWantsGrab =
            (side == HandSide.Left) ? networkPlayer.IsLeftGrab : networkPlayer.IsRightGrab;
        if (!handWantsGrab) return false;

        if (fixedJoint != null) return false;
        if (collision.transform.root == networkPlayer.transform) return false;

        if (!collision.collider.TryGetComponent(out Rigidbody otherBody)) return false;

        // Contact point for stable anchor
        Vector3 contact = (collision.contactCount > 0)
            ? collision.GetContact(0).point
            : collision.collider.ClosestPoint(transform.position);

        // Create joint
        fixedJoint = gameObject.AddComponent<FixedJoint>();
        fixedJoint.connectedBody = otherBody;
        fixedJoint.autoConfigureConnectedAnchor = false;
        fixedJoint.anchor = transform.InverseTransformPoint(contact);
        fixedJoint.connectedAnchor = otherBody.transform.InverseTransformPoint(contact);
        fixedJoint.breakForce = 1000f;
        fixedJoint.breakTorque = 1000f;
        var soundProfile = otherBody.GetComponent<SoundProfile>();
        if (soundProfile != null)
        {
            var soundNO = soundProfile.GetComponent<NetworkObject>();

            if (soundNO != null)
            {
                networkPlayer.RpcPlayLocalObjectSound(soundNO,
                    SoundProfile.SoundEvent.Grab,
                    contact,
                    1f);
            }
            else
            {
                soundProfile.PlayLocal(SoundProfile.SoundEvent.Grab, contact, 1f);
            }
        }


        grabbedBody = otherBody;
        hasMassOverride = false;

        // Mass scaling for liftable non-kinematic bodies
        if (!grabbedBody.isKinematic && grabbedBody.mass <= liftableMaxMass)
        {
            if (!massData.TryGetValue(grabbedBody, out var data))
            {
                data.originalMass = grabbedBody.mass;
                data.grabCount = 0;
            }

            data.grabCount++;

            if (data.grabCount == 1)
            {
                float targetMass = data.originalMass * grabbedMassScale;
                grabbedBody.mass = Mathf.Max(targetMass, minGrabbedMass);
            }

            massData[grabbedBody] = data;
            hasMassOverride = true;
        }
        else
        {
            grabbedBody = null;
            hasMassOverride = false;
        }

        ShowGrabIndicator(contact);
        SetArmStiff(true);

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

        PlayReleaseSound();

        Destroy(fixedJoint);
        fixedJoint = null;
        nextAllowedGrabTime = networkPlayer.Runner.SimulationTime + regrabDelay;

        if (grabIndicatorNO)
        {
            networkPlayer.Runner.Despawn(grabIndicatorNO);
            grabIndicatorNO = null;
        }

        if (hasMassOverride)
            RestoreGrabbedBodyMass();

        SetArmStiff(false);

        if (animator) animator.SetBool(grabParamHash, false);
        return true;
    }

    void OnJointBreak(float breakForce)
    {
        if (fixedJoint)
        {
            PlayReleaseSound();

            Destroy(fixedJoint);
            fixedJoint = null;
        }

        if (hasMassOverride)
            RestoreGrabbedBodyMass();

        SetArmStiff(false);

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
        grabbedBody = null;
    }

    void PlayReleaseSound()
    {
        if (fixedJoint == null || fixedJoint.connectedBody == null)
            return;

        var sp = fixedJoint.connectedBody.GetComponent<SoundProfile>();
        if (sp != null)
        {
            var soundNO = sp.GetComponent<NetworkObject>();

            if (soundNO != null)
            {
                networkPlayer.RpcPlayLocalObjectSound(soundNO,
                    SoundProfile.SoundEvent.Release,
                    transform.position,
                    1f);
            }
            else
            {
                sp.PlayLocal(SoundProfile.SoundEvent.Release, transform.position, 1f);
            }
        }
    }
}
