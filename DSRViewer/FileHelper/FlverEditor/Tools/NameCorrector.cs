using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using SoulsFormats;
using Vortice.Direct3D11;

namespace DSRViewer.FileHelper.FlverEditor.Tools
{
    internal class FlverNameCorrector : ImGuiWindow
    {
        string texcorname = string.Empty;
        string texgtype = string.Empty;
        string texcorname_new = string.Empty;
        FlverTools _flverTools = new();
        public FlverNameCorrector(string windowName, bool showWindow)
        {
            _windowName = windowName;
            _showWindow = showWindow;
        }

        public void Render(List<FileNode> flverfilelist)
        {
            if (_showWindow)
            {
                ImGui.Begin(_windowName, ref _showWindow, ImGuiWindowFlags.MenuBar | _windowFlags);
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
                   

                    if (ImGui.MenuItem("Lowcase fix"))
                    {
                        List<string> fileList = flverfilelist
                        .Select(fileNode => fileNode.VirtualPath)
                        .ToList();

                        var binder = new FileBinders();
                        var operation = new FileOperation
                        {
                            GetObject = true,
                            WriteObject = true,
                            UseFlverDelegate = true,
                            AdditionalFlverProcessing = (flver, realPath, path) =>
                            {
                                Console.WriteLine($"Lowcase fix delegate -> rp: {realPath} P: {path}");
                                List<FLVER2.Material> flver_materials = flver.Materials;
                                _flverTools.TexCorrectorFinderToLower(flver_materials);
                                _flverTools.TexCorrectorToLower(flver, flver_materials);
                            }
                        };

                        binder.ProcessPaths(fileList, operation);
                    }

                    
                    if (ImGui.MenuItem("Find errors"))
                    {
                        List<string> bug_List = [];
                        List<string> fileList = flverfilelist
                        .Select(fileNode => fileNode.VirtualPath)
                        .ToList();

                        var binder = new FileBinders();
                        var operation = new FileOperation
                        {
                            GetObject = true,
                            WriteObject = true,
                            UseFlverDelegate = true,
                            AdditionalFlverProcessing = (flver, realPath, path) =>
                            {
                                Console.WriteLine($"Find errors delegate -> rp: {realPath} p: {path}");
                                List<FLVER2.Material> flver_materials = flver.Materials;
                                bug_List = _flverTools.TexCorrectorFinder(flver_materials, realPath, path, bug_List);
                            }
                        };

                        binder.ProcessPaths(fileList, operation);

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
            ImGui.BeginChild("Cld_TCW", new Vector2(0, 0), _childFlags);
            {
                ImGui.SetNextItemWidth(300);
                ImGui.InputText($"Set g_type", ref texgtype, 256);
                ImGui.SetNextItemWidth(300);
                ImGui.InputText($"Set tex name", ref texcorname, 256);
                ImGui.SetNextItemWidth(300);
                ImGui.InputText($"Set new tex name", ref texcorname_new, 256);

                if (ImGui.Button("Replace"))
                {
                    List<string> fileList = flverfilelist
                    .Select(fileNode => fileNode.VirtualPath)
                    .ToList();

                    var binder = new FileBinders();
                    var operation = new FileOperation
                    {
                        WriteObject = true,
                        UseFlverDelegate = true,
                        AdditionalFlverProcessing = (flver, realPath, path) =>
                        {
                            Console.WriteLine($"Replace name delegate -> rp: {realPath} P: {path}");
                            List<FLVER2.Material> flver_materials = flver.Materials;

                            if (_flverTools.TexFinder(flver_materials, texcorname))
                            {
                                _flverTools.TexCorrectorReplacer(flver, flver_materials, texgtype, texcorname, texcorname_new);
                            }
                        }
                    };

                    binder.ProcessPaths(fileList, operation);
                }
            }
            ImGui.EndChild();
        }
    }
}
