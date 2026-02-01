using System;
using System.Numerics;
using DSRFileViewer.FilesHelper;
using DSRViewer.DDSHelper;
using DSRViewer.FileHelper.FileExplorer.TreeBuilder;
using DSRViewer.FileHelper.FlverEditor.Render;
using DSRViewer.FileHelper.Tools;
using DSRViewer.FileHelper.Tools;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using Veldrid;

namespace DSRViewer.FileHelper.FileExplorer.Render
{
    public class TreeChild : ImGuiChild
    {
        private TreeTabsTools _treeTabsTools = new();
        private FileTreeNodeBuilder _builder = new();
        private FileTreeViewer _treeViewer = new();
        private FileNode _root = new();
        private FileNode _selected = new();
        private DDSTextureViewChild _ddsTexViewChild;
        private FMW _flverEditor;
        private Extractor _extractor;
        private Injector _injector;
        private Config _config;

        public string RootFilePath { get; private set; } = string.Empty;

        public TreeChild(string childName, string rootFilePath, bool showChild, Config config) : base(childName, showChild)
        {
            _config = config;
            _childName = childName;
            _showChild = showChild;
            _flverEditor = new FMW($"{childName} - FlverEditor", false);
            _ddsTexViewChild = new DDSTextureViewChild($"{childName} - DDSViewer", false);
            _extractor = new Extractor(_config);
            _injector = new Injector(OnInjectionComplete);
            _treeViewer.CurrentClickHandler = HandleFileNodeClick;
            SetRoot(rootFilePath);
        }

        public override void Render(GraphicsDevice gd, ImGuiController controller)
        {
            if (!ImGui.BeginTabItem(_childName, ref _showChild)) return;

            ImGui.BeginChild(_childName, _childSize, _childFlags);
            {
                if (!string.IsNullOrEmpty(RootFilePath))
                {
                    _extractor.Render(_selected);
                    _injector.Render(_root, _selected);
                    _treeTabsTools.GetTexturesDoubles(_selected);
                    _treeTabsTools.GetTexturesFormatErrors(_selected);
                    if (ImGui.Button("Add texture"))
                    {
                        if (_selected.IsNestedTpfArchive)
                        {
                            FileBinders binders = new();
                            binders.SetDds(true, false, false, 1, "New texture");
                            binders.SetCommon(false, true);
                            binders.Read(_selected.VirtualPath);
                            OnInjectionComplete(_selected.VirtualPath);
                        }
                    }
                    _treeViewer.DrawBndTree(_root);
                }
                else
                {
                    ImGui.Text("No file loaded");
                }
            }
            ImGui.EndChild();

            ImGui.SameLine();

            if (_ddsTexViewChild.IsShowChild())
                _ddsTexViewChild.Render(gd, controller, _selected);

            if (_flverEditor.IsWindowOpen())
                _flverEditor.Render();

            ImGui.EndTabItem();
        }

        public void SetRoot(string rootFilePath)
        {
            RootFilePath = rootFilePath;
            _root = _builder.BuildTree(rootFilePath);
        }

        private void OnInjectionComplete(string archivePath)
        {
            try
            {
                Console.WriteLine($"Updating tree after injection for: {archivePath}");

                if (RootFilePath.Equals(archivePath, StringComparison.OrdinalIgnoreCase))
                {
                    _root = _builder.BuildTree(RootFilePath);
                }
                else
                {
                    UpdateArchiveNode(archivePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating tree after injection: {ex.Message}");
            }
        }

        private void UpdateArchiveNode(string archivePath)
        {
            var archiveNode = FindNodeByPath(_root, archivePath);

            if (archiveNode != null)
            {
                var newSubtree = _builder.BuildTree(archivePath);

                if (newSubtree != null)
                {
                    ReplaceNodeInTree(_root, archiveNode, newSubtree);
                }
            }
        }

        private FileNode FindNodeByPath(FileNode currentNode, string path)
        {
            if (currentNode.VirtualPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                return currentNode;

            foreach (var child in currentNode.Children)
            {
                var found = FindNodeByPath(child, path);
                if (found != null)
                    return found;
            }

            return null;
        }

        private bool ReplaceNodeInTree(FileNode parent, FileNode oldNode, FileNode newNode)
        {
            if (parent.Children.Contains(oldNode))
            {
                int index = parent.Children.IndexOf(oldNode);
                parent.Children[index] = newNode;
                return true;
            }

            foreach (var child in parent.Children)
            {
                if (ReplaceNodeInTree(child, oldNode, newNode))
                    return true;
            }

            return false;
        }

        private void HandleFileNodeClick(FileNode item)
        {
            _selected = item;

            bool showFlverEditor = item.IsFlver || item.IsNestedFlver;
            bool showDdsViewer = item.IsDDS || item.IsNestedDDS;

            _flverEditor.ShowWindow(showFlverEditor);
            _ddsTexViewChild.ShowChild(showDdsViewer);

            if (showFlverEditor)
            {
                _flverEditor.SetNewItem(item);
            }
        }
    }
}