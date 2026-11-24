using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Helpers;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Entity;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.Entity.Interfaces.Lookups;
using Yoma.Core.Domain.MyOpportunity;
using Yoma.Core.Domain.MyOpportunity.Interfaces;
using Yoma.Core.Domain.Opportunity;
using Yoma.Core.Domain.Opportunity.Interfaces.Lookups;
using Yoma.Core.Domain.Opportunity.Models;
using Yoma.Core.Domain.Pathway.Badges.Interfaces;
using Yoma.Core.Domain.Pathway.Badges.Models;
using Yoma.Core.Domain.SSI;
using Yoma.Core.Domain.SSI.Interfaces.Lookups;
using Yoma.Core.Domain.SSI.Models;

namespace Yoma.Core.Domain.Pathway.Badges.Services
{
  /// <summary>
  /// Reads what badges are computed from, then hands it to the pure calculators. Nothing is stored.
  /// </summary>
  public class PathwayBadgeService : IPathwayBadgeService, IPathwayBadgeSource
  {
    #region Class Variables
    // set by MyOpportunityService when a completion comes through an instant-verification action link
    public const string ActionLinkComment = "Auto-verification: Action Link";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PartnerBadgeOptions _partnerOptions;
    private readonly LearnerBadgeOptions _learnerOptions;
    private readonly IUserService _userService;
    private readonly IOrganizationService _organizationService;
    private readonly IOpportunityTypeService _opportunityTypeService;
    private readonly IOpportunityStatusService _opportunityStatusService;
    private readonly IOrganizationStatusService _organizationStatusService;
    private readonly IMyOpportunityActionService _myOpportunityActionService;
    private readonly IMyOpportunityVerificationStatusService _myOpportunityVerificationStatusService;
    private readonly ISSICredentialIssuanceStatusService _ssiCredentialIssuanceStatusService;
    private readonly ICourseQualityGradingService _gradingService;
    private readonly IPathwayBadgePathProvider _pathProvider;
    private readonly IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Opportunity.Models.Opportunity> _opportunityRepository;
    private readonly IRepository<OpportunitySkill> _opportunitySkillRepository;
    private readonly IRepositoryBatchedWithNavigationAndCustomFieldFilter<MyOpportunity.Models.MyOpportunity> _myOpportunityRepository;
    private readonly IRepository<SSICredentialIssuance> _ssiCredentialIssuanceRepository;
    private readonly IRepository<CourseQualityAssessmentRecord> _assessmentRepository;
    private readonly IRepositoryBatched<CourseQualityGradeRecord> _gradeRepository;
    #endregion

    #region Constructor
    public PathwayBadgeService(IHttpContextAccessor httpContextAccessor,
      IOptions<PartnerBadgeOptions> partnerOptions,
      IOptions<LearnerBadgeOptions> learnerOptions,
      IUserService userService,
      IOrganizationService organizationService,
      IOpportunityTypeService opportunityTypeService,
      IOpportunityStatusService opportunityStatusService,
      IOrganizationStatusService organizationStatusService,
      IMyOpportunityActionService myOpportunityActionService,
      IMyOpportunityVerificationStatusService myOpportunityVerificationStatusService,
      ISSICredentialIssuanceStatusService ssiCredentialIssuanceStatusService,
      ICourseQualityGradingService gradingService,
      IPathwayBadgePathProvider pathProvider,
      IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Opportunity.Models.Opportunity> opportunityRepository,
      IRepository<OpportunitySkill> opportunitySkillRepository,
      IRepositoryBatchedWithNavigationAndCustomFieldFilter<MyOpportunity.Models.MyOpportunity> myOpportunityRepository,
      IRepository<SSICredentialIssuance> ssiCredentialIssuanceRepository,
      IRepository<CourseQualityAssessmentRecord> assessmentRepository,
      IRepositoryBatched<CourseQualityGradeRecord> gradeRepository)
    {
      _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
      _partnerOptions = partnerOptions?.Value ?? throw new ArgumentNullException(nameof(partnerOptions));
      _learnerOptions = learnerOptions?.Value ?? throw new ArgumentNullException(nameof(learnerOptions));
      _userService = userService ?? throw new ArgumentNullException(nameof(userService));
      _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
      _opportunityTypeService = opportunityTypeService ?? throw new ArgumentNullException(nameof(opportunityTypeService));
      _opportunityStatusService = opportunityStatusService ?? throw new ArgumentNullException(nameof(opportunityStatusService));
      _organizationStatusService = organizationStatusService ?? throw new ArgumentNullException(nameof(organizationStatusService));
      _myOpportunityActionService = myOpportunityActionService ?? throw new ArgumentNullException(nameof(myOpportunityActionService));
      _myOpportunityVerificationStatusService = myOpportunityVerificationStatusService ?? throw new ArgumentNullException(nameof(myOpportunityVerificationStatusService));
      _ssiCredentialIssuanceStatusService = ssiCredentialIssuanceStatusService ?? throw new ArgumentNullException(nameof(ssiCredentialIssuanceStatusService));
      _gradingService = gradingService ?? throw new ArgumentNullException(nameof(gradingService));
      _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
      _opportunityRepository = opportunityRepository ?? throw new ArgumentNullException(nameof(opportunityRepository));
      _opportunitySkillRepository = opportunitySkillRepository ?? throw new ArgumentNullException(nameof(opportunitySkillRepository));
      _myOpportunityRepository = myOpportunityRepository ?? throw new ArgumentNullException(nameof(myOpportunityRepository));
      _ssiCredentialIssuanceRepository = ssiCredentialIssuanceRepository ?? throw new ArgumentNullException(nameof(ssiCredentialIssuanceRepository));
      _assessmentRepository = assessmentRepository ?? throw new ArgumentNullException(nameof(assessmentRepository));
      _gradeRepository = gradeRepository ?? throw new ArgumentNullException(nameof(gradeRepository));
    }
    #endregion

    #region Public Members
    public async Task<List<LearnerBadge>> ListMine()
    {
      var user = _userService.GetByUsername(HttpContextAccessorHelper.GetUsername(_httpContextAccessor, false), false, false);

      return await ListForUser(user.Id, await _pathProvider.GetForUser(user.Id));
    }

    public Task<List<LearnerBadge>> ListForUser(Guid userId, LearnerBadgePath? path)
    {
      if (userId == Guid.Empty) throw new ArgumentNullException(nameof(userId));

      return Task.FromResult(LearnerBadgeCalculator.Calculate(path, ListCompletions(userId), _learnerOptions));
    }

    public PartnerBadge GetPartnerBadge(Guid organizationId)
    {
      if (organizationId == Guid.Empty) throw new ArgumentNullException(nameof(organizationId));

      // one message for every case, so the response does not reveal an organisation that is not public
      var organization = _organizationService.GetByIdOrNull(organizationId, false, false, false);
      if (organization == null || organization.Status != OrganizationStatus.Active)
        throw new EntityNotFoundException($"Organization with id '{organizationId}' does not exist");

      return PartnerBadgeCalculator.Calculate(organization.Id, organization.Name, CountPartnerCourses(organization.Id), _partnerOptions);
    }
    #endregion

    #region Private Members
    /// <summary>
    /// The learner's completed verifications. A pending or declined one is not a completion, and a deleted one is gone.
    /// </summary>
    private List<LearnerBadgeCompletion> ListCompletions(Guid userId)
    {
      var actionVerificationId = _myOpportunityActionService.GetByName(MyOpportunity.Action.Verification.ToString()).Id;
      var statusCompletedId = _myOpportunityVerificationStatusService.GetByName(VerificationStatus.Completed.ToString()).Id;

      var rows = QueryCompletions(_myOpportunityRepository.Query(), userId, actionVerificationId, statusCompletedId).ToList();
      if (rows.Count == 0) return [];

      var opportunityIds = rows.Select(o => o.OpportunityId).Distinct().ToList();
      var myOpportunityIds = rows.Select(o => (Guid?)o.Id).ToList();

      var skills = _opportunitySkillRepository.Query().Where(o => opportunityIds.Contains(o.OpportunityId))
        .GroupBy(o => o.OpportunityId).Select(o => new { OpportunityId = o.Key, Count = o.Count() })
        .ToDictionary(o => o.OpportunityId, o => o.Count);

      var statusIssuedId = _ssiCredentialIssuanceStatusService.GetByName(CredentialIssuanceStatus.Issued.ToString()).Id;
      var credentials = _ssiCredentialIssuanceRepository.Query()
        .Where(o => o.UserId == userId && o.StatusId == statusIssuedId && myOpportunityIds.Contains(o.MyOpportunityId))
        .Select(o => o.MyOpportunityId!.Value).ToHashSet();

      var grades = _gradingService.ListGrades(opportunityIds);

      return [.. rows.Select(o => new LearnerBadgeCompletion(o.OpportunityId, o.OpportunityType, o.DateCompleted!.Value, Evidence(o.CommentVerification),
        skills.GetValueOrDefault(o.OpportunityId), credentials.Contains(o.Id), Grade(grades.GetValueOrDefault(o.OpportunityId))))];
    }

    public static IQueryable<LearnerBadgeCompletionRow> QueryCompletions(IQueryable<MyOpportunity.Models.MyOpportunity> myOpportunities, Guid userId,
      Guid actionVerificationId, Guid statusCompletedId)
    {
      ArgumentNullException.ThrowIfNull(myOpportunities, nameof(myOpportunities));

      return myOpportunities
        .Where(o => o.UserId == userId && o.ActionId == actionVerificationId && o.VerificationStatusId == statusCompletedId && o.DateCompleted.HasValue)
        .Select(o => new LearnerBadgeCompletionRow
        {
          Id = o.Id,
          OpportunityId = o.OpportunityId,
          OpportunityType = o.OpportunityType,
          DateCompleted = o.DateCompleted,
          CommentVerification = o.CommentVerification
        });
    }

    /// <summary>
    /// The organisation's publicly visible Learning courses, as a subquery.
    /// </summary>
    public static IQueryable<Guid> QueryPartnerCourseIds(IQueryable<Opportunity.Models.Opportunity> opportunities, Guid organizationId, Guid typeLearningId,
      Guid statusActiveId, Guid organizationStatusActiveId)
    {
      ArgumentNullException.ThrowIfNull(opportunities, nameof(opportunities));

      return opportunities.Where(o => o.OrganizationId == organizationId && o.TypeId == typeLearningId
        && o.StatusId == statusActiveId && o.OrganizationStatusId == organizationStatusActiveId && (!o.Hidden.HasValue || o.Hidden == false))
        .Select(o => o.Id);
    }

    /// <summary>
    /// Instant verification writes a fixed comment, which may have notes appended, and nothing else marks it on the record.
    /// Every other completed verification was approved, imported or synced by the organisation.
    /// </summary>
    public static LearnerBadgeEvidence Evidence(string? comment)
    {
      return comment?.StartsWith(ActionLinkComment, StringComparison.OrdinalIgnoreCase) == true ? LearnerBadgeEvidence.ActionLink : LearnerBadgeEvidence.Verified;
    }

    private static CourseGrade? Grade(CourseGradeResult? grade)
    {
      return grade == null || grade.Grade == CourseGrade.Ungraded ? null : grade.Grade;
    }

    /// <summary>
    /// Over the organisation's publicly visible Learning courses: grades A to D, held back (latest Not verified) and waiting
    /// (latest needs review, no grade). Event and Job assessments never count, because only courses are graded.
    /// </summary>
    private PartnerBadgeCounts CountPartnerCourses(Guid organizationId)
    {
      var typeLearningId = _opportunityTypeService.GetByName(Opportunity.Type.Learning.ToString()).Id;
      var statusActiveId = _opportunityStatusService.GetByName(Status.Active.ToString()).Id;
      var organizationStatusActiveId = _organizationStatusService.GetByName(OrganizationStatus.Active.ToString()).Id;

      var courseIds = QueryPartnerCourseIds(_opportunityRepository.Query(), organizationId, typeLearningId, statusActiveId, organizationStatusActiveId);

      var grades = _gradeRepository.Query().Where(o => courseIds.Contains(o.OpportunityId))
        .Select(o => new { o.OpportunityId, o.Grade }).ToList();
      var graded = grades.Where(o => o.Grade != CourseGrade.Ungraded).ToList();
      var gradedIds = graded.Select(o => o.OpportunityId).ToHashSet();

      var latest = _assessmentRepository.Query().Where(o => o.IsLatest && courseIds.Contains(o.OpportunityId))
        .Select(o => new { o.OpportunityId, o.Status }).ToList();

      return new PartnerBadgeCounts(
        TopGrades: graded.Count(o => o.Grade is CourseGrade.A or CourseGrade.B),
        Graded: graded.Count,
        HeldBack: latest.Count(o => o.Status == CourseQualityStatus.NotVerified && !gradedIds.Contains(o.OpportunityId)),
        WaitingForReview: latest.Count(o => o.Status == CourseQualityStatus.NeedsReview && !gradedIds.Contains(o.OpportunityId)));
    }
    #endregion
  }

  public sealed class LearnerBadgeCompletionRow
  {
    public Guid Id { get; init; }

    public Guid OpportunityId { get; init; }

    public Opportunity.Type OpportunityType { get; init; }

    public DateTimeOffset? DateCompleted { get; init; }

    public string? CommentVerification { get; init; }
  }

  /// <summary>
  /// No path until the pathway engine registers its own provider, so GET pathway/badges/me still answers with the badges that need none.
  /// </summary>
  public sealed class NoPathwayBadgePathProvider : IPathwayBadgePathProvider
  {
    public Task<LearnerBadgePath?> GetForUser(Guid userId) => Task.FromResult<LearnerBadgePath?>(null);
  }
}
