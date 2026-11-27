using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Helpers
{
  /// <summary>
  /// What the youth already told Yoma, in the pathway's terms: their Yoma preferences (goal, time commitment, categories,
  /// self-attested skills) and their profile country. Names, not ids, so the rules stay pure.
  /// </summary>
  /// <param name="UserGoal">Yoma's UserGoal name, for example "Start a business"; null when not set.</param>
  /// <param name="CommitmentHours">The preferred maximum time per opportunity in study hours; null when not set.</param>
  /// <param name="Categories">Preferred opportunity category names.</param>
  /// <param name="Skills">The youth's skill names, self-attested and verified.</param>
  /// <param name="CountryId">The youth's profile country.</param>
  public sealed record PathwayPreferenceInput(string? UserGoal, decimal? CommitmentHours, IReadOnlyList<string> Categories, IReadOnlyList<string> Skills,
    Guid? CountryId)
  {
    public static PathwayPreferenceInput None { get; } = new(null, null, [], [], null);
  }

  /// <summary>
  /// The answers preferences give, and which questions they settle.
  /// </summary>
  public sealed class PathwayPreferenceAnswers
  {
    /// <summary>
    /// The career goal the preferences point to most; null when nothing points to one goal more than to another.
    /// </summary>
    public string? SuggestedGoalKey { get; set; }

    /// <summary>
    /// Only one career goal fits and the evidence is strong enough to skip the question.
    /// </summary>
    public bool GoalSettled { get; set; }

    public int? HoursPerWeek { get; set; }

    public List<string>? KnownSkillKeys { get; set; }

    public Guid? CountryId { get; set; }

    /// <summary>
    /// Question ids (<see cref="PathwayQuestionId"/>) answered, in question order.
    /// </summary>
    public List<string> Answered { get; set; } = [];
  }

  /// <summary>
  /// What to write back to the youth's Yoma preferences after they save their pathway answers. Only additions, and a time commitment
  /// that the youth changed.
  /// </summary>
  public sealed class PathwayPreferenceWriteBack
  {
    /// <summary>
    /// Set the commitment to this many hours per opportunity; null leaves it.
    /// </summary>
    public int? CommitmentHours { get; set; }

    /// <summary>
    /// Known-skill keys to add as self-attested skills.
    /// </summary>
    public List<string> AddSkillKeys { get; set; } = [];

    /// <summary>
    /// Category name to add; null adds none.
    /// </summary>
    public string? AddCategory { get; set; }

    /// <summary>
    /// UserGoal name to set; only when the youth has none.
    /// </summary>
    public string? SetUserGoal { get; set; }

    public bool Any => CommitmentHours.HasValue || AddSkillKeys.Count > 0 || AddCategory != null || SetUserGoal != null;
  }

  /// <summary>
  /// Pathways start from Yoma's own preferences (W3), and save answers back to them, so the next product reads them too.
  ///
  /// Reading:
  /// - Career goal: each goal scores 2 when its UserGoals hold the youth's Yoma goal, and 1 for each preferred category matching its
  ///   CategoriesPattern. The single best goal is suggested (listed first in the question). The question is skipped only when exactly
  ///   one goal scores at all and it scores 2 or more, for example "Start a business" with no category pointing elsewhere.
  /// - Hours a week: the commitment (maximum time per opportunity) picks the smallest pace whose MaxCourseHours covers it, the largest
  ///   when none does.
  /// - Known skills: every known skill whose pattern matches one of the youth's skills. Any skill on their profile settles the question,
  ///   even when none matches, because they already told Yoma what they can do.
  /// - Country: the profile country.
  /// - The starting point (background) is never in preferences, so it is always asked.
  ///
  /// Saving the first time: for every question answered from preferences the request's value is ignored (the web sends fallbacks for
  /// hidden questions) and the preference is used instead; nothing is written back for those questions.
  ///
  /// Writing back, only for answers the youth gave (never removing, never replacing a preference with a weaker one):
  /// - Time: the chosen pace's MaxCourseHours, only when the commitment is not set or points to a different pace.
  /// - Skills: each chosen known skill the youth has no matching skill for.
  /// - Categories: the first category (by name) matching the goal's CategoriesPattern, only when none of theirs matches it.
  /// - Yoma goal: the goal's first UserGoal, only when they have none.
  /// - Career goal, background and country stay in the pathway profile.
  /// </summary>
  public static class PathwayPreferences
  {
    #region Public Members
    public static PathwayPreferenceAnswers Read(PathwayPreferenceInput input, PathwayOptions options)
    {
      ArgumentNullException.ThrowIfNull(input, nameof(input));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      var answers = new PathwayPreferenceAnswers();

      var scores = options.Goals.Select((goal, order) => (goal.Key, Order: order, Score: Score(goal, input))).Where(o => o.Score > 0)
        .OrderByDescending(o => o.Score).ThenBy(o => o.Order).ToList();
      if (scores.Count > 0 && (scores.Count == 1 || scores[0].Score > scores[1].Score)) answers.SuggestedGoalKey = scores[0].Key;
      answers.GoalSettled = scores.Count == 1 && scores[0].Score >= 2;

      answers.HoursPerWeek = HoursFor(input.CommitmentHours, options)?.HoursPerWeek;

      if (input.Skills.Count > 0)
        answers.KnownSkillKeys = [.. options.KnownSkills.Where(o => input.Skills.Any(s => PathwayMatching.IsMatch(o.Pattern, s))).Select(o => o.Key)];

      answers.CountryId = input.CountryId is { } country && country != Guid.Empty ? country : null;

      if (answers.GoalSettled) answers.Answered.Add(PathwayQuestionId.Goal);
      if (answers.KnownSkillKeys != null) answers.Answered.Add(PathwayQuestionId.KnownSkills);
      if (answers.HoursPerWeek.HasValue) answers.Answered.Add(PathwayQuestionId.HoursPerWeek);
      if (answers.CountryId.HasValue) answers.Answered.Add(PathwayQuestionId.Country);

      return answers;
    }

    /// <summary>
    /// The request with every answer preferences settled taken from preferences. The web hides those questions and sends fallback
    /// values for them (no skills, no country, 5 hours), so whatever the request holds for a settled question is ignored. Answers to
    /// the questions that were asked are kept as sent.
    /// </summary>
    public static PathwayProfileRequest Fill(PathwayProfileRequest request, PathwayPreferenceAnswers answers, PathwayOptions options)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));
      ArgumentNullException.ThrowIfNull(answers, nameof(answers));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      bool Settled(string id) => answers.Answered.Contains(id);

      return new PathwayProfileRequest
      {
        GoalKey = Settled(PathwayQuestionId.Goal) ? answers.SuggestedGoalKey! : request.GoalKey,
        Background = request.Background,
        KnownSkillKeys = Settled(PathwayQuestionId.KnownSkills) ? [.. answers.KnownSkillKeys!] : [.. request.KnownSkillKeys ?? []],
        HoursPerWeek = Settled(PathwayQuestionId.HoursPerWeek) ? answers.HoursPerWeek!.Value : request.HoursPerWeek,
        CountryId = Settled(PathwayQuestionId.Country) ? answers.CountryId : request.CountryId
      };
    }

    /// <param name="categories">Every opportunity category name on Yoma.</param>
    /// <param name="fromPreferences">Question ids answered from preferences on this save: nothing is written back for them, because
    /// their value came from preferences (or was a fallback the web sent for a hidden question), not from the youth.</param>
    public static PathwayPreferenceWriteBack PlanWriteBack(PathwayPreferenceInput current, PathwayProfileRequest saved, PathwayOptions options, IReadOnlyList<string> categories,
      IReadOnlyCollection<string>? fromPreferences = null)
    {
      var skip = fromPreferences ?? [];

      ArgumentNullException.ThrowIfNull(current, nameof(current));
      ArgumentNullException.ThrowIfNull(saved, nameof(saved));
      ArgumentNullException.ThrowIfNull(options, nameof(options));
      ArgumentNullException.ThrowIfNull(categories, nameof(categories));

      var plan = new PathwayPreferenceWriteBack();

      var pace = skip.Contains(PathwayQuestionId.HoursPerWeek) ? null : options.Hours.FirstOrDefault(o => o.HoursPerWeek == saved.HoursPerWeek);
      if (pace != null && HoursFor(current.CommitmentHours, options)?.HoursPerWeek != pace.HoursPerWeek)
        plan.CommitmentHours = (int)Math.Ceiling(pace.MaxCourseHours);

      foreach (var key in skip.Contains(PathwayQuestionId.KnownSkills) ? [] : (saved.KnownSkillKeys ?? []).Distinct(StringComparer.OrdinalIgnoreCase))
      {
        var known = options.KnownSkills.FirstOrDefault(o => o.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (known == null || current.Skills.Any(s => PathwayMatching.IsMatch(known.Pattern, s))) continue;
        plan.AddSkillKeys.Add(known.Key);
      }

      var goal = skip.Contains(PathwayQuestionId.Goal) ? null : options.Goals.FirstOrDefault(o => o.Key.Equals(saved.GoalKey, StringComparison.OrdinalIgnoreCase));
      if (goal != null)
      {
        if (!string.IsNullOrWhiteSpace(goal.CategoriesPattern) && !current.Categories.Any(o => PathwayMatching.IsMatch(goal.CategoriesPattern, o)))
          plan.AddCategory = categories.Where(o => PathwayMatching.IsMatch(goal.CategoriesPattern, o)).OrderBy(o => o, StringComparer.Ordinal).FirstOrDefault();

        if (string.IsNullOrWhiteSpace(current.UserGoal)) plan.SetUserGoal = goal.UserGoals.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o));
      }

      return plan;
    }

    /// <summary>
    /// The smallest pace whose MaxCourseHours covers the hours, the largest when none does; null for no hours.
    /// </summary>
    public static PathwayHoursOptions? HoursFor(decimal? hours, PathwayOptions options)
    {
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      if (!hours.HasValue || hours.Value <= 0 || options.Hours.Count == 0) return null;

      var ordered = options.Hours.OrderBy(o => o.MaxCourseHours).ThenBy(o => o.HoursPerWeek).ToList();
      return ordered.FirstOrDefault(o => hours.Value <= o.MaxCourseHours) ?? ordered[^1];
    }
    #endregion

    #region Private Members
    private static int Score(PathwayGoalOptions goal, PathwayPreferenceInput input)
    {
      var score = !string.IsNullOrWhiteSpace(input.UserGoal) && goal.UserGoals.Any(o => o.Equals(input.UserGoal.Trim(), StringComparison.OrdinalIgnoreCase)) ? 2 : 0;
      if (!string.IsNullOrWhiteSpace(goal.CategoriesPattern)) score += input.Categories.Count(o => PathwayMatching.IsMatch(goal.CategoriesPattern, o));
      return score;
    }
    #endregion
  }
}
