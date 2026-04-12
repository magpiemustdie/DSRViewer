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
    /// Сканирует FLVER-файлы из дерева FileNode и для каждой текстуры
    /// определяет источник по приоритету:
    ///   1. Тот же архив что и FLVER (chrtpf, tpf внутри chrbnd)
    ///   2. Внешний архив рядом (*.tpfbhd / *.tpf в той же папке)
    ///   3. Любой файл в той же папке на диске
    /// </summary>
    public static class FlverTextureScanner
    {
        public enum TextureSource { SameArchive, ExternalArchive, SameFolder, NotFound }

        public record TextureMatch(
            string FlverPath,
            string TextureName,
            string TextureParamName,
            TextureSource Source,
            string FoundAt       // виртуальный путь или путь на диске, "" если не найден
        );

        public record ScanResult(
            List<TextureMatch> Matches,
            List<string> NotFound
        );

        // ── Публичный API ────────────────────────────────────────────────

        /// <summary>
        /// Сканирует все FLVER в дереве root, группируя по корневому архиву.
        /// Каждый архив открывается ровно один раз.
        /// </summary>
        public static ScanResult Scan(FileNode root, Dictionary<string, string> texturePaths)
        {
            var matches  = new List<TextureMatch>();
            var notFound = new List<string>();
            var flvers   = root.FindAll(n => n.IsFlver || n.IsNestedFlver);

            // Группируем по корневому архиву
            var byArchive = flvers.GroupBy(n => n.VirtualPath.Split('|')[0],
                StringComparer.OrdinalIgnoreCase);

            foreach (var archiveGroup in byArchive)
            {
                string archiveFile = archiveGroup.Key;
                string diskFolder  = Path.GetDirectoryName(archiveFile) ?? "";

                // Все FLVER из этого архива читаем за один проход
                var flverPaths = archiveGroup.Select(n => n.VirtualPath).ToArray();

                try
                {
                    new FileBinders().ProcessPaths(flverPaths, new FileOperation
                    {
                        UseFlverDelegate = true,
                        AdditionalFlverProcessing = (flver, virtualPath, name, errorLogs) =>
                        {
                            string archiveName = StripAllExtensions(archiveFile).ToLower();

                            var textures = flver.Materials
                                .SelectMany(m => m.Textures)
                                .Where(t => !string.IsNullOrEmpty(t.Path))
                                .GroupBy(t => Path.GetFileNameWithoutExtension(
                                    t.Path.Replace('/', '\\').Split('\\').Last()).ToLower())
                                .Select(g => g.First());

                            foreach (var tex in textures)
                            {
                                string texName      = Path.GetFileNameWithoutExtension(
                                    tex.Path.Replace('/', '\\').Split('\\').Last());
                                string texNameLower = texName.ToLower();

                                var match = FindTexture(texNameLower, texName, tex.ParamName,
                                    virtualPath, archiveFile, archiveName, diskFolder, texturePaths);

                                lock (matches) matches.Add(match);
                                if (match.Source == TextureSource.NotFound)
                                    lock (notFound) notFound.Add(texName);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FlverTextureScanner] Error scanning {archiveFile}: {ex.Message}");
                }
            }

            return new ScanResult(matches, notFound);
        }

        /// <summary>Записывает отчёт о результатах сканирования в текстовый файл.</summary>
        public static void WriteReport(ScanResult result, string outputPath)
        {
            using var w = new StreamWriter(outputPath, false, Encoding.UTF8);

            w.WriteLine("=== FLVER Texture Source Report ===");
            w.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            w.WriteLine();

            // Группируем по FLVER
            var byFlver = result.Matches.GroupBy(m => m.FlverPath);
            foreach (var group in byFlver)
            {
                w.WriteLine($"FLVER: {group.Key}");
                foreach (var m in group.OrderBy(m => (int)m.Source))
                {
                    string src = m.Source switch
                    {
                        TextureSource.SameArchive    => "[SAME_ARCHIVE]",
                        TextureSource.ExternalArchive => "[EXT_ARCHIVE]",
                        TextureSource.SameFolder     => "[SAME_FOLDER]",
                        TextureSource.NotFound       => "[NOT_FOUND]  ",
                        _ => "[?]"
                    };
                    w.WriteLine($"  {src} {m.TextureParamName,-20} {m.TextureName}");
                    if (!string.IsNullOrEmpty(m.FoundAt))
                        w.WriteLine($"           -> {m.FoundAt}");
                }
                w.WriteLine();
            }

            w.WriteLine("=== NOT FOUND ===");
            foreach (var s in result.NotFound.Distinct().OrderBy(x => x))
                w.WriteLine($"  {s}");

            w.WriteLine();
            w.WriteLine("=== SUMMARY ===");
            w.WriteLine($"  Total textures scanned: {result.Matches.Count}");
            w.WriteLine($"  Same archive:           {result.Matches.Count(m => m.Source == TextureSource.SameArchive)}");
            w.WriteLine($"  External archive:       {result.Matches.Count(m => m.Source == TextureSource.ExternalArchive)}");
            w.WriteLine($"  Same folder:            {result.Matches.Count(m => m.Source == TextureSource.SameFolder)}");
            w.WriteLine($"  Not found:              {result.NotFound.Count}");

            Console.WriteLine($"[FlverTextureScanner] Report saved to: {outputPath}");
        }

        // ── Приватные методы ─────────────────────────────────────────────

        private static TextureMatch FindTexture(
            string texNameLower, string texName, string paramName,
            string flverVirtualPath, string archiveFile, string archiveName,
            string diskFolder, Dictionary<string, string> texturePaths)
        {
            string foundPath = null;
            texturePaths.TryGetValue(texNameLower, out foundPath);

            if (foundPath != null)
            {
                string foundArchive = foundPath.Split('|')[0];
                string foundFolder  = Path.GetDirectoryName(foundArchive) ?? "";

                // Приоритет 1: тот же архив что и FLVER
                if (string.Equals(foundArchive, archiveFile, StringComparison.OrdinalIgnoreCase))
                    return new TextureMatch(flverVirtualPath, texName, paramName,
                        TextureSource.SameArchive, foundPath);

                // Приоритет 2: внешний архив в той же папке на диске
                if (string.Equals(foundFolder, diskFolder, StringComparison.OrdinalIgnoreCase))
                    return new TextureMatch(flverVirtualPath, texName, paramName,
                        TextureSource.ExternalArchive, foundPath);
            }

            // Приоритет 3: файл на диске в той же папке
            string[] extensions = [".dds", ".tga", ".png"];
            foreach (var ext in extensions)
            {
                string candidate = Path.Combine(diskFolder, texName + ext);
                if (File.Exists(candidate))
                    return new TextureMatch(flverVirtualPath, texName, paramName,
                        TextureSource.SameFolder, candidate);
            }

            // Приоритет 4: найдено в любом другом архиве
            if (foundPath != null)
                return new TextureMatch(flverVirtualPath, texName, paramName,
                    TextureSource.ExternalArchive, foundPath);

            return new TextureMatch(flverVirtualPath, texName, paramName,
                TextureSource.NotFound, "");
        }

        // ── Хелпер для загрузки TexturesList.txt ────────────────────────

        /// <summary>
        /// Загружает файл TexturesList.txt в словарь name(lower) → virtualPath.
        /// </summary>
        public static Dictionary<string, string> LoadTextureList(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return result;

            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                int sep = line.IndexOf(';');
                if (sep < 0) continue;
                string raw   = line[..sep].Trim();
                string name  = Path.GetFileNameWithoutExtension(raw).ToLower();
                string vpath = line[(sep + 1)..].Trim();
                result.TryAdd(name, vpath);
            }

            return result;
        }

        /// <summary>Загружает TexturesList в многозначный словарь: имя → все пути (все архивы).</summary>
        public static Dictionary<string, List<string>> LoadTextureListMulti(string path)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return result;

            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                int sep = line.IndexOf(';');
                if (sep < 0) continue;
                string raw   = line[..sep].Trim();
                string name  = Path.GetFileNameWithoutExtension(raw).ToLower();
                string vpath = line[(sep + 1)..].Trim();
                if (!result.ContainsKey(name)) result[name] = new List<string>();
                result[name].Add(vpath);
            }

            return result;
        }

        /// <summary>
        /// Запись о текстуре в FLVER: какой слот (g_Diffuse и т.д.),
        /// имя текстуры и где она лежит.
        /// </summary>
        public record FlverTexEntry(
            string ArchiveBaseName,  // c0000, c2060 и т.д. (имя архива без расширения)
            string ArchiveType,      // chr, map, parts, obj, sfx
            string ParamName,        // g_Diffuse, g_Bumpmap, g_Specular и т.д.
            string TextureName,      // c0000_body
            string VirtualPath       // полный виртуальный путь к текстуре
        );

        /// <summary>
        /// Сканирует все FLVER в дереве и строит индекс:
        /// (archiveBaseName, paramName) → список FlverTexEntry.
        /// Используется для сопоставления текстур по реальному слоту материала.
        /// </summary>
        /// <summary>Строит индекс (archiveBaseName, paramName) → список FlverTexEntry по всем FLVER в дереве.</summary>
        public static Dictionary<(string archive, string param), List<FlverTexEntry>>
            BuildFlverIndex(FileNode root, Dictionary<string, List<string>> texturePaths)
        {
            var index  = new Dictionary<(string, string), List<FlverTexEntry>>();
            var flvers = root.FindAll(n => n.IsFlver || n.IsNestedFlver);

            // Группируем по корневому архиву — один проход на архив
            var byArchive = flvers.GroupBy(n => n.VirtualPath.Split('|')[0],
                StringComparer.OrdinalIgnoreCase);

            foreach (var archiveGroup in byArchive)
            {
                string archiveFile = archiveGroup.Key;
                string archiveType = GetArchiveType(archiveFile);
                var flverPaths     = archiveGroup.Select(n => n.VirtualPath).ToArray();

                try
                {
                    new FileBinders().ProcessPaths(flverPaths, new FileOperation
                    {
                        UseFlverDelegate = true,
                        AdditionalFlverProcessing = (flver, virtualPath, name, errorLogs) =>
                        {
                            string archiveBaseName = StripAllExtensions(archiveFile).ToLower();

                            foreach (var mat in flver.Materials)
                            {
                                foreach (var tex in mat.Textures.Where(t => !string.IsNullOrEmpty(t.Path)))
                                {
                                    string rawName = tex.Path.Replace('/', '\\').Split('\\').Last();
                                    string texName = Path.GetFileNameWithoutExtension(rawName).ToLower();

                                    // Ищем путь приоритетно в том же архиве что и FLVER
                                    string vpath = FindVPathInArchive(texName, archiveFile, texturePaths);

                                    var entry = new FlverTexEntry(
                                        archiveBaseName, archiveType,
                                        tex.ParamName, texName, vpath ?? "");

                                    var key = (archiveBaseName, tex.ParamName);
                                    lock (index)
                                    {
                                        if (!index.ContainsKey(key)) index[key] = new List<FlverTexEntry>();
                                        if (!index[key].Any(e => e.TextureName == texName))
                                            index[key].Add(entry);
                                    }
                                }
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FlverTextureScanner] BuildFlverIndex error {archiveFile}: {ex.Message}");
                }
            }

            return index;
        }

        /// <summary>
        /// Ищет vpath для текстуры: сначала в том же архиве что и FLVER,
        /// потом в любом другом.
        /// </summary>
        private static string FindVPathInArchive(
            string texName, string archiveFile,
            Dictionary<string, List<string>> texturePaths)
        {
            if (!texturePaths.TryGetValue(texName, out var paths) || paths.Count == 0)
                return "";

            if (paths.Count == 1) return paths[0];

            // Предпочитаем путь из того же архива
            var sameArchive = paths.FirstOrDefault(p =>
                p.Split('|')[0].Equals(archiveFile, StringComparison.OrdinalIgnoreCase));
            if (sameArchive != null) return sameArchive;

            return paths[0];
        }

        private static string GetArchiveType(string virtualPath)
        {
            string lower = virtualPath.ToLower();
            foreach (var folder in new[] { "chr", "map", "parts", "obj", "sfx" })
                if (lower.Contains($"\\{folder}\\") || lower.Contains($"/{folder}/"))
                    return folder;
            return "";
        }

        /// <summary>
        /// Убирает все расширения из имени файла итеративно.
        /// "BD_A_9560_M.partsbnd.dcx" → "BD_A_9560_M"
        /// "c0000.chrbnd.dcx"         → "c0000"
        /// </summary>
        public static string StripAllExtensions(string fileName)
        {
            string name = Path.GetFileName(fileName);
            while (true)
            {
                string stripped = Path.GetFileNameWithoutExtension(name);
                if (stripped == name || string.IsNullOrEmpty(stripped)) break;
                name = stripped;
            }
            return name;
        }
    }
}
