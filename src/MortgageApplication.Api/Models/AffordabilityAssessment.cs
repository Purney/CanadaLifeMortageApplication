namespace MortgageApplication.Api.Models;

public sealed class AffordabilityAssessment
{
    public long Sequence { get; set; }
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public long CreatedAtUtcTicks { get; set; }
    public string SnapshotJson { get; set; } = "";
}
