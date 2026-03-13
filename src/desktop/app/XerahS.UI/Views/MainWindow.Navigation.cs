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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections;
using System.Linq;
using ShareX.ImageEditor.Presentation.Theming;
using ShareX.ImageEditor.Presentation.Views;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Core.Managers;
using XerahS.UI.Helpers;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views
{
    public partial class MainWindow
    {
        private NavigationNode? _captureNavigationNode;

        private void OnMenuNavigateClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem)
            {
                return;
            }

            string? navTag = menuItem.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(navTag))
            {
                return;
            }

            NavigateTo(navTag);
        }

        private void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ContentControl? contentFrame = this.FindControl<ContentControl>("ContentFrame");
            NavigationNode? selectedItem = (sender as TreeView)?.SelectedItem as NavigationNode;

            if (contentFrame == null || selectedItem == null || selectedItem.Kind != NavigationNodeKind.Page)
            {
                return;
            }

            HandleNavigationTag(selectedItem.Tag, contentFrame);
        }

        private void OnNavigationNodeTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not Control control || control.DataContext is not NavigationNode node)
            {
                return;
            }

            if (InvokeNavigationNode(node, toggleGroups: true))
            {
                e.Handled = true;
            }
        }

        private void OnNavigationTreeKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Space)
            {
                return;
            }

            if ((sender as TreeView)?.SelectedItem is not NavigationNode node)
            {
                return;
            }

            if (InvokeNavigationNode(node, toggleGroups: true))
            {
                e.Handled = true;
            }
        }

        private bool HandleNavigationTag(string? tag, ContentControl contentFrame)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            if (tag.StartsWith("Capture_", StringComparison.Ordinal))
            {
                string workflowId = tag.Replace("Capture_", "", StringComparison.Ordinal);
                if (!string.IsNullOrEmpty(workflowId))
                {
                    WorkflowSettings? workflow = null;

                    if (Application.Current is App app && app.WorkflowManager != null)
                    {
                        workflow = app.WorkflowManager.GetWorkflowById(workflowId);
                    }

                    if (workflow == null)
                    {
                        workflow = SettingsManager.WorkflowsConfig.Hotkeys.FirstOrDefault(w => w.Id == workflowId);
                    }

                    if (workflow != null)
                    {
                        _ = ExecuteCaptureAsync(workflow.Job, workflow.Id);
                        NavigateToEditor();
                        return true;
                    }
                }

                return false;
            }

            if (tag.StartsWith("Workflow_", StringComparison.Ordinal))
            {
                string workflowId = tag.Replace("Workflow_", "", StringComparison.Ordinal);
                if (!string.IsNullOrEmpty(workflowId))
                {
                    WorkflowSettings? workflow = SettingsManager.WorkflowsConfig?.Hotkeys?.FirstOrDefault(w => w.Id == workflowId);
                    if (workflow != null)
                    {
                        _ = ExecuteCaptureAsync(workflow.Job, workflow.Id);
                        return true;
                    }
                }

                return false;
            }

            if (ToolNavigationHelper.TryHandleToolsTag(tag, this, contentFrame, ExecuteWorkflowFromNavigationAsync))
            {
                return true;
            }

            switch (tag)
            {
                case "Editor":
                    _editorView ??= new EditorView();
                    contentFrame.Content = _editorView;
                    return true;
                case "Recording":
                    contentFrame.Content = new RecordingView();
                    return true;
                case "History":
                    contentFrame.Content = new HistoryView();
                    return true;
                case "Workflows":
                    contentFrame.Content = new WorkflowsView();
                    return true;
                case "Upload_ClipboardUploadWithContentViewer":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.ClipboardUploadWithContentViewer);
                    return true;
                case "Upload_FileUpload":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.FileUpload);
                    return true;
                case "Settings":
                    contentFrame.Content = new SettingsView();
                    return true;
                case "Settings_App":
                    contentFrame.Content = new ApplicationSettingsView();
                    return true;
                case "Settings_Dest":
                    contentFrame.Content = new DestinationSettingsView();
                    return true;
                case "Debug":
                    contentFrame.Content = new DebugView();
                    return true;
                case "About":
                    contentFrame.Content = new AboutView();
                    return true;
                default:
                    return false;
            }
        }

        private void BuildNavigationNodes()
        {
            NavigationNodes.Clear();

            _captureNavigationNode = CreateNode("Capture", "Capture", HostIcons.NavigationCapture, NavigationNodeKind.Group, isExpanded: true);
            NavigationNodes.Add(_captureNavigationNode);
            NavigationNodes.Add(CreateNode("Recording", "Recording", HostIcons.NavigationRecording, NavigationNodeKind.Page));
            NavigationNodes.Add(CreateNode("Editor", "Editor", HostIcons.NavigationEditor, NavigationNodeKind.Page));
            NavigationNodes.Add(CreateNode("History", "History", HostIcons.NavigationHistory, NavigationNodeKind.Page));
            NavigationNodes.Add(CreateNode("Workflows", "Workflows", HostIcons.NavigationWorkflows, NavigationNodeKind.Page));
            NavigationNodes.Add(CreateUploadNode());
            NavigationNodes.Add(CreateToolsNode());
            NavigationNodes.Add(CreateSettingsNode());
            NavigationNodes.Add(CreateNode("Debug", "Debug", HostIcons.NavigationDebug, NavigationNodeKind.Page));
            NavigationNodes.Add(CreateNode("About", "About", HostIcons.NavigationAbout, NavigationNodeKind.Page));

            UpdateNavigationItems();
        }

        public void NavigateToEditor()
        {
            NavigateTo("Editor");
        }

        public void NavigateToSettings()
        {
            NavigateTo("Settings");
        }

        public void NavigateToHistory()
        {
            NavigateTo("History");
        }

        public void NavigateToAbout()
        {
            NavigateTo("About");
        }

        private void NavigateTo(string navTag)
        {
            bool handled = false;
            ContentControl? contentFrame = this.FindControl<ContentControl>("ContentFrame");
            TreeView? navigationTree = this.FindControl<TreeView>("NavigationTree");

            if (navigationTree != null)
            {
                NavigationNode? navNode = FindNavigationNodeByTag(NavigationNodes, navTag);
                if (navNode != null)
                {
                    navNode.ExpandPath();

                    if (navNode.Kind == NavigationNodeKind.Action)
                    {
                        if (contentFrame != null)
                        {
                            handled = HandleNavigationTag(navTag, contentFrame);
                        }
                    }
                    else if (navNode.Kind == NavigationNodeKind.Page && !ReferenceEquals(navigationTree.SelectedItem, navNode))
                    {
                        navigationTree.SelectedItem = navNode;
                        handled = true;
                    }
                    else if (navNode.Kind == NavigationNodeKind.Page && contentFrame != null)
                    {
                        handled = HandleNavigationTag(navTag, contentFrame);
                    }
                }
            }

            if (!handled && contentFrame != null)
            {
                _ = HandleNavigationTag(navTag, contentFrame);
            }

            if (!this.IsVisible)
            {
                this.Show();
            }

            if (this.WindowState == Avalonia.Controls.WindowState.Minimized)
            {
                this.WindowState = Avalonia.Controls.WindowState.Normal;
            }

            this.Activate();
            this.Focus();
        }

        private bool InvokeNavigationNode(NavigationNode node, bool toggleGroups)
        {
            TreeView? navigationTree = this.FindControl<TreeView>("NavigationTree");
            ContentControl? contentFrame = this.FindControl<ContentControl>("ContentFrame");

            if (node.Kind == NavigationNodeKind.Group)
            {
                if (toggleGroups)
                {
                    node.IsExpanded = !node.IsExpanded;
                    return true;
                }

                return false;
            }

            if (contentFrame == null)
            {
                return false;
            }

            node.ExpandPath();

            if (node.Kind == NavigationNodeKind.Action)
            {
                return HandleNavigationTag(node.Tag, contentFrame);
            }

            if (navigationTree != null && !ReferenceEquals(navigationTree.SelectedItem, node))
            {
                navigationTree.SelectedItem = node;
                return true;
            }

            return HandleNavigationTag(node.Tag, contentFrame);
        }

        private static NavigationNode? FindNavigationNodeByTag(IEnumerable? menuItems, string navTag)
        {
            if (menuItems == null)
            {
                return null;
            }

            foreach (object? item in menuItems)
            {
                if (item is not NavigationNode navItem)
                {
                    continue;
                }

                if (string.Equals(navItem.Tag, navTag, StringComparison.Ordinal))
                {
                    return navItem;
                }

                NavigationNode? child = FindNavigationNodeByTag(navItem.Children, navTag);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private void UpdateNavigationItems()
        {
            if (_captureNavigationNode == null)
            {
                return;
            }

            _captureNavigationNode.ReplaceChildren(NavigationItemsHelper.CreateCaptureNavigationNodes());
        }

        private static NavigationNode CreateNode(string text, string? tag, string? glyph, NavigationNodeKind kind, bool isExpanded = false)
        {
            return new NavigationNode(text, tag, glyph, kind)
            {
                IsExpanded = isExpanded
            };
        }

        private static NavigationNode CreateUploadNode()
        {
            NavigationNode uploadNode = CreateNode("Upload", "Upload", HostIcons.NavigationUpload, NavigationNodeKind.Group);
            uploadNode.AddChild(CreateNode("Upload File...", "Upload_FileUpload", null, NavigationNodeKind.Action));
            uploadNode.AddChild(CreateNode("Upload Content...", "Upload_ClipboardUploadWithContentViewer", null, NavigationNodeKind.Action));
            return uploadNode;
        }

        private static NavigationNode CreateToolsNode()
        {
            NavigationNode toolsNode = CreateNode("Tools", "Tools", HostIcons.NavigationTools, NavigationNodeKind.Page);
            toolsNode.AddChild(CreateNode("Color Picker...", "Tools_ColorPicker", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Pick From Screen", "Tools_ScreenColorPicker", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Ruler", "Tools_Ruler", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Index Folder...", "Tools_IndexFolder", null, NavigationNodeKind.Action));

            NavigationNode qrCodeNode = CreateNode("QR Code", null, null, NavigationNodeKind.Group);
            qrCodeNode.AddChild(CreateNode("Generator...", "Tools_QrGenerator", null, NavigationNodeKind.Action));
            qrCodeNode.AddChild(CreateNode("Scan from screen", "Tools_QrScanScreen", null, NavigationNodeKind.Action));
            qrCodeNode.AddChild(CreateNode("Scan from region", "Tools_QrScanRegion", null, NavigationNodeKind.Action));
            toolsNode.AddChild(qrCodeNode);

            toolsNode.AddChild(CreateNode("Image Combiner...", "Tools_ImageCombiner", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Image Splitter...", "Tools_ImageSplitter", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Image Thumbnailer...", "Tools_ImageThumbnailer", null, NavigationNodeKind.Action));
#if DEBUG
            toolsNode.AddChild(CreateNode("Video Editor...", "Tools_VideoEditor", null, NavigationNodeKind.Action));
#endif
            toolsNode.AddChild(CreateNode("Video Converter...", "Tools_VideoConverter", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Video Thumbnailer...", "Tools_VideoThumbnailer", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Analyze Image...", "Tools_AnalyzeImage", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Monitor Test", "Tools_MonitorTest", null, NavigationNodeKind.Action));

            return toolsNode;
        }

        private static NavigationNode CreateSettingsNode()
        {
            NavigationNode settingsNode = CreateNode("Settings", "Settings", HostIcons.NavigationSettings, NavigationNodeKind.Page, isExpanded: true);
            settingsNode.AddChild(CreateNode("Application Settings", "Settings_App", null, NavigationNodeKind.Page));
            settingsNode.AddChild(CreateNode("Destination Settings", "Settings_Dest", null, NavigationNodeKind.Page));
            return settingsNode;
        }
    }
}
