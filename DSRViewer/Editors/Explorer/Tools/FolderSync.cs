using DSRViewer.FileProcess;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSRViewer.Editors.Explorer.Tools
{
    /// <summary>
    /// Синхронизирует папку экстракции с деревом архивов.
    ///
    /// Compare — показывает что изменилось.
    /// Apply   — применяет изменения к архивам:
    ///   - Файл удалён с диска → RemoveObject текстуры из TPF
    ///   - Файл переименован   → RenameObject текстуры в TPF
    ///   - Папка [idx] удалена → RemoveObject вложенного архива из BND
    /// </summary>
    public static class FolderSync
    {
        public enum FileStatus { Unchanged, Modified, New, Deleted, Renamed }

        public record SyncEntry(
            string RelativePath,
            FileStatus Status,
            string VirtualPath,       // null для New
            string RenamedFromPath = null,  // для Renamed: старый VirtualPath
            string RenamedFromName = null   // для Renamed: старое имя текстуры
        );

        public record SyncResult(
            List<SyncEntry> Entries,
            int ModifiedCount,
            int NewCount,
            int DeletedCount,
            int UnchangedCount,
            int RenamedCount
        );

        public record ApplyResult(
            int Removed,
            int Renamed,
            int Injected,
            int Failed,
            List<string> Errors,
            List<string> ModifiedArchives
        );

        // ── Compare ──────────────────────────────────────────────────────

        /// <summary>Возвращает ключ для archiveIndex — относительный путь от baseDir.</summary>
        private static string GetArchiveKey(string virtualPath, string baseDir)
        {
            string normalized = virtualPath.Replace('/', '\\');
            string rootNormalized = baseDir.Replace('/', '\\');
            try
            {
                return Path.GetRelativePath(rootNormalized, normalized);
            }
            catch
            {
                var segments = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                return string.Join("\\", segments[^Math.Min(2, segments.Length)..]);
            }
        }

        public static SyncResult Compare(FileNode treeRoot, FileNode selectedNode, string folder)
        {
            var entries = new List<SyncEntry>();

            if (!Directory.Exists(folder))
                return new SyncResult(entries, 0, 0, 0, 0, 0);

            // ── Шаг 1: effectiveNode ─────────────────────────────────────
            // Файл (DDS/FLVER) → поднимаемся к родительскому архиву/TPF.
            // Архив/папка → используем напрямую.
            FileNode effectiveNode = selectedNode;

            bool isFile = !selectedNode.IsFolder
                && !selectedNode.IsBndArchive
                && !selectedNode.IsTpfArchive
                && !selectedNode.IsBxfArchive;

            if (isFile)
            {
                if (!selectedNode.VirtualPath.Contains('|'))
                    return new SyncResult(entries, 0, 0, 0, 0, 0); // файл вне архива

                string parentVPath = selectedNode.VirtualPath[..selectedNode.VirtualPath.LastIndexOf('|')];
                effectiveNode = treeRoot.FindFirst(n =>
                    n.VirtualPath.Equals(parentVPath, StringComparison.OrdinalIgnoreCase))
                    ?? selectedNode;
            }

            // ── Шаг 2: scanFolder + vpathPrefix ─────────────────────────
            // Extract — зеркало дерева. Структура папок в Extract совпадает
            // с относительными путями узлов от treeRoot.
            // Для любого узла его папка в Extract = folder + relPath(treeRoot → node).
            //
            // scanFolder: папка в Extract которую сканируем (ограничиваем для производительности)
            // vpathPrefix: фильтр узлов дерева (null = все)
            string scanFolder = folder;
            string vpathPrefix = null;
            string effectiveVPath = effectiveNode.VirtualPath.Split('|')[0];

            // Корень — это именно treeRoot (тот узел с которого построено дерево)
            bool isRoot = effectiveNode == treeRoot
                || effectiveNode.VirtualPath.Equals(treeRoot.VirtualPath, StringComparison.OrdinalIgnoreCase);

            if (!isRoot)
            {
                vpathPrefix = effectiveNode.VirtualPath; // включая '|' для вложенных TPF

                // Вычисляем относительный путь узла от treeRoot
                string treeRootPath = treeRoot.VirtualPath.Split('|')[0];
                string relFromRoot;
                try { relFromRoot = Path.GetRelativePath(treeRootPath, effectiveVPath).Replace('/', '\\'); }
                catch { relFromRoot = Path.GetFileName(effectiveVPath); }

                if (effectiveNode.IsFolder)
                {
                    // Папка: ищем в Extract по относительному пути, затем рекурсивно по имени
                    string candidate = Path.Combine(folder, relFromRoot);
                    if (Directory.Exists(candidate))
                        scanFolder = candidate;
                    else
                    {
                        string folderName = Path.GetFileName(effectiveVPath);
                        string direct = Path.Combine(folder, folderName);
                        if (Directory.Exists(direct))
                            scanFolder = direct;
                        else
                        {
                            var found = Directory.GetDirectories(folder, folderName, SearchOption.AllDirectories);
                            if (found.Length > 0) scanFolder = found[0];
                        }
                    }
                }
                else
                {
                    // Архив или вложенный TPF: ищем папку архива в Extract
                    // Сначала по относительному пути (точное совпадение)
                    string candidate = Path.Combine(folder, relFromRoot);
                    if (Directory.Exists(candidate))
                    {
                        string archiveDir = candidate;
                        var vpathParts = effectiveNode.VirtualPath.Split('|');
                        if (vpathParts.Length > 1)
                        {
                            string indexPrefix = $"[{vpathParts[1]}]";
                            var subDirs = Directory.GetDirectories(archiveDir)
                                .Where(d => Path.GetFileName(d).StartsWith(indexPrefix, StringComparison.OrdinalIgnoreCase))
                                .ToArray();
                            scanFolder = subDirs.Length > 0 ? subDirs[0] : archiveDir;
                        }
                        else
                        {
                            scanFolder = archiveDir;
                        }
                    }
                    else
                    {
                        // Fallback: ищем по имени архива рекурсивно
                        string archiveName = Path.GetFileName(effectiveVPath);
                        var archiveDirs = Directory.GetDirectories(folder, archiveName, SearchOption.AllDirectories);
                        if (archiveDirs.Length == 0)
                            return new SyncResult(entries, 0, 0, 0, 0, 0);

                        string archiveDir = archiveDirs[0];
                        var vpathParts = effectiveNode.VirtualPath.Split('|');
                        if (vpathParts.Length > 1)
                        {
                            string indexPrefix = $"[{vpathParts[1]}]";
                            var subDirs = Directory.GetDirectories(archiveDir)
                                .Where(d => Path.GetFileName(d).StartsWith(indexPrefix, StringComparison.OrdinalIgnoreCase))
                                .ToArray();
                            scanFolder = subDirs.Length > 0 ? subDirs[0] : archiveDir;
                        }
                        else
                        {
                            scanFolder = archiveDir;
                        }
                    }
                }
            }
            else
            {
                // isRoot: treeRoot может быть любой папкой (map, chr, DarkSoulsRemastered).
                // Ищем в Extract папку соответствующую treeRoot.
                // Стратегия: ищем папку с именем treeRoot в Extract рекурсивно.
                // Если не нашли — сканируем весь folder.
                string rootName = Path.GetFileName(effectiveVPath);
                string direct = Path.Combine(folder, rootName);
                if (Directory.Exists(direct))
                    scanFolder = direct;
                else
                {
                    var found = Directory.GetDirectories(folder, rootName, SearchOption.AllDirectories);
                    if (found.Length > 0) scanFolder = found[0];
                    // иначе scanFolder = folder (весь Extract)
                }
            }

            // ── Шаг 3: узлы дерева с фильтрацией ────────────────────────
            var allDdsNodes = treeRoot.FindAll(n => n.IsNestedDDS || n.IsDDS);
            var allArchiveNodes = treeRoot.FindAll(n => n.IsBndArchive || n.IsTpfArchive || n.IsBxfArchive);

            if (vpathPrefix != null)
            {
                allDdsNodes = allDdsNodes.Where(n =>
                    n.VirtualPath.StartsWith(vpathPrefix + "|", StringComparison.OrdinalIgnoreCase) ||
                    n.VirtualPath.StartsWith(vpathPrefix + "\\", StringComparison.OrdinalIgnoreCase) ||
                    n.VirtualPath.StartsWith(vpathPrefix + "/", StringComparison.OrdinalIgnoreCase)
                ).ToList();

                allArchiveNodes = allArchiveNodes.Where(n =>
                    n.VirtualPath.StartsWith(vpathPrefix + "|", StringComparison.OrdinalIgnoreCase) ||
                    n.VirtualPath.StartsWith(vpathPrefix + "\\", StringComparison.OrdinalIgnoreCase) ||
                    n.VirtualPath.StartsWith(vpathPrefix + "/", StringComparison.OrdinalIgnoreCase) ||
                    n.VirtualPath.Equals(vpathPrefix, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            // Нет DDS узлов в этой ветке — нечего синхронизировать
            if (allDdsNodes.Count == 0)
                return new SyncResult(entries, 0, 0, 0, 0, 0);
            // ── Шаг 4: индексы ───────────────────────────────────────────
            var treeByVPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var treeByName  = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in allDdsNodes)
            {
                treeByVPath[node.VirtualPath] = node.Name;
                string nameKey = StripExt(node.Name);
                if (!treeByName.TryGetValue(nameKey, out var list))
                    treeByName[nameKey] = list = new List<string>();
                list.Add(node.VirtualPath);
            }

            // baseDir — папка относительно которой строятся ключи archiveIndex.
            // Должна совпадать с baseDir который использовал MassTextureExtractor при Extract:
            // baseDir должен совпадать с тем что использует MassTextureExtractor:
            // всегда = родитель treeRoot, чтобы relPath включал имя treeRoot.
            string treeRootVPath = treeRoot.VirtualPath.Split('|')[0];
            string baseDir = Path.GetDirectoryName(treeRootVPath) ?? treeRootVPath;
            var archiveIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var node in allArchiveNodes)
            {
                if (node.VirtualPath.Contains('|')) continue;
                archiveIndex.TryAdd(GetArchiveKey(node.VirtualPath, baseDir), node.VirtualPath);
            }
            if (!effectiveNode.IsFolder && !effectiveNode.VirtualPath.Contains('|')
                && (effectiveNode.IsBndArchive || effectiveNode.IsTpfArchive || effectiveNode.IsBxfArchive))
            {
                archiveIndex.TryAdd(GetArchiveKey(effectiveNode.VirtualPath, baseDir), effectiveNode.VirtualPath);
            }
            if (!isRoot && effectiveNode.VirtualPath.Contains('|'))
            {
                string rootPath = effectiveNode.VirtualPath.Split('|')[0];
                archiveIndex.TryAdd(GetArchiveKey(rootPath, baseDir), rootPath);
            }

            // ── Шаг 5: матчинг файлов на диске ───────────────────────────
            var folderFiles = Directory.GetFiles(scanFolder, "*.dds", SearchOption.AllDirectories);

            var folderEntries = folderFiles.Select(f =>
            {
                string name     = Path.GetFileNameWithoutExtension(f);
                string rel      = Path.GetRelativePath(folder, f).Replace('/', '\\');
                string stripped = StripLeadingBrackets(name);
                int fileIdx     = ParseBracketIndex(name) >= 0 ? 0 : -1;
                string vpath    = BuildVPathFromFilePath(f, folder, fileIdx, archiveIndex, rel);
                return (path: f, rel, stripped, fileIdx, vpath);
            }).ToList();

            var usedTreePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var fe in folderEntries.OrderByDescending(e => e.fileIdx >= 0))
            {
                // Приоритет 1: точное совпадение по vpath (файл с индексом)
                if (!string.IsNullOrEmpty(fe.vpath) && treeByVPath.TryGetValue(fe.vpath, out string treeName)
                    && usedTreePaths.Add(fe.vpath))
                {
                    bool nameChanged = !string.Equals(fe.stripped, StripExt(treeName), StringComparison.OrdinalIgnoreCase);
                    entries.Add(nameChanged
                        ? new SyncEntry(fe.rel, FileStatus.Renamed, fe.vpath, fe.vpath, StripExt(treeName))
                        : new SyncEntry(fe.rel, FileStatus.Modified, fe.vpath));
                    continue;
                }

                // Приоритет 2: fallback по имени (только файлы без индекса)
                if (fe.fileIdx < 0 && treeByName.TryGetValue(fe.stripped, out var vpaths))
                {
                    string vpath = vpaths.FirstOrDefault(v => usedTreePaths.Add(v));
                    if (vpath != null)
                    {
                        entries.Add(new SyncEntry(fe.rel, FileStatus.Modified, vpath));
                        continue;
                    }
                }

                entries.Add(new SyncEntry(fe.rel, FileStatus.New, null));
            }

            // ── Шаг 6: Deleted + Renamed detection ───────────────────────
            var deletedInTree = allDdsNodes
                .Where(n => !usedTreePaths.Contains(n.VirtualPath))
                .Select(n => (name: StripExt(n.Name), vpath: n.VirtualPath))
                .ToList();

            var newByName = entries
                .Where(e => e.Status == FileStatus.New)
                .Select(e =>
                {
                    string full    = Path.Combine(folder, e.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                    string relFull = Path.GetRelativePath(folder, full).Replace('/', '\\');
                    int idx        = ParseBracketIndex(Path.GetFileNameWithoutExtension(e.RelativePath)) >= 0 ? 0 : -1;
                    string vp      = BuildVPathFromFilePath(full, folder, idx, archiveIndex, relFull);
                    string name    = StripLeadingBrackets(Path.GetFileNameWithoutExtension(e.RelativePath));
                    return (entry: e, vpath: vp, archive: vp?.Split('|')[0], name);
                })
                .Where(x => x.vpath != null)
                .GroupBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var (name, vpath) in deletedInTree)
            {
                string deletedArchive = vpath.Split('|')[0];
                var match = newByName.TryGetValue(name, out var candidates)
                    ? candidates.FirstOrDefault(c => string.Equals(c.archive, deletedArchive, StringComparison.OrdinalIgnoreCase))
                    : default;

                if (match.entry != null)
                {
                    entries.Remove(match.entry);
                    candidates.Remove(match);
                    entries.Add(new SyncEntry(match.entry.RelativePath, FileStatus.Renamed, match.entry.VirtualPath, vpath, name));
                }
                else
                {
                    entries.Add(new SyncEntry(name + ".dds", FileStatus.Deleted, vpath));
                }
            }

            // ── Шаг 7: подсчёт ───────────────────────────────────────────
            int modified = 0, newCount = 0, deleted = 0, unchanged = 0, renamed = 0;
            foreach (var e in entries)
                switch (e.Status)
                {
                    case FileStatus.Modified:  modified++;  break;
                    case FileStatus.New:       newCount++;  break;
                    case FileStatus.Deleted:   deleted++;   break;
                    case FileStatus.Unchanged: unchanged++; break;
                    case FileStatus.Renamed:   renamed++;   break;
                }

            Console.WriteLine($"[Sync] Result: M={modified} N={newCount} D={deleted} R={renamed} folderFiles={folderFiles.Length}");
            return new SyncResult(entries, modified, newCount, deleted, unchanged, renamed);
        }

        private static int ParseBracketIndex(string name)
        {
            if (!name.StartsWith("[")) return -1;
            int close = name.IndexOf(']');
            if (close <= 0) return -1;
            return name[1..close].Split('_').All(p => int.TryParse(p, out _)) ? 0 : -1;
        }

        private static string StripExt(string name) =>
            name.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(name) : name;

        // ── Apply ─────────────────────────────────────────────────────────

        /// <summary>
        /// Применяет изменения из SyncResult к архивам:
        /// - Deleted → RemoveObject
        /// - Renamed → RenameObject (новое имя из RelativePath)
        /// </summary>
        public static ApplyResult Apply(SyncResult sync, string folder, FileNode treeRoot = null)
        {
            int removed  = 0;
            int renamed  = 0;
            int injected = 0;
            int failed   = 0;
            var errors   = new List<string>();
            var modified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ── New → ADD через MassTextureInjector ──────────────────────
            // Только New файлы — не трогаем Modified (они идут через Inject Selected)
            var newFiles = sync.Entries
                .Where(e => e.Status == FileStatus.New)
                .Select(e => Path.Combine(folder, e.RelativePath.Replace('/', Path.DirectorySeparatorChar)))
                .Where(File.Exists)
                .ToList();

            if (newFiles.Count > 0 && treeRoot != null)
            {
                // Строим индекс архивов чтобы заранее отфильтровать файлы из несуществующих архивов.
                // Ищем любой сегмент пути (не только предпоследний) — как это делает ResolveItem.
                var knownArchives = new HashSet<string>(
                    treeRoot.FindAll(n => n.IsBndArchive || n.IsTpfArchive || n.IsBxfArchive)
                        .Where(n => !n.VirtualPath.Contains('|'))
                        .Select(n => Path.GetFileName(n.VirtualPath)),
                    StringComparer.OrdinalIgnoreCase);

                var resolvableFiles = new List<string>();
                foreach (var f in newFiles)
                {
                    string rel = Path.GetRelativePath(folder, f);
                    var parts = rel.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    // Ищем любой сегмент пути (кроме последнего — это сам файл) в knownArchives
                    bool found = parts.Take(parts.Length - 1).Any(p => knownArchives.Contains(p));
                    if (found)
                        resolvableFiles.Add(f);
                    else
                        Console.WriteLine($"[FolderSync] Skipping New file (archive not in tree): {rel}");
                }

                newFiles = resolvableFiles;
            }

            if (newFiles.Count > 0 && treeRoot != null)
            {
                try
                {
                    var injectResult = MassTextureInjector.InjectFiles(treeRoot, folder, newFiles, forceAdd: true);
                    foreach (var p in injectResult.ModifiedArchives) modified.Add(p);
                    foreach (var e in injectResult.Errors) errors.Add($"[New] {e}");
                    injected += injectResult.Added + injectResult.Created;
                    failed   += injectResult.Failed;

                    // Переименовываем файлы на диске: добавляем индекс в имя
                    // Для каждого New-файла без индекса ищем его в дереве по имени и берём индекс
                    if (injectResult.Added > 0)
                        RenameNewFilesAfterInject(sync.Entries, folder, treeRoot, errors);
                }
                catch (Exception ex) { errors.Add($"[New] Inject failed: {ex.Message}"); }
            }

            // bndIdx = -1 для корневого TPF
            var removedByArchive = new Dictionary<string, List<(int bndIdx, int texIdx)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in sync.Entries)
            {
                if (entry.Status == FileStatus.Deleted && entry.VirtualPath != null)
                {
                    try
                    {
                        new FileBinders().ProcessPaths([entry.VirtualPath], new FileOperation
                        {
                            WriteObject  = true,
                            RemoveObject = true
                        });
                        removed++;
                        string archiveRoot = entry.VirtualPath.Split('|')[0];
                        modified.Add(archiveRoot);

                        var parts = entry.VirtualPath.Split('|');
                        if (parts.Length == 2 && int.TryParse(parts[1], out int texIdx0))
                        {
                            // Корневой TPF: archive|texIdx
                            if (!removedByArchive.ContainsKey(archiveRoot))
                                removedByArchive[archiveRoot] = new List<(int, int)>();
                            removedByArchive[archiveRoot].Add((-1, texIdx0));
                        }
                        else if (parts.Length == 3 && int.TryParse(parts[1], out int bndIdx)
                                                    && int.TryParse(parts[2], out int texIdx1))
                        {
                            // Вложенный TPF: archive|bndIdx|texIdx
                            if (!removedByArchive.ContainsKey(archiveRoot))
                                removedByArchive[archiveRoot] = new List<(int, int)>();
                            removedByArchive[archiveRoot].Add((bndIdx, texIdx1));
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"Remove {entry.VirtualPath}: {ex.Message}");
                    }
                }
                else if (entry.Status == FileStatus.Renamed && entry.RenamedFromPath != null)
                {
                    string newName = StripLeadingBrackets(
                        Path.GetFileNameWithoutExtension(entry.RelativePath));

                    try
                    {
                        new FileBinders().ProcessPaths([entry.RenamedFromPath], new FileOperation
                        {
                            WriteObject   = true,
                            RenameObject  = true,
                            NewObjectName = newName
                        });
                        renamed++;
                        modified.Add(entry.RenamedFromPath.Split('|')[0]);
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"Rename {entry.RenamedFromPath} → {newName}: {ex.Message}");
                    }
                }
            }

            // После удаления текстур — обновляем индексы файлов на диске
            // Файлы с индексом > удалённого сдвигаются на -1
            foreach (var (archiveRoot, removedList) in removedByArchive)
            {
                try
                {
                    string archiveName = Path.GetFileName(archiveRoot);
                    var archiveDirs = Directory.GetDirectories(folder, "*", SearchOption.AllDirectories)
                        .Where(d => Path.GetFileName(d).Equals(archiveName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var archiveDir in archiveDirs)
                    {
                        // Группируем по bndIdx
                        var byBnd = removedList.GroupBy(x => x.bndIdx);
                        foreach (var grp in byBnd)
                        {
                            var texIndices = grp.Select(x => x.texIdx).ToList();
                            if (grp.Key < 0)
                            {
                                // Корневой TPF — файлы прямо в папке архива
                                UpdateDdsIndicesAfterRemoval(archiveDir, texIndices, errors);
                            }
                            else
                            {
                                // Вложенный TPF — ищем подпапку с нужным bndIdx
                                foreach (var tpfDir in Directory.GetDirectories(archiveDir))
                                {
                                    string dirName = Path.GetFileName(tpfDir);
                                    if (!dirName.StartsWith("[")) continue;
                                    int close = dirName.IndexOf(']');
                                    if (close < 0) continue;
                                    if (int.TryParse(dirName[1..close], out int dirBndIdx) && dirBndIdx == grp.Key)
                                        UpdateDdsIndicesAfterRemoval(tpfDir, texIndices, errors);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Index update failed for {archiveRoot}: {ex.Message}");
                }
            }

            Console.WriteLine($"[FolderSync] removed={removed}, renamed={renamed}, injected={injected}, failed={failed}");
            return new ApplyResult(removed, renamed, injected, failed, errors, modified.ToList());
        }

        /// <summary>
        /// Переименовывает DDS-файлы в папке после удаления текстур:
        /// файлы с индексом > удалённого сдвигаются на -1.
        /// </summary>
        private static void UpdateDdsIndicesAfterRemoval(string dir, List<int> removedIndices, List<string> errors)
        {
            if (!Directory.Exists(dir)) return;
            var ddsFiles = Directory.GetFiles(dir, "*.dds");

            // Парсим текущие индексы
            var parsed = new List<(string path, int idx, string baseName)>();
            foreach (var f in ddsFiles)
            {
                string name = Path.GetFileNameWithoutExtension(f);
                if (!name.StartsWith("[")) continue;
                int close = name.IndexOf(']');
                if (close < 0) continue;
                if (!int.TryParse(name[1..close], out int idx)) continue;
                string baseName = name[(close + 1)..];
                parsed.Add((f, idx, baseName));
            }

            // Для каждого файла вычисляем новый индекс
            // Новый индекс = старый - количество удалённых индексов которые меньше старого
            // Переименовываем по возрастанию чтобы не было конфликтов (сдвиг вниз)
            foreach (var (path, oldIdx, baseName) in parsed.OrderBy(x => x.idx))
            {
                int shift = removedIndices.Count(r => r < oldIdx);
                if (shift == 0) continue;

                int newIdx = oldIdx - shift;
                string dir2 = Path.GetDirectoryName(path);
                string ext  = Path.GetExtension(path);
                string newPath = Path.Combine(dir2, $"[{newIdx}]{baseName}{ext}");

                if (File.Exists(newPath))
                {
                    // Временное имя чтобы избежать коллизии
                    string tmpPath = Path.Combine(dir2, $"[tmp_{oldIdx}]{baseName}{ext}");
                    try { File.Move(path, tmpPath); File.Move(tmpPath, newPath); }
                    catch (Exception ex) { errors.Add($"Rename index {path}: {ex.Message}"); }
                }
                else
                {
                    try { File.Move(path, newPath); }
                    catch (Exception ex) { errors.Add($"Rename index {path}: {ex.Message}"); }
                }
            }
        }

        // ── Вспомогательные ─────────────────────────────────────────────

        /// <summary>
        /// Строит vpath из пути файла на диске используя rel (относительный путь от folder)
        /// и archiveIndex (ключи относительно treeRoot.VirtualPath).
        ///
        /// Структура Extract (созданная MassTextureExtractor):
        ///   folder\archiveRelPath\[bndIdx]tpfName\[texIdx]name.dds  → archive|bndIdx|texIdx
        ///   folder\archiveRelPath\[texIdx]name.dds                  → archive|texIdx
        ///
        /// archiveRelPath = Path.GetRelativePath(treeRoot.VirtualPath, archivePath)
        ///
        /// Если treeRoot != корень при Extract, ищем совпадение перебирая суффиксы rel.
        /// </summary>
        private static string BuildVPathFromFilePath(string filePath, string baseFolder, int fileIdx,
            Dictionary<string, string> archiveIndex, string cachedRelPath = null)
        {
            if (fileIdx < 0) return null;

            string rel = cachedRelPath ?? Path.GetRelativePath(baseFolder, filePath).Replace('/', '\\');
            var segments = rel.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return null;

            string fileName = Path.GetFileNameWithoutExtension(filePath);

            // Парсим texIdx из имени файла
            if (!fileName.StartsWith("[")) return null;
            int closeFile = fileName.IndexOf(']');
            if (closeFile <= 0) return null;
            if (!int.TryParse(fileName[1..closeFile], out int texIdx)) return null;

            // Ищем архив в archiveIndex.
            // Перебираем все возможные позиции архива в segments (от конца к началу),
            // и для каждой позиции пробуем все суффиксы пути (убирая префиксные сегменты).
            // Это нужно когда treeRoot != корень при Extract:
            //   rel = "map\m10\m10_0000.tpfbhd\[0]tpf\[0]tex"
            //   archiveIndex ключ = "m10\m10_0000.tpfbhd" (если treeRoot = map)
            for (int archEnd = segments.Length - 2; archEnd >= 0; archEnd--)
            {
                if (segments[archEnd].StartsWith("[")) continue;

                // Пробуем все суффиксы: segments[skip..archEnd+1] для skip = 0..archEnd
                for (int skip = 0; skip <= archEnd; skip++)
                {
                    string archKey = string.Join("\\", segments[skip..(archEnd + 1)]);
                    if (!archiveIndex.TryGetValue(archKey, out string archiveRealPath)) continue;

                    int afterArch = archEnd + 1;
                    int beforeFile = segments.Length - 1;

                    if (afterArch == beforeFile)
                    {
                        // archive\[texIdx]file.dds → archive|texIdx
                        return $"{archiveRealPath}|{texIdx}";
                    }
                    else if (afterArch < beforeFile)
                    {
                        // archive\[bndIdx]tpf\[texIdx]file.dds → archive|bndIdx|texIdx
                        string tpfFolder = segments[afterArch];
                        if (!tpfFolder.StartsWith("[")) continue;

                        int closeTpf = tpfFolder.IndexOf(']');
                        if (closeTpf <= 0) continue;

                        var idxParts = tpfFolder[1..closeTpf].Split('_');
                        var parsedIdx = new List<int>();
                        bool allOk = true;
                        foreach (var p in idxParts)
                            if (int.TryParse(p, out int n)) parsedIdx.Add(n);
                            else { allOk = false; break; }

                        if (!allOk || parsedIdx.Count == 0) continue;

                        string intermediates = string.Join("|", parsedIdx);
                        return $"{archiveRealPath}|{intermediates}|{texIdx}";
                    }
                }
            }

            return null;
        }

        public static string StripLeadingBrackets(string name)
        {
            if (!name.StartsWith("[")) return name;
            int close = name.IndexOf(']');
            return close >= 0 ? name[(close + 1)..] : name;
        }

        /// <summary>
        /// После добавления New-файлов в архив переименовывает их на диске:
        /// добавляет индекс в начало имени файла чтобы следующий Sync нашёл их по vpath.
        /// Индекс берётся из обновлённого дерева (после перезагрузки).
        /// Поскольку дерево ещё не обновлено — используем счётчик: новая текстура
        /// получает индекс = количество текстур в TPF до добавления + порядковый номер.
        /// </summary>
        private static void RenameNewFilesAfterInject(
            List<SyncEntry> entries, string folder, FileNode treeRoot, List<string> errors)
        {
            var newEntries = entries.Where(e => e.Status == FileStatus.New).ToList();
            if (newEntries.Count == 0) return;

            // Группируем по папке TPF
            var byTpfFolder = newEntries
                .GroupBy(e =>
                {
                    string rel = e.RelativePath.Replace('/', '\\');
                    int lastSep = rel.LastIndexOf('\\');
                    return lastSep >= 0 ? rel[..lastSep] : "";
                }, StringComparer.OrdinalIgnoreCase);

            foreach (var grp in byTpfFolder)
            {
                string tpfRelFolder = grp.Key;
                string tpfAbsFolder = Path.Combine(folder, tpfRelFolder);
                if (!Directory.Exists(tpfAbsFolder)) continue;

                // Определяем следующий texIdx в этой папке
                int maxTexIdx = -1;
                foreach (var f in Directory.GetFiles(tpfAbsFolder, "*.dds"))
                {
                    string n = Path.GetFileNameWithoutExtension(f);
                    if (!n.StartsWith("[")) continue;
                    int close = n.IndexOf(']');
                    if (close < 0) continue;
                    var parts = n[1..close].Split('_');
                    // Последняя часть = texIdx
                    if (parts.Length > 0 && int.TryParse(parts[^1], out int texIdx))
                        maxTexIdx = Math.Max(maxTexIdx, texIdx);
                }
                int nextIdx = maxTexIdx + 1;

                foreach (var entry in grp.OrderBy(e => e.RelativePath))
                {
                    string fileName = Path.GetFileName(entry.RelativePath.Replace('/', '\\'));
                    string baseName = Path.GetFileNameWithoutExtension(fileName);

                    // Пропускаем файлы у которых уже есть индекс
                    if (baseName.StartsWith("[")) continue;

                    string oldPath = Path.Combine(tpfAbsFolder, fileName);
                    if (!File.Exists(oldPath)) continue;

                    // Формат имени: [texIdx]name.dds для всех типов TPF
                    // (DCX — просто обёртка, не меняет индексацию)
                    string newFileName = $"[{nextIdx}]{baseName}.dds";

                    string newPath = Path.Combine(tpfAbsFolder, newFileName);

                    if (!File.Exists(newPath))
                    {
                        try
                        {
                            File.Move(oldPath, newPath);
                            Console.WriteLine($"[FolderSync] Renamed new file: {fileName} → {newFileName}");
                        }
                        catch (Exception ex) { errors.Add($"Rename new file {fileName}: {ex.Message}"); }
                    }
                    nextIdx++;
                }
            }
        }

    }
}
