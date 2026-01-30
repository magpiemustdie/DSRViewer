using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using DSRViewer.ImGuiHelper;

namespace DSRViewer.FileHelper.FileExplorer.TreeBuilder
{
    public class FileTreeViewer : ImGuiChild
    {
        private FileNode _selected;
        public delegate void ClickAction(FileNode node);
        public ClickAction CurrentClickHandler;
        public FileTreeViewer()
        {
            CurrentClickHandler = DefaultClickFunction;
            _childFlags = ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysAutoResize;
        }

        public FileNode DrawBndTree(FileNode node)
        {
            ImGui.BeginChild(_childName, _childSize, _childFlags);
            {
                ImGui.PushID(node.VirtualPath);

                try
                {
                    ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnDoubleClick;

                    if (!node.IsFolder && !node.IsBndArchive && !node.IsTpfArchive && !node.IsBxfArchive
                        && !node.IsNestedBndArchive && !node.IsNestedTpfArchive && !node.IsNestedBxfArchive)
                        flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

                    string label = node.IsFolder ? $"[DIR] {node.Name}" :
                                  node.IsBndArchive ? $"[BND] {node.Name}" :
                                  node.IsBxfArchive ? $"[BXF] {node.Name}" :
                                  node.IsTpfArchive ? $"[TPF] {node.Name}" :
                                  node.IsDDS ? $"[DDS][{node.DDSFormatFlag}] {node.Name}" :
                                  node.IsFlver ? $"[FLV] {node.Name}" :
                                  node.IsNestedBndArchive ? $"{node.ID} [n_BND] {node.Name}" :
                                  node.IsNestedBxfArchive ? $"{node.ID} [n_BXF] {node.Name}" :
                                  node.IsNestedTpfArchive ? $"{node.ID} [n_TPF] {node.Name}" :
                                  node.IsNestedDDS ? $"{node.ID} [n_DDS][{node.DDSFormatFlag}][{node.DDSFormat}] {node.Name}" :
                                  node.IsNestedFlver ? $"{node.ID} [n_FLV] {node.Name}" :
                                  $"{node.ID} {node.Name}";

                    if (_selected == node)
                    {
                        flags |= ImGuiTreeNodeFlags.Selected;
                    }

                    if (ImGui.TreeNodeEx(label, flags))
                    {
                        if (ImGui.IsItemClicked())
                        {
                            _selected = node;
                            CurrentClickHandler?.Invoke(node);
                        }

                        foreach (var child in node.Children)
                        {
                            DrawBndTree(child);
                        }

                        if (node.IsFolder || node.IsBndArchive || node.IsTpfArchive || node.IsBxfArchive
                            || node.IsNestedBndArchive || node.IsNestedTpfArchive || node.IsNestedBxfArchive)
                            ImGui.TreePop();
                    }
                }
                finally
                {
                    ImGui.PopID();
                }

                ImGui.EndChild();
            }

            return node;
        }

        private void DefaultClickFunction(FileNode node)
        {
            Console.WriteLine("Default click handler");
        }

        public FileNode GetSelectedFile() => _selected;

        public void SetSelectedFile(FileNode selected)
        {
            _selected = selected;
        }

        public void SetChildSize(Vector2 size)
        {
            _childSize = size;
        }

        public void RefreshView()
        {

        }
    }
}
