using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using Veldrid;
using SoulsFormats;
using DSRViewer.DDSHelper;
using DSRViewer.FileHelper.DDSHelper;
using DSRViewer.FileHelper;
using DSRViewer.ImGuiHelper;
using System.Reflection;
using DSRViewer.FileHelper.FileExplorer.TreeBuilder;

namespace DSRFileViewer.FilesHelper
{
    public class Injector : ImGuiChild
    {
        string file_path = "";
        string folder_path = "";

        public void Render(FileNode root, FileNode selected, GraphicsDevice gd, ImGuiController cl)
        {
            if (ImGui.CollapsingHeader("Injector"))
            {
                //ImGui.SeparatorText("Injector");

                if (ImGui.Button("Select new file"))
                {
                    SelectNewFile();
                }
                ImGui.Text("File: " + file_path.Split("\\").Last());

                if (ImGui.Button("Inject"))
                {
                    Inject(ref root, selected);
                }
            }
        }

        public void Inject(ref FileNode root, FileNode selected)
        {
            byte[] newBytes = [];

            if (file_path != "")
            {
                newBytes = File.ReadAllBytes(file_path);
            }
            else
            {
                SelectNewFile();
                newBytes = File.ReadAllBytes(file_path);
            }

            //FileNode selected = viewer.GetSelectedFile();
            if (selected.IsNestedDDS)
            {
                byte imageFlag = 128;
                string imageFormat = DDSTools.ReadDDSImageFormat(newBytes);
                if (DDS_FlagFormatList.DDSFlagListSet.ContainsKey(imageFormat))
                {
                    imageFlag = Convert.ToByte(DDS_FlagFormatList.DDSFlagListSet[imageFormat]);
                }

                FileBinders binder = new();
                binder.SetCommon(false, true, false);
                binder.SetDds(false, false, newBytes, imageFlag);
                binder.Read(selected.VirtualPath);
            }
            /*
            else if (selected.IsFlver | selected.IsNestedFlver)
            {
                FileBinders binder = new();
                binder.SetFlverReplace(FLVER2.Read(newBytes), true);
                binder.SetAllWriter(true);
                binder.Read(selected.VirtualPath);
            }
            else
            {
                FileBinders binder = new();
                binder.SetAllReplace(newBytes, true);
                binder.SetAllWriter(true);
                binder.Read(selected.VirtualPath);
            }
            */

            FileTreeNodeBuilder builder = new();
            FileNode new_root = builder.BuildTree(selected.VirtualPath.Split("|")[0]);

            if (root.VirtualPath.Split("|")[0] == new_root.VirtualPath)
            {
                root = new_root;
            }
            else
            {
                //WriteRootUpdate(root, new_root);
            }
            Console.WriteLine("Inject done");
        }


        private void SelectNewFile()
        {
            using var fileDialog = new OpenFileDialog(); //Windows dialog
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                file_path = fileDialog.FileName;
                Console.WriteLine(file_path);
            }
        }

        private void SelectNewFolder()
        {
            using (var folderDialog = new FolderBrowserDialog()) //Windows dialog
            {
                folderDialog.Description = "Select directory";
                folderDialog.UseDescriptionForTitle = true;
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    folder_path = folderDialog.SelectedPath;
                }
            }
        }

        private void WriteRootUpdate(FileNode root, FileNode new_root)
        {
            for (int i = 0; i < root.Children.Count; i++)
            {
                if (root.Children[i].VirtualPath.Split("|")[0] == new_root.VirtualPath)
                {
                    root.Children[i] = new_root;
                    break;
                }
                else
                {
                    WriteRootUpdate(root.Children[i], new_root);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="root"></param>
        /// <param name="dirPath"></param>
        public void InjectAll(ref FileNode root, string dirPath)
        {
            if (root == null || !Directory.Exists(dirPath))
                return;

            foreach (var filePath in Directory.GetFiles(dirPath))
            {
                var fileInfo = ParseFilePath(filePath);
                if (fileInfo != null)
                {
                    var targetNodes = FindAllNodesByName(root, fileInfo.Name);

                    foreach (var targetNode in targetNodes)
                    {
                        if (targetNode != null)
                        {
                            file_path = filePath;
                            Inject(ref root, targetNode);
                        }
                    }
                }
            }

            foreach (var subDirectory in Directory.GetDirectories(dirPath))
            {
                InjectAll(ref root, subDirectory);
            }
        }

        private FileInfo ParseFilePath(string filePath)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                if (string.IsNullOrEmpty(fileName))
                    return null;

                var parts = fileName.Split(';');
                if (parts.Length < 2)
                    return null;

                string shortVP = parts[0].Replace("#", "\\").Replace("~", "|");
                string name = parts[1].Split('.')[0]; // Remove extension

                return new FileInfo(shortVP, name);
            }
            catch (Exception ex)
            {
                // Log error or handle appropriately
                Console.WriteLine($"Error parsing file path {filePath}: {ex.Message}");
                return null;
            }
        }

        private FileNode FindNodeByVirtualPath(FileNode node, string shortVirtualPath)
        {
            if (node == null) return null;

            // Check current node
            if (node.ShortVirtualPath == shortVirtualPath)
                return node;

            // Check children recursively
            foreach (var child in node.Children ?? Enumerable.Empty<FileNode>())
            {
                var found = FindNodeByVirtualPath(child, shortVirtualPath);
                if (found != null)
                    return found;
            }

            return null;
        }

        private FileNode FindNodeByName(FileNode node, string shortName)
        {
            if (node == null) return null;

            // Check current node
            if (node.ShortName == shortName)
                return node;

            // Check children recursively
            foreach (var child in node.Children ?? Enumerable.Empty<FileNode>())
            {
                var found = FindNodeByName(child, shortName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private List<FileNode> FindAllNodesByName(FileNode root, string name)
        {
            var results = new List<FileNode>();
            FindAllNodesByNameRecursive(root, name, results);
            return results;
        }

        private void FindAllNodesByNameRecursive(FileNode node, string name, List<FileNode> results)
        {
            if (node == null)
                return;

            // Check if current node matches
            if (node.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(node);
            }

            // Recursively search in children
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    FindAllNodesByNameRecursive(child, name, results);
                }
            }
        }

        private void InjectNode(FileNode targetNode, string filePath, string name)
        {
            // Add any additional injection logic here

        }

        private class FileInfo
        {
            public string ShortVirtualPath { get; }
            public string Name { get; }

            public FileInfo(string shortVirtualPath, string name)
            {
                ShortVirtualPath = shortVirtualPath;
                Name = name;
            }
        }

    }
}
