using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
using ImGuiNET;
using System.Reflection;
using DSRViewer.ImGuiHelper;
using DSRViewer.FileHelper.FlverEditor.Tools;
using DSRViewer.FileHelper.MTDEditor.Render;

namespace DSRViewer.FileHelper.flverTools.Tools
{
    internal class FlverMTDReplacer : ImGuiWindow
    {
        string texturename = string.Empty;
        string mtdnamefinder = string.Empty;
        string mtdnewname = string.Empty;
        string heightnewname = string.Empty;

        FlverTools _flverTools = new();
        List<MTDShortDetails> _mtdList = [];

        public FlverMTDReplacer(string windowName, bool showWindow, List<MTDShortDetails> mtdList)
        {
            _windowName = windowName;
            _showWindow = showWindow;
            _mtdList = mtdList;
        }
        public void Render(List<FileNode> flverfilelist)
        {
            if (_showWindow)
            {
                ImGui.Begin("MTD_Replacer_window", ref _showWindow);
                {
                    ImGui.BeginChild("Cld_MTDRW", new Vector2(500, 500), _childFlags);
                    {
                        ImGui.SetNextItemWidth(300);
                        ImGui.InputText($"tex_finder", ref texturename, 100);
                        ImGui.SetNextItemWidth(300);
                        ImGui.InputText($"mtd_finder", ref mtdnamefinder, 100);
                        ImGui.SetNextItemWidth(300);
                        ImGui.InputText($"mtd_replacer", ref mtdnewname, 100);
                        ImGui.SetNextItemWidth(300);
                        ImGui.InputText($"new_height", ref heightnewname, 100);

                        if (ImGui.Button("Replace mtd (name)"))
                        {
                            foreach (var file in flverfilelist)
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

                                    if (flverTools.TexFinder(flver_materials, texturename))
                                    {
                                        if (flverTools.MTDFinder(flver_materials, texturename, mtdnamefinder))
                                        {
                                            Console.WriteLine($"Try to replace MTD: {file.VirtualPath}");
                                            flverTools.MTDReplacer(flver_materials, texturename, mtdnamefinder, mtdnewname);
                                            flverTools.FlverWriter(flver_main, flver_materials, file.VirtualPath);
                                            Console.WriteLine($"Write: {file}");
                                        }
                                    }
                                }
                                catch
                                {
                                    Console.WriteLine($"Fail: {file}");
                                }
                            }
                        }

                        if (ImGui.Button("Replace mtd (full)"))
                        {
                            foreach (var file in flverfilelist)
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

                                    if (flverTools.TexFinder(flver_materials, texturename))
                                    {
                                        if (flverTools.MTDFinder(flver_materials, texturename, mtdnamefinder))
                                        {
                                            Console.WriteLine($"Try to replace MTD (full): {file.VirtualPath}");
                                            flver_materials = flverTools.MTDReplacerHeight(_mtdList, flver_materials, texturename, mtdnamefinder, mtdnewname, heightnewname);
                                            flverTools.FlverWriter(flver_main, flver_materials, file.VirtualPath);
                                            Console.WriteLine($"Write: {file.Name}");
                                        }
                                    }
                                }
                                catch
                                {
                                    Console.WriteLine($"Fail: {file.Name}");
                                }
                            }
                        }
                    }
                    ImGui.EndChild();
                }
                ImGui.End();
            }
        }
    }
}
