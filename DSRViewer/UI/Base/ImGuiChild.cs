using System.Numerics;
using ImGuiNET;
using Veldrid;

namespace DSRViewer.UI.Base
{
    public abstract class ImGuiChild
    {
        protected ImGuiChildFlags _childFlags = ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AlwaysAutoResize;

        protected string _childName = "Child";
        protected Vector2 _childSize;
        protected bool _showChild;

        protected Vector2 _minSize = new(0, 0);
        protected Vector2 _maxSize = new(-1, -1);

        public ImGuiChild() { }

        public ImGuiChild(string childName, bool showChild)
        {
            _childName = childName;
            _showChild = showChild;
        }

        public virtual void Render()
        {
            if (!_showChild) return;

            ImGui.SetNextWindowSizeConstraints(_minSize, _maxSize);
            ImGui.BeginChild(_childName, _childSize, _childFlags);
            ImGui.EndChild();
        }

        public virtual void Render(GraphicsDevice gd, ImGuiController controller) => Render();

        public virtual void SetChildName(string childName) => _childName = childName;
        public string GetChildName() => _childName;

        public void ShowChild(bool show) => _showChild = show;
        public bool IsShowChild() => _showChild;

        public void SetChildFlags(ImGuiChildFlags flags) => _childFlags = flags;

        public void SetMinMaxChildSize(Vector2 minSize, Vector2 maxSize)
        {
            _minSize = minSize;
            _maxSize = maxSize;
        }

        public Vector2 GetChildSize() => _childSize;
    }
}
