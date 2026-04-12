using System;
using System.Linq;
using System.Numerics;
using DSRViewer.Core;
using DSRViewer.FileProcess;
using DSRViewer.UI.Windows;
using DSRViewer.Editors.Explorer.DDSHelper;
using DSRViewer.Editors.Explorer.Tools;
using DSRViewer.Editors.Explorer.TreeBuilder;
using DSRViewer.Editors.FlverEditor;
using DSRViewer.Editors.MTDEditor;
using DSRViewer.UI.Base;
using ImGuiNET;
using Veldrid;

namespace DSRViewer.Editors.Explorer
{
    /// <summary>Вкладка проводника с деревом файлов, инструментами и редакторами.</summary>
    public class TreeChild : ImGuiChild, IDisposable
    {
        GraphicsDevice _gd;
        ImGuiController _cl;

        private TreeTabsTools _treeTabsTools = new();
        private TreeTabsTexTools _treeTabsTexTools;
        private FileTreeNodeBuilder _builder = new();
        private FileTreeNodeLazyBuilder _lazyBuilder = new();
        private FileTreeViewer _treeViewer = new();
        private FileNode _root = new();
        
        private DDSTextureViewChild _ddsTexViewChild;
        private DDSTextureViewChild _fmwTexPreview;
        private FMW _flverEditor;
        private Extractor _extractor;
        private Injector _injector;
        private Finder _finder;
        private Config _config;
        private List<MTDShortDetails> _mtdList;
        private string _rootFilePath = string.Empty;
        private FileNode _selected = new();
        private bool _requestFocus = false;

        // RootFilePath синхронизирован с _rootFilePath для внешней проверки
        public string RootFilePath => _rootFilePath;
        public FileNode Root => _root;
        public FileNode Selected => _selected;

        public Injector Injector => _injector;

        /// <summary>Запрашивает фокус на этой вкладке при следующем рендере.</summary>
        public void RequestFocus() => _requestFocus = true;


        public TreeChild(string childName, string rootFilePath, bool showChild, Config config, List<MTDShortDetails> mtdList, GraphicsDevice gd, ImGuiController cl)
        {
            _gd = gd;
            _cl = cl;

            _config = config;
            _mtdList = mtdList;
            _childName = childName;
            _showChild = showChild;
            _flverEditor = new FMW($"{childName} - FlverEditor", false, _config, _mtdList);
            _flverEditor.ExplorerSearchDelegate = texName =>
                _root.FindAll(n => n.IsDDS && n.Name.Equals(texName, StringComparison.OrdinalIgnoreCase));
            // Превью текстуры из FlverEditor — отдельный экземпляр чтобы не конфликтовать с inline панелью
            FileNode _fmwPreviewNode = null;
            _flverEditor.ShowTexturePreviewDelegate = node => { _fmwPreviewNode = node; };
            _flverEditor.OnRenderExtra = () =>
            {
                if (_fmwPreviewNode != null)
                    _fmwTexPreview.RenderPopout(_gd, _cl, _fmwPreviewNode);
            };
            _ddsTexViewChild = new DDSTextureViewChild($"{childName} - DDSViewer", false);
            _fmwTexPreview   = new DDSTextureViewChild($"{childName} - FMWPreview", false);
            _extractor = new Extractor(_config, OnInjectionComplete);
            _injector = new Injector(OnInjectionComplete);
            _finder = new();
            _treeTabsTexTools = new TreeTabsTexTools(_gd, _injector, OnInjectionComplete);
            _treeViewer.CurrentClickHandler = HandleFileNodeClick;
            SetRoot(rootFilePath);
        }

        // Фиксированная высота верхней панели инструментов
        private const float RightPanelWidth = 260f;

        public override void Render()
        {
            if (string.IsNullOrEmpty(_rootFilePath)) return;

            var tabFlags = ImGuiTabItemFlags.None;
            if (_requestFocus)
            {
                tabFlags = ImGuiTabItemFlags.SetSelected;
                _requestFocus = false;
            }

            if (ImGui.BeginTabItem(_childName, ref _showChild, tabFlags))
            {
                // ── Два столбца: дерево | панель операций + превью ───────
                if (ImGui.BeginTable("##tree_layout", 2,
                    ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableSetupColumn("Tree",  ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Tools", ImGuiTableColumnFlags.WidthFixed, RightPanelWidth);

                    ImGui.TableNextColumn();

                    // Дерево
                    ImGui.BeginChild(_childName, new Vector2(-1, ImGui.GetContentRegionAvail().Y), ImGuiChildFlags.None);
                    _treeViewer.DrawBndTree(_root);
                    ImGui.EndChild();

                    ImGui.TableNextColumn();

                    // Экстрактор / инжектор
                    _extractor.Render(_root, _selected);
                    ImGui.Spacing();

                    // Поиск
                    _finder.Render(_root);
                    ImGui.Spacing();

                    // Анализ дерева
                    if (!_config.LazyLoading)
                        _treeTabsTools.RenderAnalysisButtons(_root);
                    else if (ImGui.CollapsingHeader("Analysis"))
                        ImGui.TextDisabled("Not available in Lazy Loading mode");
                    ImGui.Spacing();

                    // Операции с узлом
                    if (_selected != null && !string.IsNullOrEmpty(_selected.VirtualPath))
                    {
                        ImGui.TextDisabled(_selected.Name);
                        ImGui.Separator();
                        _treeTabsTexTools.RenderContextMenuItems(_selected);
                        ImGui.Separator();

                        if (_selected.IsFlver || _selected.IsNestedFlver)
                        {
                            if (ImGui.SmallButton("FLVER Editor"))
                            {
                                var list = _treeTabsTools.NodeFlverFinder(_selected);
                                _flverEditor.SetNewItemList(list);
                                _flverEditor.ShowWindow(true);
                            }
                        }

                        // Отправить все FLVER из дерева в редактор
                        if (!_config.LazyLoading)
                        {
                            ImGui.Spacing();
                            if (ImGui.SmallButton("Send all FLVERs to editor"))
                            {
                                var list = _treeTabsTools.NodeFlverFinder(_root);
                                _flverEditor.SetNewItemList(list);
                                _flverEditor.ShowWindow(true);
                            }
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("Select a node");
                    }

                    // Превью текстуры под операциями
                    if (_ddsTexViewChild.IsShowChild())
                    {
                        ImGui.Spacing();
                        ImGui.Separator();
                        _ddsTexViewChild.Render(_gd, _cl, _selected);
                    }

                    ImGui.EndTable();
                }

                // ── Дочерние окна ────────────────────────────────────────
                if (_flverEditor.IsShowWindow())
                    _flverEditor.Render();

                _flverEditor.OnRenderExtra?.Invoke();

                ImGui.EndTabItem();
            }
        }

        /// <summary>Устанавливает корневой файл и строит дерево (полное или ленивое).</summary>
        public void SetRoot(string rootFilePath)
        {
            _rootFilePath = rootFilePath;
            _root = _config.LazyLoading
                ? _lazyBuilder.BuildTree(_rootFilePath)
                : _builder.BuildTree(_rootFilePath);
        }

        private void OnInjectionComplete(string archivePath)
        {
            try
            {
                Console.WriteLine($"Updating tree for: {archivePath}");

                var cleanPath = archivePath.Split("|")[0];

                if (_rootFilePath.Equals(cleanPath, StringComparison.OrdinalIgnoreCase))
                {
                    _root = _config.LazyLoading
                        ? _lazyBuilder.BuildTree(_rootFilePath)
                        : _builder.BuildTree(_rootFilePath);
                }
                else
                {
                    var archiveNode = FindNodeByPath(_root, archivePath);
                    if (archiveNode != null)
                    {
                        var updatedNode = _config.LazyLoading
                            ? _lazyBuilder.BuildTree(cleanPath)
                            : _builder.BuildTree(cleanPath);
                        var newNode = FindNodeByPath(updatedNode, archivePath);
                        if (newNode != null)
                            WriteRootUpdate(_root, newNode);
                    }
                    else
                    {
                        Console.WriteLine($"Node not found for path: {archivePath}");
                    }
                }

                // Сбрасываем выбор — узел мог быть удалён или переименован
                _selected = new FileNode();
                // Сбрасываем кэш текстуры — после инжекции нужно перезагрузить
                _ddsTexViewChild.InvalidateCache();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating tree: {ex.Message}");
            }
        }

        private void WriteRootUpdate(FileNode root, FileNode newNode)
        {
            for (int i = 0; i < root.Children.Count; i++)
            {
                if (root.Children[i].VirtualPath.Equals(newNode.VirtualPath, StringComparison.OrdinalIgnoreCase))
                {
                    root.Children[i] = newNode;
                    Console.WriteLine($"Updated node: {newNode.VirtualPath}");
                }
                else
                {
                    WriteRootUpdate(root.Children[i], newNode);
                }
            }
        }

        private FileNode FindNodeByPath(FileNode currentNode, string path) =>
            currentNode.VirtualPath.Equals(path, StringComparison.OrdinalIgnoreCase)
                ? currentNode
                : currentNode.FindFirst(n => n.VirtualPath.Equals(path, StringComparison.OrdinalIgnoreCase));

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

        public void Dispose()
        {
            _ddsTexViewChild?.Cleanup();
            _fmwTexPreview?.Cleanup();
        }
    }
}