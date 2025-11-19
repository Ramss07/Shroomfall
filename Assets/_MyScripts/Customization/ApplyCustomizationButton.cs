// ApplyCustomizationButton.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class ApplyCustomizationButton : MonoBehaviour {
    [SerializeField] ShroomCustomizerMPB customizer;   // preview shroom
    [SerializeField] string gameSceneName = "GameScene";

    public void ApplyAndGo() {
        var d = customizer.GetData();
        CharacterSelection.Data = d;
        CharacterSelection.HasData = true;
        Debug.Log($"[Apply] Saved: body={d.body} cap={d.cap} eyes={d.eyes} mouth={d.mouth}");
        SceneManager.LoadScene(gameSceneName);
    }
}
