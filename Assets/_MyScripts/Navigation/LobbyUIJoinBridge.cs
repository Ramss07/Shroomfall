using System.Linq;
using UnityEngine;
using TMPro;

public class LobbyUIJoinBridge : MonoBehaviour {
  public NetworkRunnerHandler handler;
  public TMP_InputField codeInput;
  public JoinErrorUI errorUI;

  public void OnJoinClick()
  {
    var code = Sanitize(codeInput.text);
    if (!IsValidCode(code))
    {
      Debug.Log("Enter a 6-character code.");
      if (errorUI) errorUI.ShowInvalid();
      return; // <-- don't call JoinGame if invalid/blank
    }
    handler.JoinGame(code);
  }

static string Sanitize(string s) =>
  new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

  static bool IsValidCode(string s) => s.Length == 6;
}
