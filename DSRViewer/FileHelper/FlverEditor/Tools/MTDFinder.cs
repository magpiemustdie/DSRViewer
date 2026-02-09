using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
using ImGuiNET;
using System.Reflection;
using DSRViewer.FileHelper;
using DSRViewer.ImGuiHelper;

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
                            foreach (var file in flverFileList)
                            {
                                try
                                {
                                    FileBinders binders = new();
                                    binders.SetGetObjectOnly();
                                    binders.Read(file.VirtualPath);
                                    FLVER2 flver_main = (FLVER2)binders.GetObject();
                                    List<FLVER2.Material> flver_materials = flver_main.Materials;
                                    binders = null;

                                    if (_flverTools.MTDFinder(flver_materials, _mtdNameFinder))
                                    {
                                        modelList.Add(file.VirtualPath);
                                        Console.WriteLine($"MTD Found -> : {file.VirtualPath}");
                                    }
                                }
                                catch
                                {
                                    Console.WriteLine($"Fail: {file}");
                                }
                            }

                            foreach (var file in modelList)
                            {
                                Console.WriteLine(file);
                            }

                            File.WriteAllLines("MTDs.txt", modelList);
                        }

                        if (ImGui.Button("Find All MTD"))
                        {
                            List<string> mtdList = [];

                            Dictionary<string, int> countDictionary = [];

                            foreach (var file in flverFileList)
                            {
                                try
                                {
                                    FlverTools flverTools = new();
                                    FileBinders binders = new();
                                    binders.SetGetObjectOnly();
                                    binders.Read(file.VirtualPath);
                                    FLVER2 flver_main = (FLVER2)binders.GetObject();
                                    List<FLVER2.Material> flver_materials = flver_main.Materials;
                                    binders = null;

                                    flverTools.MTDFinderAll(flver_materials, mtdList);
                                }
                                catch
                                {
                                    Console.WriteLine($"Fail: {file}");
                                }
                            }

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
