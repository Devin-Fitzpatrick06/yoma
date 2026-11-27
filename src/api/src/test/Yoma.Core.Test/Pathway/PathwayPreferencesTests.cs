using Xunit;
using Yoma.Core.Domain.Lookups.Models;
using Yoma.Core.Domain.Pathway.Helpers;
using Yoma.Core.Domain.Pathway.Models;
using static Yoma.Core.Test.Pathway.PathwayTestData;

namespace Yoma.Core.Test.Pathway
{
  /// <summary>
  /// W3: pathways read Yoma's own preferences, ask only what is missing and save answers back.
  /// </summary>
  public class PathwayPreferencesTests
  {
    private static readonly Guid Nigeria = Guid.Parse("7c3d2a1e-0000-4000-8000-000000000001");

    private static readonly List<string> Categories =
    [
      "AI, Data and Analytics", "Business and Entrepreneurship", "Business, Finance & Marketing", "Health and Care", "Technology and Digitization", "Other"
    ];

    private static PathwayPreferenceInput Input(string? goal = null, decimal? hours = null, string[]? categories = null, string[]? skills = null, Guid? country = null) =>
      new(goal, hours, [.. categories ?? []], [.. skills ?? []], country);

    private static PathwayQuestions Questions(PathwayPreferenceAnswers? answers) =>
      PathwayQuestionBuilder.Build(Options, [new Country { Id = Nigeria, Name = "Nigeria", CodeAlpha2 = "NG" }], answers);

    [Fact]
    public void Read_NothingSet_AnswersNothing_AndEveryQuestionIsAsked()
    {
      var answers = PathwayPreferences.Read(PathwayPreferenceInput.None, Options);

      Assert.Empty(answers.Answered);
      Assert.Null(answers.SuggestedGoalKey);

      var questions = Questions(answers);
      Assert.Equal(5, questions.Questions.Count);
      Assert.Empty(questions.AnsweredFromPreferences);
      Assert.Empty(Questions(null).AnsweredFromPreferences);
    }

    [Fact]
    public void Read_FullPreferences_LeaveAtMostTwoQuestions()
    {
      var answers = PathwayPreferences.Read(Input("Get a job", 12, ["AI, Data and Analytics"], ["Microsoft Excel", "Teamwork"], Nigeria), Options);

      Assert.Equal("data", answers.SuggestedGoalKey);
      Assert.False(answers.GoalSettled);
      Assert.Equal(5, answers.HoursPerWeek);
      Assert.Equal(["excel"], answers.KnownSkillKeys);
      Assert.Equal(Nigeria, answers.CountryId);

      var questions = Questions(answers);
      Assert.Equal([PathwayQuestionId.Goal, PathwayQuestionId.Background], questions.Questions.Select(o => o.Id));
      Assert.Equal([PathwayQuestionId.KnownSkills, PathwayQuestionId.HoursPerWeek, PathwayQuestionId.Country], questions.AnsweredFromPreferences);

      // the suggested career is pre-selected: listed first and named in the hint, not skipped
      var goal = questions.Questions[0];
      Assert.Equal("data", goal.Options[0].Key);
      Assert.Equal(Options.Goals.Count, goal.Options.Count);
      Assert.Contains("data analyst", goal.Hint);
    }

    [Fact]
    public void Read_StartABusiness_SettlesTheCareer_WhenNothingPointsElsewhere()
    {
      var settled = PathwayPreferences.Read(Input("Start a business", categories: ["Business and Entrepreneurship"]), Options);
      Assert.Equal("business", settled.SuggestedGoalKey);
      Assert.True(settled.GoalSettled);
      Assert.Equal([PathwayQuestionId.Goal], settled.Answered);
      Assert.Equal([PathwayQuestionId.Goal], Questions(settled).AnsweredFromPreferences);

      var mixed = PathwayPreferences.Read(Input("Start a business", categories: ["Technology and Digitization"]), Options);
      Assert.Equal("business", mixed.SuggestedGoalKey);
      Assert.False(mixed.GoalSettled);
    }

    [Fact]
    public void Read_GetAJobAlone_SuggestsNoCareer()
    {
      var answers = PathwayPreferences.Read(Input("Get a job"), Options);

      Assert.Null(answers.SuggestedGoalKey);
      Assert.False(answers.GoalSettled);
      Assert.Equal(Options.Goals[0].Key, Questions(answers).Questions[0].Options[0].Key);
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(6, 2)]
    [InlineData(10, 5)]
    [InlineData(40, 10)]
    [InlineData(400, 10)]
    public void HoursFor_PicksTheSmallestPaceThatCoversTheCommitment(int hours, int expected)
    {
      Assert.Equal(expected, PathwayPreferences.HoursFor(hours, Options)!.HoursPerWeek);
    }

    [Fact]
    public void Read_SkillsOnTheProfileSettleTheSkillsQuestion_EvenWhenNoneMatch()
    {
      var answers = PathwayPreferences.Read(Input(skills: ["Carpentry"]), Options);

      Assert.Equal([], answers.KnownSkillKeys);
      Assert.Equal([PathwayQuestionId.KnownSkills], answers.Answered);
    }

    [Fact]
    public void Fill_IgnoresTheWebsFallbacksForHiddenQuestions_AndKeepsTheAskedAnswers()
    {
      var answers = PathwayPreferences.Read(Input("Start a business", 40, skills: ["Bookkeeping"], country: Nigeria), Options);

      // what the web sends for hidden questions: no skills, anywhere, 5 hours, and some goal
      var sent = new PathwayProfileRequest { GoalKey = "web", Background = PathwayBackground.SomeStudy, KnownSkillKeys = [], HoursPerWeek = 5, CountryId = null };
      var filled = PathwayPreferences.Fill(sent, answers, Options);

      Assert.Equal("business", filled.GoalKey);
      Assert.Equal(PathwayBackground.SomeStudy, filled.Background);
      Assert.Equal(["bookkeeping"], filled.KnownSkillKeys);
      Assert.Equal(10, filled.HoursPerWeek);
      Assert.Equal(Nigeria, filled.CountryId);
      Assert.Empty(PathwayQuestionBuilder.Validate(filled, Options, _ => true));

      var asked = PathwayPreferences.Fill(sent, PathwayPreferences.Read(PathwayPreferenceInput.None, Options), Options);
      Assert.Equal("web", asked.GoalKey);
      Assert.Equal(5, asked.HoursPerWeek);
      Assert.Null(asked.CountryId);
    }

    [Fact]
    public void PlanWriteBack_NeverWritesValuesThatCameFromPreferences()
    {
      // the youth set a 3-hour commitment and no skills in Yoma; the web sent 5 hours and no skills for the hidden questions
      var current = Input(hours: 3, skills: ["Carpentry"]);
      var answers = PathwayPreferences.Read(current, Options);
      var sent = new PathwayProfileRequest { GoalKey = "marketing", Background = PathwayBackground.FinishedSchool, KnownSkillKeys = [], HoursPerWeek = 5 };
      var saved = PathwayPreferences.Fill(sent, answers, Options);

      var plan = PathwayPreferences.PlanWriteBack(current, saved, Options, Categories, answers.Answered);

      Assert.Null(plan.CommitmentHours);
      Assert.Empty(plan.AddSkillKeys);
      // the career was asked, so its category and Yoma goal are written back
      Assert.Equal("Business, Finance & Marketing", plan.AddCategory);
      Assert.Equal("Get a job", plan.SetUserGoal);

      // the same request without the preferences behind it would have written the fallback 5 hours back
      Assert.Equal(15, PathwayPreferences.PlanWriteBack(Input(), sent, Options, Categories).CommitmentHours);
    }

    [Fact]
    public void PlanWriteBack_AddsOnlyWhatIsMissing_AndKeepsAMatchingCommitment()
    {
      var saved = new PathwayProfileRequest { GoalKey = "data", Background = PathwayBackground.Degree, KnownSkillKeys = ["excel", "communication"], HoursPerWeek = 5 };

      var empty = PathwayPreferences.PlanWriteBack(PathwayPreferenceInput.None, saved, Options, Categories);
      Assert.Equal(15, empty.CommitmentHours);
      Assert.Equal(["excel", "communication"], empty.AddSkillKeys);
      Assert.Equal("AI, Data and Analytics", empty.AddCategory);
      Assert.Equal("Get a job", empty.SetUserGoal);
      Assert.True(empty.Any);

      var set = PathwayPreferences.PlanWriteBack(Input("Volunteer / make an impact", 12, ["AI, Data and Analytics"], ["Microsoft Excel", "Communication"]), saved, Options, Categories);
      Assert.Null(set.CommitmentHours);
      Assert.Empty(set.AddSkillKeys);
      Assert.Null(set.AddCategory);
      Assert.Null(set.SetUserGoal);
      Assert.False(set.Any);

      // a different pace the youth chose while editing replaces the commitment
      Assert.Equal(40, PathwayPreferences.PlanWriteBack(Input(hours: 12), new PathwayProfileRequest { GoalKey = "data", HoursPerWeek = 10 }, Options, Categories).CommitmentHours);
    }
  }
}
