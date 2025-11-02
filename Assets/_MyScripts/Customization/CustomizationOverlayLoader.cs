using UnityEngine;
using UnityEngine.SceneManagement;

public class CustomizationOverlayLoader : MonoBehaviour
{
    public const string OverlayScene = "CustomizationOverlay";

    [SerializeField] GameObject lobbyUIRoot;
    [SerializeField] string playerUIName = "PlayerUI";
    [SerializeField] KeyCode toggleKey = KeyCode.C;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
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

        SetPlayerUIVisible(true);
    }

    void SetPlayerUIVisible(bool visible)
    {
        var playerUI = GameObject.Find(playerUIName);
        if (!playerUI) return;

        var cg = playerUI.GetComponent<CanvasGroup>();
        if (!cg) 
            cg = playerUI.AddComponent<CanvasGroup>();

        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
    }
}
