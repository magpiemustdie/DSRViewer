using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.ImGuiHelper;
using ImGuiNET;

namespace DSRViewer.FileHelper.FlverEditor.Render
{
    public class FlverFileList : ClickableList
    {
        List<FileNode> _fileNodes = [];
        
        public override void Render()
        {
            for (int i = 0; i < _fileNodes.Count; i++)
            {
                if (ImGui.Selectable(_fileNodes[i].Name, _selectedItem == i))
                {
                    _selectedItem = i;
                    _selectedItemName = _fileNodes[i].Name;
                    CurrentClickHandler?.Invoke(_fileNodes[i], i);
                }
            }
        }
        
        public virtual void AddItemToList(FileNode fileNode)
        {
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
    }
}
