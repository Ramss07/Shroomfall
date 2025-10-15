using UnityEngine;
using System.Collections;

public class ShroomCustomizerMPB : MonoBehaviour
{
    [Header("Renderers")]
    public Renderer body;
    public Renderer cap;
    public Renderer band;
    public Renderer mouthQuad;

    [Header("Bottles")]
    public Renderer leftBottle;   // previews Body color
    public Renderer rightBottle;  // previews Cap color

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

    void SetColorOnRenderer(Renderer r, ref MaterialPropertyBlock block, Color c)
    {
        if (!r) return;
        var b = GetBlock(ref block);
        r.GetPropertyBlock(b);
        b.SetColor("_BaseColor", c);   // URP
        b.SetColor("_Color", c);       // Built-in (Legacy/Standard)
        r.SetPropertyBlock(b);
    }

    // ---------------- PREVIEW (called by sliders) ----------------
    public void PreviewBodyColor(Color c)
    {
        _pendingBodyColor = c;
        _hasPendingBody = true;

        // bottles show preview immediately
        SetColorOnRenderer(leftBottle, ref _mpbBottleL, c);

        isBodyChanging = true;
        CancelInvoke(nameof(StopBodyChanging));
        Invoke(nameof(StopBodyChanging), 0.1f);
    }

    public void PreviewCapColor(Color c)
    {
        _pendingCapColor = c;
        _hasPendingCap = true;

        SetColorOnRenderer(rightBottle, ref _mpbBottleR, c);

        isCapChanging = true;
        CancelInvoke(nameof(StopCapChanging));
        Invoke(nameof(StopCapChanging), 0.1f);
    }

    void StopBodyChanging() => isBodyChanging = false;
    void StopCapChanging() => isCapChanging = false;

    // --------------- APPLY (called by animation event) ---------------
    // Hook these up as Animation Events on your drink clips
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
            SetColorOnRenderer(leftBottle, ref _mpbBottleL, c);
    }

    public void SetCapColor(Color c)
    {
        _capColor = c;
        SetColorOnRenderer(cap, ref _mpbCap, c);
        SetColorOnRenderer(band, ref _mpbBand, c);
        if (!_hasPendingCap)
            SetColorOnRenderer(rightBottle, ref _mpbBottleR, c);
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
        b.SetColor("_BaseColor", Color.white);
        b.SetColor("_Color", Color.white);
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
        SetColorOnRenderer(leftBottle, ref _mpbBottleL, _bodyColor);
        SetColorOnRenderer(rightBottle, ref _mpbBottleR, _capColor);
    }
}
