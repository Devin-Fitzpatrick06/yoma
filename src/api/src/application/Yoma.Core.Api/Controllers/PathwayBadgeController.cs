using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Net;
using Yoma.Core.Api.Attributes;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Pathway.Badges.Interfaces;
using Yoma.Core.Domain.Pathway.Badges.Models;

namespace Yoma.Core.Api.Controllers
{
  [Route($"api/{Common.Constants.Api_Version}/pathway/badges")]
  [ApiController]
  [PathwayEnabled]
  [Authorize(Policy = Common.Constants.Authorization_Policy)]
  [SwaggerTag("(by default, User role required)")]
  public class PathwayBadgeController : ControllerBase
  {
    #region Class Variables
    private readonly ILogger<PathwayBadgeController> _logger;
    private readonly IPathwayBadgeService _pathwayBadgeService;
    #endregion

    #region Constructor
    public PathwayBadgeController(ILogger<PathwayBadgeController> logger, IPathwayBadgeService pathwayBadgeService)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _pathwayBadgeService = pathwayBadgeService ?? throw new ArgumentNullException(nameof(pathwayBadgeService));
    }
    #endregion

    #region Public Members
    #region Anonymous Actions
    [SwaggerOperation(Summary = "Get an active organisation's partner badge, computed from its course grades (Anonymous)")]
    [HttpGet("organization/{organizationId}")]
    [ProducesResponseType(typeof(PartnerBadge), (int)HttpStatusCode.OK)]
    [AllowAnonymous]
    public IActionResult GetPartnerBadge([FromRoute] Guid organizationId)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(GetPartnerBadge));

      var result = _pathwayBadgeService.GetPartnerBadge(organizationId);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(GetPartnerBadge));

      return StatusCode((int)HttpStatusCode.OK, result);
    }

    [SwaggerOperation(Summary = "List partner badges for up to 50 organisations in one call, for course cards (Anonymous)",
      Description = "ids: organisation ids, as repeated keys (?ids=a&ids=b), one comma-separated value (?ids=a,b) or both. Badges come back in the order asked, each organisation once; organisations that do not exist or are not active are left out. 400 for no ids, a malformed id or more than 50.")]
    [HttpGet("organizations")]
    [ProducesResponseType(typeof(List<PartnerBadge>), (int)HttpStatusCode.OK)]
    [AllowAnonymous]
    public IActionResult ListPartnerBadges([FromQuery] string[]? ids)
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(ListPartnerBadges));

      var result = _pathwayBadgeService.ListPartnerBadges(ids);

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(ListPartnerBadges));

      return StatusCode((int)HttpStatusCode.OK, result);
    }
    #endregion Anonymous Actions

    #region Authenticated User Based Actions
    [SwaggerOperation(Summary = "List the signed-in learner's badges, earned or not, computed from their YoID and course grades (Authenticated User)")]
    [HttpGet("me")]
    [ProducesResponseType(typeof(List<LearnerBadge>), (int)HttpStatusCode.OK)]
    [Authorize(Roles = $"{Constants.Role_User}")]
    public async Task<IActionResult> ListMine()
    {
      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Handling request {requestName}", nameof(ListMine));

      var result = await _pathwayBadgeService.ListMine();

      if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Request {requestName} handled", nameof(ListMine));

      return StatusCode((int)HttpStatusCode.OK, result);
    }
    #endregion Authenticated User Based Actions
    #endregion
  }
}
