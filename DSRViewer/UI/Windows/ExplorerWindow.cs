using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using DSRViewer.Core;
using DSRViewer.FileProcess;
using DSRViewer.Editors.Explorer;
using DSRViewer.Editors.Explorer.TreeBuilder;
using DSRViewer.Editors.MTDEditor;
using DSRViewer.UI.Base;
using ImGuiNET;
using Veldrid;

namespace DSRViewer.UI.Windows
{
    /// <summary>Окно проводника файлов игры с деревом архивов и вкладками.</summary>
    public partial class ExplorerWindow : ImGuiWindow
    {
        private readonly Config _config;
        private FileNode _rootNode;
        private FileNode _selectedNode;
        private readonly FileTreeNodeFastBuilder _fileTreeBuilder = new();
        private readonly FileTreeViewer _fileTreeViewer = new();
        private readonly List<TreeChild> _openTreeTabs = [];
        private readonly MTDWindow _mtdWindow;

        private Vector2 _treeTabsSize = new(-1, -1);

        GraphicsDevice _gd;
        ImGuiController _cl;

        public ExplorerWindow(string windowName, bool isVisible, GraphicsDevice gd, ImGuiController cl)
        {
            _windowName = windowName;
            _showWindow = isVisible;
            _windowFlags = ImGuiWindowFlags.None;
            _minSize = new(300, 300);
            _maxSize = new Vector2(1500, 950);
            _config = new Config(_windowName + " - Config");
            _mtdWindow = new(_windowName + " - MTDEditor", false, _config);
            _fileTreeViewer.CurrentClickHandler = HandleFileNodeClick;
            _windowFlags |= ImGuiWindowFlags.MenuBar;

            _gd = gd;
            _cl = cl;

            LoadGameFolderFromConfig();
        }

        public override void Render()
        {
            if (!_showWindow) return;

            ApplySizeConstraints();
            ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
            RenderMenuBar();
            RenderFileBrowser();
            RenderMTDWindow();
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
                        LoadGameFolder();
                }
                if (ImGui.MenuItem("Set extract folder"))
                    _config.SelectExtractFolder();
                if (ImGui.MenuItem("Set MTD folder"))
                {
                    if (_config.SelectMtdFolder())
                        _mtdWindow.SetMTDPath(_config);
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Show mtd window"))
            {
                if (ImGui.MenuItem("Show MTD window"))
                    _mtdWindow.ShowWindow(true);
                ImGui.EndMenu();
            }

            // Статус папок и режим загрузки прямо в menubar
            ImGui.Separator();

            bool lazy = _config.LazyLoading;
            if (ImGui.Checkbox("Lazy##lazy", ref lazy))
                _config.SetLazyLoading(lazy);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(lazy ? "Lazy loading ON: opens instantly, loads on expand" : "Lazy loading OFF: loads everything upfront");

            ImGui.Separator();
            RenderFolderStatus("Game", _config.GameFolder);
            RenderFolderStatus("Extract", _config.ExtractFolder);
            RenderFolderStatus("MTD", _config.MtdFolder);
            ImGui.Separator();

            if (!string.IsNullOrEmpty(_selectedNode?.VirtualPath))
            {
                if (ImGui.MenuItem("Open selected"))
                    OpenFileInNewTab(_selectedNode.VirtualPath);
            }

            if (_openTreeTabs.Count > 0)
            {
                if (ImGui.MenuItem("Close all tabs"))
                {
                    foreach (var tab in _openTreeTabs)
                        tab.Dispose();
                    _openTreeTabs.Clear();
                }
                ImGui.Text($"Tabs: {_openTreeTabs.Count}");
            }

            ImGui.EndMenuBar();
        }

        private void RenderMTDWindow()
        {
            _mtdWindow.Render();
        }

        private void RenderFileBrowser()
        {
            if (!ImGui.BeginTable("##explorer_layout", 2,
                ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingFixedFit))
                return;

            ImGui.TableSetupColumn("Browser", ImGuiTableColumnFlags.WidthFixed, 280f);
            ImGui.TableSetupColumn("Tabs",    ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();

            // Левый список — дерево файлов игры
            ImGui.BeginChild("TreeBrowser", new Vector2(-1, -1), ImGuiChildFlags.None);
            if (string.IsNullOrEmpty(_config.GameFolder))
                ImGui.Text("No game folder set");
            else if (_rootNode != null)
                _fileTreeViewer.DrawBndTree(_rootNode);
            else
                ImGui.TextDisabled("Loading...");
            ImGui.EndChild();

            ImGui.TableNextColumn();

            // Правая часть — вкладки TreeChild
            RenderTreeTabs();

            ImGui.EndTable();
        }

        private void RenderTreeTabs()
        {
            if (_openTreeTabs.Count == 0) return;

            ImGui.BeginChild("TreeTabsContainer", _treeTabsSize, ImGuiChildFlags.None);
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
                    _openTreeTabs[i].Dispose();
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
            if (string.IsNullOrEmpty(_config.GameFolder)) return;
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
            // Если вкладка с таким путём уже открыта — фокусируемся на ней
            var existing = _openTreeTabs.FirstOrDefault(t => t.RootFilePath == filePath);
            if (existing != null)
            {
                existing.RequestFocus();
                return;
            }

            string fileName = Path.GetFileName(filePath);
            var newTab = new TreeChild($"{_windowName} - {fileName}", filePath, true, _config, _mtdWindow.GetMTDList(), _gd, _cl);
            _openTreeTabs.Add(newTab);
        }

        private void HandleFileNodeClick(FileNode clickedNode)
        {
            _selectedNode = clickedNode;
        }

        /// <summary>Возвращает список открытых вкладок с деревьями файлов.</summary>
        public List<TreeChild> GetTreeChildList() => _openTreeTabs;

        /// <summary>Отображает статус папки в menubar: зелёная точка если задана, красная если нет.</summary>
        private static void RenderFolderStatus(string label, string path)
        {
            bool set = !string.IsNullOrEmpty(path);
            var color = set
                ? new System.Numerics.Vector4(0.3f, 0.9f, 0.3f, 1f)
                : new System.Numerics.Vector4(0.9f, 0.3f, 0.3f, 1f);
            ImGui.TextColored(color, set ? $"● {label}" : $"○ {label}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(set ? path : $"{label} folder not set");
        }
    }
}