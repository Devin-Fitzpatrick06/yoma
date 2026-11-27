namespace Yoma.Core.Domain.CourseQuality.Models
{
  // Iteration 2 contract: course grades, the editable rubric, and learner feedback.
  // The orchestrator owns this file, like CourseQualityContract.cs. Change it only through them.

  /// <summary>
  /// What makes a course good, as opposed to Verified, which only says the listing is live and accurate.
  /// Each factor is scored 0 to 100 by fixed code; the rubric only sets weights and parameters.
  /// </summary>
  public enum CourseGradeFactor
  {
    /// <summary>Learner survey answers, shrunk towards a prior until there are enough of them.</summary>
    LearnerFeedback,

    /// <summary>A syllabus, module list or learning outcomes a learner can read before starting.</summary>
    SyllabusAvailable,

    /// <summary>How reliable the course's organisation is across all its listings.</summary>
    ProviderReliability,

    /// <summary>How much real structure the course has: modules, assessment, certificate, skills, a full description.</summary>
    Comprehensiveness,

    /// <summary>Whether the stated length suits a self-paced learner, with short practical courses not penalised.</summary>
    CourseLength,

    /// <summary>Share of learners who started and went on to complete, smoothed for small numbers.</summary>
    Completion,

    /// <summary>The listing's checks and how current it is.</summary>
    ListingIntegrity,

    /// <summary>Free, language and low-data signals that matter to learners in low- and middle-income countries.</summary>
    Accessibility,

    /// <summary>
    /// What completers did next on Yoma: another course towards the same goal, or a Job opportunity. Weight 0 until there is enough
    /// data; neutral with no data or fewer completers than the minimum group size. Describes what followed, not what the course caused.
    /// </summary>
    Outcomes,

    // jobs: graded with their own rubric and ranked only against other jobs; the course rubric never includes these

    /// <summary>A stated pay amount; a pay period or "market related" alone scores low.</summary>
    PayTransparency,

    /// <summary>Schedule or contract type, location, requirements and how to apply.</summary>
    JobClarity,

    /// <summary>Open to young people: no or entry-level experience scores high, years of experience lower.</summary>
    YouthFit,

    /// <summary>The job's checks: its status and whether it shows any scam warning sign.</summary>
    ListingSafety,

    /// <summary>How many of the partner's jobs pass the checks, smoothed for few listings.</summary>
    PartnerReliability,

    /// <summary>Applicant survey answers. Neutral until applicants are asked.</summary>
    ApplicantFeedback
  }

  /// <summary>
  /// Bands set by percentile across the graded catalogue, so only the top share earns A. Ungraded means not graded: the course is
  /// not verified, or too little is known about it.
  /// </summary>
  public enum CourseGrade
  {
    Ungraded,
    A,
    B,
    C,
    D
  }

  public class CourseGradeFactorResult
  {
    public CourseGradeFactor Factor { get; set; }

    /// <summary>
    /// 0 to 100 before weighting.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// The rubric weight used, as a share of 100 across the factors.
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// False when the factor had nothing to go on and fell back to the rubric's neutral score.
    /// </summary>
    public bool HasData { get; set; }

    /// <summary>
    /// Plain reason for reviewers, for example "Syllabus with 8 modules on the course page".
    /// </summary>
    public string Reason { get; set; } = null!;
  }

  public class CourseGradeResult
  {
    public Guid OpportunityId { get; set; }

    public CourseGrade Grade { get; set; }

    /// <summary>
    /// The band's learner-facing label from the rubric, for example "Top pick".
    /// </summary>
    public string GradeLabel { get; set; } = null!;

    /// <summary>
    /// Weighted score, 0 to 100.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Share of graded courses this one scores at or above, 0 to 100. 100 is the best.
    /// </summary>
    public int Percentile { get; set; }

    public List<CourseGradeFactorResult> Factors { get; set; } = [];

    /// <summary>
    /// Rules that held the grade below what the score alone would give, for example "Not verified courses cannot be graded above C".
    /// </summary>
    public List<string> Caps { get; set; } = [];

    public string RubricVersion { get; set; } = null!;

    public DateTimeOffset DateGraded { get; set; }
  }

  public class CourseGradeBand
  {
    public CourseGrade Grade { get; set; }

    public string Label { get; set; } = null!;

    /// <summary>
    /// Cumulative top share of graded courses this band reaches down to. A at 15 means the best 15%.
    /// </summary>
    public decimal TopPercent { get; set; }

    /// <summary>
    /// A course also needs at least this score for the band, so a weak catalogue does not hand out A.
    /// </summary>
    public int MinScore { get; set; }
  }

  public class CourseGradeFactorOptions
  {
    public decimal Weight { get; set; }

    /// <summary>
    /// Score used when the factor has no data, so a new course is neither rewarded nor punished for it.
    /// </summary>
    public int NeutralScore { get; set; } = 50;

    /// <summary>
    /// Factor-specific numbers, for example the length sweet spot in hours. Unknown keys are ignored.
    /// </summary>
    public Dictionary<string, decimal> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
  }

  /// <summary>
  /// The editable rubric. Defaults come from configuration; an admin can save an override that replaces them until reset.
  /// </summary>
  public class CourseGradeRubric
  {
    public string Version { get; set; } = null!;

    public Dictionary<CourseGradeFactor, CourseGradeFactorOptions> Factors { get; set; } = [];

    /// <summary>
    /// Best first. The last band takes everything left.
    /// </summary>
    public List<CourseGradeBand> Bands { get; set; } = [];

    /// <summary>
    /// The best grade a course still waiting for review can reach. NotVerified courses are never graded and are left out of the percentiles.
    /// </summary>
    public CourseGrade NeedsReviewMaxGrade { get; set; } = CourseGrade.B;

    /// <summary>
    /// Floors a course in the A band must also meet, or it drops to B. They may hold the A share under its band. Intended.
    /// </summary>
    public int AMinSyllabusScore { get; set; } = 50;

    public int AMinFeedbackScore { get; set; } = 60;

    /// <summary>
    /// The feedback floor applies only once a course has at least this many answers.
    /// </summary>
    public int AFeedbackFloorMinResponses { get; set; } = 5;

    /// <summary>
    /// A course needs data on at least this many factors to be graded at all.
    /// </summary>
    public int MinFactorsWithData { get; set; } = 3;
  }

  public class CourseGradeRubricState
  {
    public CourseGradeRubric Rubric { get; set; } = null!;

    /// <summary>
    /// True when no admin override is saved and configuration applies.
    /// </summary>
    public bool IsDefault { get; set; }

    public string? UpdatedByEmail { get; set; }

    public DateTimeOffset? DateUpdated { get; set; }

    /// <summary>
    /// Courses per grade under this rubric.
    /// </summary>
    public Dictionary<CourseGrade, int> Distribution { get; set; } = [];
  }

  /// <summary>
  /// One course's factor scores, so the rubric page can re-weight the whole catalogue in the browser without a request per change.
  /// </summary>
  public class CourseGradeFactorRow
  {
    public Guid OpportunityId { get; set; }

    public string OpportunityTitle { get; set; } = null!;

    public string OrganizationName { get; set; } = null!;

    public CourseQualityStatus Status { get; set; }

    public CourseGrade Grade { get; set; }

    public int Score { get; set; }

    /// <summary>
    /// Factor scores, 0 to 100, with the neutral score already applied where there was no data.
    /// </summary>
    public Dictionary<CourseGradeFactor, int> Factors { get; set; } = [];

    public List<CourseGradeFactor> FactorsWithData { get; set; } = [];

    /// <summary>
    /// Learners who answered that they took the course, so the browser can apply the A feedback floor exactly.
    /// </summary>
    public int FeedbackResponses { get; set; }
  }

  public enum CourseFeedbackAnswer
  {
    /// <summary>The learner left for the course and has not been asked yet, or asked and not answered.</summary>
    Pending,

    /// <summary>They took the course. Rating and survey answers may follow.</summary>
    Taken,

    /// <summary>Not yet: ask again after the snooze.</summary>
    NotYet,

    /// <summary>They decided not to take it.</summary>
    NotTaken,

    /// <summary>Closed the prompt. Never asked about this course again.</summary>
    Dismissed
  }

  /// <summary>
  /// A course the learner left Yoma for, to ask about on their return.
  /// </summary>
  public class CourseFeedbackPrompt
  {
    public Guid OpportunityId { get; set; }

    public string OpportunityTitle { get; set; } = null!;

    public string OrganizationName { get; set; } = null!;

    public string? OrganizationLogoURL { get; set; }

    public DateTimeOffset DateLeft { get; set; }
  }

  public class CourseFeedbackSubmission
  {
    public CourseFeedbackAnswer Answer { get; set; }

    /// <summary>
    /// 1 to 5, only with Taken.
    /// </summary>
    public int? Rating { get; set; }

    /// <summary>
    /// Whether they finished it, only with Taken.
    /// </summary>
    public bool? Completed { get; set; }

    /// <summary>
    /// Would they recommend it to a friend, only with Taken.
    /// </summary>
    public bool? WouldRecommend { get; set; }

    /// <summary>
    /// The course matched what its Yoma listing said.
    /// </summary>
    public bool? MatchedDescription { get; set; }

    /// <summary>
    /// Keys from the rubric's good tags. Unknown keys are rejected.
    /// </summary>
    public List<string> Good { get; set; } = [];

    /// <summary>
    /// Keys from the rubric's bad tags. Unknown keys are rejected.
    /// </summary>
    public List<string> Bad { get; set; } = [];

    /// <summary>
    /// Optional, at most 500 characters. Seen by Yoma reviewers only, never by partners or the public.
    /// </summary>
    public string? Comment { get; set; }
  }

  public class CourseFeedbackTag
  {
    public string Key { get; set; } = null!;

    public string Label { get; set; } = null!;
  }

  /// <summary>
  /// The survey's fixed choices, so the web never hard-codes them.
  /// </summary>
  public class CourseFeedbackOptions
  {
    public List<CourseFeedbackTag> Good { get; set; } = [];

    public List<CourseFeedbackTag> Bad { get; set; } = [];
  }

  public class CourseFeedbackTagCount
  {
    public string Key { get; set; } = null!;

    public string Label { get; set; } = null!;

    public int Count { get; set; }
  }

  /// <summary>
  /// What learners said about one course. Comments are filled only for Yoma admins.
  /// </summary>
  public class CourseFeedbackSummary
  {
    public Guid OpportunityId { get; set; }

    /// <summary>
    /// Learners who said they took the course.
    /// </summary>
    public int Responses { get; set; }

    public int Completed { get; set; }

    public decimal? AverageRating { get; set; }

    /// <summary>
    /// The rating the grade uses: the average shrunk towards the rubric's prior until enough learners answer.
    /// </summary>
    public decimal? AdjustedRating { get; set; }

    public int WouldRecommendYes { get; set; }

    public int WouldRecommendNo { get; set; }

    public int MatchedDescriptionYes { get; set; }

    public int MatchedDescriptionNo { get; set; }

    public List<CourseFeedbackTagCount> Good { get; set; } = [];

    public List<CourseFeedbackTagCount> Bad { get; set; } = [];

    /// <summary>
    /// Newest first, at most 10. Admin only: empty for anyone else.
    /// </summary>
    public List<string> RecentComments { get; set; } = [];
  }
}
