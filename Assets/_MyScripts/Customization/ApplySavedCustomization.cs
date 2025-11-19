// ApplySavedCustomization.cs
using UnityEngine;

public class ApplySavedCustomization : MonoBehaviour {
    [SerializeField] ShroomCustomizerMPB customizer;

    void Awake() {
        if (!customizer) customizer = GetComponent<ShroomCustomizerMPB>();
        if (customizer && CharacterSelection.HasData) {
            customizer.ApplyData(CharacterSelection.Data);
            var d = CharacterSelection.Data;
            Debug.Log($"[Spawn] Applied: body={d.body} cap={d.cap} eyes={d.eyes} mouth={d.mouth}");
        } else {
            Debug.Log("[Spawn] No saved data or no customizer.");
        }
    }
}
