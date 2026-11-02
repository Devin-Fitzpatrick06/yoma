using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using Xunit;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualitySourcePageReaderTests
  {
    #region Helpers
    private static readonly DateTimeOffset ListingStart = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ListingEnd = new(2026, 12, 24, 0, 0, 0, TimeSpan.Zero);
    private const string ListingUrl = "https://courses.example.org/course/introduction-to-data-analysis";

    private static readonly CourseQualityOptions DefaultOptions = new();

    private static string Fixture(string name) =>
      File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CourseQuality", "Fixtures", name));

    private static CourseQualityLinkCheckResult Link(string html, string finalUrl = ListingUrl) => CourseQualityLinkCheckResult.Reachable(200, finalUrl,
      CourseQualityPageText.FromHtml(html, DefaultOptions.LinkCheck.MaxSourceTextLength), CourseQualityPageSignalExtractor.FromHtml(html));

    private static CourseQualityAIRequest Request(CourseQualityLinkCheckResult link, string title = "Introduction to Data Analysis",
      DateTimeOffset? start = null, DateTimeOffset? end = null, bool openEnded = false) => new()
      {
        Title = title,
        Description = "Learn to clean, explore and present data.",
        URL = ListingUrl,
        DateStart = start ?? ListingStart,
        DateEnd = openEnded ? null : end ?? ListingEnd,
        CommitmentHours = 672,
        SourcePageText = link.SourcePageText,
        SourcePageSignals = link.SourcePageSignals,
        SourcePageFinalUrl = link.FinalUrl
      };

    private static Task<CourseQualityAIResult?> Read(CourseQualityAIRequest request) =>
      new CourseQualitySourcePageReader(Options.Create(new CourseQualityOptions())).Evaluate(request, CancellationToken.None);

    private static async Task<CourseQualityAIResult> ReadFixture(string name)
    {
      var result = await Read(Request(Link(Fixture(name))));
      Assert.NotNull(result);
      return result;
    }

    private static string Page(string body, string title = "Introduction to Data Analysis | Northwind", string head = "") =>
      $"<html><head><title>{title}</title>{head}</head><body><h1>Introduction to Data Analysis</h1><p>About this course: {body}</p>" +
      $"<p>{string.Concat(Enumerable.Repeat("Practical units with exercises and quizzes every week. ", 8))}</p></body></html>";

    private static async Task<LearnerCardCost> CostOf(string body, string head = "")
    {
      var result = await Read(Request(Link(Page(body, head: head))));
      Assert.NotNull(result);
      return result.Cost;
    }

    private static int Score(CourseQualityAIResult result, CourseQualityCriterion criterion) => result.Criteria.Single(o => o.Criterion == criterion).Score;

    private static Domain.Opportunity.Models.Opportunity Listing(bool openEnded = false)
    {
      var listing = CleanListing();
      listing.URL = ListingUrl;
      listing.DateStart = ListingStart.AddDays(-20);
      listing.DateEnd = openEnded ? null : ListingEnd;
      return listing;
    }

    private static async Task<CourseQualityAssessment> Assess(string fixture, Domain.Opportunity.Models.Opportunity listing, string finalUrl = ListingUrl)
    {
      var link = Link(Fixture(fixture), finalUrl);
      var ai = await Read(new CourseQualityAIRequest
      {
        Title = listing.Title,
        Description = listing.Description,
        URL = listing.URL,
        OrganizationName = listing.OrganizationName,
        DateStart = listing.DateStart,
        DateEnd = listing.DateEnd,
        SourcePageText = link.SourcePageText,
        SourcePageSignals = link.SourcePageSignals,
        SourcePageFinalUrl = link.FinalUrl
      });

      return CourseQualityEngine.Assess(listing, link, ai, new CourseQualityOptions(), Now);
    }

    private static Domain.Opportunity.Models.Opportunity DatedListing()
    {
      var listing = Listing();
      listing.DateStart = ListingStart;
      return listing;
    }
    #endregion

    #region Provider identity
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEnabled_FollowsTheOption(bool enabled)
    {
      var reader = new CourseQualitySourcePageReader(Options.Create(new CourseQualityOptions { SourcePageReaderEnabled = enabled }));

      Assert.Equal(enabled, reader.IsEnabled);
    }

    [Fact]
    public async Task Result_IsNotAI_AndNamesTheReader()
    {
      var result = await ReadFixture("paid-cohort.html");

      Assert.False(result.IsAI);
      Assert.Equal("source-page-reader", result.Model);
    }
    #endregion

    #region Unreadable pages
    [Theory]
    [InlineData("javascript-shell.html")]
    [InlineData("bot-challenge.html")]
    public async Task UnreadablePage_ReturnsNull(string fixture)
    {
      Assert.Null(await Read(Request(Link(Fixture(fixture)))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Loading...")]
    public async Task EmptyOrThinText_ReturnsNull(string? text)
    {
      Assert.Null(await Read(Request(CourseQualityLinkCheckResult.Reachable(200, ListingUrl, text))));
    }
    #endregion

    #region Dates
    [Theory]
    [InlineData("paid-cohort.html")]
    [InlineData("atingi-style-dated.html")]
    public async Task Dates_ThatAgree_ScoreHigh(string fixture)
    {
      var result = await ReadFixture(fixture);

      Assert.Equal(100, Score(result, CourseQualityCriterion.DatesAccurate));
      Assert.True(result.Confidence >= 0.7m);
    }

    [Fact]
    public async Task Dates_OneDayApart_AndOffsets_StillAgree()
    {
      // the page says 09:00 on the 12th at +02:00; the listing holds the 11th at 23:00 UTC
      var result = await Read(Request(Link(Fixture("paid-cohort.html")), start: new DateTimeOffset(2026, 11, 11, 23, 0, 0, TimeSpan.Zero), end: ListingEnd.AddDays(1)));

      Assert.Equal(100, Score(result!, CourseQualityCriterion.DatesAccurate));
    }

    [Theory]
    [InlineData("14 September 2026", "9 October 2026")]
    [InlineData("14 Sept 2026", "9 Oct 2026")]
    public async Task Dates_September_IsRead_InFullAndShort(string start, string end)
    {
      var result = await Read(Request(Link(Page($"Start date: {start}. End date: {end}. Learn to clean, explore and present data in this course.")),
        start: new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero), end: new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero)));

      Assert.Equal(100, Score(result!, CourseQualityCriterion.DatesAccurate));
    }

    [Fact]
    public async Task Dates_TwoDaysApart_Disagree()
    {
      var result = await Read(Request(Link(Fixture("paid-cohort.html")), start: ListingStart.AddDays(2)));

      Assert.True(Score(result!, CourseQualityCriterion.DatesAccurate) < 50);
      Assert.True(result!.Confidence < 0.7m);
    }

    [Fact]
    public async Task Dates_ThatDisagree_ScoreLow()
    {
      var result = await ReadFixture("conflicting-dates.html");

      Assert.True(Score(result, CourseQualityCriterion.DatesAccurate) < 50);
      Assert.True(result.Confidence < 0.7m);
    }

    [Fact]
    public async Task Dates_EnrolmentDeadline_IsNotTheStartDate()
    {
      var result = await Read(Request(Link(Page("Enrolment deadline: 5 November 2026. Published 1 January 2026."))));

      Assert.NotEqual(100, Score(result!, CourseQualityCriterion.DatesAccurate));
      Assert.True(Score(result!, CourseQualityCriterion.DatesAccurate) >= 50);
    }

    [Fact]
    public async Task Dates_SelfPacedWordingWithoutDates_IsPartial_NotAMismatch_AndDoesNotQualify()
    {
      var result = await Read(Request(Link(Fixture("self-paced-no-dates.html")), openEnded: true));

      var score = Score(result!, CourseQualityCriterion.DatesAccurate);
      Assert.InRange(score, 50, 79);
      Assert.False(result!.SelfPaced);
    }

    [Fact]
    public async Task Dates_PageSilent_ArePartial_WithLowerConfidence()
    {
      var result = await Read(Request(Link(Page("practical skills for the workplace."))));

      Assert.InRange(Score(result!, CourseQualityCriterion.DatesAccurate), 50, 79);
      Assert.True(result!.Confidence < 0.7m);
    }

    [Fact]
    public async Task Dates_PageEndOnOpenEndedListing_IsADisagreement()
    {
      var result = await Read(Request(Link(Fixture("paid-cohort.html")), openEnded: true));

      Assert.True(Score(result!, CourseQualityCriterion.DatesAccurate) < 50);
    }
    #endregion

    #region Cost
    [Fact]
    public async Task Cost_Paid_FromStructuredData_QuotesThePageText()
    {
      var cost = (await ReadFixture("paid-cohort.html")).Cost;

      Assert.Equal(CourseCostType.Paid, cost.Type);
      Assert.Equal(49m, cost.Amount);
      Assert.Equal("USD", cost.Currency);
      Assert.Contains("$49", cost.Evidence);
    }

    [Fact]
    public async Task Cost_FreeCourse_WithPaidCertificate_IsFree()
    {
      var cost = (await Read(SelfPacedRequest(Fixture("alison-style-free.html"), "Alison")))!.Cost;

      Assert.Equal(CourseCostType.Free, cost.Type);
      Assert.Null(cost.Amount);
      Assert.False(string.IsNullOrWhiteSpace(cost.Evidence));
    }

    [Fact]
    public async Task Cost_FreeCourse_WithPaidCertificate_OffAListedDomain_IsUnknown()
    {
      Assert.Equal(CourseCostType.Unknown, (await ReadFixture("alison-style-free.html")).Cost.Type);
    }

    [Fact]
    public async Task Cost_FreeOfCharge_InTextAlone_IsUnknown()
    {
      Assert.Equal(CourseCostType.Unknown, (await ReadFixture("atingi-style-dated.html")).Cost.Type);
    }

    [Theory]
    [InlineData("subscription-trial.html")]
    [InlineData("multiple-prices.html")]
    [InlineData("conflicting-dates.html")]
    public async Task Cost_AmbiguousOrSilent_IsUnknown(string fixture)
    {
      var cost = (await ReadFixture(fixture)).Cost;

      Assert.Equal(CourseCostType.Unknown, cost.Type);
      Assert.Null(cost.Evidence);
      Assert.Null(cost.Amount);
    }

    [Theory]
    [InlineData("Start your free trial today.")]
    [InlineData("Free shipping on all orders.")]
    [InlineData("Includes a free certificate upgrade.")]
    [InlineData("$49 for certificate.")]
    [InlineData("Free certificate of completion for graduates.")]
    [InlineData("The first lesson is free of charge.")]
    [InlineData("Browse our free courses and free resources.")]
    [InlineData("This course is not free.")]
    [InlineData("Graduates earn a salary of $50,000 in this field.")]
    [InlineData("Save $20 when you bring a friend.")]
    public async Task Cost_Traps_AreUnknown(string body)
    {
      Assert.Equal(CourseCostType.Unknown, (await CostOf(body)).Type);
    }

    [Theory]
    [InlineData("Price: US$49", 49, "USD")]
    [InlineData("Price: USD 49", 49, "USD")]
    [InlineData("Course fee: R 1,299.50", 1299.50, "ZAR")]
    [InlineData("Tuition: ₦15,000", 15000, "NGN")]
    [InlineData("Cost: KES 2500", 2500, "KES")]
    [InlineData("Price: 30 EUR", 30, "EUR")]
    [InlineData("Price: €30", 30, "EUR")]
    public async Task Cost_TextPrices_ParseAmountAndCurrency(string body, double amount, string currency)
    {
      var cost = await CostOf(body);

      Assert.Equal(CourseCostType.Paid, cost.Type);
      Assert.Equal((decimal)amount, cost.Amount);
      Assert.Equal(currency, cost.Currency);
    }

    [Fact]
    public async Task Cost_SameAmountRepeated_IsOnePrice()
    {
      Assert.Equal(49m, (await CostOf("Price: $49. Buy now for $49.00.")).Amount);
    }

    [Theory]
    [InlineData("Price: $49. Premium price: $99.")]
    [InlineData("Price: $39 per month.")]
    [InlineData("This course is free. Price: $49.")]
    public async Task Cost_TextConflicts_AreUnknown(string body)
    {
      Assert.Equal(CourseCostType.Unknown, (await CostOf(body)).Type);
    }

    [Fact]
    public async Task Cost_StructuredFree_ButPaidText_IsUnknown()
    {
      var head = """<script type="application/ld+json">{"@type":"Course","name":"Introduction to Data Analysis","isAccessibleForFree":true}</script>""";

      Assert.Equal(CourseCostType.Unknown, (await CostOf("Price: $49", head)).Type);
    }

    [Fact]
    public async Task Cost_MetaPrice_IsUsed()
    {
      var head = """<meta property="product:price:amount" content="75.00"><meta property="product:price:currency" content="GBP">""";

      var cost = await CostOf("practical skills for work.", head);

      Assert.Equal(CourseCostType.Paid, cost.Type);
      Assert.Equal(75m, cost.Amount);
      Assert.Equal("GBP", cost.Currency);
    }

    /// <summary>
    /// Evidence is a quote. Every cost the reader returns must be found in what the page actually said.
    /// </summary>
    [Theory]
    [InlineData("paid-cohort.html")]
    public async Task Cost_Evidence_AppearsInThePage(string fixture)
    {
      var link = Link(Fixture(fixture));
      var cost = (await Read(Request(link)))!.Cost;

      Assert.NotEqual(CourseCostType.Unknown, cost.Type);
      Assert.True(CourseQualitySafeText.AppearsInPage(cost.Evidence, link.SourcePageText, link.SourcePageSignals), cost.Evidence);
    }

    [Theory]
    [InlineData("Price: $49", null)]
    [InlineData("practical work.", """<script type="application/ld+json">{"@type":"Course","name":"Introduction to Data Analysis","offers":{"price":"25","priceCurrency":"EUR"}}</script>""")]
    [InlineData("practical work.", """<script type="application/ld+json">{"@type":"Course","name":"Introduction to Data Analysis","offers":{"price" :  0}}</script>""")]
    public async Task Cost_Evidence_AppearsInThePage_ForEachSource(string body, string? head)
    {
      var link = Link(Page(body, head: head ?? string.Empty));
      var cost = (await Read(Request(link)))!.Cost;

      Assert.NotEqual(CourseCostType.Unknown, cost.Type);
      Assert.True(CourseQualitySafeText.AppearsInPage(cost.Evidence, link.SourcePageText, link.SourcePageSignals), cost.Evidence);
    }
    #endregion

    #region Format
    [Theory]
    [InlineData("paid-cohort.html", CourseFormat.Course)]
    [InlineData("alison-style-free.html", CourseFormat.Course)]
    [InlineData("atingi-style-dated.html", CourseFormat.Course)]
    [InlineData("webinar-event.html", CourseFormat.Webinar)]
    public async Task Format_IsReadFromThePage(string fixture, CourseFormat expected)
    {
      var result = await Read(Request(Link(Fixture(fixture)), title: fixture.StartsWith("webinar") ? "Data Analysis for Beginners: Live Webinar" : "Introduction to Data Analysis"));

      Assert.Equal(expected, result!.Format);
    }

    [Fact]
    public async Task Format_LiveSessionOnACoursePage_StaysCourse()
    {
      var head = """<script type="application/ld+json">{"@type":"Course","name":"Introduction to Data Analysis"}</script>""";

      var result = await Read(Request(Link(Page("includes one live session each week. Register for this webinar too.", head: head))));

      Assert.Equal(CourseFormat.Course, result!.Format);
    }

    [Fact]
    public async Task Format_ContradictoryOrSilent_IsUnknown_AndStaysBelowTheThreshold()
    {
      var head = """<script type="application/ld+json">{"@type":"Event","name":"Introduction to Data Analysis","startDate":"2026-11-12","endDate":"2026-12-24"}</script>""";
      var result = await Read(Request(Link($"<html><head><title>Introduction to Data Analysis</title>{head}</head><body><h1>Introduction to Data Analysis</h1><p>Enrol in this course with us. {new string('x', 300)} words</p></body></html>")));

      Assert.Equal(CourseFormat.Unknown, result!.Format);
      Assert.True(result.Confidence < 0.7m);
    }

    [Fact]
    public async Task Format_LiveSessionAlone_IsAWebinar()
    {
      var html = $"<html><head><title>Introduction to Data Analysis</title></head><body><h1>Introduction to Data Analysis</h1><p>Join a live session with our analyst. {string.Concat(Enumerable.Repeat("Bring your questions and a notebook. ", 8))}</p></body></html>";

      Assert.Equal(CourseFormat.Webinar, (await Read(Request(Link(html))))!.Format);
    }
    #endregion

    #region Title match
    [Theory]
    [InlineData("generic-homepage.html")]
    [InlineData("other-course.html")]
    [InlineData("catalogue.html")]
    public async Task WrongPage_LowersDataCompleteAndConfidence(string fixture)
    {
      var result = await ReadFixture(fixture);

      Assert.True(Score(result, CourseQualityCriterion.DataComplete) < 50);
      Assert.True(result.Confidence < 0.7m);
    }

    [Fact]
    public async Task WrongPage_DoesNotContributeItsPriceOrDates()
    {
      var result = await ReadFixture("other-course.html");

      Assert.Equal(CourseCostType.Unknown, result.Cost.Type);
      Assert.Null(result.TimelineSummary);
    }

    [Fact]
    public async Task Catalogue_DoesNotLendACourseItsPrices()
    {
      Assert.Equal(CourseCostType.Unknown, (await ReadFixture("catalogue.html")).Cost.Type);
    }

    [Fact]
    public async Task RedirectToTheHomePage_IsNotAMatch_EvenWithAMatchingTitle()
    {
      var result = await Read(Request(Link(Fixture("paid-cohort.html"), "https://courses.example.org/")));

      Assert.True(result!.Confidence < 0.7m);
      Assert.True(Score(result, CourseQualityCriterion.DataComplete) < 50);
    }

    [Fact]
    public async Task SoftNotFound_FailsTheLinkCriterion()
    {
      var result = await ReadFixture("soft-404.html");

      Assert.True(Score(result, CourseQualityCriterion.LinkWorks) < 50);
      Assert.True(result.Confidence < 0.7m);
    }

    [Fact]
    public async Task SoftNotFound_ACourseAboutErrors_IsNotOne()
    {
      var html = Page("how web servers answer.", "How to Fix 404 Page Not Found Errors | Northwind").Replace("Introduction to Data Analysis", "How to Fix 404 Page Not Found Errors");

      var result = await Read(Request(Link(html), title: "How to Fix 404 Page Not Found Errors"));

      Assert.DoesNotContain(result!.Criteria, o => o.Criterion == CourseQualityCriterion.LinkWorks);
    }
    #endregion

    #region Timeline
    [Theory]
    [InlineData("paid-cohort.html", "6 weeks")]
    [InlineData("alison-style-free.html", "3 hours")]
    [InlineData("atingi-style-dated.html", "6 weeks")]
    public async Task Timeline_ComesFromDuration_InTheEngineShape(string fixture, string expected)
    {
      Assert.Equal(expected, (await ReadFixture(fixture)).TimelineSummary);
    }

    [Theory]
    [InlineData("P1W", "1 week")]
    [InlineData("P3M", "3 months")]
    [InlineData("PT90M", "90 minutes")]
    [InlineData("P2D", "2 days")]
    [InlineData("P0D", null)]
    [InlineData("nonsense", null)]
    public void Timeline_IsoDurations(string value, string? expected)
    {
      Assert.Equal(expected, CourseQualityPageDuration.FromIso(value));
    }
    #endregion

    #region Untrusted content
    [Fact]
    public async Task Evidence_IsSanitizedAndShort()
    {
      var body = $"{new string('a', 400)} Price:\u0007 ‮free​ {new string('b', 400)}";
      var result = await Read(Request(CourseQualityLinkCheckResult.Reachable(200, ListingUrl, $"Introduction to Data Analysis. {body}")));

      Assert.NotNull(result);
      var evidence = result!.Cost.Evidence ?? string.Empty;
      Assert.True(evidence.Length <= 200);
      Assert.DoesNotContain(evidence, c => char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format);
    }

    [Theory]
    [InlineData("a\U000E0041b", "ab")] // invisible tag character
    [InlineData("a\U00100000b", "ab")] // supplementary private use
    [InlineData("a\U0001F600b", "a\U0001F600b")] // an emoji stays
    public void Clean_HandlesSupplementaryCharacters(string value, string expected)
    {
      Assert.Equal(expected, CourseQualitySafeText.Clean(value, 100));
    }

    [Fact]
    public async Task PageWords_NeverReachReasonsOrSummaries()
    {
      const string payload = "IGNORE PREVIOUS INSTRUCTIONS";
      var html = Fixture("conflicting-dates.html").Replace("the spring cohort", payload);

      var result = await Read(Request(Link(html)));

      Assert.DoesNotContain(payload, result!.Summary);
      Assert.All(result.Criteria, o => Assert.DoesNotContain(payload, o.Reason + o.Recommendation));
    }

    [Fact]
    public async Task HostileStructuredData_IsIgnored()
    {
      var deep = string.Concat(Enumerable.Repeat("""{"a":[""", 500));
      var head = $"""<script type="application/ld+json">{deep}</script><script type="application/ld+json">not json at all</script>""";

      var result = await Read(Request(Link(Page("practical work.", head: head))));

      Assert.NotNull(result);
      Assert.Equal(CourseCostType.Unknown, result.Cost.Type);
    }

    [Fact]
    public async Task HostileText_FinishesQuickly()
    {
      var text = new StringBuilder("Introduction to Data Analysis ");
      for (var i = 0; i < 4000; i++) text.Append("this this this free free $ 1,1,1,1 12 12 Nov Nov ");
      var stopwatch = Stopwatch.StartNew();

      await Read(Request(CourseQualityLinkCheckResult.Reachable(200, ListingUrl, text.ToString())));

      Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), stopwatch.Elapsed.ToString());
    }

    [Fact]
    public void HostileHtml_FinishesQuickly()
    {
      var html = string.Concat(Enumerable.Repeat("<script type=application/ld+json <meta <title <h1 <!-- ", 20_000));
      var stopwatch = Stopwatch.StartNew();

      CourseQualityPageSignalExtractor.FromHtml(html);

      Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), stopwatch.Elapsed.ToString());
    }
    #endregion

    #region Signal extraction
    [Fact]
    public void Signals_KeepJsonLdAndAllowedMeta()
    {
      var signals = CourseQualityPageSignalExtractor.FromHtml(Fixture("paid-cohort.html"))!;

      Assert.Single(signals.JsonLdBlocks);
      Assert.Equal("Introduction to Data Analysis | Northwind Academy", signals.Title);
      Assert.Equal("Introduction to Data Analysis", signals.Heading);
      Assert.Equal("Introduction to Data Analysis", signals.MetaTags["og:title"]);
      Assert.False(signals.MetaTags.ContainsKey("charset"));
    }

    [Fact]
    public void Signals_AreSizeCapped()
    {
      var block = $$"""<script type="application/ld+json">{"name":"{{new string('x', 20_000)}}"}</script>""";
      var oversized = $$"""<script type="application/ld+json">{"name":"{{new string('y', 300_000)}}"}</script>""";
      var meta = string.Concat(Enumerable.Range(0, 500).Select(_ => $"""<meta name="description" content="{new string('m', 5000)}">"""));

      var signals = CourseQualityPageSignalExtractor.FromHtml($"<html><head><title>{new string('t', 5000)}</title>{oversized}{string.Concat(Enumerable.Repeat(block, 50))}{meta}</head></html>")!;

      Assert.True(signals.JsonLdBlocks.Count <= CourseQualityPageSignalExtractor.MaxJsonLdBlocks);
      Assert.True(signals.JsonLdBlocks.Sum(o => o.Length) <= CourseQualityPageSignalExtractor.MaxJsonLdTotalLength);
      Assert.DoesNotContain(signals.JsonLdBlocks, o => o.Contains("yyy"));
      Assert.True(signals.Title!.Length <= CourseQualityPageSignalExtractor.MaxTextLength);
      Assert.True(signals.MetaTags["description"].Length <= CourseQualityPageSignalExtractor.MaxTextLength);
    }

    [Fact]
    public void Signals_IgnoreCommentedOutMarkup()
    {
      var signals = CourseQualityPageSignalExtractor.FromHtml("""<html><!-- <script type="application/ld+json">{"@type":"Course"}</script> --><body></body></html>""");

      Assert.Empty(signals!.JsonLdBlocks);
    }
    #endregion

    #region Real page patterns
    [Theory]
    [InlineData("Application starts: 23 March 2026 00:00")]
    [InlineData("Applications open 23 March 2026")]
    [InlineData("Admission starts: 23 March 2026")]
    [InlineData("Enrolment starts on 23 March 2026")]
    public async Task Dates_OpeningOfApplications_IsNotTheCourseStart(string line)
    {
      var result = await Read(Request(Link(Page($"{line}. Practical exercises."))));
      Assert.NotNull(result);

      Assert.Equal(55, result.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score);
    }

    [Theory]
    [InlineData("Online course. Progress at your own speed.")]
    [InlineData("Start when suits you best.")]
    public async Task Dates_OwnSpeedWording_IsSelfPaced(string line)
    {
      var result = await Read(Request(Link(Page(line)), openEnded: true));
      Assert.NotNull(result);

      Assert.Equal(60, result.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score);
    }

    [Fact]
    public async Task Match_ProviderNameInTheListingTitle_DoesNotCountAgainstThePage()
    {
      var html = Page("Enrol today.", title: "Greening Your Place").Replace("<h1>Introduction to Data Analysis</h1>", "<h1>Greening Your Place</h1>");
      var request = Request(Link(html), title: "Green Northwind - Greening Your Place");
      request.OrganizationName = "Green Northwind";

      var result = await Read(request);

      Assert.NotNull(result);
      Assert.DoesNotContain(result.Criteria, o => o.Criterion == CourseQualityCriterion.DataComplete && o.Score < 100);
    }

    [Fact]
    public async Task Match_TitleLeftWithOneWord_KeepsTheProviderWords_SoAWeakPageStaysUnmatched()
    {
      var html = Page("Enrol today.", title: "Python Jobs").Replace("<h1>Introduction to Data Analysis</h1>", "<h1>Python Jobs</h1>");
      var request = Request(Link(html), title: "Northwind Academy - Python");
      request.Provider = "Northwind Academy";

      var result = await Read(request);

      Assert.NotNull(result);
      Assert.Contains(result.Criteria, o => o.Criterion == CourseQualityCriterion.DataComplete && o.Score < 100);
    }

    [Theory]
    [InlineData("The form Data Analysis Intake is no longer accepting responses.")]
    [InlineData("This course is no longer accepting applications.")]
    public async Task SoftNotFound_ClosedForm_FailsTheLinkCriterion(string line)
    {
      var html = $"<html><head><title>Introduction to Data Analysis</title></head><body><h1>Introduction to Data Analysis</h1><p>{line}</p><p>{new string('x', 10)} {string.Concat(Enumerable.Repeat("Try contacting the owner of the page. ", 10))}</p></body></html>";

      var result = await Read(Request(Link(html)));

      Assert.NotNull(result);
      Assert.Contains(result.Criteria, o => o.Criterion == CourseQualityCriterion.LinkWorks && o.Score < 50);
    }

    [Fact]
    public async Task Cost_BareDollar_KeepsTheAmountAndQuote_ButClaimsNoCurrency()
    {
      var cost = await CostOf("Price: $49");

      Assert.Equal(CourseCostType.Paid, cost.Type);
      Assert.Equal(49m, cost.Amount);
      Assert.Null(cost.Currency);
      Assert.Contains("$49", cost.Evidence);
    }

    [Fact]
    public async Task Cost_BareDollar_TakesTheCurrencyFromStructuredData()
    {
      var cost = (await ReadFixture("paid-cohort.html")).Cost;

      Assert.Equal("USD", cost.Currency);
    }

    [Fact]
    public async Task Cost_StructuredPriceWithNoCurrency_StaysUnknown()
    {
      var cost = await CostOf("Enrol today.", """<meta property="product:price:amount" content="49" />""");

      Assert.Equal(CourseCostType.Unknown, cost.Type);
    }

    #endregion

    #region Self-paced
    private const string ListedUrl = "https://alison.com/course/introduction-to-data-analysis";

    private static string Silent(string finalBody = "Enrol today.") => Page(finalBody);

    private static CourseQualityAIRequest SelfPacedRequest(string html, string? organization, string? provider = null, string finalUrl = ListedUrl)
    {
      var request = Request(Link(html, finalUrl), openEnded: true);
      request.Provider = provider ?? organization;
      request.OrganizationName = organization;
      return request;
    }

    [Theory]
    [InlineData("Alison")]
    [InlineData("ALISON")]
    public async Task SelfPaced_ListedOrganisation_OnASilentPage_Qualifies(string organization)
    {
      var result = await Read(SelfPacedRequest(Silent(), organization));

      Assert.NotNull(result);
      Assert.True(result.SelfPaced);
      Assert.True(result.Confidence >= DefaultOptions.ReviewConfidenceThreshold);
      Assert.Contains("self-paced", result.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Reason);
    }

    [Fact]
    public async Task SelfPaced_ListedOrganisation_CountsWhenTheProviderIsACoursePublisher()
    {
      var result = await Read(SelfPacedRequest(Silent(), "Alison", "Advance Learning"));

      Assert.NotNull(result);
      Assert.True(result.SelfPaced);
    }

    [Fact]
    public async Task SelfPaced_ASubdomainOfTheConfiguredDomain_Counts()
    {
      var result = await Read(SelfPacedRequest(Silent(), "Alison", finalUrl: "https://www.alison.com/course/x"));

      Assert.NotNull(result);
      Assert.True(result.SelfPaced);
    }

    // finding 3: Provider is partner-supplied
    [Fact]
    public async Task SelfPaced_ProviderFieldAlone_DoesNotQualify()
    {
      var result = await Read(SelfPacedRequest(Silent(), "Some Other Org", "Alison"));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
    }

    // finding 3
    [Theory]
    [InlineData("https://courses.example.org/x")]
    [InlineData("https://notalison.com/x")]
    [InlineData("https://alison.com.evil.example/x")]
    public async Task SelfPaced_ListedOrganisation_OnAnotherDomain_DoesNotQualify(string finalUrl)
    {
      var result = await Read(SelfPacedRequest(Silent(), "Alison", finalUrl: finalUrl));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
      Assert.True(result.Confidence < DefaultOptions.ReviewConfidenceThreshold);
    }

    [Theory]
    [InlineData("Alison Fans Academy")]
    [InlineData("Northwind")]
    [InlineData(null)]
    public async Task SelfPaced_ProviderNotListed_OnASilentPage_StaysInReview(string? organization)
    {
      var result = await Read(SelfPacedRequest(Silent(), organization));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
      Assert.True(result.Confidence < DefaultOptions.ReviewConfidenceThreshold);
    }

    private const string SelfPacedSchema = """<script type="application/ld+json">{"@context":"https://schema.org","@type":"Course","name":"Introduction to Data Analysis","hasCourseInstance":{"@type":"CourseInstance","courseMode":"online self-paced"}}</script>""";

    private static string Schema(string instance) => """<script type="application/ld+json">{"@context":"https://schema.org","@type":"Course","name":"Introduction to Data Analysis","hasCourseInstance":""" + instance + "}</script>";

    // A: body wording never qualifies
    [Theory]
    [InlineData("Self-paced: start anytime.")]
    [InlineData("This is not really a fully self-paced course.")]
    [InlineData("Tutor-led rather than self-paced.")]
    [InlineData("Unlike our self-paced courses, this one meets weekly.")]
    [InlineData("There is no self-paced option.")]
    [InlineData("Prefer self-paced? This one meets live.")]
    [InlineData("Cohort 3: 12 March 2027. See our self-paced options.")]
    [InlineData("Self-paced. This course has been archived.")]
    [InlineData("Self-paced. This course is no longer offered.")]
    [InlineData("Self-paced. We are not accepting new learners.")]
    [InlineData("Self-paced. Sold out.")]
    [InlineData("Self-paced. This class is full.")]
    [InlineData("Self-paced. This course was discontinued.")]
    [InlineData("Watch on demand once the class runs on Tuesday evenings.")]
    [InlineData("This course is not self-paced. Live classes run every Tuesday.")]
    public async Task SelfPaced_BodyWording_NeverQualifies(string line)
    {
      var result = await Read(SelfPacedRequest(Silent(line), "Northwind", finalUrl: ListingUrl));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
      Assert.True(result.Confidence < DefaultOptions.ReviewConfidenceThreshold);
    }

    // A
    [Theory]
    [InlineData("""{"@type":"CourseInstance","courseMode":"not asynchronous"}""")]
    [InlineData("""{"@type":"CourseInstance","courseMode":"not self-paced"}""")]
    [InlineData("""{"@type":"CourseInstance","courseMode":"online"}""")]
    [InlineData("""{"@type":"CourseInstance","courseMode":"asynchronous","courseSchedule":{"@type":"Schedule","repeatFrequency":"P1W"}}""")]
    [InlineData("""{"@type":"CourseInstance","courseMode":"asynchronous","startDate":"not a date"}""")]
    public async Task SelfPaced_StructuredData_WithoutCleanProof_DoesNotQualify(string instance)
    {
      var html = Page("Enrol today.", head: Schema(instance));

      var result = await Read(SelfPacedRequest(html, "Northwind", finalUrl: ListingUrl));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
    }

    // A
    [Fact]
    public async Task SelfPaced_ListingEndDate_Disqualifies()
    {
      var listed = SelfPacedRequest(Silent(), "Alison");
      listed.DateEnd = Now.AddDays(5);

      Assert.False((await Read(listed))!.SelfPaced);
    }

    // A: a listed organisation is still stopped by any date, cohort or schedule on the page
    [Theory]
    [InlineData("Next cohort opens soon.")]
    [InlineData("Applications close 12 March 2027.")]
    [InlineData("Runs in March 2027.")]
    [InlineData("Live classes every Tuesday.")]
    [InlineData("Weekly timetable below.")]
    [InlineData("This course has been archived.")]
    [InlineData("This course is no longer offered.")]
    [InlineData("Sold out.")]
    public async Task SelfPaced_ListedOrganisation_WithAScheduleOrClosureSignal_GoesToReview(string line)
    {
      var result = await Read(SelfPacedRequest(Silent(line), "Alison"));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
      Assert.True(result.Confidence < DefaultOptions.ReviewConfidenceThreshold);
    }

    [Fact]
    public async Task SelfPaced_ListedProvider_WithDisagreeingDates_StaysInReview()
    {
      var result = await Read(SelfPacedRequest(Silent("Starts: 1 March 2026."), "Alison"));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
      Assert.True(result.Confidence < DefaultOptions.ReviewConfidenceThreshold);
    }

    [Theory]
    [InlineData("https://alison.com/login?next=/course")]
    [InlineData("https://alison.com/sign-in")]
    public async Task SelfPaced_LoginWall_ByAddress_DoesNotQualify(string finalUrl)
    {
      var result = await Read(SelfPacedRequest(Silent("Self-paced: start anytime."), "Alison", finalUrl: finalUrl));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
      Assert.True(result.Confidence < DefaultOptions.ReviewConfidenceThreshold);
    }

    [Theory]
    [InlineData("You must log in to view this course.")]
    [InlineData("Redirecting to login...")]
    public async Task SelfPaced_LoginWall_ByWording_DoesNotQualify(string line)
    {
      var result = await Read(SelfPacedRequest(Silent(line), "Alison"));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
    }

    [Fact]
    public async Task SelfPaced_ASignInLinkInTheMenu_IsNotALoginWall()
    {
      var result = await Read(SelfPacedRequest(Silent("Log In Sign Up. Save this course for later."), "Alison"));

      Assert.NotNull(result);
      Assert.True(result.SelfPaced);
    }

    [Fact]
    public async Task SelfPaced_ClosedPage_DoesNotQualify()
    {
      var html = $"<html><head><title>Introduction to Data Analysis</title></head><body><h1>Introduction to Data Analysis</h1><p>This course is no longer accepting applications.</p></body></html>";

      var result = await Read(SelfPacedRequest(html, "Alison"));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
    }

    [Fact]
    public async Task SelfPaced_WeakMatch_DoesNotQualify()
    {
      var html = Page("Enrol today.", title: "All our topics").Replace("<h1>Introduction to Data Analysis</h1>", "<h1>All our topics</h1>");

      var result = await Read(SelfPacedRequest(html, "Alison"));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
    }

    [Fact]
    public async Task SelfPaced_UnknownFormat_DoesNotQualify()
    {
      var html = "<html><head><title>Introduction to Data Analysis</title></head><body><h1>Introduction to Data Analysis</h1><p>" +
        string.Concat(Enumerable.Repeat("Practical units with exercises and quizzes every week. ", 8)) + "</p></body></html>";

      var result = await Read(SelfPacedRequest(html, "Alison"));

      Assert.NotNull(result);
      Assert.Equal(CourseFormat.Unknown, result.Format);
      Assert.False(result.SelfPaced);
    }

    [Fact]
    public async Task SelfPaced_ThroughTheEngine_IsVerified_WithoutClaimingDates()
    {
      var opportunity = CleanListing();
      opportunity.DateEnd = null;
      opportunity.DateStart = Now.AddYears(-3);
      opportunity.OrganizationName = "Alison";
      var link = Link(Silent(), ListedUrl);
      var request = SelfPacedRequest(Silent(), "Alison");

      var assessment = CourseQualityEngine.Assess(opportunity, link, await Read(request), DefaultOptions, Now);

      Assert.Equal(CourseQualityStatus.Verified, assessment.Status);
      Assert.True(assessment.LearnerCard.SelfPaced);
      Assert.False(assessment.LearnerCard.DatesConfirmed);
      Assert.Contains("Start anytime", assessment.LearnerCard.Highlights);
      Assert.DoesNotContain("Dates checked", assessment.LearnerCard.Highlights);
      Assert.Contains("self-paced", assessment.AISummary);
    }
    #endregion

    #region Partner-written structured data is not proof by itself
    private static string Filler => string.Concat(Enumerable.Repeat("Practical units with exercises and quizzes every week. ", 8));

    private static string Ld(string json) => "<script type=\"application/ld+json\">" + json + "</script>";

    private const string FreeCourseLd = """{"@context":"https://schema.org","@type":"Course","name":"Introduction to Data Analysis","offers":{"@type":"Offer","price":"0","priceCurrency":"USD"},"hasCourseInstance":{"@type":"CourseInstance","courseMode":"online"}}""";

    private const string DatedFreeLd = """{"@context":"https://schema.org","@type":"Course","name":"Introduction to Data Analysis","offers":{"@type":"Offer","price":"0","priceCurrency":"USD"},"hasCourseInstance":{"@type":"CourseInstance","courseMode":"online","startDate":"2026-11-12","endDate":"2026-12-24"}}""";

    // x2
    [Fact]
    public async Task VisibleHeading_DecidesTheMatch_NotJsonLdName()
    {
      var html = Named("Crypto Signals VIP Group", head: Ld(FreeCourseLd));

      var result = await Read(Request(Link(html)));

      Assert.False(Matched(result));
      Assert.False(result!.StrongMatch);
    }

    [Fact]
    public async Task HeadingOutsideTheVisibleText_IsNotStrong()
    {
      var html = "<html><head><title>x</title></head><body><template><h1>Introduction to Data Analysis</h1></template><p>About this course: " + Filler + "</p></body></html>";

      Assert.False(Matched(await Read(Request(Link(html)))));
    }

    // x1, x7
    [Theory]
    [InlineData("""{"@context":"https://schema.org","@type":"Course","name":"Introduction to Data Analysis","hasCourseInstance":{"@type":"CourseInstance","courseMode":"online self-paced"}}""")]
    [InlineData("""{"@context":"https://schema.org","@type":"Course","name":"Introduction to Data Analysis","courseMode":"asynchronous"}""")]
    public async Task SelfPaced_StructuredClaim_OffAListedDomain_StaysInReview(string json)
    {
      var result = await Read(SelfPacedRequest(Page("Enrol today.", head: Ld(json)), "Northwind", finalUrl: ListingUrl));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
      Assert.True(result.Confidence < DefaultOptions.ReviewConfidenceThreshold);
    }

    // x3
    [Fact]
    public async Task ThinBody_CannotReachABadge_WhateverTheStructuredDataSays()
    {
      var thin = "<html><head><title>Introduction to Data Analysis</title>" + Ld(DatedFreeLd) + "</head><body><h1>Introduction to Data Analysis</h1><p>Enrol.</p></body></html>";
      var listed = await Read(SelfPacedRequest(thin, "Alison"));
      var dated = await Read(Request(Link(thin)));

      Assert.False(listed?.SelfPaced ?? false);
      Assert.False(dated?.DatesStartConfirmed ?? false);
    }

    // x4
    [Fact]
    public async Task StructuredDatesAndFree_AgainstAVisibleFee_ProveNothing()
    {
      var result = await Read(Request(Link(Page("Pay $499 to enrol.", head: Ld(DatedFreeLd)))));

      Assert.NotNull(result);
      Assert.False(result.DatesStartConfirmed);
      Assert.False(result.DatesEndConfirmed);
      Assert.Equal(CourseCostType.Unknown, result.Cost.Type);
    }

    // x5
    [Theory]
    [InlineData("Northwind", ListingUrl)]
    [InlineData("Alison", ListedUrl)]
    public async Task SelfPacedAndFreeClaims_AgainstAVisibleSchedule_ProveNothing(string organization, string finalUrl)
    {
      var html = Page("Classes meet Tuesdays at 18:00. Pay $499 at the door.", head: Ld(FreeCourseLd));

      var result = await Read(SelfPacedRequest(html, organization, finalUrl: finalUrl));

      Assert.NotNull(result);
      Assert.False(result.SelfPaced);
      Assert.Equal(CourseCostType.Unknown, result.Cost.Type);
    }

    [Fact]
    public async Task StructuredStart_MustBeVisible_ForDatesToCount()
    {
      var hidden = await Read(Request(Link(Page("Enrol today.", head: Ld(DatedFreeLd)))));
      var shown = await Read(Request(Link(Page("Starts 12 November 2026, ends 24 December 2026.", head: Ld(DatedFreeLd)))));

      Assert.False(hidden!.DatesStartConfirmed);
      Assert.True(shown!.DatesStartConfirmed);
      Assert.True(shown.DatesEndConfirmed);
    }

    [Fact]
    public async Task StructuredStart_ContradictedByTheBody_Disagrees()
    {
      var result = await Read(Request(Link(Page("12 November 2026 was the old date. Starts: 1 March 2027.", head: Ld(DatedFreeLd)))));

      Assert.NotNull(result);
      Assert.False(result.DatesStartConfirmed);
      Assert.True(result.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score < 50);
    }

    [Fact]
    public async Task StructuredPrice_AgainstADifferentVisibleAmount_IsUnknown()
    {
      var json = """{"@context":"https://schema.org","@type":"Course","name":"Introduction to Data Analysis","offers":{"@type":"Offer","price":"100","priceCurrency":"EUR"}}""";

      Assert.Equal(CourseCostType.Unknown, (await CostOf("Pay 499 EUR at the door.", Ld(json))).Type);
    }

    [Theory]
    [InlineData("Pay $499 to enrol.")]
    [InlineData("Costs 499 dollars.")]
    public async Task StructuredFree_WithAVisibleAmount_IsUnknown(string body)
    {
      Assert.Equal(CourseCostType.Unknown, (await CostOf(body, Ld(FreeCourseLd))).Type);
    }

    [Theory]
    [InlineData("Pay 499 at the door.")]
    [InlineData("Costs TSh 5000.")]
    [InlineData("CHF 200 at the door.")]
    [InlineData("Rs 500 on arrival.")]
    [InlineData("Costs five hundred dollars.")]
    [InlineData("This is a free course for members.")]
    [InlineData("It is only free for residents.")]
    public async Task StructuredFree_WithMoneyOrConditionWording_IsUnknown(string body)
    {
      Assert.Equal(CourseCostType.Unknown, (await CostOf(body, Ld(FreeCourseLd))).Type);
    }

    [Fact]
    public async Task StructuredFree_WithPlainPage_IsFree()
    {
      Assert.Equal(CourseCostType.Free, (await CostOf("Enrol today.", Ld(FreeCourseLd))).Type);
    }

    [Fact]
    public async Task StructuredFree_AFooterFee_IsUnknown()
    {
      var html = Page("Enrol today.", head: Ld(FreeCourseLd)).Replace("</body>", "<footer>Registration fee: $99</footer></body>");

      Assert.Equal(CourseCostType.Unknown, (await Read(Request(Link(html))))!.Cost.Type);
    }

    [Fact]
    public async Task StructuredFree_AmountAfterManyZeroAmounts_IsUnknown()
    {
      var cost = await CostOf(string.Concat(Enumerable.Repeat("$0 ", 60)) + "$499 at the door", Ld(FreeCourseLd));

      Assert.Equal(CourseCostType.Unknown, cost.Type);
    }

    [Theory]
    [InlineData("Alison", ListedUrl)]
    [InlineData("Northwind", ListingUrl)]
    public async Task StructuredFree_OnATruncatedPage_IsUnknown(string organization, string finalUrl)
    {
      var request = SelfPacedRequest(Page("Enrol today.", head: Ld(FreeCourseLd)), organization, finalUrl: finalUrl);
      request.SourcePageTruncated = true;

      Assert.Equal(CourseCostType.Unknown, (await Read(request))!.Cost.Type);
    }

    [Fact]
    public async Task MetaPriceZero_Alone_NeverGivesFree()
    {
      Assert.Equal(CourseCostType.Unknown, (await CostOf("Enrol today.", """<meta property="product:price:amount" content="0.00" /><meta property="product:price:currency" content="USD" />""")).Type);
    }

    [Theory]
    [InlineData("This is a free course.")]
    [InlineData("This course is free of charge.")]
    public async Task FreeWording_WithoutStructuredProof_IsUnknown(string body)
    {
      Assert.Equal(CourseCostType.Unknown, (await CostOf(body)).Type);
    }

    [Theory]
    [InlineData("Pay $499 to enrol.")]
    [InlineData("Pay 499 at the door.")]
    public async Task StructuredFree_ListedDomain_StillStopsAtARealFee(string body)
    {
      Assert.Equal(CourseCostType.Unknown, (await ListedCost(body, "Alison", ListedUrl)).Type);
    }

    private static async Task<LearnerCardCost> ListedCost(string body, string organization, string finalUrl)
    {
      var result = await Read(SelfPacedRequest(Page(body, head: Ld(FreeCourseLd)), organization, finalUrl: finalUrl));
      Assert.NotNull(result);
      return result.Cost;
    }

    [Fact]
    public async Task StructuredFree_ListedDomain_KeepsFree_PastAnUpsellAmount()
    {
      Assert.Equal(CourseCostType.Free, (await ListedCost("Get your certificate for $29.", "Alison", ListedUrl)).Type);
    }

    [Theory]
    [InlineData("Alison", ListedUrl, "Pay $499 to enrol.")]
    [InlineData("Northwind", ListingUrl, "Get your certificate for $29.")]
    public async Task StructuredFree_UpsellException_IsMinimal(string organization, string finalUrl, string body)
    {
      Assert.Equal(CourseCostType.Unknown, (await ListedCost(body, organization, finalUrl)).Type);
    }
    #endregion

    #region Unambiguous prices only
    [Theory]
    [InlineData("Price: €1.200,00", 1200, "EUR")]
    [InlineData("Fee: $1,200.50", 1200.50, null)]
    public async Task Cost_ThousandSeparators_ParseWhole(string body, double amount, string? currency)
    {
      var cost = await CostOf(body);

      Assert.Equal(CourseCostType.Paid, cost.Type);
      Assert.Equal((decimal)amount, cost.Amount);
      Assert.Equal(currency, cost.Currency);
    }

    [Theory]
    [InlineData("Price: €1.200")]
    [InlineData("Price: $49 2 seats left")]
    [InlineData("Fee: R 1 20")]
    [InlineData("Price: R 1 200")]
    [InlineData("Price: 1 200 EUR")]
    [InlineData("Fee: R 12 500,00")]
    [InlineData("Price: $49 120 hours")]
    [InlineData("Fee: R500 100 learners")]
    public async Task Cost_NumbersThatCouldReadTwoWays_AreUnknown(string body)
    {
      Assert.Equal(CourseCostType.Unknown, (await CostOf(body)).Type);
    }

    [Theory]
    [InlineData("Fee: R500 for members, R900 for others")]
    [InlineData("Price: $49, early bird $39")]
    [InlineData("Price: $49 for members")]
    [InlineData("Tuition: $4,000 a semester")]
    [InlineData("Fee: $120 a term")]
    [InlineData("Fee: $120 per term")]
    [InlineData("Tuition: $900 per year")]
    [InlineData("Price: $10 a month")]
    [InlineData("Price: $49 early bird")]
    [InlineData("Price: $49 for module 1")]
    [InlineData("Price: $49 to start")]
    [InlineData("Price: $49 for South Africans only")]
    [InlineData("Price: $49 until Friday")]
    [InlineData("Fee: $49 residents")]
    public async Task Cost_TiersAndPeriods_AreUnknown(string body)
    {
      Assert.Equal(CourseCostType.Unknown, (await CostOf(body)).Type);
    }

    [Theory]
    [InlineData("We offer free course materials.")]
    [InlineData("Textbooks are provided free of charge.")]
    [InlineData("Try a free course from our library.")]
    [InlineData("Attendance is free of charge.")]
    public async Task Cost_FreePhrases_NotAboutThisCourse_AreUnknown(string body)
    {
      Assert.Equal(CourseCostType.Unknown, (await CostOf(body)).Type);
    }
    #endregion

    #region Strict match (B)
    [Fact]
    public async Task StrictMatch_PartnerProviderField_IsNeverStrippedAsALabel()
    {
      var request = Request(Link(Named("Python Programming")), title: "Python Programming - Advanced");
      request.Provider = "Advanced";

      Assert.False(Matched(await Read(request)));
    }

    [Theory]
    [InlineData("https://courses.example.org/skills/data-analysis-basics")]
    [InlineData("https://courses.example.org/learn/data-analysis-basics")]
    public async Task StrictMatch_SkillsAndLearnAddresses_AreNotStrong(string finalUrl)
    {
      Assert.False(Matched(await Read(Request(Link(Named("Data Analysis Basics"), finalUrl), title: "Data Analysis Basics"))));
    }

    [Fact]
    public async Task StrictMatch_SchemaPrefixedItemList_IsNotStrong()
    {
      var head = Ld("""{"@type":"schema:ItemList","name":"Data Analysis Basics"}""");

      Assert.False(Matched(await Read(Request(Link(Named("Data Analysis Basics", head: head)), title: "Data Analysis Basics"))));
    }

    [Theory]
    [InlineData("This course will not run this year.")]
    [InlineData("This course is not running.")]
    public async Task PageGone_NotRunningWording(string line)
    {
      Assert.True((await Read(Request(Link(Named("Introduction to Data Analysis", line)))))!.PageGone);
    }

    [Fact]
    public async Task StrictMatch_OrganisationWords_DoNotRemoveIdentifyingWords()
    {
      var request = Request(Link(Named("Python Programming")), title: "Advanced Python Programming");
      request.Provider = "Advanced Academy";
      request.OrganizationName = "Advanced Academy";

      Assert.False(Matched(await Read(request)));
    }

    [Theory]
    [InlineData("Northwind Academy - Data Analysis Basics", "Data Analysis Basics")]
    [InlineData("Data Analysis Basics | Northwind Academy", "Data Analysis Basics")]
    [InlineData("Data Analysis Basics", "Data Analysis Basics | Northwind Academy")]
    public async Task StrictMatch_ProviderNameAsASeparateLabel_IsDropped(string listing, string heading)
    {
      var request = Request(Link(Named(heading)), title: listing);
      request.OrganizationName = "Northwind Academy";

      Assert.True(Matched(await Read(request)));
    }

    [Fact]
    public async Task StrictMatch_ATrailingCourseWord_IsAllowed()
    {
      Assert.True(Matched(await Read(Request(Link(Named("Data Analysis Essentials course")), title: "Data Analysis Essentials"))));
    }

    [Fact]
    public async Task StrictMatch_TrailingWordAllowance_IsOnlyCourse()
    {
      Assert.False(Matched(await Read(Request(Link(Named("Data Analysis Essentials bootcamp")), title: "Data Analysis Essentials"))));
    }

    [Theory]
    [InlineData("https://courses.example.org/subject/data-analysis-basics")]
    [InlineData("https://courses.example.org/subjects/data-analysis-basics")]
    public async Task StrictMatch_SubjectAddress_IsNotStrong(string finalUrl)
    {
      Assert.False(Matched(await Read(Request(Link(Named("Data Analysis Basics"), finalUrl), title: "Data Analysis Basics"))));
    }

    [Fact]
    public async Task StrictMatch_ArrayTypeListPage_IsNotStrong()
    {
      var head = Ld("""{"@type":["WebPage","CollectionPage"],"name":"Data Analysis Basics"}""");

      Assert.False(Matched(await Read(Request(Link(Named("Data Analysis Basics", head: head)), title: "Data Analysis Basics"))));
    }

    [Fact]
    public async Task PageGone_CancelledWording()
    {
      var result = await Read(Request(Link(Named("Introduction to Data Analysis", "This course was cancelled last year."))));

      Assert.NotNull(result);
      Assert.True(result.PageGone);
    }

    [Theory]
    [InlineData("Data Analysis Basics", "data analysis: basics!")]
    [InlineData("Data Analysis for the Basics", "Data Analysis Basics")]
    [InlineData("Northwind - Data Analysis Basics", "Data Analysis Basics")]
    public async Task StrictMatch_EqualNames_AreStrong(string listing, string heading)
    {
      var request = Request(Link(Named(heading)), title: listing);
      request.OrganizationName = "Northwind";

      Assert.True(Matched(await Read(request)));
    }

    [Theory]
    [InlineData("Introduction to Python", "Advanced Python")]
    [InlineData("Introduction to Python", "Python")]
    [InlineData("Basics of Power Query", "Fundamentals of Power Query")]
    public async Task StrictMatch_DistinguishingWords_AreKept(string listing, string heading)
    {
      Assert.False(Matched(await Read(Request(Link(Named(heading)), title: listing))));
    }

    [Theory]
    [InlineData("Data Skills Courses", "Data Skills Courses")]
    [InlineData("Data Analyst Path", "Data Analyst Path")]
    [InlineData("Data Skills Collection", "Data Skills Collection")]
    public async Task StrictMatch_ListNames_AreNotStrong_EvenIfEqual(string listing, string heading)
    {
      Assert.False(Matched(await Read(Request(Link(Named(heading)), title: listing))));
    }

    [Theory]
    [InlineData("https://courses.example.org/topic/data-analysis-basics")]
    [InlineData("https://courses.example.org/topics/data-analysis-basics")]
    [InlineData("https://courses.example.org/paths/data-analysis-basics")]
    public async Task StrictMatch_TopicAddress_IsNotStrong(string finalUrl)
    {
      Assert.False(Matched(await Read(Request(Link(Named("Data Analysis Basics"), finalUrl), title: "Data Analysis Basics"))));
    }

    [Fact]
    public async Task StrictMatch_ItemListOfTwoCourses_IsNotStrong()
    {
      var head = """<script type="application/ld+json">{"@type":"ItemList","itemListElement":[{"@type":"ListItem","position":1,"item":{"@type":"Course","name":"Data Analysis Basics"}},{"@type":"ListItem","position":2,"item":{"@type":"Course","name":"Other Course"}}]}</script>""";

      Assert.False(Matched(await Read(Request(Link(Named("Data Analysis Basics", head: head)), title: "Data Analysis Basics"))));
    }
    #endregion

    #region Gone anywhere (D)
    [Theory]
    [InlineData("This course is no longer available.")]
    [InlineData("This course was discontinued.")]
    [InlineData("This course has ended.")]
    [InlineData("Enrolment is closed.")]
    public async Task PageGone_FoundAnywhereOnThePage(string line)
    {
      var html = Named("Introduction to Data Analysis", string.Concat(Enumerable.Repeat("Practical units with exercises and quizzes every week. ", 30)) + line);

      var result = await Read(Request(Link(html)));

      Assert.NotNull(result);
      Assert.True(result.PageGone);
    }

    [Fact]
    public async Task PageGone_EvenWhenTheListingTitleItselfContains404()
    {
      var result = await Read(Request(Link(Named("Fixing a 404 Error", "404 page not found")), title: "Fixing a 404 Error"));

      Assert.NotNull(result);
      Assert.True(result.PageGone);
    }

    [Fact]
    public async Task RedirectedHome_IsNotAStrongMatch()
    {
      var request = Request(Link(Named("Introduction to Data Analysis"), "https://courses.example.org/"));

      var result = await Read(request);

      Assert.NotNull(result);
      Assert.False(result.StrongMatch);
    }
    #endregion

    #region Labelled cost (E)
    [Theory]
    [InlineData("Enrol in R 101 this term.")]
    [InlineData("Only R 4.2 left.")]
    [InlineData("Enrol for $49 today.")]
    [InlineData("Free to enrol. R499 to receive grades.")]
    [InlineData("Free to join. 49 dollars to finish.")]
    [InlineData("A free course. 49 dollars for the exam.")]
    [InlineData("Price: $49 p/m")]
    [InlineData("Monthly plan price: $49")]
    [InlineData("Price: $49 in 3 instalments")]
    [InlineData("Fee: $49 deposit")]
    public async Task Cost_WithoutAPlainOneOffLabel_IsUnknown(string body)
    {
      Assert.Equal(CourseCostType.Unknown, (await CostOf(body)).Type);
    }

    [Theory]
    [InlineData("Fee: R 1,299.50", 1299.50)]
    [InlineData("Tuition: $49", 49)]
    [InlineData("Cost: 30 EUR", 30)]
    public async Task Cost_LabelledOneOffPrice_IsPaid(string body, double amount)
    {
      var cost = await CostOf(body);

      Assert.Equal(CourseCostType.Paid, cost.Type);
      Assert.Equal((decimal)amount, cost.Amount);
    }

    [Theory]
    [InlineData("Free to join and learn.")]
    [InlineData("Price: Free")]
    [InlineData("Price: $0")]
    [InlineData("This course is completely free for everyone.")]
    [InlineData("It is free to enrol.")]
    [InlineData("Available at no cost to learners.")]
    [InlineData("Enrol for free.")]
    [InlineData("100% free.")]
    public async Task Cost_LooserFreeWording_IsUnknown(string body)
    {
      Assert.Equal(CourseCostType.Unknown, (await CostOf(body)).Type);
    }

    #endregion

    #region Review findings
    private static string Named(string name, string body = "About this course: enrol today.", string title = "", string head = "") =>
      $"<html><head><title>{(title == "" ? name : title)}</title>{head}</head><body><h1>{name}</h1><p>{body}</p><p>{string.Concat(Enumerable.Repeat("Practical units with exercises and quizzes every week. ", 8))}</p></body></html>";

    private static bool Matched(CourseQualityAIResult? result) =>
      result != null && result.Criteria.Any(o => o.Criterion == CourseQualityCriterion.DataComplete && o.Score == 100);

    // finding 2
    [Fact]
    public async Task Match_CataloguePage_IsNotStrong()
    {
      var result = await Read(Request(Link(Named("Digital Marketing Strategy Courses")), title: "Digital Marketing Strategy"));

      Assert.False(Matched(result));
    }

    // finding 2
    [Theory]
    [InlineData("<h1>Data Analysis Basics</h1>", "https://courses.example.org/course/index.php?categoryid=10", "")]
    [InlineData("<h1>Data Analysis Basics</h1>", "https://courses.example.org/courses", "")]
    [InlineData("<h1>Data Analysis Basics</h1>", "https://courses.example.org/category/data-analysis-basics", "")]
    [InlineData("<h1>Data Analysis Basics</h1>", "https://courses.example.org/data", "<script type=\"application/ld+json\">{\"@type\":\"CollectionPage\",\"name\":\"Data Analysis Basics\"}</script>")]
    [InlineData("<h1>Data Analysis Basics</h1>", "https://courses.example.org/data", "<script type=\"application/ld+json\">{\"@type\":\"ItemList\",\"name\":\"Data Analysis Basics\"}</script>")]
    public async Task Match_ListPageSignals_AreNotStrong(string heading, string finalUrl, string head)
    {
      var html = Named("x", head: head, title: "Data Analysis Basics").Replace("<h1>x</h1>", heading);

      var result = await Read(Request(Link(html, finalUrl), title: "Data Analysis Basics"));

      Assert.False(Matched(result));
    }

    // finding 2
    [Fact]
    public async Task Match_AListedCourseWithItsOwnSchema_IsNotAListPage()
    {
      var head = "<script type=\"application/ld+json\">{\"@context\":\"https://schema.org\",\"@type\":\"Course\",\"name\":\"Data Analysis Basics\"}</script>";

      var result = await Read(Request(Link(Named("Data Analysis Basics", head: head), "https://courses.example.org/courses/data-analysis-basics/"), title: "Data Analysis Basics"));

      Assert.True(Matched(result));
    }

    // finding 2
    [Fact]
    public async Task Match_TitleTagAlone_IsNotStrong()
    {
      var html = Named("Welcome to our learning site", title: "Data Analysis Basics");

      var result = await Read(Request(Link(html), title: "Data Analysis Basics"));

      Assert.False(Matched(result));
    }

    // finding 2
    [Theory]
    [InlineData("Advanced Spreadsheet Formulas For Accountants And Bookkeepers", "Spreadsheet Formulas For Accountants")]
    [InlineData("HTML", "HTML Jobs And Careers In Software Development Today")]
    public async Task Match_PartialOverlap_IsNotStrong(string listing, string pageName)
    {
      var result = await Read(Request(Link(Named(pageName)), title: listing));

      Assert.False(Matched(result));
    }

    // finding 7
    [Fact]
    public async Task Cost_ACourseCodeInTheName_IsNotAPrice()
    {
      var html = Named("R 101 Data Analysis Basics", "Enrol today.");

      var result = await Read(Request(Link(html), title: "R 101 Data Analysis Basics"));

      Assert.NotNull(result);
      Assert.Equal(CourseCostType.Unknown, result.Cost.Type);
    }

    // finding 7
    [Fact]
    public async Task Cost_FreeToStartThenPaid_IsNotFree()
    {
      var cost = await CostOf("This course is free to start. Then $49 to finish.");

      Assert.NotEqual(CourseCostType.Free, cost.Type);
    }

    // finding 7
    [Fact]
    public async Task Cost_FreeWordingOnATruncatedPage_IsUnknown()
    {
      var request = Request(Link(Page("This course is free of charge.")));
      request.SourcePageTruncated = true;

      var result = await Read(request);

      Assert.NotNull(result);
      Assert.Equal(CourseCostType.Unknown, result.Cost.Type);
    }

    // finding 7
    // finding 6
    [Theory]
    [InlineData("This course is no longer available.")]
    [InlineData("This form is no longer accepting responses.")]
    public async Task SoftNotFound_SetsPageGone(string line)
    {
      var result = await Read(Request(Link(Named("Introduction to Data Analysis", line))));

      Assert.NotNull(result);
      Assert.True(result.PageGone);
    }
    #endregion

    #region Through the real engine
    [Fact]
    public async Task EndToEnd_FixturePage_IsVerified_WithPaidCostAndEvidence()
    {
      var assessment = await Assess("paid-cohort.html", DatedListing());

      Assert.Equal(CourseQualityStatus.Verified, assessment.Status);
      Assert.False(assessment.AIUsed);
      Assert.Equal("source-page-reader", assessment.AIModel);
      Assert.Equal(CourseCostType.Paid, assessment.LearnerCard.Cost.Type);
      Assert.Equal(49m, assessment.LearnerCard.Cost.Amount);
      Assert.Equal("USD", assessment.LearnerCard.Cost.Currency);
      Assert.Contains("$49", assessment.LearnerCard.Cost.Evidence);
      Assert.Equal(CourseFormat.Course, assessment.LearnerCard.Format);
    }

    [Theory]
    [InlineData("javascript-shell.html")]
    [InlineData("bot-challenge.html")]
    public async Task EndToEnd_ThinPage_NeedsReview(string fixture)
    {
      var assessment = await Assess(fixture, DatedListing());

      Assert.Equal(CourseQualityStatus.NeedsReview, assessment.Status);
      Assert.Equal(CourseCostType.Unknown, assessment.LearnerCard.Cost.Type);
    }

    [Theory]
    [InlineData("generic-homepage.html")]
    [InlineData("other-course.html")]
    [InlineData("catalogue.html")]
    [InlineData("conflicting-dates.html")]
    [InlineData("soft-404.html")]
    public async Task EndToEnd_Mismatch_IsNotVerified(string fixture)
    {
      var assessment = await Assess(fixture, DatedListing());

      Assert.NotEqual(CourseQualityStatus.Verified, assessment.Status);
    }

    [Fact]
    public async Task EndToEnd_SelfPacedListing_IsVerifiedAsSelfPaced_WithCostShown()
    {
      var listing = Listing(openEnded: true);
      listing.OrganizationName = "Alison";

      var assessment = await Assess("alison-style-free.html", listing, ListedUrl);

      Assert.Equal(CourseQualityStatus.Verified, assessment.Status);
      Assert.True(assessment.LearnerCard.SelfPaced);
      Assert.False(assessment.LearnerCard.DatesConfirmed);
      Assert.Equal(CourseCostType.Free, assessment.LearnerCard.Cost.Type);
    }

    [Fact]
    public async Task EndToEnd_APerfectPage_CannotClearAHardGate()
    {
      var listing = DatedListing();
      listing.DateEnd = Now.AddDays(-1);
      listing.DateStart = Now.AddDays(-2);
      var expired = await Assess("paid-cohort.html", listing);

      var hidden = DatedListing();
      hidden.Hidden = true;
      var hiddenResult = await Assess("paid-cohort.html", hidden);

      Assert.Equal(CourseQualityStatus.NotVerified, expired.Status);
      Assert.Equal(CourseQualityStatus.NotVerified, hiddenResult.Status);
      Assert.True(expired.Score <= new CourseQualityOptions().HardGateScoreCap);
    }

    [Fact]
    public async Task EndToEnd_NullResultFromTheReader_IsNeverAPass()
    {
      var assessment = CourseQualityEngine.Assess(DatedListing(), Link(Fixture("javascript-shell.html")), null, new CourseQualityOptions(), Now);

      Assert.NotEqual(CourseQualityStatus.Verified, assessment.Status);
      await Task.CompletedTask;
    }
    #endregion

    #region Live (off by default)
    [Fact]
    public async Task Live_ReadsARealPage_WithoutThrowing()
    {
      Assert.SkipWhen(string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("COURSE_QUALITY_LIVE")), "COURSE_QUALITY_LIVE is not set");

      var checker = new CourseQualityLinkChecker(Microsoft.Extensions.Logging.Abstractions.NullLogger<CourseQualityLinkChecker>.Instance, Options.Create(new CourseQualityOptions()),
        new DnsCourseQualityHostResolver(), CourseQualityLinkChecker.CreatePinnedHandler());
      var link = await checker.Check("https://alison.com/course/introduction-to-data-analysis", TestContext.Current.CancellationToken);

      await Read(Request(link));
      checker.Dispose();
    }
    #endregion
  }
}
