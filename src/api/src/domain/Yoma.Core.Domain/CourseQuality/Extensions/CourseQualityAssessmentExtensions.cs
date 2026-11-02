using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
        DateAssessed = value.DateAssessed
      };
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
    /// Copies the fields that can change after a review. Identity and listing snapshot stay as they are.
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
      record.LearnerCard = JsonConvert.SerializeObject(value.LearnerCard, Settings);
      record.FixNote = value.FixNote == null ? null : JsonConvert.SerializeObject(value.FixNote, Settings);
      record.ReviewedByEmail = value.ReviewedByEmail;
      record.ReviewNote = value.ReviewNote;
      record.DateReviewed = value.DateReviewed;
      record.DateAssessed = value.DateAssessed;

      return record;
    }
    #endregion

    #region Private Members
    private static T Deserialize<T>(string value)
    {
      return JsonConvert.DeserializeObject<T>(value, Settings)
        ?? throw new InvalidOperationException($"Failed to deserialize stored course quality value of type '{typeof(T).Name}'");
    }
    #endregion
  }
}
