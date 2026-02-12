using System.IO;
using System.Collections.Generic;
using SoulsFormats;
using ImGuiNET;
using SharpGen.Runtime;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Veldrid.MetalBindings;

//using DSRFileViewer.DDSHelper;

namespace DSRViewer.FileHelper.FileExplorer.TreeBuilder
{
    public class FileTreeNodeFastBuilder
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
        private bool IsValidFile(string path)
        {
            HashSet<string> _fileExtensions = new() { ".chrbnd", ".partsbnd", ".tpfbhd", ".ffxbnd", ".rumblebnd", ".objbnd", ".fgbnd", ".msgbnd", ".mtdbnd", ".anibnd", ".chresdbnd", ".remobnd", ".shaderbnd", ".parambnd", ".tpf", ".flver" };
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
