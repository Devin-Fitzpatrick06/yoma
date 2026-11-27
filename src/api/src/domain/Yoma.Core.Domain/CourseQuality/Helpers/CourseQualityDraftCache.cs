using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// A small in-process cache for the draft check, so a form that asks again on every pause in typing fetches a link and reads
  /// its page once every few minutes, not once a keystroke. Bounded: when full, expired entries go first, then everything.
  /// Per API instance only; a miss just checks again.
  /// </summary>
  public sealed class CourseQualityDraftCache
  {
    #region Class Variables
    public const int MaxEntries = 2000;

    private readonly ConcurrentDictionary<string, (object? Value, DateTimeOffset Expires)> _items = new(StringComparer.Ordinal);
    #endregion

    #region Public Members
    public static CourseQualityDraftCache Shared { get; } = new();

    public int Count => _items.Count;

    public bool TryGet<T>(string key, DateTimeOffset now, out T? value)
    {
      ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

      if (_items.TryGetValue(key, out var item) && item.Expires > now && item.Value is T or null)
      {
        value = (T?)item.Value;
        return true;
      }

      value = default;
      return false;
    }

    public void Set<T>(string key, T? value, DateTimeOffset expires, DateTimeOffset now)
    {
      ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

      if (_items.Count >= MaxEntries)
      {
        foreach (var expired in _items.Where(o => o.Value.Expires <= now).Select(o => o.Key).ToList()) _items.TryRemove(expired, out _);
        if (_items.Count >= MaxEntries) _items.Clear();
      }

      _items[key] = (value, expires);
    }

    /// <summary>
    /// A fixed-length key for long input, such as a listing's text and the page it links to.
    /// </summary>
    public static string Hash(params string?[] parts)
    {
      var text = string.Join('\u001f', parts.Select(o => o ?? string.Empty));
      return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
    #endregion
  }
}
