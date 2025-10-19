using Fusion;

public class VotingManager : NetworkBehaviour
{
    [Networked]
    private NetworkDictionary<PlayerRef, PortalController> PlayerVotes => default;

    // This is the RPC the manager needs -> called by the player
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerVote(PortalController newPortal, RpcInfo info = default)
    {
        // Check if the player had a previous vote
        if (PlayerVotes.TryGet(info.Source, out PortalController oldPortal) && oldPortal != null)
        {
            // remove old vote
            if (oldPortal != newPortal)
            {
                oldPortal.RemoveVote(info.Source);
            }
        }

        // add new vote
        newPortal.AddVote(info.Source);

        // Update with new vote
        PlayerVotes.Set(info.Source, newPortal);
    }
}