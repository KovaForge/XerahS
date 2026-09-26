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

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using CommunityToolkit.Mvvm.ComponentModel;
using NUnit.Framework;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;
using XerahS.UI;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;
using XerahS.UI.Views.Dialogs;

namespace XerahS.Tests.Avalonia;

[TestFixture]
public class ViewLocatorTests
{
    [AvaloniaTest]
    public void Build_Resolves_ImageEditor_ConfirmationDialogView_From_Loaded_Assembly()
    {
        var viewLocator = new ViewLocator();
        var viewModel = new ConfirmationDialogViewModel(
            onYes: () => { },
            onNo: () => { },
            onCancel: () => { });

        Control? control = viewLocator.Build(viewModel);

        Assert.That(control, Is.TypeOf<ConfirmationDialogView>());
        Assert.That(control?.DataContext, Is.SameAs(viewModel));
    }

    [AvaloniaTest]
    public void Build_Resolves_CustomUploaderEditorDialog_For_ModalContent()
    {
        var viewLocator = new ViewLocator();
        var viewModel = new CustomUploaderEditorViewModel();

        Assert.That(viewLocator.Match(viewModel), Is.True);

        Control? control = viewLocator.Build(viewModel);

        Assert.That(control, Is.TypeOf<CustomUploaderEditorDialog>());
        Assert.That(control?.DataContext, Is.SameAs(viewModel));
        Assert.That((control as TextBlock)?.Text, Is.Null.Or.Not.Contain("Not Found"));
    }

    [AvaloniaTest]
    public void Build_Resolves_ModalDialogHost_Sibling_Views()
    {
        var viewLocator = new ViewLocator();

        AssertModal(viewLocator, new WindowSelectorViewModel(), typeof(WindowSelectorDialog));
        AssertModal(viewLocator, new SimplePromptViewModel(), typeof(SimplePromptView));
        AssertModal(viewLocator, new OpenImageChoiceViewModel(), typeof(OpenImageChoiceDialog));
        AssertModal(viewLocator, new UpdateMessageBoxViewModel(), typeof(UpdateMessageBox));
        AssertModal(viewLocator, new WatchFolderEditViewModel(), typeof(WatchFolderDialog));
        AssertModal(viewLocator, new FFmpegOptionsViewModel(), typeof(FFmpegOptionsWindow));
        AssertModal(viewLocator, new AfterCaptureViewModel(), typeof(AfterCaptureWindow));
    }

    [AvaloniaTest]
    public void Match_Returns_False_For_Unresolved_ObservableObject()
    {
        var viewLocator = new ViewLocator();
        var orphan = new OrphanModalViewModel();

        Assert.That(viewLocator.Match(orphan), Is.False);

        Control? control = viewLocator.Build(orphan);
        Assert.That(control, Is.TypeOf<StackPanel>());

        var panel = (StackPanel)control!;
        var texts = panel.Children.OfType<TextBlock>().Select(tb => tb.Text).ToList();
        Assert.That(texts, Does.Contain(ViewLocator.DialogOpenFailureTitle));
        Assert.That(texts, Does.Contain(ViewLocator.DialogOpenFailureHint));

        var combined = string.Join('\n', texts);
        Assert.That(combined, Does.Not.Contain("Not Found"));
        Assert.That(combined, Does.Not.Contain("ViewModel"));
        Assert.That(combined, Does.Not.Contain("OrphanModal"));
        Assert.That(combined, Does.Not.Contain(orphan.GetType().Namespace ?? "___none___"));
        Assert.That(panel.MinHeight, Is.GreaterThanOrEqualTo(44));
        Assert.That(panel.MinWidth, Is.GreaterThanOrEqualTo(44));
    }

    [AvaloniaTest]
    public void Build_Unresolved_Does_Not_Leak_Type_Names_In_Ui()
    {
        var viewLocator = new ViewLocator();
        var orphan = new OrphanModalViewModel();

        Control? control = viewLocator.Build(orphan);
        Assert.That(control, Is.Not.Null);

        var dumped = DumpText(control!);
        Assert.That(dumped, Does.Contain(ViewLocator.DialogOpenFailureTitle));
        Assert.That(dumped, Does.Not.Contain("Not Found:"));
        Assert.That(dumped, Does.Not.Contain(orphan.GetType().FullName!));
        Assert.That(dumped, Does.Not.Contain(orphan.GetType().Name));
    }

    private static string DumpText(Control root)
    {
        var parts = new List<string>();
        void Walk(Control c)
        {
            if (c is TextBlock tb && tb.Text != null)
                parts.Add(tb.Text);
            if (c is Panel panel)
            {
                foreach (var child in panel.Children.OfType<Control>())
                    Walk(child);
            }
        }
        Walk(root);
        return string.Join('\n', parts);
    }

    private static void AssertModal(ViewLocator viewLocator, ObservableObject viewModel, Type expectedViewType)
    {
        Assert.That(viewLocator.Match(viewModel), Is.True, $"Match failed for {viewModel.GetType().Name}");

        Control? control = viewLocator.Build(viewModel);

        Assert.That(control, Is.TypeOf(expectedViewType), $"Build failed for {viewModel.GetType().Name}");
        Assert.That(control?.DataContext, Is.SameAs(viewModel));
    }

    private sealed partial class OrphanModalViewModel : ObservableObject;
}
