using System.IO;
using System.Collections.Generic;
using SoulsFormats;
using ImGuiNET;
using SharpGen.Runtime;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Veldrid.MetalBindings;
using DSRViewer.FileHelper.FileExplorer.DDSHelper;

namespace DSRViewer.FileHelper.FileExplorer.TreeBuilder
{
    public class FileTreeNodeBuilder
    {
        private const int MaxDepth = 8; // Prevent infinite recursion

        public FileNode BuildTree(string rootPath)
        {
            var RootNode = new FileNode
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
                Console.WriteLine("Please select Root");
            }

            return RootNode;
        }

        private string ShortString(string str)
        {
            string[] parts = str.Split(new[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
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

                int file_index = 0;
                foreach (var file in bnd.Files)
                {
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

                    if (IsTpfData(file.Bytes) && depth < MaxDepth)
                    {
                        child.Type = NodeType.NestedTpfArchive;
                        child.Children.AddRange(ReadNestedTPF(file.Bytes, depth + 1, child.VirtualPath));
                    }

                    if (IsBxfData(file.Bytes) && depth < MaxDepth)
                    {
                        child.Type = NodeType.NestedBxfArchive;
                        child.Children.AddRange(ReadNestedBXF(file.Bytes, file.Name, bndPath, depth + 1, child.VirtualPath));
                    }

                    if (IsFlvData(file.Bytes) && depth < MaxDepth)
                    {
                        child.Type = NodeType.NestedFlver;
                    }

                    node.Children.Add(child);

                    file_index++;
                }
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

                int file_index = 0;
                foreach (var file in tpf.Textures)
                {
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
                    node.Children.Add(child);
                    file_index++;
                }
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

            string bdtPath = bhdPath.Replace(".tpfbhd", ".tpfbdt");

            try
            {
                if (File.Exists(bdtPath))
                {
                    var bxf = BXF3.Read(bhdPath, bdtPath);
                    int file_index = 0;
                    foreach (var file in bxf.Files)
                    {
                        var child = new FileNode
                        {
                            ID = file_index,
                            Name = file.Name,
                            VirtualPath = $"{bhdPath}|{file_index}",
                            ShortName = ShortString(file.Name),
                            ShortVirtualPath = $"{ShortString(bhdPath)}|{file_index}",
                            ArchiveDepth = depth + 1
                        };
                        node.Children.Add(child);

                        if (file.Name.EndsWith(".tpf.dcx") || file.Name.EndsWith(".tpf") || IsTpfData(DCX.Decompress(file.Bytes)) && depth < MaxDepth)
                        {
                            child.Type = NodeType.NestedTpfArchive;
                            child.Children.AddRange(ReadNestedTPF(file.Bytes, depth + 1, child.VirtualPath));
                        }
                        file_index++;
                    }
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
                int file_index = 0;
                foreach (var file in nestedBnd.Files)
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

                    if (IsBndData(file.Bytes) && depth < MaxDepth)
                    {
                        node.Type = NodeType.NestedBndArchive;
                        node.Children.AddRange(ReadNestedBnd(file.Bytes, depth + 1, node.VirtualPath));
                    }

                    if (IsTpfData(file.Bytes) && depth < MaxDepth)
                    {
                        node.Type = NodeType.NestedTpfArchive;
                        node.Children.AddRange(ReadNestedTPF(file.Bytes, depth + 1, node.VirtualPath));
                    }

                    if (IsFlvData(file.Bytes) && depth < MaxDepth)
                    {
                        node.Type = NodeType.NestedFlver;
                    }

                    nodes.Add(node);
                    file_index++;
                }
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
                //using var ms = new MemoryStream(bndData);
                var nestedTPF = TPF.Read(tpfData);
                int file_index = 0;
                foreach (var file in nestedTPF.Textures)
                {
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
                    nodes.Add(node);
                    file_index++;
                }
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
            string bdtPath = bndPath.Replace(".chrbnd.dcx", ".chrtpfbdt");

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

                        if (file.Name.EndsWith(".tpf.dcx") || file.Name.EndsWith(".tpf") || IsTpfData(DCX.Decompress(file.Bytes)) && depth < MaxDepth)
                        {
                            node.Type = NodeType.NestedTpfArchive;
                            node.Children.AddRange(ReadNestedTPF(file.Bytes, depth + 1, node.VirtualPath));
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
        private bool IsValidFile(string path)
        {
            HashSet<string> _fileExtensions = new() { ".partsbnd", ".tpf", ".flver", "tpfbhd", ".chrbnd", ".ffxbnd", ".rumblebnd", ".objbnd", ".fgbnd", ".msgbnd", ".mtdbnd", ".anibnd", ".chresdbnd", ".remobnd", ".shaderbnd", ".parambnd" };
            if (_fileExtensions.Any(name => path.EndsWith(name) || path.EndsWith(name + ".dcx")))
                return true;
            return false;
        }

        // Вспомогательные методы для проверки типов файлов с учетом DCX
        private static bool IsBnd(string p) => HasExtension(p,
            ".chrbnd", ".partsbnd", ".ffxbnd", ".rumblebnd", ".objbnd", ".fgbnd", ".msgbnd", ".mtdbnd", ".anibnd", ".chresdbnd", ".remobnd", ".shaderbnd", ".parambnd");

        private static bool IsTpf(string p) => HasExtension(p, ".tpf");
        private static bool IsFlver(string p) => HasExtension(p, ".flver");
        private static bool IsBxf(string p) => HasExtension(p, ".tpfbhd");

        private static bool HasExtension(string path, params string[] extensions)
        {
            var pathLower = path.ToLowerInvariant();
            return extensions.Any(ext =>
                pathLower.EndsWith(ext.ToLowerInvariant()) ||
                pathLower.EndsWith(ext.ToLowerInvariant() + ".dcx"));
        }

        private static bool IsBndData(byte[] b) => b.Length >= 4 && b[0] == 'B' && b[1] == 'N' && b[2] == 'D' && b[3] == '3';
        private static bool IsTpfData(byte[] b) => b.Length >= 3 && b[0] == 'T' && b[1] == 'P' && b[2] == 'F';
        private static bool IsBxfData(byte[] b) => b.Length >= 4 && b[0] == 'B' && b[1] == 'H' && b[2] == 'F' && b[3] == '3';
        private static bool IsFlvData(byte[] b) => b.Length >= 5 && b[0] == 'F' && b[1] == 'L' && b[2] == 'V' && b[3] == 'E' && b[4] == 'R';
        private static bool IsDcxData(byte[] b) => b.Length >= 3 && b[0] == 'D' && b[1] == 'C' && b[2] == 'X';
    }

}
