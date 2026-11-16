using Xunit;
using Yoma.Core.Domain.CourseQuality.Extensions;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityPageEvidenceTests
  {
    private const string Title = "Introduction to Data Analysis";

    /// <summary>
    /// Shaped like an Alison course page: a modules section with one heading per module and lessons under each, a "what you
    /// will learn" list, an assessment, a paid certificate and a site menu that must not count as a curriculum.
    /// </summary>
    private const string AlisonLikePage = """
      <!doctype html><html><head><title>Introduction to Data Analysis | Free Online Course</title>
      <script type="application/ld+json">{"@context":"https://schema.org","@type":"Course","name":"Introduction to Data Analysis",
        "teaches":["Clean data","Explore data","Present data","Use pivot tables"],"inLanguage":"en",
        "educationalCredentialAwarded":"Certificate","syllabusSections":[{"@type":"Syllabus","name":"Module 1"},{"@type":"Syllabus","name":"Module 2"}]}</script>
      </head><body>
      <nav><ul><li>Courses</li><li>Careers</li><li>Diplomas</li><li>Certificates</li><li>Log in</li></ul></nav>
      <h1>Introduction to Data Analysis</h1>
      <p>This free online course is self-paced. Learn on any device, including your phone.</p>
      <h2>What You Will Learn In This Free Course</h2>
      <ul><li>Clean messy spreadsheets</li><li>Explore a dataset</li><li>Summarise results</li><li>Present findings</li><li>Build a pivot table</li></ul>
      <h2>Course Modules</h2>
      <div class="module"><h3>Module 1: Getting Started</h3><ul><li>Lesson 1</li><li>Lesson 2</li></ul></div>
      <div class="module"><h3>Module 2: Cleaning Data</h3><ul><li>Lesson 1</li><li>Lesson 2</li><li>Lesson 3</li></ul></div>
      <div class="module"><h3>Module 3: Exploring Data</h3><ul><li>Lesson 1</li></ul></div>
      <div class="module"><h3>Module 4: Presenting Data</h3><ul><li>Lesson 1</li></ul></div>
      <div class="module"><h3>Course Assessment</h3><p>Pass the final assessment with 80% or more.</p></div>
      <h2>Who Is This Course For?</h2><p>Anyone who works with numbers. No prior experience needed.</p>
      <h2>Certification</h2><p>All Alison courses are free. Pay $49 for your digital certificate when you graduate.</p>
      <footer><ul><li>About</li><li>Privacy</li></ul></footer>
      </body></html>
      """;

    private static CourseQualityPageEvidence FromPage(string html, bool matched = true) =>
      CourseQualityPageEvidenceExtractor.Extract(CourseQualityPageText.FromHtml(html, int.MaxValue), CourseQualityPageSignalExtractor.FromHtml(html), matched, "Listing text.", Title);

    #region Page structure
    [Fact]
    public void Sections_CountListItemsAndSubHeadings_LeavingTheMenuOut()
    {
      var signals = CourseQualityPageSignalExtractor.FromHtml(AlisonLikePage)!;

      var outcomes = signals.Sections.Single(o => o.Heading.StartsWith("What You Will Learn", StringComparison.Ordinal));
      Assert.Equal(2, outcomes.Level);
      Assert.Equal(5, outcomes.ListItems);

      var modules = signals.Sections.Single(o => o.Heading == "Course Modules");
      Assert.Equal(0, modules.ListItems);
      Assert.Equal(5, modules.SubHeadings);

      Assert.Equal(3, signals.Sections.Single(o => o.Heading == "Module 2: Cleaning Data").ListItems);
      Assert.DoesNotContain(signals.Sections, o => o.Heading.Contains("Log in", StringComparison.Ordinal));
      Assert.Equal("Introduction to Data Analysis", signals.Sections[0].Heading);
    }

    [Fact]
    public void Sections_AreCapped()
    {
      var html = string.Concat(Enumerable.Range(0, 200).Select(i => $"<h2>Heading {i}</h2><ul><li>a</li></ul>"));

      var signals = CourseQualityPageSignalExtractor.FromHtml(html)!;

      Assert.Equal(CourseQualityPageSignalExtractor.MaxSections, signals.Sections.Count);
    }

    [Fact]
    public void Sections_KeepNoMarkup_FromHeadings()
    {
      var signals = CourseQualityPageSignalExtractor.FromHtml("<h2>What <b>you</b> will learn‮</h2><ul><li>x</li></ul>")!;

      Assert.Equal("What you will learn", signals.Sections.Single().Heading);
    }
    #endregion

    #region Page evidence
    [Fact]
    public void AlisonLikePage_GivesTheCurriculumOutcomesAndCertificate()
    {
      var evidence = FromPage(AlisonLikePage);

      Assert.Equal(CourseQualityEvidenceSource.Page, evidence.Source);
      Assert.True(evidence.HasCourseJsonLd);
      Assert.True(evidence.JsonLdSyllabus);
      Assert.True(evidence.JsonLdTeaches);
      Assert.True(evidence.JsonLdCredential);
      Assert.Equal(5, evidence.OutcomeCount);
      Assert.Equal(5, evidence.ModuleCount);
      Assert.True(evidence.AudienceStated);
      Assert.True(evidence.PrerequisitesStated);
      Assert.True(evidence.CertificateMentioned);
      Assert.True(evidence.CertificatePaid);
      Assert.True(evidence.AssessmentMentioned);
      Assert.True(evidence.MobileMentioned);
      Assert.Contains("en", evidence.Languages);
      Assert.InRange(evidence.WordCount, 80, 200);
    }

    [Fact]
    public void AlisonLikePage_EarnsTheFullSyllabusScore()
    {
      var input = new CourseGradeInput
      {
        OpportunityId = Guid.NewGuid(),
        OpportunityTitle = Title,
        OrganizationId = Guid.NewGuid(),
        OrganizationName = "Alison",
        Status = CourseQualityStatus.Verified,
        PageEvidence = FromPage(AlisonLikePage),
        DateCreated = Now
      };

      var result = CourseQualityGradingEngine.Grade([input], CourseQualityGrading.Configured(new CourseQualityOptions()), Now).Single();

      Assert.Equal(100, result.Factors.Single(o => o.Factor == CourseGradeFactor.SyllabusAvailable).Score);
    }

    [Fact]
    public void NumberedModules_CountWithoutAList()
    {
      var evidence = FromPage("<h1>Introduction to Data Analysis</h1><p>Week 1 basics. Week 2 cleaning. Week 3 charts. Week 3 review. Module 1 intro.</p>");

      Assert.Equal(3, evidence.ModuleCount);
      Assert.False(evidence.HasCourseJsonLd);
    }

    [Fact]
    public void AnUnmatchedPage_IsNotLentToTheListing()
    {
      var evidence = CourseQualityPageEvidenceExtractor.Extract(CourseQualityPageText.FromHtml(AlisonLikePage, int.MaxValue),
        CourseQualityPageSignalExtractor.FromHtml(AlisonLikePage), false, "A short listing.", Title);

      Assert.Equal(CourseQualityEvidenceSource.Listing, evidence.Source);
      Assert.Equal(0, evidence.ModuleCount);
      Assert.False(evidence.HasCourseJsonLd);
    }

    [Fact]
    public void ListingText_IsReadLineByLine()
    {
      var description = """
        A practical course for new analysts.

        What you will learn:
        - Clean data
        - Explore data
        * Present data

        Course outline
        1. Spreadsheets
        2. Charts
        3. Reports
        4. Dashboards
        Finish with a quiz and earn a certificate. Available in English and French. Lessons are text-based and work offline.
        """;

      var evidence = CourseQualityPageEvidenceExtractor.Extract(null, null, false, description, Title);

      Assert.Equal(CourseQualityEvidenceSource.Listing, evidence.Source);
      Assert.Equal(3, evidence.OutcomeCount);
      Assert.Equal(4, evidence.ModuleCount);
      Assert.True(evidence.AssessmentMentioned);
      Assert.True(evidence.CertificateMentioned);
      Assert.False(evidence.CertificatePaid);
      Assert.True(evidence.TextMentioned);
      Assert.True(evidence.OfflineMentioned);
      Assert.Equal(["English", "French"], evidence.Languages);
    }

    [Fact]
    public void ListingText_WithMarkup_UsesItsStructure()
    {
      var evidence = CourseQualityPageEvidenceExtractor.Extract(null, null, false, "<h3>Learning outcomes</h3><ul><li>a</li><li>b</li><li>c</li><li>d</li></ul>", Title);

      Assert.Equal(4, evidence.OutcomeCount);
    }

    [Fact]
    public void LanguageNames_CountOnlyWhereTheTextSpeaksOfLanguage()
    {
      var evidence = CourseQualityPageEvidenceExtractor.Extract(null, null, false, "Business English for beginners.", Title);

      Assert.Empty(evidence.Languages);
    }

    [Fact]
    public void EmptyListing_GivesEmptyEvidence()
    {
      var evidence = CourseQualityPageEvidenceExtractor.Extract(null, null, true, null, Title);

      Assert.Equal(CourseQualityEvidenceSource.Listing, evidence.Source);
      Assert.Equal(0, evidence.WordCount);
    }

    [Fact]
    public void HugeInput_IsCapped()
    {
      var text = string.Concat(Enumerable.Repeat("Module 7 lesson text ", 50_000));

      var evidence = CourseQualityPageEvidenceExtractor.Extract(text, null, true, null, Title);

      Assert.True(evidence.WordCount <= CourseQualityPageEvidenceExtractor.MaxTextLength);
      Assert.Equal(1, evidence.ModuleCount);
    }

    [Fact]
    public void Evidence_RoundTripsOnTheRecord()
    {
      var record = new CourseQualityAssessmentRecord { PageEvidence = FromPage(AlisonLikePage).Serialize() };

      var evidence = record.ToPageEvidence()!;

      Assert.Equal(5, evidence.ModuleCount);
      Assert.Null(new CourseQualityAssessmentRecord().ToPageEvidence());
    }
    #endregion
  }
}
