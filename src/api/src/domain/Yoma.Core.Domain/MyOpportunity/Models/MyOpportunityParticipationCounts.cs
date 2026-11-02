namespace Yoma.Core.Domain.MyOpportunity.Models
{
  public sealed class MyOpportunityParticipationCounts
  {
    public Guid OpportunityId { get; set; }

    public int Completed { get; set; }

    /// <summary>
    /// People who followed the external link or submitted for verification, and have not completed. Each person counts once.
    /// </summary>
    public int Started { get; set; }
  }
}
