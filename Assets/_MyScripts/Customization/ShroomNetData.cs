using UnityEngine;
using Fusion;

[System.Serializable]
public struct ShroomNetData : INetworkStruct {
    public Color32 body;
    public Color32 cap;
    public int eyes;
    public int mouth;
}

public static class ShroomNet {
    public static ShroomNetData ToNet(ShroomData d) => new ShroomNetData {
        body = (Color32)d.body,
        cap  = (Color32)d.cap,
        eyes = d.eyes,
        mouth= d.mouth
    };

    public static ShroomData ToLocal(ShroomNetData n) => new ShroomData {
        body = (Color)n.body,
        cap  = (Color)n.cap,
        eyes = n.eyes,
        mouth= n.mouth
    };
}
