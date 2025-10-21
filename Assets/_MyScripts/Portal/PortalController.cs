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
    private int _lastKnownPlayerCount = -1;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _lastKnownPlayerCount = SafePlayerCount();
        UpdateVoteUI();
        HidePrompt();
    }

    public override void Render()
    {
        // Update when VoteCount changes
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(VoteCount))
                UpdateVoteUI();
        }

        // Also update when player count changes (join/leave)
        int currentCount = SafePlayerCount();
        if (currentCount != _lastKnownPlayerCount)
        {
            _lastKnownPlayerCount = currentCount;
            UpdateVoteUI();
        }
    }

    public void ShowPrompt()
    {
        if (promptCanvasGroup != null) promptCanvasGroup.alpha = 1f;
    }

    public void HidePrompt()
    {
        if (promptCanvasGroup != null) promptCanvasGroup.alpha = 0f;
    }

    private void UpdateVoteUI()
    {
        if (voteText == null) return;
        voteText.text = $"({VoteCount}/{SafePlayerCount()})";
    }

    private int SafePlayerCount()
    {
        if (Runner == null) return 0;

        // ActivePlayers is authoritative for current players and is IEnumerable<PlayerRef>
        int active = 0;
        var enumerable = Runner.ActivePlayers;
        if (enumerable != null)
        {
            foreach (var _ in enumerable)
                active++;
            return active;
        }

        // Fallback if needed
        return (Runner.SessionInfo != null) ? Runner.SessionInfo.PlayerCount : 0;
    }


    public void AddVote(PlayerRef player)
    {
        if (!Runner.IsServer) return;

        if (!PlayersWhoVoted.ContainsKey(player))
        {
            PlayersWhoVoted.Add(player, true);
            VoteCount++;
            CheckVoteThreshold();
        }
    }

    public void RemoveVote(PlayerRef player)
    {
        if (!Runner.IsServer) return;

        if (PlayersWhoVoted.ContainsKey(player))
        {
            PlayersWhoVoted.Remove(player);
            VoteCount = Mathf.Max(0, VoteCount - 1);
            // UI will refresh via ChangeDetector + Render polling
        }
    }

    private void CheckVoteThreshold()
    {
        int playerCount = SafePlayerCount();
        if (playerCount <= 0) return;

        int requiredVotes = (playerCount / 2) + 1;
        if (VoteCount >= requiredVotes)
        {
            Debug.Log($"Vote passed! Teleporting to {TargetSceneName}");
            Runner.LoadScene(TargetSceneName, LoadSceneMode.Single);
        }
    }
}
