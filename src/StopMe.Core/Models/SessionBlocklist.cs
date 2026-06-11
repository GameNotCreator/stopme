namespace StopMe.Core.Models;

public class SessionBlocklist
{
    public Guid FocusSessionId { get; set; }

    public Guid BlocklistId { get; set; }

    public FocusSession? FocusSession { get; set; }

    public Blocklist? Blocklist { get; set; }
}
