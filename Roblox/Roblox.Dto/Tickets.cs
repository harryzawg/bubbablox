namespace Roblox.Dto.Tickets;

public class TranscriptRequest
{
	public string user { get; set; }
	public string message { get; set; }
	public string discordId { get; set; }
}

public class TicketTranscriptRequest
{
	public string name { get; set; }
	public Dictionary<string, TranscriptRequest> data { get; set; }
}

public class UserDiscord
{
	public long userId { get; set; }
	public string username { get; set; }
	public DateTime created { get; set; }
	public DateTime lastOnline { get; set; }
}

public class Transcript
{
	public long id { get; set; }
	public long ticket_id { get; set; }
	public long user_id { get; set; }
	public string discord_id { get; set; }
	public string message { get; set; }
	public DateTime created_at { get; set; }
	public DateTime updated_at { get; set; }
	public string username { get; set; }
}

public class TranscriptTicket
{
	public long ticket_id { get; set; }
	public string name { get; set; }
	public DateTime last_message { get; set; }
	public long message_count { get; set; }
}
