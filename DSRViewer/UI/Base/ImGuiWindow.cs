using System.Numerics;
using ImGuiNET;
using Veldrid;

namespace DSRViewer.UI.Base
{
    public class ImGuiWindow
    {
        protected ImGuiWindowFlags _windowFlags = ImGuiWindowFlags.NoDocking;
        protected ImGuiChildFlags _childFlags = ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AlwaysAutoResize;

        protected string _windowName = "Window";
        protected Vector2 _windowSize;
        protected Vector2 _minSize;
        protected Vector2 _maxSize;
        protected bool _showWindow;

        public ImGuiWindow() { }

        public ImGuiWindow(string windowName, bool isVisible = false)
        {
            _windowName = windowName;
            _showWindow = isVisible;
        }

        public virtual void Render()
        {
            if (!_showWindow) return;

            ApplySizeConstraints();
            ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
            ImGui.End();
        }

        // Вызывай в начале переопределённого Render() перед ImGui.Begin
        protected void ApplySizeConstraints()
        {
            if (_minSize != Vector2.Zero || _maxSize != Vector2.Zero)
                ImGui.SetNextWindowSizeConstraints(_minSize, _maxSize);

            // Начальный размер — только при первом появлении, потом пользователь сам
            var size = _windowSize != Vector2.Zero ? _windowSize : _minSize;
            if (size != Vector2.Zero)
                ImGui.SetNextWindowSize(size, ImGuiCond.Appearing);
        }

        public virtual void Render(GraphicsDevice gd, ImGuiController cl) => Render();

        public void ShowWindow(bool show) => _showWindow = show;
        public bool IsShowWindow() => _showWindow;

        public void SetWindowName(string windowName) => _windowName = windowName;
        public string GetWindowName() => _windowName;

        public void SetSize(Vector2 size) => _windowSize = size;
        public Vector2 GetWindowSize() => _windowSize;

        public void SetMinMaxWindowSize(Vector2 minSize, Vector2 maxSize)
        {
            _minSize = minSize;
            _maxSize = maxSize;
        }
    }
}
