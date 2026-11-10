namespace CS2Panel;

/// <summary>A sound library entry: name -> what to play on the client with the <c>play</c> command.</summary>
public sealed class SoundEntry
{
    public string Play { get; set; } = "";
    public string Title { get; set; } = "";
}

/// <summary>Team look: display name, logo (a team code from the game files) and a country flag.</summary>
public sealed class TeamLook
{
    public string Name { get; set; } = "";
    public string Logo { get; set; } = "";
    public string Flag { get; set; } = "";
}

/// <summary>Player look: clan tag and an optional in-server nickname.</summary>
public sealed class PlayerLook
{
    public string Tag { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class Identity
{
    public Dictionary<string, TeamLook> Teams { get; set; } = new();
    public Dictionary<string, PlayerLook> Players { get; set; } = new();
}
