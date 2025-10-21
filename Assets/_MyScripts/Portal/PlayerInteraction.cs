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
        // Local player only
        if (!Object.HasInputAuthority) return;

        if (_mainCamera == null)
            _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        Ray ray = _mainCamera.ScreenPointToRay(
            new Vector3(Screen.width / 2f, Screen.height / 2f));

        PortalController detectedPortal = null;
        if (Physics.SphereCast(ray, interactionRadius, out RaycastHit hit, interactionDistance, portalLayer))
        {
            hit.collider.TryGetComponent(out detectedPortal);
        }

        if (detectedPortal != null)
        {
            if (detectedPortal != _currentPortal)
            {
                _currentPortal?.HidePrompt();  // local-only visual
                _currentPortal = detectedPortal;
                _currentPortal.ShowPrompt();   // local-only visual
            }
        }
        else if (_currentPortal != null)
        {
            _currentPortal.HidePrompt();
            _currentPortal = null;
        }

        if (_currentPortal != null && Input.GetKeyDown(KeyCode.E))
        {
            if (NetworkPlayer.Local != null)
                NetworkPlayer.Local.RPC_CastVote(_currentPortal);
        }
    }
}
