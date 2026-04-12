using System.IO;
using System.Collections.Generic;
using SoulsFormats;
using DSRViewer.Editors.Explorer.DDSHelper;
using DSRViewer.FileProcess;

namespace DSRViewer.Editors.Explorer.TreeBuilder
{
    /// <summary>
    /// Строит дерево лениво — содержимое архива загружается только
    /// когда узел раскрывается в UI или обходится через FindAll.
    /// Открытие вкладки мгновенное.
    /// </summary>
    public class FileTreeNodeLazyBuilder
    {
        private const int MaxDepth = 8;

        /// <summary>Строит ленивое дерево FileNode начиная с указанного пути.</summary>
        public FileNode BuildTree(string rootPath)
        {
            var root = new FileNode
            {
                Name = Path.GetFileName(rootPath),
                ShortName = ShortString(Path.GetFileName(rootPath)),
                VirtualPath = rootPath,
                ShortVirtualPath = ShortString(rootPath),
                ArchiveDepth = 0
            };

            try
            {
                if (Directory.Exists(rootPath))
                    return BuildDirectoryNode(rootPath, 0);
                if (File.Exists(rootPath))
                    return BuildFileNode(rootPath, 0);
            }
            catch { }

            return root;
        }

        // ── Директория ───────────────────────────────────────────────────

        private FileNode BuildDirectoryNode(string dirPath, int depth)
        {
            var node = new FileNode
            {
                Name = Path.GetFileName(dirPath),
                ShortName = ShortString(Path.GetFileName(dirPath)),
                VirtualPath = dirPath,
                ShortVirtualPath = ShortString(dirPath),
                Type = NodeType.Folder,
                ArchiveDepth = depth,
                IsLoaded = false
            };

            node.LoadChildren = () =>
            {
                foreach (var dir in Directory.GetDirectories(dirPath))
                    node.Children.Add(BuildDirectoryNode(dir, depth));

                foreach (var file in Directory.GetFiles(dirPath))
                    if (FileSignatures.IsValidGameFile(file))
                        node.Children.Add(BuildFileNode(file, depth + 1));
            };

            return node;
        }

        // ── Файл (архив или FLVER) ────────────────────────────────────────

        private FileNode BuildFileNode(string path, int depth)
        {
            if (FileSignatures.IsBnd(path))   return BuildBndNode(path, depth);
            if (FileSignatures.IsBxf(path))   return BuildBxfNode(path, depth);
            if (FileSignatures.IsTpf(path))   return BuildTpfNode(path, depth);
            if (FileSignatures.IsFlver(path)) return BuildLeafNode(path, NodeType.Flver, depth);
            return BuildLeafNode(path, NodeType.Unknown, depth);
        }

        // ── BND ──────────────────────────────────────────────────────────

        private FileNode BuildBndNode(string path, int depth)
        {
            var node = MakeNode(path, NodeType.BndArchive, depth, isLoaded: false);

            node.LoadChildren = () =>
            {
                try
                {
                    var bnd = BND3.Read(path);
                    for (int i = 0; i < bnd.Files.Count; i++)
                    {
                        var file = bnd.Files[i];
                        var child = MakeVirtualNode(file.Name, $"{path}|{i}", depth + 1);
                        DetectAndSetType(child, file.Bytes, path, depth + 1);
                        node.Children.Add(child);
                    }
                }
                catch (Exception ex)
                {
                    node.Children.Add(ErrorNode(ex.Message));
                }
            };

            return node;
        }

        // ── TPF ──────────────────────────────────────────────────────────

        private FileNode BuildTpfNode(string path, int depth)
        {
            var node = MakeNode(path, NodeType.TpfArchive, depth, isLoaded: false);

            node.LoadChildren = () =>
            {
                try
                {
                    var tpf = TPF.Read(path);
                    
                    // Параллельная обработка текстур
                    var children = new FileNode[tpf.Textures.Count];
                    
                    Parallel.For(0, tpf.Textures.Count,
                        new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                        i =>
                    {
                        var tex = tpf.Textures[i];
                        children[i] = new FileNode
                        {
                            ID = i,
                            Name = tex.Name,
                            ShortName = tex.Name,
                            VirtualPath = $"{path}|{i}",
                            ShortVirtualPath = $"{ShortString(path)}|{i}",
                            Type = NodeType.NestedDds,
                            DDSFormatFlag = tex.Format,
                            DDSFormat = DDSTools.ReadDDSImageFormat(tex.Bytes),
                            Size = tex.Bytes.Length,
                            ArchiveDepth = depth + 1
                        };
                    });
                    
                    node.Children.AddRange(children);
                }
                catch (Exception ex)
                {
                    node.Children.Add(ErrorNode(ex.Message));
                }
            };

            return node;
        }

        // ── BXF ──────────────────────────────────────────────────────────

        private FileNode BuildBxfNode(string bhdPath, int depth)
        {
            var node = MakeNode(bhdPath, NodeType.BxfArchive, depth, isLoaded: false);

            node.LoadChildren = () =>
            {
                string bdtPath = bhdPath.Replace(".tpfbhd", ".tpfbdt", StringComparison.OrdinalIgnoreCase);
                if (!File.Exists(bdtPath)) return;
                try
                {
                    var bxf = BXF3.Read(bhdPath, bdtPath);
                    for (int i = 0; i < bxf.Files.Count; i++)
                    {
                        var file = bxf.Files[i];
                        var child = MakeVirtualNode(file.Name, $"{bhdPath}|{i}", depth + 1);
                        DetectAndSetType(child, file.Bytes, bhdPath, depth + 1);
                        node.Children.Add(child);
                    }
                }
                catch (Exception ex)
                {
                    node.Children.Add(ErrorNode(ex.Message));
                }
            };

            return node;
        }

        // ── Определение типа вложенного файла ────────────────────────────

        private void DetectAndSetType(FileNode child, byte[] bytes, string parentPath, int depth)
        {
            if (depth >= MaxDepth) return;

            if (FileSignatures.IsBndData(bytes))
            {
                child.Type = NodeType.NestedBndArchive;
                child.IsLoaded = false;
                child.LoadChildren = () => LoadNestedBnd(child, bytes, depth);
            }
            else if (FileSignatures.IsTpfData(bytes))
            {
                child.Type = NodeType.NestedTpfArchive;
                child.IsLoaded = false;
                child.LoadChildren = () => LoadNestedTpf(child, bytes, depth);
            }
            else if (FileSignatures.IsBxfData(bytes))
            {
                child.Type = NodeType.NestedBxfArchive;
                child.IsLoaded = false;
                child.LoadChildren = () => LoadNestedBxf(child, bytes, parentPath, depth);
            }
            else if (FileSignatures.IsDcxData(bytes))
            {
                child.IsLoaded = false;
                child.LoadChildren = () =>
                {
                    try
                    {
                        var decompressed = DCX.Decompress(bytes);
                        // Определяем тип и сразу загружаем детей — не переназначаем LoadChildren
                        if (FileSignatures.IsTpfData(decompressed))
                        {
                            child.Type = NodeType.NestedTpfArchive;
                            LoadNestedTpf(child, decompressed, depth);
                        }
                        else if (FileSignatures.IsBndData(decompressed))
                        {
                            child.Type = NodeType.NestedBndArchive;
                            LoadNestedBnd(child, decompressed, depth);
                        }
                        else if (FileSignatures.IsFlvData(decompressed))
                        {
                            child.Type = NodeType.NestedFlver;
                        }
                    }
                    catch { }
                };
            }
            else if (FileSignatures.IsFlvData(bytes))
            {
                child.Type = NodeType.NestedFlver;
            }
        }

        private static void LoadNestedBnd(FileNode parent, byte[] bytes, int depth)
        {
            try
            {
                var bnd = BND3.Read(bytes);
                for (int i = 0; i < bnd.Files.Count; i++)
                {
                    var file = bnd.Files[i];
                    var child = MakeVirtualNode(file.Name, $"{parent.VirtualPath}|{i}", depth + 1);

                    if (FileSignatures.IsFlvData(file.Bytes))
                    {
                        child.Type = NodeType.NestedFlver;
                    }
                    else if (FileSignatures.IsTpfData(file.Bytes))
                    {
                        child.Type = NodeType.NestedTpfArchive;
                        child.IsLoaded = false;
                        child.LoadChildren = () => LoadNestedTpf(child, file.Bytes, depth + 1);
                    }
                    else if (FileSignatures.IsDcxData(file.Bytes) && depth < MaxDepth)
                    {
                        // tpf.dcx внутри chrbnd
                        var captured = file.Bytes;
                        child.IsLoaded = false;
                        child.LoadChildren = () =>
                        {
                            try
                            {
                                var decompressed = DCX.Decompress(captured);
                                if (FileSignatures.IsTpfData(decompressed))
                                {
                                    child.Type = NodeType.NestedTpfArchive;
                                    LoadNestedTpf(child, decompressed, depth + 1);
                                }
                                else if (FileSignatures.IsBndData(decompressed))
                                {
                                    child.Type = NodeType.NestedBndArchive;
                                    LoadNestedBnd(child, decompressed, depth + 1);
                                }
                            }
                            catch { }
                        };
                    }

                    parent.Children.Add(child);
                }
            }
            catch { }
        }

        private static void LoadNestedTpf(FileNode parent, byte[] bytes, int depth)
        {
            try
            {
                var tpf = TPF.Read(bytes);
                
                // Параллельная обработка текстур
                var children = new FileNode[tpf.Textures.Count];
                
                Parallel.For(0, tpf.Textures.Count,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    i =>
                {
                    var tex = tpf.Textures[i];
                    children[i] = new FileNode
                    {
                        ID = i,
                        Name = tex.Name,
                        ShortName = tex.Name,
                        VirtualPath = $"{parent.VirtualPath}|{i}",
                        ShortVirtualPath = $"{parent.ShortVirtualPath}|{i}",
                        Type = NodeType.NestedDds,
                        DDSFormatFlag = tex.Format,
                        DDSFormat = DDSTools.ReadDDSImageFormat(tex.Bytes),
                        Size = tex.Bytes.Length,
                        ArchiveDepth = depth + 1
                    };
                });
                
                parent.Children.AddRange(children);
            }
            catch { }
        }

        private static void LoadNestedBxf(FileNode parent, byte[] bhdBytes, string parentPath, int depth)
        {
            // Ищем bdt рядом с bnd
            string bdtPath = parentPath;
            foreach (var (from, to) in new[] {
                (".chrbnd.dcx", ".chrtpfbdt"),
                (".chrbnd",     ".chrtpfbdt") })
            {
                if (parentPath.EndsWith(from, StringComparison.OrdinalIgnoreCase))
                { bdtPath = parentPath[..^from.Length] + to; break; }
            }
            if (!File.Exists(bdtPath)) return;
            try
            {
                var bxf = BXF3.Read(bhdBytes, bdtPath);
                for (int i = 0; i < bxf.Files.Count; i++)
                {
                    var file = bxf.Files[i];
                    var child = MakeVirtualNode(file.Name, $"{parent.VirtualPath}|{i}", depth + 1);
                    if (FileSignatures.IsTpfData(file.Bytes))
                    {
                        child.Type = NodeType.NestedTpfArchive;
                        child.IsLoaded = false;
                        child.LoadChildren = () => LoadNestedTpf(child, file.Bytes, depth + 1);
                    }
                    else if (FileSignatures.IsDcxData(file.Bytes) && depth < MaxDepth)
                    {
                        var captured = file.Bytes;
                        child.IsLoaded = false;
                        child.LoadChildren = () =>
                        {
                            try
                            {
                                var inner = DCX.Decompress(captured);
                                if (FileSignatures.IsTpfData(inner))
                                {
                                    child.Type = NodeType.NestedTpfArchive;
                                    LoadNestedTpf(child, inner, depth + 1);
                                }
                            }
                            catch { }
                        };
                    }
                    parent.Children.Add(child);
                }
            }
            catch { }
        }

        // ── Вспомогательные ─────────────────────────────────────────────

        private static FileNode MakeNode(string path, NodeType type, int depth, bool isLoaded = true) =>
            new()
            {
                Name = Path.GetFileName(path),
                ShortName = ShortString(Path.GetFileName(path)),
                VirtualPath = path,
                ShortVirtualPath = ShortString(path),
                Type = type,
                ArchiveDepth = depth,
                IsLoaded = isLoaded
            };

        private static FileNode MakeVirtualNode(string name, string virtualPath, int depth) =>
            new()
            {
                Name = name,
                ShortName = name,
                VirtualPath = virtualPath,
                ShortVirtualPath = virtualPath,
                ArchiveDepth = depth
            };

        private static FileNode BuildLeafNode(string path, NodeType type, int depth) =>
            MakeNode(path, type, depth, isLoaded: true);

        private static FileNode ErrorNode(string msg) =>
            new() { Name = $"ERROR: {msg}" };

        private static string ShortString(string str)
        {
            var parts = str.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            return string.Join("\\", parts.Skip(Math.Max(0, parts.Length - 2)));
        }
    }
}
