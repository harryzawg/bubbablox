namespace Roblox.Dto.Users;

public class BadgeEntry
{
    public int id { get; set; }
    public string name { get; set; } = "Unknown Badge";
    public string description { get; set; } = "Unknown Badge";
}

public class GameBadgeEntry
{
    public int id { get; set; }
    public string name { get; set; } = "Badge";
}

public class GamePassEntry
{
    public int id { get; set; }
    public string name { get; set; } = "Game Pass";
}