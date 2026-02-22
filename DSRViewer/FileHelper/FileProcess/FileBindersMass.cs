using DSRViewer.FileHelper.FileExplorer.DDSHelper;
using Org.BouncyCastle.Utilities;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Vortice.Direct3D11;

namespace DSRViewer.FileHelper
{
    public class FileBinders
    {
        private string _currentRealPath = "";
        private object? _mainObject;
        private List<string> _errorLogs = [];

        public void ProcessPaths(IEnumerable<string> virtualPaths, FileOperation operation)
        {
            _errorLogs.Clear();
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
                    Console.WriteLine(errorMsg);
                    _errorLogs.Add(errorMsg);
                }
            }

            File.WriteAllLines("_errorLogs.txt", _errorLogs);
        }

        private Dictionary<string, List<int[]>> GroupPaths(IEnumerable<string> paths)
        {
            var grouped = new Dictionary<string, List<int[]>>();

            foreach (var path in paths)
            {
                var segments = path.Split('|');
                if (segments.Length == 0) continue;

                var filePath = segments[0];
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
            Console.WriteLine($"Processing file group: {filePath}");

            // Выводим все виртуальные пути, которые будут обработаны
            foreach (var indices in indicesList)
            {
                if (indices.Length == 0)
                    Console.WriteLine($"  Virtual path: {filePath}");
                else
                    Console.WriteLine($"  Virtual path: {filePath}|{string.Join("|", indices)}");
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
                ProcessFlver(filePath, indicesList, operation, baseVirtualPath);
        }

        private void ProcessInnerFile(BinderFile file, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Console.WriteLine($"Processing inner file at {virtualBasePath}");

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

        private void ProcessFileData(BinderFile file, FileOperation operation, string virtualBasePath)
        {
            Console.WriteLine($"Processing file data at {virtualBasePath}");

            // Общие операции с файлом внутри контейнера
            if (operation.RenameObject)
            {
                file.Name = operation.NewObjectName;
                Console.WriteLine($"Renamed inner file at {virtualBasePath} to {file.Name}");
            }

            if (operation.ReplaceObject && operation.NewObjectBytes.Length > 0)
            {
                file.Bytes = operation.NewObjectBytes;
                Console.WriteLine($"Replaced bytes of inner file at {virtualBasePath}");
            }

            // Специфическая обработка по типу данных
            if (IsFlvData(file.Bytes))
            {
                Console.WriteLine($"Processing FLVER data at {virtualBasePath}");
                ProcessFlverData(file, [[]], operation, virtualBasePath);
            }
            else if (IsTpfData(file.Bytes))
            {
                Console.WriteLine($"Processing TPF data at {virtualBasePath}");
                ProcessTpfData(file, [[]], operation, virtualBasePath);
            }
            else if (IsBxfData(file.Bytes))
            {
                Console.WriteLine($"Processing BXF data at {virtualBasePath}");
                ProcessBxfData(file, [[]], operation, virtualBasePath);
            }
            else if (IsDcxData(file.Bytes))
            {
                Console.WriteLine($"Processing DCX data at {virtualBasePath}");
                ProcessDcxData(file, [[]], operation, virtualBasePath);
            }
            else if (operation.GetObject)
            {
                _mainObject = file;
            }
        }

        // ---------- BND ----------
        private void ProcessBnd(string path, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Console.WriteLine($"Processing BND archive at {virtualBasePath}");
            var bnd = BND3.Read(path);
            ProcessBndCore(bnd, indicesList, operation, virtualBasePath);
            if (operation.WriteObject)
                bnd.Write(path);
        }
        private void ProcessBndData(BinderFile file, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Console.WriteLine($"Processing BND data at {virtualBasePath}");
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
                    Console.WriteLine($"Added new file to BND at {baseVirtualPath}");
                }
            }

            // Обработка конкретных файлов по индексам
            var fileGroups = indicesList
                .Where(indices => indices.Length > 0)
                .GroupBy(indices => indices[0])
                .ToDictionary(g => g.Key, g => g.Select(indices => indices.Skip(1).ToArray()).ToList());

            foreach (var group in fileGroups)
            {
                var fileIndex = group.Key;
                var innerIndices = group.Value;

                if (fileIndex < 0 || fileIndex >= bnd.Files.Count) continue;

                var file = bnd.Files[fileIndex];
                string fullPath = $"{baseVirtualPath}|{fileIndex}";

                // Удаление файла
                if (operation.RemoveObject && innerIndices.Count == 1 && innerIndices[0].Length == 0)
                {
                    bnd.Files.RemoveAt(fileIndex);
                    Console.WriteLine($"Removed file at {fullPath} from BND");
                    continue;
                }

                // Переименование файла
                if (operation.RenameObject && innerIndices.Count == 1 && innerIndices[0].Length == 0)
                {
                    file.Name = operation.NewObjectName;
                    Console.WriteLine($"Renamed file at {fullPath} to {file.Name}");
                }

                // Рекурсивная обработка внутренностей файла
                ProcessInnerFile(file, innerIndices, operation, fullPath);
            }
        }

        // ---------- TPF ----------
        private void ProcessTpf(string path, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Console.WriteLine($"Processing TPF archive at {virtualBasePath}");
            var tpf = TPF.Read(path);
            ProcessTpfCore(tpf, indicesList, operation, virtualBasePath);
            if (operation.WriteObject)
                tpf.Write(path);
        }

        private void ProcessTpfData(BinderFile file, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Console.WriteLine($"Processing TPF data at {virtualBasePath}");
            var tpf = TPF.Read(file.Bytes);
            ProcessTpfCore(tpf, indicesList, operation, virtualBasePath);
            if (operation.WriteObject)
                file.Bytes = tpf.Write();
        }

        private void ProcessTpfCore(TPF tpf, List<int[]> indicesList, FileOperation operation, string baseVirtualPath)
        {
            foreach (var indices in indicesList)
            {
                if (indicesList.Any(indices => indices.Length == 0))
                {
                    // Операции на самом TPF
                    if (operation.GetObject) _mainObject = tpf;
                    if (operation.ReplaceObject) tpf = TPF.Read(operation.NewObjectBytes);

                    // Общее добавление объекта (текстуры)
                    if (operation.AddObject)
                    {
                        tpf.Textures.Add(CreateTextureFromBytes(operation.NewObjectBytes, operation.NewObjectName));
                        Console.WriteLine($"Added new texture via AddObject to TPF at {baseVirtualPath}");
                    }
                    continue;
                }

                var textureIndex = indices[0];
                if (textureIndex >= 0 && textureIndex < tpf.Textures.Count)
                {
                    string fullPath = $"{baseVirtualPath}|{textureIndex}";

                    Console.WriteLine($"Processing Texture data at {fullPath}");
                    var texture = tpf.Textures[textureIndex];
                    
                    // Удаление текстуры (общее)
                    if (operation.RemoveObject)
                    {
                        tpf.Textures.RemoveAt(textureIndex);
                        Console.WriteLine($"Removed texture at {fullPath} via RemoveObject");
                        continue;
                    }

                    if (operation.ReplaceObject)
                    {
                        texture.Name = operation.NewObjectName;
                        texture.Bytes = operation.NewObjectBytes;
                        Console.WriteLine($"Replaced texture at {fullPath} via ReplaceObject");
                    }

                    // Переименование текстуры (общее)
                    if (operation.RenameObject)
                    {
                        texture.Name = operation.NewObjectName;
                        Console.WriteLine($"Renamed texture at {fullPath} to {texture.Name} via RenameObject");
                    }

                    if (operation.ChangeTextureFormat)
                    {
                        texture.Format = operation.NewTextureFormat;
                    }

                    if (operation.GetObject)
                    {
                        _mainObject = texture;
                    }
                }
            }
        }

        private TPF.Texture CreateTextureFromBytes(byte[] bytes, string name)
        {
            return new TPF.Texture
            {
                Name = string.IsNullOrEmpty(name) ? "NewTexture" : name,
                Platform = TPF.TPFPlatform.PC,
                Bytes = bytes.Length > 0 ? bytes : DDSTools.fatcat
            };
        }

        // ---------- BXF ----------
        private void ProcessBxf(string bhdPath, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Console.WriteLine($"Processing BXF archive at {virtualBasePath}");
            var bdtPath = FindBdtPath(bhdPath);
            if (!File.Exists(bdtPath)) return;

            var bxf = BXF3.Read(bhdPath, bdtPath);
            ProcessBxfCore(bxf, indicesList, operation, virtualBasePath);

            if (operation.WriteObject)
                bxf.Write(bhdPath, bdtPath);
        }

        private void ProcessBxfData(BinderFile file, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Console.WriteLine($"Processing BXF data at {virtualBasePath}");
            var bdtPath = FindBdtPathForFile(file);
            if (!File.Exists(bdtPath)) return;

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
                    Console.WriteLine($"Added new file to BXF at {baseVirtualPath} via AddObject");
                }

                // Специфичное добавление TPF.DCX
                if (operation.AddTpfDcx)
                    AddTpfDcxToBxf(bxf, operation, baseVirtualPath);
            }

            // Обработка конкретных файлов по индексам
            var fileGroups = indicesList
                .Where(indices => indices.Length > 0)
                .GroupBy(indices => indices[0])
                .ToDictionary(g => g.Key, g => g.Select(indices => indices.Skip(1).ToArray()).ToList());

            foreach (var group in fileGroups)
            {
                var fileIndex = group.Key;
                var innerIndices = group.Value;

                if (fileIndex < 0 || fileIndex >= bxf.Files.Count) continue;

                var file = bxf.Files[fileIndex];
                string fullPath = $"{baseVirtualPath}|{fileIndex}";

                // Удаление файла
                if (operation.RemoveObject && innerIndices.Count == 1 && innerIndices[0].Length == 0)
                {
                    bxf.Files.RemoveAt(fileIndex);
                    Console.WriteLine($"Removed file at {fullPath} from BXF via RemoveObject");
                    continue;
                }

                // Переименование файла
                if (operation.RenameObject && innerIndices.Count == 1 && innerIndices[0].Length == 0)
                {
                    file.Name = operation.NewObjectName;
                    Console.WriteLine($"Renamed file at {fullPath} to {file.Name} via RenameObject");
                }

                ProcessInnerFile(file, innerIndices, operation, fullPath);
            }

            // Специфичное удаление TPF.DCX (для обратной совместимости)
            if (operation.RemoveTpfDcx && indicesList.Count == 1)
            {
                var indices = indicesList[0];
                if (indices.Length == 1)
                {
                    var tpfDcxIndex = indices[0];
                    if (tpfDcxIndex >= 0 && tpfDcxIndex < bxf.Files.Count)
                    {
                        bxf.Files.RemoveAt(tpfDcxIndex);
                        Console.WriteLine($"Removed tpf.dcx at {baseVirtualPath}|{tpfDcxIndex} via RemoveTpfDcx");
                    }
                }
            }
        }

        // ---------- FLVER ----------
        private void ProcessFlver(string path, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Console.WriteLine($"Processing FLVER file at {virtualBasePath}");
            var flver = FLVER2.Read(path);

            // Операции на самом файле
            if (indicesList.Any(indices => indices.Length == 0))
            {
                if (operation.GetObject)
                    GetFlverSafe(flver, path, virtualBasePath);
                if (operation.ReplaceObject)
                {
                    try
                    {
                        var temp = flver.Write();
                        Console.WriteLine($"Read normal flver at {virtualBasePath}");
                        flver = FLVER2.Read(operation.NewObjectBytes);
                        Console.WriteLine($"Replaced FLVER file at {virtualBasePath}");
                    }
                    catch
                    {
                        Console.WriteLine($"Broken flver at {virtualBasePath}");
                        flver = FLVER2.Read(operation.NewObjectBytes);
                        File.WriteAllBytes(path, operation.NewObjectBytes);
                        Console.WriteLine($"Replaced FLVER file bytes at {virtualBasePath}");
                    }
                }

                // Переименование файла на диске
                if (operation.RenameObject && !string.IsNullOrEmpty(operation.NewObjectName))
                {
                    var dir = Path.GetDirectoryName(path);
                    var newPath = Path.Combine(dir ?? "", operation.NewObjectName);
                    if (!path.Equals(newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Move(path, newPath);
                        Console.WriteLine($"Renamed FLVER file at {virtualBasePath} to {newPath}");
                        path = newPath; // обновляем для последующей записи
                    }
                }

                if (operation.UseFlverDelegate)
                    operation.AdditionalFlverProcessing?.Invoke(flver, _currentRealPath, path, _errorLogs);

                // Удаление файла (осторожно!)
                if (operation.RemoveObject)
                {
                    File.Delete(path);
                    Console.WriteLine($"Removed FLVER file at {virtualBasePath}");
                    return; // файл удалён, запись не нужна
                }

                if (operation.WriteObject)
                {
                    byte[] original = File.ReadAllBytes(path);
                    WriteFlverSafe(flver, path, original, virtualBasePath);
                }
            }
        }

        private void ProcessFlverData(BinderFile file, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Console.WriteLine($"Processing FLVER data at {virtualBasePath}");
            var flver = FLVER2.Read(file.Bytes);
            if (indicesList.Any(indices => indices.Length == 0))
            {

                if (operation.GetObject)
                    GetFlverSafe(flver, file, virtualBasePath);

                if (operation.ReplaceObject)
                {
                    try
                    {
                        var temp = flver.Write();
                        Console.WriteLine($"Read normal flver at {virtualBasePath}");
                        flver = FLVER2.Read(operation.NewObjectBytes);
                        Console.WriteLine($"Replaced FLVER file at {virtualBasePath}");
                    }
                    catch
                    {
                        Console.WriteLine($"Read broken flver at {virtualBasePath}");
                        flver = FLVER2.Read(operation.NewObjectBytes);
                        file.Bytes = operation.NewObjectBytes;
                        Console.WriteLine($"Replaced FLVER file bytes at {virtualBasePath}");
                    }
                }

                // Переименование файла внутри контейнера
                if (operation.RenameObject)
                    file.Name = operation.NewObjectName;

                if (operation.UseFlverDelegate)
                    operation.AdditionalFlverProcessing?.Invoke(flver, virtualBasePath, file.Name, _errorLogs);

                if (operation.WriteObject)
                    file.Bytes = WriteFlverSafe(flver, file.Bytes, virtualBasePath);
            }
        }

        // ---------- DCX ----------
        private void ProcessDcxData(BinderFile file, List<int[]> indicesList, FileOperation operation, string virtualBasePath)
        {
            Console.WriteLine($"Processing DCX data at {virtualBasePath}");
            try
            {
                var decompressed = DCX.Decompress(file.Bytes, out var dcxType);
                var tempFile = new BinderFile { Bytes = decompressed, Name = file.Name };

                // Рекурсивно обрабатываем содержимое с тем же виртуальным путём
                ProcessInnerFile(tempFile, indicesList, operation, virtualBasePath);

                // Общие операции с самим DCX (без индексов)
                if (indicesList.Any(indices => indices.Length == 0))
                {
                    if (operation.RenameObject)
                        file.Name = operation.NewObjectName;

                    if (operation.GetObject)
                        _mainObject = file;

                    // Замена содержимого DCX
                    if (operation.ReplaceObject && operation.NewObjectBytes.Length > 0)
                    {
                        file.Bytes = operation.NewObjectBytes;
                        Console.WriteLine($"Replaced DCX data at {virtualBasePath}");
                        return; // уже записали новые сжатые данные
                    }
                }

                // Сжатие обратно, если были изменения в tempFile
                file.Bytes = DCX.Compress(tempFile.Bytes, dcxType);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to decompress DCX for {virtualBasePath}: {ex.Message}";
                Console.WriteLine(errorMsg);
                _errorLogs.Add(errorMsg);
            }
        }

        // ---------- Вспомогательные методы ----------

        private void AddTpfDcxToBxf(BXF3 bxf, FileOperation operation, string baseVirtualPath)
        {
            Console.WriteLine($"Adding TPF.DCX archive to BXF at {baseVirtualPath}");
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
                Console.WriteLine($"Added TPF.DCX archive as {newFileName} to BXF at {baseVirtualPath}");

                if (operation.GetObject)
                    _mainObject = newFile;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to add TPF.DCX to BXF at {baseVirtualPath}: {ex.Message}";
                Console.WriteLine(errorMsg);
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
            try
            {
                byte[] temp = flver.Write();
                _mainObject = flver;
            }
            catch
            {
                _mainObject = file;
                Console.WriteLine($"Read broken FLVER at {virtualBasePath}");
            }
        }
        private void GetFlverSafe(FLVER2 flver, string path, string virtualBasePath)
        {
            try
            {
                byte[] temp = flver.Write();
                _mainObject = flver;
            }
            catch
            {
                _mainObject = File.ReadAllBytes(path);
                Console.WriteLine($"Read broken FLVER at {virtualBasePath}");
            }
        }

        private byte[] WriteFlverSafe(FLVER2 flver, byte[] original, string virtualBasePath)
        {
            try
            {
                return flver.Write();
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to write FLVER at {virtualBasePath}: {ex.Message}";
                Console.WriteLine(errorMsg);
                _errorLogs.Add(errorMsg);
                Console.WriteLine("Try to write original bytes");
                return original;
            }
        }

        private void WriteFlverSafe(FLVER2 flver, string path, byte[] original, string virtualBasePath)
        {
            try
            {
                flver.Write(path);
                Console.WriteLine($"Successfully saved(?) changes to: {virtualBasePath}");

            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to write FLVER to {virtualBasePath}: {ex.Message}";
                Console.WriteLine(errorMsg);
                _errorLogs.Add(errorMsg);
                Console.WriteLine("Try to write original bytes");
                File.WriteAllBytes(path, original);
            }
        }

        private string FindBdtPath(string bhdPath)
        {
            var possiblePaths = new[]
            {
                bhdPath.Replace(".tpfbhd", ".tpfbdt", StringComparison.OrdinalIgnoreCase)
            };
            return possiblePaths.FirstOrDefault(File.Exists) ?? "";
        }

        private string FindBdtPathForFile(BinderFile file)
        {
            var basePath = Path.GetDirectoryName(_currentRealPath) ?? "";
            var name = file.Name.Split("\\").Last();

            if (name.EndsWith(".chrtpfbhd", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(basePath, name.Replace(".chrtpfbhd", ".chrtpfbdt", StringComparison.OrdinalIgnoreCase));

            return "";
        }

        // ---------- Проверки типов ----------
        private static bool IsBnd(string path) => HasExtension(path,
            ".chrbnd", ".partsbnd", ".ffxbnd", ".rumblebnd", ".objbnd", ".fgbnd", ".msgbnd", ".mtdbnd", ".anibnd", ".chresdbnd", ".remobnd", ".shaderbnd", ".parambnd");
        private static bool IsTpf(string path) => HasExtension(path, ".tpf");
        private static bool IsFlver(string path) => HasExtension(path, ".flver");
        private static bool IsBxf(string path) => HasExtension(path, ".tpfbhd");
        private static bool HasExtension(string path, params string[] extensions)
        {
            var pathLower = path.ToLowerInvariant();
            return extensions.Any(ext =>
                pathLower.EndsWith(ext.ToLowerInvariant()) ||
                pathLower.EndsWith(ext.ToLowerInvariant() + ".dcx"));
        }

        private static bool IsBndData(byte[] data) => data.Length >= 4 && data[0] == 'B' && data[1] == 'N' && data[2] == 'D' && data[3] == '3';
        private static bool IsBxfData(byte[] data) => data.Length >= 4 && data[0] == 'B' && data[1] == 'H' && data[2] == 'F' && data[3] == '3';
        private static bool IsTpfData(byte[] data) => data.Length >= 3 && data[0] == 'T' && data[1] == 'P' && data[2] == 'F';
        private static bool IsFlvData(byte[] data) => data.Length >= 5 && data[0] == 'F' && data[1] == 'L' && data[2] == 'V' && data[3] == 'E' && data[4] == 'R';
        private static bool IsDcxData(byte[] data) => data.Length >= 3 && data[0] == 'D' && data[1] == 'C' && data[2] == 'X';

        public object? GetObject() => _mainObject;
        public List<string> GetErrorLogs() => _errorLogs;
        public void Clear()
        {
            _mainObject = null;
            _errorLogs.Clear();
        }
    }

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
        public bool RemoveTpfDcx { get; set; }
        public string NewTpfDcxArchiveName { get; set; } = "";

        // Делегаты
        public bool UseFlverDelegate { get; set; }
        public bool UseTexDelegate { get; set; }
        public Action<FLVER2, string, string, List<string>> AdditionalFlverProcessing { get; set; }
        public Action<TPF.Texture, string> AdditionalTexProcessing { get; set; }
    }
}