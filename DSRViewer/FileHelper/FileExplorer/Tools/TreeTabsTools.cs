using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.FileHelper.FileExplorer.DDSHelper;
using ImGuiNET;

namespace DSRViewer.FileHelper.FileExplorer.Tools
{
    public class TreeTabsTools
    {
        public List<FileNode> NodeFlverFinder(FileNode fileNode)
        {
            Console.WriteLine("Start flver finder...");
            var flverList = ReadAllFlvers(fileNode);
            Console.WriteLine("Done");
            return flverList;
        }
        private List<FileNode> ReadAllFlvers(FileNode fileNode)
        {
            List<FileNode> fileNodeList = [];
            RecursiveFlverFinder(fileNode, fileNodeList);
            return fileNodeList;
        }
        private void RecursiveFlverFinder(FileNode fileNode, List<FileNode> fileNodeList)
        {
            foreach (var child in fileNode.Children)
            {
                if (child.IsFlver || child.IsNestedFlver)
                {
                    fileNodeList.Add(child);
                }
                RecursiveFlverFinder(child, fileNodeList);
            }
        }
        public List<FileNode> NodeTexFinder(FileNode fileNode)
        {
            Console.WriteLine("Start texture finder...");
            var texList = ReadAllTex(fileNode);
            Console.WriteLine("Done");
            return texList;
        }


        private List<FileNode> ReadAllTex(FileNode fileNode)
        {
            List<FileNode> fileNodeList = [];
            RecursiveTexFinder(fileNode, fileNodeList);
            return fileNodeList;
        }

        private void RecursiveTexFinder(FileNode fileNode, List<FileNode> fileNodeList)
        {
            foreach (var child in fileNode.Children)
            {
                if (child.IsNestedDDS)
                {
                    fileNodeList.Add(child);
                }
                RecursiveTexFinder(child, fileNodeList);
            }
        }

        //*
        public void GetTexturesDoubles(FileNode root)
        {
            if (ImGui.Button("GetTexturesDoubles"))
            {
                Console.WriteLine("Search doubles:...");
                int counter = 0;
                Dictionary<string, string> fileCount = [];
                counter = FileCounter(root, counter);
                fileCount = Dbl_Counter(root, fileCount);
                File.WriteAllLines("Test_Doubles.txt", fileCount.Select(kvp => $"{kvp.Key}; " + $"{kvp.Value}"));
                Console.WriteLine($"DDS counter: {counter}");
                Console.WriteLine($"DDS w/o doubles counter: {fileCount.Count}");
                Console.WriteLine("Saved in file: Test_Doubles.txt");
            }
        }

        private int FileCounter(FileNode root, int counter)
        {
            foreach (var file in root.Children)
            {
                if (file.IsNestedDDS)
                    counter++;
                counter = FileCounter(file, counter);
            }
            return counter;
        }

        private Dictionary<string, string> Dbl_Counter(FileNode root, Dictionary<string, string> fileCount)
        {
            foreach (var file in root.Children)
            {
                Console.WriteLine(file.Name);
                if (fileCount.ContainsKey(file.Name) & file.IsNestedDDS)
                {
                    fileCount[file.Name] = fileCount[file.Name] + "; " + file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                }
                else
                {
                    if (file.IsNestedDDS)
                    {
                        fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                    }
                }
                fileCount = Dbl_Counter(file, fileCount);
            }
            return fileCount;
        }

        //
        public void GetTexturesFormatErrors(FileNode root)
        {
            if (ImGui.Button("Get tex formats errors"))
            {
                if (root != null)
                {
                    Console.WriteLine("Test for formats errors:...Start");
                    Dictionary<string, string> fileCount = [];
                    fileCount = Format_Counter_Err(root, fileCount);
                    File.WriteAllLines("Format_Err.txt", fileCount.Select(kvp => $"{kvp.Key}; " + $"{kvp.Value}"));
                    Console.WriteLine("Test for formats errors:...Done");
                    Console.WriteLine("Saved in file: Format_Err.txt");
                }
            }
        }

        private Dictionary<string, string> Format_Counter_Err(FileNode root, Dictionary<string, string> fileCount)
        {
            foreach (var file in root.Children)
            {
                Console.WriteLine(file.Name);
                if (file.IsNestedDDS)
                {
                    if (DDS_FlagFormatList.DDSFlagList.ContainsKey(file.DDSFormatFlag))
                    {
                        foreach (var key in DDS_FlagFormatList.DDSFlagList.Keys)
                        {
                            if (file.DDSFormatFlag == key & file.DDSFormat != DDS_FlagFormatList.DDSFlagList[key])
                            {
                                fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                            }
                        }
                    }

                    switch (file.DDSFormatFlag)
                    {
                        case 0:
                            {
                                if (file.Name.ToLower().EndsWith("_n"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }
                                break;
                            }
                        case 1:
                            {
                                if (file.Name.ToLower().EndsWith("_n"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }
                                break;
                            }
                        case 5:
                            {
                                if (file.Name.ToLower().EndsWith("_s"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }

                                if (file.Name.ToLower().EndsWith("_n"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }

                                if (file.Name.ToLower().EndsWith("_t"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }

                                if (file.Name.ToLower().EndsWith("_h"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }
                                break;
                            }
                        case 24:
                            {
                                fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                break;
                            }
                        case 35:
                            {
                                fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                break;
                            }
                        case 36:
                            {
                                if (!file.Name.ToLower().EndsWith("_n"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }
                                break;
                            }
                        case 37:
                            {
                                if (file.Name.ToLower().EndsWith("_s"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }

                                if (file.Name.ToLower().EndsWith("_n"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }

                                if (file.Name.ToLower().EndsWith("_t"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }

                                if (file.Name.ToLower().EndsWith("_h"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }
                                break;
                            }
                        case 38:
                            {
                                if (file.Name.ToLower().EndsWith("_n"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }

                                if (file.Name.ToLower().EndsWith("_t"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }

                                if (file.Name.ToLower().EndsWith("_h"))
                                {
                                    fileCount[file.Name] = file.ShortVirtualPath + "; " + file.DDSFormatFlag + "; " + file.DDSFormat;
                                }
                                break;
                            }
                    }
                }
                fileCount = Format_Counter_Err(file, fileCount);
            }
            return fileCount;
        }
    }
}
