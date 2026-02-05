using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;

namespace DSRViewer.FileHelper.FlverEditor.Tools.FlverTexFinder
{
    // Список текстур в материале
    public class MTDTexTypeList : BaseFinderList
    {
        public void SetTextures(List<string> textures)
        {
            Clear();
            _items = new List<string>(textures);

            for (int i = 0; i < _items.Count; i++)
            {
                _ids.Add(i);
            }
        }

        public override void Render()
        {
            ImGui.BeginChild("MTDTexTypeList", _size, _childFlags);

            for (int i = 0; i < _items.Count; i++)
            {
                var displayText = $"[{i}] {_items[i]}";

                if (ImGui.Selectable(displayText, _selectedIndex == i))
                {
                    _selectedIndex = i;
                    _selectedItem = _items[i];
                    InvokeItemSelected(_selectedIndex, _selectedItem);
                }
            }

            ImGui.EndChild();
        }
    }
}
