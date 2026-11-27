using System.Net;
using System.Text.RegularExpressions;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Entity;
using Yoma.Core.Domain.Opportunity;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// The job checks. Pure: same listing, link result and clock give the same answer. They read the listing and its feed,
  /// never a job page: feed pages are app shells or carry text (a "report: job no longer available" option) that reads
  /// as a signal it is not. Reasons and recommendations are partner-facing, so they state facts and next steps.
  /// </summary>
  public static partial class CourseQualityJobRules
  {
    #region Class Variables
    private const int NegationWindow = 40;
    private const int DetailsQuoteLength = 120;
    #endregion

    #region Public Members
    /// <summary>
    /// A scam signal found in the listing text, with the words that raised it.
    /// </summary>
    public sealed class ScamSignal
    {
      public string Reason { get; init; } = null!;
      public string Quote { get; init; } = null!;
      public bool Strong { get; init; }
    }

    /// <param name="link">Null when the job comes from a partner feed, which is what proves it is still open.</param>
    public static List<CourseQualityRuleResult> Evaluate(Opportunity.Models.Opportunity opportunity, CourseQualityLinkCheckResult? link,
      CourseQualityJobOptions options, DateTimeOffset now, out List<ScamSignal> signals)
    {
      ArgumentNullException.ThrowIfNull(opportunity, nameof(opportunity));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      var text = ListingText(opportunity);
      signals = ScamSignals(text);

      List<CourseQualityRuleResult> results =
      [
        JobOpen(opportunity, link, now),
        NoScamSignals(signals),
        JobDetailsClear(opportunity, text)
      ];

      // a feed lists a job only while it is open, so its age says nothing more; the score then counts the other three
      if (!FeedPartner(opportunity).HasValue) results.Add(JobFresh(opportunity, options, now));

      return results;
    }

    /// <summary>
    /// The partner feed a pulled job comes from, or null for a job a partner posted on Yoma.
    /// </summary>
    public static SyncPartner? FeedPartner(Opportunity.Models.Opportunity opportunity)
    {
      ArgumentNullException.ThrowIfNull(opportunity, nameof(opportunity));

      var info = opportunity.SyncedInfo;
      if (info == null || info.SyncType != SyncType.Pull || info.Partners.Count == 0) return null;
      return info.Partners[0].Partner;
    }

    /// <summary>
    /// Title and description as plain text: tags removed, entities decoded, markdown emphasis dropped.
    /// </summary>
    public static string ListingText(Opportunity.Models.Opportunity opportunity)
    {
      ArgumentNullException.ThrowIfNull(opportunity, nameof(opportunity));

      var raw = $"{opportunity.Title}\n{opportunity.Summary}\n{opportunity.Description}\n{opportunity.Instructions}";
      var text = TagRegex().Replace(raw, " ");
      text = WebUtility.HtmlDecode(text).Replace("**", string.Empty, StringComparison.Ordinal);
      return SpaceRegex().Replace(text, " ").Trim();
    }

    /// <summary>
    /// What a job listing states, from its custom fields first and its text otherwise. The checks and the job grade read the same facts.
    /// </summary>
    public sealed class JobFacts
    {
      public bool PayAmount { get; init; }

      /// <summary>
      /// A pay period or "market related" with no amount.
      /// </summary>
      public bool PayWithoutAmount { get; init; }

      public bool Schedule { get; init; }

      public bool Location { get; init; }

      public bool Requirements { get; init; }

      public bool HowToApply { get; init; }

      /// <summary>
      /// The experience level field, when the listing has one: None, EntryJunior, Mid or Senior.
      /// </summary>
      public string? ExperienceLevel { get; init; }

      /// <summary>
      /// The fewest years of experience the text asks for, when it names any.
      /// </summary>
      public int? YearsRequired { get; init; }

      /// <summary>
      /// The text says no experience is needed, or names an entry-level route (internship, learnership, graduate programme).
      /// </summary>
      public bool EntryLevelWording { get; init; }

      /// <summary>
      /// The fewest months of experience the text asks for, when it asks for less than a year.
      /// </summary>
      public int? MonthsRequired { get; init; }

      /// <summary>
      /// The only qualification named is a school-leaving one (matric, grade 12, SSCE, WASSCE), with no degree or diploma.
      /// </summary>
      public bool SchoolLevelOnly { get; init; }
    }

    public static JobFacts Facts(Opportunity.Models.Opportunity opportunity, string? text = null)
    {
      ArgumentNullException.ThrowIfNull(opportunity, nameof(opportunity));
      text ??= ListingText(opportunity);

      // "Market related" names a pay period but not what it pays, so only an amount counts
      var payAmount = HasField(opportunity, CustomFieldConstants.Job.Salary.Minimum) || IsTrue(opportunity, CustomFieldConstants.Job.Salary.Disclosed) || PayRegex().IsMatch(text);

      var years = YearsRegex().Matches(text)
        .Select(o => int.TryParse(o.Groups["years"].Value, out var value) ? value : (int?)null)
        .Where(o => o is >= 0 and <= 40)
        .Min();

      return new JobFacts
      {
        PayAmount = payAmount,
        PayWithoutAmount = !payAmount && (HasField(opportunity, CustomFieldConstants.Job.Salary.PayInterval) || PayWithoutAmountRegex().IsMatch(text)),
        Schedule = HasField(opportunity, CustomFieldConstants.Job.Employment.Schedule) || HasField(opportunity, CustomFieldConstants.Job.Employment.Type) || ScheduleRegex().IsMatch(text),
        Location = (opportunity.Countries?.Count ?? 0) > 0 || LocationRegex().IsMatch(text),
        Requirements = HasField(opportunity, CustomFieldConstants.Job.MinimumQualification) || HasField(opportunity, CustomFieldConstants.Job.Experience.Level) || RequirementsRegex().IsMatch(text),
        HowToApply = !string.IsNullOrWhiteSpace(opportunity.URL) || ApplyRegex().IsMatch(text),
        ExperienceLevel = opportunity.CustomFields?.FirstOrDefault(o => o.Key == CustomFieldConstants.Job.Experience.Level && !string.IsNullOrWhiteSpace(o.ValueRaw))?.ValueRaw?.Trim(),
        YearsRequired = years,
        EntryLevelWording = EntryLevelRegex().IsMatch(text),
        MonthsRequired = MonthsRegex().Matches(text)
          .Select(o => int.TryParse(o.Groups["months"].Value, out var value) ? value : (int?)null)
          .Where(o => o is >= 0 and < 12)
          .Min(),
        SchoolLevelOnly = SchoolLevelRegex().IsMatch(text) && !DegreeRegex().IsMatch(text)
      };
    }

    public static List<ScamSignal> ScamSignals(string text)
    {
      ArgumentNullException.ThrowIfNull(text, nameof(text));

      var results = new List<ScamSignal>();
      Add(results, text, FeeRegex(), "The listing asks applicants to pay a fee or deposit.", true);
      Add(results, text, SensitiveDataRegex(), "The listing asks applicants for bank, ID or login details.", true);
      Add(results, text, MessengerRegex(), "The listing asks applicants to apply through WhatsApp or Telegram.", true);
      Add(results, text, GuaranteeRegex(), "The listing promises a job or income without a normal hiring process.", true);
      Add(results, text, PersonalEmailRegex(), "The listing asks applicants to send details to a personal email address.", false);
      return results;
    }
    #endregion

    #region Private Members
    private static CourseQualityRuleResult JobOpen(Opportunity.Models.Opportunity opportunity, CourseQualityLinkCheckResult? link, DateTimeOffset now)
    {
      var problems = new List<(string Reason, string Recommendation)>();

      if (opportunity.Status != Status.Active)
        problems.Add(($"The listing status is {opportunity.Status}, not Active.", "Set the listing to Active while the job is open."));

      if (opportunity.OrganizationStatus != OrganizationStatus.Active)
        problems.Add(("The organisation is not active.", "Complete the organisation's approval so that its listings can be shown."));

      if (opportunity.Hidden == true)
        problems.Add(("The listing is hidden.", "Unhide the listing when it is ready for applicants."));

      if (opportunity.DateEnd.HasValue && opportunity.DateEnd.Value < now)
        problems.Add(("The closing date has passed.", "Remove the job, or update the closing date if it is still open."));

      if (link?.Outcome == CourseQualityLinkOutcome.Failed)
        problems.Add((link.Failure == CourseQualityLinkFailure.Missing ? "The listing has no link to apply." : "The link to apply does not open a usable page.",
          "Update the listing with a working link where applicants can apply."));

      if (problems.Count > 0)
        return new CourseQualityRuleResult
        {
          Criterion = CourseQualityCriterion.JobOpen,
          Score = 0,
          Failed = true,
          Reason = string.Join(" ", problems.Select(o => o.Reason)),
          Recommendation = string.Join(" ", problems.Select(o => o.Recommendation))
        };

      if (link?.Outcome == CourseQualityLinkOutcome.Inconclusive)
        return new CourseQualityRuleResult
        {
          Criterion = CourseQualityCriterion.JobOpen,
          Score = 50,
          Inconclusive = true,
          Reason = "The listing is active, but the link to apply could not be confirmed either way.",
          Recommendation = "Check that the link to apply opens without a sign-in or access restriction."
        };

      var partner = FeedPartner(opportunity);
      return new CourseQualityRuleResult
      {
        Criterion = CourseQualityCriterion.JobOpen,
        Score = 100,
        Reason = partner.HasValue
          ? $"The listing is active and the {partner.Value} feed still lists this job."
          : "The listing is active and the link to apply works."
      };
    }

    private static CourseQualityRuleResult NoScamSignals(List<ScamSignal> signals)
    {
      if (signals.Count == 0)
        return new CourseQualityRuleResult
        {
          Criterion = CourseQualityCriterion.NoScamSignals,
          Score = 100,
          Reason = "The listing shows none of the common scam warning signs."
        };

      var strong = signals.Any(o => o.Strong);
      return new CourseQualityRuleResult
      {
        Criterion = CourseQualityCriterion.NoScamSignals,
        Score = strong ? 0 : 60,
        // a weak sign alone lowers the score; only a strong one holds the job for a person
        Failed = strong,
        Reason = string.Join(" ", signals.Select(o => $"{o.Reason} (\"{o.Quote}\")")),
        Recommendation = "Remove any request for payment or personal details, and let applicants apply through the job's own page."
      };
    }

    private static CourseQualityRuleResult JobDetailsClear(Opportunity.Models.Opportunity opportunity, string text)
    {
      var facts = Facts(opportunity, text);
      var fields = new (string Name, bool Present)[]
      {
        ("pay amount", facts.PayAmount),
        ("work schedule or contract type", facts.Schedule),
        ("location", facts.Location),
        ("requirements", facts.Requirements),
        ("how to apply", facts.HowToApply)
      };

      var missing = fields.Where(o => !o.Present).Select(o => o.Name).ToList();

      if (missing.Count == 0)
        return new CourseQualityRuleResult
        {
          Criterion = CourseQualityCriterion.JobDetailsClear,
          Score = 100,
          Reason = "The listing states the pay amount, schedule, location, requirements and how to apply."
        };

      return new CourseQualityRuleResult
      {
        Criterion = CourseQualityCriterion.JobDetailsClear,
        Score = (int)Math.Round(100m * (fields.Length - missing.Count) / fields.Length, MidpointRounding.AwayFromZero),
        Failed = true,
        Reason = $"The listing does not state: {string.Join(", ", missing)}.",
        Recommendation = $"Add to the listing: {string.Join(", ", missing)}."
      };
    }

    /// <summary>
    /// A pulled job's start date is when the feed first listed it, so its age is how long the job has been advertised.
    /// </summary>
    private static CourseQualityRuleResult JobFresh(Opportunity.Models.Opportunity opportunity, CourseQualityJobOptions options, DateTimeOffset now)
    {
      var days = Math.Max(0, (int)Math.Floor((now - opportunity.DateStart).TotalDays));
      var fresh = Math.Max(0, options.FreshDays);
      var stale = Math.Max(fresh + 1, options.StaleDays);
      var posted = days == 1 ? "1 day ago" : $"{days} days ago";

      if (days <= fresh)
        return new CourseQualityRuleResult { Criterion = CourseQualityCriterion.JobFresh, Score = 100, Reason = $"Posted {posted}." };

      if (days >= stale)
        return new CourseQualityRuleResult
        {
          Criterion = CourseQualityCriterion.JobFresh,
          Score = 0,
          Failed = true,
          Reason = $"Posted {posted}, so it may already be filled.",
          Recommendation = "Confirm the job is still open, or remove it."
        };

      // from 100 at the fresh limit down towards 50 just before the stale one
      var score = (int)Math.Round(100m - 50m * (days - fresh) / (stale - fresh), MidpointRounding.AwayFromZero);
      return new CourseQualityRuleResult
      {
        Criterion = CourseQualityCriterion.JobFresh,
        Score = score,
        Reason = $"Posted {posted}.",
        Recommendation = "Confirm the job is still open."
      };
    }

    private static bool HasField(Opportunity.Models.Opportunity opportunity, string key)
    {
      return opportunity.CustomFields?.Any(o => o.Key == key && !string.IsNullOrWhiteSpace(o.ValueRaw)) == true;
    }

    private static bool IsTrue(Opportunity.Models.Opportunity opportunity, string key)
    {
      return opportunity.CustomFields?.Any(o => o.Key == key && string.Equals(o.ValueRaw?.Trim(), "true", StringComparison.OrdinalIgnoreCase)) == true;
    }

    /// <summary>
    /// Legitimate listings warn against scams in the same words ("we never charge an application fee"), so a match right
    /// after a negation does not count.
    /// </summary>
    private static void Add(List<ScamSignal> results, string text, Regex regex, string reason, bool strong)
    {
      foreach (Match match in regex.Matches(text))
      {
        var before = text[Math.Max(0, match.Index - NegationWindow)..match.Index];
        if (NegationRegex().IsMatch(before)) continue;

        var quote = match.Value.Trim();
        results.Add(new ScamSignal { Reason = reason, Quote = quote.Length <= DetailsQuoteLength ? quote : quote[..DetailsQuoteLength], Strong = strong });
        return;
      }
    }

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    [GeneratedRegex(@"\b(no|never|not|free of|without|don't|do not|won't|will not|nobody|no one)\b[^.!?]*$", RegexOptions.IgnoreCase)]
    private static partial Regex NegationRegex();

    [GeneratedRegex(@"\b(application|registration|processing|training|joining|admin(istration)?|placement|uniform|medical|screening|interview)\s+(fee|charge|deposit)s?\b|\bpay\s+(a\s+|the\s+)?(small\s+)?(fee|deposit)\b|\b(fee|deposit)\s+of\s+(R|N|₦|GH₵|GHS|ZAR|NGN|KES|KSh|\$)\s?\d", RegexOptions.IgnoreCase)]
    private static partial Regex FeeRegex();

    [GeneratedRegex(@"\b(send|provide|submit|share|forward)\s+(us\s+)?(your\s+|a\s+copy\s+of\s+your\s+)?(bank\s+(account|details|card)|card\s+details|id\s+(number|copy)|bvn|nin|passwords?|pin|otp)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveDataRegex();

    [GeneratedRegex(@"\b(apply|send|submit|forward)\b[^.!?\n]{0,60}\b(whatsapp|telegram)\b|\b(whatsapp|telegram)\b[^.!?\n]{0,20}(\+|\b0)\d[\d\s-]{7,}", RegexOptions.IgnoreCase)]
    private static partial Regex MessengerRegex();

    [GeneratedRegex(@"\bguaranteed\s+(job|employment|placement|income|salary)\b|\bno\s+interview(s)?\s+(needed|required)\b|\bget\s+hired\s+instantly\b", RegexOptions.IgnoreCase)]
    private static partial Regex GuaranteeRegex();

    [GeneratedRegex(@"\b[\w.+-]+@(gmail|yahoo|hotmail|outlook|ymail|aol|live)\.(com|co\.za|co\.uk)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PersonalEmailRegex();

    [GeneratedRegex(@"(\b(salary|pay|remuneration|stipend|wage|earn)\b[^.\n]{0,40}\d)|((\bR|₦|GH₵|\bGHS|\bZAR|\bNGN|\bKES|\bKSh|\$)\s?\d[\d,. ]*\d)", RegexOptions.IgnoreCase)]
    private static partial Regex PayRegex();

    [GeneratedRegex(@"\b(full[- ]time|part[- ]time|contract|temporary|permanent|internship|learnership|casual|freelance|fixed[- ]term|shifts?|\d+\s*hours?\s*(per|a|/)\s*week)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ScheduleRegex();

    [GeneratedRegex(@"\b(location|based in|remote|hybrid|on-?site|work from home)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LocationRegex();

    [GeneratedRegex(@"\b(requirements?|qualifications?|experience|skills required|minimum|must have|you will need|matric|diploma|degree)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RequirementsRegex();

    [GeneratedRegex(@"\bapply\b", RegexOptions.IgnoreCase)]
    private static partial Regex ApplyRegex();

    [GeneratedRegex(@"\b(market[- ]related|negotiable|competitive|per\s+(hour|day|week|month|annum|year))\b", RegexOptions.IgnoreCase)]
    private static partial Regex PayWithoutAmountRegex();

    // "2 years experience", "3+ years of relevant experience", "2-4 years' work experience", "minimum of 5 years"
    [GeneratedRegex(@"\b(?<years>\d{1,2})\)?\s*(\+|plus|-\s*\d{1,2}|to\s+\d{1,2})?\s*years?['’]?\s+(of\s+)?(relevant\s+|proven\s+|working\s+|work\s+|industry\s+|post[- ]qualification\s+)?(experience|exp)\b|\b(minimum|at\s+least)\s+(of\s+)?(?<years>\d{1,2})\s*years?\b", RegexOptions.IgnoreCase)]
    private static partial Regex YearsRegex();

    [GeneratedRegex(@"\bno\s+(prior\s+|previous\s+|work\s+)?experience\s+(is\s+)?(needed|required|necessary)\b|\bentry[- ]level\b|\b(internship|learnership|graduate\s+(trainee|programme|program)|school\s+leavers?|trainee)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EntryLevelRegex();

    [GeneratedRegex(@"\b(?<months>\d{1,2})\)?\s*months?['’]?\s+(of\s+)?(\w+\s+)?(experience|exp)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MonthsRegex();

    [GeneratedRegex(@"\b(matric|grade\s*12|ssce|wassce|o['’]?\s*levels?|school\s+certificate)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SchoolLevelRegex();

    [GeneratedRegex(@"\b(degree|b\.?\s?sc|bachelor'?s?|hnd|ond|diploma|masters?|mba|phd)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DegreeRegex();
    #endregion
  }
}
