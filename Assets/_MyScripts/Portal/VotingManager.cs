using Fusion;
using UnityEngine;

public class VotingManager : NetworkBehaviour, IPlayerLeft
{
    // Which portal each player voted for
    [Networked] private NetworkDictionary<PlayerRef, PortalController> PlayerVotes => default;
    

    // Called by the local player to set/change their vote
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerVote(PortalController newPortal, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;

        if (PlayerVotes.TryGet(info.Source, out var oldPortal) && oldPortal != null && oldPortal != newPortal)
            oldPortal.RemoveVote(info.Source);

        newPortal.AddVote(info.Source);
        PlayerVotes.Set(info.Source, newPortal);
    }

    // IPlayerLeft callback – fires on the object with State Authority
    public void PlayerLeft(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        if (PlayerVotes.TryGet(player, out var portal) && portal != null)
        {
            portal.RemoveVote(player);   // decrements VoteCount + removes from PlayersWhoVoted
            PlayerVotes.Remove(player);  // clear mapping
        }
    }
}
