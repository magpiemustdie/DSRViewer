using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.FileHelper.MTDEditor.Render;
using DSRViewer.ImGuiHelper;
using ImGuiNET;

namespace DSRViewer.FileHelper.FlverEditor.Tools.FlverTexFinder
{
    // Список материалов
    public class MTDFinderList : BaseFinderList
    {
        private List<string> _materialTypes = [];
        private List<string> _materialPaths = [];

        public override void Render()
        {
            ImGui.BeginChild("MTDFinderList", _size, _childFlags);

            for (int i = 0; i < _items.Count; i++)
            {
                var displayText = $"[{i}] {_items[i]}";
                var isSelected = _selectedIndex == i;

                if (ImGui.Selectable(displayText, isSelected))
                {
                    _selectedIndex = i;
                    _selectedItem = _items[i];
                    InvokeItemSelected(_selectedIndex, _selectedItem);
                }

                if (_materialTypes.Count > i)
                {
                    ImGui.SameLine();
                    ImGui.Text($"{_materialTypes[i]}");
                }
            }

            ImGui.EndChild();
        }

        public void AddMaterial(string materialPath)
        {
            if (!_materialPaths.Contains(materialPath))
            {
                _materialPaths.Add(materialPath);
                _items.Add(materialPath);
                _ids.Add(_materialPaths.Count - 1);
            }
        }

        public override void Clear()
        {
            base.Clear();
            _materialPaths.Clear();
            _materialTypes.Clear();
        }

        public void UpdateMtdTypes(List<MTDShortDetails> allMtdList)
        {
            _materialTypes.Clear();

            foreach (var materialPath in _materialPaths)
            {
                var materialName = materialPath.Split("\\").Last();
                var matchingMtd = allMtdList.FirstOrDefault(mtd =>
                    mtd.Name.Equals(materialName, StringComparison.CurrentCultureIgnoreCase));

                if (matchingMtd != null)
                {
                    _materialTypes.Add(matchingMtd.MW.ToString());
                }
                else
                {
                    _materialTypes.Add("Unknown");
                }
            }
        }

        public string GetSelectedMaterial()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _materialPaths.Count)
                return _materialPaths[_selectedIndex];
            return null;
        }
    }
}
