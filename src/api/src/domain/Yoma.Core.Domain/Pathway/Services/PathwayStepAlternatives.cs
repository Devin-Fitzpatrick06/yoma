using Microsoft.Extensions.Options;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Pathway.Helpers;
using Yoma.Core.Domain.Pathway.Interfaces;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Services
{
  /// <summary>
  /// Minimal alternatives until the engine's own lands (see <see cref="IPathwayStepAlternatives"/>): the catalogue's courses that the
  /// engine would score for the same stage, best first, skipping anything on the path or a near-duplicate title of a path step.
  /// </summary>
  public sealed class PathwayStepAlternatives : IPathwayStepAlternatives
  {
    #region Class Variables
    public const int MaxAlternatives = 3;

    private readonly PathwayOptions _options;
    private readonly IPathwayCatalogueService _catalogueService;
    #endregion

    #region Constructor
    public PathwayStepAlternatives(IOptions<PathwayOptions> options, IPathwayCatalogueService catalogueService)
    {
      _options = PathwayDefaults.Configured(options?.Value ?? throw new ArgumentNullException(nameof(options)));
      _catalogueService = catalogueService ?? throw new ArgumentNullException(nameof(catalogueService));
    }
    #endregion

    #region Public Members
    public List<PathwayCourse> List(MyYoma myYoma, Guid opportunityId)
    {
      ArgumentNullException.ThrowIfNull(myYoma, nameof(myYoma));

      var stage = myYoma.Stages.FirstOrDefault(o => o.Courses.Any(c => c.OpportunityId == opportunityId))
        ?? throw new EntityNotFoundException("This step is not on your path");

      var goal = _options.Goals.FirstOrDefault(o => o.Key.Equals(myYoma.Profile.GoalKey, StringComparison.OrdinalIgnoreCase));
      var index = stage.Order - 1;
      if (goal == null || index < 0 || index >= goal.Stages.Count) return [];

      var stageOptions = goal.Stages[index];
      var profile = myYoma.Profile;
      var request = new PathwayEngineRequest
      {
        Goal = goal,
        Background = _options.Backgrounds.FirstOrDefault(o => o.Background == profile.Background) ?? _options.Backgrounds[0],
        Hours = _options.Hours.FirstOrDefault(o => o.HoursPerWeek == profile.HoursPerWeek)
          ?? _options.Hours.OrderBy(o => Math.Abs(o.HoursPerWeek - profile.HoursPerWeek)).First(),
        KnownSkills = [.. _options.KnownSkills.Where(o => profile.KnownSkillKeys.Contains(o.Key, StringComparer.OrdinalIgnoreCase))],
        CountryId = profile.CountryId
      };

      var onPath = myYoma.Stages.SelectMany(o => o.Courses).ToList();
      var ids = onPath.Select(o => o.OpportunityId).ToHashSet();
      var stems = onPath.Select(o => PathwayMatching.Stem(o.Title)).ToHashSet(StringComparer.Ordinal);

      var result = new List<PathwayCourse>();
      var candidates = _catalogueService.Get().Courses
        .Where(o => !ids.Contains(o.OpportunityId))
        .Select(o => (Course: o, Score: PathwayEngine.ScoreCourse(o, stageOptions, index, request, _options)))
        .Where(o => o.Score.HasValue)
        .OrderByDescending(o => o.Score).ThenBy(o => o.Course.OpportunityId);

      foreach (var (course, _) in candidates)
      {
        if (result.Count >= MaxAlternatives) break;
        if (!stems.Add(PathwayMatching.Stem(course.Title))) continue;

        result.Add(ToCourse(course, stageOptions));
      }

      return result;
    }
    #endregion

    #region Private Members
    private static PathwayCourse ToCourse(PathwayCatalogueCourse course, PathwayStageOptions stage)
    {
      var skills = PathwayMatching.MatchingSkills(stage, course).Take(3).ToList();
      if (skills.Count == 0) skills = [.. course.Skills.Take(3)];
      var what = skills.Count > 0 ? PathwayMatching.JoinList(skills) : $"the basics of {PathwayMatching.Lower(stage.Name)}";

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
        Reason = $"Another way into \"{stage.Name}\": it adds {what}.",
        Grade = course.Grade,
        GradeLabel = course.Grade == null ? null : course.GradeLabel,
        Status = PathwayStepStatus.NotStarted,
        CompletedCount = course.CompletedCount
      };
    }
    #endregion
  }
}
