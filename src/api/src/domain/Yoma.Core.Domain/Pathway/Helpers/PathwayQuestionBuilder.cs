using System.Globalization;
using Yoma.Core.Domain.Lookups.Models;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Helpers
{
  /// <summary>
  /// The five onboarding questions from configuration, less those Yoma's preferences answer,, and the rules an answer must meet.
  /// </summary>
  public static class PathwayQuestionBuilder
  {
    #region Public Members
    /// <summary>
    /// Option keys: the goal key, the background name, the known skill key, the hours as a number ("2") and the country id, with
    /// <see cref="PathwayDefaults.AnywhereKey"/> for anywhere (sent as a null countryId).
    /// </summary>
    /// <param name="preferences">What Yoma's own preferences already answer, for a signed-in youth without a path: those questions are
    /// left out and listed in AnsweredFromPreferences, and a suggested career goal is listed first and named in the hint.</param>
    public static PathwayQuestions Build(PathwayOptions options, List<Country> countries, PathwayPreferenceAnswers? preferences = null)
    {
      ArgumentNullException.ThrowIfNull(options, nameof(options));
      ArgumentNullException.ThrowIfNull(countries, nameof(countries));

      var suggested = preferences?.SuggestedGoalKey == null ? null : options.Goals.FirstOrDefault(o => o.Key.Equals(preferences.SuggestedGoalKey, StringComparison.OrdinalIgnoreCase));
      var goals = suggested == null ? options.Goals : [suggested, .. options.Goals.Where(o => o != suggested)];

      var questions = new PathwayQuestions
      {
        Questions =
        [
          new()
          {
            Id = PathwayQuestionId.Goal,
            Title = "Where do you want to be?",
            Hint = suggested == null
              ? "Pick the work you would most like to be doing. You can change it later."
              : $"From your Yoma preferences, {PathwayMatching.Lower(suggested.Label)} looks like a fit. Pick it or any other. You can change it later.",
            Options = [.. goals.Select(o => new PathwayOption { Key = o.Key, Label = o.Label, Description = o.Blurb })]
          },
          new()
          {
            Id = PathwayQuestionId.Background,
            Title = "Where are you starting from?",
            Hint = "This sets how far back your path starts.",
            Options = [.. options.Backgrounds.Select(o => new PathwayOption { Key = o.Background.ToString(), Label = o.Label, Description = o.Description })]
          },
          new()
          {
            Id = PathwayQuestionId.KnownSkills,
            Title = "What can you already do?",
            Hint = "Choose any that apply. We will skip what you know.",
            MultiSelect = true,
            Options = [.. options.KnownSkills.Select(o => new PathwayOption { Key = o.Key, Label = o.Label, Description = null })]
          },
          new()
          {
            Id = PathwayQuestionId.HoursPerWeek,
            Title = "How much time can you give each week?",
            Hint = "We use this to choose course lengths and estimate when you reach your goal.",
            Options = [.. options.Hours.Select(o => new PathwayOption { Key = o.HoursPerWeek.ToString(CultureInfo.InvariantCulture), Label = o.Label, Description = o.Description })]
          },
          new()
          {
            Id = PathwayQuestionId.Country,
            Title = "Where do you want to work?",
            Hint = "We match jobs on Yoma in that country.",
            Options =
            [
              .. countries.Select(o => new PathwayOption { Key = o.Id.ToString(), Label = o.Name, Description = null }),
              new PathwayOption { Key = PathwayDefaults.AnywhereKey, Label = "Somewhere else", Description = "We show jobs from every country" }
            ]
          }
        ]
      };

      if (preferences == null || preferences.Answered.Count == 0) return questions;

      questions.AnsweredFromPreferences = [.. questions.Questions.Select(o => o.Id).Where(preferences.Answered.Contains)];
      questions.Questions.RemoveAll(o => preferences.Answered.Contains(o.Id));
      return questions;
    }

    /// <summary>
    /// Plain messages, empty when the answers can be saved.
    /// </summary>
    public static List<string> Validate(PathwayProfileRequest? request, PathwayOptions options, Func<Guid, bool> countryExists)
    {
      ArgumentNullException.ThrowIfNull(options, nameof(options));
      ArgumentNullException.ThrowIfNull(countryExists, nameof(countryExists));

      if (request == null) return ["Answers are required"];

      var errors = new List<string>();

      if (string.IsNullOrWhiteSpace(request.GoalKey) || !options.Goals.Any(o => o.Key.Equals(request.GoalKey, StringComparison.OrdinalIgnoreCase)))
        errors.Add("Choose one of the goals offered");

      if (!Enum.IsDefined(request.Background) || !options.Backgrounds.Any(o => o.Background == request.Background))
        errors.Add("Choose where you are starting from");

      var unknown = (request.KnownSkillKeys ?? []).Where(o => !options.KnownSkills.Any(k => k.Key.Equals(o, StringComparison.OrdinalIgnoreCase))).ToList();
      if (unknown.Count > 0) errors.Add($"Unknown skill{(unknown.Count == 1 ? string.Empty : "s")}: {string.Join(", ", unknown)}");

      if (!options.Hours.Any(o => o.HoursPerWeek == request.HoursPerWeek))
        errors.Add($"Hours a week must be one of {string.Join(", ", options.Hours.Select(o => o.HoursPerWeek))}");

      if (request.CountryId.HasValue && (request.CountryId == Guid.Empty || !countryExists(request.CountryId.Value)))
        errors.Add("Choose a country offered, or anywhere");

      return errors;
    }
    #endregion
  }
}
