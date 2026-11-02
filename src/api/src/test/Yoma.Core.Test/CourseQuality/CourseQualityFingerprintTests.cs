using Xunit;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.Entity;
using Yoma.Core.Domain.Lookups.Models;
using Yoma.Core.Domain.Opportunity;
using Yoma.Core.Domain.Opportunity.Models;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;
using OpportunityModel = Yoma.Core.Domain.Opportunity.Models.Opportunity;
using LanguageModel = Yoma.Core.Domain.Lookups.Models.Language;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityFingerprintTests
  {
    private static readonly Guid SkillA = Guid.NewGuid();
    private static readonly Guid SkillB = Guid.NewGuid();

    private static OpportunityModel Listing()
    {
      var result = CleanListing();
      result.Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
      result.OrganizationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
      result.DateStart = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
      result.DateEnd = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
      result.Provider = "Open Learning";
      result.ProviderURL = "https://open.example.org";
      result.Summary = "Summary";
      result.Instructions = "Instructions";
      result.EngagementType = EngagementTypeOption.Remote;
      result.Skills = [new Skill { Id = SkillA, Name = "A" }, new Skill { Id = SkillB, Name = "B" }];
      result.Languages = [new LanguageModel { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "English" }];
      result.Countries = [new OpportunityCountryInfo { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "Kenya" }];
      return result;
    }

    public static TheoryData<string> ContentChanges => ["title", "description", "summary", "instructions", "url", "provider", "providerUrl", "dateStart", "dateEnd", "dateEndRemoved", "interval", "count", "engagement", "skills", "languages", "countries", "type"];

    [Theory]
    [MemberData(nameof(ContentChanges))]
    public void LearnerRelevantChanges_ChangeTheFingerprint(string change)
    {
      var listing = Listing();
      var before = CourseQualityFingerprint.Compute(listing);

      switch (change)
      {
        case "title": listing.Title += "!"; break;
        case "description": listing.Description += "!"; break;
        case "summary": listing.Summary += "!"; break;
        case "instructions": listing.Instructions += "!"; break;
        case "url": listing.URL += "/new"; break;
        case "provider": listing.Provider = "Other"; break;
        case "providerUrl": listing.ProviderURL = "https://other.example.org"; break;
        case "dateStart": listing.DateStart = listing.DateStart.AddDays(1); break;
        case "dateEnd": listing.DateEnd = listing.DateEnd!.Value.AddDays(1); break;
        case "dateEndRemoved": listing.DateEnd = null; break;
        case "interval": listing.CommitmentInterval = TimeIntervalOption.Month; break;
        case "count": listing.CommitmentIntervalCount = 5; break;
        case "engagement": listing.EngagementType = EngagementTypeOption.OnSite; break;
        case "skills": listing.Skills!.RemoveAt(0); break;
        case "languages": listing.Languages = [new LanguageModel { Id = Guid.NewGuid(), Name = "French" }]; break;
        case "countries": listing.Countries = [new OpportunityCountryInfo { Id = Guid.NewGuid(), Name = "Ghana" }]; break;
        case "type": listing.Type = Domain.Opportunity.Type.Event; break;
      }

      Assert.NotEqual(before, CourseQualityFingerprint.Compute(listing));
    }

    public static TheoryData<string> OperationalChanges => ["participants", "limit", "rewardCumulative", "rewardPool", "reward", "dateModified", "modifiedBy", "status", "hidden", "featured", "organizationStatus", "organizationName"];

    [Theory]
    [MemberData(nameof(OperationalChanges))]
    public void OperationalChanges_LeaveTheFingerprintAlone(string change)
    {
      var listing = Listing();
      var before = CourseQualityFingerprint.Compute(listing);

      switch (change)
      {
        case "participants": listing.ParticipantCount = 40; break;
        case "limit": listing.ParticipantLimit = 100; break;
        case "rewardCumulative": listing.ZltoRewardCumulative = 900; break;
        case "rewardPool": listing.ZltoRewardPool = 5000; break;
        case "reward": listing.ZltoReward = 10; break;
        case "dateModified": listing.DateModified = listing.DateModified.AddDays(1); break;
        case "modifiedBy": listing.ModifiedByUserId = Guid.NewGuid(); break;
        case "status": listing.Status = Status.Expired; break;
        case "hidden": listing.Hidden = true; break;
        case "featured": listing.Featured = true; break;
        case "organizationStatus": listing.OrganizationStatus = OrganizationStatus.Inactive; break;
        case "organizationName": listing.OrganizationName = "Renamed"; break;
      }

      Assert.Equal(before, CourseQualityFingerprint.Compute(listing));
    }

    [Fact]
    public void ChildOrder_DoesNotMatter()
    {
      var listing = Listing();
      var before = CourseQualityFingerprint.Compute(listing);

      listing.Skills!.Reverse();

      Assert.Equal(before, CourseQualityFingerprint.Compute(listing));
    }

    [Fact]
    public void SameInstantInAnotherOffset_IsTheSameDate()
    {
      var listing = Listing();
      var before = CourseQualityFingerprint.Compute(listing);

      listing.DateStart = listing.DateStart.ToOffset(TimeSpan.FromHours(2));

      Assert.Equal(before, CourseQualityFingerprint.Compute(listing));
    }

    [Fact]
    public void AdjacentFields_CannotCollide()
    {
      var first = Listing();
      first.Title = "ab";
      first.Description = "c";
      var second = Listing();
      second.Title = "a";
      second.Description = "bc";

      Assert.NotEqual(CourseQualityFingerprint.Compute(first), CourseQualityFingerprint.Compute(second));
    }

    [Fact]
    public void IsAFixedSizeHash()
    {
      var value = CourseQualityFingerprint.Compute(Listing());

      Assert.Equal(64, value.Length);
      Assert.Matches("^[0-9a-f]{64}$", value);
    }
  }
}
