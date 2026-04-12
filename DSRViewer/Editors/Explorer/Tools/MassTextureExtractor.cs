using DSRViewer.FileProcess;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSRViewer.Editors.Explorer.Tools
{
    /// <summary>
    /// Массовый экстрактор текстур из дерева FileNode.
    ///
    /// Структура папок:
    ///
    ///   outputDir/
    ///     chr/
    ///       c0000.chrbnd.dcx/          ← корневой BND
    ///         [0]c0000.tpf/            ← TPF с индексом 0 в BND
    ///           [0]c0000_body.dds      ← texIdx=0
    ///           [1]c0000_body_n.dds    ← texIdx=1
    ///         [1_7]WP_A.tpf.dcx/       ← TPF через BXF: bndIdx=1, bxfIdx=7
    ///           [0]WP_A.dds            ← texIdx=0
    ///     font/
    ///       english/                   ← подпапка языка
    ///         DSFont24.tpf.dcx/        ← корневой TPF (нет подпапки архива)
    ///           [0]dsfont24_0000.dds   ← texIdx=0
    ///           [1]dsfont24_0001.dds   ← texIdx=1
    ///       japanese/
    ///         DSFont24.tpf.dcx/
    ///           [0]dsfont24_0000.dds
    ///           [23]dsfont24_1000.dds
    ///
    /// Папка всегда несёт все промежуточные индексы через '_', файл — только texIdx.
    ///
    /// Инжектор:
    ///   - "[idx]name.ext/"    → REPLACE/ADD в существующий архив
    ///   - "[i0_i1]name.ext/"  → то же для BXF-вложения
    ///   - "[new]name.ext/"    → CREATE новый архив и добавить в BND
    ///   - "[idx]name.dds"     → REPLACE текстуры по индексу
    ///   - "name.dds"          → ADD новой текстуры в TPF
    /// </summary>
    public static class MassTextureExtractor
    {
        public record ExtractResult(int Extracted, int Failed, List<string> Errors);

        public static ExtractResult Extract(FileNode node, string outputDir, FileNode treeRoot = null)
        {
            int extracted = 0;
            int failed    = 0;
            var errors    = new List<string>();
            var errorsLock = new object();

            var texNodes = node.FindAll(n => n.IsDDS);
            if (texNodes.Count == 0)
            {
                Console.WriteLine("[MassExtract] No DDS nodes found");
                return new ExtractResult(0, 0, errors);
            }

            // Базовая директория для построения относительных путей.
            // Если передан treeRoot — используем его как базу (полный путь от корня игры).
            // Иначе используем родителя узла.
            string rootPath = node.VirtualPath.Split('|')[0];
            string baseDir;
            if (treeRoot != null)
            {
                string treeRootPath = treeRoot.VirtualPath.Split('|')[0];
                // baseDir = родитель treeRoot, чтобы relPath включал имя treeRoot.
                // Например: treeRoot = N:\game\parts → baseDir = N:\game
                // → relPath(parts\AM_A_1000.partsbnd.dcx) вместо AM_A_1000.partsbnd.dcx
                baseDir = Path.GetDirectoryName(treeRootPath) ?? treeRootPath;
            }
            else
            {
                baseDir = Directory.Exists(rootPath)
                    ? rootPath
                    : Path.GetDirectoryName(rootPath) ?? "";
            }

            var parentNames = BuildParentNameMap(node);

            var byArchive = texNodes
                .GroupBy(n => n.VirtualPath.Split('|')[0], StringComparer.OrdinalIgnoreCase)
                .ToList();

            Parallel.ForEach(byArchive, 
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                archiveGroup =>
            {
                string archivePath = archiveGroup.Key;
                var nodes   = archiveGroup.ToList();
                var vpaths  = nodes.Select(n => n.VirtualPath).ToArray();
                var nodeByVPath = nodes.ToDictionary(n => n.VirtualPath, n => n,
                    StringComparer.OrdinalIgnoreCase);

                int localExtracted = 0;
                int localFailed = 0;

                try
                {
                    var binder = new FileBinders();
                    binder.ProcessPaths(vpaths, new FileOperation
                    {
                        UseTexDelegate = true,
                        AdditionalTextureProcessing = (texture, virtualPath) =>
                        {
                            if (!nodeByVPath.TryGetValue(virtualPath, out var texNode)) return;
                            byte[] ddsBytes = texture.Bytes;
                            if (ddsBytes == null || ddsBytes.Length == 0) return;

                            try
                            {
                                parentNames.TryGetValue(virtualPath, out var parentInfo);
                                var (relDir, fileName) = BuildPathComponents(
                                    virtualPath, texNode.Name, parentInfo, baseDir);
                                string dir = Path.Combine(outputDir, relDir);
                                Directory.CreateDirectory(dir);
                                File.WriteAllBytes(Path.Combine(dir, fileName), ddsBytes);
                                localExtracted++;
                            }
                            catch (Exception ex)
                            {
                                lock (errorsLock)
                                {
                                    errors.Add($"{virtualPath}: {ex.Message}");
                                }
                                localFailed++;
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    localFailed += nodes.Count;
                    lock (errorsLock)
                    {
                        errors.Add($"{archivePath}: {ex.Message}");
                    }
                }

                Interlocked.Add(ref extracted, localExtracted);
                Interlocked.Add(ref failed, localFailed);
            });

            Console.WriteLine($"[MassExtract] Done: {extracted} extracted, {failed} failed");
            return new ExtractResult(extracted, failed, errors);
        }

        /// <summary>
        /// Строит (relativeDir, fileName) из виртуального пути.
        ///
        ///   path|texIdx              → dir="folder/archive"                file="[texIdx]name.dds"
        ///   path|i0|texIdx           → dir="folder/archive/[i0]tpf"        file="[texIdx]name.dds"
        ///   path|i0|i1|texIdx        → dir="folder/archive/[i0_i1]tpf"     file="[texIdx]name.dds"
        ///
        /// Папка несёт все промежуточные индексы через '_', файл — только последний (texIdx).
        /// parentInfo = (intermediateIndices, tpfName).
        /// </summary>
        public static (string relDir, string fileName) BuildPathComponents(
            string virtualPath, string texNodeName,
            (int[] intermediateIndices, string tpfName)? parentInfo = null,
            string baseDir = null)
        {
            var parts    = virtualPath.Split('|');
            string filePath = parts[0];
            var indices  = parts[1..];

            // Строим относительный путь архива от baseDir (папка игры или папка архива)
            string archiveFolder;
            if (!string.IsNullOrEmpty(baseDir))
            {
                try { archiveFolder = Path.GetRelativePath(baseDir, filePath).Replace('/', '\\'); }
                catch
                {
                    var p = filePath.Replace('/', '\\').Split('\\');
                    archiveFolder = string.Join("\\", p[^Math.Min(2, p.Length)..]);
                }
            }
            else
            {
                var p = filePath.Replace('/', '\\').Split('\\');
                archiveFolder = string.Join("\\", p[^Math.Min(2, p.Length)..]);
            }

            string baseName = texNodeName.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(texNodeName)
                : texNodeName;

            if (indices.Length == 1)
            {
                // Корневой TPF — нет подпапки
                return (archiveFolder, $"[{indices[0]}]{baseName}.dds");
            }

            // Промежуточные индексы = все кроме последнего (texIdx)
            // Папка: "[i0_i1_...]tpfName"
            string subFolderName;
            if (parentInfo.HasValue)
            {
                string idxPrefix = string.Join("_", parentInfo.Value.intermediateIndices);
                subFolderName = $"[{idxPrefix}]{EscapeName(parentInfo.Value.tpfName)}";
            }
            else
            {
                // Fallback: берём промежуточные из vpath
                string idxPrefix = string.Join("_", indices[..^1]);
                subFolderName = $"[{idxPrefix}]";
            }

            string relDir = Path.Combine(archiveFolder, subFolderName);
            // Файл: только texIdx (последний индекс)
            string texIdx = indices[^1];
            return (relDir, $"[{texIdx}]{baseName}.dds");
        }

        // ── Вспомогательные ─────────────────────────────────────────────

        /// <summary>Заменяет слеши в имени файла на § для безопасного использования как имя папки.</summary>
        public static string EscapeName(string name) =>
            name.Replace('\\', '§').Replace('/', '§');

        /// <summary>Восстанавливает оригинальное имя файла из экранированного.</summary>
        public static string UnescapeName(string name) =>
            name.Replace('§', '\\');

        /// <summary>
        /// Строит карту: VirtualPath текстуры → (intermediateIndices, tpfName).
        /// intermediateIndices — все индексы пути до TPF (всё кроме texIdx).
        /// tpfName — реальное имя TPF-файла.
        /// </summary>
        private static Dictionary<string, (int[] intermediateIndices, string tpfName)> BuildParentNameMap(FileNode root)
        {
            var map = new Dictionary<string, (int[], string)>(StringComparer.OrdinalIgnoreCase);

            void Traverse(FileNode parent, FileNode current)
            {
                if (current.IsNestedDDS && parent != null &&
                    (parent.IsNestedTpfArchive || parent.IsTpfArchive))
                {
                    // VirtualPath родителя: "path|i0|i1|..." — берём все индексы
                    var parentParts = parent.VirtualPath.Split('|');
                    if (parentParts.Length >= 2)
                    {
                        var intermediateIndices = new List<int>();
                        bool allOk = true;
                        foreach (var p in parentParts[1..])
                            if (int.TryParse(p, out int n)) intermediateIndices.Add(n);
                            else { allOk = false; break; }

                        if (allOk)
                        {
                            string tpfName = Path.GetFileName(parent.Name.Replace('/', '\\'));
                            map[current.VirtualPath] = (intermediateIndices.ToArray(), tpfName);
                        }
                    }
                }
                current.EnsureLoaded();
                foreach (var child in current.Children)
                    Traverse(current, child);
            }

            Traverse(null, root);
            return map;
        }
    }
}

