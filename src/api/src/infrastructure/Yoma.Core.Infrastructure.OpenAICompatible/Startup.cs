using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Infrastructure.OpenAICompatible.Client;
using Yoma.Core.Infrastructure.OpenAICompatible.Interfaces;
using Yoma.Core.Infrastructure.OpenAICompatible.Services;

namespace Yoma.Core.Infrastructure.OpenAICompatible
{
  public static class Startup
  {
    /// <summary>
    /// Only when AI:Provider is OpenAICompatible. Without a base URL and model nothing but the endpoint description is
    /// registered, so the keyless reader stays the provider. With both this replaces it, and the reader takes over whenever
    /// the model returns null. Call after the reader's registration. Reads IOptions&lt;CourseQualityAIOptions&gt;, which the
    /// host configures from the "AI" section.
    /// </summary>
    public static void ConfigureServices_InfrastructureCourseQualityOpenAICompatibleProvider(this IServiceCollection services, IConfiguration configuration)
    {
      var ai = configuration.GetSection(CourseQualityAIOptions.Section).Get<CourseQualityAIOptions>() ?? new CourseQualityAIOptions();
      if (ai.Provider != AIProviderKind.OpenAICompatible) return;

      var options = ai.OpenAICompatible;
      services.AddSingleton(new CourseQualityAIEndpoint(AIProviderKind.OpenAICompatible, options.Model, options.EndpointHost, options.IsLocal, options.IsConfigured));

      if (!options.IsConfigured) return;

      services.AddSingleton<IOpenAICompatibleChatClient>(provider => new OpenAICompatibleChatClient(provider.GetRequiredService<IOptions<CourseQualityAIOptions>>()));
      services.AddScoped<OpenAICompatibleCourseQualityProvider>();
      services.AddScoped<ICourseQualityAIProvider>(provider => new CourseQualityProviderFallback(
        provider.GetRequiredService<OpenAICompatibleCourseQualityProvider>(),
        ActivatorUtilities.CreateInstance<CourseQualitySourcePageReader>(provider)));
    }
  }
}
