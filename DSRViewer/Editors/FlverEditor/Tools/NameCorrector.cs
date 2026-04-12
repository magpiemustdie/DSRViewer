using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSRViewer.UI.Base;
using DSRViewer.FileProcess;
using DSRViewer.Editors.FlverEditor.Tools;
using ImGuiNET;
using SoulsFormats;

namespace DSRViewer.Editors.FlverEditor.Tools
{
    /// <summary>Окно корректора имён текстур в FLVER-файлах (регистр, замена путей).</summary>
    internal class FlverNameCorrector : ImGuiWindow
    {
        string texcorname = string.Empty;
        string texgtype = string.Empty;
        string texcorname_new = string.Empty;
        FlverTools _flverTools = new();
        bool _useBytePatch = true;

        public FlverNameCorrector(string windowName, bool showWindow) : base(windowName, showWindow)
        {
            _windowFlags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        }

        /// <summary>Отображает окно корректора имён текстур в FLVER-файлах.</summary>
        public void Render(List<FileNode> flverfilelist)
        {
            if (!_showWindow) return;

            ImGui.Begin(_windowName, ref _showWindow, ImGuiWindowFlags.MenuBar | _windowFlags);
            RenderMenuBar(flverfilelist);
            RenderNameCorrector(flverfilelist);
            ImGui.End();
        }

        private void RenderMenuBar(List<FileNode> flverfilelist)
        {
            if (!ImGui.BeginMenuBar()) return;

            if (ImGui.BeginMenu("Tools"))
            {
                if (ImGui.MenuItem("Lowcase fix"))
                    RunFlverOp(flverfilelist, writeBack: true, _useBytePatch, (flver, _, __, ___) =>
                    {
                        if (_flverTools.TexCorrectorFinderToLower(flver))
                            _flverTools.TexCorrectorToLower(flver);
                    });

                if (ImGui.MenuItem("Find errors"))
                {
                    var bugList = new List<string>();
                    RunFlverOp(flverfilelist, writeBack: false, useBytePatch: false, (flver, realPath, name, _) =>
                        _flverTools.TexCorrectorFinder(flver.Materials, realPath, name, bugList));
                    File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "NameBugs.txt"), bugList);
                    Console.WriteLine($"Saved {bugList.Count} errors to NameBugs.txt");
                }

                ImGui.EndMenu();
            }
            ImGui.EndMenuBar();
        }

        private void RenderNameCorrector(List<FileNode> flverfilelist)
        {
            // Без BeginChild — нет двойного скроллбара

            ImGui.Checkbox("Byte patch fallback", ref _useBytePatch);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("On Write() failure, patches FLVER bytes directly.\nNew name must not be longer than the old one (or it gets truncated from the start).");
            ImGui.Spacing();

            // Тип слота — комбо
            ImGui.SetNextItemWidth(300);
            if (ImGui.BeginCombo("g_type", texgtype, ImGuiComboFlags.HeightRegular))
            {
                foreach (var t in _texSlotTypes)
                {
                    bool sel = texgtype == t;
                    if (ImGui.Selectable(t, sel)) texgtype = t;
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.Separator();
                ImGui.TextDisabled("Custom:");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##gtype_custom", ref texgtype, 256);
                ImGui.EndCombo();
            }

            ImGui.SetNextItemWidth(300);
            ImGui.InputText("tex name", ref texcorname, 256);
            ImGui.SetNextItemWidth(300);
            ImGui.InputText("new tex name", ref texcorname_new, 256);

            if (ImGui.Button("Replace"))
                RunFlverOp(flverfilelist, writeBack: true, _useBytePatch, (flver, _, __, ___) =>
                {
                    if (_flverTools.TexFinder(flver.Materials, texcorname))
                        _flverTools.TexCorrectorReplacer(flver, texgtype, texcorname, texcorname_new);
                });
        }

        private static readonly string[] _texSlotTypes =
        [
            "g_Diffuse", "g_Diffuse_2",
            "g_Specular", "g_Specular_2",
            "g_Bumpmap", "g_Bumpmap_2", "g_Bumpmap_3",
            "g_DetailBumpmap",
            "g_Height", "g_Subsurf", "g_Lightmap"
        ];

        private static void RunFlverOp(List<FileNode> fileList, bool writeBack, bool useBytePatch,
            System.Action<FLVER2, string, string, System.Collections.Generic.List<string>> action)
        {
            var paths = fileList.Select(n => n.VirtualPath).ToList();
            new FileBinders().ProcessPaths(paths, new FileOperation
            {
                WriteObject          = writeBack,
                UseFlverDelegate     = true,
                UseBytePatchFallback = useBytePatch,
                AdditionalFlverProcessing = action
            });
        }
    }
}
