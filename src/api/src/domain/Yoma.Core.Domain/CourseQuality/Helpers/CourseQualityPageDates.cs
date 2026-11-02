using System.Globalization;
using System.Text.RegularExpressions;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  public enum CourseQualityDateEvidence
  {
    /// <summary>The page states dates that agree with every date the listing has.</summary>
    Confirmed,

    /// <summary>The start agrees; the listing's end has nothing to compare with.</summary>
    StartConfirmed,

    /// <summary>The end agrees; the page gives no start.</summary>
    EndConfirmed,

    /// <summary>No dates, and the page says it is self-paced or ongoing.</summary>
    SelfPaced,

    /// <summary>No usable dates.</summary>
    Silent,

    /// <summary>The page states a date that differs from the listing.</summary>
    Disagrees
  }

  /// <summary>
  /// Compares page dates with the listing by calendar date with a day of tolerance, because a listing stored in UTC can
  /// fall on the neighbouring day of a page written in local time. Only labelled text counts: a page holds many dates
  /// (deadlines, publishing, copyright) that say nothing about when a course runs. Structured dates outrank text.
  /// </summary>
  public static partial class CourseQualityPageDates
  {
    #region Class Variables
    private const int ToleranceInDays = 1;
    private const int LabelWindow = 32;
    private const int MaxDatesRead = 40;
    private const int MinYear = 2000;
    private const int MaxYear = 2100;
    private const int RegexTimeoutMilliseconds = 1000;
    private const RegexOptions SafeOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

    private const string Month = @"(?:jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\.?";
    private const string Weekday = @"(?:(?:mon|tues|wednes|thurs|fri|satur|sun)day,?\s*)?";
    private const string Connector = @"\s*(?:on|is|:|-|–|—)?\s*:?\s*";
    #endregion

    #region Public Members
    public static CourseQualityDateEvidence Read(CourseQualityAIRequest request, string text, CourseQualityStructuredFacts facts)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));
      ArgumentNullException.ThrowIfNull(facts, nameof(facts));

      var (textStarts, textEnds) = FromText(text);

      // structured dates are the partner's claim: they count only where a visitor can see the same date, and never over the body
      var seen = AllDates(text);
      var starts = facts.Starts.Where(o => seen.Any(d => SameDay(o, d))).Concat(textStarts).ToList();
      var ends = facts.Ends.Where(o => seen.Any(d => SameDay(o, d))).Concat(textEnds).ToList();

      if (Contradicted(facts.Starts, textStarts) || Contradicted(facts.Ends, textEnds)) return CourseQualityDateEvidence.Disagrees;

      if (starts.Count == 0 && ends.Count == 0)
        return facts.SelfPaced || SelfPacedRegex().IsMatch(text) ? CourseQualityDateEvidence.SelfPaced : CourseQualityDateEvidence.Silent;

      var startAgrees = starts.Any(o => Near(o, request.DateStart));
      if (starts.Count > 0 && !startAgrees) return CourseQualityDateEvidence.Disagrees;

      // cohorts: any published run that matches confirms the listing
      if (ends.Count > 0 && (!request.DateEnd.HasValue || !ends.Any(o => Near(o, request.DateEnd.Value)))) return CourseQualityDateEvidence.Disagrees;

      if (!startAgrees) return CourseQualityDateEvidence.EndConfirmed;

      return ends.Count > 0 || !request.DateEnd.HasValue ? CourseQualityDateEvidence.Confirmed : CourseQualityDateEvidence.StartConfirmed;
    }

    /// <summary>
    /// Any calendar date at all, labelled or not.
    /// </summary>
    public static bool MentionsDate(string text)
    {
      ArgumentNullException.ThrowIfNull(text, nameof(text));

      return DateRegex().IsMatch(text);
    }
    #endregion

    #region Private Members
    private static bool Contradicted(List<DateTimeOffset> structured, List<DateTimeOffset> body)
    {
      return structured.Count > 0 && body.Count > 0 && !structured.Any(o => body.Any(b => SameDay(o, b)));
    }

    private static bool SameDay(DateTimeOffset first, DateTimeOffset second)
    {
      return Math.Abs((first.Date - second.Date).Days) <= ToleranceInDays;
    }

    private static List<DateTimeOffset> AllDates(string text)
    {
      var dates = new List<DateTimeOffset>();

      foreach (var match in DateRegex().Matches(text).Take(MaxDatesRead))
        if (TryParse(match.Value, out var date)) dates.Add(date);

      return dates;
    }

    private static bool Near(DateTimeOffset page, DateTimeOffset listing)
    {
      // the calendar date as the page wrote it, against the listing's stored UTC date
      return Math.Abs((page.Date - listing.UtcDateTime.Date).Days) <= ToleranceInDays;
    }

    private static (List<DateTimeOffset> Starts, List<DateTimeOffset> Ends) FromText(string text)
    {
      var starts = new List<DateTimeOffset>();
      var ends = new List<DateTimeOffset>();
      var previousEnd = -1;

      foreach (var match in DateRegex().Matches(text).Take(MaxDatesRead))
      {
        if (!TryParse(match.Value, out var date)) continue;

        var range = previousEnd >= 0 && RangeRegex().IsMatch(text[previousEnd..match.Index]);
        var before = text[Math.Max(0, match.Index - LabelWindow)..match.Index];
        previousEnd = -1;

        if (range)
        {
          ends.Add(date);
          continue;
        }

        if (OtherDateRegex().IsMatch(before)) continue;

        if (EndLabelRegex().IsMatch(before)) ends.Add(date);
        else if (StartLabelRegex().IsMatch(before))
        {
          starts.Add(date);
          previousEnd = match.Index + match.Length;
        }
      }

      return (starts, ends);
    }

    private static bool TryParse(string value, out DateTimeOffset date)
    {
      var normalized = OrdinalRegex().Replace(value, "$1").Replace("Sept", "Sep", StringComparison.OrdinalIgnoreCase).Replace(".", string.Empty);

      return DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date) && date.Year is >= MinYear and <= MaxYear;
    }

    [GeneratedRegex(@"\b(?:\d{4}-\d{2}-\d{2}|\d{1,2}(?:st|nd|rd|th)?\s+" + Month + @",?\s+\d{4}|" + Month + @"\s+\d{1,2}(?:st|nd|rd|th)?\s*,?\s+\d{4})\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"(\d)(?:st|nd|rd|th)", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex OrdinalRegex();

    [GeneratedRegex(@"^\s*(?:to|until|through|till|-|–|—)\s*$", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"deadline|closes?\b|closing|\b(?:enrol|enroll|register|apply)\s+by\b|registration|\bapplications?\b|\badmissions?\b|\benrol+ments?\b|publish|updated|modified|posted|created|copyright|expires?\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex OtherDateRegex();

    [GeneratedRegex(@"\b(?:start(?:s|ing)?(?:\s+date)?|begin(?:s|ning)?(?:\s+date)?|commenc(?:es|ing|ement)(?:\s+date)?|course\s+dates?|dates?|from|next\s+(?:cohort|intake|session))" + Connector + Weekday + @"$", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex StartLabelRegex();

    [GeneratedRegex(@"\b(?:end(?:s|ing)?(?:\s+date)?|finish(?:es)?(?:\s+date)?|conclud(?:es|ing)|completion\s+date|runs?\s+(?:until|to)|until)" + Connector + Weekday + @"$", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex EndLabelRegex();

    // only what the review note says; it proves nothing, because a sentence can say the opposite of these words
    [GeneratedRegex(@"self[\s-]?paced|start\s+(?:at\s+)?any\s*time|start\s+when\s+(?:it\s+)?suits\s+you|at\s+your\s+own\s+(?:pace|speed)|on[\s-]demand|no\s+fixed\s+(?:start|dates?)|always\s+open|rolling\s+(?:start|enrol)", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex SelfPacedRegex();
    #endregion
  }
}
