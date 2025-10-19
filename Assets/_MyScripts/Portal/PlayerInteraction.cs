using UnityEngine;
using Fusion;

public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private float interactionRadius = 0.5f;
    [SerializeField] private LayerMask portalLayer;

    private Camera _mainCamera;
    private PortalController _currentPortal;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    void Update()
    {
        if (!HasStateAuthority) return;

        Ray ray = _mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        PortalController detectedPortal = null;
        if (Physics.SphereCast(ray, interactionRadius, out RaycastHit hit, interactionDistance, portalLayer))
        {
            hit.collider.TryGetComponent(out detectedPortal);
        }

        if (detectedPortal != null)
        {
            if (detectedPortal != _currentPortal)
            {
                _currentPortal?.HidePrompt();
                _currentPortal = detectedPortal;
                _currentPortal.ShowPrompt();
            }
        }
        else
        {
            if (_currentPortal != null)
            {
                _currentPortal.HidePrompt();
                _currentPortal = null;
            }
        }

        if (_currentPortal != null && Input.GetKeyDown(KeyCode.E))
        {
            if (NetworkPlayer.Local != null)
            {
                NetworkPlayer.Local.RPC_CastVote(_currentPortal);
            }
        }
    }
}