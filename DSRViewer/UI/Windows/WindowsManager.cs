using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ImGuiNET;
using Veldrid;
using DSRViewer.Core;
using DSRViewer.UI.Base;
using DSRViewer.Editors.FlverEditor;
using DSRViewer.Editors.Explorer;
using DSRViewer.Editors.Explorer.DDSHelper;
using DSRViewer.Editors.MTDEditor;
using DSRViewer.FileProcess;

namespace DSRViewer.UI.Windows
{
    /// <summary>Управляет всеми открытыми окнами приложения и главным меню.</summary>
    public class WindowsManager : IDisposable
    {
        List<ExplorerWindow> _explorerWindows = new();
        List<FMW> _flverEditorWindows = new();
        List<MTDWindow> _mtdEditorWindows = new();
        List<TransferFilesWindow> _transferFilesWindows = new();
        List<CutsceneEditor> _cutsceneWindows = new();
        // DDSTextureViewChild для каждого FMW окна (по имени окна)
        Dictionary<string, DDSTextureViewChild> _flverTexPreviews = new();

        GraphicsDevice _gd;
        ImGuiController _controller;

        public WindowsManager(GraphicsDevice gd, ImGuiController controller)
        {
            _gd = gd;
            _controller = controller;
        }
        /// <summary>Выполняет рендеринг всех активных окон и главного меню.</summary>
        public void MainRender()
        {
            ViewMainMenubar();
            ViewExplorerWindows();
            ViewFlverEditorWindows();
            ViewMTDEditorWindows();
            ViewTransferWindows();
            ViewCutsceneWindows();
        }

        private void ViewMainMenubar()
        {
            ImGui.BeginMainMenuBar();
            {
                if (ImGui.BeginMenu("New explorer..."))
                {
                    if (ImGui.MenuItem("Create..."))
                    {
                        int idx = GetNextIndex(_explorerWindows.Select(w => w.GetWindowName()), "E");
                        ExplorerWindow explorerWindow = new($"E{idx}", true, _gd, _controller);
                        _explorerWindows.Add(explorerWindow);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("New Flver Editor..."))
                {
                    if (ImGui.MenuItem("Create..."))
                    {
                        int idx = GetNextIndex(_flverEditorWindows.Select(w => w.GetWindowName()), "FE_");
                        FMW flverEditorWindow = new($"FE_{idx}", true);

                        // Поиск текстуры по имени во всех открытых Explorer
                        flverEditorWindow.ExplorerSearchDelegate = texName =>
                            _explorerWindows
                                .SelectMany(e => e.GetTreeChildList())
                                .SelectMany(t => t.Root.FindAll(n =>
                                    n.IsDDS && n.Name.Equals(texName, StringComparison.OrdinalIgnoreCase)))
                                .ToList();

                        // Превью текстуры через DDSTextureViewChild
                        var texPreview = new DDSTextureViewChild(
                            $"FE_{idx} - TexPreview", false);
                        _flverTexPreviews[$"FE_{idx}"] = texPreview;

                        FileNode lastPreviewNode = null;

                        // ShowTexturePreviewDelegate: только сохраняет узел
                        flverEditorWindow.ShowTexturePreviewDelegate = node =>
                        {
                            lastPreviewNode = node;
                        };

                        // OnRenderExtra: каждый кадр рендерит pop-out если узел задан
                        flverEditorWindow.OnRenderExtra = () =>
                        {
                            if (lastPreviewNode != null)
                                texPreview.RenderPopout(_gd, _controller, lastPreviewNode);
                        };

                        _flverEditorWindows.Add(flverEditorWindow);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("New MTD Editor..."))
                {
                    if (ImGui.MenuItem("Create..."))
                    {
                        int idx = GetNextIndex(_mtdEditorWindows.Select(w => w.GetWindowName()), "MTDE_");
                        MTDWindow mtdEditorWindow = new($"MTDE_{idx}", true);
                        _mtdEditorWindows.Add(mtdEditorWindow);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("New Transfer window..."))
                {
                    if (ImGui.MenuItem("Create..."))
                    {
                        int idx = GetNextIndex(_transferFilesWindows.Select(w => w.GetWindowName()), "Transfer files_");
                        TransferFilesWindow transferWindow = new($"Transfer files_{idx}", true);
                        transferWindow.ExplorerWindows = _explorerWindows;
                        _transferFilesWindows.Add(transferWindow);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("New Remo editor..."))
                {
                    if (ImGui.MenuItem("Create..."))
                    {
                        int idx = GetNextIndex(_cutsceneWindows.Select(w => w.GetWindowName()), "Cutscene editor_");
                        CutsceneEditor cutsceneWindow = new($"Cutscene editor_{idx}", true);
                        _cutsceneWindows.Add(cutsceneWindow);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Tools"))
                {
                    if (ImGui.MenuItem("Open app folder"))
                        Process.Start("explorer.exe", AppContext.BaseDirectory);
                    ImGui.EndMenu();
                }

            }
            ImGui.EndMainMenuBar();
        }

        private void ViewExplorerWindows()
        {
            foreach (var window in _explorerWindows)
                window.Render();
            _explorerWindows.RemoveAll(w => !w.IsShowWindow());
        }

        private void ViewFlverEditorWindows()
        {
            foreach (var window in _flverEditorWindows)
                window.Render();

            var closed = _flverEditorWindows.Where(w => !w.IsShowWindow()).ToList();
            foreach (var w in closed)
            {
                if (_flverTexPreviews.TryGetValue(w.GetWindowName(), out var preview))
                {
                    preview.Cleanup();
                    _flverTexPreviews.Remove(w.GetWindowName());
                }
            }
            _flverEditorWindows.RemoveAll(w => !w.IsShowWindow());
        }

        private void ViewMTDEditorWindows()
        {
            foreach (var window in _mtdEditorWindows)
                window.Render();
            _mtdEditorWindows.RemoveAll(w => !w.IsShowWindow());
        }

        private void ViewTransferWindows()
        {
            foreach (var window in _transferFilesWindows)
                window.Render();
            _transferFilesWindows.RemoveAll(w => !w.IsShowWindow());
        }

        private void ViewCutsceneWindows()
        {
            foreach (var window in _cutsceneWindows)
                window.Render();
            _cutsceneWindows.RemoveAll(w => !w.IsShowWindow());
        }

        // Ищет первый свободный номер среди открытых окон по их именам
        private int GetNextIndex(IEnumerable<string> existingNames, string prefix)
        {
            var usedNumbers = existingNames
                .Where(n => n.StartsWith(prefix))
                .Select(n => int.TryParse(n[prefix.Length..], out int num) ? num : 0)
                .ToHashSet();

            int i = 1;
            while (usedNumbers.Contains(i)) i++;
            return i;
        }

        /// <summary>Освобождает GPU-ресурсы всех открытых вкладок и превью текстур.</summary>
        public void Dispose()
        {
            foreach (var w in _explorerWindows)
                foreach (var tab in w.GetTreeChildList())
                    tab.Dispose();

            foreach (var preview in _flverTexPreviews.Values)
                preview.Cleanup();
            _flverTexPreviews.Clear();
        }
    }
}

