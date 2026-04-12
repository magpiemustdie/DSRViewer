using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSRViewer.Core.Transfer
{
    /// <summary>
    /// Сравнивает два списка текстур (формат: "texName; virtualPath").
    /// Находит: отсутствующие, совпадающие по имени архива но с разным путём,
    /// совпадающие по имени текстуры но с разным архивом.
    /// </summary>
    public static class TextureListComparer
    {
        public record TextureEntry(string Name, string ArchiveFile, string VirtualPath);

        public record CompareResult(
            List<TextureEntry> OnlyInA,          // есть в A, нет в B
            List<TextureEntry> OnlyInB,          // есть в B, нет в A
            List<(TextureEntry A, TextureEntry B)> SameNameDiffArchive,   // имя совпадает, архив разный
            List<(TextureEntry A, TextureEntry B)> SameArchiveDiffPath    // архив совпадает, путь разный
        );

        /// <summary>Сравнивает два файла списков текстур и возвращает результат сравнения.</summary>
        public static CompareResult Compare(string pathA, string pathB)
        {
            var listA = ParseFile(pathA);
            var listB = ParseFile(pathB);

            var byNameA = listA.ToLookup(e => e.Name, StringComparer.OrdinalIgnoreCase);
            var byNameB = listB.ToLookup(e => e.Name, StringComparer.OrdinalIgnoreCase);

            var namesA = new HashSet<string>(listA.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);
            var namesB = new HashSet<string>(listB.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);

            // Только в A
            var onlyInA = listA.Where(e => !namesB.Contains(e.Name)).ToList();

            // Только в B
            var onlyInB = listB.Where(e => !namesA.Contains(e.Name)).ToList();

            // Имя совпадает, но архив разный
            var sameNameDiffArchive = new List<(TextureEntry, TextureEntry)>();
            // Архив совпадает, но путь разный
            var sameArchiveDiffPath = new List<(TextureEntry, TextureEntry)>();

            foreach (var name in namesA.Intersect(namesB, StringComparer.OrdinalIgnoreCase))
            {
                var entriesA = byNameA[name].ToList();
                var entriesB = byNameB[name].ToList();

                foreach (var a in entriesA)
                {
                    foreach (var b in entriesB)
                    {
                        bool sameArchive = string.Equals(a.ArchiveFile, b.ArchiveFile, StringComparison.OrdinalIgnoreCase);
                        bool samePath    = string.Equals(a.VirtualPath, b.VirtualPath, StringComparison.OrdinalIgnoreCase);

                        if (!sameArchive)
                            sameNameDiffArchive.Add((a, b));
                        else if (!samePath)
                            sameArchiveDiffPath.Add((a, b));
                    }
                }
            }

            return new CompareResult(onlyInA, onlyInB, sameNameDiffArchive, sameArchiveDiffPath);
        }

        /// <summary>Записывает отчёт о сравнении списков текстур в текстовый файл.</summary>
        public static void WriteReport(CompareResult result, string outputPath)
        {
            using var w = new StreamWriter(outputPath, false, Encoding.UTF8);

            w.WriteLine($"=== Texture List Comparison Report ===");
            w.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            w.WriteLine();

            WriteSection(w, "ONLY IN A (missing from B)", result.OnlyInA,
                e => $"  {e.Name}  |  {e.VirtualPath}");

            WriteSection(w, "ONLY IN B (missing from A)", result.OnlyInB,
                e => $"  {e.Name}  |  {e.VirtualPath}");

            w.WriteLine($"--- SAME NAME, DIFFERENT ARCHIVE ({result.SameNameDiffArchive.Count}) ---");
            foreach (var (a, b) in result.SameNameDiffArchive)
            {
                w.WriteLine($"  Name:  {a.Name}");
                w.WriteLine($"    A: {a.ArchiveFile}");
                w.WriteLine($"    B: {b.ArchiveFile}");
            }
            w.WriteLine();

            w.WriteLine($"--- SAME ARCHIVE, DIFFERENT INTERNAL PATH ({result.SameArchiveDiffPath.Count}) ---");
            foreach (var (a, b) in result.SameArchiveDiffPath)
            {
                w.WriteLine($"  Name:  {a.Name}  |  Archive: {a.ArchiveFile}");
                w.WriteLine($"    A path: {a.VirtualPath}");
                w.WriteLine($"    B path: {b.VirtualPath}");
            }
            w.WriteLine();

            w.WriteLine("=== SUMMARY ===");
            w.WriteLine($"  Only in A:              {result.OnlyInA.Count}");
            w.WriteLine($"  Only in B:              {result.OnlyInB.Count}");
            w.WriteLine($"  Same name, diff archive: {result.SameNameDiffArchive.Count}");
            w.WriteLine($"  Same archive, diff path: {result.SameArchiveDiffPath.Count}");

            Console.WriteLine($"Report saved to: {outputPath}");
        }

        private static void WriteSection(StreamWriter w, string title,
            IReadOnlyList<TextureEntry> entries, Func<TextureEntry, string> format)
        {
            w.WriteLine($"--- {title} ({entries.Count}) ---");
            foreach (var e in entries)
                w.WriteLine(format(e));
            w.WriteLine();
        }

        private static List<TextureEntry> ParseFile(string path)
        {
            var result = new List<TextureEntry>();
            if (!File.Exists(path))
            {
                Console.WriteLine($"[TextureListComparer] File not found: {path}");
                return result;
            }

            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                int sep = line.IndexOf(';');
                if (sep < 0) continue;

                string name        = line[..sep].Trim();
                string virtualPath = line[(sep + 1)..].Trim();

                // Имя архива — часть пути до первого '|'
                int pipe = virtualPath.IndexOf('|');
                string archiveFile = pipe >= 0
                    ? Path.GetFileName(virtualPath[..pipe])
                    : Path.GetFileName(virtualPath);

                result.Add(new TextureEntry(name, archiveFile, virtualPath));
            }

            return result;
        }
    }
}
