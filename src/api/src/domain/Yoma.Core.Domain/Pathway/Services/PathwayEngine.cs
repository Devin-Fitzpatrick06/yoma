using Yoma.Core.Domain.Pathway.Helpers;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Services
{
  /// <summary>
  /// Pure path builder, ported from the approved prototype's buildPath: the same catalogue, request and options give the same path.
  ///
  /// Each stage takes the best-scoring steps of the kinds it holds (courses, or impact actions and tasks for a practice stage) whose
  /// skills or title match it: skill and title hits, preferred difficulty, length for the youth's weekly time, completions on Yoma, the
  /// Zeki grade where there is one, and a push down for non-English listings for an English-first youth. A practice stage with no
  /// listing matching it takes ones related to the rest of the path (a shared skill or category), else falls back to courses, and says
  /// which. A stage the youth already knows keeps one step. Steps the youth completed or
  /// started count towards the path. Near-duplicate titles are skipped, and short paths are topped up, middle stages first, to the
  /// configured minimum.
  ///
  /// The youth's own choices come first (iteration 4): pinned (locked) steps are placed before anything is scored and are never trimmed;
  /// a swap bans the replaced step and places the chosen one in the stage the replaced step held.
  /// </summary>
  public static class PathwayEngine
  {
    #region Class Variables
    private const decimal CompletedBonus = 10;
    private const decimal InProgressBonus = 5;
    private const decimal RepeatPenalty = 2;
    private static readonly List<PathwayStepType> LearningOnly = [PathwayStepType.Learning];
    #endregion

    #region Public Members
    public static PathwayEngineResult Build(PathwayCatalogue catalogue, PathwayEngineRequest request, PathwayOptions options, PathwayOutcomeSnapshot? outcomes = null)
    {
      ArgumentNullException.ThrowIfNull(catalogue, nameof(catalogue));
      ArgumentNullException.ThrowIfNull(request, nameof(request));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      var goal = request.Goal;
      var swaps = ResolveSwaps(request.Swaps);
      var locked = request.Locked.Where(o => !swaps.Banned.Contains(o)).ToHashSet();

      // a replacement goes where the step it replaced sat in the path without any swaps
      var pinned = locked.ToDictionary(o => o, _ => (int?)null);
      if (swaps.Replacements.Count > 0)
      {
        var plain = Plan(catalogue, request, options, [], locked.ToDictionary(o => o, _ => (int?)null), locked);
        foreach (var (origin, replacement) in swaps.Replacements)
        {
          var index = plain.FindIndex(o => o.Picks.Any(p => p.Course.OpportunityId == origin));
          pinned[replacement] = index < 0 ? null : index;
        }
      }

      var stages = Plan(catalogue, request, options, swaps.Banned, pinned, locked);

      var result = new PathwayEngineResult();
      for (var index = 0; index < stages.Count; index++)
      {
        var plan = stages[index];
        var next = index < stages.Count - 1 ? stages[index + 1].Stage : null;

        var ordered = plan.Picks.OrderBy(o => o.Status switch { PathwayStepStatus.Completed => 0, PathwayStepStatus.InProgress => 1, _ => 2 })
          .ThenBy(o => o.Locked ? 0 : 1)
          .ThenByDescending(o => o.Score).ThenBy(o => o.Course.OpportunityId).ToList();

        var courses = ordered.Select((o, position) => ToCourse(o, plan.Stage, next, position, goal, catalogue, request, outcomes)).ToList();
        result.Stages.Add(new PathwayStage
        {
          Order = index + 1,
          Name = plan.Stage.Name,
          Why = plan.FellBack ? $"{plan.Stage.Why} {PathwayDefaults.PracticeFallbackNote}"
            : plan.Related ? $"{plan.Stage.Why} {PathwayDefaults.PracticeRelatedNote}" : plan.Stage.Why,
          LeadsTo = next?.Name,
          Shortened = plan.Shortened,
          Courses = courses
        });
      }

      ApplyContinuation(result.Stages, goal, outcomes, options);

      var all = result.Stages.SelectMany(o => o.Courses).ToList();
      decimal Hours(PathwayCourse o) => o.Hours ?? options.DefaultCourseHours;

      result.TotalCourses = all.Count;
      result.CompletedCourses = all.Count(o => o.Status == PathwayStepStatus.Completed);
      result.TotalHours = Math.Round(all.Sum(Hours), 1, MidpointRounding.AwayFromZero);

      var remaining = all.Where(o => o.Status != PathwayStepStatus.Completed).Sum(Hours);
      result.EstimatedWeeks = (int)Math.Ceiling(remaining / Math.Max(request.Hours.HoursPerWeek, 1));

      result.ThroughLine = ThroughLine(goal);
      result.Jobs = MatchJobs(catalogue.Jobs, goal, request.CountryId, options.MaxJobs);

      return result;
    }

    /// <summary>
    /// Up to Pathway:MaxAlternatives steps that could take the place of one on the path: the same stage and kind, not already on the
    /// path and not a near duplicate of another step on it, best first. Empty when the step is not on the path.
    /// </summary>
    public static List<PathwayCourse> Alternatives(PathwayCatalogue catalogue, PathwayEngineRequest request, PathwayOptions options, PathwayEngineResult path,
      Guid opportunityId, PathwayOutcomeSnapshot? outcomes = null)
    {
      ArgumentNullException.ThrowIfNull(catalogue, nameof(catalogue));
      ArgumentNullException.ThrowIfNull(request, nameof(request));
      ArgumentNullException.ThrowIfNull(options, nameof(options));
      ArgumentNullException.ThrowIfNull(path, nameof(path));

      var stageIndex = path.Stages.FindIndex(o => o.Courses.Any(c => c.OpportunityId == opportunityId));
      if (stageIndex < 0 || stageIndex >= request.Goal.Stages.Count) return [];

      var stage = request.Goal.Stages[stageIndex];
      var next = stageIndex < request.Goal.Stages.Count - 1 ? request.Goal.Stages[stageIndex + 1] : null;
      var step = path.Stages[stageIndex].Courses.First(o => o.OpportunityId == opportunityId);
      var position = path.Stages[stageIndex].Courses.IndexOf(step);

      var onPath = path.Stages.SelectMany(o => o.Courses).Select(o => o.OpportunityId).ToHashSet();
      var stems = path.Stages.SelectMany(o => o.Courses).Where(o => o.OpportunityId != opportunityId).Select(o => PathwayMatching.Stem(o.Title)).ToHashSet(StringComparer.Ordinal);
      List<PathwayStepType> types = [step.Type];

      var picks = new List<Scored>();
      foreach (var candidate in catalogue.Courses.Where(o => o.Type == step.Type && !onPath.Contains(o.OpportunityId))
        .Select(o => Score(o, stage, stageIndex, request, options, types))
        .Where(o => o != null).Select(o => o!)
        .OrderByDescending(o => o.Score).ThenBy(o => o.Course.OpportunityId))
      {
        if (picks.Count >= options.MaxAlternatives) break;
        if (!stems.Add(PathwayMatching.Stem(candidate.Course.Title))) continue;
        picks.Add(candidate);
      }

      return [.. picks.Select(o => ToCourse(o, stage, next, position, request.Goal, catalogue, request, outcomes))];
    }

    /// <summary>
    /// Live jobs whose title fits the goal, in the youth's country when there are any there, otherwise from every country. Newest first.
    /// </summary>
    public static List<PathwayJob> MatchJobs(List<PathwayCatalogueJob> jobs, PathwayGoalOptions goal, Guid? countryId, int max)
    {
      ArgumentNullException.ThrowIfNull(jobs, nameof(jobs));
      ArgumentNullException.ThrowIfNull(goal, nameof(goal));

      var matching = jobs.Where(o => PathwayMatching.IsMatch(goal.JobsPattern, o.Title)).ToList();
      if (countryId.HasValue)
      {
        var local = matching.Where(o => o.CountryIds.Contains(countryId.Value)).ToList();
        if (local.Count > 0) matching = local;
      }

      return [.. matching.OrderByDescending(o => o.DateStart).ThenBy(o => o.OpportunityId).Take(Math.Max(max, 0)).Select(o => new PathwayJob
      {
        OpportunityId = o.OpportunityId,
        Title = o.Title,
        OrganizationName = o.OrganizationName,
        Countries = [.. o.Countries]
      })];
    }

    public static string ThroughLine(PathwayGoalOptions goal)
    {
      ArgumentNullException.ThrowIfNull(goal, nameof(goal));

      var steps = string.Join(", then ", goal.Stages.Select(o => PathwayMatching.Lower(o.Name)));
      return $"Each step adds a skill the next one uses: {steps}, and then open {PathwayMatching.Lower(goal.Label)} jobs on Yoma.";
    }

    /// <summary>
    /// The map's start: where the youth starts from, and the country they want to work in when they chose one.
    /// </summary>
    public static string StartLabel(PathwayBackgroundOptions background, string? countryName)
    {
      ArgumentNullException.ThrowIfNull(background, nameof(background));

      return string.IsNullOrWhiteSpace(countryName) ? background.Label : $"{background.Label} · {countryName.Trim()}";
    }

    /// <summary>
    /// The map's end: the goal's configured destination, or "Your first {goal} job".
    /// </summary>
    public static string DestinationLabel(PathwayGoalOptions goal)
    {
      ArgumentNullException.ThrowIfNull(goal, nameof(goal));

      return string.IsNullOrWhiteSpace(goal.Destination) ? $"Your first {PathwayMatching.Lower(goal.Label)} job" : goal.Destination.Trim();
    }

    /// <summary>
    /// The prototype's score, plus the grade and the youth's own progress. Null when the listing does not belong to the stage, or is
    /// not of a kind the stage holds.
    /// </summary>
    public static decimal? ScoreCourse(PathwayCatalogueCourse course, PathwayStageOptions stage, int stageIndex, PathwayEngineRequest request, PathwayOptions options)
    {
      return Score(course, stage, stageIndex, request, options, Types(stage))?.Score;
    }

    /// <summary>
    /// The kinds of step a stage holds; Learning when the stage does not say.
    /// </summary>
    public static List<PathwayStepType> Types(PathwayStageOptions stage)
    {
      ArgumentNullException.ThrowIfNull(stage, nameof(stage));

      return stage.Types.Count > 0 ? [.. stage.Types.Distinct()] : LearningOnly;
    }

    /// <summary>
    /// Swaps replayed in order. Every replaced step is banned unless a later swap chose it again; each replacement remembers the step
    /// that first held its place, so a chain (A for B, then B for C) puts C where A was. Swapping back to the original leaves nothing to
    /// place.
    /// </summary>
    public static (HashSet<Guid> Banned, List<(Guid Origin, Guid Replacement)> Replacements) ResolveSwaps(List<(Guid Replaced, Guid Replacement)> swaps)
    {
      ArgumentNullException.ThrowIfNull(swaps, nameof(swaps));

      var banned = new HashSet<Guid>();
      var origins = new Dictionary<Guid, Guid>();
      var order = new List<Guid>();

      foreach (var (replaced, replacement) in swaps)
      {
        if (replaced == replacement || replaced == Guid.Empty || replacement == Guid.Empty) continue;

        var origin = origins.Remove(replaced, out var first) ? first : replaced;
        order.Remove(replaced);
        banned.Add(replaced);
        banned.Remove(replacement);

        if (replacement == origin)
        {
          banned.Remove(origin);
          continue;
        }

        origins[replacement] = origin;
        order.Remove(replacement);
        order.Add(replacement);
      }

      return (banned, [.. order.Select(o => (origins[o], o))]);
    }
    #endregion

    #region Private Members
    private sealed record Scored(PathwayCatalogueCourse Course, decimal Score, List<string> Skills, PathwayStepStatus Status, bool Pinned = false, bool Locked = false);

    private sealed class StagePlan(PathwayStageOptions stage, bool shortened)
    {
      public PathwayStageOptions Stage { get; } = stage;

      public bool Shortened { get; } = shortened;

      /// <summary>
      /// A practice stage with no matching impact action or task, filled with courses instead.
      /// </summary>
      public bool FellBack { get; set; }

      /// <summary>
      /// A practice stage with no impact action or task matching it, filled with ones related to the rest of the path.
      /// </summary>
      public bool Related { get; set; }

      public List<Scored> Picks { get; } = [];

      public List<Scored> Others { get; set; } = [];
    }

    private static List<StagePlan> Plan(PathwayCatalogue catalogue, PathwayEngineRequest request, PathwayOptions options, HashSet<Guid> banned,
      Dictionary<Guid, int?> pinned, HashSet<Guid> locked)
    {
      var goal = request.Goal;
      var used = new HashSet<Guid>(banned);
      var stems = new HashSet<string>(StringComparer.Ordinal);

      var byId = new Dictionary<Guid, PathwayCatalogueCourse>();
      foreach (var course in catalogue.Courses) byId.TryAdd(course.OpportunityId, course);

      var stages = goal.Stages.Select((stage, index) => new StagePlan(stage,
        index < request.Background.StartStage || request.KnownSkills.Any(o => o.Probes.Any(p => PathwayMatching.IsMatch(stage.Pattern, p))))).ToList();
      if (stages.Count == 0) return stages;

      // the youth's own choices first: a pinned step that is no longer live cannot be shown and is left out
      foreach (var (id, target) in pinned.OrderBy(o => o.Key))
      {
        if (used.Contains(id) || !byId.TryGetValue(id, out var course)) continue;

        var index = target is { } chosen && chosen >= 0 && chosen < stages.Count ? chosen : BestStage(course, stages, request, options);
        var scored = Score(course, stages[index].Stage, index, request, options, null)
          ?? new Scored(course, 0, [], request.Statuses.GetValueOrDefault(course.OpportunityId, PathwayStepStatus.NotStarted));

        stages[index].Picks.Add(scored with { Pinned = true, Locked = locked.Contains(id) });
        used.Add(id);
        stems.Add(PathwayMatching.Stem(course.Title));
      }

      for (var index = 0; index < stages.Count; index++)
      {
        var plan = stages[index];
        var want = plan.Stage.Steps ?? (index == 0 ? options.FirstStageCourses : options.StageCourses);
        if (plan.Shortened) want = Math.Min(want, 1);

        var types = Types(plan.Stage);
        var scored = Candidates(catalogue, plan.Stage, index, request, options, used, types);
        if (scored.Count == 0 && plan.Picks.Count == 0 && !types.Contains(PathwayStepType.Learning))
        {
          // no practice listing matches the stage itself: first ones related to the rest of the path, then courses
          scored = Related(catalogue, stages, index, request, used, types);
          plan.Related = scored.Count > 0;
          if (!plan.Related)
          {
            scored = Candidates(catalogue, plan.Stage, index, request, options, used, LearningOnly);
            plan.FellBack = scored.Count > 0;
          }
        }

        // what the youth has done here, and what they pinned, counts towards the path, even past the stage's usual size
        var own = scored.Count(o => o.Status != PathwayStepStatus.NotStarted) + plan.Picks.Count;
        want = Math.Max(want, Math.Min(own, options.MaxStageCourses));

        var covered = new HashSet<string>(plan.Picks.SelectMany(o => o.Skills), StringComparer.OrdinalIgnoreCase);
        var rest = scored.ToList();
        while (plan.Picks.Count < want)
        {
          // best first, but a step that only repeats skills the stage already has gives way to one that adds a new skill
          var next = rest.Where(o => !stems.Contains(PathwayMatching.Stem(o.Course.Title)))
            .OrderByDescending(o => o.Score - (o.Skills.Count > 0 && o.Skills.All(covered.Contains) ? RepeatPenalty : 0))
            .ThenBy(o => o.Course.OpportunityId)
            .FirstOrDefault();
          if (next == null) break;

          rest.Remove(next);
          stems.Add(PathwayMatching.Stem(next.Course.Title));
          used.Add(next.Course.OpportunityId);
          covered.UnionWith(next.Skills);
          plan.Picks.Add(next);
        }

        plan.Others = rest;
      }

      TopUp(stages, used, stems, options);
      Trim(stages, options);

      return stages;
    }

    private static List<Scored> Candidates(PathwayCatalogue catalogue, PathwayStageOptions stage, int index, PathwayEngineRequest request, PathwayOptions options,
      HashSet<Guid> used, List<PathwayStepType> types)
    {
      return [.. catalogue.Courses.Where(o => !used.Contains(o.OpportunityId))
        .Select(o => Score(o, stage, index, request, options, types))
        .Where(o => o != null).Select(o => o!)
        .OrderByDescending(o => o.Score).ThenBy(o => o.Course.OpportunityId)];
    }

    /// <summary>
    /// Practice listings related to the rest of the path, for a practice stage none matches: one sharing a skill with another stage of
    /// the goal (its pattern matches the skill), or a category with the goal (its CategoriesPattern) or with a step already picked.
    /// Shared skills count most, then a shared category, then completions on Yoma; ties go by id, so the result is deterministic.
    /// </summary>
    private static List<Scored> Related(PathwayCatalogue catalogue, List<StagePlan> stages, int index, PathwayEngineRequest request, HashSet<Guid> used,
      List<PathwayStepType> types)
    {
      var goal = request.Goal;
      var others = goal.Stages.Where((_, i) => i != index).ToList();
      var picked = stages.SelectMany(o => o.Picks).SelectMany(o => o.Course.Categories).ToHashSet(StringComparer.OrdinalIgnoreCase);

      var result = new List<Scored>();
      foreach (var course in catalogue.Courses.Where(o => types.Contains(o.Type) && !used.Contains(o.OpportunityId)))
      {
        var shared = course.Skills.Where(s => others.Any(o => PathwayMatching.IsMatch(o.Pattern, s))).ToList();
        var category = course.Categories.Any(c => picked.Contains(c) || (!string.IsNullOrWhiteSpace(goal.CategoriesPattern) && PathwayMatching.IsMatch(goal.CategoriesPattern, c)));
        if (shared.Count == 0 && !category) continue;

        var status = request.Statuses.GetValueOrDefault(course.OpportunityId, PathwayStepStatus.NotStarted);
        var own = status switch { PathwayStepStatus.Completed => CompletedBonus, PathwayStepStatus.InProgress => InProgressBonus, _ => 0m };
        var language = request.EnglishFirst && !PathwayMatching.LooksEnglish(course) ? -4m : 0m;
        var score = shared.Count * 3m + (category ? 1m : 0m) + (decimal)Math.Log10(course.CompletedCount + 1) * 0.5m + language + own;

        result.Add(new Scored(course, score, shared, status));
      }

      return [.. result.OrderByDescending(o => o.Score).ThenBy(o => o.Course.OpportunityId)];
    }

    /// <summary>
    /// Where a pinned step goes when nothing says: the stage of its kind it scores best in, then any stage it matches, then the first
    /// stage of its kind, then the first stage. Ties go to the earlier stage.
    /// </summary>
    private static int BestStage(PathwayCatalogueCourse course, List<StagePlan> stages, PathwayEngineRequest request, PathwayOptions options)
    {
      int? Best(bool ofItsKind)
      {
        var best = Enumerable.Range(0, stages.Count)
          .Where(o => !ofItsKind || Types(stages[o].Stage).Contains(course.Type))
          .Select(o => (Index: o, Score: Score(course, stages[o].Stage, o, request, options, null)?.Score))
          .Where(o => o.Score.HasValue)
          .OrderByDescending(o => o.Score).ThenBy(o => o.Index)
          .FirstOrDefault();
        return best.Score.HasValue ? best.Index : null;
      }

      var kind = Enumerable.Range(0, stages.Count).FirstOrDefault(o => Types(stages[o].Stage).Contains(course.Type), 0);
      return Best(true) ?? Best(false) ?? kind;
    }

    /// <param name="types">The kinds of step the stage takes; null takes any.</param>
    private static Scored? Score(PathwayCatalogueCourse course, PathwayStageOptions stage, int stageIndex, PathwayEngineRequest request, PathwayOptions options,
      List<PathwayStepType>? types)
    {
      if (types != null && !types.Contains(course.Type)) return null;

      var skills = PathwayMatching.MatchingSkills(stage, course);
      var titleHit = PathwayMatching.IsMatch(stage.Pattern, course.Title);
      if (skills.Count == 0 && !titleHit) return null;

      // a listing whose other skills are ones the youth already has adds little
      var knownHit = skills.Count == 0 && course.Skills.Any(s => request.KnownSkills.Any(k => PathwayMatching.IsMatch(k.Pattern, s)));

      var difficulty = stage.Difficulties.Count > 0 && Same(stage.Difficulties[0], course.Difficulty) ? 2m
        : stage.Difficulties.Any(o => Same(o, course.Difficulty)) ? 1m : 0m;
      if (request.Background.Level >= 2 && Same("Beginner", course.Difficulty) && stageIndex > 0) difficulty -= 1;

      var hours = course.Hours ?? options.DefaultCourseHours;
      var maxHours = request.Hours.MaxCourseHours;
      var length = hours <= maxHours ? 1m : hours <= maxHours * 2 ? 0.3m : -1m;

      var quality = (decimal)Math.Min(1, Math.Log10(course.CompletedCount + 1) / 3.5);
      var reach = (decimal)Math.Log10(course.CompletedCount + course.StartedCount + 1) * 0.6m;
      var language = request.EnglishFirst && !PathwayMatching.LooksEnglish(course) ? -4m : 0m;
      var grade = course.Grade != null && options.GradeBonus.TryGetValue(course.Grade, out var bonus) ? bonus : 0m;

      var status = request.Statuses.GetValueOrDefault(course.OpportunityId, PathwayStepStatus.NotStarted);
      var own = status switch { PathwayStepStatus.Completed => CompletedBonus, PathwayStepStatus.InProgress => InProgressBonus, _ => 0m };

      var score = skills.Count * 2m + (titleHit ? 2.5m : -1.5m) + difficulty * 1.5m + quality * 6 + reach + length - (knownHit ? 2 : 0) + language + grade + own;

      return new Scored(course, score, skills, status);
    }

    private static bool Same(string a, string? b) => b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Under the minimum, add the next best step to stages that are not shortened, the middle stages first, one at a time.
    /// </summary>
    private static void TopUp(List<StagePlan> stages, HashSet<Guid> used, HashSet<string> stems, PathwayOptions options)
    {
      var middle = (stages.Count - 1) / 2m;
      var order = Enumerable.Range(0, stages.Count).Where(o => !stages[o].Shortened).OrderBy(o => Math.Abs(o - middle)).ThenByDescending(o => o).ToList();

      bool added;
      do
      {
        added = false;
        foreach (var index in order)
        {
          if (stages.Sum(o => o.Picks.Count) >= options.MinCourses) return;

          var plan = stages[index];
          if (plan.Picks.Count >= options.MaxStageCourses) continue;

          var next = plan.Others.FirstOrDefault(o => !used.Contains(o.Course.OpportunityId) && !stems.Contains(PathwayMatching.Stem(o.Course.Title)));
          if (next == null) continue;

          plan.Others.Remove(next);
          used.Add(next.Course.OpportunityId);
          stems.Add(PathwayMatching.Stem(next.Course.Title));
          plan.Picks.Add(next);
          added = true;
        }
      } while (added);
    }

    /// <summary>
    /// Over the maximum, drop the weakest step the youth has neither started nor pinned from the largest stage, until it fits.
    /// </summary>
    private static void Trim(List<StagePlan> stages, PathwayOptions options)
    {
      static bool Removable(Scored o) => o.Status == PathwayStepStatus.NotStarted && !o.Pinned;

      while (stages.Sum(o => o.Picks.Count) > options.MaxCourses)
      {
        var largest = stages.Where(o => o.Picks.Any(Removable)).OrderByDescending(o => o.Picks.Count).FirstOrDefault();
        if (largest == null) return;

        var weakest = largest.Picks.Where(Removable).OrderBy(o => o.Score).ThenByDescending(o => o.Course.OpportunityId).First();
        largest.Picks.Remove(weakest);
      }
    }

    private static PathwayCourse ToCourse(Scored item, PathwayStageOptions stage, PathwayStageOptions? next, int position, PathwayGoalOptions goal,
      PathwayCatalogue catalogue, PathwayEngineRequest request, PathwayOutcomeSnapshot? outcomes)
    {
      var course = item.Course;
      var skills = item.Skills.Take(3).ToList();
      if (skills.Count == 0) skills = [.. course.Skills.Take(3)];

      CourseOutcome? outcome = null;
      if (outcomes != null && outcomes.Courses.TryGetValue(course.OpportunityId, out var found)) outcome = Copy(found);

      return new PathwayCourse
      {
        OpportunityId = course.OpportunityId,
        Title = course.Title,
        OrganizationName = course.OrganizationName,
        OrganizationLogoURL = course.OrganizationLogoURL,
        Difficulty = course.Difficulty,
        Hours = course.Hours,
        URL = course.URL,
        SkillsAdded = skills,
        Reason = Reason(item.Status, course.Type, skills, stage, next, position, goal),
        Grade = course.Grade,
        GradeLabel = course.Grade == null ? null : course.GradeLabel,
        Status = item.Status,
        CompletedCount = course.CompletedCount,
        Outcome = outcome,
        Type = course.Type,
        PartnerBadge = catalogue.PartnerBadges.TryGetValue(course.OrganizationId, out var badge) ? badge : null,
        UsuallyNext = request.UsuallyNext.TryGetValue(course.OpportunityId, out var nextSteps)
          ? [.. nextSteps.Select(o => new PathwayNextStep { OpportunityId = o.OpportunityId, Title = o.Title, Share = o.Share, Count = o.Count })]
          : [],
        DropOff = StepDropOff.Collecting(),
        Locked = item.Locked
      };
    }

    private static string Reason(PathwayStepStatus status, PathwayStepType type, List<string> skills, PathwayStageOptions stage, PathwayStageOptions? next, int position,
      PathwayGoalOptions goal)
    {
      var what = skills.Count > 0 ? PathwayMatching.JoinList(skills) : $"the basics of {PathwayMatching.Lower(stage.Name)}";
      var practice = type != PathwayStepType.Learning;

      return status switch
      {
        PathwayStepStatus.Completed => $"You completed this, so it already counts towards \"{stage.Name}\" with {what}.",
        PathwayStepStatus.InProgress => $"You have started this: finishing it gives you {what} for \"{stage.Name}\".",
        _ when practice && next == null => $"Puts {what} into practice, the kind of experience employers ask {PathwayMatching.Lower(goal.Label)} candidates about.",
        _ when practice => $"Puts {what} into practice in a real project before \"{next!.Name}\".",
        _ when next == null => $"Adds {what}, the kind of skill employers list for {PathwayMatching.Lower(goal.Label)} roles.",
        _ when position == 0 => $"Starts \"{stage.Name}\" with {what}, which \"{next.Name}\" builds on.",
        _ => $"Adds {what} to \"{stage.Name}\" before you move on to \"{next.Name}\"."
      };
    }

    /// <summary>
    /// Continuation into each course from the stage before it, and the drop-off flag when it is well below the median of the other
    /// courses of the same stage. The flag asks for a review; it never changes a grade. Practice steps are left out: continuation is
    /// worked out from course starts only.
    /// </summary>
    private static void ApplyContinuation(List<PathwayStage> stages, PathwayGoalOptions goal, PathwayOutcomeSnapshot? outcomes, PathwayOptions options)
    {
      if (outcomes == null || !outcomes.Continuation.TryGetValue(goal.Key, out var byStage)) return;

      for (var index = 0; index < stages.Count; index++)
      {
        if (!byStage.TryGetValue(index, out var rates)) continue;

        foreach (var course in stages[index].Courses.Where(o => o.Type == PathwayStepType.Learning))
        {
          if (!rates.TryGetValue(course.OpportunityId, out var rate)) continue;

          course.Outcome ??= new CourseOutcome
          {
            OpportunityId = course.OpportunityId,
            MinimumGroupSize = outcomes.MinimumGroupSize,
            Note = PathwayOutcomeEngine.WithNote(PathwayOutcomeEngine.NotEnough(outcomes.MinimumGroupSize), outcomes.DataNote)
          };
          course.Outcome.ContinuationRate = rate;
          course.DropOffFlag = IsDropOff(course.OpportunityId, rate, rates, options.DropOffRatio);
        }
      }
    }

    /// <summary>
    /// Below ratio times the median of at least two siblings with a median above zero.
    /// </summary>
    public static bool IsDropOff(Guid opportunityId, decimal rate, Dictionary<Guid, decimal> stageRates, decimal ratio)
    {
      ArgumentNullException.ThrowIfNull(stageRates, nameof(stageRates));

      var siblings = stageRates.Where(o => o.Key != opportunityId).Select(o => o.Value).OrderBy(o => o).ToList();
      if (siblings.Count < 2) return false;

      var middle = siblings.Count / 2;
      var median = siblings.Count % 2 == 1 ? siblings[middle] : (siblings[middle - 1] + siblings[middle]) / 2;
      return median > 0 && rate < ratio * median;
    }

    private static CourseOutcome Copy(CourseOutcome o) => new()
    {
      OpportunityId = o.OpportunityId,
      Completers = o.Completers,
      ProgressionRate = o.ProgressionRate,
      EmploymentRate = o.EmploymentRate,
      ContinuationRate = o.ContinuationRate,
      MinimumGroupSize = o.MinimumGroupSize,
      Note = o.Note
    };
    #endregion
  }
}
