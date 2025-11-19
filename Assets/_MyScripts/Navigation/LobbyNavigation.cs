
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuNavigation : MonoBehaviour {
  [SerializeField] string lobbySceneName = "LobbyScene"; // set to your Lobby scene name

  public void GoToLobby() {
    SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
  }
}
