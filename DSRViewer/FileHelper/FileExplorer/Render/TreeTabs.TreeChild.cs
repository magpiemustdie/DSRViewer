using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DSRViewer.DDSHelper;
using DSRViewer.FileHelper.FileExplorer.TreeBuilder;
using DSRViewer.FileHelper.FlverEditor.Render;
using DSRViewer.FileHelper.Tools;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using Veldrid;

namespace DSRViewer.FileHelper.FileExplorer.Render
{
    public class TreeChild : ImGuiChild
    {
        FileTreeNodeBuilder _builder = new();
        FileTreeViewer _treeViewer = new();
        FileNode _root = new();
        FileNode _selected = new();

        DDSTextureViewChild _ddsTexViewChild = new();
        FMW _flverEditor = new();
        Extractor _extractor = new();

        new Vector2 _minSize = new Vector2(0, 0);
        new Vector2 _maxSize = new Vector2(1000, 800);

        public TreeChild()
        {
            _treeViewer.CurrentClickHandler = ClickFunction;
        }

        string _rootFilePath = string.Empty;

        public override void Render(GraphicsDevice _gd, ImGuiController _controller)
        {
            if (ImGui.BeginTabItem(_childName, ref _showChild))
            {
                ImGui.SetNextWindowSizeConstraints(_minSize, _maxSize);
                ImGui.BeginChild(_childName, _childSize, _childFlags);
                {
                    if (_rootFilePath != string.Empty)
                    {
                        _extractor.Render(_treeViewer.GetSelectedFile());
                        _treeViewer.DrawBndTree(_root);
                    }
                    ImGui.EndChild();

                    ItemClickAction(_gd, _controller);
                }
                ImGui.EndTabItem();
            }
        }

        public void SetRoot(string rootFilePath)
        {
            _rootFilePath = rootFilePath;
            _root = _builder.BuildTree(rootFilePath);
        }
        public virtual void ClickFunction(FileNode item)
        {
            Console.WriteLine($"Test click: {item}");
            _selected = item;
        }
        public FileNode GetSelected() => _selected;

        private void ItemClickAction(GraphicsDevice _gd, ImGuiController _controller)
        {
            if (_selected.IsNestedDDS)
            {
                ImGui.SameLine();
                _ddsTexViewChild.Render(_gd, _controller, _selected);
            }

            if (_selected.IsFlver)
            {
                ImGui.SameLine();
                _flverEditor.ShowWindow(true);
                _flverEditor.Render();
            }
        }
    }
}
