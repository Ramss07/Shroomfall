using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HandGrabHandler : MonoBehaviour
{
    [SerializeField] Animator animator;

    //Fixed joint for grabbing
    FixedJoint fixedJoint;

    //Our own rigidbody
    Rigidbody rigidbody3D;

    //References
    NetworkPlayer networkPlayer;

    void Awake()
    {
        //Get references
        networkPlayer = transform.root.GetComponent<NetworkPlayer>();
        rigidbody3D = GetComponent<Rigidbody>();

        //Change solver iterations to prevent joint from flexing too much
        rigidbody3D.solverIterations = 255; 
    }

    public void UpdateState()
    {
        //Check if grabbing is active
        if (networkPlayer.IsGrabbingActive)
        {
            animator.SetBool("IsGrabbing", true);
        }
        else
        {
            //We are no longer carrying, chec if there is a joint to destroy
            if (fixedJoint != null)
            {
                //Give the connected rigidbody some velocity
                if (fixedJoint.connectedBody != null)
                {
                    float forceAmountMultiplier = 0.1f;
                    //Get the other player
                    if (fixedJoint.connectedBody.transform.root.TryGetComponent(out NetworkPlayer otherNetworkPlayer))
                    {
                        //If the other player is in ragdoll mode, apply more force
                        if (otherNetworkPlayer.IsActiveRagdoll)
                            forceAmountMultiplier = 10f;
                        else forceAmountMultiplier = 15f;
                    }

                    //Toss the object a bit when releasing
                    fixedJoint.connectedBody.AddForce((networkPlayer.transform.forward + Vector3.up * 0.25f) * forceAmountMultiplier, ForceMode.Impulse);
                }
                Destroy(fixedJoint);

            }
            //Change animator state
            animator.SetBool("IsGrabbing", false);
            //animator.SetBool("IsClimbing", false);
        }
    }

    bool TryCarryObject(Collision collision)
    {
        //Check if we are allowed to carry objects
        if (!networkPlayer.Object.HasStateAuthority)
            return false;

        //Check if we are in ragdoll mode
        if (!networkPlayer.IsActiveRagdoll)
            return false;

        //Check that we are trying to grab something
        if (!networkPlayer.IsGrabbingActive)
            return false;

        //Check if we are already carrying something
        if (fixedJoint != null)
            return false;

        //Avoid trying to grab yourself
        if (collision.transform.root == networkPlayer.transform)
            return false;

        //Check if the object has a rigidbody
        if (!collision.collider.TryGetComponent(out Rigidbody otherObjectRigidbody))
            return false;

        //Add a fixed joint
        fixedJoint = transform.gameObject.AddComponent<FixedJoint>();

        //Connect the joint to the other object
        fixedJoint.connectedBody = otherObjectRigidbody;

        //Will take care of the anchor point on our own
        fixedJoint.autoConfigureConnectedAnchor = false;

        //Transform the contact point to local space
        fixedJoint.connectedAnchor = collision.transform.InverseTransformPoint(collision.GetContact(0).point);


        //Set animator to carry
        animator.SetBool("IsGrabbing", true);
        return true;
    }

    void OnCollisionEnter(Collision collision)
    {
        //Attempt to carry the other object
        TryCarryObject(collision);
    }

}
