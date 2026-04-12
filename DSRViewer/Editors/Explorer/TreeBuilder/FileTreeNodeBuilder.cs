using System.IO;
using System.Collections.Generic;
using SoulsFormats;
using DSRViewer.Editors.Explorer.DDSHelper;
using DSRViewer.FileProcess;

namespace DSRViewer.Editors.Explorer.TreeBuilder
{
    /// <summary>Строит полное дерево FileNode из файла или папки (загружает всё сразу).</summary>
    public class FileTreeNodeBuilder
    {
        private const int MaxDepth = 8; // Prevent infinite recursion

        /// <summary>Строит дерево FileNode начиная с указанного пути (файл или папка).</summary>
        public FileNode BuildTree(string rootPath)
        {
            var RootNode = new FileNode
            {
                Name = Path.GetFileName(rootPath),
                ShortName = ShortString(Path.GetFileName(rootPath)),
                VirtualPath = rootPath,
                ShortVirtualPath = ShortString(rootPath),
                Type = NodeType.Unknown,
                ArchiveDepth = 0
            };

            try
            {
                if (Directory.Exists(rootPath))
                {
                    return BuildDirectoryNode(rootPath, 0);
                }
                else if (File.Exists(rootPath) && IsBnd(rootPath))
                {
                    return BuildBndNode(rootPath, 0);
                }
                else if (File.Exists(rootPath) && IsBxf(rootPath))
                {
                    return BuildBxfNode(rootPath, 0);
                }
                else if (File.Exists(rootPath) && IsTpf(rootPath))
                {
                    return BuildTPFNode(rootPath, 0);
                }
                else if (File.Exists(rootPath) && IsFlver(rootPath))
                {
                    return BuildFlverNode(rootPath, 0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TreeBuilder] BuildTree failed: {ex.Message}");
            }

            return RootNode;
        }

        private string ShortString(string str)
        {
            string[] parts = str.Split(new[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return str;
            str = string.Join("\\", parts.Skip(parts.Length - 2));
            return str;
        }

        private FileNode BuildDirectoryNode(string dirPath, int depth)
        {
            Console.WriteLine($"...{dirPath}");
            var node = new FileNode
            {
                Name = Path.GetFileName(dirPath),
                ShortName = ShortString(Path.GetFileName(dirPath)),
                VirtualPath = dirPath,
                ShortVirtualPath = ShortString(dirPath),
                Type = NodeType.Folder,
                ArchiveDepth = depth
            };

            foreach (var dir in Directory.GetDirectories(dirPath))
            {
                node.Children.Add(BuildDirectoryNode(dir, depth));
            }

            foreach (var file in Directory.GetFiles(dirPath))
            {
                if (IsValidFile(file))
                {
                    if (IsBnd(file) && depth < MaxDepth)
                    {
                        node.Children.Add(BuildBndNode(file, depth + 1));
                    }
                    else if (IsTpf(file) && depth < MaxDepth)
                    {
                        node.Children.Add(BuildTPFNode(file, depth + 1));
                    }
                    else if (IsBxf(file) && depth < MaxDepth)
                    {
                        node.Children.Add(BuildBxfNode(file, depth + 1));
                    }
                    else if (IsFlver(file) && depth < MaxDepth)
                    {
                        node.Children.Add(BuildFlverNode(file, depth + 1));
                    }
                    else
                    {
                        node.Children.Add(BuildUnkNode(file, depth + 1));
                    }
                }
            }

            return node;
        }

        private FileNode BuildBndNode(string bndPath, int depth)
        {
            var node = new FileNode
            {
                Name = Path.GetFileName(bndPath),
                ShortName = ShortString(Path.GetFileName(bndPath)),
                VirtualPath = bndPath,
                ShortVirtualPath = ShortString(bndPath),
                Type = NodeType.BndArchive,
                ArchiveDepth = depth,
            };

            try
            {
                var bnd = BND3.Read(bndPath);

                // Параллельная обработка файлов в архиве
                var children = new FileNode[bnd.Files.Count];
                
                Parallel.For(0, bnd.Files.Count, 
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    file_index =>
                {
                    var file = bnd.Files[file_index];
                    var child = new FileNode
                    {
                        ID = file_index,
                        Name = file.Name,
                        VirtualPath = $"{bndPath}|{file_index}",
                        ShortName = ShortString(file.Name),
                        ShortVirtualPath = $"{ShortString(bndPath)}|{file_index}",
                        ArchiveDepth = depth + 1
                    };

                    // Check if file is another BND
                    if (IsBndData(file.Bytes) && depth < MaxDepth)
                    {
                        child.Type = NodeType.NestedBndArchive;
                        child.Children.AddRange(ReadNestedBnd(file.Bytes, depth + 1, child.VirtualPath));
                    }
                    else if (IsTpfData(file.Bytes) && depth < MaxDepth)
                    {
                        child.Type = NodeType.NestedTpfArchive;
                        child.Children.AddRange(ReadNestedTPF(file.Bytes, depth + 1, child.VirtualPath));
                    }
                    else if (IsBxfData(file.Bytes) && depth < MaxDepth)
                    {
                        child.Type = NodeType.NestedBxfArchive;
                        child.Children.AddRange(ReadNestedBXF(file.Bytes, file.Name, bndPath, depth + 1, child.VirtualPath));
                    }
                    else if (IsFlvData(file.Bytes) && depth < MaxDepth)
                    {
                        child.Type = NodeType.NestedFlver;
                    }
                    else if (IsDcxData(file.Bytes) && depth < MaxDepth)
                    {
                        // DCX-обёртка — декомпрессируем и определяем тип
                        try
                        {
                            var inner = DCX.Decompress(file.Bytes);
                            if (IsTpfData(inner))
                            {
                                child.Type = NodeType.NestedTpfArchive;
                                child.Children.AddRange(ReadNestedTPF(inner, depth + 1, child.VirtualPath));
                            }
                            else if (IsBndData(inner))
                            {
                                child.Type = NodeType.NestedBndArchive;
                                child.Children.AddRange(ReadNestedBnd(inner, depth + 1, child.VirtualPath));
                            }
                        }
                        catch { /* не удалось декомпрессировать — оставляем Unknown */ }
                    }

                    children[file_index] = child;
                });

                node.Children.AddRange(children);
            }
            catch (Exception ex)
            {
                node.Children.Add(new FileNode { Name = $"ERROR: {ex.Message}" });
            }

            return node;
        }

        private FileNode BuildTPFNode(string tpfPath, int depth)
        {
            var node = new FileNode
            {
                Name = Path.GetFileName(tpfPath),
                VirtualPath = tpfPath,
                ShortName = ShortString(Path.GetFileName(tpfPath)),
                ShortVirtualPath = ShortString(tpfPath),
                Type = NodeType.TpfArchive,
                ArchiveDepth = depth
            };

            try
            {
                var tpf = TPF.Read(tpfPath);

                // Параллельная обработка текстур (чтение формата DDS)
                var children = new FileNode[tpf.Textures.Count];
                
                Parallel.For(0, tpf.Textures.Count,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    file_index =>
                {
                    var file = tpf.Textures[file_index];
                    var child = new FileNode
                    {
                        ID = file_index,
                        Name = file.Name,
                        VirtualPath = $"{tpfPath}|{file_index}",
                        ShortName = ShortString(file.Name),
                        ShortVirtualPath = $"{ShortString(tpfPath)}|{file_index}",
                        Type = NodeType.NestedDds,
                        DDSFormatFlag = file.Format,
                        DDSFormat = DDSTools.ReadDDSImageFormat(file.Bytes),
                        ArchiveDepth = depth + 1
                    };
                    children[file_index] = child;
                });

                node.Children.AddRange(children);
            }
            catch (Exception ex)
            {
                node.Children.Add(new FileNode { Name = $"ERROR: {ex.Message}" });
            }

            return node;
        }

        private FileNode BuildBxfNode(string bhdPath, int depth)
        {
            var node = new FileNode
            {
                Name = Path.GetFileName(bhdPath),
                VirtualPath = bhdPath,
                ShortName = ShortString(Path.GetFileName(bhdPath)),
                ShortVirtualPath = ShortString(bhdPath),
                Type = NodeType.BxfArchive,
                ArchiveDepth = depth
            };

            string bdtPath = bhdPath.Replace(".tpfbhd", ".tpfbdt", StringComparison.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(bdtPath))
                {
                    var bxf = BXF3.Read(bhdPath, bdtPath);
                    
                    // Параллельная обработка файлов в BXF
                    var children = new FileNode[bxf.Files.Count];
                    
                    Parallel.For(0, bxf.Files.Count,
                        new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                        file_index =>
                    {
                        var file = bxf.Files[file_index];
                        var child = new FileNode
                        {
                            ID = file_index,
                            Name = file.Name,
                            VirtualPath = $"{bhdPath}|{file_index}",
                            ShortName = ShortString(file.Name),
                            ShortVirtualPath = $"{ShortString(bhdPath)}|{file_index}",
                            ArchiveDepth = depth + 1
                        };

                        if (file.Name.EndsWith(".tpf.dcx", StringComparison.OrdinalIgnoreCase)
                            || file.Name.EndsWith(".tpf", StringComparison.OrdinalIgnoreCase)
                            || IsTpfData(file.Bytes))
                        {
                            child.Type = NodeType.NestedTpfArchive;
                            child.Children.AddRange(ReadNestedTPF(file.Bytes, depth + 1, child.VirtualPath));
                        }
                        else if (IsDcxData(file.Bytes) && depth < MaxDepth)
                        {
                            try
                            {
                                var inner = DCX.Decompress(file.Bytes);
                                if (IsTpfData(inner))
                                {
                                    child.Type = NodeType.NestedTpfArchive;
                                    child.Children.AddRange(ReadNestedTPF(inner, depth + 1, child.VirtualPath));
                                }
                            }
                            catch { }
                        }
                        
                        children[file_index] = child;
                    });

                    node.Children.AddRange(children);
                }
            }
            catch (Exception ex)
            {
                node.Children.Add(new FileNode { Name = $"ERROR: {ex.Message}" });
            }

            return node;
        }

        private FileNode BuildFlverNode(string flverPath, int depth)
        {
            var node = new FileNode
            {
                Name = Path.GetFileName(flverPath),
                VirtualPath = flverPath,
                ShortName = ShortString(Path.GetFileName(flverPath)),
                ShortVirtualPath = ShortString(flverPath),
                Type = NodeType.Flver,
                ArchiveDepth = depth
            };
            return node;
        }

        private FileNode BuildUnkNode(string filePath, int depth)
        {
            var node = new FileNode
            {
                Name = Path.GetFileName(filePath),
                ShortName = ShortString(Path.GetFileName(filePath)),
                VirtualPath = filePath,
                ShortVirtualPath = ShortString(filePath),
                Type = NodeType.Unknown,
                ArchiveDepth = depth
            };
            return node;
        }

        private List<FileNode> ReadNestedBnd(byte[] bndData, int depth, string virtualPath)
        {
            var nodes = new List<FileNode>();

            try
            {
                var nestedBnd = BND3.Read(bndData);
                
                // Параллельная обработка файлов во вложенном BND
                var children = new FileNode[nestedBnd.Files.Count];
                
                Parallel.For(0, nestedBnd.Files.Count,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    file_index =>
                {
                    var file = nestedBnd.Files[file_index];
                    var node = new FileNode
                    {
                        ID = file_index,
                        Name = file.Name,
                        VirtualPath = $"{virtualPath}|{file_index}",
                        ShortName = ShortString(file.Name),
                        ShortVirtualPath = $"{ShortString(virtualPath)}|{file_index}",
                        ArchiveDepth = depth + 1
                    };

                    if (IsBndData(file.Bytes) && depth < MaxDepth)
                    {
                        node.Type = NodeType.NestedBndArchive;
                        node.Children.AddRange(ReadNestedBnd(file.Bytes, depth + 1, node.VirtualPath));
                    }
                    else if (IsTpfData(file.Bytes) && depth < MaxDepth)
                    {
                        node.Type = NodeType.NestedTpfArchive;
                        node.Children.AddRange(ReadNestedTPF(file.Bytes, depth + 1, node.VirtualPath));
                    }
                    else if (IsFlvData(file.Bytes) && depth < MaxDepth)
                    {
                        node.Type = NodeType.NestedFlver;
                    }
                    else if (IsDcxData(file.Bytes) && depth < MaxDepth)
                    {
                        try
                        {
                            var inner = DCX.Decompress(file.Bytes);
                            if (IsTpfData(inner))
                            {
                                node.Type = NodeType.NestedTpfArchive;
                                node.Children.AddRange(ReadNestedTPF(inner, depth + 1, node.VirtualPath));
                            }
                            else if (IsBndData(inner))
                            {
                                node.Type = NodeType.NestedBndArchive;
                                node.Children.AddRange(ReadNestedBnd(inner, depth + 1, node.VirtualPath));
                            }
                        }
                        catch { }
                    }

                    children[file_index] = node;
                });

                nodes.AddRange(children);
            }
            catch
            {
                nodes.Add(new FileNode { Name = "Invalid nested BND" });
            }

            return nodes;
        }

        private List<FileNode> ReadNestedTPF(byte[] tpfData, int depth, string virtualPath)
        {
            var nodes = new List<FileNode>();

            try
            {
                var nestedTPF = TPF.Read(tpfData);
                
                // Параллельная обработка текстур
                var children = new FileNode[nestedTPF.Textures.Count];
                
                Parallel.For(0, nestedTPF.Textures.Count,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    file_index =>
                {
                    var file = nestedTPF.Textures[file_index];
                    var node = new FileNode
                    {
                        ID = file_index,
                        Name = file.Name,
                        VirtualPath = $"{virtualPath}|{file_index}",
                        ShortName = ShortString(file.Name),
                        ShortVirtualPath = $"{ShortString(virtualPath)}|{file_index}",
                        Type = NodeType.NestedDds,
                        DDSFormatFlag = file.Format,
                        DDSFormat = DDSTools.ReadDDSImageFormat(file.Bytes),
                        Size = file.Bytes.Length,
                        ArchiveDepth = depth + 1
                    };
                    children[file_index] = node;
                });

                nodes.AddRange(children);
            }
            catch
            {
                nodes.Add(new FileNode { Name = "Invalid nested TPF" });
            }

            return nodes;
        }

        private List<FileNode> ReadNestedBXF(byte[] bhdData, string bhdName, string bndPath, int depth, string virtualPath)
        {
            List<FileNode> nodes = [];
            // Ищем bdt рядом с bnd: заменяем расширение chrbnd(.dcx) → chrtpfbdt
            string bdtPath = bndPath;
            foreach (var (from, to) in new[] {
                (".chrbnd.dcx", ".chrtpfbdt"),
                (".chrbnd",     ".chrtpfbdt") })
            {
                if (bndPath.EndsWith(from, StringComparison.OrdinalIgnoreCase))
                { bdtPath = bndPath[..^from.Length] + to; break; }
            }

            if (File.Exists(bdtPath))
            {
                try
                {
                    BXF3 nestedBXF = BXF3.Read(bhdData, bdtPath);

                    int file_index = 0;
                    foreach (var file in nestedBXF.Files)
                    {
                        var node = new FileNode
                        {
                            ID = file_index,
                            Name = file.Name,
                            VirtualPath = $"{virtualPath}|{file_index}",
                            ShortName = ShortString(file.Name),
                            ShortVirtualPath = $"{ShortString(virtualPath)}|{file_index}",
                            ArchiveDepth = depth + 1
                        };

                        if (file.Name.EndsWith(".tpf.dcx", StringComparison.OrdinalIgnoreCase)
                            || file.Name.EndsWith(".tpf", StringComparison.OrdinalIgnoreCase)
                            || IsTpfData(file.Bytes))
                        {
                            node.Type = NodeType.NestedTpfArchive;
                            node.Children.AddRange(ReadNestedTPF(file.Bytes, depth + 1, node.VirtualPath));
                        }
                        else if (IsDcxData(file.Bytes) && depth < MaxDepth)
                        {
                            try
                            {
                                var inner = DCX.Decompress(file.Bytes);
                                if (IsTpfData(inner))
                                {
                                    node.Type = NodeType.NestedTpfArchive;
                                    node.Children.AddRange(ReadNestedTPF(inner, depth + 1, node.VirtualPath));
                                }
                            }
                            catch { }
                        }

                        nodes.Add(node);
                        file_index++;
                    }
                }
                catch
                {
                    nodes.Add(new FileNode { Name = "Invalid nested TPF" });
                }
            }

            return nodes;
        }
        private bool IsValidFile(string path) => FileSignatures.IsValidGameFile(path);

        private static bool IsBnd(string p) => FileSignatures.IsBnd(p);
        private static bool IsTpf(string p) => FileSignatures.IsTpf(p);
        private static bool IsFlver(string p) => FileSignatures.IsFlver(p);
        private static bool IsBxf(string p) => FileSignatures.IsBxf(p);

        private static bool IsBndData(byte[] b) => FileSignatures.IsBndData(b);
        private static bool IsTpfData(byte[] b) => FileSignatures.IsTpfData(b);
        private static bool IsBxfData(byte[] b) => FileSignatures.IsBxfData(b);
        private static bool IsFlvData(byte[] b) => FileSignatures.IsFlvData(b);
        private static bool IsDcxData(byte[] b) => FileSignatures.IsDcxData(b);
    }
}
