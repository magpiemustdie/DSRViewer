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

        public TreeChild(string childName)
        {
            _childName = childName;
            _flverEditor.SetWindowName($"{childName} - FE");
            _ddsTexViewChild.SetChildName($"{childName} - DDSViewer");
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

                    if (_flverEditor.IsWindowOpen())
                        _flverEditor.Render();
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
            Console.WriteLine($"Click: {item}");
            _selected = item;

            if (_selected.IsFlver || _selected.IsNestedFlver)
            {
                _flverEditor.ShowWindow(true);
                _flverEditor.SetNewItem(_selected);
            }
        }
        public FileNode GetSelected() => _selected;
    }
}
