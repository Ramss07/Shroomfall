using UnityEngine;
using UnityEngine.SceneManagement;

public class CustomizationOverlayLoader : MonoBehaviour
{
  public const string OverlayScene = "CustomizationOverlay";

  [SerializeField] GameObject lobbyUIRoot;  // ← assign your Lobby UI Canvas root

  public void OpenCustomization()
  {
    if (!SceneManager.GetSceneByName(OverlayScene).isLoaded)
      SceneManager.LoadScene(OverlayScene, LoadSceneMode.Additive);

    if (lobbyUIRoot) lobbyUIRoot.SetActive(false);  // hide Lobby UI while overlay is open
  }

  public void CloseCustomization()
  {
    var s = SceneManager.GetSceneByName(OverlayScene);
    if (s.isLoaded) SceneManager.UnloadSceneAsync(s);
    if (lobbyUIRoot) lobbyUIRoot.SetActive(true);   // show Lobby UI again
  }
  
  
}
