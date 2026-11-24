using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Net;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Pathway.Interfaces;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Api.Controllers
{
  /// <summary>
  /// "My Yoma": onboarding questions, the youth's answers, their path to a goal, and course outcomes. Badge endpoints in the shared
  /// contract (src/web/src/api/models/pathway.ts) are added by the badges work.
  /// </summary>
  [Route($"api/{Common.Constants.Api_Version}/pathway")]
  [ApiController]
  [Authorize(Policy = Common.Constants.Authorization_Policy)]
  [SwaggerTag("(by default, User role required)")]
  public class PathwayController : ControllerBase
  {
    #region Class Variables
    private readonly ILogger<PathwayController> _logger;
    private readonly IPathwayService _pathwayService;
    private readonly IPathwayOutcomeService _outcomeService;
    #endregion

    #region Constructor
    public PathwayController(ILogger<PathwayController> logger, IPathwayService pathwayService, IPathwayOutcomeService outcomeService)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _pathwayService = pathwayService ?? throw new ArgumentNullException(nameof(pathwayService));
      _outcomeService = outcomeService ?? throw new ArgumentNullException(nameof(outcomeService));
    }
    #endregion

    #region Public Members
    #region Anonymous Actions
    [SwaggerOperation(Summary = "Get the My Yoma onboarding questions (Anonymous)")]
    [HttpGet("questions")]
    [ProducesResponseType(typeof(PathwayQuestions), (int)HttpStatusCode.OK)]
    [AllowAnonymous]
    public IActionResult GetQuestions()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetQuestions));

      var result = _pathwayService.GetQuestions();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetQuestions));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Get what happened after youth completed a course: progression and employment, hidden below the minimum group size (Anonymous)")]
    [HttpGet("outcomes/opportunity/{opportunityId}")]
    [ProducesResponseType(typeof(CourseOutcome), (int)HttpStatusCode.OK)]
    [AllowAnonymous]
    public IActionResult GetOutcome([FromRoute] Guid opportunityId)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetOutcome));

      var result = _outcomeService.GetOutcome(opportunityId);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetOutcome));

      return StatusCode((int)HttpStatusCode.OK, result);
    }
    #endregion Anonymous Actions

    #region Authenticated User Based Actions
    [SwaggerOperation(Summary = "Get the signed-in youth's My Yoma answers; 404 when not onboarded (Authenticated User)")]
    [HttpGet("me/profile")]
    [ProducesResponseType(typeof(PathwayProfile), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_User}")]
    public IActionResult GetProfile()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetProfile));

      var result = _pathwayService.GetProfile();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetProfile));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Save the signed-in youth's My Yoma answers (Authenticated User)")]
    [HttpPut("me/profile")]
    [ProducesResponseType(typeof(PathwayProfile), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_User}")]
    public async Task<IActionResult> UpsertProfile([FromBody] PathwayProfileRequest request)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(UpsertProfile));

      var result = await _pathwayService.UpsertProfile(request);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(UpsertProfile));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Get the signed-in youth's My Yoma: YoID summary, path to their goal and matching jobs; 404 when not onboarded (Authenticated User)")]
    [HttpGet("me")]
    [ProducesResponseType(typeof(MyYoma), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_User}")]
    public async Task<IActionResult> Get()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(Get));

      var result = await _pathwayService.Get();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(Get));

      return StatusCode((int)HttpStatusCode.OK, result);
    }
    #endregion Authenticated User Based Actions
    #endregion
  }
}
