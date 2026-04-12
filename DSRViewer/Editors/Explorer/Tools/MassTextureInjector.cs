using DSRViewer.Editors.Explorer.DDSHelper;
using DSRViewer.FileProcess;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSRViewer.Editors.Explorer.Tools
{
    /// <summary>
    /// Массовый инжектор текстур.
    ///
    /// Формат имён папок:
    ///   "[idx]name.ext/"     → существующий вложенный архив с индексом idx
    ///   "[new]name.ext/"     → создать новый архив name.ext, добавить в BND
    ///   "[new N]name.ext/"   → то же, N = порядок добавления (для нескольких новых)
    ///
    /// Формат имён файлов:
    ///   "[idx]name.dds"      → REPLACE текстуры по индексу
    ///   "name.dds"           → ADD новой текстуры в TPF
    ///
    /// Поддерживаемые расширения для [new N]:
    ///   .tpf       → TPF без сжатия
    ///   .tpf.dcx   → TPF в DCX-обёртке
    ///   .bnd       → BND3 без сжатия
    ///   .bnd.dcx   → BND3 в DCX-обёртке
    ///   другое     → raw bytes первого файла
    ///
    /// Поддерживаемые корневые архивы: BND, TPF, BXF (все через FileBinders).
    /// </summary>
    public static class MassTextureInjector
    {
        public record InjectResult(
            int Injected,
            int Added,
            int Created,
            int NotFound,
            int Failed,
            List<string> Errors,
            List<string> ModifiedArchives
        );

        public static InjectResult Inject(FileNode treeRoot, string inputDir, Action<string> onProgress = null)
        {
            var ddsFiles = Directory.GetFiles(inputDir, "*.dds", SearchOption.AllDirectories);
            return InjectFiles(treeRoot, inputDir, ddsFiles, onProgress, forceAdd: false);
        }

        /// <summary>Инжектирует только файлы из выбранной подветки дерева.</summary>
        public static InjectResult InjectSubtree(FileNode treeRoot, FileNode subtreeRoot, string inputDir, Action<string> onProgress = null)
        {
            var texNodes = subtreeRoot.FindAll(n => n.IsDDS);
            
            if (texNodes.Count == 0)
            {
                Console.WriteLine("[MassInject] No DDS nodes in subtree");
                return new InjectResult(0, 0, 0, 0, 0, new List<string>(), new List<string>());
            }

            var subtreeVPaths = new HashSet<string>(
                texNodes.Select(n => n.VirtualPath),
                StringComparer.OrdinalIgnoreCase);

            var allDdsFiles = Directory.GetFiles(inputDir, "*.dds", SearchOption.AllDirectories);
            var archiveIndex = BuildArchiveIndex(treeRoot);
            var treeVPaths = new HashSet<string>(
                treeRoot.FindAll(n => n.IsBndArchive || n.IsTpfArchive || n.IsBxfArchive || n.IsNestedTpfArchive)
                    .Select(n => n.VirtualPath),
                StringComparer.OrdinalIgnoreCase);
            
            var filteredFiles = allDdsFiles.Where(ddsPath =>
            {
                var resolved = ResolveItem(ddsPath, inputDir, archiveIndex, forceAdd: false, treeVPaths);
                if (resolved == null) return false;
                
                if (resolved.op == Op.Replace)
                    return subtreeVPaths.Contains(resolved.vpath);
                
                if (resolved.op == Op.Add)
                    return subtreeVPaths.Any(vp => 
                        vp.StartsWith(resolved.tpfVPath + "|", StringComparison.OrdinalIgnoreCase) ||
                        vp.Equals(resolved.tpfVPath, StringComparison.OrdinalIgnoreCase));

                if (resolved.op == Op.Create)
                {
                    // Create: проверяем что BND архив принадлежит subtree
                    string archiveKey = resolved.archiveKey;
                    return subtreeVPaths.Any(vp =>
                        vp.StartsWith(archiveKey + "|", StringComparison.OrdinalIgnoreCase) ||
                        vp.Equals(archiveKey, StringComparison.OrdinalIgnoreCase)) ||
                        archiveKey.Equals(subtreeRoot.VirtualPath.Split('|')[0], StringComparison.OrdinalIgnoreCase);
                }
                
                return false;
            }).ToList();

            Console.WriteLine($"[MassInject] Subtree filter: {filteredFiles.Count}/{allDdsFiles.Length} files match subtree");
            
            return InjectFiles(treeRoot, inputDir, filteredFiles, onProgress, forceAdd: false);
        }

        /// <summary>Инжектирует только указанные файлы (используется из FolderSync.Apply для New записей).</summary>
        public static InjectResult InjectFiles(FileNode treeRoot, string inputDir,
            IEnumerable<string> ddsFiles, Action<string> onProgress = null, bool forceAdd = false)
        {
            int injected = 0;
            int added    = 0;
            int created  = 0;
            int notFound = 0;
            int failed   = 0;
            var errors   = new List<string>();
            var modifiedArchives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var archiveIndex = BuildArchiveIndex(treeRoot);
            var ddsFilesList = ddsFiles.ToList();

            // Набор всех vpath архивов в дереве для определения Create vs Replace
            var treeVPaths = new HashSet<string>(
                treeRoot.FindAll(n => n.IsBndArchive || n.IsTpfArchive || n.IsBxfArchive || n.IsNestedTpfArchive)
                    .Select(n => n.VirtualPath),
                StringComparer.OrdinalIgnoreCase);

            var replaceItems = new Dictionary<string, List<(string vpath, string ddsPath)>>(
                StringComparer.OrdinalIgnoreCase);
            var addItems = new Dictionary<string, List<(string tpfVPath, string texName, string ddsPath)>>(
                StringComparer.OrdinalIgnoreCase);
            var createItems = new Dictionary<string, List<(string bndPath, string newArchiveName, string texName, string ddsPath, int createCount)>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var ddsPath in ddsFilesList)
            {
                ResolvedItem resolved = ResolveItem(ddsPath, inputDir, archiveIndex, forceAdd, treeVPaths);
                if (resolved == null)
                {
                    notFound++;
                    errors.Add($"Cannot resolve: {Path.GetRelativePath(inputDir, ddsPath)}");
                    continue;
                }

                switch (resolved.op)
                {
                    case Op.Replace:
                        var rKey = resolved.archiveKey;
                        if (!replaceItems.ContainsKey(rKey)) replaceItems[rKey] = new();
                        replaceItems[rKey].Add((resolved.vpath, ddsPath));
                        break;
                    case Op.Add:
                        var aKey = resolved.archiveKey;
                        if (!addItems.ContainsKey(aKey)) addItems[aKey] = new();
                        addItems[aKey].Add((resolved.tpfVPath, resolved.texName, ddsPath));
                        break;
                    case Op.Create:
                        var cKey = resolved.archiveKey;
                        if (!createItems.ContainsKey(cKey)) createItems[cKey] = new();
                        createItems[cKey].Add((resolved.archiveKey,
                            resolved.newArchiveName, resolved.texName, ddsPath,
                            resolved.createCount));
                        break;
                }
            }

            // ── REPLACE ──────────────────────────────────────────────────
            // Параллельная обработка разных архивов (независимые операции)
            var replaceResults = new System.Collections.Concurrent.ConcurrentBag<(int injected, int failed, List<string> errors, string archivePath)>();
            
            Parallel.ForEach(replaceItems,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                kvp =>
            {
                var (archivePath, items) = (kvp.Key, kvp.Value);
                var vpaths  = items.Select(x => x.vpath).ToArray();
                // При дублирующихся vpath берём последний файл (последняя экстракция перезаписывает)
                var byVPath = items
                    .GroupBy(x => x.vpath, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Last().ddsPath,
                        StringComparer.OrdinalIgnoreCase);
                int archiveInjected = 0;
                var localErrors = new List<string>();

                try
                {
                    var binder = new FileBinders();
                    binder.ProcessPaths(vpaths, new FileOperation
                    {
                        WriteObject    = true,
                        UseTexDelegate = true,
                        AdditionalTextureProcessing = (texture, virtualPath) =>
                        {
                            if (!byVPath.TryGetValue(virtualPath, out var ddsFile)) return;
                            byte[] bytes = File.ReadAllBytes(ddsFile);
                            if (bytes.Length == 0) return;
                            DDSTextureApplier.Apply(texture, bytes);
                            archiveInjected++;
                            onProgress?.Invoke($"[replace] {virtualPath}");
                        }
                    });

                    replaceResults.Add((archiveInjected, 0, localErrors, archivePath));
                }
                catch (Exception ex)
                {
                    localErrors.Add($"Replace {archivePath}: {ex.Message}");
                    replaceResults.Add((0, items.Count, localErrors, archivePath));
                }
            });

            // Собираем результаты
            foreach (var (archiveInjected, archiveFailed, localErrors, archivePath) in replaceResults)
            {
                injected += archiveInjected;
                failed += archiveFailed;
                errors.AddRange(localErrors);
                if (archiveInjected > 0) modifiedArchives.Add(archivePath);
            }

            // ── ADD ───────────────────────────────────────────────────────
            foreach (var (archivePath, items) in addItems)
            {
                var byTpf = items.GroupBy(x => x.tpfVPath, StringComparer.OrdinalIgnoreCase);
                foreach (var tpfGroup in byTpf)
                {
                    string tpfVPath = tpfGroup.Key;
                    var addQueue = BuildAddQueue(tpfGroup.Select(x => (x.texName, x.ddsPath)));
                    if (addQueue.Count == 0) continue;

                    try
                    {
                        // ПРИМЕЧАНИЕ: FileBinders.AddObject работает только с одной текстурой за раз.
                        // Каждый вызов читает TPF, добавляет текстуру, пишет обратно.
                        // Это неэффективно при добавлении нескольких текстур, но изменение
                        // потребует рефакторинга FileBinders API (ProcessTpfCore строка 313).
                        foreach (var (texName, bytes, fmt) in addQueue)
                        {
                            try
                            {
                                new FileBinders().ProcessPaths([tpfVPath], new FileOperation
                                {
                                    WriteObject      = true,
                                    AddObject        = true,
                                    NewObjectName    = texName,
                                    NewObjectBytes   = bytes,
                                    NewTextureFormat = fmt
                                });
                                added++;
                                onProgress?.Invoke($"[add] {texName} → {Path.GetFileName(tpfVPath)}");
                            }
                            catch (Exception texEx)
                            {
                                failed++;
                                errors.Add($"Add {texName} to {tpfVPath}: {texEx.Message}");
                            }
                        }
                        modifiedArchives.Add(archivePath);
                    }
                    catch (Exception ex)
                    {
                        failed += addQueue.Count;
                        errors.Add($"Add to {tpfVPath}: {ex.Message}");
                    }
                }
            }

            // ── CREATE ────────────────────────────────────────────────────
            // Группируем по (bndPath, newArchiveName), сортируем по createCount (порядок добавления)
            var createGroups = createItems.Values
                .SelectMany(x => x)
                .GroupBy(x => (x.bndPath, x.newArchiveName),
                    (k, g) => (k.bndPath, k.newArchiveName,
                               textures: g.Select(x => (x.texName, x.ddsPath)).ToList(),
                               order: g.Min(x => x.createCount)))
                .OrderBy(x => x.order);

            foreach (var (bndPath, newArchiveName, textures, order) in createGroups)
            {
                try
                {
                    bool isDcxTpf = newArchiveName.EndsWith(".tpf.dcx", StringComparison.OrdinalIgnoreCase);
                    bool isTpf    = newArchiveName.EndsWith(".tpf", StringComparison.OrdinalIgnoreCase);
                    bool isBnd    = newArchiveName.EndsWith(".bnd", StringComparison.OrdinalIgnoreCase)
                                 || newArchiveName.EndsWith(".bnd.dcx", StringComparison.OrdinalIgnoreCase);

                    var addQueue = BuildAddQueue(textures);
                    if (addQueue.Count == 0) continue;

                    byte[] archiveBytes;

                    if (isTpf || isDcxTpf)
                    {
                        var tpf = new TPF { Platform = TPF.TPFPlatform.PC };
                        foreach (var (texName, bytes, fmt) in addQueue)
                            tpf.Textures.Add(new TPF.Texture { Name = texName, Platform = TPF.TPFPlatform.PC, Bytes = bytes, Format = fmt });

                        archiveBytes = tpf.Write();
                        if (isDcxTpf)
                            archiveBytes = DCX.Compress(archiveBytes, new DCX.DcxDfltCompressionInfo(0));
                    }
                    else if (isBnd)
                    {
                        var bnd = new BND3();
                        foreach (var (texName, bytes, fmt) in addQueue)
                            bnd.Files.Add(new BinderFile { Name = texName, Bytes = bytes });

                        archiveBytes = bnd.Write();
                        if (newArchiveName.EndsWith(".dcx", StringComparison.OrdinalIgnoreCase))
                            archiveBytes = DCX.Compress(archiveBytes, new DCX.DcxDfltCompressionInfo(0));
                    }
                    else
                    {
                        archiveBytes = addQueue[0].bytes;
                    }

                    // Узнаём текущее количество файлов в BND — это будет индекс нового файла
                    int newFileIdx = GetBndFileCount(bndPath);

                    new FileBinders().ProcessPaths([bndPath], new FileOperation
                    {
                        WriteObject    = true,
                        AddObject      = true,
                        NewObjectName  = newArchiveName,
                        NewObjectBytes = archiveBytes
                    });

                    created++; // Создали один архив (не считаем текстуры внутри)
                    modifiedArchives.Add(bndPath);
                    onProgress?.Invoke($"[create] {newArchiveName} in {Path.GetFileName(bndPath)}");

                    // Переименовываем папку на диске: [new N]name → [actualIdx]name
                    RenameCreatedFolder(inputDir, bndPath, newArchiveName, order, newFileIdx);
                }
                catch (Exception ex)
                {
                    failed += textures.Count;
                    errors.Add($"Create {newArchiveName} in {bndPath}: {ex.Message}");
                }
            }

            Console.WriteLine($"[MassInject] replaced={injected}, added={added}, created={created}, notFound={notFound}, failed={failed}");
            return new InjectResult(injected, added, created, notFound, failed, errors,
                modifiedArchives.ToList());
        }

        // ── Разбор пути ──────────────────────────────────────────────────

        private enum Op { Replace, Add, Create }

        private class ResolvedItem
        {
            public Op     op             { get; init; }
            public string archiveKey     { get; init; }
            public string vpath          { get; init; }
            public string tpfVPath       { get; init; }
            public string texName        { get; init; }
            public string newArchiveName { get; init; }
            public int    createCount    { get; init; } = 1;
        }

        /// <summary>
        /// Разбирает путь DDS-файла.
        ///
        /// Структура:
        ///   inputDir / [typeFolder] / archiveName / [subFolder] / texName.dds
        ///
        /// subFolder форматы:
        ///   "[idx] name.ext"  → существующий архив
        ///   "[new] name.ext"  → создать новый
        ///   (нет)             → корневой TPF
        /// </summary>
        private static ResolvedItem? ResolveItem(
            string ddsPath, string inputDir,
            Dictionary<string, string> archiveIndex, bool forceAdd = false,
            HashSet<string> treeVPaths = null)
        {
            string relPath = Path.GetRelativePath(inputDir, ddsPath).Replace('/', '\\');
            var segments = relPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2) return null;

            // Ищем корневой архив в сегментах пути
            string archiveRealPath = null;
            int archiveSegIdx = -1;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (archiveIndex.TryGetValue(segments[i], out var rp) && !rp.Contains('|'))
                {
                    archiveSegIdx   = i;
                    archiveRealPath = rp;
                    break;
                }
            }
            if (archiveSegIdx < 0)
            {
                return null;
            }

            string fileName = Path.GetFileNameWithoutExtension(ddsPath);
            // Формат: "[idx]texName" — индекс в начале
            string texName  = StripLeadingBrackets(fileName);

            int bracketOpen  = fileName.StartsWith("[") ? 0 : -1;
            int bracketClose = bracketOpen >= 0 ? fileName.IndexOf(']') : -1;
            bool hasTexIdx   = bracketOpen == 0 && bracketClose > 0
                && fileName[1..bracketClose]
                    .Split('_').All(s => int.TryParse(s, out _));

            // Есть ли подпапка между архивом и файлом?
            string subFolder = archiveSegIdx + 2 < segments.Length
                ? segments[archiveSegIdx + 1]
                : null;

            if (subFolder == null)
            {
                // Корневой TPF
                if (!forceAdd && hasTexIdx)
                {
                    string idx   = fileName[1..bracketClose];
                    string vpath = $"{archiveRealPath}|{idx.Replace('_', '|')}";
                    return new ResolvedItem { op = Op.Replace, archiveKey = archiveRealPath, vpath = vpath, tpfVPath = archiveRealPath, texName = texName };
                }
                return new ResolvedItem { op = Op.Add, archiveKey = archiveRealPath, tpfVPath = archiveRealPath, texName = texName };
            }

            // Разбираем subFolder: "[idx]name.ext" или "[new N]name.ext"
            if (!TryParseSubFolder(subFolder, out bool isNew, out string subName, out int createOrder))
                return null;

            if (isNew)
            {
                // CREATE: добавить новый архив subName в BND, createOrder = порядок добавления
                return new ResolvedItem { op = Op.Create, archiveKey = archiveRealPath, texName = texName, newArchiveName = subName, createCount = createOrder };
            }

            // Существующий вложенный архив
            // subFolder может содержать составной индекс: "[i0_i1]tpfName"
            // vpath = archive|i0|i1|...|texIdx
            string intermediates = string.Join("|", subFolder[1..subFolder.IndexOf(']')].Split('_')
                .Select(p => int.TryParse(p, out int n) ? n.ToString() : p));
            string tpfVPath = $"{archiveRealPath}|{intermediates}";

            // Если архив с таким vpath не существует в дереве — создаём новый
            if (treeVPaths != null && !treeVPaths.Contains(tpfVPath))
            {
                return new ResolvedItem { op = Op.Create, archiveKey = archiveRealPath, texName = texName, newArchiveName = subName, createCount = 1 };
            }

            if (!forceAdd && hasTexIdx)
            {
                string idx   = fileName[1..bracketClose];
                string vpath = $"{tpfVPath}|{idx.Replace('_', '|')}";
                return new ResolvedItem { op = Op.Replace, archiveKey = archiveRealPath, vpath = vpath, tpfVPath = tpfVPath, texName = texName };
            }
            return new ResolvedItem { op = Op.Add, archiveKey = archiveRealPath, tpfVPath = tpfVPath, texName = texName };
        }

        /// <summary>
        /// Разбирает имя подпапки:
        ///   "[3]c0000.tpf"         → isNew=false, count=1, name="c0000.tpf"
        ///   "[1_7]WP_A.tpf.dcx"    → isNew=false, count=1, name="WP_A.tpf.dcx"
        ///   "[new]myarch.tpf.dcx"  → isNew=true,  count=1, name="myarch.tpf.dcx"
        ///   "[new 5]myarch.tpf"    → isNew=true,  count=5, name="myarch.tpf"
        /// </summary>
        private static bool TryParseSubFolder(string folder,
            out bool isNew, out string name, out int count)
        {
            isNew = false; name = null; count = 1;

            if (!folder.StartsWith("[")) return false;
            int close = folder.IndexOf(']');
            if (close < 0) return false;

            string tag = folder[1..close].Trim();
            string rawName = folder[(close + 1)..].TrimStart();
            name = MassTextureExtractor.UnescapeName(rawName);
            if (string.IsNullOrEmpty(name)) return false;

            if (tag.StartsWith("new", StringComparison.OrdinalIgnoreCase))
            {
                isNew = true;
                string rest = tag[3..].Trim();
                if (!string.IsNullOrEmpty(rest) && int.TryParse(rest, out int n) && n > 0)
                    count = n;
                return true;
            }

            // Составной или одиночный числовой индекс — просто проверяем что все части числа
            bool allNumeric = tag.Split('_').All(p => int.TryParse(p, out _));
            return allNumeric;
        }

        /// <summary>Убирает ведущий [idx] из имени файла: "[3]c0000_body" → "c0000_body".</summary>
        private static string StripLeadingBrackets(string name)
        {
            if (!name.StartsWith("[")) return name;
            int close = name.IndexOf(']');
            return close >= 0 ? name[(close + 1)..] : name;
        }

        /// <summary>Возвращает количество файлов в BND — это будет индекс нового файла.</summary>
        private static int GetBndFileCount(string bndPath)
        {
            try
            {
                var bnd = BND3.Read(bndPath);
                return bnd.Files.Count;
            }
            catch { return -1; }
        }

        /// <summary>
        /// Переименовывает папку "[new N]archiveName" → "[actualIdx]archiveName" на диске.
        /// Ищет папку рядом с архивом в inputDir.
        /// </summary>
        private static void RenameCreatedFolder(
            string inputDir, string bndPath, string archiveName,
            int createOrder, int actualIdx)
        {
            if (actualIdx < 0) return;

            // Ищем папку архива в inputDir
            string archiveFileName = Path.GetFileName(bndPath);
            string archiveDir = Directory.GetDirectories(inputDir, "*", SearchOption.AllDirectories)
                .FirstOrDefault(d => Path.GetFileName(d).Equals(archiveFileName, StringComparison.OrdinalIgnoreCase));
            if (archiveDir == null) return;

            // Ищем подпапку "[new N]archiveName" или "[new]archiveName"
            string oldFolderName = null;
            foreach (var dir in Directory.GetDirectories(archiveDir))
            {
                string dirName = Path.GetFileName(dir);
                if (!dirName.StartsWith("[")) continue;
                int close = dirName.IndexOf(']');
                if (close < 0) continue;
                string tag  = dirName[1..close].Trim();
                string name = MassTextureExtractor.UnescapeName(dirName[(close + 1)..].TrimStart());

                bool isNewTag = tag.StartsWith("new", StringComparison.OrdinalIgnoreCase);
                if (!isNewTag) continue;

                // Проверяем что имя совпадает
                if (!name.Equals(archiveName, StringComparison.OrdinalIgnoreCase)) continue;

                // Проверяем порядок если указан
                string rest = tag[3..].Trim();
                if (!string.IsNullOrEmpty(rest) && int.TryParse(rest, out int n) && n != createOrder)
                    continue;

                oldFolderName = dir;
                break;
            }

            if (oldFolderName == null) return;

            string newFolderName = Path.Combine(archiveDir, $"[{actualIdx}]{archiveName}");
            if (Directory.Exists(newFolderName)) return; // уже существует

            try
            {
                Directory.Move(oldFolderName, newFolderName);
                Console.WriteLine($"[MassInject] Renamed folder: {Path.GetFileName(oldFolderName)} → [{actualIdx}]{archiveName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MassInject] Cannot rename folder: {ex.Message}");
            }
        }
        private static List<(string texName, byte[] bytes, byte fmt)> BuildAddQueue(
            IEnumerable<(string texName, string ddsPath)> items)
        {
            var queue = new List<(string, byte[], byte)>();
            foreach (var (texName, ddsPath) in items)
            {
                byte fmt = 1;
                try
                {
                    // Оптимизация: сначала читаем только заголовок для определения формата
                    var meta = DDSTools.ReadDDSMetaFromFile(ddsPath);
                    if (DDS_FlagFormatList.DDSFlagListSet.TryGetValue(meta.Format, out int f))
                        fmt = Convert.ToByte(f);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MassInject] Cannot read DDS meta for {ddsPath}: {ex.Message}");
                }
                
                // Затем читаем весь файл для байтов
                byte[] bytes = File.ReadAllBytes(ddsPath);
                if (bytes.Length == 0) continue;
                
                queue.Add((texName, bytes, fmt));
            }
            return queue;
        }

        // ── Индекс архивов ────────────────────────────────────────────────

        private static Dictionary<string, string> BuildArchiveIndex(FileNode root)
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var node in root.FindAll(n => n.IsBndArchive || n.IsTpfArchive || n.IsBxfArchive))
            {
                string vp = node.VirtualPath;
                // Только корневые архивы (vpath без '|')
                if (vp.Contains('|')) continue;
                string archiveName = Path.GetFileName(vp);
                if (!index.ContainsKey(archiveName))
                    index[archiveName] = vp;
            }

            // Добавляем сам корень если он является архивом
            if (!string.IsNullOrEmpty(root.VirtualPath) &&
                (root.IsBndArchive || root.IsTpfArchive || root.IsBxfArchive) &&
                !root.VirtualPath.Contains('|'))
            {
                string archiveName = Path.GetFileName(root.VirtualPath);
                index.TryAdd(archiveName, root.VirtualPath);
            }

            return index;
        }
    }
}
