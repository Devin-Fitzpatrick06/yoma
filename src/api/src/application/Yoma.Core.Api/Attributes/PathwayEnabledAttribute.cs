using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using System.Net;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Api.Attributes
{
  /// <summary>
  /// Production switch Pathway:Enabled (off by default). On a controller or an action, every endpoint it covers answers 404 while the
  /// switch is off, as if it did not exist. Apply it with one line: [PathwayEnabled].
  ///
  /// As a filter it runs before model binding and the action, so an anonymous endpoint and a signed-in caller both get 404. Endpoint
  /// authorization runs earlier, in the middleware, so a caller without a token gets 401 from a User endpoint unless the app also adds
  /// <see cref="PathwayEnabledApplicationBuilderExtensions.UsePathwayEnabledSwitch"/> between UseRouting and UseAuthentication.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
  public sealed class PathwayEnabledAttribute : Attribute, IResourceFilter, IOrderedFilter
  {
    #region Public Members
    /// <summary>
    /// First among the filters, so nothing else runs while the switch is off.
    /// </summary>
    public int Order => int.MinValue;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
      ArgumentNullException.ThrowIfNull(context, nameof(context));

      if (!IsEnabled(context.HttpContext.RequestServices)) context.Result = new StatusCodeResult((int)HttpStatusCode.NotFound);
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }

    /// <summary>
    /// Read on every request, so a configuration reload takes effect without a restart. Off when the options are not registered.
    /// </summary>
    public static bool IsEnabled(IServiceProvider services)
    {
      ArgumentNullException.ThrowIfNull(services, nameof(services));

      return services.GetService<IOptionsMonitor<PathwayOptions>>()?.CurrentValue.Enabled == true;
    }
    #endregion
  }

  public static class PathwayEnabledApplicationBuilderExtensions
  {
    /// <summary>
    /// Answers 404 for an endpoint marked [PathwayEnabled] while the switch is off, before authentication, so a caller without a token
    /// gets 404 rather than 401. Add after UseRouting (the endpoint must be known) and before UseAuthentication.
    /// </summary>
    public static IApplicationBuilder UsePathwayEnabledSwitch(this IApplicationBuilder app)
    {
      ArgumentNullException.ThrowIfNull(app, nameof(app));

      return app.Use(Gate);
    }

    public static async Task Gate(HttpContext context, RequestDelegate next)
    {
      ArgumentNullException.ThrowIfNull(context, nameof(context));
      ArgumentNullException.ThrowIfNull(next, nameof(next));

      if (context.GetEndpoint()?.Metadata.GetMetadata<PathwayEnabledAttribute>() != null && !PathwayEnabledAttribute.IsEnabled(context.RequestServices))
      {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        return;
      }

      await next(context);
    }
  }
}
