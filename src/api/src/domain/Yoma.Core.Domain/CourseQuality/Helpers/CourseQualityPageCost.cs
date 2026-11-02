using System.Globalization;
using System.Text.RegularExpressions;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Upfront cost is never guessed. Paid needs structured price data or an explicitly labelled one-off price. Free needs
  /// structured proof with nothing on the page that could be a fee. Anything else stays Unknown.
  /// </summary>
  public static partial class CourseQualityPageCost
  {
    #region Class Variables
    private const int ContextWindow = 50;
    private const int FreeContextWindow = 30;
    private const int MaxMatchesRead = 50;
    private const decimal MaxAmount = 10_000_000m;
    private const int RegexTimeoutMilliseconds = 1000;
    private const RegexOptions SafeOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    private const string Number = @"\d+(?:[.,]\d+)*";
    private const string Codes = "USD|ZAR|EUR|GBP|NGN|KES|GHS|UGX|TZS|RWF|ZMW|INR|CAD|AUD";

    private static readonly char[] SentenceEnds = ['.', '!', '?', ';', '|'];

    private static readonly Dictionary<string, string> CurrencySymbols = new(StringComparer.Ordinal)
    {
      ["US$"] = "USD", ["A$"] = "AUD", ["AU$"] = "AUD", ["C$"] = "CAD", ["CA$"] = "CAD",
      ["R"] = "ZAR", ["€"] = "EUR", ["£"] = "GBP", ["₦"] = "NGN", ["KSh"] = "KES", ["GH₵"] = "GHS", ["₹"] = "INR"
    };
    #endregion

    #region Public Members
    /// <param name="truncated">
    /// Free is a statement about the whole page. When only part of it was kept, a fee may sit in the rest.
    /// </param>
    /// <param name="listedDomain">
    /// The page is on a configured provider's own domain, the only place a visible amount may be explained as an upsell.
    /// </param>
    public static LearnerCardCost Read(CourseQualityPageSignals? signals, string text, CourseQualityStructuredFacts facts, bool truncated = false, bool listedDomain = false)
    {
      ArgumentNullException.ThrowIfNull(text, nameof(text));
      ArgumentNullException.ThrowIfNull(facts, nameof(facts));

      var unknown = new LearnerCardCost { Type = CourseCostType.Unknown };
      if (facts.PriceIsRange) return unknown;

      var (prices, ambiguous, amounts) = ReadPrices(text);
      if (ambiguous || TrialRegex().IsMatch(text)) return unknown;

      var paid = facts.Offers.Where(o => !o.IsFree).Select(o => (Amount: o.Amount!.Value, o.Currency, o.Quote, Structured: true))
        .Concat(prices.Select(o => (o.Amount, o.Currency, o.Quote, Structured: false)))
        .ToList();

      var structuredQuote = StructuredFreeQuote(facts);
      var phraseQuote = FreePhraseQuote(text);

      if (paid.Count > 0)
      {
        if (structuredQuote != null || phraseQuote != null || facts.AccessibleForFree == true || FreeStatementRegex().IsMatch(text)) return unknown;
        if (paid.Select(o => o.Amount).Distinct().Count() > 1) return unknown;

        // a partner's structured price stands only if the visible page shows no other amount
        if (amounts.Any(o => o.Amount != paid[0].Amount)) return unknown;

        // structured data states the currency. A bare "$" in text could be several dollars, so it names none,
        // while the amount and quote still stand; a structured price with no currency has no text to back it
        var currency = paid.FirstOrDefault(o => o.Structured && o.Currency != null).Currency ?? paid.FirstOrDefault(o => o.Currency != null).Currency;
        var quote = paid.FirstOrDefault(o => !o.Structured).Quote ?? paid.First().Quote;
        if (quote == null || CourseQualitySafeText.ContainsUrl(quote) || (currency == null && paid.All(o => o.Structured))) return unknown;

        return new LearnerCardCost { Type = CourseCostType.Paid, Amount = paid[0].Amount, Currency = currency, Evidence = quote };
      }

      if (facts.AccessibleForFree == false) return unknown;

      // Free rests on structured proof over the whole page, with nothing visible that could be a fee
      if (structuredQuote == null || truncated || FreeConditionRegex().IsMatch(text)) return unknown;

      var seen = signals?.Footer == null ? text : $"{text} {signals.Footer}";
      if (MoneyWordsNearDigits(seen, listedDomain) || (!listedDomain && OtherCurrencyRegex().IsMatch(seen))) return unknown;

      var footer = signals?.Footer == null ? [] : ReadPrices(signals.Footer).Amounts;
      var explained = amounts.Concat(footer).All(o => listedDomain && o.Upsell);

      return explained ? new LearnerCardCost { Type = CourseCostType.Free, Evidence = structuredQuote } : unknown;
    }

    /// <summary>
    /// True for an offer that is about something other than the course itself.
    /// </summary>
    public static bool IsNotTheCourse(string? label)
    {
      return !string.IsNullOrWhiteSpace(label) && AddOnRegex().IsMatch(label);
    }
    #endregion

    #region Private Members
    private sealed record TextPrice(decimal Amount, string? Currency, string? Quote);

    private sealed record PageAmount(decimal? Amount, bool Upsell, int Index);

    /// <summary>
    /// Every amount a visitor can see, and the labelled one-off prices among them. A label whose price cannot be read one way
    /// only, or that sits beside another amount, a period or a tier, makes the whole cost ambiguous.
    /// </summary>
    private static (List<TextPrice> Prices, bool Ambiguous, List<PageAmount> Amounts) ReadPrices(string text)
    {
      var prices = new List<TextPrice>();
      var amounts = new List<PageAmount>();
      var labelled = new List<(Match Match, decimal Amount, string? Currency, string Context)>();
      var ambiguous = false;

      foreach (Match match in PriceRegex().Matches(text))
      {
        var before = Before(text, match.Index, ContextWindow);
        var context = $"{before} {After(text, match.Index + match.Length, ContextWindow)}";
        var parsed = TryAmount(match, out var amount, out var currency) && !RunsIntoAnotherNumber(text, match);

        if (parsed && amount == 0) continue;

        amounts.Add(new PageAmount(parsed ? amount : null, AddOnRegex().IsMatch(context) || UpsellRegex().IsMatch(context), match.Index));

        if (!LabelRegex().IsMatch(before)) continue;

        ambiguous |= !parsed;
        if (parsed) labelled.Add((match, amount, currency, context));
      }

      foreach (Match match in WordAmountRegex().Matches(text)) amounts.Add(new PageAmount(null, false, match.Index));

      foreach (var (match, amount, currency, context) in labelled)
      {
        if (AddOnRegex().IsMatch(context)) continue;

        var crowded = amounts.Any(o => o.Amount != amount && Math.Abs(o.Index - match.Index) <= ContextWindow);
        if (crowded || ConditionsRegex().IsMatch(context))
        {
          ambiguous = true;
          continue;
        }

        prices.Add(new TextPrice(amount, currency, CourseQualitySafeText.Quote(text, match.Index, match.Length)));
      }

      return (prices, ambiguous, amounts);
    }

    /// <summary>
    /// Numbers are never joined across a space: "R 1 200" and "$49 120 hours" look alike, so neither can be trusted.
    /// </summary>
    private static bool RunsIntoAnotherNumber(string text, Match match)
    {
      var end = match.Index + match.Length;
      var after = end + 1 < text.Length && char.IsWhiteSpace(text[end]) && char.IsDigit(text[end + 1]);
      var before = match.Index >= 2 && char.IsWhiteSpace(text[match.Index - 1]) && char.IsDigit(text[match.Index - 2]);

      return after || before;
    }

    /// <summary>
    /// A number beside pay, fee, cost, price, tuition or enrol wording. On a listed domain a certificate upsell explains it.
    /// </summary>
    private static bool MoneyWordsNearDigits(string text, bool listedDomain)
    {
      return MoneyWordsRegex().Matches(text).Any(o =>
      {
        var context = $"{Before(text, o.Index, ContextWindow)} {After(text, o.Index, ContextWindow)}";
        return !listedDomain || !(AddOnRegex().IsMatch(context) || UpsellRegex().IsMatch(context));
      });
    }

    private static bool TryAmount(Match match, out decimal amount, out string? currency)
    {
      var number = match.Groups["a"].Success ? match.Groups["a"].Value : match.Groups["b"].Success ? match.Groups["b"].Value : match.Groups["c"].Value;

      currency = match.Groups["symbol"].Success ? CurrencySymbols.GetValueOrDefault(match.Groups["symbol"].Value)
        : match.Groups["code"].Success ? match.Groups["code"].Value
        : match.Groups["code2"].Value;

      return TryNumber(number, out amount) && amount < MaxAmount;
    }

    /// <summary>
    /// Only forms with one reading. "1.200" could be twelve hundred or one point two, so it is refused rather than guessed.
    /// </summary>
    private static bool TryNumber(string raw, out decimal amount)
    {
      var number = raw;
      string? plain = null;

      if (PlainNumberRegex().IsMatch(number)) plain = number;
      else if (CommaThousandsRegex().IsMatch(number)) plain = number.Replace(",", string.Empty);
      else if (DotThousandsRegex().IsMatch(number)) plain = number.Replace(".", string.Empty).Replace(',', '.');
      else if (DecimalCommaRegex().IsMatch(number)) plain = number.Replace(',', '.');

      amount = 0;
      return plain != null && decimal.TryParse(plain, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
    }

    private static string? StructuredFreeQuote(CourseQualityStructuredFacts facts)
    {
      // a meta tag alone is shop-template data, often 0 by default
      var offer = facts.Offers.FirstOrDefault(o => o.IsFree && !o.FromMetaTag && o.Quote != null)?.Quote;
      return offer ?? (facts.AccessibleForFree == true ? facts.AccessibleForFreeQuote : null);
    }

    private static string? FreePhraseQuote(string text)
    {
      foreach (var match in FreePhraseRegex().Matches(text).Take(MaxMatchesRead))
      {
        var before = Before(text, match.Index, FreeContextWindow);
        var after = After(text, match.Index + match.Length, FreeContextWindow);

        if (!FreeTrapBeforeRegex().IsMatch(before) && !FreeTrapAfterRegex().IsMatch(after)) return CourseQualitySafeText.Quote(text, match.Index, match.Length);
      }

      return null;
    }

    private static string Before(string text, int index, int window)
    {
      var start = Math.Max(0, index - window);
      var slice = text[start..index];
      var end = slice.LastIndexOfAny(SentenceEnds);

      return end < 0 ? slice : slice[(end + 1)..];
    }

    private static string After(string text, int index, int window)
    {
      var slice = text[index..Math.Min(text.Length, index + window)];
      var end = slice.IndexOfAny(SentenceEnds);

      return end < 0 ? slice : slice[..end];
    }

    [GeneratedRegex(@"(?<symbol>US\$|AU\$|CA\$|A\$|C\$|GH₵|\$|€|£|₦|₹|\bKSh|\bR)\s?(?<a>" + Number + @")|\b(?<code>" + Codes + @")\s?(?<b>" + Number + @")|\b(?<c>" + Number + @")\s?(?<code2>" + Codes + @")\b",
      RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, RegexTimeoutMilliseconds)]
    private static partial Regex PriceRegex();

    [GeneratedRegex(@"^\d+(?:\.\d{1,2})?$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, RegexTimeoutMilliseconds)]
    private static partial Regex PlainNumberRegex();

    [GeneratedRegex(@"^\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, RegexTimeoutMilliseconds)]
    private static partial Regex CommaThousandsRegex();

    [GeneratedRegex(@"^\d{1,3}\.\d{3},\d{1,2}$|^\d{1,3}(?:\.\d{3}){2,}(?:,\d{1,2})?$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, RegexTimeoutMilliseconds)]
    private static partial Regex DotThousandsRegex();

    [GeneratedRegex(@"^\d+,\d{1,2}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, RegexTimeoutMilliseconds)]
    private static partial Regex DecimalCommaRegex();

    [GeneratedRegex(@"\b(?:\d[\d,.]*)\s*(?:dollars?|euros?|pounds?|rand|naira|shillings?|cedis?|rupees?)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex WordAmountRegex();

    // the label must sit directly ahead of the amount
    [GeneratedRegex(@"\b(?:price|fee|fees|cost|tuition)\s*:\s*$", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex LabelRegex();

    [GeneratedRegex(@"\bonly\s+free\b|\bfree\b[^.!?]{0,30}\b(?:members?|residents?|until|to\s+start)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex FreeConditionRegex();

    [GeneratedRegex(@"\b(?:pay|pays|payment|fees?|costs?|prices?|tuition|enrol|enroll)\b[^.!?\d]{0,20}\d|\d[^.!?\d]{0,20}\b(?:pay|pays|payment|fees?|costs?|prices?|tuition|enrol|enroll)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex MoneyWordsRegex();

    [GeneratedRegex(@"\b(?:[A-Z]{3}|TSh|Rs)\.?\s?\d|\b(?:dollars?|euros?|pounds?|rand|naira|shillings?|cedis?|rupees?)\b", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, RegexTimeoutMilliseconds)]
    private static partial Regex OtherCurrencyRegex();

    [GeneratedRegex(@"\b(?:diploma|premium)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex UpsellRegex();

    [GeneratedRegex(@"\b(?:certificate|certificates|certification|transcript|upgrade|upgrades|shipping|delivery|postage|workbook|textbook|exam)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex AddOnRegex();

    [GeneratedRegex(@"/\s*(?:mo|month|yr|year|wk|week)\b|\bp/?[mwy]\b|\bper\s+\w+|\b(?:a|an|every|each)\s+(?:month|year|week)\b|\b(?:monthly|annually|annual|yearly|weekly|terms?|semesters?|trimesters?|quarters?|members?|residents?|modules?|early[\s-]?bird|tiers?|discount|regular|subscription|membership|instal+ments?|deposit|plan|from|starting|until|only|for|to\s+start)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex ConditionsRegex();

    [GeneratedRegex(@"\bfree\s+trial\b|\b\d+[\s-]day\s+trial\b|\bsubscription\b|\bfinancial\s+aid\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex TrialRegex();

    // a looser statement cannot prove Free, but it does contradict a price
    [GeneratedRegex(@"\b(?:course|this|it)\s+is\s+(?:\w+\s+)?free\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex FreeStatementRegex();

    // only wording that says it of this course: "free course materials" and "a free course from our library" do not
    [GeneratedRegex(@"\bthis\s+is\s+a\s+free\s+course\b|\bthis\s+free\s+(?:online\s+)?course\b|\b(?:this|the)\s+course\s+is\s+free\s+of\s+charge\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex FreePhraseRegex();

    [GeneratedRegex(@"\b(?:certificate|certification|shipping|delivery|trial|upgrade|lesson|lessons|module|modules|chapter|chapters|first|preview|sample|not|never|isn'?t)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex FreeTrapBeforeRegex();

    [GeneratedRegex(@"^\s*(?:materials?|resources?|trial|shipping|delivery|preview|sample|demo)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex FreeTrapAfterRegex();
    #endregion
  }
}
