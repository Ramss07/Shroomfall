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
    // Make sure the avatar pushes its inspector defaults into the renderers
    if (avatar) avatar.Reapply();

    if (Object.HasInputAuthority)
    {
      // This is *my* pawn. Decide what my look is (from your customization save),
      // or fall back to the prefab's current inspector values.
      ShroomData d = CharacterSelectionExists()
        ? CharacterSelection.Data
        : (avatar ? avatar.GetData() : default);

      // Send to the server so it becomes authoritative and replicates to everyone.
      RPC_SetLookServer(
        (byte)d.eyes, (byte)d.mouth, Pack(d.body), Pack(d.cap)
      );
    }
    else
    {
      // For proxies / late join, apply whatever network already has (may be defaults until RPC arrives)
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
    // Server stores — this is what makes it sync to *all* clients and late joiners
    EyeIndex = eye; MouthIndex = mouth; BodyRGBA = body; CapRGBA = cap;
  }

  void ApplyFromNetwork()
  {
    if (!avatar) return;
    avatar.SetEyesIndex(EyeIndex);
    avatar.SetMouthIndex(MouthIndex);
    avatar.SetBodyColor(Unpack(BodyRGBA));
    avatar.SetCapColor(Unpack(CapRGBA));
  }

  void SnapCaches() { _lastEye = EyeIndex; _lastMouth = MouthIndex; _lastBody = BodyRGBA; _lastCap = CapRGBA; }

  // pack/unpack helpers
  static uint Pack(Color c) { var k = (Color32)c; return (uint)(k.a << 24 | k.r << 16 | k.g << 8 | k.b); }
  static Color Unpack(uint v) { return new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, (byte)(v >> 24)); }

  // replace this with your own check if CharacterSelection is in another assembly
  static bool CharacterSelectionExists()
  {
    // quick null-safe check: if the type or data doesn't exist, fall back to prefab data
    try { var _ = CharacterSelection.Data; return true; } catch { return false; }
  }
  
  // Call this from your Apply button (via your UI controller)
  public void SendCurrentLook(ShroomData d)
  {
    if (Object.HasInputAuthority)
      RPC_SetLookServer((byte)d.eyes, (byte)d.mouth, Pack(d.body), Pack(d.cap));
  }

}
