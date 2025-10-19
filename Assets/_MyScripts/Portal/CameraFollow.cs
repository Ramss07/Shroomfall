using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    // You can adjust these in the Inspector for the perfect camera angle.
    [SerializeField] private Vector3 offset = new Vector3(0, 5, -10);
    [SerializeField] private float smoothSpeed = 0.125f;

    // We use LateUpdate for cameras to make sure the player has
    // finished all its movement calculations for the frame.
    void LateUpdate()
    {
        // First, check if the local player has been set yet.
        // It might be null for a few frames while the scene loads.
        if (NetworkPlayer.Local != null)
        {
            // Calculate the desired position for the camera.
            Transform playerTransform = NetworkPlayer.Local.transform;
            Vector3 desiredPosition = playerTransform.position + offset;

            // Smoothly move the camera towards that position.
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
            transform.position = smoothedPosition;

            // Make sure the camera is always looking at the player.
            transform.LookAt(playerTransform);
        }
    }
}