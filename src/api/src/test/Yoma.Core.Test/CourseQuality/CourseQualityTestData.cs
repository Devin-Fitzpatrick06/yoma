using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Entity;
using Yoma.Core.Domain.Lookups.Models;
using Yoma.Core.Domain.Opportunity;
using Yoma.Core.Domain.Opportunity.Models;

namespace Yoma.Core.Test.CourseQuality
{
  internal static class CourseQualityTestData
  {
    public static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public static Opportunity CleanListing() => new()
    {
      Id = Guid.NewGuid(),
      Title = "Introduction to Data Analysis",
      Description = "Learn to clean, explore and present data through short practical exercises.",
      Type = Domain.Opportunity.Type.Learning,
      OrganizationId = Guid.NewGuid(),
      OrganizationName = "Open Learning Org",
      OrganizationStatus = OrganizationStatus.Active,
      Status = Status.Active,
      URL = "https://courses.example.org/data",
      DateStart = Now.AddDays(-10),
      DateEnd = Now.AddDays(30),
      DateModified = Now.AddDays(-2),
      CommitmentInterval = TimeIntervalOption.Week,
      CommitmentIntervalCount = 4,
      Skills = [new Skill { Name = "Data analysis" }],
      Languages = [new Domain.Lookups.Models.Language { Name = "English" }],
      Countries = [new OpportunityCountryInfo()]
    };

    public static CourseQualityLinkCheckResult WorkingLink(string? text = "Course page text") =>
      CourseQualityLinkCheckResult.Reachable(200, "https://courses.example.org/data", text);

    public static CourseQualityAIResult Ai(decimal confidence = 0.9m, int score = 100) => new()
    {
      IsAI = true,
      Model = "test-model",
      Confidence = confidence,
      Summary = "Listing matches the page.",
      StrongMatch = true,
      DatesStartConfirmed = true,
      DatesEndConfirmed = true,
      Cost = new LearnerCardCost { Type = CourseCostType.Unknown },
      Criteria =
      [
        .. Enum.GetValues<CourseQualityCriterion>().Select(o => new CourseQualityAICriterionResult { Criterion = o, Score = score, Reason = "AI reason", Recommendation = "AI recommendation" })
      ]
    };
  }
}
