using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Collections;

public class NetworkRotateObject : NetworkBehaviour
{
    [SerializeField]
    Rigidbody rigidbody3D;

    [SerializeField]
    Vector3 rotationAmount;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            Vector3 rotateBy = transform.rotation.eulerAngles + rotationAmount * Runner.DeltaTime;

            if (rigidbody3D != null)
                rigidbody3D.MoveRotation(Quaternion.Euler(rotateBy));
            else
                transform.rotation = Quaternion.Euler(rotateBy);
        }
    }
}
