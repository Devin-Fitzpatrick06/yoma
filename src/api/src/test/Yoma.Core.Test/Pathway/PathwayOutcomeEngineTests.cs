using Xunit;
using Yoma.Core.Domain.Pathway.Models;
using Yoma.Core.Domain.Pathway.Services;
using static Yoma.Core.Test.Pathway.PathwayTestData;

namespace Yoma.Core.Test.Pathway
{
  public class PathwayOutcomeEngineTests
  {
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly PathwayCatalogueCourse Marketing = Course("Principles of Marketing", ["Marketing"]);
    private static readonly PathwayCatalogueCourse Social = Course("Social Media Marketing", ["Social Media"]);
    private static readonly PathwayCatalogueCourse Plumbing = Course("Plumbing Basics", ["Plumbing"]);
    private static readonly PathwayCatalogueCourse FirstAid = Course("First Aid at Work", ["First Aid"]);

    private static List<PathwayCatalogueCourse> Courses => [Marketing, Social, Plumbing, FirstAid];

    private static List<Guid> Youth(int count) => [.. Enumerable.Range(0, count).Select(_ => Guid.NewGuid())];

    private static PathwayActivity At(Guid user, PathwayCatalogueCourse course, int day) => new(user, course.OpportunityId, Start.AddDays(day));

    private static PathwayOutcomeSnapshot Compute(PathwayOutcomeInput input, int min = 20, string? note = null) =>
      PathwayOutcomeEngine.Compute(input, Courses, Options.Goals, min, note);

    [Fact]
    public void Compute_HidesEveryRate_BelowTheMinimumGroupSize()
    {
      var youth = Youth(19);
      var input = new PathwayOutcomeInput
      {
        LearningCompletions = [.. youth.Select(o => At(o, Marketing, 1)), .. youth.Select(o => At(o, Social, 10))],
        JobCompletions = [.. youth.Select(o => new PathwayActivity(o, Guid.NewGuid(), Start.AddDays(20)))]
      };

      var outcome = Compute(input).Courses[Marketing.OpportunityId];

      Assert.Equal(19, outcome.Completers);
      Assert.Null(outcome.ProgressionRate);
      Assert.Null(outcome.EmploymentRate);
      Assert.Null(outcome.ContinuationRate);
      Assert.Equal(20, outcome.MinimumGroupSize);
      Assert.StartsWith("Not enough learners yet", outcome.Note);
    }

    [Fact]
    public void Compute_CountsOnlyLaterCoursesInTheSameGoal_AndOnlyLaterJobs()
    {
      var youth = Youth(40);
      var input = new PathwayOutcomeInput
      {
        LearningCompletions =
        [
          .. youth.Select(o => At(o, Marketing, 10)),
          // 10 go on to a marketing course, 5 took it before, 10 take a course in no shared goal
          .. youth.Take(10).Select(o => At(o, Social, 20)),
          .. youth.Skip(10).Take(5).Select(o => At(o, Social, 5)),
          .. youth.Skip(15).Take(10).Select(o => At(o, Plumbing, 20))
        ],
        // 4 jobs after the course, 6 before it
        JobCompletions = [.. youth.Take(4).Select(o => new PathwayActivity(o, Guid.NewGuid(), Start.AddDays(30))),
          .. youth.Skip(4).Take(6).Select(o => new PathwayActivity(o, Guid.NewGuid(), Start.AddDays(1)))]
      };

      var outcome = Compute(input).Courses[Marketing.OpportunityId];

      Assert.Equal(40, outcome.Completers);
      Assert.Equal(0.25m, outcome.ProgressionRate);
      Assert.Equal(0.1m, outcome.EmploymentRate);
      Assert.Contains("25% later completed another course towards the same goal", outcome.Note);
      Assert.Contains("10% later completed a job opportunity", outcome.Note);
    }

    [Fact]
    public void Compute_GivesNoProgression_ForACourseInNoGoal()
    {
      var youth = Youth(25);
      var outcome = Compute(new PathwayOutcomeInput { LearningCompletions = [.. youth.Select(o => At(o, Plumbing, 1))] }).Courses[Plumbing.OpportunityId];

      Assert.Null(outcome.ProgressionRate);
      Assert.Equal(0m, outcome.EmploymentRate);
    }

    [Fact]
    public void Compute_NeverStatesAMultiplierOrACause()
    {
      var youth = Youth(30);
      var input = new PathwayOutcomeInput
      {
        LearningCompletions = [.. youth.Select(o => At(o, Marketing, 1))],
        JobCompletions = [.. youth.Take(15).Select(o => new PathwayActivity(o, Guid.NewGuid(), Start.AddDays(9)))]
      };

      var note = Compute(input).Courses[Marketing.OpportunityId].Note;

      Assert.DoesNotMatch(@"\d+(\.\d+)?\s*[x×]\b|times|more likely", note);
      Assert.Contains("not what the course caused", note);
    }

    [Fact]
    public void Compute_CoversCompletedCoursesThatAreNoLongerListed()
    {
      var gone = Guid.NewGuid();
      var outcome = Compute(new PathwayOutcomeInput { LearningCompletions = [new(Guid.NewGuid(), gone, Start)] }).Courses[gone];

      Assert.Equal(1, outcome.Completers);
    }

    [Fact]
    public void Compute_GivesContinuationFromTheStageBefore_OnlyWithEnoughYouth()
    {
      // marketing stage 2 (Learn how marketing works) comes before stage 3 (Master the digital channels)
      var youth = Youth(20);
      var input = new PathwayOutcomeInput
      {
        LearningCompletions = [.. youth.Select(o => At(o, Marketing, 1))],
        LearningStarts = [.. youth.Take(5).Select(o => At(o, Social, 3)), .. youth.Skip(5).Take(2).Select(o => At(o, Social, -3))]
      };

      var snapshot = Compute(input);
      Assert.Equal(0.25m, snapshot.Continuation["marketing"][2][Social.OpportunityId]);
      Assert.False(snapshot.Continuation["marketing"].ContainsKey(1));

      Assert.False(Compute(input, 21).Continuation.ContainsKey("marketing"));
    }

    [Fact]
    public void Compute_AddsTheDemoDataNote_ToEveryNote()
    {
      var snapshot = Compute(new PathwayOutcomeInput { LearningCompletions = [At(Guid.NewGuid(), Marketing, 1)] }, note: "Demo data: these learners are synthetic.");

      Assert.All(snapshot.Courses.Values, o => Assert.EndsWith("Demo data: these learners are synthetic.", o.Note));
      Assert.Equal("Demo data: these learners are synthetic.", snapshot.DataNote);
    }
  }
}
