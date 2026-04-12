using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SoulsFormats;
using ImGuiNET;
using DSRViewer.UI.Base;
using DSRViewer.FileProcess;
using DSRViewer.Editors.FlverEditor.Tools;
using DSRViewer.Editors.MTDEditor;

namespace DSRViewer.Editors.FlverEditor.Tools
{
    /// <summary>Окно массовой замены MTD в FLVER-файлах с логированием результатов.</summary>
    internal class FlverMTDReplacer : ImGuiWindow
    {
        string texturename   = string.Empty;
        string texslottype   = string.Empty;
        string mtdnamefinder = string.Empty;
        string mtdnewname    = string.Empty;
        string heightnewname = string.Empty;
        string _uvWarning    = string.Empty;

        FlverTools _flverTools = new();
        bool _useBytePatch = true;

        // Галки автозаполнения слотов
        bool _fillSpecular      = true;
        bool _fillBumpmap       = true;
        bool _fillHeight        = true;
        bool _fillSubsurf       = true;
        bool _fillDetailBumpmap = true;
        bool _fillLightmap      = true;

        private static readonly string[] _texSlotTypes =
        [
            "g_Diffuse", "g_Diffuse_2",
            "g_Specular", "g_Specular_2",
            "g_Bumpmap", "g_Bumpmap_2", "g_Bumpmap_3",
            "g_DetailBumpmap",
            "g_Height", "g_Subsurf", "g_Lightmap"
        ];

        public FlverMTDReplacer(string windowName, bool showWindow) : base(windowName, showWindow)
        {
            _windowFlags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        }

        /// <summary>Отображает окно замены MTD и выполняет замену в загруженных FLVER-файлах.</summary>
        public void Render(List<FileNode> flverfilelist, List<MTDShortDetails> mtdList)
        {
            if (!_showWindow) return;

            ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
            // Без BeginChild — нет двойного скроллбара

            // Имя текстуры — свободный ввод
            ImGui.SetNextItemWidth(300);
            ImGui.InputText("tex name", ref texturename, 256);

            // Тип слота — комбо (опциональный фильтр)
            ImGui.SetNextItemWidth(300);
            if (ImGui.BeginCombo("tex type (optional)", texslottype, ImGuiComboFlags.HeightRegular))
            {
                if (ImGui.Selectable("(any)##slot_any", string.IsNullOrEmpty(texslottype)))
                    texslottype = "";
                ImGui.Separator();
                foreach (var t in _texSlotTypes)
                {
                    bool sel = texslottype == t;
                    if (ImGui.Selectable(t, sel)) texslottype = t;
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.Separator();
                ImGui.TextDisabled("Custom:");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##slot_custom", ref texslottype, 256);
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Filter by slot type. Leave empty to search all slots.");

            // MTD для поиска — комбо + ручной ввод
            ImGui.SetNextItemWidth(300);
            RenderMtdCombo("mtd_finder##combo", ref mtdnamefinder, mtdList, null);

            // Новый MTD — комбо + ручной ввод + UV-предупреждение
            ImGui.SetNextItemWidth(300);
            if (RenderMtdCombo("mtd_new##combo", ref mtdnewname, mtdList, mtdnamefinder))
                _uvWarning = CheckUvWarning(mtdnamefinder, mtdnewname, mtdList);

            if (!string.IsNullOrEmpty(_uvWarning))
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), _uvWarning);

            // Высота (только для full replace)
            ImGui.SetNextItemWidth(300);
            ImGui.InputText("new_height", ref heightnewname, 100);

            ImGui.Spacing();

            // Галки автозаполнения — показываем только слоты которые есть в новом MTD
            var newMtdInfo = mtdList.FirstOrDefault(m =>
                m.Name.Split('\\').Last().Equals(mtdnewname, StringComparison.OrdinalIgnoreCase));
            if (newMtdInfo != null && newMtdInfo.TexType.Count > 0)
            {
                bool hasSpecular      = newMtdInfo.TexType.Any(t => t.StartsWith("g_Specular",      StringComparison.OrdinalIgnoreCase));
                bool hasBumpmap       = newMtdInfo.TexType.Any(t => t.StartsWith("g_Bumpmap",       StringComparison.OrdinalIgnoreCase));
                bool hasHeight        = newMtdInfo.TexType.Any(t => t.Equals("g_Height",            StringComparison.OrdinalIgnoreCase));
                bool hasSubsurf       = newMtdInfo.TexType.Any(t => t.Equals("g_Subsurf",           StringComparison.OrdinalIgnoreCase));
                bool hasDetailBumpmap = newMtdInfo.TexType.Any(t => t.Equals("g_DetailBumpmap",     StringComparison.OrdinalIgnoreCase));
                bool hasLightmap      = newMtdInfo.TexType.Any(t => t.Equals("g_Lightmap",          StringComparison.OrdinalIgnoreCase));

                if (hasSpecular || hasBumpmap || hasHeight || hasSubsurf || hasDetailBumpmap || hasLightmap)
                {
                    ImGui.TextDisabled("Auto-fill new slots:");
                    if (hasSpecular)      { ImGui.SameLine(); ImGui.Checkbox("Specular##af",      ref _fillSpecular); }
                    if (hasBumpmap)       { ImGui.SameLine(); ImGui.Checkbox("Bumpmap##af",       ref _fillBumpmap); }
                    if (hasHeight)        { ImGui.SameLine(); ImGui.Checkbox("Height##af",        ref _fillHeight); }
                    if (hasSubsurf)       { ImGui.SameLine(); ImGui.Checkbox("Subsurf##af",       ref _fillSubsurf); }
                    if (hasDetailBumpmap) { ImGui.SameLine(); ImGui.Checkbox("DetailBump##af",    ref _fillDetailBumpmap); }
                    if (hasLightmap)      { ImGui.SameLine(); ImGui.Checkbox("Lightmap##af",      ref _fillLightmap); }
                }
            }

            ImGui.Spacing();
            ImGui.Checkbox("Byte patch fallback", ref _useBytePatch);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("On Write() failure, patches FLVER bytes directly.\nNew name must not be longer than the old one (or it gets truncated from the start).");

            ImGui.Spacing();
            ShowMaterial(mtdList);

            if (ImGui.Button("Replace mtd (full)") && mtdList.Count != 0)
                RunReplacement(flverfilelist, mtdList, fullReplace: true);

            ImGui.SameLine();
            if (ImGui.Button("Replace mtd (only name)") && mtdList.Count != 0)
                RunReplacement(flverfilelist, mtdList, fullReplace: false);
            ImGui.End();
        }

        /// <summary>
        /// Рисует комбо для выбора MTD из списка + поле ручного ввода.
        /// Возвращает true если значение изменилось.
        /// </summary>
        private static bool RenderMtdCombo(string label, ref string value,
            List<MTDShortDetails> mtdList, string compareWith)
        {
            bool changed = false;
            string display = value.Split('\\').LastOrDefault() ?? value;

            if (ImGui.BeginCombo(label, display, ImGuiComboFlags.HeightLarge))
            {
                // Отдельное поле фильтра внутри комбо
                string filterKey = $"##filter_{label}";
                if (!_mtdComboFilters.TryGetValue(label, out string filter)) filter = "";
                ImGui.SetNextItemWidth(-30);
                if (ImGui.InputText(filterKey, ref filter, 128))
                    _mtdComboFilters[label] = filter;
                ImGui.SameLine();
                if (ImGui.SmallButton($"X{filterKey}")) { filter = ""; _mtdComboFilters[label] = ""; }
                ImGui.Separator();

                string filterLow = filter.ToLower();
                foreach (var m in mtdList)
                {
                    string mName = m.Name.Split('\\').Last();
                    if (!string.IsNullOrEmpty(filterLow) &&
                        !mName.Contains(filterLow, StringComparison.OrdinalIgnoreCase)) continue;

                    bool sel = value.EndsWith(mName, StringComparison.OrdinalIgnoreCase);
                    if (ImGui.Selectable($"{mName} [MW{m.MW}]##{label}_{mName}", sel))
                    {
                        value   = mName;
                        changed = true;
                    }
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.Separator();
                ImGui.TextDisabled("Custom:");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText($"##custom_{label}", ref value, 512))
                    changed = true;
                ImGui.EndCombo();
            }
            return changed;
        }

        // Фильтры для каждого комбо (по label)
        private static readonly Dictionary<string, string> _mtdComboFilters = new();

        /// <summary>
        /// Сравнивает UV-сеты старого и нового MTD по их TexType.
        /// Возвращает предупреждение если количество UV-сетов отличается.
        /// </summary>
        private static string CheckUvWarning(string oldMtdName, string newMtdName,
            List<MTDShortDetails> mtdList)
        {
            if (string.IsNullOrEmpty(oldMtdName) || string.IsNullOrEmpty(newMtdName))
                return "";

            string oldFile = oldMtdName.Split('\\').Last();
            string newFile = newMtdName.Split('\\').Last();

            var oldInfo = mtdList.FirstOrDefault(m =>
                m.Name.Equals(oldFile, StringComparison.OrdinalIgnoreCase));
            var newInfo = mtdList.FirstOrDefault(m =>
                m.Name.Equals(newFile, StringComparison.OrdinalIgnoreCase));

            if (oldInfo == null || newInfo == null) return "";

            int oldUv = CountUvFromTexTypes(oldInfo.TexType);
            int newUv = CountUvFromTexTypes(newInfo.TexType);

            if (oldUv == newUv) return "";
            return $"⚠ UV mismatch: old MTD ~{oldUv} UV, new MTD ~{newUv} UV — mesh may break!";
        }

        private static int CountUvFromTexTypes(IEnumerable<string> texTypes)
        {
            var uvIndices = new HashSet<int>();
            foreach (var t in texTypes)
            {
                if (t.Equals("g_Lightmap", StringComparison.OrdinalIgnoreCase) ||
                    t.EndsWith("_3", StringComparison.OrdinalIgnoreCase))
                    uvIndices.Add(3);
                else if (t.EndsWith("_2", StringComparison.OrdinalIgnoreCase) ||
                         t.Contains("_2_", StringComparison.OrdinalIgnoreCase))
                    uvIndices.Add(2);
                else
                    uvIndices.Add(1);
            }
            return uvIndices.Count > 0 ? uvIndices.Max() : 1;
        }

        private void RunReplacement(List<FileNode> flverfilelist, List<MTDShortDetails> mtdList, bool fullReplace)
        {
            string logPath = $"mtd_replacement_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            int success = 0, fail = 0, skipped = 0;

            using var log = new StreamWriter(logPath, append: false);
            log.WriteLine($"MTD Replacement Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.WriteLine("=========================================");

            RunFlverOp(flverfilelist, writeBack: true, _useBytePatch, (flver, virtualPath, name, errorLogs) =>
            {
                try
                {
                    var mats = flver.Materials;

                    // Фильтр по имени текстуры (если задан)
                    if (!string.IsNullOrEmpty(texturename) &&
                        !_flverTools.TexFinder(mats, texturename))
                    { log.WriteLine($"  SKIPPED: texture '{texturename}' not found in {virtualPath}"); skipped++; return; }

                    // Фильтр по типу слота (если задан)
                    if (!string.IsNullOrEmpty(texslottype) &&
                        !_flverTools.TexSlotFinder(mats, texslottype))
                    { log.WriteLine($"  SKIPPED: slot '{texslottype}' not found in {virtualPath}"); skipped++; return; }

                    // Проверяем наличие искомого MTD (с учётом текстуры если задана)
                    bool mtdFound = string.IsNullOrEmpty(texturename)
                        ? _flverTools.MTDFinder(mats, mtdnamefinder)
                        : _flverTools.MTDFinder(mats, texturename, mtdnamefinder);

                    if (!mtdFound)
                    { log.WriteLine($"  SKIPPED: MTD '{mtdnamefinder}' not found in {virtualPath}"); skipped++; return; }

                    if (fullReplace)
                    {
                        var opts = new FlverTools.AutoFillOptions
                        {
                            Specular      = _fillSpecular,
                            Bumpmap       = _fillBumpmap,
                            Height        = _fillHeight,
                            Subsurf       = _fillSubsurf,
                            DetailBumpmap = _fillDetailBumpmap,
                            Lightmap      = _fillLightmap,
                        };
                        mats = _flverTools.MTDReplacerHeight(mtdList, mats, texturename, mtdnamefinder, mtdnewname, heightnewname, opts);
                        _flverTools.FlverMTDWriter(flver, mats, virtualPath);
                        log.WriteLine($"  SUCCESS (full): {virtualPath}");
                    }
                    else
                    {
                        _flverTools.MTDReplacer(mats, texturename, mtdnamefinder, mtdnewname);
                        _flverTools.FlverMTDWriter(flver, mats, virtualPath);
                        log.WriteLine($"  SUCCESS (name): {virtualPath}");
                    }
                    success++;
                }
                catch (Exception ex)
                {
                    log.WriteLine($"  FAILED: {virtualPath} — {ex.Message}");
                    fail++;
                }
            });

            log.WriteLine("=========================================");
            log.WriteLine($"Total: {flverfilelist.Count}  Success: {success}  Skipped: {skipped}  Failed: {fail}");
            Console.WriteLine($"MTD replacement done. Log: {logPath}");
        }

        private static void RunFlverOp(List<FileNode> fileList, bool writeBack, bool useBytePatch,
            Action<FLVER2, string, string, List<string>> action)
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

        private void ShowMaterial(List<MTDShortDetails> mtdList)
        {
            if (!ImGui.Button("Test material")) return;

            foreach (var m in mtdList.Where(m =>
                m.Name.Equals(mtdnamefinder, StringComparison.OrdinalIgnoreCase) ||
                m.Name.Equals(mtdnewname, StringComparison.OrdinalIgnoreCase) ||
                m.Name.Split('\\').Last().Equals(mtdnamefinder, StringComparison.OrdinalIgnoreCase) ||
                m.Name.Split('\\').Last().Equals(mtdnewname, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine(m.Name);
                foreach (var tex in m.TexType)
                    Console.WriteLine($"  {tex}");
            }
        }
    }
}
