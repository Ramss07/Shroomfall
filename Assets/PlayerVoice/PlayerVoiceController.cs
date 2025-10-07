using Fusion;
using Photon.Voice.Unity;
using UnityEngine;

public class PlayerVoiceController : NetworkBehaviour
{
    [SerializeField]
    private Recorder voiceRecorder;

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            // If it's the local player, enable the recorder to transmit audio
            voiceRecorder.TransmitEnabled = true;
        }
        else
        {
            // If it's a remote player, disable it to prevent echo
            voiceRecorder.TransmitEnabled = false;
        }
    }
}