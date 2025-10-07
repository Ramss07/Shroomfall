using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ProximityVoiceChat : NetworkBehaviour
{
    private FusionVoiceClient voiceClient;
    private byte currentGroup = 0;
    private byte previousGroup = 255;
    public float gridCellSize = 25f;

    void Start()
    {
        voiceClient = FindFirstObjectByType<FusionVoiceClient>();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority || voiceClient == null || !voiceClient.Client.IsConnectedAndReady)
        {
            return;
        }

        byte groupX = (byte)(transform.position.x / gridCellSize);
        byte groupZ = (byte)(transform.position.z / gridCellSize);
        currentGroup = (byte)((groupX * 10 + groupZ) % 254 + 1);

        if (currentGroup != previousGroup)
        {
            voiceClient.Client.OpChangeGroups(
                groupsToRemove: new byte[] { previousGroup },
                groupsToAdd: new byte[] { currentGroup }
            );

            GetComponent<Recorder>().InterestGroup = currentGroup;
            previousGroup = currentGroup;
            Debug.Log($"Player changed voice interest group to: {currentGroup}");
        }
    }
}