using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalController : NetworkBehaviour
{
    [Header("Portal Settings")]
    public string TargetSceneName;

    [Header("UI Feedback")]
    [SerializeField] private CanvasGroup promptCanvasGroup;
    [SerializeField] private TextMeshProUGUI voteText;

    [Networked] public int VoteCount { get; set; }
    [Networked] public NetworkDictionary<PlayerRef, NetworkBool> PlayersWhoVoted => default;

    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        UpdateVoteUI();
        HidePrompt();
    }
    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(VoteCount)) UpdateVoteUI();
        }
    }
    public void ShowPrompt() { promptCanvasGroup.alpha = 1; }
    public void HidePrompt() { promptCanvasGroup.alpha = 0; }
    private void UpdateVoteUI()
    {
        if (voteText != null)
        {
            int playerCount = Runner != null && Runner.SessionInfo != null ? Runner.SessionInfo.PlayerCount : 0;
            voteText.text = $"({VoteCount}/{playerCount})";
        }
    }


    
    public void AddVote(PlayerRef player)
    {
        if (!Runner.IsServer) return; // Only the server can change votes

        if (!PlayersWhoVoted.ContainsKey(player))
        {
            PlayersWhoVoted.Add(player, true);
            VoteCount++;
            CheckVoteThreshold(); // Check if should teleport
        }
    }

   
    public void RemoveVote(PlayerRef player)
    {
        if (!Runner.IsServer) return;

        if (PlayersWhoVoted.ContainsKey(player))
        {
            PlayersWhoVoted.Remove(player);
            VoteCount--;
        }
    }

    private void CheckVoteThreshold()
    {
        int playerCount = Runner.SessionInfo.PlayerCount;
        int requiredVotes = (playerCount / 2) + 1;

        if (VoteCount >= requiredVotes)
        {
            Debug.Log($"Vote passed! Teleporting to {TargetSceneName}");
            Runner.LoadScene(TargetSceneName, LoadSceneMode.Single);
        }
    }
}