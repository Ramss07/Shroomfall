using UnityEngine;
using UnityEngine.UI;

public class CustomizerUI_Sliders : MonoBehaviour
{
    [Header("Target character")]
    public ShroomCustomizerMPB shroom;

    [Header("Hue sliders")]
    public HueSlider bodyHue;   // pastel body
    public HueSlider capHue;    // full-sat cap

    [Range(0f, 1f)] public float bodySaturation = 0.35f;

    [Header("Selections (runtime)")]
    public int eyesIdx = 0;
    public int mouthIdx = 0;

    void Awake()
    {
        if (bodyHue)
            bodyHue.onHueChanged += h =>
                shroom.PreviewBodyColor(Color.HSVToRGB(h, bodySaturation, 1f));

        if (capHue)
            capHue.onHueChanged += h =>
                shroom.PreviewCapColor(Color.HSVToRGB(h, 1f, 1f));

        ApplyEyes(eyesIdx);
        ApplyMouth(mouthIdx);
    }

    // ---------- Eyes ----------
    public void NextEyes() => ShiftEyes(+1);
    public void PrevEyes() => ShiftEyes(-1);

    void ShiftEyes(int delta)
    {
        int len = shroom.eyeVariants != null ? shroom.eyeVariants.Length : 0;
        if (len == 0) return;
        eyesIdx = Loop(eyesIdx + delta, len);
        ApplyEyes(eyesIdx);
    }

    void ApplyEyes(int i) => shroom.SetEyesIndex(i);

    // ---------- Mouth ----------
    public void NextMouth() => ShiftMouth(+1);
    public void PrevMouth() => ShiftMouth(-1);

    void ShiftMouth(int delta)
    {
        int len = shroom.mouthOptions != null ? shroom.mouthOptions.Length : 0;
        if (len == 0) return;
        mouthIdx = Loop(mouthIdx + delta, len);
        ApplyMouth(mouthIdx);
    }

    void ApplyMouth(int i) => shroom.SetMouthIndex(i);

    // ---------- Helpers ----------
    public void Randomize()
    {
        float hb = Random.value, hc = Random.value;
        if (bodyHue) bodyHue.SetHue(hb);
        if (capHue) capHue.SetHue(hc);

        if (shroom.eyeVariants != null && shroom.eyeVariants.Length > 0)
            ApplyEyes(Random.Range(0, shroom.eyeVariants.Length));
        if (shroom.mouthOptions != null && shroom.mouthOptions.Length > 0)
            ApplyMouth(Random.Range(0, shroom.mouthOptions.Length));
    }

    int Loop(int v, int len) => (v % len + len) % len;
}
