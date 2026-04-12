using DSRViewer.FileProcess;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSRViewer.Core.Transfer
{
    /// <summary>
    /// Автоматически переносит текстуры из источника (PTDE) в цель (DSR).
    /// Совпадение ищется по убыванию приоритета:
    ///   1. Точное имя + тот же тип архива (chr/map/parts/obj/sfx)
    ///   2. Точное имя, любой архив (выбирается ближайший по пути)
    ///   3. Нечёткое: та же база + тот же UV-сет + тот же тип суффикса (_n/_s/_h)
    ///      Нельзя заменять нормаль диффузом или спекуляром.
    /// </summary>
    public static class TextureAutoTransfer
    {
        public enum MatchKind { Exact, ExactAnyArchive, FuzzyObject, None }

        public record TransferEntry(
            string TargetVirtualPath,   // куда переносим (DSR)
            string SourceVirtualPath,   // откуда берём (PTDE)
            string TextureName,
            MatchKind Match
        );

        public record TransferResult(
            List<TransferEntry> Transferred,
            List<TransferEntry> Skipped,    // найдено но не перенесено (dry run)
            List<string> NotFound
        );

        // ── Публичный API ────────────────────────────────────────────────

        /// <summary>Строит план переноса текстур и при dryRun=false выполняет его через FileBinders.</summary>
        public static TransferResult Run(
            Dictionary<string, List<string>> sourceIndex,
            Dictionary<string, List<string>> targetIndex,
            bool dryRun = false,
            Action<string> onProgress = null)
        {
            var skipped  = new List<TransferEntry>();
            var notFound = new List<string>();
            var toTransfer = new List<TransferEntry>();

            foreach (var (texName, targetPaths) in targetIndex)
            {
                foreach (var targetPath in targetPaths)
                {
                    var (sourcePath, kind) = FindBestMatch(texName, targetPath, sourceIndex);

                    if (kind == MatchKind.None) { notFound.Add(texName); continue; }

                    var entry = new TransferEntry(targetPath, sourcePath, texName, kind);
                    if (dryRun) skipped.Add(entry);
                    else        toTransfer.Add(entry);
                }
            }

            if (dryRun)
                return new TransferResult([], skipped, notFound.Distinct(StringComparer.OrdinalIgnoreCase).ToList());

            var (ok, failed) = TransferBatch(toTransfer);
            foreach (var e in ok) onProgress?.Invoke($"[{e.Match}] {e.TextureName}");
            notFound.AddRange(failed);

            return new TransferResult(ok, skipped, notFound.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        /// <summary>Переносит текстуры на основе FLVER-индексов, сопоставляя по архиву и слоту материала.</summary>
        /// <param name="sourceTexIndex">
        /// Опциональный TexturesList-индекс источника (имя → vpath).
        /// Используется как fallback когда FLVER-индекс не содержит нужной текстуры
        /// (например DSR добавил новый слот материала которого не было в PTDE).
        /// </param>
        public static TransferResult RunWithFlverIndex(
            Dictionary<(string archive, string param), List<FlverTextureScanner.FlverTexEntry>> sourceFlverIndex,
            Dictionary<(string archive, string param), List<FlverTextureScanner.FlverTexEntry>> targetFlverIndex,
            bool dryRun = false,
            Action<string> onProgress = null,
            Dictionary<string, List<string>> sourceTexIndex = null)
        {
            var skipped    = new List<TransferEntry>();
            var notFound   = new List<string>();
            var toTransfer = new List<TransferEntry>();

            foreach (var (key, targetEntries) in targetFlverIndex)
            {
                string archiveName = key.archive;
                string paramName   = key.param;

                foreach (var target in targetEntries)
                {
                    if (string.IsNullOrEmpty(target.VirtualPath))
                    { notFound.Add($"{archiveName}/{paramName}/{target.TextureName}"); continue; }

                    // Пробуем найти через FLVER-индекс
                    var sourceEntry = FindSourceByParam(archiveName, paramName,
                        target.ArchiveType, sourceFlverIndex);

                    TransferEntry entry;

                    if (sourceEntry != null && !string.IsNullOrEmpty(sourceEntry.VirtualPath))
                    {
                        string srcBase = StripArchiveSuffixes(sourceEntry.ArchiveBaseName);
                        string dstBase = StripArchiveSuffixes(archiveName);

                        // Проверяем что источник и цель — один и тот же объект.
                        // Если имена архивов совсем разные (например bd_m_body vs bd_m_9530)
                        // — это DSR-exclusive текстура, не переносим.
                        if (ArchiveNameScore(srcBase, dstBase) == 0)
                        {
                            notFound.Add($"{archiveName}/{paramName}/{target.TextureName}");
                            continue;
                        }

                        var kind = srcBase.Equals(dstBase, StringComparison.OrdinalIgnoreCase)
                            ? MatchKind.Exact : MatchKind.FuzzyObject;

                        entry = new TransferEntry(
                            target.VirtualPath, sourceEntry.VirtualPath,
                            $"{target.TextureName} [{paramName}]", kind);
                    }
                    else if (sourceTexIndex != null)
                    {
                        // Fallback: ищем текстуру по имени в TexturesList-индексе
                        var (sourcePath, kind) = FindBestMatch(
                            target.TextureName, target.VirtualPath, sourceTexIndex);

                        if (kind == MatchKind.None)
                        { notFound.Add($"{archiveName}/{paramName}/{target.TextureName}"); continue; }

                        entry = new TransferEntry(
                            target.VirtualPath, sourcePath,
                            $"{target.TextureName} [{paramName}]", kind);
                    }
                    else
                    { notFound.Add($"{archiveName}/{paramName}/{target.TextureName}"); continue; }

                    if (dryRun) skipped.Add(entry);
                    else        toTransfer.Add(entry);
                }
            }

            if (dryRun)
                return new TransferResult([], skipped, notFound.Distinct(StringComparer.OrdinalIgnoreCase).ToList());

            var (ok, failed) = TransferBatch(toTransfer);
            foreach (var e in ok) onProgress?.Invoke($"[{e.Match}] {e.TextureName}");
            notFound.AddRange(failed);

            return new TransferResult(ok, skipped, notFound.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        /// <summary>
        /// Стриппит известные суффиксы вариантов архива: _m, _a, _l, _s, _l_s.
        /// Порядок важен — сначала составные (_l_s), потом одиночные.
        /// Например: am_f_9340_m → am_f_9340, wp_a_1800_l_s → wp_a_1800.
        /// </summary>
        private static string StripArchiveSuffixes(string archiveName)
        {
            // Составные суффиксы сначала
            foreach (var suffix in new[] { "_l_s", "_m", "_a", "_l" })
            {
                if (archiveName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return archiveName[..^suffix.Length];
            }
            return archiveName;
        }

        private static FlverTextureScanner.FlverTexEntry FindSourceByParam(
            string archiveName, string paramName, string archiveType,
            Dictionary<(string, string), List<FlverTextureScanner.FlverTexEntry>> sourceIndex)
        {
            // 1. Точное совпадение: тот же архив + тот же слот
            if (sourceIndex.TryGetValue((archiveName, paramName), out var exact) && exact.Count > 0)
                return exact[0];

            // 2. Стриппим суффиксы варианта архива (_m, _a, _l, _l_s и т.д.)
            //    am_f_9340_m → am_f_9340, wp_a_1800_l_s → wp_a_1800
            string baseName = StripArchiveSuffixes(archiveName);

            if (!baseName.Equals(archiveName, StringComparison.OrdinalIgnoreCase))
            {
                if (sourceIndex.TryGetValue((baseName, paramName), out var baseExact) && baseExact.Count > 0)
                    return baseExact[0];
            }

            // 3. Тот же тип архива + тот же слот — ищем по сходству имени.
            //    Скорим каждого кандидата: чем выше — тем лучше совпадение.
            var candidates = sourceIndex
                .Where(kv => kv.Key.Item2 == paramName)
                .SelectMany(kv => kv.Value)
                .Where(e => e.ArchiveType.Equals(archiveType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 0) return null;

            var scored = candidates
                .Select(e => (entry: e, score: ArchiveNameScore(e.ArchiveBaseName, baseName)))
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .ToList();

            return scored.Count > 0 ? scored[0].entry : null;
        }

        /// <summary>
        /// Возвращает score сходства двух имён архивов (без суффиксов вариантов).
        /// 0 = нет совпадения, >0 = есть, чем больше — тем лучше.
        /// </summary>
        private static int ArchiveNameScore(string sourceName, string targetBaseName)
        {
            string srcBase = StripArchiveSuffixes(sourceName);

            // Точное совпадение после стриппинга суффиксов
            if (srcBase.Equals(targetBaseName, StringComparison.OrdinalIgnoreCase))
                return 1000;

            // Один является точным префиксом другого с разделителем '_'
            // Например: am_f_9340 и am_f_9340_m (уже покрыто выше после стриппинга)
            // Но также: hd_m_9360 и hd_m_9360_a
            if (srcBase.StartsWith(targetBaseName + "_", StringComparison.OrdinalIgnoreCase) ||
                targetBaseName.StartsWith(srcBase + "_", StringComparison.OrdinalIgnoreCase))
                return 500;

            // Совпадение длинного общего префикса (минимум до последнего '_' сегмента)
            // Например: am_f_9340 и am_f_9340_extra → общий префикс am_f_9340
            int commonLen = CommonPrefixLength(srcBase, targetBaseName);
            int minLen = Math.Min(srcBase.Length, targetBaseName.Length);
            if (minLen > 1 && commonLen >= minLen - 1)
                return 100 + commonLen;

            return 0;
        }

        /// <summary>Длина общего префикса двух строк (case-insensitive).</summary>
        private static int CommonPrefixLength(string a, string b)
        {
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
                if (char.ToLowerInvariant(a[i]) != char.ToLowerInvariant(b[i]))
                    return i;
            return len;
        }

        /// <summary>Строит индекс текстур из TexturesList.txt: имя → список виртуальных путей.</summary>
        public static Dictionary<string, List<string>> BuildIndex(string textureListPath)
        {
            var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(textureListPath)) return index;

            foreach (var line in File.ReadLines(textureListPath, Encoding.UTF8))
            {
                int sep = line.IndexOf(';');
                if (sep < 0) continue;
                // Убираем расширение если есть (на случай .dds/.tga в имени)
                string raw  = line[..sep].Trim();
                string name = Path.GetFileNameWithoutExtension(raw).ToLower();
                string vpath = line[(sep + 1)..].Trim();
                if (!index.ContainsKey(name)) index[name] = new List<string>();
                index[name].Add(vpath);
            }

            return index;
        }

        /// <summary>Записывает текстовый отчёт о результатах переноса.</summary>
        public static void WriteReport(TransferResult result, string outputPath)
        {
            using var w = new StreamWriter(outputPath, false, Encoding.UTF8);
            w.WriteLine("=== Auto Texture Transfer Report ===");
            w.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            w.WriteLine();

            w.WriteLine($"--- TRANSFERRED ({result.Transferred.Count}) ---");
            foreach (var e in result.Transferred)
            {
                string dstArchive = Path.GetFileName(e.TargetVirtualPath.Split('|')[0]);
                w.WriteLine($"  [{e.Match,-16}] {e.TextureName}  →  {dstArchive}");
            }
            w.WriteLine();

            w.WriteLine($"--- DRY RUN / SKIPPED ({result.Skipped.Count}) ---");
            foreach (var e in result.Skipped)
            {
                string dstArchive = Path.GetFileName(e.TargetVirtualPath.Split('|')[0]);
                string srcArchive = Path.GetFileName(e.SourceVirtualPath.Split('|')[0]);
                w.WriteLine($"  [{e.Match,-16}] {e.TextureName}  →  {dstArchive}");
                w.WriteLine($"    src: {srcArchive}  ({e.SourceVirtualPath})");
                w.WriteLine($"    dst: {e.TargetVirtualPath}");
            }
            w.WriteLine();

            w.WriteLine($"--- NOT FOUND ({result.NotFound.Count}) ---");
            foreach (var s in result.NotFound)
                w.WriteLine($"  {s}");

            w.WriteLine();
            w.WriteLine("=== SUMMARY ===");
            w.WriteLine($"  Transferred: {result.Transferred.Count}");
            w.WriteLine($"  Skipped:     {result.Skipped.Count}");
            w.WriteLine($"  Not found:   {result.NotFound.Count}");

            // Разбивка NOT_FOUND по суффиксу типа текстуры
            var bySuffix = result.NotFound
                .GroupBy(n => GetTypeSuffix(n) switch { "" => "diffuse", var s => s })
                .OrderByDescending(g => g.Count());
            w.WriteLine();
            w.WriteLine("  Not found by type:");
            foreach (var g in bySuffix)
                w.WriteLine($"    {g.Key,-10} {g.Count()}");

            // Разбивка NOT_FOUND по префиксу архива (chr/map/parts/obj/sfx/etc)
            var byPrefix = result.NotFound
                .GroupBy(n => n.Length >= 3 ? n[..3] : n)
                .OrderByDescending(g => g.Count())
                .Take(10);
            w.WriteLine();
            w.WriteLine("  Not found by prefix (top 10):");
            foreach (var g in byPrefix)
                w.WriteLine($"    {g.Key,-10} {g.Count()}");

            Console.WriteLine($"[AutoTransfer] Report: {outputPath}");
        }

        /// <summary>Записывает результаты переноса в CSV-файл.</summary>
        public static void WriteCsv(TransferResult result, string outputPath)
        {
            using var w = new StreamWriter(outputPath, false, Encoding.UTF8);

            // Заголовок
            w.WriteLine("Status,MatchKind,TextureName,SourcePath,TargetPath");

            // Перенесённые
            foreach (var e in result.Transferred)
                w.WriteLine($"OK,{e.Match},{CsvEscape(e.TextureName)},{CsvEscape(e.SourceVirtualPath)},{CsvEscape(e.TargetVirtualPath)}");

            // Dry run / найдено но не перенесено
            foreach (var e in result.Skipped)
            {
                string status = "DRY_RUN";
                w.WriteLine($"{status},{e.Match},{CsvEscape(e.TextureName)},{CsvEscape(e.SourceVirtualPath)},{CsvEscape(e.TargetVirtualPath)}");
            }

            // Не найдено
            foreach (var s in result.NotFound)
                w.WriteLine($"NOT_FOUND,None,{CsvEscape(s)},,");

            Console.WriteLine($"[AutoTransfer] CSV: {outputPath}");
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            // Если содержит запятую, кавычку или перенос — оборачиваем в кавычки
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        // ── Поиск совпадения ─────────────────────────────────────────────

        private static (string path, MatchKind kind) FindBestMatch(
            string texName, string targetPath,
            Dictionary<string, List<string>> sourceIndex)
        {
            string targetArchiveType = GetArchiveType(targetPath);

            // 1. Точное имя + тот же тип архива
            if (sourceIndex.TryGetValue(texName, out var exactPaths))
            {
                // Предпочитаем: тот же тип папки + максимальное сходство пути
                var sameType = exactPaths
                    .Where(p => GetArchiveType(p).Equals(targetArchiveType, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p => PathSimilarity(p, targetPath))
                    .FirstOrDefault();
                if (sameType != null) return (sameType, MatchKind.Exact);

                // 2. Точное имя, любой архив — выбираем ближайший по пути
                var best = exactPaths.OrderByDescending(p => PathSimilarity(p, targetPath)).First();
                return (best, MatchKind.ExactAnyArchive);
            }

            // 3. Нечёткое: та же модель + тот же UV-сет + тот же тип текстуры
            string texType = GetTypeSuffix(texName);
            string texUv   = GetUvSuffix(texName);
            string texBase = GetBaseName(texName);

            var sameBaseAndSuffix = sourceIndex
                .Where(kv =>
                {
                    string srcType = GetTypeSuffix(kv.Key);
                    string srcUv   = GetUvSuffix(kv.Key);
                    string srcBase = GetBaseName(kv.Key);
                    // База, UV-сет и тип должны совпадать
                    return srcBase.Equals(texBase, StringComparison.OrdinalIgnoreCase)
                        && srcUv.Equals(texUv, StringComparison.OrdinalIgnoreCase)
                        && srcType.Equals(texType, StringComparison.OrdinalIgnoreCase);
                })
                .SelectMany(kv => kv.Value.Select(p => (kv.Key, p)))
                .ToList();

            if (sameBaseAndSuffix.Count > 0)
            {
                // Предпочитаем тот же тип архива, затем ближайший по пути
                var candidates = sameBaseAndSuffix
                    .Where(x => GetArchiveType(x.p).Equals(targetArchiveType, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (candidates.Count == 0) candidates = sameBaseAndSuffix;

                var best = candidates.OrderByDescending(x => PathSimilarity(x.p, targetPath)).First();
                return (best.p, MatchKind.FuzzyObject);
            }

            return ("", MatchKind.None);
        }

        // ── Перенос ──────────────────────────────────────────────────────

        /// <summary>
        /// Переносит список записей, группируя по целевому архиву.
        /// Каждый архив открывается и сохраняется ровно один раз.
        /// </summary>
        private static (List<TransferEntry> ok, List<string> failed) TransferBatch(
            IEnumerable<TransferEntry> entries)
        {
            var ok     = new List<TransferEntry>();
            var failed = new List<string>();

            // Группируем по корневому файлу цели (часть до первого '|')
            var byTargetArchive = entries
                .GroupBy(e => e.TargetVirtualPath.Split('|')[0],
                         StringComparer.OrdinalIgnoreCase);

            foreach (var archiveGroup in byTargetArchive)
            {
                string targetArchive = archiveGroup.Key;

                // Группируем источники по их корневому архиву
                // чтобы каждый исходный архив открывался ровно один раз
                var bySourceArchive = archiveGroup
                    .GroupBy(e => e.SourceVirtualPath.Split('|')[0],
                             StringComparer.OrdinalIgnoreCase);

                var prepared = new List<(TransferEntry entry, byte[] bytes)>();

                foreach (var srcGroup in bySourceArchive)
                {
                    var srcPaths = srcGroup.Select(e => e.SourceVirtualPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

                    var entriesBySourcePath = srcGroup
                        .GroupBy(e => e.SourceVirtualPath, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                    var resolved = new HashSet<TransferEntry>();

                    try
                    {
                        var srcBinder = new FileBinders();
                        srcBinder.ProcessPaths(srcPaths, new FileOperation
                        {
                            UseTexDelegate = true,
                            AdditionalTextureProcessing = (texture, virtualPath) =>
                            {
                                if (!entriesBySourcePath.TryGetValue(virtualPath, out var matchedEntries)) return;

                                byte[] bytes = texture.Bytes;
                                if (bytes == null || bytes.Length == 0) return;

                                foreach (var entry in matchedEntries)
                                {
                                    prepared.Add((entry, bytes));
                                    resolved.Add(entry);
                                }
                            }
                        });

                        // Fallback для записей которые не были текстурами (BinderFile, FLVER и т.д.)
                        foreach (var entry in srcGroup)
                        {
                            if (resolved.Contains(entry)) continue;

                            var fallback = new FileBinders();
                            fallback.ProcessPaths([entry.SourceVirtualPath],
                                new FileOperation { GetObject = true });

                            byte[] bytes = fallback.GetObject() switch
                            {
                                TPF.Texture tex => tex.Bytes,
                                BinderFile  bf  => bf.Bytes,
                                byte[]      b   => b,
                                _               => null
                            };

                            if (bytes == null) { failed.Add(entry.TextureName); continue; }

                            prepared.Add((entry, bytes));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AutoTransfer] Read failed from {srcGroup.Key}: {ex.Message}");
                        foreach (var e in srcGroup) failed.Add(e.TextureName);
                    }
                }

                if (prepared.Count == 0) continue;

                var byTexName = new Dictionary<string, (TransferEntry entry, byte[] bytes)>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var p in prepared)
                {
                    if (!byTexName.TryGetValue(p.entry.TargetVirtualPath, out var existing))
                    {
                        byTexName[p.entry.TargetVirtualPath] = p;
                    }
                    else
                    {
                        int newScore = PathSimilarity(p.entry.SourceVirtualPath, p.entry.TargetVirtualPath);
                        int oldScore = PathSimilarity(existing.entry.SourceVirtualPath, existing.entry.TargetVirtualPath);
                        if (newScore > oldScore)
                            byTexName[p.entry.TargetVirtualPath] = p;
                    }
                }

                var targetPaths = byTexName.Keys.ToArray();

                try
                {
                    var dstBinder = new FileBinders();
                    int delegateFired = 0;
                    var actuallyWritten = new List<(TransferEntry entry, byte[] bytes)>();
                    dstBinder.ProcessPaths(targetPaths, new FileOperation
                    {
                        WriteObject    = true,
                        UseTexDelegate = true,
                        AdditionalTextureProcessing = (texture, virtualPath) =>
                        {
                            delegateFired++;
                            if (byTexName.TryGetValue(virtualPath, out var p))
                            {
                                DSRViewer.Editors.Explorer.DDSHelper.DDSTextureApplier.Apply(texture, p.bytes);
                                actuallyWritten.Add(p);
                            }
                            else
                            {
                                Console.WriteLine($"[AutoTransfer] delegate miss: '{virtualPath}'");
                            }
                        }
                    });
                    Console.WriteLine($"[AutoTransfer] {targetArchive}: fired={delegateFired}, written={actuallyWritten.Count}/{byTexName.Count}");

                    foreach (var p in actuallyWritten)
                        ok.Add(p.entry);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AutoTransfer] Write failed for {targetArchive}: {ex.Message}");
                    foreach (var p in prepared)
                        failed.Add(p.entry.TextureName);
                }
            }

            return (ok, failed);
        }

        // ── Вспомогательные ─────────────────────────────────────────────

        // chr, map, parts, obj, sfx — тип папки/архива
        private static string GetArchiveType(string virtualPath)
        {
            string lower = virtualPath.ToLower();
            foreach (var folder in new[] { "chr", "map", "parts", "obj", "sfx" })
                if (lower.Contains($"\\{folder}\\") || lower.Contains($"/{folder}/"))
                    return folder;
            return "";
        }

        // Возвращает суффикс типа текстуры: _n, _s, _h, _t, _l
        private static string GetTypeSuffix(string name)
        {
            string[] types = ["_n", "_s", "_h", "_t", "_l"];
            foreach (var s in types)
                if (name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                    return s;
            return "";
        }

        // Возвращает суффикс UV-сета: "", "_2" или "_3"
        private static string GetUvSuffix(string name)
        {
            string stripped = name;
            // Сначала убираем суффикс типа если есть
            string type = GetTypeSuffix(name);
            if (type.Length > 0) stripped = name[..^type.Length];
            // Проверяем _3 раньше _2 чтобы не перепутать
            if (stripped.EndsWith("_3", StringComparison.OrdinalIgnoreCase)) return "_3";
            if (stripped.EndsWith("_2", StringComparison.OrdinalIgnoreCase)) return "_2";
            return "";
        }

        // Базовое имя без UV и без типа: c0000_body_2_n → c0000_body
        private static string GetBaseName(string name)
        {
            string type = GetTypeSuffix(name);
            string withoutType = type.Length > 0 ? name[..^type.Length] : name;
            string uv = GetUvSuffix(name);
            return uv.Length > 0 ? withoutType[..^uv.Length] : withoutType;
        }

        private static bool IsDds(byte[] b) =>
            b.Length >= 3 && b[0] == 'D' && b[1] == 'D' && b[2] == 'S';

        /// <summary>
        /// Считает количество совпадающих сегментов пути (папок/имён архивов).
        /// Чем больше — тем ближе источник к цели по структуре.
        /// Например: chr\c2239 vs chr\c2239 → 2, chr\c2060 vs chr\c2239 → 1
        /// </summary>
        private static int PathSimilarity(string sourcePath, string targetPath)
        {
            // Берём только реальный путь до первого '|'
            string src = sourcePath.Split('|')[0].ToLower();
            string dst = targetPath.Split('|')[0].ToLower();

            var srcParts = src.Replace('/', '\\').Split('\\');
            var dstParts = dst.Replace('/', '\\').Split('\\');

            int score = 0;

            // Совпадение имени архива (без расширения) — самый важный критерий
            // Убираем все расширения: BD_A_9560.partsbnd.dcx → BD_A_9560
            string srcArchive = srcParts.Last();
            string dstArchive = dstParts.Last();
            // Убираем расширения итеративно
            while (Path.GetExtension(srcArchive).Length > 0) srcArchive = Path.GetFileNameWithoutExtension(srcArchive);
            while (Path.GetExtension(dstArchive).Length > 0) dstArchive = Path.GetFileNameWithoutExtension(dstArchive);

            if (string.Equals(srcArchive, dstArchive, StringComparison.OrdinalIgnoreCase))
                score += 100;
            // Частичное совпадение: BD_A_9560 vs BD_A_9560_M — один является префиксом другого
            else if (srcArchive.StartsWith(dstArchive, StringComparison.OrdinalIgnoreCase) ||
                     dstArchive.StartsWith(srcArchive, StringComparison.OrdinalIgnoreCase))
                score += 50;

            // Совпадение типа папки (chr/map/parts/obj/sfx)
            foreach (var folder in new[] { "chr", "map", "parts", "obj", "sfx" })
            {
                bool srcHas = srcParts.Any(p => p.Equals(folder, StringComparison.OrdinalIgnoreCase));
                bool dstHas = dstParts.Any(p => p.Equals(folder, StringComparison.OrdinalIgnoreCase));
                if (srcHas && dstHas) score += 10;
            }

            // Совпадение общих сегментов пути (от конца)
            int minLen = Math.Min(srcParts.Length, dstParts.Length);
            for (int i = 0; i < minLen; i++)
                if (string.Equals(srcParts[srcParts.Length - 1 - i],
                                  dstParts[dstParts.Length - 1 - i],
                                  StringComparison.OrdinalIgnoreCase))
                    score++;

            return score;
        }
    }
}
