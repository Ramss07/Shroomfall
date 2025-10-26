//STILL needs optimization (Vector2 is 2 floats = 8 bytes, instead could send 2 bytes)
using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector2 movementInput;
    public NetworkBool isJumpPressed;
    public NetworkBool isAwakeButtonPressed;
    public Vector2 lookDelta;
    public float aimYawDeg;
    public NetworkBool isGrabPressed;
    public NetworkBool isSprinting;
    public bool isLeftGrabPressed;
    public bool isRightGrabPressed;
}