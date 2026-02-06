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
using DSRViewer.FileHelper.MTDEditor.Render;

namespace DSRViewer.Core.MainRender
{
    public class ViewMainWindow
    {
        List<ExplorerWindow> _explorerWindows = new();
        List<FMW> _flverEditorWindows = new();
        List<MTDWindow> _mtdEditorWindows = new();

        GraphicsDevice _gd;
        ImGuiController _controller;

        public ViewMainWindow(GraphicsDevice gd, ImGuiController controller)
        {
            _gd = gd;
            _controller = controller;
        }
        public void MainRender()
        {
            ViewMainMenubar();
            ViewExplorerWindows();
            ViewFlverEditorWindows();
            ViewMTDEditorWindows();
        }

        private void ViewMainMenubar()
        {
            ImGui.BeginMainMenuBar();
            {
                if (ImGui.BeginMenu("New explorer..."))
                {
                    if (ImGui.MenuItem("Create..."))
                    {
                        ExplorerWindow explorerWindow = new($"E{_explorerWindows.Count + 1}", true, _gd, _controller);
                        _explorerWindows.Add(explorerWindow);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("New Flver Editor..."))
                {
                    if (ImGui.MenuItem("Create..."))
                    {
                        FMW flverEditorWindow = new($"FE_{_flverEditorWindows.Count + 1}", true);
                        _flverEditorWindows.Add(flverEditorWindow);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("New MTD Editor..."))
                {
                    if (ImGui.MenuItem("Create..."))
                    {
                        MTDWindow _mtdEditorWindow = new($"MTDE_{_mtdEditorWindows.Count + 1}", true);
                        _mtdEditorWindow.ShowWindow(true);
                        _mtdEditorWindows.Add(_mtdEditorWindow);
                    }
                    ImGui.EndMenu();
                }

            }
            ImGui.EndMainMenuBar();
        }

        private void ViewExplorerWindows()
        {
            foreach (var window in _explorerWindows)
            {
                window.Render();
            }
        }

        private void ViewFlverEditorWindows()
        {
            foreach (var window in _flverEditorWindows)
            {
                window.Render();
            }
        }

        private void ViewMTDEditorWindows()
        {
            foreach (var window in _mtdEditorWindows)
            {
                window.Render();
            }
        }
    }
}

