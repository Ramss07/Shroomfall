//This script copies the local player's shroom appearance to a target ShroomCustomizerMPB when enabled
using UnityEngine;

public class ShroomAppearanceMirror : MonoBehaviour {
  [SerializeField] ShroomCustomizerMPB target;
  void OnEnable() => TryMirror();

  public void TryMirror() {
    var lp = NetworkPlayer.Local;
    if (lp == null || target == null) return;

    var src = lp.GetComponentInChildren<ShroomCustomizerMPB>(true);
    if (src == null) return;

    target.ApplyData(src.GetData());
  }
}
