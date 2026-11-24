using Xunit;
using Yoma.Core.Domain.Lookups.Models;
using Yoma.Core.Domain.Pathway.Helpers;
using Yoma.Core.Domain.Pathway.Models;
using static Yoma.Core.Test.Pathway.PathwayTestData;

namespace Yoma.Core.Test.Pathway
{
  public class PathwayQuestionBuilderTests
  {
    private static readonly Country Nigeria = new() { Id = Guid.NewGuid(), Name = "Nigeria", CodeAlpha2 = "NG", CodeAlpha3 = "NGA", CodeNumeric = "566" };

    private static PathwayProfileRequest Valid() => new()
    {
      GoalKey = "data",
      Background = PathwayBackground.SomeStudy,
      KnownSkillKeys = ["excel"],
      HoursPerWeek = 5,
      CountryId = Nigeria.Id
    };

    [Fact]
    public void Build_AsksTheFiveQuestions_InTheContractsOrder_WithItsKeys()
    {
      var questions = PathwayQuestionBuilder.Build(Options, [Nigeria]).Questions;

      Assert.Equal(["goal", "background", "knownSkills", "hoursPerWeek", "country"], questions.Select(o => o.Id));
      Assert.Equal(["marketing", "sales", "web", "data", "business", "health"], questions[0].Options.Select(o => o.Key));
      Assert.Equal(Enum.GetNames<PathwayBackground>(), questions[1].Options.Select(o => o.Key));
      Assert.True(questions[2].MultiSelect);
      Assert.All(questions.Where(o => o.Id != "knownSkills"), o => Assert.False(o.MultiSelect));
      Assert.Equal(["2", "5", "10"], questions[3].Options.Select(o => o.Key));
      Assert.Equal([Nigeria.Id.ToString(), "anywhere"], questions[4].Options.Select(o => o.Key));
      Assert.All(questions, o => Assert.All(o.Options, p => Assert.False(string.IsNullOrWhiteSpace(p.Label))));
    }

    [Fact]
    public void Validate_AcceptsAnswersFromTheQuestions()
    {
      Assert.Empty(PathwayQuestionBuilder.Validate(Valid(), Options, id => id == Nigeria.Id));

      var anywhere = Valid();
      anywhere.CountryId = null;
      anywhere.KnownSkillKeys = [];
      Assert.Empty(PathwayQuestionBuilder.Validate(anywhere, Options, _ => false));
    }

    [Fact]
    public void Validate_RejectsAnswersTheQuestionsDoNotOffer()
    {
      static List<string> With(Action<PathwayProfileRequest> change)
      {
        var request = Valid();
        change(request);
        return PathwayQuestionBuilder.Validate(request, Options, id => id == Nigeria.Id);
      }

      Assert.Contains(With(o => o.GoalKey = "astronaut"), e => e.Contains("goals"));
      Assert.Contains(With(o => o.Background = (PathwayBackground)99), e => e.Contains("starting from"));
      Assert.Contains(With(o => o.KnownSkillKeys = ["excel", "juggling"]), e => e.Contains("juggling"));
      Assert.Contains(With(o => o.HoursPerWeek = 7), e => e.Contains("2, 5, 10"));
      Assert.Contains(With(o => o.CountryId = Guid.NewGuid()), e => e.Contains("country"));
      Assert.Equal(["Answers are required"], PathwayQuestionBuilder.Validate(null, Options, _ => true));
    }
  }
}
