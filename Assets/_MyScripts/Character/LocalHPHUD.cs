using UnityEngine;
using UnityEngine.UI;

public class LocalHPHUD : MonoBehaviour {
    [SerializeField] Slider slider;

    void Update() {
        var p = NetworkPlayer.Local;
        if (!p) return;
        slider.value = p.HpPercent;
    }
}
