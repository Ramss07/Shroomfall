using UnityEngine;
using UnityEngine.UI;

public class LocalStaminaHUD : MonoBehaviour {
    [SerializeField] Slider slider;

    void Awake() {
        if (slider) { slider.minValue = 0f; slider.maxValue = 1f; }
    }

    void Update() {
        var p = NetworkPlayer.Local;
        if (!p || !slider) return;
        slider.value = p.StaminaPercent;
    }
}
