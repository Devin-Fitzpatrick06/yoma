using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  public sealed record CourseQualityOffer(decimal? Amount, string? Currency, bool IsFree, string? Quote, bool FromMetaTag = false);

  public sealed class CourseQualityStructuredFacts
  {
    public bool HasCourseType { get; set; }

    public bool HasEventType { get; set; }

    public bool EventIsOffline { get; set; }

    /// <summary>
    /// A course instance declares itself self-paced or asynchronous, with no schedule of its own.
    /// </summary>
    public bool SelfPaced { get; set; }

    /// <summary>
    /// A date or schedule anywhere in this listing's structured data, parseable or not.
    /// </summary>
    public bool HasSchedule { get; set; }

    public bool? AccessibleForFree { get; set; }

    public string? AccessibleForFreeQuote { get; set; }

    /// <summary>
    /// More than one price in one offer (AggregateOffer with a range), so no single amount can be claimed.
    /// </summary>
    public bool PriceIsRange { get; set; }

    public string? Duration { get; set; }

    public List<string> Names { get; } = [];

    public List<DateTimeOffset> Starts { get; } = [];

    public List<DateTimeOffset> Ends { get; } = [];

    public List<CourseQualityOffer> Offers { get; } = [];

    public bool HasPageType => HasCourseType || HasEventType;
  }

  /// <summary>
  /// Reads schema.org Course, CourseInstance, Event and EducationEvent from JSON-LD. Malformed blocks are skipped, nesting
  /// is limited by the parser and the node walk is capped. A page listing many courses only counts through the one
  /// named like this listing, so a catalogue cannot lend a listing another course's price.
  /// </summary>
  public static partial class CourseQualityStructuredData
  {
    #region Class Variables
    private const int MaxNodes = 500;
    private const int MinYear = 2000;
    private const int MaxYear = 2100;
    private const int RegexTimeoutMilliseconds = 1000;
    private const decimal MaxAmount = 10_000_000m;

    private static readonly string[] ScheduleProperties = ["startDate", "endDate", "courseSchedule", "eventSchedule"];

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
      MaxDepth = 32,
      AllowTrailingCommas = true,
      CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly HashSet<string> CourseTypes = new(StringComparer.OrdinalIgnoreCase) { "Course", "CourseInstance" };
    private static readonly HashSet<string> EventTypes = new(StringComparer.OrdinalIgnoreCase) { "Event", "EducationEvent" };
    #endregion

    #region Public Members
    public static CourseQualityStructuredFacts Read(CourseQualityPageSignals? signals, string listingTitle)
    {
      var facts = new CourseQualityStructuredFacts();
      if (signals == null) return facts;

      var nodes = new List<(JsonElement Node, string Block)>();
      var documents = new List<JsonDocument>();

      try
      {
        foreach (var block in signals.JsonLdBlocks)
        {
          try
          {
            var document = JsonDocument.Parse(block, ParseOptions);
            documents.Add(document);
            Collect(document.RootElement, block, nodes);
          }
          catch (JsonException)
          {
            // sites publish broken JSON-LD; the rest of the page still counts
          }
        }

        var named = nodes.Select(o => (o.Node, o.Block, Name: Text(o.Node, "name"))).ToList();
        facts.Names.AddRange(named.Where(o => o.Name != null).Select(o => o.Name!));

        var relevant = named.Count == 1 ? named : [.. named.Where(o => CourseQualityPageMatch.NameMatches(listingTitle, o.Name))];
        foreach (var (node, block, _) in relevant) Apply(node, block, facts);
      }
      finally
      {
        documents.ForEach(o => o.Dispose());
      }

      ReadMetaFacts(signals, facts);

      return facts;
    }
    #endregion

    #region Private Members
    private static void Collect(JsonElement element, string block, List<(JsonElement Node, string Block)> nodes)
    {
      if (nodes.Count >= MaxNodes) return;

      if (element.ValueKind == JsonValueKind.Array)
      {
        foreach (var item in element.EnumerateArray()) Collect(item, block, nodes);
        return;
      }

      if (element.ValueKind != JsonValueKind.Object) return;

      if (Types(element).Any(o => CourseTypes.Contains(o) || EventTypes.Contains(o)))
      {
        nodes.Add((element, block));
        return; // its instances and offers belong to it and are read with it
      }

      foreach (var property in element.EnumerateObject()) Collect(property.Value, block, nodes);
    }

    private static List<string> Types(JsonElement node)
    {
      if (!node.TryGetProperty("@type", out var type)) return [];

      var values = type.ValueKind == JsonValueKind.Array
        ? type.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.String).Select(o => o.GetString()!)
        : type.ValueKind == JsonValueKind.String ? [type.GetString()!] : [];

      // "https://schema.org/Course" and "schema:Course" both name Course
      return [.. values.Select(o => o[(o.LastIndexOfAny(['/', ':', '#']) + 1)..])];
    }

    private static string? Text(JsonElement node, string property)
    {
      if (!node.TryGetProperty(property, out var value)) return null;

      if (value.ValueKind == JsonValueKind.Array) value = value.EnumerateArray().FirstOrDefault();

      return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static IEnumerable<JsonElement> Items(JsonElement node, string property)
    {
      if (!node.TryGetProperty(property, out var value)) return [];

      return value.ValueKind switch
      {
        JsonValueKind.Array => value.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.Object).ToList(),
        JsonValueKind.Object => [value],
        _ => []
      };
    }

    private static void Apply(JsonElement node, string block, CourseQualityStructuredFacts facts)
    {
      var types = Types(node);

      if (types.Any(CourseTypes.Contains)) facts.HasCourseType = true;

      if (types.Any(EventTypes.Contains))
      {
        // CourseInstance is an Event in schema.org, but on a page it signals a course
        facts.HasEventType = true;
        if (Text(node, "eventAttendanceMode")?.Contains("Offline", StringComparison.OrdinalIgnoreCase) == true) facts.EventIsOffline = true;
      }

      ApplyInstance(node, block, facts);
      foreach (var instance in Items(node, "hasCourseInstance")) ApplyInstance(instance, block, facts);

      facts.Duration ??= Text(node, "timeRequired");

      if (node.TryGetProperty("isAccessibleForFree", out var free))
      {
        var value = free.ValueKind switch
        {
          JsonValueKind.True => true,
          JsonValueKind.False => false,
          JsonValueKind.String => bool.TryParse(free.GetString(), out var parsed) ? parsed : (bool?)null,
          _ => null
        };

        facts.AccessibleForFree ??= value;
        if (value == true) facts.AccessibleForFreeQuote ??= AccessibleQuote(block);
      }
    }

    private static void ApplyInstance(JsonElement node, string block, CourseQualityStructuredFacts facts)
    {
      AddDate(Text(node, "startDate"), facts.Starts);
      AddDate(Text(node, "endDate"), facts.Ends);

      var scheduled = ScheduleProperties.Any(o => node.TryGetProperty(o, out _));
      facts.HasSchedule |= scheduled;

      facts.Duration ??= Text(node, "duration") ?? Text(node, "courseWorkload");

      if (node.TryGetProperty("courseMode", out var mode))
      {
        var modes = mode.ValueKind == JsonValueKind.Array
          ? mode.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.String).Select(o => o.GetString()!)
          : mode.ValueKind == JsonValueKind.String ? [mode.GetString()!] : [];

        if (!scheduled && modes.Any(o => SelfPacedModeRegex().IsMatch(o))) facts.SelfPaced = true;
      }

      foreach (var offer in Items(node, "offers")) ApplyOffer(offer, block, facts);
    }

    private static void AddDate(string? value, List<DateTimeOffset> dates)
    {
      if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
        && date.Year is >= MinYear and <= MaxYear)
        dates.Add(date);
    }

    private static void ApplyOffer(JsonElement offer, string block, CourseQualityStructuredFacts facts)
    {
      var label = string.Join(' ', Text(offer, "name"), Text(offer, "description"), Text(offer, "category"));
      if (CourseQualityPageCost.IsNotTheCourse(label)) return;

      var source = offer.TryGetProperty("price", out _) ? offer : Items(offer, "priceSpecification").FirstOrDefault();
      var currency = source.ValueKind == JsonValueKind.Object ? Text(source, "priceCurrency") ?? Text(offer, "priceCurrency") : Text(offer, "priceCurrency");

      var low = Amount(offer, "lowPrice");
      var high = Amount(offer, "highPrice");
      if (low.HasValue && high.HasValue && low != high)
      {
        facts.PriceIsRange = true;
        return;
      }

      var isFreeWord = source.ValueKind == JsonValueKind.Object && Text(source, "price")?.Trim().Equals("free", StringComparison.OrdinalIgnoreCase) == true;
      var amount = isFreeWord ? 0m : (source.ValueKind == JsonValueKind.Object ? Amount(source, "price") : null) ?? low;
      if (!amount.HasValue || amount < 0 || amount > MaxAmount) return;

      facts.Offers.Add(new CourseQualityOffer(amount, NormalizeCurrency(currency), amount == 0, PriceQuote(block, amount.Value, isFreeWord)));
    }

    private static decimal? Amount(JsonElement node, string property)
    {
      if (!node.TryGetProperty(property, out var value)) return null;

      var raw = value.ValueKind switch
      {
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.String => value.GetString(),
        _ => null
      };

      return ParseAmount(raw);
    }

    private static decimal? ParseAmount(string? raw)
    {
      return decimal.TryParse(raw?.Trim().Replace(",", string.Empty), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ? amount : null;
    }

    private static string? NormalizeCurrency(string? value)
    {
      var trimmed = value?.Trim().ToUpperInvariant();
      return trimmed is { Length: 3 } && trimmed.All(char.IsAsciiLetter) ? trimmed : null;
    }

    /// <summary>
    /// The exact fragment of the block, so that structured evidence is a quote too.
    /// </summary>
    private static string? PriceQuote(string block, decimal amount, bool isFreeWord)
    {
      foreach (Match match in PriceFragmentRegex().Matches(block))
      {
        var raw = match.Groups[1].Value.Trim('"');
        if (isFreeWord ? raw.Equals("free", StringComparison.OrdinalIgnoreCase) : ParseAmount(raw) == amount)
          return CourseQualitySafeText.Clean(match.Value, CourseQualitySafeText.MaxQuoteLength);
      }

      return null;
    }

    private static string? AccessibleQuote(string block)
    {
      var match = AccessibleFragmentRegex().Match(block);
      return match.Success ? CourseQualitySafeText.Clean(match.Value, CourseQualitySafeText.MaxQuoteLength) : null;
    }

    private static void ReadMetaFacts(CourseQualityPageSignals signals, CourseQualityStructuredFacts facts)
    {
      AddDate(signals.MetaTags.GetValueOrDefault("event:start_time"), facts.Starts);
      AddDate(signals.MetaTags.GetValueOrDefault("event:end_time"), facts.Ends);

      var tag = signals.MetaTags.ContainsKey("product:price:amount") ? "product:price:amount" : "og:price:amount";
      var raw = signals.MetaTags.GetValueOrDefault(tag);
      var amount = ParseAmount(raw);
      if (!amount.HasValue || amount < 0 || amount > MaxAmount) return;

      var currency = signals.MetaTags.GetValueOrDefault("product:price:currency") ?? signals.MetaTags.GetValueOrDefault("og:price:currency");
      var code = NormalizeCurrency(currency);
      facts.Offers.Add(new CourseQualityOffer(amount, code, amount == 0, $"{tag} {raw} {code}".TrimEnd(), true));
    }

    [GeneratedRegex(@"^\s*(?:online\s*[,-]?\s*)?(?:self[\s-]?paced|asynchronous)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, RegexTimeoutMilliseconds)]
    private static partial Regex SelfPacedModeRegex();

    [GeneratedRegex(@"""(?:price|lowPrice)""\s*:\s*(""[^""]{0,32}""|[0-9][0-9.,]{0,20})", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, RegexTimeoutMilliseconds)]
    private static partial Regex PriceFragmentRegex();

    [GeneratedRegex(@"""isAccessibleForFree""\s*:\s*""?true""?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, RegexTimeoutMilliseconds)]
    private static partial Regex AccessibleFragmentRegex();
    #endregion
  }
}
