using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using DSRViewer.FileHelper;

namespace DSRViewer.ImGuiHelper
{
    public class ClickableList : ImGuiChild
    {
        protected int _selectedItem = -1;
        protected string _selectedItemName = string.Empty;

        public delegate void ClickAction(FileNode node, int index);
        public delegate void ClickActionString(string item, int index);

        public ClickAction CurrentClickHandler;
        public ClickActionString CurrentClickHandlerString;

        public ClickableList()
        {
            CurrentClickHandler = DefaultClickFunction;
            CurrentClickHandlerString = DefaultClickFunctionString;
        }

        private void DefaultClickFunction(FileNode node, int index)
        {
            Console.WriteLine("Default click handler for FileNode");
        }

        private void DefaultClickFunctionString(string item, int index)
        {
            Console.WriteLine($"Default click handler for string: {item}");
        }

        public virtual void Render()
        {
            ImGui.BeginChild("clickable list child", _childSize, _childFlags);
            {
                ImGui.Text("put list here");
            }
            ImGui.EndChild();
        }

        public void Render(List<FileNode> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (ImGui.Selectable(items[i].Name, _selectedItem == i))
                {
                    _selectedItem = i;
                    _selectedItemName = items[i].Name;
                    CurrentClickHandler?.Invoke(items[i], i);
                }
            }
        }

        public void Render(List<string> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (ImGui.Selectable(items[i], _selectedItem == i))
                {
                    _selectedItem = i;
                    _selectedItemName = items[i];
                    CurrentClickHandlerString?.Invoke(items[i], i);
                }
            }
        }
    }
}