using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Helpers
{
  /// <summary>
  /// "Young people who completed this usually go on to ...", from the same completion records as the outcome figures. For each
  /// opportunity, each completer's next completion (the earliest one strictly after it) is counted, and the most common ones are kept.
  /// Like every outcome figure it describes what happened next, not what the step caused, and it is hidden below the minimum group
  /// size of completers.
  /// </summary>
  public static class PathwayNextSteps
  {
    #region Public Members
    /// <param name="completions">Completed verifications of any opportunity type.</param>
    /// <param name="titleOf">The title of an opportunity youth can open today, null for one that is no longer listed (never shown).</param>
    /// <param name="minimumGroupSize">Opportunities with fewer completers get no entry.</param>
    /// <param name="max">Next steps kept per opportunity.</param>
    public static Dictionary<Guid, List<PathwayNextStep>> Compute(IEnumerable<PathwayActivity> completions, Func<Guid, string?> titleOf, int minimumGroupSize, int max)
    {
      ArgumentNullException.ThrowIfNull(completions, nameof(completions));
      ArgumentNullException.ThrowIfNull(titleOf, nameof(titleOf));

      var min = Math.Max(minimumGroupSize, 1);
      var result = new Dictionary<Guid, List<PathwayNextStep>>();
      if (max <= 0) return result;

      // each youth's completions, earliest first, one per opportunity
      var byUser = completions.GroupBy(o => (o.UserId, o.OpportunityId)).Select(o => (o.Key.UserId, o.Key.OpportunityId, Date: o.Min(a => a.Date)))
        .GroupBy(o => o.UserId)
        .Select(o => o.OrderBy(a => a.Date).ThenBy(a => a.OpportunityId).ToList())
        .ToList();

      var completers = new Dictionary<Guid, int>();
      var next = new Dictionary<Guid, Dictionary<Guid, int>>();

      foreach (var items in byUser)
      {
        for (var index = 0; index < items.Count; index++)
        {
          var current = items[index];
          completers[current.OpportunityId] = completers.GetValueOrDefault(current.OpportunityId) + 1;

          // completed at the same moment is not "after"
          var following = items.Skip(index + 1).FirstOrDefault(o => o.Date > current.Date);
          if (following.OpportunityId == Guid.Empty) continue;

          if (!next.TryGetValue(current.OpportunityId, out var counts)) next[current.OpportunityId] = counts = [];
          counts[following.OpportunityId] = counts.GetValueOrDefault(following.OpportunityId) + 1;
        }
      }

      foreach (var (opportunityId, counts) in next.OrderBy(o => o.Key))
      {
        var total = completers[opportunityId];
        if (total < min) continue;

        var steps = counts
          .Select(o => (o.Key, Count: o.Value, Title: titleOf(o.Key)))
          .Where(o => !string.IsNullOrWhiteSpace(o.Title))
          .OrderByDescending(o => o.Count).ThenBy(o => o.Key)
          .Take(max)
          .Select(o => new PathwayNextStep
          {
            OpportunityId = o.Key,
            Title = o.Title!,
            Share = Math.Round((decimal)o.Count / total, 4, MidpointRounding.AwayFromZero),
            Count = o.Count
          })
          .ToList();

        if (steps.Count > 0) result[opportunityId] = steps;
      }

      return result;
    }
    #endregion
  }
}
