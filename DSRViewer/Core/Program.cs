using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using ImGuiNET;
using System.Diagnostics;
using System.Numerics;
using DSRViewer.UI.Base;
using DSRViewer.UI.Windows;

namespace DSRViewer
{
    class Program
    {
        private static Sdl2Window _window;
        private static GraphicsDevice _gd;
        private static CommandList _cl;
        private static ImGuiController _controller;
        private static Vector3 _clearColor = new Vector3(0f, 0f, 0f);

        static int _windowWidth = 1500;
        static int _windowHeight = 1000;
        static void Main(string[] args)
        {
            VeldridStartup.CreateWindowAndGraphicsDevice(
                new WindowCreateInfo(50, 50, _windowWidth, _windowHeight, WindowState.Normal, "DSRViewer"),
                new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
                out _window,
                out _gd);
            _window.Resized += () =>
            {
                _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
                _controller.WindowResized(_window.Width, _window.Height);
            };
            _cl = _gd.ResourceFactory.CreateCommandList();
            _controller = new ImGuiController(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);

            var stopwatch = Stopwatch.StartNew();
            float deltaTime = 0f;
            // Main application loop
            WindowsManager mainWindow = new(_gd, _controller);

            while (_window.Exists)
            {
                deltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
                stopwatch.Restart();

                InputSnapshot snapshot = _window.PumpEvents();
                if (!_window.Exists) { break; }

                bool minimized = _window.WindowState == WindowState.Minimized;

                // При свёрнутом окне — пропускаем рендер, только спим
                if (minimized)
                {
                    System.Threading.Thread.Sleep(200);
                    stopwatch.Restart();
                    continue;
                }

                _controller.Update(deltaTime, snapshot);

                // Draw ImGui UI

                mainWindow.MainRender();

                _cl.Begin();
                _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
                _cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));
                _controller.Render(_gd, _cl);
                _cl.End();
                _gd.SubmitCommands(_cl);
                _gd.SwapBuffers(_gd.MainSwapchain);

                // Ограничение FPS: 30 в фокусе, 5 при неактивном
                float targetMs = _window.Focused ? 1000f / 30f : 1000f / 5f;

                float frameMs = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency * 1000f;
                if (frameMs < targetMs)
                    System.Threading.Thread.Sleep((int)(targetMs - frameMs));
            }

            // Clean up Veldrid resources
            _gd.WaitForIdle();
            mainWindow.Dispose();
            _controller.Dispose();
            _cl.Dispose();
            _gd.Dispose();
        }
    }
}
