using System;
using System.Collections.Generic;
using System.Numerics;
using DSRViewer.UI.Base;
using ImGuiNET;

namespace DSRViewer.Editors.FlverEditor.Tools.FlverTexFinder
{
    public abstract class BaseFinderList : ImGuiChild
    {
        protected List<string> _items = [];
        protected List<int> _ids = [];
        protected int _selectedIndex = -1;
        protected string _selectedItem = "";
        protected Vector2 _size;

        // Уникальный суффикс чтобы несколько экземпляров одного класса не конфликтовали в ImGui
        protected readonly string _imguiId;

        public event Action<int, string> OnItemSelected;

        protected void InvokeItemSelected(int index, string item) =>
            OnItemSelected?.Invoke(index, item);

        protected BaseFinderList()
        {
            _imguiId = $"{GetType().Name}_{Guid.NewGuid():N}";
        }

        public virtual void Clear()
        {
            _items.Clear();
            _ids.Clear();
            _selectedIndex = -1;
            _selectedItem = "";
        }

        public override void Render()
        {
            ImGui.BeginChild(_imguiId, _size, _childFlags);

            for (int i = 0; i < _items.Count; i++)
            {
                string display = _ids.Count > i ? $"[{_ids[i]}] {_items[i]}" : _items[i];

                if (ImGui.Selectable(display, _selectedIndex == i))
                {
                    _selectedIndex = i;
                    _selectedItem = _items[i];
                    InvokeItemSelected(_selectedIndex, _selectedItem);
                }
            }

            ImGui.EndChild();
        }

        public void SetSize(Vector2 size) => _size = size;
    }
}
