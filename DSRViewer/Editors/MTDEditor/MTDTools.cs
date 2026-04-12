using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSRViewer.Editors.MTDEditor
{
    /// <summary>
    /// Инструменты массовой обработки MTD-файлов.
    /// Все операции применяются ко всем найденным BND-файлам (основной + Patch).
    /// Исключение: MergeWithPtdeMTDs пишет только в Patch.
    /// </summary>
    public class MTDTools
    {
        // ── Публичный API ────────────────────────────────────────────────

        /// <summary>Добавляет параметр g_MaterialWorkflow во все MTD во всех файлах.</summary>
        public void MassAddMaterialWorkflow(string folderOrFilePath)
        {
            RunOnAll(folderOrFilePath, (mtd, _) =>
                EnsureParam(mtd, "g_MaterialWorkflow"));
        }

        /// <summary>Добавляет параметр g_MaterialWorkflow в конкретный MTD во всех файлах.</summary>
        public void AddMaterialWorkflow(string folderOrFilePath, string mtdName)
        {
            RunOnMatching(folderOrFilePath, mtdName, (mtd, _) =>
                EnsureParam(mtd, "g_MaterialWorkflow"));
        }

        /// <summary>Переключает значение g_MaterialWorkflow (0↔1) во всех файлах.</summary>
        public void SwapMaterialWorkflow(string folderOrFilePath, string mtdName)
        {
            RunOnMatching(folderOrFilePath, mtdName, (mtd, _) =>
            {
                EnsureParam(mtd, "g_MaterialWorkflow");
                var prm = mtd.Params.First(p => p.Name == "g_MaterialWorkflow");
                prm.Value = Convert.ToInt32(!Convert.ToBoolean((int)prm.Value));
            });
        }

        /// <summary>
        /// Объединяет MTD из PTDE-архива в DSR Patch-файл (только Patch).
        /// PTDE-файлы получают суффикс _ptde и MW=1.
        /// </summary>
        public void MergeWithPtdeMTDs(string dsrFolderOrFile, string ptdeFolderOrFile)
        {
            string ptdePath = MTDReader.ResolveMainBndPath(ptdeFolderOrFile);
            if (string.IsNullOrEmpty(ptdePath))
            {
                Console.WriteLine("[MTDTools] MergeWithPtdeMTDs: cannot resolve PTDE BND path");
                return;
            }

            var patchBnd = GetOrCreatePatch(dsrFolderOrFile, out string patchPath);
            if (patchBnd == null) return;

            var ptdeBnd = MTDReader.ReadBnd(ptdePath);
            int added = 0, updated = 0;

            foreach (var entry in ptdeBnd.Files)
            {
                string newName = entry.Name.Replace(".mtd", "_ptde.mtd",
                    StringComparison.OrdinalIgnoreCase);

                var mtd = MTD.Read(entry.Bytes);
                EnsureParam(mtd, "g_MaterialWorkflow");
                SetParam(mtd, "g_MaterialWorkflow", 1);
                byte[] newBytes = mtd.Write();

                var existing = patchBnd.Files.FirstOrDefault(f =>
                    f.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
                if (existing != null) { existing.Bytes = newBytes; updated++; }
                else { patchBnd.Files.Add(new BinderFile { Name = newName, Bytes = newBytes }); added++; }
            }

            patchBnd.Write(patchPath);
            Console.WriteLine($"[MTDTools] Patch saved: {Path.GetFileName(patchPath)} (added {added}, updated {updated})");
        }

        /// <summary>Устанавливает параметры освещения и спекуляра во всех файлах.</summary>
        public void ReduceSpecular(string folderOrFilePath)
        {
            RunOnAll(folderOrFilePath, (mtd, _) =>
            {
                SetParamIfExists(mtd, "g_LightingType",          3);
                SetParamIfExists(mtd, "g_DiffuseMapColor",       new float[] { 1f, 1f, 1f });
                SetParamIfExists(mtd, "g_SpecularMapColor",      new float[] { 1f, 1f, 1f });
                SetParamIfExists(mtd, "g_DiffuseMapColorPower",  1f);
                SetParamIfExists(mtd, "g_SpecularMapColorPower", 1f);
                SetParamIfExists(mtd, "g_SpecularPower",         1f);
                SetParamIfExists(mtd, "g_ShadowPowMul",          1f);
            });
        }

        /// <summary>Применяет произвольное действие ко всем MTD во всех файлах.</summary>
        public void ApplyParams(string folderOrFilePath, Action<MTD> action)
        {
            RunOnAll(folderOrFilePath, (mtd, _) => action(mtd));
        }

        /// <summary>
        /// Экспортирует список имён MTD и их g_MaterialWorkflow в CSV-файлы рядом с папкой.
        /// Основной файл и Patch сохраняются отдельно.
        /// Формат: Name,MaterialWorkflow
        /// Возвращает список созданных файлов.
        /// </summary>
        public List<string> ExportMWList(string folderOrFilePath)
        {
            var created = new List<string>();

            string dir = Directory.Exists(folderOrFilePath)
                ? folderOrFilePath
                : Path.GetDirectoryName(folderOrFilePath) ?? "";

            foreach (var bndPath in MTDReader.ResolveMtdbndFiles(folderOrFilePath))
            {
                try
                {
                    var bnd = MTDReader.ReadBnd(bndPath);
                    string bndName = Path.GetFileNameWithoutExtension(
                        Path.GetFileNameWithoutExtension(bndPath)); // убираем .dcx и .mtdbnd

                    string outPath = Path.Combine(dir, $"{bndName}_MW_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                    using var w = new StreamWriter(outPath, false, System.Text.Encoding.UTF8);
                    w.WriteLine("Name,MaterialWorkflow");

                    foreach (var entry in bnd.Files.OrderBy(e => e.Name))
                    {
                        int mw = -1;
                        try
                        {
                            var mtd = MTD.Read(entry.Bytes);
                            var prm = mtd.Params.FirstOrDefault(p => p.Name == "g_MaterialWorkflow");
                            if (prm != null) mw = Convert.ToInt32(prm.Value);
                        }
                        catch { }
                        w.WriteLine($"{entry.Name},{(mw < 0 ? "" : mw.ToString())}");
                    }

                    Console.WriteLine($"[MTDTools] Exported {bnd.Files.Count} MTDs from {Path.GetFileName(bndPath)} → {outPath}");
                    created.Add(outPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MTDTools] ExportMWList: cannot read {bndPath}: {ex.Message}");
                }
            }

            return created;
        }

        // ── Вспомогательные методы ───────────────────────────────────────

        /// <summary>Применяет action ко всем MTD во всех BND-файлах (основной + Patch).</summary>
        private static void RunOnAll(string folderOrFilePath, Action<MTD, BinderFile> action)
        {
            foreach (var bndPath in MTDReader.ResolveMtdbndFiles(folderOrFilePath))
                RunOnBnd(bndPath, _ => true, action);
        }

        /// <summary>Применяет action к MTD с указанным именем во всех BND-файлах.</summary>
        private static void RunOnMatching(string folderOrFilePath, string mtdName,
            Action<MTD, BinderFile> action)
        {
            foreach (var bndPath in MTDReader.ResolveMtdbndFiles(folderOrFilePath))
                RunOnBnd(bndPath,
                    entry => entry.Name.Equals(mtdName, StringComparison.OrdinalIgnoreCase),
                    action);
        }

        private static void RunOnBnd(string bndPath, Func<BinderFile, bool> filter,
            Action<MTD, BinderFile> action)
        {
            try
            {
                var bnd = MTDReader.ReadBnd(bndPath);
                bool changed = false;

                foreach (var entry in bnd.Files.Where(filter))
                {
                    try
                    {
                        var mtd = MTD.Read(entry.Bytes);
                        action(mtd, entry);
                        entry.Bytes = mtd.Write();
                        changed = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MTDTools] Error processing '{entry.Name}': {ex.Message}");
                    }
                }

                if (changed)
                {
                    bnd.Write(bndPath);
                    Console.WriteLine($"[MTDTools] Saved {Path.GetFileName(bndPath)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MTDTools] Cannot process '{bndPath}': {ex.Message}");
            }
        }

        /// <summary>Возвращает существующий Patch BND или создаёт новый.</summary>
        private static BND3 GetOrCreatePatch(string folderOrFilePath, out string patchPath)
        {
            patchPath = MTDReader.ResolvePatchBndPath(folderOrFilePath);

            if (!string.IsNullOrEmpty(patchPath) && File.Exists(patchPath))
            {
                try { return MTDReader.ReadBnd(patchPath); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MTDTools] Cannot read Patch: {ex.Message}");
                    return null;
                }
            }

            string mainPath = MTDReader.ResolveMainBndPath(folderOrFilePath);
            if (string.IsNullOrEmpty(mainPath))
            {
                Console.WriteLine("[MTDTools] Cannot resolve main BND path to create Patch");
                return null;
            }

            string dir = Path.GetDirectoryName(mainPath) ?? "";
            string ext = mainPath.EndsWith(".dcx", StringComparison.OrdinalIgnoreCase)
                ? ".mtdbnd.dcx" : ".mtdbnd";
            patchPath = Path.Combine(dir, "MtdPatch" + ext);

            var patch = new BND3();
            try
            {
                var mainBnd = MTDReader.ReadBnd(mainPath);
                patch.Version      = mainBnd.Version;
                patch.Format       = mainBnd.Format;
                patch.BigEndian    = mainBnd.BigEndian;
                patch.BitBigEndian = mainBnd.BitBigEndian;
            }
            catch { }

            Console.WriteLine($"[MTDTools] Creating new Patch: {Path.GetFileName(patchPath)}");
            return patch;
        }

        // ── Утилиты ──────────────────────────────────────────────────────

        private static void EnsureParam(MTD mtd, string name)
        {
            if (!mtd.Params.Any(p => p.Name == name))
                mtd.Params.Add(new MTD.Param { Name = name, Value = 0 });
        }

        private static void SetParam(MTD mtd, string name, object value)
        {
            var prm = mtd.Params.FirstOrDefault(p => p.Name == name);
            if (prm != null) prm.Value = value;
        }

        private static void SetParamIfExists(MTD mtd, string name, object value)
        {
            var prm = mtd.Params.FirstOrDefault(p => p.Name == name);
            if (prm != null) prm.Value = value;
        }
    }
}
