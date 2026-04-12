using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSRViewer.Core.Transfer
{
    /// <summary>
    /// Анализирует совместимость двух TexturesList.txt файлов
    /// без реального переноса файлов (только логика индексов).
    /// Запуск: TextureAutoTransferAnalyzer.Run(sourcePath, targetPath)
    /// </summary>
    public static class TextureAutoTransferAnalyzer
    {
        public record AnalysisResult(
            int TotalTarget,
            int Exact,
            int ExactAnyArchive,
            int FuzzyObject,
            int NotFound,
            List<string> NotFoundNames,
            List<(string name, string srcPath, string dstPath, TextureAutoTransfer.MatchKind kind)> Matches
        );

        /// <summary>
        /// Прогоняет логику FindBestMatch по всем текстурам из targetList
        /// против sourceList и выводит статистику.
        /// </summary>
        public static AnalysisResult Run(string sourcePath, string targetPath, bool verbose = false)
        {
            Console.WriteLine($"[Analyzer] Source: {Path.GetFileName(sourcePath)}");
            Console.WriteLine($"[Analyzer] Target: {Path.GetFileName(targetPath)}");
            Console.WriteLine();

            var sourceIndex = TextureAutoTransfer.BuildIndex(sourcePath);
            var targetIndex = TextureAutoTransfer.BuildIndex(targetPath);

            Console.WriteLine($"[Analyzer] Source entries: {sourceIndex.Count} unique names");
            Console.WriteLine($"[Analyzer] Target entries: {targetIndex.Count} unique names");
            Console.WriteLine();

            // Dry run — без реального чтения файлов
            var result = TextureAutoTransfer.Run(sourceIndex, targetIndex, dryRun: true);

            // Группируем по MatchKind
            var byKind = result.Skipped
                .GroupBy(e => e.Match)
                .ToDictionary(g => g.Key, g => g.ToList());

            int exact          = byKind.TryGetValue(TextureAutoTransfer.MatchKind.Exact,          out var e1) ? e1.Count : 0;
            int exactAny       = byKind.TryGetValue(TextureAutoTransfer.MatchKind.ExactAnyArchive, out var e2) ? e2.Count : 0;
            int fuzzy          = byKind.TryGetValue(TextureAutoTransfer.MatchKind.FuzzyObject,     out var e3) ? e3.Count : 0;
            int notFound       = result.NotFound.Count;
            int total          = result.Skipped.Count + notFound;

            Console.WriteLine("=== РЕЗУЛЬТАТЫ АНАЛИЗА ===");
            Console.WriteLine($"  Всего целевых слотов : {total}");
            Console.WriteLine($"  Exact (тот же архив) : {exact}  ({Pct(exact, total)})");
            Console.WriteLine($"  ExactAnyArchive      : {exactAny}  ({Pct(exactAny, total)})");
            Console.WriteLine($"  FuzzyObject          : {fuzzy}  ({Pct(fuzzy, total)})");
            Console.WriteLine($"  NOT FOUND            : {notFound}  ({Pct(notFound, total)})");
            Console.WriteLine();

            // Топ-20 не найденных
            Console.WriteLine($"=== NOT FOUND (первые 20 из {notFound}) ===");
            foreach (var name in result.NotFound.Take(20))
                Console.WriteLine($"  {name}");
            if (notFound > 20) Console.WriteLine($"  ... и ещё {notFound - 20}");
            Console.WriteLine();

            // Группировка NotFound по суффиксу — помогает понять паттерн
            var notFoundBySuffix = result.NotFound
                .GroupBy(n => GetSuffix(n))
                .OrderByDescending(g => g.Count())
                .ToList();

            Console.WriteLine("=== NOT FOUND по суффиксу ===");
            foreach (var g in notFoundBySuffix)
                Console.WriteLine($"  [{(g.Key == "" ? "diffuse" : g.Key),6}] : {g.Count()}");
            Console.WriteLine();

            if (verbose)
            {
                Console.WriteLine("=== ExactAnyArchive (разные типы архивов) ===");
                foreach (var entry in result.Skipped.Where(e => e.Match == TextureAutoTransfer.MatchKind.ExactAnyArchive).Take(30))
                    Console.WriteLine($"  {entry.TextureName}\n    src: {entry.SourceVirtualPath}\n    dst: {entry.TargetVirtualPath}");
                Console.WriteLine();

                Console.WriteLine("=== FuzzyObject ===");
                foreach (var entry in result.Skipped.Where(e => e.Match == TextureAutoTransfer.MatchKind.FuzzyObject).Take(30))
                    Console.WriteLine($"  {entry.TextureName}\n    src: {entry.SourceVirtualPath}\n    dst: {entry.TargetVirtualPath}");
            }

            var matches = result.Skipped
                .Select(e => (e.TextureName, e.SourceVirtualPath, e.TargetVirtualPath, e.Match))
                .ToList();

            return new AnalysisResult(total, exact, exactAny, fuzzy, notFound, result.NotFound, matches);
        }

        /// <summary>Сохраняет полный отчёт в CSV рядом с targetPath.</summary>
        public static void SaveCsv(AnalysisResult result, string outputPath)
        {
            using var w = new StreamWriter(outputPath, false, Encoding.UTF8);
            w.WriteLine("Status,MatchKind,TextureName,SourcePath,TargetPath");

            foreach (var (name, src, dst, kind) in result.Matches)
                w.WriteLine($"FOUND,{kind},{Esc(name)},{Esc(src)},{Esc(dst)}");

            foreach (var name in result.NotFoundNames)
                w.WriteLine($"NOT_FOUND,None,{Esc(name)},,");

            Console.WriteLine($"[Analyzer] CSV saved: {outputPath}");
        }

        private static string GetSuffix(string name)
        {
            foreach (var s in new[] { "_n", "_s", "_h", "_t" })
                if (name.EndsWith(s, StringComparison.OrdinalIgnoreCase)) return s;
            return "";
        }

        private static string Pct(int n, int total) =>
            total == 0 ? "0%" : $"{n * 100.0 / total:F1}%";

        private static string Esc(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
                return $"\"{v.Replace("\"", "\"\"")}\"";
            return v;
        }
    }
}
