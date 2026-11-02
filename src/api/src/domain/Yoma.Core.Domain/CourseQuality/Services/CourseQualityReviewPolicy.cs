using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// Pure rules for reviewer decisions: how one is applied, and how long it lasts.
  /// </summary>
  public static class CourseQualityReviewPolicy
  {
    #region Public Members
    public static bool IsDecided(CourseQualityReviewStatus status)
    {
      return status is CourseQualityReviewStatus.Confirmed or CourseQualityReviewStatus.Overridden;
    }

    /// <summary>
    /// A decision lasts until the listing's own content changes, and a reviewer's NotVerified then stays. A Verified has to
    /// be backed afresh: the same site, answering, not gone or closed, and still this listing's page. Anything else ends it
    /// and goes to a person. A network that said nothing either way keeps it for a limited time, counted from the last check
    /// that did back it, so repeated failures cannot extend it.
    /// </summary>
    public static bool ShouldCarry(CourseQualityAssessmentRecord? prior, string contentFingerprint, CourseQualityFreshCheck fresh, CourseQualityOptions options, DateTimeOffset now)
    {
      ArgumentNullException.ThrowIfNull(fresh, nameof(fresh));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      if (prior == null || !IsDecided(prior.ReviewStatus) || prior.ContentFingerprint != contentFingerprint) return false;
      if (prior.Status != CourseQualityStatus.Verified) return true;

      if (fresh.DefiniteFailure) return false;
      if (fresh.Inconclusive) return (prior.DateConfirmed ?? prior.DateReviewed) >= now.AddDays(-options.CardMaxAgeInDays);

      return Backs(prior, fresh);
    }

    /// <summary>
    /// Whether this check proves a Verified.
    /// </summary>
    public static bool Backs(CourseQualityAssessmentRecord prior, CourseQualityFreshCheck fresh)
    {
      ArgumentNullException.ThrowIfNull(prior, nameof(prior));
      ArgumentNullException.ThrowIfNull(fresh, nameof(fresh));

      return fresh.HttpSuccess && !fresh.DefiniteFailure && !fresh.PageGone && !fresh.ProviderLinkFailed && fresh.StrongMatch
        && fresh.FinalHost != null && string.Equals(fresh.FinalHost, prior.FinalHost, StringComparison.OrdinalIgnoreCase);
    }

    public static void ApplyDecision(CourseQualityAssessment assessment, CourseQualityReviewDecision decision, string reviewerEmail,
      DateTimeOffset now, CourseQualityOptions options)
    {
      ArgumentNullException.ThrowIfNull(assessment, nameof(assessment));
      ArgumentNullException.ThrowIfNull(decision, nameof(decision));
      ArgumentException.ThrowIfNullOrWhiteSpace(reviewerEmail, nameof(reviewerEmail));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      if (decision.Format.HasValue) CourseQualityEngine.ApplyFormat(assessment, decision.Format.Value, options);

      assessment.ReviewStatus = decision.Status == CourseQualityEngine.ProposedStatus(assessment, options)
        ? CourseQualityReviewStatus.Confirmed
        : CourseQualityReviewStatus.Overridden;
      assessment.Status = decision.Status;
      assessment.ReviewedByEmail = reviewerEmail;
      assessment.ReviewNote = string.IsNullOrWhiteSpace(decision.Note) ? null : decision.Note.Trim();
      assessment.DateReviewed = now;

      CourseQualityEngine.RefreshOutputs(assessment);
    }

    /// <summary>
    /// Keeps the fresh findings but applies the earlier decision to them.
    /// </summary>
    public static void CarryDecision(CourseQualityAssessment assessment, CourseQualityAssessment prior, CourseQualityOptions options)
    {
      ArgumentNullException.ThrowIfNull(assessment, nameof(assessment));
      ArgumentNullException.ThrowIfNull(prior, nameof(prior));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      // the reviewer decided on this format; a fresh detection must not change it underneath them
      var format = prior.LearnerCard.Format;
      if (format != CourseFormat.Unknown && format != assessment.LearnerCard.Format) CourseQualityEngine.ApplyFormat(assessment, format, options);

      assessment.Status = prior.Status;
      assessment.ReviewStatus = prior.ReviewStatus;
      assessment.ReviewedByEmail = prior.ReviewedByEmail;
      assessment.ReviewNote = prior.ReviewNote;
      assessment.DateReviewed = prior.DateReviewed;

      CourseQualityEngine.RefreshOutputs(assessment);
    }
    #endregion
  }
}
