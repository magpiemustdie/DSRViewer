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
            // Инициализируем инжектор с колбэком для обновления узла
            _injector = new Injector();
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
                    // Рендер экстрактора
                    _extractor.Render(_selected);

                    // Рендер инжектора
                    _injector.Render(gd, controller);

                    // Остальной код
                    _treeTabsTools.GetTexturesDoubles(_selected);
                    _treeTabsTools.GetTexturesFormatErrors(_selected);

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

        private void UpdateTreeNode(FileNode oldNode, FileNode newNode)
        {
            if (oldNode == null) return;

            try
            {
                // Если newNode равен null, значит нужно обновить архив и его содержимое
                if (newNode == null)
                {
                    UpdateArchiveContents(oldNode);
                    return;
                }

                // Обновляем свойства узла
                UpdateNodeProperties(oldNode, newNode);

                // Обновляем отображение
                _treeViewer.RefreshView();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating tree node: {ex.Message}");
                // В случае ошибки обновляем весь архив
                UpdateArchiveContents(oldNode);
            }
        }

        private void UpdateNodeProperties(FileNode oldNode, FileNode newNode)
        {
            // Копируем свойства из нового узла в старый
            oldNode.Size = newNode.Size;

            // Обновляем другие свойства, если они есть
            // Например, хэши, флаги и т.д.

            Console.WriteLine($"Updated node: {oldNode.Name}, Size: {oldNode.Size}");
        }

        private void UpdateArchiveContents(FileNode node)
        {
            // Находим архив в цепочке родителей
            FileNode archiveNode = FindArchiveNode(node);

            if (archiveNode != null)
            {
                // Перестраиваем поддерево архива
                FileNode newArchiveSubtree = _builder.BuildTree(archiveNode.VirtualPath);

                if (newArchiveSubtree != null && newArchiveSubtree.Children.Count > 0)
                {
                    // Обновляем детей архивного узла
                    UpdateNodeChildren(archiveNode, newArchiveSubtree);

                    // Если это был сам архив, обновляем его свойства
                    if (archiveNode == node)
                    {
                        UpdateNodeProperties(archiveNode, newArchiveSubtree);
                    }
                }
            }
        }

        private FileNode FindArchiveNode(FileNode node)
        {
            FileNode current = node;
            while (current != null)
            {
                if (IsArchiveFile(current.VirtualPath))
                {
                    return current;
                }
                //current = current.Parent;
            }
            return node; // Если архив не найден, возвращаем исходный узел
        }

        private bool IsArchiveFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            var archiveExtensions = new[] { ".dcx", ".bnd", ".bhd", ".tpf", ".bdt" };
            var ext = Path.GetExtension(path).ToLower();

            return archiveExtensions.Contains(ext);
        }

        private void UpdateNodeChildren(FileNode parentNode, FileNode newSubtree)
        {
            if (parentNode == null || newSubtree == null) return;

            try
            {
                // Обновляем детей родительского узла
                parentNode.Children.Clear();

                foreach (var child in newSubtree.Children)
                {
                    //child.Parent = parentNode;
                    parentNode.Children.Add(child);
                }

                Console.WriteLine($"Updated children for: {parentNode.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating node children: {ex.Message}");
            }
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