using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using SoulsFormats;

namespace DSRViewer.FileHelper.FlverEditor.Tools
{
    internal class FlverNameCorrector : ImGuiWindow
    {
        string texcorname = string.Empty;
        string texgtype = string.Empty;
        string texcorname_new = string.Empty;

        public FlverNameCorrector(string windowName, bool showWindow)
        {
            _windowName = windowName;
            _showWindow = showWindow;
        }

        public void Render(List<FileNode> flverfilelist)
        {
            if (_showWindow)
            {
                ImGui.Begin("Tex_corrector_window", ref _showWindow, ImGuiWindowFlags.MenuBar);
                {
                    Cld_MenuBar(flverfilelist);
                    Cld_NameCorrector(flverfilelist);
                    ImGui.End();
                }
            }
        }
        private void Cld_MenuBar(List<FileNode> flverfilelist)
        {
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("Tools"))
                {
                    if (ImGui.MenuItem("Get g_type"))
                    {
                        List<string> gTypeList = [];
                        foreach (var file in flverfilelist)
                        {
                            try
                            {
                                FlverTools flverTools = new();
                                FileBinders binders = new();
                                binders.SetGetObjectOnly();
                                binders.Read(file.VirtualPath);
                                FLVER2 newFlver = (FLVER2)binders.GetObject();
                                List<FLVER2.Material> flver_materials = newFlver.Materials;
                                Console.WriteLine($"Read file:...{file.VirtualPath}");
                                gTypeList = flverTools.GetGType(flver_materials, file.VirtualPath, gTypeList);
                            }
                            catch
                            {
                                Console.WriteLine($"Fail: {file}");
                            }
                        }
                        gTypeList = gTypeList.Distinct().ToList();
                        foreach (var str in gTypeList)
                        {
                            Console.WriteLine(str);
                        }
                    }

                    if (ImGui.MenuItem("Lowcase fix"))
                    {
                        foreach (var file in flverfilelist)
                        {
                            try
                            {
                                FlverTools flverTools = new();
                                FileBinders binders = new();
                                binders.SetGetObjectOnly();
                                binders.Read(file.VirtualPath);
                                FLVER2 newFlver = (FLVER2)binders.GetObject();
                                List<FLVER2.Material> flver_materials = newFlver.Materials;
                                Console.WriteLine($"Read file:...{file.VirtualPath}");
                                if (flverTools.TexCorrectorFinderToLower(flver_materials))
                                {
                                    Console.WriteLine($"Found:...{file.VirtualPath}");
                                    flverTools.TexCorrectorToLower(newFlver, flver_materials);
                                    binders.SetFlver(true, true, newFlver);
                                    binders.SetCommon(false, true);
                                    binders.Read(file.VirtualPath);
                                    Console.WriteLine($"Replace done:...{file.VirtualPath}");
                                }
                                binders = null;
                            }
                            catch
                            {
                                Console.WriteLine($"Fail: {file}");
                            }
                        }
                    }

                    if (ImGui.MenuItem("Find"))
                    {
                        List<string> bug_List = [];
                        foreach (var file in flverfilelist)
                        {
                            try
                            {
                                FlverTools flverTools = new();
                                FileBinders binders = new();
                                binders.SetGetObjectOnly();
                                binders.Read(file.VirtualPath);
                                FLVER2 newFlver = (FLVER2)binders.GetObject();
                                List<FLVER2.Material> flver_materials = newFlver.Materials;
                                Console.WriteLine($"Read file:...{file.VirtualPath}");
                                bug_List = flverTools.TexCorrectorFinder(flver_materials, file.ShortVirtualPath, file.ShortName, bug_List);
                            }
                            catch
                            {
                                Console.WriteLine($"Fail: {file}");
                            }
                        }

                        foreach (var str in bug_List)
                        {
                            Console.WriteLine(str);
                        }
                        File.WriteAllLines("NameBugs.txt", bug_List);
                    }

                    ImGui.EndMenu();
                }
                ImGui.EndMenuBar();
            }
        }

        private void Cld_NameCorrector(List<FileNode> flverfilelist)
        {
            ImGui.BeginChild("Cld_TCW", new Vector2(500, 500), _childFlags);
            {
                ImGui.SetNextItemWidth(300);
                ImGui.InputText($"Set g_type", ref texgtype, 100);
                ImGui.SetNextItemWidth(300);
                ImGui.InputText($"Set tex name", ref texcorname, 100);
                ImGui.SetNextItemWidth(300);
                ImGui.InputText($"Set new tex name", ref texcorname_new, 100);

                if (ImGui.Button("Replace"))
                {
                    foreach (var file in flverfilelist)
                    {
                        try
                        {
                            string path = file.VirtualPath.Split("|")[0];
                            int[] v_path = file.VirtualPath.Split("|").Skip(1).Where(s => int.TryParse(s, out _)).Select(int.Parse).ToArray();

                            FlverTools flverTools = new();
                            FileBinders binders = new();
                            binders.SetGetObjectOnly();
                            binders.Read(file.VirtualPath);
                            FLVER2 newFlver = (FLVER2)binders.GetObject();
                            List<FLVER2.Material> flver_materials = newFlver.Materials;
                            if (flverTools.TexFinder(flver_materials, texcorname))
                            {
                                Console.WriteLine($"Found:...{file.VirtualPath}");
                                flverTools.TexCorrectorReplacer(newFlver, flver_materials, texgtype, texcorname, texcorname_new);
                                binders.SetFlver(true, true, newFlver);
                                binders.SetCommon(false, true);
                                binders.Read(file.VirtualPath);

                                Console.WriteLine($"Replace done:...{file.VirtualPath}");
                            }
                            binders = null;
                        }
                        catch
                        {
                            Console.WriteLine($"Fail: {file}");
                        }
                    }
                }
            }
            ImGui.EndChild();
        }
    }
}
