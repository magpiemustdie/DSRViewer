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
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using Veldrid;

namespace DSRViewer.FileHelper.FileExplorer.Render
{
    public partial class ViewExplorerWindow : ImGuiWindow
    {
        public ViewExplorerWindow()
        {
            _fileTreeViewer.CurrentClickHandler = ClickFunction;
        }

        FileBrowser _fileViewer = new();
        string _gameFolder = string.Empty;

        FileTreeNodeFastBuilder _fileTreeBuilder = new();
        //bool _buildTree = false;

        FileTreeViewer _fileTreeViewer = new();

        FileNode _root = new();
        FileNode _selected = new();

        List<TreeChild> _treeChilds = [];

        public override void Render(GraphicsDevice _gd, ImGuiController _controller)
        {
            if (_showWindow)
            {
                ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
                {
                    SetRootButton();
                    OpenSelectedButton();
                    CloseTreeWindowsButton();
                    ViewTreesBrowser(_gd, _controller);
                }
                ImGui.End();
            }
        }

        private void SetRootButton()
        {
            if (ImGui.Button("Select game folder"))
            {
                _gameFolder = _fileViewer.SetFolderPath();
                _root = _fileTreeBuilder.BuildTree(_gameFolder);
                _fileTreeViewer.SetChildName(_windowName + "_treeViewer");
                _fileTreeViewer.ShowChild(true);
            }
        }

        private void OpenSelectedButton()
        {
            if (ImGui.Button("Open selected"))
            {
                if (!string.IsNullOrEmpty(_selected.VirtualPath))
                {
                    var newTree = new TreeChild();
                    newTree.SetChildName($"{_windowName} - {Path.GetFileName(_selected.VirtualPath)}");
                    newTree.SetRoot(_selected.VirtualPath);
                    newTree.ShowChild(true);
                    _treeChilds.Add(newTree);
                }
                else
                {
                    Console.WriteLine("No file selected!");
                }
            }
        }

        private void CloseTreeWindowsButton()
        {
            if (ImGui.Button("Close all Tree"))
            {
                _treeChilds.Clear();
                Console.WriteLine("All Tree closed");
            }

            ImGui.SameLine();
            ImGui.Text($"Open windows: {_treeChilds.Count}");
        }

        private void ViewTreesBrowser(GraphicsDevice _gd, ImGuiController _controller)
        {
            ImGui.SetNextWindowSizeConstraints(new Vector2(0, 0), new Vector2(400, 900));
            ImGui.BeginChild("TreeBrowser", _childSize, _childFlags);
            {
                if (_gameFolder != string.Empty)
                {
                    _fileTreeViewer.DrawBndTree(_root);
                }
                else
                {
                    ImGui.Text("Set folder/file");
                }
            }
            ImGui.EndChild();

            ImGui.SameLine();

            ViewTreesTabs(_gd, _controller);
        }

        private void ViewTreesTabs(GraphicsDevice _gd, ImGuiController _controller)
        {
            if (_treeChilds.Count > 0)
            {
                ImGui.BeginChild("trees child", _childSize, _childFlags);
                {
                    if (ImGui.BeginTabBar($"tree tab"))
                    {
                        foreach (var child in _treeChilds)
                        {
                            child.Render(_gd, _controller);
                        }
                        ImGui.EndTabBar();
                    }
                }
                ImGui.EndChild();

                // Удаляем закрытые вкладки
                for (int i = _treeChilds.Count - 1; i >= 0; i--)
                {
                    if (_treeChilds[i].IsShowChild() == false)
                    {
                        _treeChilds.RemoveAt(i);
                    }
                }

                ImGui.SameLine();
            }
        }

        private void ClickFunction(FileNode item)
        {
            Console.WriteLine($"Test click: {item}");
            _selected = item;
        }

    }
}