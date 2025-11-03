using Fusion;
using UnityEngine;

public class VotingManager : NetworkBehaviour, IPlayerLeft
{
    // Per-player vote
    [Networked] private NetworkDictionary<PlayerRef, PortalController> PlayerVotes => default;

    // ===== Debug =====
    public override void Spawned()
    {
        Debug.Log($"[VM] Spawned sa={Object.HasStateAuthority}");
    }

    // ===== Centralized handler used by both paths =====
    private void HandlePlayerVote(PlayerRef player, PortalController newPortal)
    {
        if (!Object.HasStateAuthority) return;
        if (newPortal == null) return;

        // If player had a different vote, remove it from the old portal
        if (PlayerVotes.TryGet(player, out var oldPortal) && oldPortal != null && oldPortal != newPortal)
        {
            oldPortal.RemoveVote(player);   // your PortalController should decrement internal count
        }

        // Add the new vote and store mapping
        newPortal.AddVote(player);
        PlayerVotes.Set(player, newPortal);
    }

    // ===== Host-only direct path (no RPC when we're StateAuthority) =====
    public void Server_SetPlayerVote(PortalController newPortal, PlayerRef player)
    {
        HandlePlayerVote(player, newPortal);
    }

    // ===== Client -> Host RPC path =====
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerVote(PortalController newPortal, RpcInfo info = default)
    {
        HandlePlayerVote(info.Source, newPortal);
    }

    // ===== IPlayerLeft callback – fires on the object with State Authority =====
    public void PlayerLeft(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        if (PlayerVotes.TryGet(player, out var portal) && portal != null)
        {
            portal.RemoveVote(player);
            PlayerVotes.Remove(player);
        }
    }
}
