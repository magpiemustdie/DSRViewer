using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSRViewer.Editors.MTDEditor
{
    /// <summary>
    /// Читает MTD-файлы из BND3-архивов.
    /// Поддерживает все варианты структуры папки mtd:
    ///   DSR:  Mtd.mtdbnd.dcx + MtdPatch.mtdbnd.dcx
    ///   PTDE: Mtd.mtdbnd     + mtd.mtdbnd.dcx
    /// Оба файла читаются и мержатся, Patch-файл имеет приоритет (перезаписывает дубли).
    /// </summary>
    public class MTDReader
    {
        // ── Публичный API ────────────────────────────────────────────────

        /// <summary>
        /// Находит все mtdbnd-файлы в папке, читает их и возвращает объединённый список MTD.
        /// Patch-файлы имеют приоритет над основными.
        /// </summary>
        public List<MTDShortDetails> MTDViewer(string folderOrFilePath)
        {
            var files = ResolveMtdbndFiles(folderOrFilePath);
            if (files.Count == 0)
            {
                Console.WriteLine($"[MTDReader] No mtdbnd files found in: {folderOrFilePath}");
                return [];
            }

            // Читаем все файлы, Patch перезаписывает дубли
            var result = new Dictionary<string, MTDShortDetails>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                try
                {
                    var bnd = ReadBnd(file);
                    foreach (var entry in bnd.Files)
                    {
                        try
                        {
                            var mtd = MTD.Read(entry.Bytes);
                            var details = BuildDetails(entry.Name, mtd);
                            result[entry.Name] = details; // Patch перезаписывает
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[MTDReader] Cannot read MTD '{entry.Name}': {ex.Message}");
                        }
                    }
                    Console.WriteLine($"[MTDReader] Loaded {bnd.Files.Count} entries from {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MTDReader] Cannot read BND '{file}': {ex.Message}");
                }
            }

            return result.Values.OrderBy(m => m.Name).ToList();
        }

        /// <summary>Загружает конкретный MTD по имени из папки (ищет во всех mtdbnd-файлах).</summary>
        public MTD LoadMTDByName(string folderOrFilePath, string name)
        {
            var files = ResolveMtdbndFiles(folderOrFilePath);

            // Ищем в обратном порядке — Patch-файл имеет приоритет
            foreach (var file in Enumerable.Reverse(files))
            {
                try
                {
                    var bnd = ReadBnd(file);
                    var entry = bnd.Files.FirstOrDefault(f =>
                        f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (entry != null)
                        return MTD.Read(entry.Bytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MTDReader] Cannot read BND '{file}': {ex.Message}");
                }
            }

            Console.WriteLine($"[MTDReader] MTD not found: {name}");
            return new MTD();
        }

        /// <summary>
        /// Возвращает путь к основному mtdbnd-файлу (не Patch) для записи.
        /// Предпочитает DCX-версию.
        /// </summary>
        public static string ResolveMainBndPath(string folderOrFilePath)
        {
            if (File.Exists(folderOrFilePath)) return folderOrFilePath;
            if (!Directory.Exists(folderOrFilePath)) return "";

            // Предпочитаем DCX, потом без компрессии, исключаем Patch
            var candidates = Directory.GetFiles(folderOrFilePath, "*.mtdbnd*")
                .Where(f => !Path.GetFileName(f).Contains("Patch", StringComparison.OrdinalIgnoreCase)
                         && !Path.GetFileName(f).Contains("patch", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.EndsWith(".dcx", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return candidates.FirstOrDefault() ?? "";
        }

        /// <summary>
        /// Возвращает путь к Patch-файлу если он есть.
        /// </summary>
        public static string ResolvePatchBndPath(string folderOrFilePath)
        {
            if (!Directory.Exists(folderOrFilePath)) return "";

            return Directory.GetFiles(folderOrFilePath, "*.mtdbnd*")
                .FirstOrDefault(f =>
                    Path.GetFileName(f).Contains("Patch", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(f).Contains("patch", StringComparison.OrdinalIgnoreCase))
                ?? "";
        }

        // ── Внутренние методы ────────────────────────────────────────────

        /// <summary>
        /// Возвращает список mtdbnd-файлов в папке в порядке загрузки:
        /// сначала основной, потом Patch (чтобы Patch перезаписал дубли).
        /// </summary>
        public static List<string> ResolveMtdbndFiles(string folderOrFilePath)
        {
            // Если передан конкретный файл — возвращаем его
            if (File.Exists(folderOrFilePath))
                return [folderOrFilePath];

            if (!Directory.Exists(folderOrFilePath))
                return [];

            var all = Directory.GetFiles(folderOrFilePath, "*.mtdbnd*")
                .Concat(Directory.GetFiles(folderOrFilePath, "*.mtdbnd"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Сортируем: основные файлы первыми, Patch последними
            return all
                .OrderBy(f =>
                    Path.GetFileName(f).Contains("Patch", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(f).Contains("patch", StringComparison.OrdinalIgnoreCase)
                    ? 1 : 0)
                .ToList();
        }

        /// <summary>Читает BND3 из файла (с DCX или без).</summary>
        public static BND3 ReadBnd(string path) => BND3.Read(path);

        private static MTDShortDetails BuildDetails(string name, MTD mtd)
        {
            int mw = 0;
            foreach (var prm in mtd.Params)
                if (prm.Name == "g_MaterialWorkflow")
                    mw = (int)prm.Value;

            return new MTDShortDetails
            {
                Name    = name,
                MW      = mw,
                TexType = mtd.Textures.Select(t => t.Type).ToList()
            };
        }
    }
}
