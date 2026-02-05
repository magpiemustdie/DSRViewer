using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using Veldrid;

namespace DSRViewer.ImGuiHelper
{
    public abstract class ImGuiChild
    {
        protected ImGuiChildFlags _childFlags = ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AlwaysAutoResize;

        protected string _childName = "Child";
        protected Vector2 _childSize;
        protected bool _showChild;

        protected Vector2 _minSize = new(0, 0);
        protected Vector2 _maxSize = new(500, 500);

        public ImGuiChild() { }
        public ImGuiChild(string childName, bool showChild) : this()
        {
            this._childName = childName;
            this._showChild = showChild;
        }

        public virtual void Render()
        {
            if (_showChild)
            {
                ImGui.SetNextWindowSizeConstraints(_minSize, _maxSize);
                ImGui.BeginChild($"{_childName}", _childSize, _childFlags);
                {
                    ImGui.Text($"childName: {_childName}");
                }
                ImGui.EndChild();
            }
        }

        public virtual void Render(GraphicsDevice _gd, ImGuiController _controller)
        {
            if (_showChild)
            {
                ImGui.SetNextWindowSizeConstraints(_minSize, _maxSize);
                ImGui.BeginChild($"{_childName}", _childSize, _childFlags);
                {
                    ImGui.Text($"childName: {_childName}");
                }
                ImGui.EndChild();
            }
        }

        public virtual void SetChildName(string childName)
        {
            this._childName = childName;
        }

        public string GetChildName() => _childName;

        public void ShowChild(bool showChild)
        {
            this._showChild = showChild;
        }

        public bool IsShowChild() => _showChild;

        public void SetChildFlags(ImGuiChildFlags flags)
        {
            _childFlags = flags;
        }

        public void SetMinMaxChildSize(Vector2 minSize, Vector2 maxSize)
        {
            _minSize = minSize;
            _maxSize = maxSize;
        }
    }
}
