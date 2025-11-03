using UnityEngine;
using Fusion;

public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private float interactionRadius   = 0.5f;
    [SerializeField] private LayerMask portalLayer;

    private Camera _mainCamera;
    private PortalController _currentPortal;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        // Only process local input
        if (!Object || !Object.HasInputAuthority)
            return;

        // Reacquire camera if needed (scene load, etc.)
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        // Ray-sphere style probe for a portal in front of the camera
        _currentPortal = null;
        if (_mainCamera)
        {
            var origin = _mainCamera.transform.position;
            var dir    = _mainCamera.transform.forward;

            if (Physics.SphereCast(origin, interactionRadius, dir, out RaycastHit hit,
                                   interactionDistance, portalLayer, QueryTriggerInteraction.Collide))
            {
                var detectedPortal = hit.collider.GetComponentInParent<PortalController>();
                if (detectedPortal != null)
                {
                    // Update prompt if portal changed
                    if (_currentPortal != detectedPortal)
                    {
                        _currentPortal?.HidePrompt();
                        _currentPortal = detectedPortal;
                        _currentPortal.ShowPrompt();
                    }
                }
            }
        }

        // If we lost the portal, clear the prompt
        if (_currentPortal == null)
        {
            // Nothing to show this frame
        }

        // E to vote
        if (_currentPortal != null && Input.GetKeyDown(KeyCode.E))
        {
            var vm = FindObjectOfType<VotingManager>();
            if (vm == null || vm.Object == null)
            {
                Debug.LogWarning("[PI] VotingManager not ready yet — skipping vote this frame.");
                return;
            }

            // Host calls server method directly (no RPC to itself). Clients use RPC.
            if (vm.Object.HasStateAuthority)
            {
                Debug.Log($"[PI] Host vote → direct call for '{_currentPortal.name}'");
                vm.Server_SetPlayerVote(_currentPortal, Runner.LocalPlayer);
            }
            else
            {
                Debug.Log($"[PI] Client vote → RPC for '{_currentPortal.name}'");
                vm.RPC_SetPlayerVote(_currentPortal);
            }
        }
    }
}
