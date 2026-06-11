namespace StopMe.Core.Models;

public class CategoryDomain
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string CategoryName { get; set; } = string.Empty;

    public string Domain { get; set; } = string.Empty;

    public string Source { get; set; } = "builtin";

    public DateTime CreatedAtUtc { get; set; }
}
