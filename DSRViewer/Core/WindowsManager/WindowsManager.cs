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
using DSRViewer.FileHelper;

namespace DSRViewer.Core.WindowsManager
{
    public class WindowsManager
    {
        List<ExplorerWindow> _explorerWindows = new();
        List<FMW> _flverEditorWindows = new();
        List<MTDWindow> _mtdEditorWindows = new();
        List<TransferFilesWindow> _transferFilesWindows = new();


        GraphicsDevice _gd;
        ImGuiController _controller;

        public WindowsManager(GraphicsDevice gd, ImGuiController controller)
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
            ViewTransferWindows();
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
                        MTDWindow mtdEditorWindow = new($"MTDE_{_mtdEditorWindows.Count + 1}", true);
                        _mtdEditorWindows.Add(mtdEditorWindow);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("New Transfer window..."))
                {
                    if (ImGui.MenuItem("Create..."))
                    {
                        TransferFilesWindow transferWindow = new($"Transfer files_{_transferFilesWindows.Count + 1}", true);
                        transferWindow.ExplorerWindows = _explorerWindows;
                        _transferFilesWindows.Add(transferWindow);
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
        private void ViewTransferWindows()
        {
            foreach (var window in _transferFilesWindows)
            {
                window.Render();
            }
        }

        
    }
}

