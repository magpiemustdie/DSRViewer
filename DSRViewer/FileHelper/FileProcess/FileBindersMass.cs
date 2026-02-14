using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SoulsFormats;
using DSRViewer.FileHelper.FileExplorer.DDSHelper;
using System.Diagnostics;

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
            Console.WriteLine(_currentRealPath);

            if (IsBnd(filePath))
                ProcessBnd(filePath, indicesList, operation);
            else if (IsTpf(filePath))
                ProcessTpf(filePath, indicesList, operation);
            else if (IsBxf(filePath))
                ProcessBxf(filePath, indicesList, operation);
            else if (IsFlver(filePath))
                ProcessFlver(filePath, indicesList, operation);
        }

        private void ProcessInnerFile(BinderFile file, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing inner file {file.Name}");

            if (indicesList.Count == 0 || indicesList.All(indices => indices.Length == 0))
            {
                ProcessFileData(file, operation);
                return;
            }

            if (IsBndData(file.Bytes))
                ProcessBndData(file, indicesList, operation);
            else if (IsTpfData(file.Bytes))
                ProcessTpfData(file, indicesList, operation);
            else if (IsBxfData(file.Bytes))
                ProcessBxfData(file, indicesList, operation);
            else if (IsDcxData(file.Bytes))
                ProcessDcxData(file, indicesList, operation);
        }

        private void ProcessFileData(BinderFile file, FileOperation operation)
        {
            Console.WriteLine("Processing file data");

            // Общие операции с файлом внутри контейнера
            if (operation.RenameObject)
            {
                file.Name = operation.NewObjectName;
                Console.WriteLine($"Renamed inner file to {file.Name}");
            }

            if (operation.ReplaceObject && operation.NewObjectBytes.Length > 0)
            {
                file.Bytes = operation.NewObjectBytes;
                Console.WriteLine($"Replaced bytes of inner file {file.Name}");
            }

            // Специфическая обработка по типу данных
            if (IsFlvData(file.Bytes))
            {
                Console.WriteLine("Processing FLVER data");
                ProcessFlverData(file, operation);
            }
            else if (IsTpfData(file.Bytes))
            {
                Console.WriteLine("Processing TPF data");
                ProcessTpfData(file, [[]], operation);
            }
            else if (IsBxfData(file.Bytes))
            {
                Console.WriteLine("Processing BXF data");
                ProcessBxfData(file, [[]], operation);
            }
            else if (IsDcxData(file.Bytes))
            {
                Console.WriteLine("Processing DCX data");
                ProcessDcxData(file, [[]], operation);
            }
            else if (operation.GetObject)
            {
                _mainObject = file;
            }
        }

        // ---------- BND ----------
        private void ProcessBnd(string path, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing BND archive {path}");
            var bnd = BND3.Read(path);
            ProcessBndCore(bnd, indicesList, operation);
            if (operation.WriteObject)
                bnd.Write(path);
        }
        private void ProcessBndData(BinderFile file, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing BND data {file.Name}");
            var bnd = BND3.Read(file.Bytes);
            ProcessBndCore(bnd, indicesList, operation);
            if (operation.WriteObject)
                file.Bytes = bnd.Write();
        }
        private void ProcessBndCore(BND3 bnd, List<int[]> indicesList, FileOperation operation)
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
                    Console.WriteLine($"Added new file '{newFile.Name}' to BND");
                }
            }

            // Обработка конкретных файлов по индексам
            var fileGroups = indicesList
                .Where(indices => indices.Length > 0)
                .GroupBy(indices => indices[0])
                .ToDictionary(g => g.Key, g => g.Select(indices => indices.Skip(1).ToArray()).ToList());

            foreach (var i in fileGroups)
            {
                Console.WriteLine(i);
                foreach (var j in i.Value)
                {
                    Console.WriteLine(j);
                }
            }

            foreach (var group in fileGroups)
            {
                var fileIndex = group.Key;
                var innerIndices = group.Value;

                if (fileIndex < 0 || fileIndex >= bnd.Files.Count) continue;

                var file = bnd.Files[fileIndex];

                // Удаление файла
                if (operation.RemoveObject && innerIndices.Count == 1 && innerIndices[0].Length == 0)
                {
                    bnd.Files.RemoveAt(fileIndex);
                    Console.WriteLine($"Removed file at index {fileIndex} from BND");
                    continue;
                }

                // Переименование файла
                if (operation.RenameObject && innerIndices.Count == 1 && innerIndices[0].Length == 0)
                {
                    file.Name = operation.NewObjectName;
                    Console.WriteLine($"Renamed file at index {fileIndex} to {file.Name}");
                    continue;
                }

                // Рекурсивная обработка внутренностей файла
                ProcessInnerFile(file, innerIndices, operation);
            }
        }

        // ---------- TPF ----------
        private void ProcessTpf(string path, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing TPF archive {path}");
            var tpf = TPF.Read(path);
            ProcessTpfCore(tpf, indicesList, operation);
            if (operation.WriteObject)
                tpf.Write(path);
        }

        private void ProcessTpfData(BinderFile file, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing TPF data {file.Name}");
            var tpf = TPF.Read(file.Bytes);
            ProcessTpfCore(tpf, indicesList, operation);
            if (operation.WriteObject)
                file.Bytes = tpf.Write();
        }

        private void ProcessTpfCore(TPF tpf, List<int[]> indicesList, FileOperation operation)
        {
            foreach (var indices in indicesList)
            {
                if (indices.Length == 0)
                {
                    // Операции на самом TPF
                    if (operation.GetObject) _mainObject = tpf;
                    if (operation.ReplaceObject) tpf = TPF.Read(operation.NewObjectBytes);

                    // Общее добавление объекта (текстуры)
                    if (operation.AddObject)
                    {
                        tpf.Textures.Add(CreateTextureFromBytes(operation.NewObjectBytes, operation.NewObjectName));
                        Console.WriteLine($"Added new texture via AddObject");
                    }
                    continue;
                }

                var textureIndex = indices[0];
                if (textureIndex >= 0 && textureIndex < tpf.Textures.Count)
                {
                    var texture = tpf.Textures[textureIndex];

                    // Удаление текстуры (общее)
                    if (operation.RemoveObject)
                    {
                        tpf.Textures.RemoveAt(textureIndex);
                        Console.WriteLine($"Removed texture at index {textureIndex} via RemoveObject");
                        continue;
                    }

                    if (operation.ReplaceObject)
                    {
                        texture.Name = operation.NewObjectName;
                        texture.Bytes = operation.NewObjectBytes;
                        Console.WriteLine($"Replaced texture at index {textureIndex} to {texture.Name} via RenameObject");
                        continue;
                    }

                    // Переименование текстуры (общее)
                    if (operation.RenameObject)
                    {
                        texture.Name = operation.NewObjectName;
                        Console.WriteLine($"Renamed texture at index {textureIndex} to {texture.Name} via RenameObject");
                        continue;
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
        private void ProcessBxf(string bhdPath, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing BXF archive {bhdPath}");
            var bdtPath = FindBdtPath(bhdPath);
            if (!File.Exists(bdtPath)) return;

            var bxf = BXF3.Read(bhdPath, bdtPath);
            ProcessBxfCore(bxf, indicesList, operation);

            if (operation.WriteObject)
                bxf.Write(bhdPath, bdtPath);
        }

        private void ProcessBxfData(BinderFile file, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing BXF data {file.Name}");
            var bdtPath = FindBdtPathForFile(file);
            if (!File.Exists(bdtPath)) return;

            var bxf = BXF3.Read(file.Bytes, bdtPath);
            ProcessBxfCore(bxf, indicesList, operation);

            if (operation.WriteObject)
            {
                bxf.Write(out var bhdBytes, out var bdtBytes);
                file.Bytes = bhdBytes;
                File.WriteAllBytes(bdtPath, bdtBytes);
            }
        }

        private void ProcessBxfCore(BXF3 bxf, List<int[]> indicesList, FileOperation operation)
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
                    Console.WriteLine($"Added new file '{newFile.Name}' to BXF via AddObject");
                }

                // Специфичное добавление TPF.DCX
                if (operation.AddTpfDcx)
                    AddTpfDcxToBxf(bxf, operation);
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

                // Удаление файла
                if (operation.RemoveObject && innerIndices.Count == 1 && innerIndices[0].Length == 0)
                {
                    bxf.Files.RemoveAt(fileIndex);
                    Console.WriteLine($"Removed file at index {fileIndex} from BXF via RemoveObject");
                    continue;
                }

                // Переименование файла
                if (operation.RenameObject && innerIndices.Count == 1 && innerIndices[0].Length == 0)
                {
                    file.Name = operation.NewObjectName;
                    Console.WriteLine($"Renamed file at index {fileIndex} to {file.Name} via RenameObject");
                    continue;
                }

                ProcessInnerFile(file, innerIndices, operation);
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
                        Console.WriteLine($"Removed tpf.dcx at index {tpfDcxIndex} via RemoveTpfDcx");
                    }
                }
            }
        }

        // ---------- FLVER ----------
        private void ProcessFlver(string path, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing FLVER file {path}");
            var flver = FLVER2.Read(path);

            // Операции на самом файле
            if (indicesList.Any(indices => indices.Length == 0))
            {
                if (operation.GetObject) _mainObject = flver;
                if (operation.ReplaceObject) flver = FLVER2.Read(operation.NewObjectBytes);
                if (operation.UseFlverDelegate)
                    operation.AdditionalFlverProcessing?.Invoke(flver, _currentRealPath, path);

                // Переименование файла на диске
                if (operation.RenameObject && !string.IsNullOrEmpty(operation.NewObjectName))
                {
                    var dir = Path.GetDirectoryName(path);
                    var newPath = Path.Combine(dir ?? "", operation.NewObjectName);
                    if (!path.Equals(newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Move(path, newPath);
                        Console.WriteLine($"Renamed FLVER file to {newPath}");
                        path = newPath; // обновляем для последующей записи
                    }
                }

                // Удаление файла (осторожно!)
                if (operation.RemoveObject)
                {
                    File.Delete(path);
                    Console.WriteLine($"Deleted FLVER file {path}");
                    return; // файл удалён, запись не нужна
                }
            }

            if (operation.WriteObject)
            {
                byte[] original = File.ReadAllBytes(path);
                WriteFlverSafe(flver, path, original);
            }
        }

        private void ProcessFlverData(BinderFile file, FileOperation operation)
        {
            Console.WriteLine($"Processing FLVER file {file.Name}");
            var flver = FLVER2.Read(file.Bytes);

            if (operation.GetObject) _mainObject = flver;
            if (operation.ReplaceObject) flver = FLVER2.Read(operation.NewObjectBytes);
            if (operation.UseFlverDelegate)
                operation.AdditionalFlverProcessing?.Invoke(flver, _currentRealPath, file.Name);

            // Переименование файла внутри контейнера
            if (operation.RenameObject)
                file.Name = operation.NewObjectName;

            if (operation.WriteObject)
                file.Bytes = WriteFlverSafe(flver, file.Bytes, file.Name);
        }

        // ---------- DCX ----------
        private void ProcessDcxData(BinderFile file, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing DCX data {file.Name}");
            try
            {
                var decompressed = DCX.Decompress(file.Bytes, out var dcxType);
                var tempFile = new BinderFile { Bytes = decompressed, Name = file.Name };

                ProcessInnerFile(tempFile, indicesList, operation);

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
                        Console.WriteLine($"Replaced DCX data of {file.Name}");
                        return; // уже записали новые сжатые данные
                    }
                }

                // Сжатие обратно, если были изменения в tempFile
                file.Bytes = DCX.Compress(tempFile.Bytes, dcxType);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to decompress DCX for {file.Name}: {ex.Message}";
                Console.WriteLine(errorMsg);
                _errorLogs.Add(errorMsg);
            }
        }

        // ---------- Вспомогательные методы ----------

        private void AddTpfDcxToBxf(BXF3 bxf, FileOperation operation)
        {
            Console.WriteLine($"Adding TPF.DCX archive to BXF");
            try
            {
                var tpf = new TPF();
                tpf.Platform = TPF.TPFPlatform.PC;

                tpf.Textures.Add(CreateNewTexture(operation));

                var tpfBytes = tpf.Write();

                var dcxType = DCX.Type.DCX_DFLT_10000_24_9;
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
                Console.WriteLine($"Added TPF.DCX archive as {newFileName} to BXF");

                if (operation.GetObject)
                    _mainObject = newFile;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to add TPF.DCX to BXF: {ex.Message}";
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

        private byte[] WriteFlverSafe(FLVER2 flver, byte[] original, string context)
        {
            try
            {
                return flver.Write();
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to write FLVER (context: {context}): {ex.Message}";
                Console.WriteLine(errorMsg);
                _errorLogs.Add(errorMsg);
                return original;
            }
        }

        private void WriteFlverSafe(FLVER2 flver, string path, byte[] original)
        {
            try
            {
                flver.Write(path);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to write FLVER to {path}: {ex.Message}";
                Console.WriteLine(errorMsg);
                _errorLogs.Add(errorMsg);
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

        // Для обратной совместимости (замена всего объекта)
        //public byte[] NewBytes { get; set; } = [];

        // FLVER
        //public bool WriteFlver { get; set; }
        //public bool ReplaceFlver { get; set; }
        //public FLVER2 NewFlver { get; set; } = new();

        // TPF (специфичные)
        //public bool AddTexture { get; set; }
        //public bool RemoveTexture { get; set; }
        //public bool ReplaceTexture { get; set; }
        //public bool RenameTexture { get; set; }
        public bool ChangeTextureFormat { get; set; }
        //public byte[] NewTextureBytes { get; set; } = [];
        public byte NewTextureFormat { get; set; }
        //public string NewTextureName { get; set; } = "";

        // BXF (специфичные)
        public bool AddTpfDcx { get; set; }
        public bool RemoveTpfDcx { get; set; }
        public string NewTpfDcxArchiveName { get; set; } = "";

        // Делегаты
        public bool UseFlverDelegate { get; set; }
        public bool UseTexDelegate { get; set; }
        public Action<FLVER2, string, string> AdditionalFlverProcessing { get; set; }
        public Action<TPF.Texture, string> AdditionalTexProcessing { get; set; }
    }
}