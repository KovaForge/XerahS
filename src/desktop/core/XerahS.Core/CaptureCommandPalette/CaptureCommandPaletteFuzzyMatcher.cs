#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

namespace XerahS.Core.CaptureCommandPalette;

public static class CaptureCommandPaletteFuzzyMatcher
{
    public static double Score(string? query, string? target)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(target))
        {
            return 0;
        }

        string normalizedQuery = query.Trim();
        string normalizedTarget = target.Trim();

        if (normalizedTarget.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        int directIndex = normalizedTarget.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase);
        if (directIndex >= 0)
        {
            double prefixBonus = directIndex == 0 ? 0.12 : 0;
            double coverage = Math.Min(0.25, normalizedQuery.Length / (double)normalizedTarget.Length * 0.25);
            return Math.Min(0.95, 0.65 + prefixBonus + coverage);
        }

        int queryIndex = 0;
        int firstMatch = -1;
        int lastMatch = -1;
        int adjacentMatches = 0;

        for (int targetIndex = 0; targetIndex < normalizedTarget.Length && queryIndex < normalizedQuery.Length; targetIndex++)
        {
            if (char.ToUpperInvariant(normalizedTarget[targetIndex]) != char.ToUpperInvariant(normalizedQuery[queryIndex]))
            {
                continue;
            }

            if (firstMatch < 0)
            {
                firstMatch = targetIndex;
            }

            if (lastMatch >= 0 && targetIndex == lastMatch + 1)
            {
                adjacentMatches++;
            }

            lastMatch = targetIndex;
            queryIndex++;
        }

        if (queryIndex != normalizedQuery.Length)
        {
            return 0;
        }

        int span = lastMatch - firstMatch + 1;
        double compactness = normalizedQuery.Length / (double)Math.Max(span, normalizedQuery.Length);
        double adjacency = normalizedQuery.Length <= 1 ? 0 : adjacentMatches / (double)(normalizedQuery.Length - 1);
        double prefix = firstMatch == 0 ? 0.1 : 0;

        return Math.Min(0.6, 0.25 + compactness * 0.2 + adjacency * 0.05 + prefix);
    }
}
