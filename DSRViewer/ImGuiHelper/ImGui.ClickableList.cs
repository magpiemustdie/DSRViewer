using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using DSRViewer.FileHelper;
using SoulsFormats;

namespace DSRViewer.ImGuiHelper
{
    public class ImGuiClickableList : ImGuiChild
    {
        protected int SelectedItem { get; set; } = -1;
        protected string SelectedItemName { get; set; } = string.Empty;

        public delegate void ClickActionNode(FileNode node, int index);
        public delegate void ClickActionString(string item, int index);
        public delegate void ClickActionMaterial(FLVER2.Material item, int index);
        public delegate void ClickActionMatTexture(FLVER2.Texture item, int index);

        public ClickActionNode ClickHandlerNode;
        public ClickActionString ClickHandlerString;
        public ClickActionMaterial ClickHandlerMaterial;
        public ClickActionMatTexture ClickHandlerMatTexture;


        public ImGuiClickableList()
        {
            ClickHandlerNode = DefaultClickFunctionNode;
            ClickHandlerString = DefaultClickFunctionString;
            ClickHandlerMaterial = DefaultClickFunctionMaterial;
            ClickHandlerMatTexture = DefaultClickFunctionMatTexture;
        }

        protected virtual void DefaultClickFunctionNode(FileNode node, int index)
        {
            Console.WriteLine("Default click handler for FileNode");
        }

        private void DefaultClickFunctionString(string item, int index)
        {
            Console.WriteLine($"Default click handler for string: {item}");
        }

        private void DefaultClickFunctionMaterial(FLVER2.Material item, int index)
        {
            Console.WriteLine($"Default click handler for material: {item}");
        }
        private void DefaultClickFunctionMatTexture(FLVER2.Texture item, int index)
        {
            Console.WriteLine($"Default click handler for material texture: {item}");
        }

        public virtual void Render()
        {
            ImGui.BeginChild("clickable list child", _childSize, _childFlags);
            {
                ImGui.Text("Put list here");
            }
            ImGui.EndChild();
        }
    }
}