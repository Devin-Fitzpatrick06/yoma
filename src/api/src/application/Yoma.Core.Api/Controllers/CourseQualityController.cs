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
    #endregion

    #region Constructor
    public CourseQualityController(ILogger<CourseQualityController> logger, ICourseQualityService courseQualityService)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _courseQualityService = courseQualityService ?? throw new ArgumentNullException(nameof(courseQualityService));
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
    #endregion Anonymous Actions

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
    #endregion Organization Admin Actions
    #endregion
  }
}
