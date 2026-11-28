using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Net;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Api.Controllers
{
  [Route($"api/{Common.Constants.Api_Version}/courseQuality")]
  [ApiController]
  [Authorize(Policy = Common.Constants.Authorization_Policy)]
  [SwaggerTag("(by default, Admin role required)")]
  public class CourseQualityController : ControllerBase
  {
    #region Class Variables
    private readonly ILogger<CourseQualityController> _logger;
    private readonly ICourseQualityService _courseQualityService;
    private readonly ICourseQualityGradingService _gradingService;
    private readonly ICourseQualityFeedbackService _feedbackService;
    private readonly ICourseQualityAIUsageService _aiUsageService;
    #endregion

    #region Constructor
    public CourseQualityController(ILogger<CourseQualityController> logger, ICourseQualityService courseQualityService, ICourseQualityGradingService gradingService,
      ICourseQualityFeedbackService feedbackService, ICourseQualityAIUsageService aiUsageService)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _courseQualityService = courseQualityService ?? throw new ArgumentNullException(nameof(courseQualityService));
      _gradingService = gradingService ?? throw new ArgumentNullException(nameof(gradingService));
      _feedbackService = feedbackService ?? throw new ArgumentNullException(nameof(feedbackService));
      _aiUsageService = aiUsageService ?? throw new ArgumentNullException(nameof(aiUsageService));
    }
    #endregion

    #region Public Members
    #region Anonymous Actions
    [SwaggerOperation(Summary = "Get the learner card of a publicly visible course (Anonymous)")]
    [HttpGet("opportunity/{opportunityId}/card")]
    [ProducesResponseType(typeof(LearnerCard), (int)HttpStatusCode.OK)]
    [AllowAnonymous]
    public IActionResult GetCard([FromRoute] Guid opportunityId)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetCard));

      var result = _courseQualityService.GetPublicCard(opportunityId);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetCard));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Get the good and bad tags the learner survey offers (Anonymous)")]
    [HttpGet("feedback/options")]
    [ProducesResponseType(typeof(CourseFeedbackOptions), (int)HttpStatusCode.OK)]
    [AllowAnonymous]
    public IActionResult GetFeedbackOptions()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetFeedbackOptions));

      var result = _feedbackService.GetOptions();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetFeedbackOptions));

      return StatusCode((int)HttpStatusCode.OK, result);
    }
    #endregion Anonymous Actions

    #region Authenticated User Based Actions
    [SwaggerOperation(Summary = "Record that the signed-in learner left Yoma for a course (Authenticated User)")]
    [HttpPost("opportunity/{opportunityId}/feedback/left")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize(Roles = $"{Constants.Role_User}")]
    public async Task<IActionResult> RecordFeedbackLeft([FromRoute] Guid opportunityId)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(RecordFeedbackLeft));

      await _feedbackService.RecordLeft(opportunityId);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(RecordFeedbackLeft));

      return NoContent();
    }

    [SwaggerOperation(Summary = "Get the course the signed-in learner is due to be asked about, at most one (Authenticated User)")]
    [HttpGet("feedback/pending")]
    [ProducesResponseType(typeof(List<CourseFeedbackPrompt>), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_User}")]
    public async Task<IActionResult> ListPendingFeedback()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(ListPendingFeedback));

      var result = await _feedbackService.ListPending();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(ListPendingFeedback));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Save the signed-in learner's answer about a course (Authenticated User)")]
    [HttpPost("opportunity/{opportunityId}/feedback")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize(Roles = $"{Constants.Role_User}")]
    public async Task<IActionResult> SubmitFeedback([FromRoute] Guid opportunityId, [FromBody] CourseFeedbackSubmission submission)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(SubmitFeedback));

      await _feedbackService.Submit(opportunityId, submission);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(SubmitFeedback));

      return NoContent();
    }
    #endregion Authenticated User Based Actions

    #region Administrative Actions
    [SwaggerOperation(Summary = "Assess a course now (Admin role required)")]
    [HttpPost("opportunity/{opportunityId}/assess")]
    [ProducesResponseType(typeof(CourseQualityAssessment), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public async Task<IActionResult> Assess([FromRoute] Guid opportunityId)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(Assess));

      var result = await _courseQualityService.Assess(opportunityId, HttpContext.RequestAborted);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(Assess));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Search the latest assessments for the reviewer queue (Admin role required)")]
    [HttpPost("review/search")]
    [ProducesResponseType(typeof(CourseQualityReviewSearchResults), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public IActionResult SearchReviews([FromBody] CourseQualityReviewSearchFilter filter)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(SearchReviews));

      var result = _courseQualityService.SearchReviews(filter);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(SearchReviews));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Record a reviewer decision on the latest assessment (Admin role required)")]
    [HttpPost("review/{assessmentId}")]
    [ProducesResponseType(typeof(CourseQualityAssessment), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public async Task<IActionResult> Review([FromRoute] Guid assessmentId, [FromBody] CourseQualityReviewDecision decision)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(Review));

      var result = await _courseQualityService.Review(assessmentId, decision);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(Review));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Get coverage totals across all publicly visible courses (Admin role required)")]
    [HttpGet("coverage")]
    [ProducesResponseType(typeof(CourseQualityCoverage), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public IActionResult GetCoverage()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetCoverage));

      var result = _courseQualityService.GetCoverage();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetCoverage));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Get the configured AI model and the tokens, cost and outcomes of AI runs (Admin role required)")]
    [HttpGet("ai/usage")]
    [ProducesResponseType(typeof(CourseQualityAIUsageSummary), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public IActionResult GetAIUsage()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetAIUsage));

      var result = _aiUsageService.GetSummary();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetAIUsage));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Get the grading rubric in force, with the grade distribution (Admin role required)")]
    [HttpGet("grading/rubric")]
    [ProducesResponseType(typeof(CourseGradeRubricState), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public IActionResult GetGradeRubric()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetGradeRubric));

      var result = _gradingService.GetRubricState();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetGradeRubric));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Save a grading rubric override and regrade every course (Admin role required)")]
    [HttpPut("grading/rubric")]
    [ProducesResponseType(typeof(CourseGradeRubricState), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public async Task<IActionResult> SaveGradeRubric([FromBody] CourseGradeRubric rubric)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(SaveGradeRubric));

      var result = await _gradingService.SaveRubric(rubric);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(SaveGradeRubric));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Remove the grading rubric override, back to configuration, and regrade every course (Admin role required)")]
    [HttpDelete("grading/rubric")]
    [ProducesResponseType(typeof(CourseGradeRubricState), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public async Task<IActionResult> ResetGradeRubric()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(ResetGradeRubric));

      var result = await _gradingService.ResetRubric();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(ResetGradeRubric));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "List the factor scores of every graded or gradeable course (Admin role required)")]
    [HttpGet("grading/factors")]
    [ProducesResponseType(typeof(List<CourseGradeFactorRow>), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public IActionResult ListGradeFactors()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(ListGradeFactors));

      var result = _gradingService.ListFactors();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(ListGradeFactors));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Regrade every course now (Admin role required)")]
    [HttpPost("grading/run")]
    [ProducesResponseType(typeof(CourseGradeRubricState), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public async Task<IActionResult> RunGrading()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(RunGrading));

      var result = await _gradingService.Run();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(RunGrading));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Get a course's current grade (Admin role required)")]
    [HttpGet("opportunity/{opportunityId}/grade")]
    [ProducesResponseType(typeof(CourseGradeResult), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public IActionResult GetGrade([FromRoute] Guid opportunityId)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetGrade));

      var result = _gradingService.GetGrade(opportunityId);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetGrade));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Check the health of up to 200 links from any platform, storing nothing; works whether or not course quality is on (Admin role required)")]
    [HttpPost("links/check")]
    [ProducesResponseType(typeof(LinkHealthReport), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public async Task<IActionResult> CheckLinks([FromBody] LinkHealthRequest request, [FromServices] ILinkHealthService linkHealthService)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(CheckLinks));

      var result = await linkHealthService.Check(request, HttpContext.RequestAborted);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(CheckLinks));

      return StatusCode((int)HttpStatusCode.OK, result);
    }
    #endregion Administrative Actions

    #region Organization Admin Actions
    [SwaggerOperation(Summary = "List the partner fix notes of an organization (Admin or Organization Admin roles required)")]
    [HttpGet("organization/{organizationId}/fixNotes")]
    [ProducesResponseType(typeof(List<PartnerFixNote>), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}, {Constants.Role_OrganizationAdmin}")]
    public IActionResult ListFixNotes([FromRoute] Guid organizationId)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(ListFixNotes));

      var result = _courseQualityService.ListFixNotes(organizationId);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(ListFixNotes));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Check a draft listing before it is published, storing nothing; 404 unless the pre-publish check is on (Admin or Organization Admin roles required)")]
    [HttpPost("organization/{organizationId}/draft/check")]
    [ProducesResponseType(typeof(DraftCheckResult), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}, {Constants.Role_OrganizationAdmin}")]
    public async Task<IActionResult> CheckDraft([FromRoute] Guid organizationId, [FromBody] CourseQualityDraftRequest request,
      [FromServices] ICourseQualityDraftService draftService)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(CheckDraft));

      var result = await draftService.Check(organizationId, request, HttpContext.RequestAborted);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(CheckDraft));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Get what learners said about a course; comments for Admins only (Admin or Organization Admin roles required)")]
    [HttpGet("opportunity/{opportunityId}/feedback/summary")]
    [ProducesResponseType(typeof(CourseFeedbackSummary), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}, {Constants.Role_OrganizationAdmin}")]
    public IActionResult GetFeedbackSummary([FromRoute] Guid opportunityId)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetFeedbackSummary));

      var result = _feedbackService.GetSummary(opportunityId);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetFeedbackSummary));

      return StatusCode((int)HttpStatusCode.OK, result);
    }
    #endregion Organization Admin Actions
    #endregion
  }
}
