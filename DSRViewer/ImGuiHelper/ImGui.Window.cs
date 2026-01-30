using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using ImGuiNET;
using Vortice.Direct3D11;
using Veldrid;

namespace DSRViewer.ImGuiHelper
{
    public class ImGuiWindow
    {
        protected ImGuiWindowFlags _windowFlags = ImGuiWindowFlags.AlwaysAutoResize;
        protected ImGuiChildFlags _childFlags = ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AlwaysAutoResize;
        protected string _windowName { get; set; } = "Window";
        protected Vector2 _windowSize { get; set; }
        protected Vector2 _childSize { get; set; }

        protected Vector2 _minSize { get; set; }
        protected Vector2 _maxSize  { get; set; }

        protected bool _showWindow;

        public ImGuiWindow() { }
        public ImGuiWindow(string windowName) : this()
        {
            this._windowName = windowName;
        }

        public ImGuiWindow(string windowName, bool isVisible) : this(windowName)
        {
            this._showWindow = isVisible;
        }

        public virtual void Render()
        {
            
            if (this._showWindow)
            {
                ImGui.SetNextWindowSizeConstraints(new Vector2(0, 0), new Vector2(400, 400));
                ImGui.Begin(_windowName, ref this._showWindow, _windowFlags);
                {
                    ImGui.Text($"Window render");
                }
                ImGui.End();
            }
        }

        public virtual void Render(GraphicsDevice gd, ImGuiController cl)
        {
            if (this._showWindow)
            {
                ImGui.SetNextWindowSizeConstraints(new Vector2(0, 0), new Vector2(400, 400));
                ImGui.Begin(_windowName, ref this._showWindow, _windowFlags);
                {
                    ImGui.Text($"Window render");
                }
                ImGui.End();
            }
        }

        public void SetWindowName(string windowName)
        {
            this._windowName = windowName;
        }

        public string GetWindowName() => _windowName;

        public void SetSize(Vector2 size)
        {
            this._windowSize = size;
        }

        public Vector2 GetWindowSize() => _windowSize;

        public void ShowWindow(bool showWindow)
        {
            this._showWindow = showWindow;
        }

        public bool IsShowWindow() => _showWindow;

        public void SetMinMaxWindowSize(Vector2 minSize, Vector2 maxSize)
        {
            _minSize = minSize;
            _maxSize = maxSize;
        }
    }
}
