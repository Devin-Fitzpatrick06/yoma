using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;
using Xunit;
using Yoma.Core.Api.Controllers;
using Yoma.Core.Domain;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Pathway.Helpers;
using Yoma.Core.Domain.Pathway.Interfaces;
using Yoma.Core.Domain.Pathway.Models;
using Yoma.Core.Domain.Pathway.Services;

namespace Yoma.Core.Test.Pathway
{
  /// <summary>
  /// The stored step store replaces the no-op, the step endpoints sit where the web contract expects them with the right roles, and
  /// the interim alternatives adapter stays inside the step's stage.
  /// </summary>
  public class PathwayStepWiringTests
  {
    #region Registration
    [Fact]
    public void Domain_RegistersTheStoredStepStore_OverTheNoOp()
    {
      var services = new ServiceCollection();
      services.ConfigureServices_DomainServices();

      Assert.Equal(typeof(PathwayStepStore), services.Last(o => o.ServiceType == typeof(IPathwayStepStore)).ImplementationType);
      Assert.Contains(services, o => o.ServiceType == typeof(IPathwayStepService) && o.ImplementationType == typeof(PathwayStepService));
    }
    #endregion

    #region Endpoints
    [Theory]
    [InlineData(nameof(PathwayStepController.SetLocked), "PUT", "me/steps/{opportunityId}/lock", Constants.Role_User)]
    [InlineData(nameof(PathwayStepController.ListAlternatives), "GET", "me/steps/{opportunityId}/alternatives", Constants.Role_User)]
    [InlineData(nameof(PathwayStepController.Swap), "POST", "me/steps/{opportunityId}/swap", Constants.Role_User)]
    [InlineData(nameof(PathwayStepController.RecordEvent), "POST", "me/events", Constants.Role_User)]
    [InlineData(nameof(PathwayStepController.GetHealth), "GET", "health", Constants.Role_Admin)]
    [InlineData(nameof(PathwayStepController.GetSummary), "GET", "yoid/me", Constants.Role_User)]
    public void Endpoint_MatchesTheContract(string action, string verb, string template, string role)
    {
      var method = typeof(PathwayStepController).GetMethod(action)!;
      var http = method.GetCustomAttributes<HttpMethodAttribute>().Single();

      Assert.Contains(verb, http.HttpMethods);
      Assert.Equal(template, http.Template);
      Assert.Equal(role, method.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
      Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
      Assert.EndsWith("/pathway", typeof(PathwayStepController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Fact]
    public async Task RecordEvent_AnswersNoContent()
    {
      var service = new Mock<IPathwayStepService>();
      var controller = new PathwayStepController(Microsoft.Extensions.Logging.Abstractions.NullLogger<PathwayStepController>.Instance, service.Object);

      var result = await controller.RecordEvent(new PathwayStepEventRequest { OpportunityId = Guid.NewGuid(), Event = PathwayStepEventType.Shown });

      Assert.Equal(204, Assert.IsType<StatusCodeResult>(result).StatusCode);
      service.Verify(o => o.RecordEvent(It.IsAny<PathwayStepEventRequest>()), Times.Once);
    }
    #endregion

  }
}
