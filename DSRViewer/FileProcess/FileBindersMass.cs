using DSRViewer.Editors.Explorer.DDSHelper;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSRViewer.FileProcess
{
    /// <summary>Обрабатывает виртуальные пути к файлам игры, выполняя операции чтения, записи и замены.</summary>
    public class FileBinders
    {
        private string _currentRealPath = "";
        private object? _mainObject;
        private List<string> _errorLogs = [];
        private int _depth = 0;
        private const int MaxDepth = 12;
        private bool _useBytePatch = false;

        // Отступ зависит от глубины вложенности (количество '|' в пути)
        private static void Log(string virtualPath, string message)
        {
            int depth = virtualPath.Count(c => c == '|');
            string prefix = depth == 0 ? "►" : depth == 1 ? "  └" : new string(' ', (depth - 1) * 2) + "  └";
            Console.WriteLine($"{prefix} {message}");
        }

        private static void LogAction(string virtualPath, string action)
        {
            int depth = virtualPath.Count(c => c == '|');
            string indent = new string(' ', depth * 2 + 4);
            Console.WriteLine($"{indent}✓ {action}");
        }

        private static void LogError(string message)
        {
            Console.WriteLine($"  ✗ {message}");
        }

        /// <summary>Обрабатывает список виртуальных путей согласно заданной операции.</summary>
        public void ProcessPaths(IEnumerable<string> virtualPaths, FileOperation operation)
        {
            _errorLogs.Clear();
            _depth = 0;
            _useBytePatch = operation.UseBytePatchFallback;
            var groupedPaths = GroupPaths(virtualPaths);

            foreach (var group in groupedPaths)
            {
                try
                {
                    // Для корневого файла виртуальный путь — это сам filePath (реальный путь на диске)
                    ProcessFileGroup(group.Key, group.Value, operation);
                }
                catch (Exception e)
                {
                    var errorMsg = $"Error: {group.Key}: {e.Message}";
                    LogError(errorMsg);
                    _errorLogs.Add(errorMsg);
                }
            }

            if (_errorLogs.Count > 0)
                File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "_errorLogs.txt"), _errorLogs);
        }

        private Dictionary<string, List<int[]>> GroupPaths(IEnumerable<string> paths)
        {
            var grouped = new Dictionary<string, List<int[]>>();

            foreach (var path in paths)
            {
                var segments = path.Split('|');
                var filePath = segments[0];
                if (string.IsNullOrEmpty(filePath)) continue;
                var indices = segments.Skip(1)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => int.TryParse(s, out var i) ? i : -1)
                    .Where(i => i >= 0)
                    .ToArray();

                if (!grouped.ContainsKey(filePath))
                    grouped[filePath] = new List<int[]>();

                grouped[filePath].Add(indices);
            }

            return grouped;
        }

        private void ProcessFileGroup(string filePath, List<int[]> indicesList, FileOperation operation)
        {
            _currentRealPath = filePath;
            Log(filePath, $"[FILE] {Path.GetFileName(filePath)}");

            foreach (var indices in indicesList)
            {
                if (indices.Length == 0)
                    Log(filePath, $"  path: {filePath}");
                else
                    Log(filePath, $"  path: {filePath}|{string.Join("|", indices)}");
            }

            // Базовый виртуальный путь для корня — сам filePath
            string baseVirtualPath = filePath;

            if (IsBnd(filePath))
                ProcessBnd(filePath, indicesList, operation, baseVirtualPath);
            else if (IsTpf(filePath))
                ProcessTpf(filePath, indicesList, operation, baseVirtualPath);
            else if (IsBxf(filePath))
                ProcessBxf(filePath, indicesList, operation, baseVirtualPath);
            else if (IsFlver(filePath))
                ProcessFlver(filePath, indicesList, operation, baseVirtualPath);        }

        private void ProcessInnerFile(BinderFile file, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            if (_depth >= MaxDepth)
            {
                LogError($"Max depth {MaxDepth} reached at {virtualBasePath}, skipping");
                return;
            }

            _depth++;
            try
            {
                if (indicesList.Count == 0 || indicesList.All(indices => indices.Length == 0))
                {
                    ProcessFileData(file, operation, virtualBasePath);
                    return;
                }

                if (IsBndData(file.Bytes))
                    ProcessBndData(file, indicesList, operation, virtualBasePath);
                else if (IsTpfData(file.Bytes))
                    ProcessTpfData(file, indicesList, operation, virtualBasePath);
                else if (IsBxfData(file.Bytes))
                    ProcessBxfData(file, indicesList, operation, virtualBasePath);
                else if (IsDcxData(file.Bytes))
                    ProcessDcxData(file, indicesList, operation, virtualBasePath);
            }
            finally
            {
                _depth--;
            }
        }

        private void ProcessFileData(BinderFile file, FileOperation operation, string virtualBasePath)
        {
            // Rename уже применён в ProcessBndCore/ProcessBxfCore до вызова ProcessInnerFile

            // ReplaceObject: просто заменяем байты и выходим — не нужно идти глубже
            if (operation.ReplaceObject && operation.NewObjectBytes.Length > 0)
            {
                file.Bytes = operation.NewObjectBytes;
                LogAction(virtualBasePath, "Replaced bytes");
                if (operation.GetObject) _mainObject = file;
                return;
            }

            // GetObject / UseDelegate / WriteObject — идём внутрь по типу содержимого
            if (IsFlvData(file.Bytes))
                ProcessFlverData(file, [[]], operation, virtualBasePath);
            else if (IsTpfData(file.Bytes))
                ProcessTpfData(file, [[]], operation, virtualBasePath);
            else if (IsBxfData(file.Bytes))
                ProcessBxfData(file, [[]], operation, virtualBasePath);
            else if (IsDcxData(file.Bytes))
                ProcessDcxData(file, [[]], operation, virtualBasePath);
            else if (operation.GetObject)
                _mainObject = file;
        }

        // ---------- BND ----------
        private void ProcessBnd(string path, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Log(virtualBasePath, $"[BND] {Path.GetFileName(path)}");
            var bnd = BND3.Read(path);
            ProcessBndCore(bnd, indicesList, operation, virtualBasePath);
            if (operation.WriteObject)
                bnd.Write(path);
        }

        private void ProcessBndData(BinderFile file, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Log(virtualBasePath, $"[BND] {Path.GetFileName(file.Name)}");
            var bnd = BND3.Read(file.Bytes);
            ProcessBndCore(bnd, indicesList, operation, virtualBasePath);
            if (operation.WriteObject)
                file.Bytes = bnd.Write();
        }
        private void ProcessBndCore(BND3 bnd, List<int[]> indicesList, FileOperation operation, string baseVirtualPath)
        {
            // Операции на самом контейнере (без индексов)
            if (indicesList.Any(indices => indices.Length == 0))
            {
                if (operation.AddObject)
                {
                    var newFile = new BinderFile
                    {
                        Name = string.IsNullOrEmpty(operation.NewObjectName) ? "NewFile" : operation.NewObjectName,
                        Bytes = operation.NewObjectBytes.Length > 0 ? operation.NewObjectBytes : []
                    };
                    bnd.Files.Add(newFile);
                    LogAction(baseVirtualPath, $"Added file to BND: {newFile.Name}");
                }
            }

            // Группируем по первому индексу
            var fileGroups = indicesList
                .Where(indices => indices.Length > 0)
                .GroupBy(indices => indices[0])
                .ToDictionary(g => g.Key, g => g.Select(indices => indices.Skip(1).ToArray()).ToList());

            // Удаляем в обратном порядке — индексы не сдвигаются для меньших значений
            var toRemoveIndices = fileGroups
                .Where(g => operation.RemoveObject && g.Value.All(i => i.Length == 0))
                .Select(g => g.Key)
                .Where(i => i >= 0 && i < bnd.Files.Count)
                .OrderByDescending(i => i)
                .ToList();

            foreach (var fileIndex in toRemoveIndices)
            {
                LogAction($"{baseVirtualPath}|{fileIndex}", "Removed from BND");
                bnd.Files.RemoveAt(fileIndex);
            }

            var toRemoveSet = new HashSet<int>(toRemoveIndices);

            // Для оставшихся операций корректируем индексы с учётом удалённых
            foreach (var group in fileGroups)
            {
                var originalIndex = group.Key;
                var innerIndices  = group.Value;

                if (toRemoveSet.Contains(originalIndex)) continue;

                // Скорректированный индекс: сколько удалённых было меньше текущего
                int adjustedIndex = originalIndex - toRemoveIndices.Count(r => r < originalIndex);

                if (adjustedIndex < 0 || adjustedIndex >= bnd.Files.Count) continue;

                var file = bnd.Files[adjustedIndex];
                string fullPath = $"{baseVirtualPath}|{originalIndex}";

                if (operation.RenameObject && innerIndices.All(i => i.Length == 0)
                    && !string.IsNullOrEmpty(operation.NewObjectName))
                {
                    file.Name = operation.NewObjectName;
                    LogAction(fullPath, $"Renamed → {file.Name}");
                }

                ProcessInnerFile(file, innerIndices, operation, fullPath);
            }
        }

        // ---------- TPF ----------
        private void ProcessTpf(string path, List<int[]> indicesList, FileOperation operation, string virtualBasePath)        {
            Log(virtualBasePath, $"[TPF] {Path.GetFileName(path)}");
            var tpf = TPF.Read(path);
            tpf = ProcessTpfCore(tpf, indicesList, operation, virtualBasePath);
            if (operation.WriteObject)
                tpf.Write(path);
        }

        private void ProcessTpfData(BinderFile file, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Log(virtualBasePath, $"[TPF] {Path.GetFileName(file.Name)}");
            var tpf = TPF.Read(file.Bytes);
            tpf = ProcessTpfCore(tpf, indicesList, operation, virtualBasePath);
            if (operation.WriteObject)
                file.Bytes = tpf.Write();
        }

        private TPF ProcessTpfCore(TPF tpf, List<int[]> indicesList, FileOperation operation, string baseVirtualPath)
        {
            // Сначала собираем индексы для удаления и удаляем в обратном порядке
            if (operation.RemoveObject)
            {
                var removeIndices = indicesList
                    .Where(i => i.Length > 0)
                    .Select(i => i[0])
                    .Where(i => i >= 0 && i < tpf.Textures.Count)
                    .Distinct()
                    .OrderByDescending(i => i);

                foreach (var idx in removeIndices)
                {
                    LogAction($"{baseVirtualPath}|{idx}", "Removed texture");
                    tpf.Textures.RemoveAt(idx);
                }
                // Операции без индекса (на весь TPF)
                if (indicesList.Any(i => i.Length == 0))
                {
                    if (operation.GetObject) _mainObject = tpf;
                    if (operation.ReplaceObject && operation.NewObjectBytes.Length > 0) tpf = TPF.Read(operation.NewObjectBytes);
                }
                return tpf;
            }

            foreach (var indices in indicesList)
            {
                if (indices.Length == 0)
                {
                    if (operation.GetObject) _mainObject = tpf;
                    if (operation.ReplaceObject && operation.NewObjectBytes.Length > 0) tpf = TPF.Read(operation.NewObjectBytes);

                    if (operation.AddObject)
                    {
                        tpf.Textures.Add(CreateTextureFromBytes(operation.NewObjectBytes, operation.NewObjectName, operation.NewTextureFormat));
                        LogAction(baseVirtualPath, "Added texture to TPF");
                    }
                    continue;
                }

                var textureIndex = indices[0];
                if (textureIndex < 0 || textureIndex >= tpf.Textures.Count) continue;

                string fullPath = $"{baseVirtualPath}|{textureIndex}";
                var texture = tpf.Textures[textureIndex];

                if (operation.ReplaceObject && operation.NewObjectBytes.Length > 0)
                {
                    if (!string.IsNullOrEmpty(operation.NewObjectName))
                        texture.Name = operation.NewObjectName;
                    texture.Bytes = operation.NewObjectBytes;
                    LogAction(fullPath, "Replaced texture");
                }

                if (operation.RenameObject && !string.IsNullOrEmpty(operation.NewObjectName))
                {
                    texture.Name = operation.NewObjectName;
                    LogAction(fullPath, $"Renamed texture → {texture.Name}");
                }

                if (operation.ChangeTextureFormat)
                {
                    texture.Format = operation.NewTextureFormat;
                    LogAction(fullPath, "Format changed");
                }

                if (operation.UseTexDelegate)
                    operation.AdditionalTextureProcessing?.Invoke(texture, fullPath);

                if (operation.GetObject)
                    _mainObject = texture;
            }
            return tpf;
        }

        private TPF.Texture CreateTextureFromBytes(byte[] bytes, string name, byte format)
        {
            return new TPF.Texture
            {
                Name = string.IsNullOrEmpty(name) ? "NewTexture" : name,
                Platform = TPF.TPFPlatform.PC,
                Bytes = bytes.Length > 0 ? bytes : DDSTools.fatcat,
                Format = format
            };
        }

        // ---------- BXF ----------
        private void ProcessBxf(string bhdPath, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Log(virtualBasePath, $"[BXF] {Path.GetFileName(bhdPath)}");
            var bdtPath = FindBdtPath(bhdPath);
            if (!File.Exists(bdtPath)) return;

            var bxf = BXF3.Read(bhdPath, bdtPath);
            ProcessBxfCore(bxf, indicesList, operation, virtualBasePath);

            if (operation.WriteObject)
                bxf.Write(bhdPath, bdtPath);
        }

        private void ProcessBxfData(BinderFile file, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Log(virtualBasePath, $"[BXF] {Path.GetFileName(file.Name)}");
            var bdtPath = FindBdtPathForFile(file);
            if (!File.Exists(bdtPath))
            {
                LogError($"BDT file not found for {virtualBasePath}: {bdtPath}");
                return;
            }

            var bxf = BXF3.Read(file.Bytes, bdtPath);
            ProcessBxfCore(bxf, indicesList, operation, virtualBasePath);

            if (operation.WriteObject)
            {
                bxf.Write(out var bhdBytes, out var bdtBytes);
                file.Bytes = bhdBytes;
                File.WriteAllBytes(bdtPath, bdtBytes);
            }
        }

        private void ProcessBxfCore(BXF3 bxf, List<int[]> indicesList, FileOperation operation, string baseVirtualPath)
        {
            // Операции на самом BXF (без индексов)
            if (indicesList.Any(indices => indices.Length == 0))
            {
                if (operation.AddObject)
                {
                    var newFile = new BinderFile
                    {
                        Name = string.IsNullOrEmpty(operation.NewObjectName) ? GenerateUniqueFileName(bxf, ".file") : operation.NewObjectName,
                        Bytes = operation.NewObjectBytes.Length > 0 ? operation.NewObjectBytes : []
                    };
                    bxf.Files.Add(newFile);
                    LogAction(baseVirtualPath, $"Added file to BXF: {newFile.Name}");
                }

                if (operation.AddTpfDcx)
                    AddTpfDcxToBxf(bxf, operation, baseVirtualPath);
            }

            var fileGroups = indicesList
                .Where(indices => indices.Length > 0)
                .GroupBy(indices => indices[0])
                .ToDictionary(g => g.Key, g => g.Select(indices => indices.Skip(1).ToArray()).ToList());

            // Удаляем в обратном порядке
            var toRemoveIndices = fileGroups
                .Where(g => operation.RemoveObject && g.Value.All(i => i.Length == 0))
                .Select(g => g.Key)
                .Where(i => i >= 0 && i < bxf.Files.Count)
                .OrderByDescending(i => i)
                .ToList();

            foreach (var fileIndex in toRemoveIndices)
            {
                LogAction($"{baseVirtualPath}|{fileIndex}", "Removed from BXF");
                bxf.Files.RemoveAt(fileIndex);
            }

            var toRemoveSet = new HashSet<int>(toRemoveIndices);

            foreach (var group in fileGroups)
            {
                var originalIndex = group.Key;
                var innerIndices  = group.Value;

                if (toRemoveSet.Contains(originalIndex)) continue;

                int adjustedIndex = originalIndex - toRemoveIndices.Count(r => r < originalIndex);

                if (adjustedIndex < 0 || adjustedIndex >= bxf.Files.Count) continue;

                var file = bxf.Files[adjustedIndex];
                string fullPath = $"{baseVirtualPath}|{originalIndex}";

                if (operation.RenameObject && innerIndices.All(i => i.Length == 0)
                    && !string.IsNullOrEmpty(operation.NewObjectName))
                {
                    file.Name = operation.NewObjectName;
                    LogAction(fullPath, $"Renamed → {file.Name}");
                }

                ProcessInnerFile(file, innerIndices, operation, fullPath);
            }
        }

        // ---------- FLVER ----------
        private void ProcessFlver(string path, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Log(virtualBasePath, $"[FLV] {Path.GetFileName(path)}");
            string currentPath = path;

            if (!indicesList.Any(indices => indices.Length == 0)) return;

            // ReplaceObject: записываем байты напрямую без лишней десериализации
            if (operation.ReplaceObject && operation.NewObjectBytes.Length > 0)
            {
                File.WriteAllBytes(currentPath, operation.NewObjectBytes);
                LogAction(virtualBasePath, "Replaced FLVER");
                if (operation.GetObject)
                {
                    try { _mainObject = FLVER2.Read(operation.NewObjectBytes); }
                    catch { _mainObject = operation.NewObjectBytes; }
                }
                return;
            }

            var flver = FLVER2.Read(path);

            if (operation.GetObject)
                GetFlverSafe(flver, currentPath, virtualBasePath);

            if (operation.RenameObject && !string.IsNullOrEmpty(operation.NewObjectName))
            {
                var dir = Path.GetDirectoryName(currentPath);
                var newPath = Path.Combine(dir ?? "", operation.NewObjectName);
                if (!currentPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(currentPath, newPath);
                    LogAction(virtualBasePath, $"Renamed FLVER → {newPath}");
                    currentPath = newPath;
                }
            }

            if (operation.UseFlverDelegate)
                operation.AdditionalFlverProcessing?.Invoke(flver, _currentRealPath, currentPath, _errorLogs);

            if (operation.RemoveObject)
            {
                File.Delete(currentPath);
                LogAction(virtualBasePath, "Removed FLVER");
                return;
            }

            if (operation.WriteObject)
            {
                byte[] original = File.ReadAllBytes(currentPath);
                WriteFlverSafe(flver, currentPath, original, virtualBasePath);
            }
        }

        private void ProcessFlverData(BinderFile file, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Log(virtualBasePath, $"[FLV] {Path.GetFileName(file.Name)}");

            if (!indicesList.Any(indices => indices.Length == 0)) return;

            // ReplaceObject: заменяем байты напрямую, без лишней десериализации
            if (operation.ReplaceObject && operation.NewObjectBytes.Length > 0)
            {
                file.Bytes = operation.NewObjectBytes;
                LogAction(virtualBasePath, "Replaced FLVER bytes");
                // GetObject после замены — возвращаем новый объект
                if (operation.GetObject)
                {
                    try { _mainObject = FLVER2.Read(file.Bytes); }
                    catch { _mainObject = file; }
                }
                return;
            }

            // Десериализуем только если нужна работа с содержимым
            var flver = FLVER2.Read(file.Bytes);

            if (operation.GetObject)
                GetFlverSafe(flver, file, virtualBasePath);

            string originalName = file.Name;
            if (operation.RenameObject && !string.IsNullOrEmpty(operation.NewObjectName))
                file.Name = operation.NewObjectName;

            if (operation.UseFlverDelegate)
                operation.AdditionalFlverProcessing?.Invoke(flver, virtualBasePath, originalName, _errorLogs);

            if (operation.WriteObject)
                file.Bytes = WriteFlverSafe(flver, file.Bytes, virtualBasePath);
        }

        // ---------- DCX ----------
        private void ProcessDcxData(BinderFile file, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Log(virtualBasePath, $"[DCX] {Path.GetFileName(file.Name)}");
            try
            {
                // Если нужно заменить весь DCX целиком — делаем это до декомпрессии
                if (indicesList.Any(i => i.Length == 0) && operation.ReplaceObject && operation.NewObjectBytes.Length > 0)
                {
                    file.Bytes = operation.NewObjectBytes;
                    LogAction(virtualBasePath, "Replaced DCX");
                    if (operation.GetObject) _mainObject = file;
                    return;
                }

                // Если операция только на контейнере (без погружения внутрь) — декомпрессируем для Add/Replace
                if (indicesList.All(i => i.Length == 0))
                {
                    if (operation.GetObject) _mainObject = file;
                    // AddObject — нужно декомпрессировать, добавить текстуру, перепаковать
                    if (operation.AddObject)
                    {
                        var decompressedAdd = DCX.Decompress(file.Bytes, out var dcxTypeAdd);
                        var tempFileAdd = new BinderFile { Bytes = decompressedAdd, Name = file.Name };
                        ProcessInnerFile(tempFileAdd, [[]], operation, virtualBasePath);
                        file.Bytes = DCX.Compress(tempFileAdd.Bytes, dcxTypeAdd);
                    }
                    return;
                }

                // Есть вложенные индексы — декомпрессируем и идём глубже
                var decompressed = DCX.Decompress(file.Bytes, out var dcxType);
                var tempFile = new BinderFile { Bytes = decompressed, Name = file.Name };

                var innerIndices = indicesList.Where(i => i.Length > 0).ToList();
                ProcessInnerFile(tempFile, innerIndices, operation, virtualBasePath);

                file.Bytes = DCX.Compress(tempFile.Bytes, dcxType);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to decompress DCX for {virtualBasePath}: {ex.Message}";
                LogError($"DCX decompress failed: {ex.Message}");
                _errorLogs.Add(errorMsg);
            }
        }

        // ---------- Вспомогательные методы ----------

        private void AddTpfDcxToBxf(BXF3 bxf, FileOperation operation, string baseVirtualPath)
        {
            LogAction(baseVirtualPath, "Adding TPF.DCX to BXF");
            try
            {
                var tpf = new TPF();
                tpf.Platform = TPF.TPFPlatform.PC;

                tpf.Textures.Add(CreateNewTexture(operation));

                var tpfBytes = tpf.Write();

                var dcxType = new DCX.DcxDfltCompressionInfo(0);
                var compressedTpf = DCX.Compress(tpfBytes, dcxType);

                var newFileName = string.IsNullOrEmpty(operation.NewTpfDcxArchiveName)
                    ? GenerateUniqueFileName(bxf, ".tpf.dcx")
                    : operation.NewTpfDcxArchiveName;

                var newFile = new BinderFile
                {
                    Name = newFileName,
                    Bytes = compressedTpf
                };

                bxf.Files.Add(newFile);
                LogAction(baseVirtualPath, $"Added TPF.DCX: {newFileName}");

                if (operation.GetObject)
                    _mainObject = newFile;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to add TPF.DCX to BXF at {baseVirtualPath}: {ex.Message}";
                LogError($"Failed to add TPF.DCX: {ex.Message}");
                _errorLogs.Add(errorMsg);
            }
        }

        private string GenerateUniqueFileName(BXF3 bxf, string extension)
        {
            var baseName = "new_file";
            var counter = 0;
            string fileName;
            do
            {
                fileName = $"{baseName}_{counter:000}{extension}";
                counter++;
            }
            while (bxf.Files.Any(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)));
            return fileName;
        }

        private static TPF.Texture CreateNewTexture(FileOperation operation)
        {
            return new TPF.Texture
            {
                Name = string.IsNullOrEmpty(operation.NewObjectName) ? "NewTex" : operation.NewObjectName.Split("tpf.dcx")[0],
                Platform = TPF.TPFPlatform.PC,
                Bytes = DDSTools.fatcat
            };
        }

        private void GetFlverSafe(FLVER2 flver, BinderFile file, string virtualBasePath)
        {
            // Не вызываем Write() для валидации — Read() уже прошёл, объект валиден для чтения.
            // Оригинальные байты сохраняем как fallback на случай если Write() упадёт позже.
            _mainObject = new FlverWithFallback(flver, file.Bytes);
        }

        private void GetFlverSafe(FLVER2 flver, string path, string virtualBasePath)
        {
            _mainObject = new FlverWithFallback(flver, File.ReadAllBytes(path));
        }

        private byte[] WriteFlverSafe(FLVER2 flver, byte[] original, string virtualBasePath)
        {
            try { return flver.Write(); }
            catch (Exception ex)
            {
                LogError($"Write failed at {virtualBasePath}: {ex.Message}");
                if (_useBytePatch)
                {
                    var fb = new FlverWithFallback(flver, original);
                    var patched = fb.TryBytePatch(msg => LogError(msg));
                    if (!ReferenceEquals(patched, original))
                    { LogAction(virtualBasePath, "Saved via byte patch"); return patched; }
                    LogError($"Byte patch not applicable (structural change) — original restored: {virtualBasePath}");
                }
                DumpFailedFlver(flver, virtualBasePath, ex.Message);
                _errorLogs.Add($"[WriteError] {virtualBasePath}: {ex.Message}");
                return original;
            }
        }

        private void WriteFlverSafe(FLVER2 flver, string path, byte[] original, string virtualBasePath)
        {
            try
            {
                flver.Write(path);
                LogAction(virtualBasePath, "Saved");
            }
            catch (Exception ex)
            {
                LogError($"Write failed at {virtualBasePath}: {ex.Message}");
                if (_useBytePatch)
                {
                    var fb = new FlverWithFallback(flver, original);
                    var patched = fb.TryBytePatch(msg => LogError(msg));
                    if (!ReferenceEquals(patched, original))
                    {
                        File.WriteAllBytes(path, patched);
                        LogAction(virtualBasePath, "Saved via byte patch");
                        return;
                    }
                    LogError($"Byte patch not applicable (structural change) — original restored: {virtualBasePath}");
                }
                DumpFailedFlver(flver, virtualBasePath, ex.Message);
                File.WriteAllBytes(path, original);
                _errorLogs.Add($"[WriteError] {virtualBasePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Сохраняет сломанный FLVER и текстовый отчёт в подпапку flver_dump/{timestamp}_{name}/ рядом с exe.
        /// </summary>
        private static void DumpFailedFlver(FLVER2 flver, string virtualBasePath, string errorMessage)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string realPath  = virtualBasePath.Split('|')[0];
                string fileName  = Path.GetFileName(realPath);
                string folderName = $"{timestamp}_{Path.GetFileNameWithoutExtension(fileName)}";

                string dumpDir = Path.Combine(AppContext.BaseDirectory, "flver_dump", folderName);
                Directory.CreateDirectory(dumpDir);

                string flverPath = Path.Combine(dumpDir, fileName);
                try { File.WriteAllBytes(flverPath, flver.Write()); }
                catch { flverPath = "(write failed — modified FLVER not saved)"; }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== FLVER Write Error Dump ===");
                sb.AppendLine($"Time:         {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"File name:    {fileName}");
                sb.AppendLine($"Virtual path: {virtualBasePath}");
                sb.AppendLine($"Real path:    {realPath}");
                sb.AppendLine($"Dump file:    {flverPath}");
                sb.AppendLine($"Error:        {errorMessage}");
                sb.AppendLine();
                sb.AppendLine($"--- Materials ({flver.Materials.Count}) ---");
                for (int i = 0; i < flver.Materials.Count; i++)
                {
                    var m = flver.Materials[i];
                    sb.AppendLine($"  [{i}] MTD={m.MTD}  Name={m.Name}  Textures={m.Textures.Count}");
                    foreach (var t in m.Textures)
                        sb.AppendLine($"       {t.ParamName}: {t.Path}");
                }
                File.WriteAllText(Path.Combine(dumpDir, "report.txt"), sb.ToString(), System.Text.Encoding.UTF8);

                Console.WriteLine($"[FlverDump] {dumpDir}");
            }
            catch (Exception dumpEx)
            {
                Console.WriteLine($"[FlverDump] Failed to dump: {dumpEx.Message}");
            }
        }

        private static string FindBdtPath(string bhdPath)
        {
            var bdtPath = bhdPath.Replace(".tpfbhd", ".tpfbdt", StringComparison.OrdinalIgnoreCase);
            return File.Exists(bdtPath) ? bdtPath : "";
        }

        private string FindBdtPathForFile(BinderFile file)
        {
            // file.Name хранит виртуальный путь внутри архива (N:\FRPG\...\c9990.chrtpfbhd)
            // Реальный .bdt лежит рядом с BND-файлом на диске
            string fileName = Path.GetFileName(file.Name);
            string realDir  = Path.GetDirectoryName(_currentRealPath) ?? "";

            if (fileName.EndsWith(".chrtpfbhd", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(realDir, fileName.Replace(".chrtpfbhd", ".chrtpfbdt", StringComparison.OrdinalIgnoreCase));

            if (fileName.EndsWith(".tpfbhd", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(realDir, fileName.Replace(".tpfbhd", ".tpfbdt", StringComparison.OrdinalIgnoreCase));

            return "";
        }

        // ---------- Проверки типов — делегируем в FileSignatures ----------
        private static bool IsBnd(string path)      => FileSignatures.IsBnd(path);
        private static bool IsTpf(string path)      => FileSignatures.IsTpf(path);
        private static bool IsFlver(string path)    => FileSignatures.IsFlver(path);
        private static bool IsBxf(string path)      => FileSignatures.IsBxf(path);
        private static bool IsBndData(byte[] data)  => FileSignatures.IsBndData(data);
        private static bool IsBxfData(byte[] data)  => FileSignatures.IsBxfData(data);
        private static bool IsTpfData(byte[] data)  => FileSignatures.IsTpfData(data);
        private static bool IsFlvData(byte[] data)  => FileSignatures.IsFlvData(data);
        private static bool IsDcxData(byte[] data)  => FileSignatures.IsDcxData(data);

        /// <summary>Возвращает объект, полученный при операции GetObject.</summary>
        public object? GetObject() => _mainObject;
        /// <summary>Возвращает список ошибок, возникших при обработке.</summary>
        public List<string> GetErrorLogs() => _errorLogs;
        /// <summary>Сбрасывает внутреннее состояние (объект и логи ошибок).</summary>
        public void Clear()
        {
            _mainObject = null;
            _errorLogs.Clear();
        }
    }

    /// <summary>
    /// Хранит FLVER2 объект вместе с оригинальными байтами.
    /// Используется как результат GetObject — позволяет редактировать FLVER
    /// и при ошибке Write() вернуть оригинал без потери данных.
    /// </summary>
    public class FlverWithFallback
    {
        public FLVER2  Flver         { get; }
        public byte[]  OriginalBytes { get; }

        public FlverWithFallback(FLVER2 flver, byte[] originalBytes)
        {
            Flver         = flver;
            OriginalBytes = originalBytes;
        }

        /// <summary>
        /// Сериализует FLVER. При ошибке пробует байтовый патч строк,
        /// при неудаче возвращает оригинальные байты.
        /// </summary>
        public byte[] WriteOrFallback(Action<string> onError = null)
        {
            try { return Flver.Write(); }
            catch (Exception ex)
            {
                onError?.Invoke($"Write failed: {ex.Message}, trying byte patch...");
                return TryBytePatch(onError);
            }
        }

        /// <summary>
        /// Собирает список изменённых строк (MTD, пути текстур) сравнивая
        /// текущий FLVER с оригиналом, и патчит байты напрямую.
        /// </summary>
        public byte[] TryBytePatch(Action<string> onError = null)
        {
            var patches = CollectChanges();
            if (patches.Count == 0)
            {
                onError?.Invoke("No string changes detected, returning original bytes");
                return OriginalBytes;
            }

            var (patched, failed) = FlverBytePatcher.Apply(OriginalBytes, patches);

            if (failed.Count > 0)
                onError?.Invoke($"Byte patch partial failures: {string.Join("; ", failed)}");

            return patched;
        }

        /// <summary>
        /// Сравнивает текущий FLVER с оригиналом и возвращает список патчей.
        /// Строки идут по порядку — каждый патч содержит номер вхождения.
        ///
        /// Особенность пути текстуры: "N:\path\name" — игра читает только имя после '\'.
        /// При изменении пути патчим только имя (последний сегмент), сохраняя префикс.
        ///
        /// Ограничение: byte patch работает только если структура FLVER не изменилась
        /// (количество материалов и текстур в каждом материале осталось прежним).
        /// Если структура изменилась — Write() обязателен.
        /// </summary>
        public List<FlverBytePatcher.StringPatch> CollectChanges()
        {
            var patches = new List<FlverBytePatcher.StringPatch>();

            FLVER2 original;
            try { original = FLVER2.Read(OriginalBytes); }
            catch { return patches; }

            // Если количество материалов изменилось — byte patch невозможен
            if (Flver.Materials.Count != original.Materials.Count)
            {
                Console.WriteLine($"[FlverBytePatcher] Material count changed ({original.Materials.Count} → {Flver.Materials.Count}), byte patch not applicable");
                return patches;
            }

            // Счётчики вхождений для каждой уникальной строки
            var occurrenceCounter = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < Flver.Materials.Count; i++)
            {
                var cur  = Flver.Materials[i];
                var orig = original.Materials[i];

                // Если количество текстур в материале изменилось — byte patch невозможен для этого материала
                if (cur.Textures.Count != orig.Textures.Count)
                {
                    Console.WriteLine($"[FlverBytePatcher] Material[{i}] texture count changed ({orig.Textures.Count} → {cur.Textures.Count}), byte patch not applicable");
                    return [];
                }

                // MTD
                if (!string.IsNullOrEmpty(orig.MTD) && orig.MTD != cur.MTD)
                {
                    int occ = GetAndIncrement(occurrenceCounter, orig.MTD);
                    patches.Add(new FlverBytePatcher.StringPatch(orig.MTD, cur.MTD ?? "", occ));
                }
                else if (!string.IsNullOrEmpty(orig.MTD))
                    GetAndIncrement(occurrenceCounter, orig.MTD);

                // Name материала
                if (!string.IsNullOrEmpty(orig.Name) && orig.Name != cur.Name)
                {
                    int occ = GetAndIncrement(occurrenceCounter, orig.Name);
                    patches.Add(new FlverBytePatcher.StringPatch(orig.Name, cur.Name ?? "", occ));
                }
                else if (!string.IsNullOrEmpty(orig.Name))
                    GetAndIncrement(occurrenceCounter, orig.Name);

                for (int j = 0; j < orig.Textures.Count; j++)
                {
                    var ct = cur.Textures[j];
                    var ot = orig.Textures[j];

                    // ParamName (g_Diffuse и т.д.)
                    if (!string.IsNullOrEmpty(ot.ParamName) && ot.ParamName != ct.ParamName)
                    {
                        int occ = GetAndIncrement(occurrenceCounter, ot.ParamName);
                        patches.Add(new FlverBytePatcher.StringPatch(ot.ParamName, ct.ParamName ?? "", occ));
                    }
                    else if (!string.IsNullOrEmpty(ot.ParamName))
                        GetAndIncrement(occurrenceCounter, ot.ParamName);

                    // Path текстуры
                    if (!string.IsNullOrEmpty(ot.Path) && ot.Path != ct.Path)
                    {
                        int occ = GetAndIncrement(occurrenceCounter, ot.Path);
                        patches.Add(new FlverBytePatcher.StringPatch(ot.Path, ct.Path ?? "", occ));
                    }
                    else if (!string.IsNullOrEmpty(ot.Path))
                        GetAndIncrement(occurrenceCounter, ot.Path);
                }
            }

            return patches;
        }

        private static int GetAndIncrement(Dictionary<string, int> counter, string key)
        {
            if (!counter.TryGetValue(key, out int val)) val = 0;
            counter[key] = val + 1;
            return val;
        }
    }

    /// <summary>Описывает операцию, выполняемую над файлом или архивом через FileBinders.</summary>
    public class FileOperation
    {
        // Общие операции
        public bool GetObject { get; set; }
        public bool WriteObject { get; set; }
        public bool ReplaceObject { get; set; }
        public bool RemoveObject { get; set; }
        public bool RenameObject { get; set; }
        public string NewObjectName { get; set; } = "";
        public byte[] NewObjectBytes { get; set; } = [];   // для AddObject и ReplaceObject
        public bool AddObject { get; set; }
        public bool ChangeTextureFormat { get; set; }
        public byte NewTextureFormat { get; set; }

        // BXF (специфичные)
        public bool AddTpfDcx { get; set; }
        public string NewTpfDcxArchiveName { get; set; } = "";

        // Делегаты
        public bool UseFlverDelegate { get; set; }
        public bool UseTexDelegate { get; set; }
        public bool UseBytePatchFallback { get; set; } = false;
        public Action<FLVER2, string, string, List<string>> AdditionalFlverProcessing { get; set; }
        public Action<TPF.Texture, string> AdditionalTextureProcessing { get; set; }
    }
}