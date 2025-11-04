using UnityEngine;
using UnityEngine.SceneManagement;

public class CustomizationOverlayLoader : MonoBehaviour
{
    public const string OverlayScene = "CustomizationOverlay";

    [SerializeField] GameObject lobbyUIRoot;
    [SerializeField] GameObject playerUIRoot;
    [SerializeField] KeyCode toggleKey = KeyCode.C;

    public bool canCustomize = false;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && canCustomize)
        {
            var overlay = SceneManager.GetSceneByName(OverlayScene);
            if (!overlay.isLoaded)
                OpenCustomization();
            else
                CloseCustomization();
        }
    }

    public void OpenCustomization()
    {
        if (!SceneManager.GetSceneByName(OverlayScene).isLoaded)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadSceneAsync(OverlayScene, LoadSceneMode.Additive);
        }

        if (lobbyUIRoot)
            lobbyUIRoot.SetActive(false);

        // Disable input
        var player = NetworkPlayer.Local;
        if (player) player.isCustomizing = true;

        SetPlayerUIVisible(false);
    }

    public void CloseCustomization()
    {
        var s = SceneManager.GetSceneByName(OverlayScene);
        if (s.isLoaded)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SceneManager.UnloadSceneAsync(s);
        }

        if (lobbyUIRoot)
            lobbyUIRoot.SetActive(true);

        // Enable input
        var player = NetworkPlayer.Local;
        if (player) player.isCustomizing = false;
        SetPlayerUIVisible(true);
    }

    void SetPlayerUIVisible(bool visible)
    {
        if (!playerUIRoot) return;
        playerUIRoot.SetActive(visible);
    }
}
