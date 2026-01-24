using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using SoulsFormats;
using System.Security.Cryptography;
using Veldrid;
using System.Text;
using System.Diagnostics;
using Vulkan;
using System.Linq.Expressions;
using DSRViewer.ImGuiHelper;
using static Org.BouncyCastle.Math.EC.ECCurve;
using Veldrid.Sdl2;
using DSRViewer.FileHelper.FlverEditor.Render;
using DSRViewer.FileHelper.FileExplorer.Render;

namespace DSRViewer.Core.MainRender
{
    public class ViewMainWindow
    {
        List<ViewExplorerWindow> _explorerWindows = new();
        List<FMW> _flverExplorerWindows = new();
        public void MainRender(GraphicsDevice _gd, ImGuiController _controller)
        {

            ViewMainMenubar();

            ViewExplorerWindows(_gd, _controller);

            ViewFlverExplorerWindows();
        }

        private void ViewMainMenubar()
        {
            ImGui.BeginMainMenuBar();
            {
                if (ImGui.BeginMenu("New explorer..."))
                {
                    if (ImGui.MenuItem("Create..."))
                    {
                        ViewExplorerWindow explorerWindow = new();
                        explorerWindow.SetWindowName($"E{_explorerWindows.Count + 1}");
                        explorerWindow.SetSize(new Vector2(1000, 500));
                        explorerWindow.ShowWindow(true);
                        _explorerWindows.Add(explorerWindow);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("New Flver Editor..."))
                {
                    if (ImGui.MenuItem("Create..."))
                    {
                        FMW flverEditorWindow = new();
                        flverEditorWindow.SetWindowName($"FE_{_flverExplorerWindows.Count + 1}");
                        flverEditorWindow.ShowWindow(true);
                        _flverExplorerWindows.Add(flverEditorWindow);
                    }
                    ImGui.EndMenu();
                }
            }
            ImGui.EndMainMenuBar();
        }

        private void ViewExplorerWindows(GraphicsDevice _gd, ImGuiController _controller)
        {
            foreach (var window in _explorerWindows)
            {
                window.Render(_gd, _controller);
            }
        }

        private void ViewFlverExplorerWindows()
        {
            foreach (var window in _flverExplorerWindows)
            {
                window.Render();
            }
        }
    }
}

