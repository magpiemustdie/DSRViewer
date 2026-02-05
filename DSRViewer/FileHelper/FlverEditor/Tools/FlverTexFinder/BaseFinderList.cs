using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.FileHelper.MTDEditor.Render;
using DSRViewer.ImGuiHelper;
using ImGuiNET;

namespace DSRViewer.FileHelper.FlverEditor.Tools.FlverTexFinder
{
    // Базовый класс для списков
    public abstract class BaseFinderList : ImGuiChild
    {
        protected List<string> _items = [];
        protected List<int> _ids = [];
        protected int _selectedIndex = -1;
        protected string _selectedItem = "";
        protected Vector2 _size;

        public event Action<int, string> OnItemSelected;

        public virtual void Clear()
        {
            _items.Clear();
            _ids.Clear();
            _selectedIndex = -1;
            _selectedItem = "";
        }

        public override void Render()
        {
            ImGui.BeginChild(GetType().Name, _size, _childFlags);

            for (int i = 0; i < _items.Count; i++)
            {
                var displayText = _ids.Count > i ? $"[{_ids[i]}] {_items[i]}" : _items[i];

                if (ImGui.Selectable(displayText, _selectedIndex == i))
                {
                    _selectedIndex = i;
                    _selectedItem = _items[i];
                    InvokeItemSelected(_selectedIndex, _selectedItem);
                }
            }

            ImGui.EndChild();
        }

        protected void InvokeItemSelected(int index, string item)
        {
            OnItemSelected?.Invoke(index, item);
        }

        public void SetSize(Vector2 size)
        {
            _size = size;
        }
    }
}
