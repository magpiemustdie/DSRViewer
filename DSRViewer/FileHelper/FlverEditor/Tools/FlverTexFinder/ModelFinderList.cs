using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.FileHelper.FlverEditor.Render;
using DSRViewer.ImGuiHelper;
using ImGuiNET;

namespace DSRViewer.FileHelper.FlverEditor.Tools.FlverTexFinder
{
    // Список моделей
    public class ModelFinderList : BaseFinderList
    {
        private List<FileNode> _modelFiles = [];

        public void AddModel(FileNode modelFile)
        {
            _modelFiles.Add(modelFile);
            _items.Add(modelFile.ShortName);
            _ids.Add(_modelFiles.Count - 1);
        }

        public override void Clear()
        {
            base.Clear();
            _modelFiles.Clear();
        }

        public override void Render()
        {
            ImGui.BeginChild("ModelFinderList", _size, _childFlags);

            for (int i = 0; i < _modelFiles.Count; i++)
            {
                var displayText = $"[{i}] {_modelFiles[i].ShortName}";

                if (ImGui.Selectable(displayText, _selectedIndex == i))
                {
                    _selectedIndex = i;
                    _selectedItem = _modelFiles[i].ShortName;
                    InvokeItemSelected(_selectedIndex, _selectedItem);
                }
            }

            ImGui.EndChild();
        }

        public FileNode GetSelectedModel()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _modelFiles.Count)
                return _modelFiles[_selectedIndex];
            return null;
        }
    }
}
