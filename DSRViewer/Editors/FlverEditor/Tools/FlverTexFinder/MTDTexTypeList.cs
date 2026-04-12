using System.Collections.Generic;
using ImGuiNET;

namespace DSRViewer.Editors.FlverEditor.Tools.FlverTexFinder
{
    public class MTDTexTypeList : BaseFinderList
    {
        public void SetTextures(List<string> textures)
        {
            Clear();
            _items = new List<string>(textures);
            for (int i = 0; i < _items.Count; i++)
                _ids.Add(i);
        }

        public override void Render()
        {
            ImGui.BeginChild(_imguiId, _size, _childFlags);
            for (int i = 0; i < _items.Count; i++)
            {
                if (ImGui.Selectable($"[{i}] {_items[i]}", _selectedIndex == i))
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
