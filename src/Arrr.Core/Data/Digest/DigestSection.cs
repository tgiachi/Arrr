namespace Arrr.Core.Data.Digest;

/// <summary>A single titled section within a digest notification body.</summary>
public class DigestSection
{
    /// <summary>Section heading rendered before the bullet list (e.g. "Today's Calendar").</summary>
    public string Title { get; set; } = "";

    /// <summary>Ordered list of items rendered as bullet points.</summary>
    public List<DigestItem> Items { get; set; } = [];
}
