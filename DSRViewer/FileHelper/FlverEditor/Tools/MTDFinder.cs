using DSRViewer.FileHelper;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using SharpGen.Runtime.Win32;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D11;

namespace DSRViewer.FileHelper.FlverEditor.Tools
{
    public class FlverMTDFinder : ImGuiWindow
    {
        string _mtdNameFinder = string.Empty;
        FlverTools _flverTools = new();

        public FlverMTDFinder(string windowName, bool showWindow)
        {
            _windowName = windowName;
            _showWindow = showWindow;
        }

        public void Render(List<FileNode> flverFileList)
        {
            if (_showWindow)
            {
                ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
                {
                    ImGui.BeginChild("Cld_MTDFW", new Vector2(0, 0), _childFlags);
                    {
                        ImGui.SetNextItemWidth(300);
                        ImGui.InputText($"mtd_finder", ref _mtdNameFinder, 255);

                        if (ImGui.Button("Find MTD"))
                        {
                            List<string> modelList = [];

                            List<string> tempList = flverFileList.Where(node => node.VirtualPath != null)
                            .Select(node => node.VirtualPath)
                            .ToList();

                            
                            var binder = new FileBinders();
                            var operation = new FileOperation
                            {
                                UseFlverDelegate = true,
                                AdditionalFlverProcessing = (flver, virtualPath, name, errorLogs) =>
                                {
                                    List<FLVER2.Material> flver_materials = flver.Materials;

                                    if (_flverTools.MTDFinder(flver_materials, _mtdNameFinder))
                                    {
                                        modelList.Add(virtualPath);
                                        Console.WriteLine($"MTD Found -> : {virtualPath}");
                                    }
                                }
                            };
                            binder.ProcessPaths(tempList, operation);

                            File.WriteAllLines("MTDs.txt", modelList);
                        }

                            
                        }

                        if (ImGui.Button("Find All MTD"))
                        {
                            List<string> mtdList = [];
                            
                            List<string> tempList = flverFileList.Where(node => node.VirtualPath != null)
                            .Select(node => node.VirtualPath)
                            .ToList();

                            Dictionary<string, int> countDictionary = [];

                        var binder = new FileBinders();
                        var operation = new FileOperation
                        {
                            UseFlverDelegate = true,
                            AdditionalFlverProcessing = (flver, virtualPath, name, errorLogs) =>
                            {
                                List<FLVER2.Material> flver_materials = flver.Materials;

                                _flverTools.MTDFinderAll(flver_materials, mtdList);
                            }
                        };
                        binder.ProcessPaths(tempList, operation);

                        foreach (string str in mtdList)
                            {
                                if (countDictionary.ContainsKey(str))
                                {
                                    countDictionary[str]++;
                                }
                                else
                                {
                                    countDictionary[str] = 1;
                                }
                            }

                            File.WriteAllLines("MTDCount.txt", countDictionary.Select(kvp => $"{kvp.Key}; " + $"{kvp.Value}"));
                            Console.WriteLine("Done, Saved in MTDCount.txt");
                        }
                    }
                    ImGui.EndChild();
                }
                ImGui.End();
            }
        }
    }
}
