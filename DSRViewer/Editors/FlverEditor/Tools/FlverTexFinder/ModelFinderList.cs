using System.Collections.Generic;
using DSRViewer.Editors.FlverEditor;
using DSRViewer.FileProcess;
using DSRViewer.UI.Base;
using ImGuiNET;

namespace DSRViewer.Editors.FlverEditor.Tools.FlverTexFinder
{
    // Список моделей
    public class ModelFinderList : BaseFinderList
    {
        private List<string> _modelFiles = [];

        public void AddModel(string modelFile)
        {
            _modelFiles.Add(modelFile);
            _items.Add(modelFile);
            _ids.Add(_modelFiles.Count - 1);
        }

        public override void Clear()
        {
            base.Clear();
            _modelFiles.Clear();
        }

        public override void Render()
        {
            ImGui.BeginChild(_imguiId, _size, _childFlags);

            for (int i = 0; i < _modelFiles.Count; i++)
            {
                var displayText = $"[{i}] {_modelFiles[i]}";

                if (ImGui.Selectable(displayText, _selectedIndex == i))
                {
                    _selectedIndex = i;
                    _selectedItem = _modelFiles[i];
                    InvokeItemSelected(_selectedIndex, _selectedItem);
                }
            }

            ImGui.EndChild();
        }

        public string GetSelectedModel()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _modelFiles.Count)
                return _modelFiles[_selectedIndex];
            return null;
        }
    }
}
