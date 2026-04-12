using System.IO;
using System.Collections.Generic;
using SoulsFormats;
using DSRViewer.FileProcess;

namespace DSRViewer.Editors.Explorer.TreeBuilder
{
    /// <summary>Быстро строит дерево FileNode без загрузки содержимого архивов (только структура).</summary>
    public class FileTreeNodeFastBuilder
    {
        private const int MaxDepth = 8; // Prevent infinite recursion

        /// <summary>Строит быстрое дерево FileNode начиная с указанного пути (без раскрытия архивов).</summary>
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
                Console.WriteLine($"[FastBuilder] BuildTree failed: {ex.Message}");
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
        private bool IsValidFile(string path) => FileSignatures.IsValidGameFile(path);

        private static bool IsBnd(string p)   => FileSignatures.IsBnd(p);
        private static bool IsTpf(string p)   => FileSignatures.IsTpf(p);
        private static bool IsFlver(string p) => FileSignatures.IsFlver(p);
        private static bool IsBxf(string p)   => FileSignatures.IsBxf(p);
    }

}
