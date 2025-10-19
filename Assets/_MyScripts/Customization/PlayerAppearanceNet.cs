using UnityEngine;
using Fusion;

public class PlayerAppearanceNet : NetworkBehaviour
{
  [Header("Assign the model's customizer on the prefab")]
  [SerializeField] ShroomCustomizerMPB avatar;

  // --- Networked state stored on the server (state authority) ---
  [Networked] public byte EyeIndex { get; set; }
  [Networked] public byte MouthIndex { get; set; }
  [Networked] public uint BodyRGBA { get; set; }
  [Networked] public uint CapRGBA { get; set; }

  // local caches so we can detect changes every Render()
  byte _lastEye, _lastMouth;
  uint _lastBody, _lastCap;

  void Awake()
  {
    if (!avatar) avatar = GetComponentInChildren<ShroomCustomizerMPB>(true);
  }

  public override void Spawned()
  {
    if (!avatar) avatar = GetComponentInChildren<ShroomCustomizerMPB>(true);

    // IMPORTANT: do NOT push inspector MPBs here; it overrides defaults/materials.
    // if (avatar) avatar.Reapply();

    if (Object.HasStateAuthority)
    {
      // Seed safe defaults for everyone (prevents black spawns)
      if (BodyRGBA == 0 && CapRGBA == 0)
      {
        var d = ShroomDefaults.Default();  // always use code defaults (now red)
        EyeIndex  = (byte)Mathf.Clamp(d.eyes,  0, 255);
        MouthIndex= (byte)Mathf.Clamp(d.mouth, 0, 255);
        BodyRGBA  = Pack(FixAlpha(d.body));
        CapRGBA   = Pack(FixAlpha(d.cap));
      }

      // Show immediately on host
      ApplyFromNetwork();
      SnapCaches();
    }

    if (Object.HasInputAuthority)
    {
      // Prefer saved selection; else fall back to code defaults (red)
      var d = TryGetCharacterSelection(out var sel) ? sel : ShroomDefaults.Default();
      RPC_SetLookServer(
        (byte)Mathf.Clamp(d.eyes,  0, 255),
        (byte)Mathf.Clamp(d.mouth, 0, 255),
        Pack(FixAlpha(d.body)),
        Pack(FixAlpha(d.cap))
      );
    }
    else
    {
      // For proxies / late join, apply whatever replicated values we have
      ApplyFromNetwork();
      SnapCaches();
    }
  }

  public override void Render()
  {
    // Apply when replicated values change (covers late join + runtime edits)
    if (_lastEye != EyeIndex ||
        _lastMouth != MouthIndex ||
        _lastBody != BodyRGBA ||
        _lastCap != CapRGBA)
    {
      ApplyFromNetwork();
      SnapCaches();
    }
  }

  [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
  void RPC_SetLookServer(byte eye, byte mouth, uint body, uint cap)
  {
    var bc = FixAlpha(Unpack(body));
    var cc = FixAlpha(Unpack(cap));
    EyeIndex  = eye;
    MouthIndex= mouth;
    BodyRGBA  = Pack(bc);
    CapRGBA   = Pack(cc);
  }

  void ApplyFromNetwork()
  {
    if (!avatar) return;

    // Apply per field so partial initialization still shows up
    if (BodyRGBA != 0)
      avatar.SetBodyColor(Unpack(BodyRGBA));

    if (CapRGBA != 0)
      avatar.SetCapColor(Unpack(CapRGBA));

    avatar.SetEyesIndex(EyeIndex);
    avatar.SetMouthIndex(MouthIndex);
  }

  void SnapCaches()
  {
    _lastEye  = EyeIndex;
    _lastMouth= MouthIndex;
    _lastBody = BodyRGBA;
    _lastCap  = CapRGBA;
  }

  // pack/unpack helpers
  static uint Pack(Color c) { var k = (Color32)c; return (uint)(k.a << 24 | k.r << 16 | k.g << 8 | k.b); }
  static Color Unpack(uint v) { return new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, (byte)(v >> 24)); }

  // Call this from your Apply button (via your UI controller)
  public void SendCurrentLook(ShroomData d)
  {
    if (!Object.HasInputAuthority) return;
    RPC_SetLookServer(
      (byte)d.eyes, (byte)d.mouth,
      Pack(FixAlpha(d.body)),
      Pack(FixAlpha(d.cap))
    );
  }

  // --- helpers / defaults ---

  static Color FixAlpha(Color c)
  {
    if (c.a <= 0f) c.a = 1f;
    return c;
  }

  static bool TryGetCharacterSelection(out ShroomData d)
  {
    d = default;
    try
    {
      var x = CharacterSelection.Data;
      // Guard against “default struct” (all zeros)
      if (x.body == default && x.cap == default && x.eyes == 0 && x.mouth == 0)
        return false;
      d = x;
      return true;
    }
    catch { return false; }
  }

  static class ShroomDefaults
  {
    public static ShroomData Default() => new ShroomData {
      body = Color.HSVToRGB(0f, 0.25f, 1f),   // default body color (red)
      cap  = Color.red,   // default cap color  (red)
      eyes = 0,
      mouth= 0
    };
  }
}
