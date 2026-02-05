using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using DSRViewer.FileHelper.FileExplorer.TreeBuilder;
using DSRViewer.FileHelper.MTDEditor.Render;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using Veldrid;

namespace DSRViewer.FileHelper.FileExplorer.Render
{
    public partial class ExplorerWindow : ImGuiWindow
    {
        private readonly Config _config;
        private FileNode _rootNode;
        private FileNode _selectedNode;
        private readonly FileTreeNodeFastBuilder _fileTreeBuilder = new();
        private readonly FileTreeViewer _fileTreeViewer = new();
        private readonly List<TreeChild> _openTreeTabs = new();
        private readonly MTDWindow _mtdWindow;

        // Window sizing
        private Vector2 _controlPanelSize = new(300, 60);
        private Vector2 _treeBrowserSize = new();
        private Vector2 _treeTabsSize = new();


        GraphicsDevice _gd;
        ImGuiController _cl;

        public ExplorerWindow(string windowName, bool isVisible, GraphicsDevice gd, ImGuiController cl)
        {
            _windowName = windowName;
            _showWindow = isVisible;
            _config = new Config(_windowName + "Config");
            _mtdWindow = new(_windowName + "MTDEditor", false);
            _mtdWindow.SetMTDPath(_config);
            _fileTreeViewer.CurrentClickHandler = HandleFileNodeClick;
            _windowFlags |= ImGuiWindowFlags.MenuBar;

            _gd = gd;
            _cl = cl;

            LoadGameFolderFromConfig();
        }

        public override void Render()
        {
            if (!_showWindow) return;

            ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
            {
                RenderMenuBar();
                RenderControlPanel();
                RenderMTDWindow();
                RenderFileBrowser();
            }
            ImGui.End();
        }

        private void RenderMenuBar()
        {
            if (!ImGui.BeginMenuBar()) return;

            if (ImGui.BeginMenu("Set config"))
            {
                if (ImGui.MenuItem("Set game folder"))
                {
                    if (_config.SelectGameFolder())
                    {
                        LoadGameFolder();
                    }
                }

                if (ImGui.MenuItem("Set extract folder"))
                {
                    _config.SelectExtractFolder();
                }

                if (ImGui.MenuItem("Set MTD folder"))
                {
                    _config.SelectMtdFolder();
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Show mtd window"))
            {
                if (ImGui.MenuItem("Show MTD window"))
                {
                    _mtdWindow.ShowWindow(true);
                }

                ImGui.EndMenu();
            }

            ImGui.EndMenuBar();
        }

        private void RenderMTDWindow()
        {
            _mtdWindow.Render();
        }

        private void RenderControlPanel()
        {
            ImGui.BeginChild($"{_windowName} - Control", _controlPanelSize, _childFlags);
            {
                RenderOpenSelectedButton();
                RenderTreeTabsControls();
            }
            ImGui.EndChild();
        }

        private void RenderOpenSelectedButton()
        {
            if (ImGui.Button("Open selected") && !string.IsNullOrEmpty(_selectedNode.VirtualPath))
            {
                OpenFileInNewTab(_selectedNode.VirtualPath);
            }
        }

        private void RenderTreeTabsControls()
        {
            if (ImGui.Button("Close all tabs"))
            {
                _openTreeTabs.Clear();
            }

            ImGui.SameLine();
            ImGui.Text($"Open tabs: {_openTreeTabs.Count}");
        }

        private void RenderFileBrowser()
        {
            ImGui.BeginChild("TreeBrowser", _treeBrowserSize, _childFlags);
            {
                if (string.IsNullOrEmpty(_config.GameFolder))
                {
                    ImGui.Text("No game folder set");
                }
                else
                {
                    _fileTreeViewer.DrawBndTree(_rootNode);
                }
            }
            ImGui.EndChild();

            ImGui.SameLine();
            RenderTreeTabs();
        }

        private void RenderTreeTabs()
        {
            if (_openTreeTabs.Count == 0) return;

            ImGui.BeginChild("TreeTabsContainer", _treeTabsSize, _childFlags);
            {
                if (ImGui.BeginTabBar("TreeTabs"))
                {
                    RenderEachTab();
                    ImGui.EndTabBar();
                }
            }
            ImGui.EndChild();

            CleanupClosedTabs();
        }

        private void RenderEachTab()
        {
            foreach (var tab in _openTreeTabs)
            {
                tab.Render();
            }
        }

        private void CleanupClosedTabs()
        {
            for (int i = _openTreeTabs.Count - 1; i >= 0; i--)
            {
                if (!_openTreeTabs[i].IsShowChild())
                {
                    _openTreeTabs.RemoveAt(i);
                }
            }
        }

        private void LoadGameFolderFromConfig()
        {
            if (!string.IsNullOrEmpty(_config.GameFolder))
            {
                LoadGameFolder();
            }
        }

        private void LoadGameFolder()
        {
            try
            {
                _rootNode = _fileTreeBuilder.BuildTree(_config.GameFolder);
                _fileTreeViewer.SetChildName($"{_windowName}_treeViewer");
                _fileTreeViewer.ShowChild(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading game folder: {ex.Message}");
            }
        }

        private void OpenFileInNewTab(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            // Передаем _config в конструктор TreeChild
            var newTab = new TreeChild($"{_windowName} - {fileName}", filePath, true, _config, _mtdWindow.GetMTDList(), _gd, _cl);
            _openTreeTabs.Add(newTab);
        }

        private void HandleFileNodeClick(FileNode clickedNode)
        {
            _selectedNode = clickedNode;
        }


    }
}