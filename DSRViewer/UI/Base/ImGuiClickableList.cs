using System.Numerics;
using ImGuiNET;
using DSRViewer.FileProcess;
using SoulsFormats;

namespace DSRViewer.UI.Base
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

        public ImGuiClickableList() { }

        public ImGuiClickableList(string childName, Vector2 size)
        {
            _childName = childName;
            _childSize = size;
        }

        public override void Render()
        {
            if (!_showChild) return;

            ImGui.BeginChild(_childName, _childSize, ImGuiChildFlags.None);
            ImGui.EndChild();
        }

        protected void InvokeNode(FileNode node, int index) =>
            ClickHandlerNode?.Invoke(node, index);

        protected void InvokeString(string item, int index) =>
            ClickHandlerString?.Invoke(item, index);

        protected void InvokeMaterial(FLVER2.Material item, int index) =>
            ClickHandlerMaterial?.Invoke(item, index);

        protected void InvokeMatTexture(FLVER2.Texture item, int index) =>
            ClickHandlerMatTexture?.Invoke(item, index);
    }
}
