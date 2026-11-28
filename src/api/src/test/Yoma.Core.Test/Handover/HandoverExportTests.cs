using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Xunit;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Pathway.Badges.Models;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Test.Handover
{
  /// <summary>
  /// Keeps the handover package's prompts and rubrics (docs/ziqi-handover) identical to what the code runs. Each file is
  /// generated from the code's defaults; this test fails when a file and the code disagree. To refresh the files after a
  /// deliberate change, run the tests with ZIQI_HANDOVER_UPDATE=1 and commit the result.
  /// </summary>
  public class HandoverExportTests
  {
    private static readonly JsonSerializerSettings Settings = new()
    {
      Formatting = Formatting.Indented,
      Converters = [new StringEnumConverter()],
      NullValueHandling = NullValueHandling.Include
    };

    private const string AnthropicProvider = "Yoma.Core.Infrastructure.Anthropic.Services.AnthropicCourseQualityProvider, Yoma.Core.Infrastructure.Anthropic";

    public static TheoryData<string> Files =>
    [
      "rubrics/verification-checks.json",
      "rubrics/course-grading-rubric.json",
      "rubrics/job-grading-rubric.json",
      "rubrics/partner-badges.json",
      "rubrics/learner-badges.json",
      "rubrics/pathway-goals-and-questions.json",
      "prompts/course-check.system-prompt.txt",
      "prompts/course-check.output-schema.json"
    ];

    [Theory]
    [MemberData(nameof(Files))]
    public void HandoverFile_MatchesTheCode(string relativePath)
    {
      var path = Path.Combine(HandoverFolder(), relativePath);
      var expected = Normalize(Generate(relativePath));

      if (Environment.GetEnvironmentVariable("ZIQI_HANDOVER_UPDATE") == "1")
      {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, expected);
      }

      Assert.True(File.Exists(path), $"{relativePath} is missing: run the tests with ZIQI_HANDOVER_UPDATE=1");
      Assert.True(Normalize(File.ReadAllText(path)) == expected,
        $"{relativePath} no longer matches the code: run the tests with ZIQI_HANDOVER_UPDATE=1 and commit the file");
    }

    private static string Generate(string relativePath) => relativePath switch
    {
      "rubrics/verification-checks.json" => Verification(),
      "rubrics/course-grading-rubric.json" => Json(CourseQualityGradingDefaults.Rubric()),
      "rubrics/job-grading-rubric.json" => Json(CourseQualityJobGradingDefaults.Rubric()),
      "rubrics/partner-badges.json" => Json(new PartnerBadgeOptions()),
      "rubrics/learner-badges.json" => Json(new LearnerBadgeOptions()),
      "rubrics/pathway-goals-and-questions.json" => Json(PathwayDefaults.Configured(new PathwayOptions())),
      "prompts/course-check.system-prompt.txt" => Constant("SystemPrompt"),
      "prompts/course-check.output-schema.json" => Constant("OutputSchema"),
      _ => throw new ArgumentOutOfRangeException(nameof(relativePath))
    };

    // the verification checks and thresholds; the grading rubrics have their own files
    private static string Verification()
    {
      var options = JObject.FromObject(new CourseQualityOptions(), JsonSerializer.Create(Settings));
      options.Remove(nameof(CourseQualityOptions.Grading));
      if (options[nameof(CourseQualityOptions.Jobs)] is JObject jobs) jobs.Remove(nameof(CourseQualityJobOptions.Grading));
      return options.ToString(Formatting.Indented, [new StringEnumConverter()]);
    }

    private static string Json(object value) => JsonConvert.SerializeObject(value, Settings);

    private static string Constant(string name)
    {
      var type = Type.GetType(AnthropicProvider, throwOnError: true)!;
      var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} is no longer a constant on the Anthropic course quality provider");
      return (string)field.GetRawConstantValue()!;
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd() + "\n";

    private static string HandoverFolder()
    {
      for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
      {
        var candidate = Path.Combine(directory.FullName, "docs", "ziqi-handover");
        if (Directory.Exists(candidate)) return candidate;
      }
      throw new DirectoryNotFoundException("docs/ziqi-handover was not found above the test output folder");
    }
  }
}
