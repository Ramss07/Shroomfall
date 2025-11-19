using UnityEngine;

public class DetectCollision : MonoBehaviour
{
    NetworkPlayer networkPlayer;
    Rigidbody hitRigidbody;
    //Array to store which points detect collision
    ContactPoint[] contactPoints = new ContactPoint[5];

    void Awake()
    {
        networkPlayer = GetComponentInParent<NetworkPlayer>();
        hitRigidbody = GetComponent<Rigidbody>();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!networkPlayer.HasStateAuthority)
            return;

        if (!networkPlayer.IsActiveRagdoll)
            return;

        if (!collision.collider.CompareTag("CauseDamage"))
            return;

        //If root of object is the same as the player, ignore (self collision protection if we have weapons)
        if (collision.collider.transform.root == networkPlayer.transform)
            return;

        int numberOfContacts = collision.GetContacts(contactPoints);
        for (int i = 0; i < numberOfContacts; i++)
        {
            ContactPoint contactPoint = contactPoints[i];

            //Get the contact impulse
            Vector3 contactImpulse = contactPoint.impulse / Time.fixedDeltaTime;
            float mag = contactImpulse.magnitude;

            //Check that the force was great enough to cause damage
            if (mag < 15f)
                continue;

            //Calculate a damage value based on the impulse
            float t = Mathf.InverseLerp(15f, 60f, mag);
            //15 -> ~10 dmg, 60+ -> ~40 dmg (clamped)
            int damage = Mathf.RoundToInt(Mathf.Lerp(10f, 40f, t));

            //Apply an impulse force to the body part that was hit, away from the contact point
            Vector3 forceDirection = (contactImpulse + Vector3.up) * 0.5f;

            //Limit the force so it doesnt get too big
            forceDirection = Vector3.ClampMagnitude(forceDirection, 30);
            Debug.DrawRay(hitRigidbody.position, forceDirection * 40, Color.red, 4);

            //Increase the effect of the hit ?
            networkPlayer.OnPlayerBodyPartHit(damage, forceDirection, hitRigidbody);


        }
        
    }
}
