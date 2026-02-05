using System;
using System.Numerics;
using DSRViewer.FileHelper.FileExplorer.DDSHelper;
using DSRViewer.FileHelper.FileExplorer.Tools;
using DSRViewer.FileHelper.FileExplorer.TreeBuilder;
using DSRViewer.FileHelper.FlverEditor.Render;
using DSRViewer.FileHelper.MTDEditor.Render;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using Veldrid;

namespace DSRViewer.FileHelper.FileExplorer.Render
{
    public class TreeChild : ImGuiChild
    {
        GraphicsDevice _gd;
        ImGuiController _cl;

        private TreeTabsTools _treeTabsTools = new();
        private TreeTabsTexTools _treeTabsTexTools;
        private FileTreeNodeBuilder _builder = new();
        private FileTreeViewer _treeViewer = new();
        private FileNode _root = new();
        private FileNode _selected = new();
        private DDSTextureViewChild _ddsTexViewChild;
        private FMW _flverEditor;
        private Extractor _extractor;
        private Injector _injector;
        private Config _config;
        private List<MTDShortDetails> _mtdList;

        public string RootFilePath { get; private set; } = string.Empty;

        public TreeChild(string childName, string rootFilePath, bool showChild, Config config, List<MTDShortDetails> mtdList, GraphicsDevice gd, ImGuiController cl)
        {
            _gd = gd;
            _cl = cl;

            _config = config;
            _mtdList = mtdList;
            _childName = childName;
            _showChild = showChild;
            _flverEditor = new FMW($"{childName} - FlverEditor", false, _config, _mtdList);
            _ddsTexViewChild = new DDSTextureViewChild($"{childName} - DDSViewer", false);
            _extractor = new Extractor(_config);
            _injector = new Injector(OnInjectionComplete);
            _treeTabsTexTools = new TreeTabsTexTools(OnInjectionComplete);
            _treeViewer.CurrentClickHandler = HandleFileNodeClick;
            SetRoot(rootFilePath);
        }

        public override void Render()
        {
            if (!ImGui.BeginTabItem(_childName, ref _showChild)) return;

            ImGui.BeginChild(_childName, _childSize, _childFlags);
            {
                if (!string.IsNullOrEmpty(RootFilePath))
                {
                    _extractor.Render(_selected);
                    _injector.Render(_root, _selected);
                    if (ImGui.CollapsingHeader("Tools"))
                    {
                        if (ImGui.Button("Get flver list"))
                        {
                            List<FileNode> newList = _treeTabsTools.NodeFlverFinder(_selected);
                            _flverEditor.SetNewItemList(newList);
                            _flverEditor.ShowWindow(true);
                        }
                        ImGui.Separator();
                        _treeTabsTools.GetTexturesDoubles(_selected);
                        _treeTabsTools.GetTexturesFormatErrors(_selected);
                        ImGui.Separator();
                        _treeTabsTexTools.ButtonAddTexture(_selected);
                        _treeTabsTexTools.ButtonRemoveTexture(_selected);
                        _treeTabsTexTools.ButtonRenameTexture(_selected);
                        _treeTabsTexTools.ButtonReFlagTexture(_selected);
                        ImGui.Spacing();
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
                _ddsTexViewChild.Render(_gd, _cl, _selected);

            if (_flverEditor.IsShowWindow())
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
                var newNode = _builder.BuildTree(archivePath.Split("|")[0]);

                if (newNode != null)
                {
                    _root = newNode;
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