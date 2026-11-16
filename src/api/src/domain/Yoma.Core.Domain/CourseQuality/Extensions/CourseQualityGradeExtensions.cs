using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Extensions
{
  public static class CourseQualityGradeExtensions
  {
    #region Class Variables
    private static readonly JsonSerializerSettings Settings = new() { Converters = [new StringEnumConverter()] };
    #endregion

    #region Public Members
    public static CourseGradeResult ToResult(this CourseQualityGradeRecord value)
    {
      ArgumentNullException.ThrowIfNull(value, nameof(value));

      return new CourseGradeResult
      {
        OpportunityId = value.OpportunityId,
        Grade = value.Grade,
        GradeLabel = value.GradeLabel,
        Score = value.Score,
        Percentile = value.Percentile,
        Factors = Deserialize<List<CourseGradeFactorResult>>(value.Factors),
        Caps = Deserialize<List<string>>(value.Caps),
        RubricVersion = value.RubricVersion,
        DateGraded = value.DateGraded
      };
    }

    /// <summary>
    /// Copies a fresh result onto a stored row, new or existing. The row's identity stays as it is.
    /// </summary>
    public static CourseQualityGradeRecord ApplyResult(this CourseQualityGradeRecord record, CourseGradeResult value, CourseQualityStatus status, int feedbackResponses)
    {
      ArgumentNullException.ThrowIfNull(record, nameof(record));
      ArgumentNullException.ThrowIfNull(value, nameof(value));

      record.OpportunityId = value.OpportunityId;
      record.Status = status;
      record.Grade = value.Grade;
      record.GradeLabel = value.GradeLabel;
      record.Score = value.Score;
      record.Percentile = value.Percentile;
      record.FeedbackResponses = feedbackResponses;
      record.Factors = JsonConvert.SerializeObject(value.Factors, Settings);
      record.Caps = JsonConvert.SerializeObject(value.Caps, Settings);
      record.RubricVersion = value.RubricVersion;
      record.DateGraded = value.DateGraded;

      return record;
    }

    /// <summary>
    /// Whether a run would change what is stored, so an unchanged catalogue writes nothing.
    /// </summary>
    public static bool Differs(this CourseQualityGradeRecord record, CourseGradeResult value, CourseQualityStatus status, int feedbackResponses)
    {
      ArgumentNullException.ThrowIfNull(record, nameof(record));
      ArgumentNullException.ThrowIfNull(value, nameof(value));

      return record.Status != status || record.Grade != value.Grade || record.GradeLabel != value.GradeLabel || record.Score != value.Score
        || record.Percentile != value.Percentile || record.FeedbackResponses != feedbackResponses || record.RubricVersion != value.RubricVersion
        || !SameJson(record.Factors, value.Factors) || !SameJson(record.Caps, value.Caps);
    }

    public static CourseGradeFactorRow ToFactorRow(this CourseQualityGradeRecord value)
    {
      ArgumentNullException.ThrowIfNull(value, nameof(value));

      var factors = Deserialize<List<CourseGradeFactorResult>>(value.Factors);

      return new CourseGradeFactorRow
      {
        OpportunityId = value.OpportunityId,
        OpportunityTitle = value.OpportunityTitle,
        OrganizationName = value.OrganizationName,
        Status = value.Status,
        Grade = value.Grade,
        Score = value.Score,
        FeedbackResponses = value.FeedbackResponses,
        Factors = factors.ToDictionary(o => o.Factor, o => o.Score),
        FactorsWithData = [.. factors.Where(o => o.HasData).Select(o => o.Factor)]
      };
    }
    #endregion

    #region Private Members
    /// <summary>
    /// Compared as JSON, because jsonb gives a stored value back with its own key order and spacing.
    /// </summary>
    private static bool SameJson(string? stored, object value)
    {
      if (string.IsNullOrEmpty(stored)) return false;

      return JToken.DeepEquals(JToken.Parse(stored), JToken.FromObject(value, JsonSerializer.Create(Settings)));
    }

    private static T Deserialize<T>(string value)
    {
      return JsonConvert.DeserializeObject<T>(value, Settings)
        ?? throw new InvalidOperationException($"Failed to deserialize stored course grade value of type '{typeof(T).Name}'");
    }
    #endregion
  }
}
