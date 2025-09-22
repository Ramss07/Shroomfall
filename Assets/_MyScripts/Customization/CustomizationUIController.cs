using System.Collections;
using System.Linq;
using UnityEngine;

public class CustomizationApplyController : MonoBehaviour {
  [Header("Hook this to the PREVIEW model in the overlay")]
  [SerializeField] ShroomCustomizerMPB previewAvatar;

  PlayerAppearanceNet _owner;              // local player's network script
  ShroomCustomizerMPB _liveAvatar;         // local player's in-game customizer

  [SerializeField] MonoBehaviour applyButton;

  void OnEnable() {
    StartCoroutine(FindLocalPlayerThenInit());
  }

  IEnumerator FindLocalPlayerThenInit() {
    // Wait until the local player exists
    while (true) {
      _owner = FindObjectsOfType<PlayerAppearanceNet>(true)
               .FirstOrDefault(p => p && p.Object && p.Object.HasInputAuthority);
      if (_owner) break;
      yield return null;
    }

    _liveAvatar = _owner.GetComponentInChildren<ShroomCustomizerMPB>(true);

    // Enable Apply button when ready
    if (applyButton) applyButton.enabled = true;

    // (Optional) initialize UI widgets from current live look:
    // if (previewAvatar && _liveAvatar) previewAvatar.ApplyData(_liveAvatar.GetData());
  }

  // Called by the Apply button
  public void OnApply() {
    if (!_owner || !previewAvatar) return;

    // 1) Tell the server to store & replicate this look
    _owner.SendCurrentLook(previewAvatar.GetData());

    // 2) (Nice UX) Update our local in-game model immediately, so it matches before replication lands
    if (_liveAvatar) _liveAvatar.ApplyData(previewAvatar.GetData());

    // 3) Close the overlay
    FindObjectOfType<CustomizationOverlayLoader>()?.CloseCustomization();
    // reopen the lobby UI
    FindObjectOfType<CustomizationOverlayLoader>(true)?.CloseCustomization();
  }
}
