using System.Text;
using Arrr.Core.Data.Digest;

namespace Arrr.Service.Internal;

internal static class DigestFormatter
{
    internal static string Format(IReadOnlyList<DigestSection> sections)
    {
        var sb = new StringBuilder();

        foreach (var section in sections)
        {
            sb.AppendLine(section.Title);

            if (section.Items.Count == 0)
            {
                sb.AppendLine("No events.");
            }
            else
            {
                foreach (var item in section.Items)
                {
                    sb.Append("• ");
                    sb.AppendLine(item.Text);
                }
            }
        }

        return sb.ToString().TrimEnd();
    }
}
