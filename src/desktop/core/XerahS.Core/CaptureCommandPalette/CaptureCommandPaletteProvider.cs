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

using XerahS.Common;
using XerahS.Core.Hotkeys;

namespace XerahS.Core.CaptureCommandPalette;

public static class CaptureCommandPaletteProvider
{
    public static IReadOnlyList<CaptureCommandPaletteItem> CreateItems(IEnumerable<WorkflowSettings>? workflows)
    {
        if (workflows == null)
        {
            return Array.Empty<CaptureCommandPaletteItem>();
        }

        List<CaptureCommandPaletteItem> items = new();

        foreach (WorkflowSettings workflow in workflows)
        {
            if (!IsPaletteWorkflow(workflow))
            {
                continue;
            }

            string label = string.IsNullOrWhiteSpace(workflow.Name)
                ? EnumExtensions.GetDescription(workflow.Job)
                : workflow.Name!.Trim();
            string jobDescription = EnumExtensions.GetDescription(workflow.Job);
            string description = string.Equals(label, jobDescription, StringComparison.Ordinal)
                ? FormatCategory(workflow.Job)
                : jobDescription;

            items.Add(new CaptureCommandPaletteItem(
                workflow.Id,
                label,
                description,
                workflow.HotkeyInfo.GetDisplayString(),
                workflow.Job,
                workflow));
        }

        return items;
    }

    public static IReadOnlyList<CaptureCommandPaletteItem> FilterAndRank(
        IEnumerable<CaptureCommandPaletteItem> items,
        string? query)
    {
        string normalizedQuery = query?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(normalizedQuery))
        {
            return items.ToList();
        }

        return items
            .Select((item, index) => new
            {
                Item = item,
                Index = index,
                Score = Math.Max(
                    CaptureCommandPaletteFuzzyMatcher.Score(normalizedQuery, item.Label),
                    CaptureCommandPaletteFuzzyMatcher.Score(normalizedQuery, item.Description))
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Index)
            .Select(result => result.Item)
            .ToList();
    }

    public static bool IsPaletteWorkflow(WorkflowSettings? workflow)
    {
        if (workflow?.Enabled != true || workflow.Job == WorkflowType.None)
        {
            return false;
        }

        string category = workflow.Job.GetHotkeyCategory();
        return category == EnumExtensions.WorkflowType_Category_ScreenCapture ||
            category == EnumExtensions.WorkflowType_Category_ScreenRecord;
    }

    private static string FormatCategory(WorkflowType workflowType)
    {
        string category = workflowType.GetHotkeyCategory();
        return category switch
        {
            EnumExtensions.WorkflowType_Category_ScreenCapture => "Screen capture",
            EnumExtensions.WorkflowType_Category_ScreenRecord => "Screen recording",
            _ => "Capture workflow"
        };
    }
}
