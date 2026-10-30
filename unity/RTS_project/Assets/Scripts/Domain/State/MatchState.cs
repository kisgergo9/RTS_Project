using System;

public enum MatchStatus
{
    Waiting,
    Running,
    Finished
}

public sealed class MatchState
{
    public MatchStatus Status { get; private set; } =
        MatchStatus.Waiting;

    public int? WinnerPlayerId { get; private set; }

    public bool IsRunning => Status == MatchStatus.Running;
    public bool IsFinished => Status == MatchStatus.Finished;

    public void Start()
    {
        if (Status != MatchStatus.Waiting)
        {
            throw new InvalidOperationException(
                "A mérkõzés csak Waiting állapotból indítható.");
        }

        Status = MatchStatus.Running;
    }

    public void Finish(int winnerPlayerId)
    {
        if (Status != MatchStatus.Running)
        {
            throw new InvalidOperationException(
                "Csak folyamatban lévõ mérkõzés fejezhetõ be.");
        }

        WinnerPlayerId = winnerPlayerId;
        Status = MatchStatus.Finished;
    }
}