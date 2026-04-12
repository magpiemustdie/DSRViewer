using DSRViewer.UI.Base;
using DSRViewer.FileProcess;
using ImGuiNET;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DSRViewer.Editors.FlverEditor
{
    public class FlverFileList : ImGuiClickableList
    {
        public FlverFileList()
        {
            _childSize = new(0, -1);
        }

        List<FileNode> _fileNodes = [];

        /// <summary>Вызывается при выборе файла в списке.</summary>
        public Action<FileNode> OnFlverSelected { get; set; }

        public override void Render()
        {
            for (int i = 0; i < _fileNodes.Count; i++)
            {
                bool selected = SelectedItem == i;
                if (ImGui.Selectable($"{_fileNodes[i].ShortVirtualPath}...{_fileNodes[i].ShortName}##{i}", selected))
                {
                    SelectedItem     = i;
                    SelectedItemName = _fileNodes[i].Name;
                    OnFlverSelected?.Invoke(_fileNodes[i]);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(_fileNodes[i].VirtualPath);
            }
        }

        public virtual void AddItemToList(FileNode fileNode)
        {
            if (fileNode == null) return;
            if (_fileNodes.Any(node => string.Equals(node.VirtualPath, fileNode.VirtualPath,
                StringComparison.OrdinalIgnoreCase)))
                return;
            _fileNodes.Add(fileNode);
        }

        public void UpdateList(List<FileNode> newList)
        {
            _fileNodes = newList;
        }

        public void ClearList()
        {
            _fileNodes.Clear();
        }

        public List<FileNode> GetFileList() => _fileNodes;
        public int GetSelectedIndex() => SelectedItem;
        public string GetSelectedName() => SelectedItemName;
        public int GetItemCount() => _fileNodes.Count;
    }
}