using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SoulsFormats;

namespace DSRViewer.FileHelper
{
    public class FileBinders
    {
        private string _currentRealPath = "";
        private object? _mainObject;

        // Основной метод для обработки файлов
        public void ProcessPaths(IEnumerable<string> virtualPaths, FileOperation operation)
        {
            var groups = GroupPaths(virtualPaths);

            foreach (var group in groups)
            {
                try
                {
                    ProcessFileGroup(group.Key, group.Value, operation);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {group.Key}: {e.Message}");
                }
            }
        }

        // Группировка путей
        private Dictionary<string, List<int[]>> GroupPaths(IEnumerable<string> paths)
        {
            var groups = new Dictionary<string, List<int[]>>();

            foreach (var path in paths)
            {
                var parts = path.Split('|');
                if (parts.Length == 0) continue;

                var filePath = parts[0];
                var indices = parts.Skip(1)
                                 .Select(s => int.TryParse(s, out var i) ? i : -1)
                                 .Where(i => i >= 0)
                                 .ToArray();

                if (!groups.ContainsKey(filePath))
                    groups[filePath] = new List<int[]>();

                groups[filePath].Add(indices);
            }

            return groups;
        }

        // Обработка группы файлов
        private void ProcessFileGroup(string filePath, List<int[]> indicesList, FileOperation op)
        {
            _currentRealPath = filePath;

            if (IsBnd(filePath))
                ProcessBnd(filePath, indicesList, op);
            else if (IsTpf(filePath))
                ProcessTpf(filePath, indicesList, op);
            else if (IsFlver(filePath))
                ProcessFlver(filePath, indicesList, op);
            else if (IsBxf(filePath))
                ProcessBxf(filePath, indicesList, op);
        }

        // Обработка BND файлов
        private void ProcessBnd(string path, List<int[]> indicesList, FileOperation op)
        {
            var bnd = BND3.Read(path);

            foreach (var indices in indicesList)
            {
                if (indices.Length == 0)
                {
                    // Весь BND
                    ProcessObject(bnd, op);
                    continue;
                }

                // Файл внутри BND
                var fileIndex = indices[0];
                if (fileIndex < 0 || fileIndex >= bnd.Files.Count) continue;

                var file = bnd.Files[fileIndex];
                ProcessInnerFile(file, indices.Skip(1).ToArray(), op);
            }

            if (op.ShouldWrite)
                bnd.Write(path);
        }

        // Обработка вложенных файлов
        private void ProcessInnerFile(BinderFile file, int[] indices, FileOperation op)
        {
            if (indices.Length == 0)
            {
                // Конечный файл
                ProcessFileData(file, op);
                return;
            }

            // Определяем тип и рекурсивно обрабатываем
            if (IsBndData(file.Bytes))
                ProcessBndData(file, indices, op);
            else if (IsTpfData(file.Bytes))
                ProcessTpfData(file, indices, op);
            else if (IsBxfData(file.Bytes))
                ProcessBxfData(file, indices, op);
            else if (IsDcxData(file.Bytes))
                ProcessDcxData(file, indices, op);
        }

        // Обработка файловых данных
        private void ProcessFileData(BinderFile file, FileOperation op)
        {
            if (IsFlvData(file.Bytes))
            {
                var flver = FLVER2.Read(file.Bytes);
                if (op.GetObject) _mainObject = flver;
                if (op.ReplaceFlver) flver = op.NewFlver;
                if (op.WriteFlver || op.ReplaceFlver)
                    file.Bytes = SafeWrite(flver, file.Bytes);
            }
            else if (IsTpfData(file.Bytes))
            {
                var tpf = TPF.Read(file.Bytes);
                ProcessTpfObject(tpf, op);
                if (op.ShouldWrite) file.Bytes = tpf.Write();
            }
            else if (op.GetObject)
            {
                _mainObject = file;
            }
        }

        // Обработка BND данных
        private void ProcessBndData(BinderFile file, int[] indices, FileOperation op)
        {
            var bnd = BND3.Read(file.Bytes);
            var innerIndex = indices[0];

            if (innerIndex >= 0 && innerIndex < bnd.Files.Count)
            {
                var innerFile = bnd.Files[innerIndex];
                ProcessInnerFile(innerFile, indices.Skip(1).ToArray(), op);

                if (op.ShouldWrite)
                    file.Bytes = bnd.Write();
            }
        }

        // Обработка TPF файлов
        private void ProcessTpf(string path, List<int[]> indicesList, FileOperation op)
        {
            var tpf = TPF.Read(path);

            foreach (var indices in indicesList)
            {
                if (indices.Length == 0)
                {
                    ProcessTpfObject(tpf, op);
                    continue;
                }

                var texIndex = indices[0];
                if (texIndex >= 0 && texIndex < tpf.Textures.Count)
                {
                    var tex = tpf.Textures[texIndex];
                    ProcessTexture(tex, op);
                }
            }

            if (op.ShouldWrite)
                tpf.Write(path);
        }

        // Обработка TPF данных
        private void ProcessTpfData(BinderFile file, int[] indices, FileOperation op)
        {
            var tpf = TPF.Read(file.Bytes);

            if (indices.Length == 0)
            {
                ProcessTpfObject(tpf, op);
            }
            else
            {
                var texIndex = indices[0];
                if (texIndex >= 0 && texIndex < tpf.Textures.Count)
                {
                    var tex = tpf.Textures[texIndex];
                    ProcessTexture(tex, op);
                }
            }

            if (op.ShouldWrite)
                file.Bytes = tpf.Write();
        }

        // Обработка TPF объекта
        private void ProcessTpfObject(TPF tpf, FileOperation op)
        {
            if (op.GetObject) _mainObject = tpf;
            if (op.Replace) tpf = TPF.Read(op.NewBytes);
            if (op.AddTexture) tpf.Textures.Add(CreateNewTexture());
        }

        // Обработка текстуры
        private void ProcessTexture(TPF.Texture tex, FileOperation op)
        {
            if (op.GetObject) _mainObject = tex;
            if (op.ReplaceTexture) tex.Bytes = op.NewTextureBytes;
            if (op.RenameTexture) tex.Name = op.NewTextureName;
            if (op.ChangeTextureFormat) tex.Format = op.NewTextureFormat;
        }

        // Обработка FLVER файлов
        private void ProcessFlver(string path, List<int[]> indicesList, FileOperation op)
        {
            var flver = FLVER2.Read(path);

            if (indicesList.Any(indices => indices.Length == 0))
            {
                if (op.GetObject) _mainObject = flver;
                if (op.ReplaceFlver) flver = op.NewFlver;
            }

            if (op.WriteFlver || op.ReplaceFlver)
                SafeWrite(flver, path);
        }

        // Обработка BXF файлов
        private void ProcessBxf(string bhdPath, List<int[]> indicesList, FileOperation op)
        {
            var bdtPath = FindBdtPath(bhdPath);
            if (!File.Exists(bdtPath)) return;

            var bxf = BXF3.Read(bhdPath, bdtPath);

            foreach (var indices in indicesList)
            {
                if (indices.Length == 0)
                {
                    ProcessObject(bxf, op);
                    continue;
                }

                var fileIndex = indices[0];
                if (fileIndex >= 0 && fileIndex < bxf.Files.Count)
                {
                    var file = bxf.Files[fileIndex];
                    ProcessInnerFile(file, indices.Skip(1).ToArray(), op);
                }
            }

            if (op.ShouldWrite)
                bxf.Write(bhdPath, bdtPath);
        }

        // Обработка BXF данных
        private void ProcessBxfData(BinderFile file, int[] indices, FileOperation op)
        {
            var bdtPath = FindBdtPathForFile(file);
            if (!File.Exists(bdtPath)) return;

            var bxf = BXF3.Read(file.Bytes, bdtPath);
            var innerIndex = indices[0];

            if (innerIndex >= 0 && innerIndex < bxf.Files.Count)
            {
                var innerFile = bxf.Files[innerIndex];
                ProcessInnerFile(innerFile, indices.Skip(1).ToArray(), op);

                if (op.ShouldWrite)
                {
                    bxf.Write(out var bhdBytes, out var bdtBytes);
                    file.Bytes = bhdBytes;
                    File.WriteAllBytes(bdtPath, bdtBytes);
                }
            }
        }

        // Обработка DCX данных
        private void ProcessDcxData(BinderFile file, int[] indices, FileOperation op)
        {
            try
            {
                var decompressed = DCX.Decompress(file.Bytes, out var dcxType);
                var tempFile = new BinderFile { Bytes = decompressed, Name = file.Name };

                ProcessInnerFile(tempFile, indices, op);

                if (op.ShouldWrite)
                    file.Bytes = DCX.Compress(tempFile.Bytes, dcxType);
            }
            catch
            {
                Console.WriteLine("Failed to decompress DCX");
            }
        }

        // Общая обработка объекта
        private void ProcessObject(object obj, FileOperation op)
        {
            if (op.GetObject) _mainObject = obj;
        }

        // Вспомогательные методы
        private static TPF.Texture CreateNewTexture()
        {
            return new TPF.Texture
            {
                Name = "New",
                Platform = TPF.TPFPlatform.PC,
                Bytes = new byte[128] // Заглушка
            };
        }

        private static byte[] SafeWrite(FLVER2 flver, byte[] original)
        {
            try { return flver.Write(); }
            catch { return original; }
        }

        private static void SafeWrite(FLVER2 flver, string path)
        {
            try { flver.Write(path); }
            catch { }
        }

        // Поиск BDT файлов
        private string FindBdtPath(string bhdPath)
        {
            var basePath = Path.GetDirectoryName(bhdPath) ?? "";
            var name = Path.GetFileNameWithoutExtension(bhdPath);

            var possiblePaths = new[]
            {
                bhdPath.Replace(".tpfbhd", ".tpfbdt", StringComparison.OrdinalIgnoreCase),
                bhdPath.Replace(".bhd", ".bdt", StringComparison.OrdinalIgnoreCase),
                Path.Combine(basePath, name + ".bdt")
            };

            return possiblePaths.FirstOrDefault(File.Exists) ?? "";
        }

        private string FindBdtPathForFile(BinderFile file)
        {
            var basePath = Path.GetDirectoryName(_currentRealPath) ?? "";
            var name = Path.GetFileNameWithoutExtension(file.Name);

            if (name.EndsWith(".tpfbhd", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(basePath, name.Replace(".tpfbhd", ".tpfbdt", StringComparison.OrdinalIgnoreCase));

            return "";
        }

        // Проверки типов файлов
        private static bool IsBnd(string path) => path.EndsWith(".bnd", StringComparison.OrdinalIgnoreCase) ||
                                                  path.EndsWith(".bnd.dcx", StringComparison.OrdinalIgnoreCase);

        private static bool IsTpf(string path) => path.EndsWith(".tpf", StringComparison.OrdinalIgnoreCase) ||
                                                  path.EndsWith(".tpf.dcx", StringComparison.OrdinalIgnoreCase);

        private static bool IsFlver(string path) => path.EndsWith(".flver", StringComparison.OrdinalIgnoreCase) ||
                                                    path.EndsWith(".flver.dcx", StringComparison.OrdinalIgnoreCase);

        private static bool IsBxf(string path) => path.EndsWith(".bhd", StringComparison.OrdinalIgnoreCase) ||
                                                  path.EndsWith(".tpfbhd", StringComparison.OrdinalIgnoreCase);

        private static bool IsBndData(byte[] data) => data.Length >= 4 && data[0] == 'B' && data[1] == 'N' && data[2] == 'D' && data[3] == '3';
        private static bool IsTpfData(byte[] data) => data.Length >= 3 && data[0] == 'T' && data[1] == 'P' && data[2] == 'F';
        private static bool IsBxfData(byte[] data) => data.Length >= 4 && data[0] == 'B' && data[1] == 'H' && data[2] == 'F' && data[3] == '3';
        private static bool IsFlvData(byte[] data) => data.Length >= 5 && data[0] == 'F' && data[1] == 'L' && data[2] == 'V' && data[3] == 'E' && data[4] == 'R';
        private static bool IsDcxData(byte[] data) => data.Length >= 3 && data[0] == 'D' && data[1] == 'C' && data[2] == 'X';

        // Геттер объекта
        public object? GetObject() => _mainObject;
        public void Clear() => _mainObject = null;

        public static void ExtractFile(string sourcePath, string outputDir)
        {
            var binder = new FileBinders();
            var operation = new FileOperation { GetObject = true };

            binder.ProcessPaths(new[] { sourcePath }, operation);
            var obj = binder.GetObject();

            if (obj is BinderFile file)
            {
                File.WriteAllBytes(Path.Combine(outputDir, "extracted.dat"), file.Bytes);
            }
            else if (obj is TPF.Texture texture)
            {
                File.WriteAllBytes(Path.Combine(outputDir, "texture.dds"), texture.Bytes);
            }
            else if (obj is FLVER2 flver)
            {
                flver.Write(Path.Combine(outputDir, "model.flver"));
            }
        }
    }



    // Класс для хранения операции
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

        public bool ShouldWrite => Write || Replace || WriteFlver || ReplaceFlver ||
                                   ReplaceTexture || RemoveTexture || RenameTexture || ChangeTextureFormat;
    }
}