using UnityEngine;
using Fusion;
using TMPro;

public class SessionCodeDisplay : MonoBehaviour {
  [SerializeField] TMP_Text label;

  void OnEnable() { InvokeRepeating(nameof(UpdateLabel), 0f, 0.5f); }
  void OnDisable() { CancelInvoke(nameof(UpdateLabel)); }

  void UpdateLabel() {
    var runner = FindObjectOfType<NetworkRunner>();
    if (runner && runner.SessionInfo.IsValid)
      label.text = $"Code: {runner.SessionInfo.Name}";
    else
      label.text = "Code: …";
  }
}
