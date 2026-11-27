using Microsoft.AspNetCore.Authorization;
using Yoma.Core.Api.Attributes;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Net;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Pathway.Interfaces;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Api.Controllers
{
  /// <summary>
  /// "My Yoma" iteration 4: lock and swap steps, step events for drop-off (W5), pathway health, and the YoID "My path" card.
  /// </summary>
  [Route($"api/{Common.Constants.Api_Version}/pathway")]
  [PathwayEnabled]
  [ApiController]
  [Authorize(Policy = Common.Constants.Authorization_Policy)]
  [SwaggerTag("(by default, User role required)")]
  public class PathwayStepController : ControllerBase
  {
    #region Class Variables
    private readonly ILogger<PathwayStepController> _logger;
    private readonly IPathwayStepService _stepService;
    #endregion

    #region Constructor
    public PathwayStepController(ILogger<PathwayStepController> logger, IPathwayStepService stepService)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _stepService = stepService ?? throw new ArgumentNullException(nameof(stepService));
    }
    #endregion

    #region Public Members
    #region Administrative Actions
    [SwaggerOperation(Summary = "Get pathway health: steps tracked, events recorded and the aggregate drop-off, collecting until the pilot (Admin role required)")]
    [HttpGet("health")]
    [ProducesResponseType(typeof(PathwayHealth), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_Admin}")]
    public IActionResult GetHealth()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetHealth));

      var result = _stepService.GetHealth();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetHealth));

      return StatusCode((int)HttpStatusCode.OK, result);
    }
    #endregion Administrative Actions

    #region Authenticated User Based Actions
    [SwaggerOperation(Summary = "Pin or unpin a step on the signed-in youth's path; returns the rebuilt path (Authenticated User)")]
    [HttpPut("me/steps/{opportunityId}/lock")]
    [ProducesResponseType(typeof(MyYoma), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_User}")]
    public async Task<IActionResult> SetLocked([FromRoute] Guid opportunityId, [FromBody] PathwayStepLockRequest request)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(SetLocked));

      var result = await _stepService.SetLocked(opportunityId, request);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(SetLocked));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Get up to three alternatives for a step on the signed-in youth's path (Authenticated User)")]
    [HttpGet("me/steps/{opportunityId}/alternatives")]
    [ProducesResponseType(typeof(List<PathwayCourse>), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_User}")]
    public async Task<IActionResult> ListAlternatives([FromRoute] Guid opportunityId)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(ListAlternatives));

      var result = await _stepService.ListAlternatives(opportunityId);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(ListAlternatives));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Swap a step on the signed-in youth's path for one of its alternatives; returns the rebuilt path (Authenticated User)")]
    [HttpPost("me/steps/{opportunityId}/swap")]
    [ProducesResponseType(typeof(MyYoma), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_User}")]
    public async Task<IActionResult> Swap([FromRoute] Guid opportunityId, [FromBody] PathwayStepSwapRequest request)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(Swap));

      var result = await _stepService.Swap(opportunityId, request);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(Swap));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "Record a step event (Shown, Started, Completed, Swapped, Dismissed) for drop-off collection (Authenticated User)")]
    [HttpPost("me/events")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize(Roles = $"{Constants.Role_User}")]
    public async Task<IActionResult> RecordEvent([FromBody] PathwayStepEventRequest request)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(RecordEvent));

      await _stepService.RecordEvent(request);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(RecordEvent));

      return StatusCode((int)HttpStatusCode.NoContent);
    }

    [SwaggerOperation(Summary = "Get the signed-in youth's path summary for the YoID \"My path\" card; 404 when not onboarded (Authenticated User)")]
    [HttpGet("yoid/me")]
    [ProducesResponseType(typeof(MyPathSummary), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_User}")]
    public async Task<IActionResult> GetSummary()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetSummary));

      var result = await _stepService.GetSummary();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetSummary));

      return StatusCode((int)HttpStatusCode.OK, result);
    }
    #endregion Authenticated User Based Actions
    #endregion
  }
}
