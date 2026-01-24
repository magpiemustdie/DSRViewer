using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using SoulsFormats;

namespace DSRViewer.FileHelper.FlverEditor.Render
{
    public class FlverFileList : ClickableList
    {
        List<FileNode> _fileNodes = [];

        // Добавляем событие для уведомления о выборе FLVER-файла
        public event Action<FileNode> OnFlverSelected;

        public override void Render()
        {
            for (int i = 0; i < _fileNodes.Count; i++)
            {
                if (ImGui.Selectable(_fileNodes[i].Name, this.SelectedItem == i))
                {
                    this.SelectedItem = i;
                    this.SelectedItemName = _fileNodes[i].Name;
                    CurrentClickHandlerNode?.Invoke(_fileNodes[i], i);

                    // Вызываем событие при выборе FLVER-файла
                    OnFlverSelected?.Invoke(_fileNodes[i]);
                }
            }
        }

        public virtual void AddItemToList(FileNode fileNode)
        {
            if (fileNode == null) return;

            if (_fileNodes.Any(node => node.VirtualPath == fileNode.VirtualPath))
            {
                return;
            }

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

        public int GetSelectedIndex() => SelectedItem;
        public string GetSelectedName() => SelectedItemName;

        public int GetItemCount() => _fileNodes.Count;
    }
}