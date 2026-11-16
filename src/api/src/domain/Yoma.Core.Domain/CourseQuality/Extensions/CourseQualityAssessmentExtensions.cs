using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Extensions
{
  public static class CourseQualityAssessmentExtensions
  {
    #region Class Variables
    private static readonly JsonSerializerSettings Settings = new() { Converters = [new StringEnumConverter()] };
    #endregion

    #region Public Members
    public static CourseQualityAssessment ToAssessment(this CourseQualityAssessmentRecord value)
    {
      ArgumentNullException.ThrowIfNull(value, nameof(value));

      var fixNote = string.IsNullOrEmpty(value.FixNote) ? null : Deserialize<PartnerFixNote>(value.FixNote);
      if (fixNote != null) fixNote.OpportunityTitle = value.OpportunityTitle;

      return new CourseQualityAssessment
      {
        Id = value.Id,
        OpportunityId = value.OpportunityId,
        OpportunityTitle = value.OpportunityTitle,
        OrganizationId = value.OrganizationId,
        OrganizationName = value.OrganizationName,
        RubricVersion = value.RubricVersion,
        Score = value.Score,
        Confidence = value.Confidence,
        Status = value.Status,
        ReviewStatus = value.ReviewStatus,
        Criteria = Deserialize<List<CourseQualityCriterionResult>>(value.Criteria),
        AIUsed = value.AIUsed,
        AIModel = value.AIModel,
        AISummary = value.AISummary,
        LearnerCard = Deserialize<LearnerCard>(value.LearnerCard),
        FixNote = fixNote,
        ReviewedByEmail = value.ReviewedByEmail,
        ReviewNote = value.ReviewNote,
        DateReviewed = value.DateReviewed,
        DateAssessed = value.DateAssessed,
        ReviewReasons = string.IsNullOrEmpty(value.ReviewReasons) ? [] : Deserialize<List<string>>(value.ReviewReasons)
      };
    }

    /// <summary>
    /// Whether a fresh row changes anything grading reads, so an unchanged batch does not regrade the catalogue.
    /// </summary>
    public static bool DiffersForGrading(this CourseQualityAssessmentRecord prior, CourseQualityAssessmentRecord next)
    {
      ArgumentNullException.ThrowIfNull(prior, nameof(prior));
      ArgumentNullException.ThrowIfNull(next, nameof(next));

      if (prior.Status != next.Status || prior.ReviewStatus != next.ReviewStatus || prior.Score != next.Score
        || prior.Confidence != next.Confidence || prior.Format != next.Format) return true;

      if (!SameJson(prior.Criteria, next.Criteria) || !SameJson(prior.PageEvidence, next.PageEvidence)) return true;

      var priorCard = Deserialize<LearnerCard>(prior.LearnerCard);
      var nextCard = Deserialize<LearnerCard>(next.LearnerCard);
      return priorCard.Cost?.Type != nextCard.Cost?.Type || priorCard.SelfPaced != nextCard.SelfPaced;
    }

    public static CourseQualityPageEvidence? ToPageEvidence(this CourseQualityAssessmentRecord value)
    {
      ArgumentNullException.ThrowIfNull(value, nameof(value));

      return string.IsNullOrEmpty(value.PageEvidence) ? null : Deserialize<CourseQualityPageEvidence>(value.PageEvidence);
    }

    public static string Serialize(this CourseQualityPageEvidence value)
    {
      ArgumentNullException.ThrowIfNull(value, nameof(value));

      return JsonConvert.SerializeObject(value, Settings);
    }

    public static CourseQualityAssessmentRecord ToRecord(this CourseQualityAssessment value, DateTimeOffset opportunityDateModified, string contentFingerprint)
    {
      ArgumentNullException.ThrowIfNull(value, nameof(value));

      return new CourseQualityAssessmentRecord
      {
        Id = value.Id,
        OpportunityId = value.OpportunityId,
        OpportunityTitle = value.OpportunityTitle,
        OrganizationId = value.OrganizationId,
        OrganizationName = value.OrganizationName,
        OpportunityDateModified = opportunityDateModified,
        ContentFingerprint = contentFingerprint,
        IsLatest = true
      }.ApplyAssessment(value);
    }

    /// <summary>
    /// Copies the fields that can change after a review. Identity, listing snapshot and page evidence stay as they are.
    /// </summary>
    public static CourseQualityAssessmentRecord ApplyAssessment(this CourseQualityAssessmentRecord record, CourseQualityAssessment value)
    {
      ArgumentNullException.ThrowIfNull(record, nameof(record));
      ArgumentNullException.ThrowIfNull(value, nameof(value));

      record.RubricVersion = value.RubricVersion;
      record.Score = value.Score;
      record.Confidence = value.Confidence;
      record.Status = value.Status;
      record.ReviewStatus = value.ReviewStatus;
      record.Format = value.LearnerCard.Format;
      var selfPacedVerified = value.LearnerCard.SelfPaced && value.Status == CourseQualityStatus.Verified;
      record.DatesSuspect = !selfPacedVerified && (!value.LearnerCard.DatesConfirmed || value.Criteria.Any(o => o.Criterion == CourseQualityCriterion.DatesAccurate && o.Score < 100));
      record.CardReady = !record.DatesSuspect && value.LearnerCard.Format != CourseFormat.Unknown
        && value.LearnerCard.CommitmentHours.HasValue && !string.IsNullOrWhiteSpace(value.LearnerCard.TimelineSummary);
      record.AIUsed = value.AIUsed;
      record.AIModel = value.AIModel;
      record.AISummary = value.AISummary;
      record.Criteria = JsonConvert.SerializeObject(value.Criteria, Settings);
      record.LearnerCard = StoredCard(value.LearnerCard);
      record.FixNote = value.FixNote == null ? null : JsonConvert.SerializeObject(value.FixNote, Settings);
      record.ReviewReasons = value.ReviewReasons.Count == 0 ? null : JsonConvert.SerializeObject(value.ReviewReasons, Settings);
      record.ReviewedByEmail = value.ReviewedByEmail;
      record.ReviewNote = value.ReviewNote;
      record.DateReviewed = value.DateReviewed;
      record.DateAssessed = value.DateAssessed;

      return record;
    }
    #endregion

    #region Private Members
    /// <summary>
    /// Compared as JSON, because jsonb gives a stored value back with its own key order and spacing.
    /// </summary>
    private static bool SameJson(string? a, string? b)
    {
      if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return string.IsNullOrEmpty(a) == string.IsNullOrEmpty(b);

      return JToken.DeepEquals(JToken.Parse(a), JToken.Parse(b));
    }

    /// <summary>
    /// The grade is read from the latest grading run when a card is shown, so a stored card never carries a stale one.
    /// </summary>
    private static string StoredCard(LearnerCard card)
    {
      var stored = JObject.FromObject(card, JsonSerializer.Create(Settings));
      stored.Remove(nameof(LearnerCard.Grade));
      stored.Remove(nameof(LearnerCard.GradeLabel));
      return stored.ToString(Formatting.None);
    }

    private static T Deserialize<T>(string value)
    {
      return JsonConvert.DeserializeObject<T>(value, Settings)
        ?? throw new InvalidOperationException($"Failed to deserialize stored course quality value of type '{typeof(T).Name}'");
    }
    #endregion
  }
}
