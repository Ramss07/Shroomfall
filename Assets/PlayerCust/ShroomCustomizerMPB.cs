using UnityEngine;

public class ShroomCustomizerMPB : MonoBehaviour
{


    [Header("Renderers")]
    public Renderer body;
    public Renderer cap;
    public Renderer band;
    public Renderer mouthQuad;

    [Header("Variants")]
    public GameObject[] eyeVariants;
    public Texture2D[] mouthOptions;

    // one block per renderer (reused)
    MaterialPropertyBlock _mpbBody, _mpbCap, _mpbBand, _mpbMouth;

    // >>> add: remember current picks
    [SerializeField] Color _bodyColor = Color.white;
    [SerializeField] Color _capColor = Color.white;
    [SerializeField] int _eyeIndex = 0;
    [SerializeField] int _mouthIndex = 0;

    MaterialPropertyBlock GetBlock(ref MaterialPropertyBlock b)
    {
        if (b == null) b = new MaterialPropertyBlock();
        else b.Clear();
        return b;
    }

    // ---- Colors ----
    public void SetBodyColor(Color c)
    {
        _bodyColor = c; // <<< store
        if (!body) return;
        var b = GetBlock(ref _mpbBody);
        body.GetPropertyBlock(b);
        b.SetColor("_BaseColor", c);   // URP
        b.SetColor("_Color", c);       // Built-in fallback
        body.SetPropertyBlock(b);
    }

    public void SetCapColor(Color c)
    {
        _capColor = c; // <<< store
        if (cap)
        {
            var b = GetBlock(ref _mpbCap);
            cap.GetPropertyBlock(b);
            b.SetColor("_BaseColor", c);
            b.SetColor("_Color", c);
            cap.SetPropertyBlock(b);
        }
        if (band)
        {
            var b = GetBlock(ref _mpbBand);
            band.GetPropertyBlock(b);
            b.SetColor("_BaseColor", c);
            b.SetColor("_Color", c);
            band.SetPropertyBlock(b);
        }
    }

    // ---- Eyes ----
    public void SetEyesIndex(int i)
    {
        if (eyeVariants == null || eyeVariants.Length == 0) return;
        i = (i % eyeVariants.Length + eyeVariants.Length) % eyeVariants.Length;
        _eyeIndex = i; // <<< store
        for (int k = 0; k < eyeVariants.Length; k++)
            if (eyeVariants[k]) eyeVariants[k].SetActive(k == i);
    }

    // ---- Mouth (texture swap on the mouth plane) ----
    public void SetMouthIndex(int i)
    {
        if (!mouthQuad || mouthOptions == null || mouthOptions.Length == 0) return;
        i = (i % mouthOptions.Length + mouthOptions.Length) % mouthOptions.Length;
        _mouthIndex = i; // <<< store

        var tex = mouthOptions[i];
        var b = GetBlock(ref _mpbMouth);
        mouthQuad.GetPropertyBlock(b);
        b.SetTexture("_BaseMap", tex); // URP
        b.SetTexture("_MainTex", tex); // Built-in fallback
        // ensure visible (white, full alpha)
        b.SetColor("_BaseColor", Color.white);
        b.SetColor("_Color", Color.white);
        mouthQuad.SetPropertyBlock(b);
    }

    // >>> add: export current picks
    public ShroomData GetData() => new ShroomData
    {
        body = _bodyColor,
        cap = _capColor,
        eyes = _eyeIndex,
        mouth = _mouthIndex
    };

    // >>> add: apply a saved snapshot
    public void ApplyData(ShroomData d)
    {
        SetBodyColor(d.body);
        SetCapColor(d.cap);
        SetEyesIndex(d.eyes);
        SetMouthIndex(d.mouth);
    }
    
    void OnEnable() { Reapply(); }

    public void Reapply() 
    {
    SetBodyColor(_bodyColor);
    SetCapColor(_capColor);
    SetEyesIndex(_eyeIndex);
    SetMouthIndex(_mouthIndex);
    }

}
