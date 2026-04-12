using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using DSRViewer.UI.Base;
using DSRViewer.FileProcess;

namespace DSRViewer.Editors.Explorer.TreeBuilder
{
    /// <summary>Отображает дерево FileNode в ImGui с поддержкой ленивой загрузки и обработки кликов.</summary>
    public class FileTreeViewer : ImGuiChild
    {
        private FileNode _selected;
        public delegate void ClickAction(FileNode node);
        public ClickAction CurrentClickHandler;
        public FileTreeViewer()
        {
            CurrentClickHandler = DefaultClickFunction;
        }

        /// <summary>Рекурсивно отрисовывает дерево узлов FileNode.</summary>
        public FileNode DrawBndTree(FileNode node)
        {
            DrawNode(node);
            return node;
        }

        /// <summary>Рекурсивно отрисовывает один узел и его дочерние узлы.</summary>
        private void DrawNode(FileNode node)
        {
            ImGui.PushID(node.VirtualPath);

            try
            {
                ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnDoubleClick;

                bool isLeaf = !node.IsFolder && !node.IsBndArchive && !node.IsTpfArchive && !node.IsBxfArchive
                    && !node.IsNestedBndArchive && !node.IsNestedTpfArchive && !node.IsNestedBxfArchive
                    && !node.IsNestedFlver;

                if (isLeaf)
                    flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

                string label = node.IsFolder              ? $"{node.ID}: [DIR] {node.Name}" :
                               node.IsBndArchive          ? $"{node.ID}: [BND] {node.Name}" :
                               node.IsBxfArchive          ? $"{node.ID}: [BXF] {node.Name}" :
                               node.IsTpfArchive          ? $"{node.ID}: [TPF] {node.Name}" :
                               node.IsDDS                 ? $"{node.ID}: [DDS][{node.DDSFormatFlag}][{node.DDSFormat}] {node.Name}" :
                               node.IsFlver               ? $"{node.ID} [FLV]: {node.Name}" :
                               node.IsNestedBndArchive    ? $"{node.ID}: [n_BND] {node.Name}" :
                               node.IsNestedBxfArchive    ? $"{node.ID}: [n_BXF] {node.Name}" :
                               node.IsNestedTpfArchive    ? $"{node.ID}: [n_TPF] {node.Name}" :
                               node.IsNestedDDS           ? $"{node.ID}: [n_DDS][{node.DDSFormatFlag}][{node.DDSFormat}] {node.Name}" :
                               node.IsNestedFlver         ? $"{node.ID}: [n_FLV] {node.Name}" :
                               $"{node.ID} {node.Name}";

                if (_selected == node)
                    flags |= ImGuiTreeNodeFlags.Selected;

                bool opened = ImGui.TreeNodeEx(label, flags);

                if (ImGui.IsItemClicked())
                {
                    _selected = node;
                    CurrentClickHandler?.Invoke(node);
                }

                if (opened)
                {
                    // Ленивая загрузка: загружаем содержимое при раскрытии узла
                    node.EnsureLoaded();

                    foreach (var child in node.Children)
                        DrawNode(child);

                    if (!isLeaf)
                        ImGui.TreePop();
                }
            }
            finally
            {
                ImGui.PopID();
            }
        }

        private void DefaultClickFunction(FileNode node)
        {
            Console.WriteLine("Default click handler");
        }

        /// <summary>Возвращает текущий выбранный узел.</summary>
        public FileNode GetSelectedFile() => _selected;

        /// <summary>Устанавливает выбранный узел.</summary>
        public void SetSelectedFile(FileNode selected)
        {
            _selected = selected;
        }

        /// <summary>Устанавливает размер дочернего окна дерева.</summary>
        public void SetChildSize(Vector2 size)
        {
            _childSize = size;
        }
    }
}
