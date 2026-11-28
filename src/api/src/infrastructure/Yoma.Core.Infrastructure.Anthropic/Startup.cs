using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Infrastructure.Anthropic.Client;
using Yoma.Core.Infrastructure.Anthropic.Interfaces;
using Yoma.Core.Infrastructure.Anthropic.Models;
using Yoma.Core.Infrastructure.Anthropic.Services;

namespace Yoma.Core.Infrastructure.Anthropic
{
  public static class Startup
  {
    public static void ConfigureServices_CourseQualityAIProvider(this IServiceCollection services, IConfiguration configuration)
    {
      services.Configure<AnthropicOptions>(options => configuration.GetSection(AnthropicOptions.Section).Bind(options));
    }

    /// <summary>
    /// Only when AI:Provider is Anthropic (the default). With no key nothing but the endpoint description is registered, so the
    /// keyless reader stays the provider and the SDK client is never constructed. With a key this replaces it, and the reader
    /// takes over whenever Claude returns null. Call after the reader's registration.
    /// </summary>
    public static void ConfigureServices_InfrastructureCourseQualityAIProvider(this IServiceCollection services, IConfiguration configuration)
    {
      var ai = configuration.GetSection(CourseQualityAIOptions.Section).Get<CourseQualityAIOptions>() ?? new CourseQualityAIOptions();
      if (ai.Provider != AIProviderKind.Anthropic) return;

      var options = configuration.GetSection(AnthropicOptions.Section).Get<AnthropicOptions>() ?? new AnthropicOptions();
      services.AddSingleton(new CourseQualityAIEndpoint(AIProviderKind.Anthropic, options.Model, null, false, options.HasApiKey));

      if (!options.HasApiKey) return;

      services.AddSingleton<IAnthropicMessageClient>(provider => new AnthropicMessageClient(provider.GetRequiredService<IOptions<AnthropicOptions>>()));
      services.AddScoped<AnthropicCourseQualityProvider>();
      services.AddScoped<ICourseQualityAIProvider>(provider => new CourseQualityProviderFallback(
        provider.GetRequiredService<AnthropicCourseQualityProvider>(),
        ActivatorUtilities.CreateInstance<CourseQualitySourcePageReader>(provider)));
    }
  }
}
