using UnityEngine;
using UnityEngine.UI;

public class HueSlider : MonoBehaviour
{
    public Slider slider;   // UI Slider (0..1)
    public Image bgImage;  // background Image to show the hue gradient

    public System.Action<float> onHueChanged;  // hue in [0..1]

    void Awake()
    {
        if (!slider) slider = GetComponentInChildren<Slider>();
        if (slider)
        {
            slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(OnChanged);
        }
        ApplyGradientToBackground();
        OnChanged(slider ? slider.value : 0f);
    }

    void ApplyGradientToBackground()
    {
        if (!bgImage) return;
        const int W = 512, H = 1;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < W; x++)
        {
            float h = x / (W - 1f);
            tex.SetPixel(x, 0, Color.HSVToRGB(h, 1f, 1f));
        }
        tex.Apply(false, false);

        var sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 1f);
        bgImage.sprite = sp;
        bgImage.type = Image.Type.Sliced;
        bgImage.pixelsPerUnitMultiplier = 1f;
        bgImage.color = Color.white;
    }

    void OnChanged(float h)
    {
        h = Mathf.Repeat(h, 1f);
        onHueChanged?.Invoke(h);
    }

    public void SetHue(float h, bool notify = true)
    {
        if (!slider) return;
        if (notify) slider.value = h;
        else slider.SetValueWithoutNotify(h);
        OnChanged(slider.value);
    }
}
