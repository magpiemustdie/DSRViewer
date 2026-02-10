using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SoulsFormats;
using DSRViewer.FileHelper.FileExplorer.DDSHelper;

namespace DSRViewer.FileHelper
{
    public class FileBinders
    {
        private string _currentRealPath = "";
        private object? _mainObject;
        private List<string> _errorLogs = new List<string>();

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

            if (IsBnd(filePath))
                ProcessBnd(filePath, indicesList, operation);
            else if (IsTpf(filePath))
                ProcessTpf(filePath, indicesList, operation);
            else if (IsFlver(filePath))
                ProcessFlver(filePath, indicesList, operation);
            else if (IsBxf(filePath))
                ProcessBxf(filePath, indicesList, operation);
        }

        private void ProcessBnd(string path, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing BND archive {path}");
            var bnd = BND3.Read(path);

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
                ProcessInnerFile(file, innerIndices, operation);
            }

            if (indicesList.Any(indices => indices.Length == 0))
            {
                ProcessObject(bnd, operation);
            }

            if (operation.Write)
                bnd.Write(path);
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
            else
            {
                foreach (var indices in indicesList)
                {
                    ProcessSingleInnerFile(file, indices, operation);
                }
            }
        }

        private void ProcessSingleInnerFile(BinderFile file, int[] indices, FileOperation operation)
        {
            Console.WriteLine($"Processing single inner file {file.Name}");
            if (indices.Length == 0)
            {
                ProcessFileData(file, operation);
                return;
            }

            if (IsBndData(file.Bytes))
                ProcessBndData(file, new List<int[]> { indices }, operation);
            else if (IsTpfData(file.Bytes))
                ProcessTpfData(file, new List<int[]> { indices }, operation);
            else if (IsBxfData(file.Bytes))
                ProcessBxfData(file, new List<int[]> { indices }, operation);
            else if (IsDcxData(file.Bytes))
                ProcessDcxData(file, new List<int[]> { indices }, operation);
        }

        private void ProcessFileData(BinderFile file, FileOperation operation)
        {
            Console.WriteLine("Processing file data");

            if (IsFlvData(file.Bytes))
            {
                Console.WriteLine("Processing FLVER data");
                var flver = FLVER2.Read(file.Bytes);
                if (operation.GetObject) _mainObject = flver;
                if (operation.ReplaceFlver) flver = operation.NewFlver;
                operation.AdditionalFlverProcessing?.Invoke(flver, _currentRealPath, file.Name);
                if (operation.WriteFlver || operation.ReplaceFlver)
                    file.Bytes = WriteFlverSafe(flver, file.Bytes, file.Name);
            }
            else if (IsTpfData(file.Bytes))
            {
                Console.WriteLine("Processing TPF data");
                var tpf = TPF.Read(file.Bytes);
                ProcessTpfObject(tpf, operation);
                if (operation.Write) file.Bytes = tpf.Write();
            }
            else if (IsDcxData(file.Bytes))
            {
                Console.WriteLine("Processing DCX data");

                try
                {
                    var decompressed = DCX.Decompress(file.Bytes, out var dcxType);

                    if (IsTpfData(decompressed))
                    {
                        var tpf = TPF.Read(decompressed);
                        ProcessTpfObject(tpf, operation);

                        if (operation.Write)
                        {
                            var tpfBytes = tpf.Write();
                            file.Bytes = DCX.Compress(tpfBytes, dcxType);
                        }
                        else if (operation.GetObject)
                        {
                            _mainObject = tpf;
                        }
                    }
                    else if (IsFlvData(decompressed))
                    {
                        var flver = FLVER2.Read(decompressed);
                        if (operation.GetObject) _mainObject = flver;
                        if (operation.ReplaceFlver) flver = operation.NewFlver;
                        if (operation.UseFlverDelegate)
                            operation.AdditionalFlverProcessing?.Invoke(flver, _currentRealPath, file.Name);
                        if (operation.WriteFlver || operation.ReplaceFlver)
                        {
                            var flverBytes = WriteFlverSafe(flver, decompressed, file.Name);
                            file.Bytes = DCX.Compress(flverBytes, dcxType);
                        }
                    }
                    else if (IsBndData(decompressed))
                    {
                        var bnd = BND3.Read(decompressed);
                        if (operation.GetObject) _mainObject = bnd;

                        if (operation.Write)
                        {
                            var bndBytes = bnd.Write();
                            file.Bytes = DCX.Compress(bndBytes, dcxType);
                        }
                    }
                    else
                    {
                        if (operation.GetObject)
                        {
                            _mainObject = decompressed;
                        }

                        if (operation.Write)
                        {
                            file.Bytes = DCX.Compress(decompressed, dcxType);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to process DCX data for {file.Name}: {ex.Message}";
                    Console.WriteLine(errorMsg);
                    _errorLogs.Add(errorMsg);

                    if (operation.GetObject)
                    {
                        _mainObject = file;
                    }
                }
            }
            else if (operation.GetObject)
            {
                _mainObject = file;
            }
        }

        private void ProcessBndData(BinderFile file, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing BND data {file.Name}");
            var bnd = BND3.Read(file.Bytes);

            var fileGroups = indicesList
                .Where(indices => indices.Length > 0)
                .GroupBy(indices => indices[0])
                .ToDictionary(g => g.Key, g => g.Select(indices => indices.Skip(1).ToArray()).ToList());

            foreach (var group in fileGroups)
            {
                var innerIndex = group.Key;
                var innerIndices = group.Value;

                if (innerIndex >= 0 && innerIndex < bnd.Files.Count)
                {
                    var innerFile = bnd.Files[innerIndex];
                    ProcessInnerFile(innerFile, innerIndices, operation);
                }
            }

            if (operation.Write)
                file.Bytes = bnd.Write();
        }

        private void ProcessTpf(string path, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing TPF archive {path}");
            var tpf = TPF.Read(path);

            ProcessTpfIndices(tpf, indicesList, operation);

            if (operation.Write)
                tpf.Write(path);
        }

        private void ProcessTpfData(BinderFile file, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing TPF data {file.Name}");
            var tpf = TPF.Read(file.Bytes);

            ProcessTpfIndices(tpf, indicesList, operation);

            if (operation.Write)
                file.Bytes = tpf.Write();
        }

        private void ProcessTpfIndices(TPF tpf, List<int[]> indicesList, FileOperation operation)
        {
            foreach (var indices in indicesList)
            {
                if (indices.Length == 0)
                {
                    ProcessTpfObject(tpf, operation);
                    continue;
                }

                var textureIndex = indices[0];
                if (textureIndex >= 0 && textureIndex < tpf.Textures.Count)
                {
                    var texture = tpf.Textures[textureIndex];
                    ProcessTexture(texture, operation);
                }
            }

            if (operation.RemoveTexture && indicesList.Count == 1)
            {
                var indices = indicesList[0];
                if (indices.Length == 1)
                {
                    var textureIndex = indices[0];
                    if (textureIndex >= 0 && textureIndex < tpf.Textures.Count)
                    {
                        tpf.Textures.RemoveAt(textureIndex);
                        Console.WriteLine($"Removed texture at index {textureIndex}");
                    }
                }
                else if (indices.Length == 0)
                {
                    Console.WriteLine("Cannot remove texture without specifying index");
                }
            }
        }

        private void ProcessTpfObject(TPF tpf, FileOperation operation)
        {
            Console.WriteLine($"Processing TPF object");
            if (operation.GetObject) _mainObject = tpf;
            if (operation.Replace) tpf = TPF.Read(operation.NewBytes);
            if (operation.AddTexture) tpf.Textures.Add(CreateNewTexture(operation));
        }

        private void ProcessTexture(TPF.Texture texture, FileOperation operation)
        {
            Console.WriteLine($"Processing texture {texture.Name}");
            if (operation.GetObject) _mainObject = texture;
            if (operation.ReplaceTexture) texture.Bytes = operation.NewTextureBytes;
            if (operation.RenameTexture) texture.Name = operation.NewTextureName;
            if (operation.ChangeTextureFormat) texture.Format = operation.NewTextureFormat;
        }

        private void ProcessFlver(string path, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing FLVER file {path}");
            var flver = FLVER2.Read(path);

            if (indicesList.Any(indices => indices.Length == 0))
            {
                if (operation.GetObject) _mainObject = flver;
                if (operation.ReplaceFlver) flver = operation.NewFlver;
                if (operation.UseFlverDelegate)
                    operation.AdditionalFlverProcessing?.Invoke(flver, _currentRealPath, path);
            }

            if (operation.WriteFlver || operation.ReplaceFlver)
            {
                byte[] original = File.ReadAllBytes(path);
                WriteFlverSafe(flver, path, original);
            }
        }

        private void ProcessBxf(string bhdPath, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing BXF archive {bhdPath}");
            var bdtPath = FindBdtPath(bhdPath);
            if (!File.Exists(bdtPath)) return;

            var bxf = BXF3.Read(bhdPath, bdtPath);

            var fileGroups = indicesList
                .Where(indices => indices.Length > 0)
                .GroupBy(indices => indices[0])
                .ToDictionary(g => g.Key, g => g.Select(indices => indices.Skip(1).ToArray()).ToList());

            foreach (var group in fileGroups)
            {
                var fileIndex = group.Key;
                var innerIndices = group.Value;

                if (fileIndex >= 0 && fileIndex < bxf.Files.Count)
                {
                    var file = bxf.Files[fileIndex];
                    ProcessInnerFile(file, innerIndices, operation);
                }
            }

            // Обработка добавления нового файла TPF.DCX
            if (indicesList.Any(indices => indices.Length == 0) && operation.AddTpfDcx)
            {
                AddTpfDcxToBxf(bxf, operation);
            }
            else if (indicesList.Any(indices => indices.Length == 0))
            {
                ProcessObject(bxf, operation);
            }

            if (operation.Write || operation.AddTpfDcx)
                bxf.Write(bhdPath, bdtPath);
        }

        private void ProcessBxfData(BinderFile file, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing BXF data {file.Name}");
            var bdtPath = FindBdtPathForFile(file);
            if (!File.Exists(bdtPath)) return;

            var bxf = BXF3.Read(file.Bytes, bdtPath);

            var fileGroups = indicesList
                .Where(indices => indices.Length > 0)
                .GroupBy(indices => indices[0])
                .ToDictionary(g => g.Key, g => g.Select(indices => indices.Skip(1).ToArray()).ToList());

            foreach (var group in fileGroups)
            {
                var innerIndex = group.Key;
                var innerIndices = group.Value;

                if (innerIndex >= 0 && innerIndex < bxf.Files.Count)
                {
                    var innerFile = bxf.Files[innerIndex];
                    ProcessInnerFile(innerFile, innerIndices, operation);
                }
            }

            // Обработка добавления нового файла TPF.DCX
            if (indicesList.Any(indices => indices.Length == 0) && operation.AddTpfDcx)
            {
                AddTpfDcxToBxf(bxf, operation);
            }

            if (operation.Write || operation.AddTpfDcx)
            {
                bxf.Write(out var bhdBytes, out var bdtBytes);
                file.Bytes = bhdBytes;
                File.WriteAllBytes(bdtPath, bdtBytes);
            }
        }

        // Новый метод для добавления TPF.DCX в BXF
        private void AddTpfDcxToBxf(BXF3 bxf, FileOperation operation)
        {
            Console.WriteLine($"Adding TPF.DCX archive to BXF");

            try
            {
                // Создаем новый TPF
                var tpf = new TPF();
                tpf.Platform = TPF.TPFPlatform.PC;

                // Добавляем текстуры если требуется
                if (operation.AddTexture)
                {
                    tpf.Textures.Add(CreateNewTexture(operation));
                }

                // Дополнительная обработка через делегат
                if (operation.UseTexDelegate)
                    operation.AdditionalTexProcessing?.Invoke(null, _currentRealPath);

                // Записываем TPF и сжимаем в DCX
                var tpfBytes = tpf.Write();
                var dcxType = DCX.Type.DCX_DFLT_10000_24_9; // или другой тип DCX, используемый в игре
                var compressedTpf = DCX.Compress(tpfBytes, dcxType);
                var newFileName = "";
                // Создаем новый файл для BXF
                if (operation.NewTpfDcxArchiveName != "")
                {
                    newFileName = operation.NewTpfDcxArchiveName;
                }
                else
                {
                    newFileName = GenerateUniqueFileName(bxf, ".tpf.dcx");
                }

                var newFile = new BinderFile
                {
                    Name = newFileName,
                    Bytes = compressedTpf
                };


                bxf.Files.Add(newFile);
                Console.WriteLine($"Added TPF.DCX archive as {newFileName} to BXF");

                if (operation.GetObject)
                {
                    _mainObject = newFile;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to add TPF.DCX to BXF: {ex.Message}";
                Console.WriteLine(errorMsg);
                _errorLogs.Add(errorMsg);
            }
        }

        // Вспомогательный метод для генерации уникального имени файла
        private string GenerateUniqueFileName(BXF3 bxf, string extension)
        {
            var baseName = "new_texture";
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

        private void ProcessDcxData(BinderFile file, List<int[]> indicesList, FileOperation operation)
        {
            Console.WriteLine($"Processing DCX data {file.Name}");
            try
            {
                var decompressed = DCX.Decompress(file.Bytes, out var dcxType);
                var tempFile = new BinderFile { Bytes = decompressed, Name = file.Name };

                ProcessInnerFile(tempFile, indicesList, operation);

                if (operation.Write)
                    file.Bytes = DCX.Compress(tempFile.Bytes, dcxType);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to decompress DCX for {file.Name}: {ex.Message}";
                Console.WriteLine(errorMsg);
                _errorLogs.Add(errorMsg);
            }
        }

        private void ProcessObject(object obj, FileOperation operation)
        {
            if (operation.GetObject) _mainObject = obj;
        }

        private static TPF.Texture CreateNewTexture(FileOperation operation)
        {
            if (operation.NewTextureName != "")
            {
                return new TPF.Texture
                {
                    Name = operation.NewTextureName,
                    Platform = TPF.TPFPlatform.PC,
                    Bytes = DDSTools.fatcat
                };
            }

            return new TPF.Texture
            {
                Name = "New tex",
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
        private static bool IsTpfData(byte[] data) => data.Length >= 3 && data[0] == 'T' && data[1] == 'P' && data[2] == 'F';
        private static bool IsBxfData(byte[] data) => data.Length >= 4 && data[0] == 'B' && data[1] == 'H' && data[2] == 'F' && data[3] == '3';
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
        public bool GetObject { get; set; }
        public bool Write { get; set; }
        public bool Replace { get; set; }
        public byte[] NewBytes { get; set; } = Array.Empty<byte>();

        public bool WriteFlver { get; set; }
        public bool ReplaceFlver { get; set; }
        public FLVER2 NewFlver { get; set; } = new();

        public bool AddTexture { get; set; }
        public bool RemoveTexture { get; set; }
        public bool ReplaceTexture { get; set; }
        public bool RenameTexture { get; set; }
        public bool ChangeTextureFormat { get; set; }
        public byte[] NewTextureBytes { get; set; } = Array.Empty<byte>();
        public byte NewTextureFormat { get; set; }
        public string NewTextureName { get; set; } = "";

        // Флаг для добавления TPF.DCX архива
        public bool AddTpfDcx { get; set; }
        public string NewTpfDcxArchiveName = "";

        public bool UseFlverDelegate { get; set; }
        public bool UseTexDelegate { get; set; }
        public Action<FLVER2, string, string> AdditionalFlverProcessing { get; set; }
        public Action<TPF.Texture, string> AdditionalTexProcessing { get; set; }

        //public bool ShouldWrite => Write || Replace || WriteFlver || ReplaceFlver || ReplaceTexture || RemoveTexture || RenameTexture || ChangeTextureFormat;
    }
}