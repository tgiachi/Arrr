namespace Arrr.Core.Data.Digest;

/// <summary>A single bullet-point entry within a <see cref="DigestSection" />.</summary>
public class DigestItem
{
    /// <summary>
    /// Display text. For timed events use "HH:mm - Summary"; for all-day use "Summary (all day)".
    /// </summary>
    public string Text { get; set; } = "";
}
