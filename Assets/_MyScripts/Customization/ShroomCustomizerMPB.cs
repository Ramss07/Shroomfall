using UnityEngine;
using System.Collections;

public class ShroomCustomizerMPB : MonoBehaviour
{
    [Header("Renderers")]
    public Renderer body;
    public Renderer cap;
    public Renderer band;
    public Renderer mouthQuad;

    [Header("Bottles (Liquids)")]
    public Renderer leftBottle;   // previews Body color (LiquidBodyEffect)
    public Renderer rightBottle;  // previews Cap color  (LiquidCapEffect)

    [HideInInspector] public bool isBodyChanging;
    [HideInInspector] public bool isCapChanging;

    [Header("Variants")]
    public GameObject[] eyeVariants;
    public Texture2D[] mouthOptions;

    // MPBs
    MaterialPropertyBlock _mpbBody, _mpbCap, _mpbBand, _mpbMouth, _mpbBottleL, _mpbBottleR;

    // Applied (live on character)
    [SerializeField] Color _bodyColor = Color.white;
    [SerializeField] Color _capColor = Color.white;

    // Preview (live on bottles until animation event applies)
    Color _pendingBodyColor;
    Color _pendingCapColor;
    bool _hasPendingBody, _hasPendingCap;

    [SerializeField] int _eyeIndex = 0;
    [SerializeField] int _mouthIndex = 0;

    [SerializeField] Animator anim;   // assign in Inspector

    // ======= Shader property IDs (Liquid Effects) =======
    static readonly int ID_TopColor   = Shader.PropertyToID("_TopColor");
    static readonly int ID_BottomColor= Shader.PropertyToID("_BottomColor");
    static readonly int ID_FoamColor  = Shader.PropertyToID("_FoamColor");

    // ======= (for character meshes) =======
    static readonly int ID_BaseColor  = Shader.PropertyToID("_BaseColor");
    static readonly int ID_Color      = Shader.PropertyToID("_Color");

    void Update()
    {
        if (!anim) return;
        anim.SetBool("isBodyChanging", isBodyChanging);
        anim.SetBool("isCapChanging", isCapChanging);
    }

    MaterialPropertyBlock GetBlock(ref MaterialPropertyBlock b)
    {
        if (b == null) b = new MaterialPropertyBlock();
        else b.Clear();
        return b;
    }

    // Character mesh color (URP/Built-in)
    void SetColorOnRenderer(Renderer r, ref MaterialPropertyBlock block, Color c)
    {
        if (!r) return;
        var b = GetBlock(ref block);
        r.GetPropertyBlock(b);
        b.SetColor(ID_BaseColor, c);   // URP Lit
        b.SetColor(ID_Color, c);       // Built-in/Standard
        r.SetPropertyBlock(b);
    }

    // NEW: Liquid material color writer (sets all three channels)
    void SetLiquidColors(Renderer r, ref MaterialPropertyBlock block, Color c)
    {
        if (!r) return;
        var b = GetBlock(ref block);
        r.GetPropertyBlock(b);

        // Simple version: drive all three colors from 'c'.
        // (Change here if you want different tints for Top/Bottom/Foam.)
        b.SetColor(ID_TopColor,    c);
        b.SetColor(ID_BottomColor, c);
        b.SetColor(ID_FoamColor,   c);

        r.SetPropertyBlock(b);
    }

    // ---------------- PREVIEW (called by sliders) ----------------
    public void PreviewBodyColor(Color c)
    {
        _pendingBodyColor = c;
        _hasPendingBody = true;

        // bottles show preview immediately (LiquidBodyEffect)
        SetLiquidColors(leftBottle, ref _mpbBottleL, c);

        isBodyChanging = true;
        CancelInvoke(nameof(StopBodyChanging));
        Invoke(nameof(StopBodyChanging), 0.1f);
    }

    public void PreviewCapColor(Color c)
    {
        _pendingCapColor = c;
        _hasPendingCap = true;

        // LiquidCapEffect
        SetLiquidColors(rightBottle, ref _mpbBottleR, c);

        isCapChanging = true;
        CancelInvoke(nameof(StopCapChanging));
        Invoke(nameof(StopCapChanging), 0.1f);
    }

    void StopBodyChanging() => isBodyChanging = false;
    void StopCapChanging() => isCapChanging = false;

    // --------------- APPLY (called by animation event) ---------------
    public void ApplyPendingBody()
    {
        if (_hasPendingBody)
        {
            StopCoroutine(nameof(LerpBodyColor));
            StartCoroutine(LerpBodyColor(_pendingBodyColor, 0.5f));
            _hasPendingBody = false;
        }
    }

    public void ApplyPendingCap()
    {
        if (_hasPendingCap)
        {
            StopCoroutine(nameof(LerpCapColor));
            StartCoroutine(LerpCapColor(_pendingCapColor, 0.5f));
            _hasPendingCap = false;
        }
    }

    public void ApplyPendingBoth()
    {
        ApplyPendingBody();
        ApplyPendingCap();
    }

    // ---------------- LERP HELPERS ----------------
    IEnumerator LerpBodyColor(Color target, float duration = 1f)
    {
        Color start = _bodyColor;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            Color c = Color.Lerp(start, target, t / duration);
            SetColorOnRenderer(body, ref _mpbBody, c);
            yield return null;
        }

        SetBodyColor(target); // finalize
    }

    IEnumerator LerpCapColor(Color target, float duration = 0.5f)
    {
        Color start = _capColor;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            Color c = Color.Lerp(start, target, t / duration);
            SetColorOnRenderer(cap, ref _mpbCap, c);
            SetColorOnRenderer(band, ref _mpbBand, c);
            yield return null;
        }

        SetCapColor(target);
    }

    // ---------------- LIVE APPLY (character meshes) ----------------
    public void SetBodyColor(Color c)
    {
        _bodyColor = c;
        SetColorOnRenderer(body, ref _mpbBody, c);

        if (!_hasPendingBody)
            SetLiquidColors(leftBottle, ref _mpbBottleL, c);   // keep bottle preview in sync
    }

    public void SetCapColor(Color c)
    {
        _capColor = c;
        SetColorOnRenderer(cap, ref _mpbCap, c);
        SetColorOnRenderer(band, ref _mpbBand, c);

        if (!_hasPendingCap)
            SetLiquidColors(rightBottle, ref _mpbBottleR, c);
    }

    // ---------------- Eyes / Mouth ----------------
    public void SetEyesIndex(int i)
    {
        if (eyeVariants == null || eyeVariants.Length == 0) return;
        i = (i % eyeVariants.Length + eyeVariants.Length) % eyeVariants.Length;
        _eyeIndex = i;
        for (int k = 0; k < eyeVariants.Length; k++)
            if (eyeVariants[k]) eyeVariants[k].SetActive(k == i);
    }

    public void SetMouthIndex(int i)
    {
        if (!mouthQuad || mouthOptions == null || mouthOptions.Length == 0) return;
        i = (i % mouthOptions.Length + mouthOptions.Length) % mouthOptions.Length;
        _mouthIndex = i;

        var tex = mouthOptions[i];
        var b = GetBlock(ref _mpbMouth);
        mouthQuad.GetPropertyBlock(b);
        b.SetTexture("_BaseMap", tex);
        b.SetTexture("_MainTex", tex);
        b.SetColor(ID_BaseColor, Color.white);
        b.SetColor(ID_Color, Color.white);
        mouthQuad.SetPropertyBlock(b);
    }

    // ---------------- Save/Load ----------------
    public ShroomData GetData() => new ShroomData
    {
        body = _bodyColor,
        cap = _capColor,
        eyes = _eyeIndex,
        mouth = _mouthIndex
    };

    public void ApplyData(ShroomData d)
    {
        SetBodyColor(d.body);
        SetCapColor(d.cap);
        SetEyesIndex(d.eyes);
        SetMouthIndex(d.mouth);

        _pendingBodyColor = _bodyColor; _hasPendingBody = false;
        _pendingCapColor = _capColor; _hasPendingCap = false;
        SyncBottlesToApplied();
    }

    void OnEnable() => Reapply();

    public void Reapply()
    {
        SetBodyColor(_bodyColor);
        SetCapColor(_capColor);

        _pendingBodyColor = _bodyColor; _hasPendingBody = false;
        _pendingCapColor = _capColor; _hasPendingCap = false;
        SyncBottlesToApplied();

        SetEyesIndex(_eyeIndex);
        SetMouthIndex(_mouthIndex);
    }

    void SyncBottlesToApplied()
    {
        SetLiquidColors(leftBottle,  ref _mpbBottleL, _bodyColor); // LiquidBodyEffect
        SetLiquidColors(rightBottle, ref _mpbBottleR, _capColor);  // LiquidCapEffect
    }
}
