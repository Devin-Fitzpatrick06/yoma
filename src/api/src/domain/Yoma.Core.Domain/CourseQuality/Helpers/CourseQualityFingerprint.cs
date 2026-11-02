using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Hash of what a reviewer actually judged: the listing's learner-relevant content. Status, visibility, counts and
  /// rewards are left out on purpose, so operational updates do not void a decision. ListingLive covers those.
  /// </summary>
  public static class CourseQualityFingerprint
  {
    #region Class Variables
    // bump when the field list changes; stored decisions then lapse once rather than matching by accident
    private const int Version = 1;
    #endregion

    #region Public Members
    public static string Compute(Opportunity.Models.Opportunity opportunity)
    {
      ArgumentNullException.ThrowIfNull(opportunity, nameof(opportunity));

      // serialized as an array, so a value cannot run into its neighbour
      var content = JsonConvert.SerializeObject(new object?[]
      {
        Version,
        opportunity.Type.ToString(),
        Text(opportunity.Title),
        Text(opportunity.Description),
        Text(opportunity.Summary),
        Text(opportunity.Instructions),
        Text(opportunity.URL),
        Text(opportunity.Provider),
        Text(opportunity.ProviderURL),
        opportunity.DateStart.UtcTicks,
        opportunity.DateEnd?.UtcTicks,
        opportunity.CommitmentInterval?.ToString(),
        opportunity.CommitmentIntervalCount,
        opportunity.EngagementType?.ToString(),
        opportunity.AgeFrom,
        opportunity.AgeTo,
        Ids(opportunity.Skills?.Select(o => o.Id)),
        Ids(opportunity.Languages?.Select(o => o.Id)),
        Ids(opportunity.Countries?.Select(o => o.Id)),
        Ids(opportunity.Categories?.Select(o => o.Id))
      });

      return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
    #endregion

    #region Private Members
    private static string? Text(string? value) => value?.Trim();

    private static List<string> Ids(IEnumerable<Guid>? values) => values?.Select(o => o.ToString()).Order(StringComparer.Ordinal).ToList() ?? [];
    #endregion
  }
}
