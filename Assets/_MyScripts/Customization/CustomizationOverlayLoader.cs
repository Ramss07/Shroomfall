using UnityEngine;
using UnityEngine.SceneManagement;

public class CustomizationOverlayLoader : MonoBehaviour
{
    public const string OverlayScene = "CustomizationOverlay";

    [SerializeField] GameObject lobbyUIRoot;
    [SerializeField] KeyCode toggleKey = KeyCode.C;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C) && !SceneManager.GetSceneByName(OverlayScene).isLoaded)
            OpenCustomization();
    }


    public void OpenCustomization()
    {
        if (!SceneManager.GetSceneByName(OverlayScene).isLoaded)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(OverlayScene, LoadSceneMode.Additive);

        if (lobbyUIRoot) lobbyUIRoot.SetActive(false);
    }

    public void CloseCustomization()
    {
        var s = SceneManager.GetSceneByName(OverlayScene);
        if (s.isLoaded)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SceneManager.UnloadSceneAsync(s);

        if (lobbyUIRoot) lobbyUIRoot.SetActive(true);
    }
}
