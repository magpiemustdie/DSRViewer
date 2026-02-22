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
        public FlverMTDReplacer(string windowName, bool showWindow)
        {
            _windowName = windowName;
            _showWindow = showWindow;
        }
        public void Render(List<FileNode> flverfilelist, List<MTDShortDetails> mtdList)
        {
            if (_showWindow)
            {
                ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
                {
                    ImGui.BeginChild("Cld_MTDRW", new Vector2(0, 0), _childFlags);
                    {
                        ImGui.SetNextItemWidth(300);
                        ImGui.InputText($"tex_finder", ref texturename, 100);
                        ImGui.SetNextItemWidth(300);
                        ImGui.InputText($"mtd_finder", ref mtdnamefinder, 100);
                        ImGui.SetNextItemWidth(300);
                        ImGui.InputText($"mtd_new", ref mtdnewname, 100);
                        ImGui.SetNextItemWidth(300);
                        ImGui.InputText($"new_height", ref heightnewname, 100);

                        ShowMaterial(mtdList);

                        if (ImGui.Button("Replace mtd (full)"))
                        {
                            List<string> fileList = flverfilelist
                                .Select(fileNode => fileNode.VirtualPath)
                                .ToList();

                            // Создаем имя файла лога с временной меткой
                            string logFileName = $"mtd_replacement_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                            string logFilePath = Path.Combine(logFileName);

                            using (StreamWriter logWriter = new StreamWriter(logFilePath, append: true))
                            {
                                logWriter.WriteLine($"MTD Replacement Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                                logWriter.WriteLine("=========================================");
                                logWriter.WriteLine();

                                int successCount = 0;
                                int failCount = 0;
                                int skippedCount = 0;

                                var binder = new FileBinders();
                                var operation = new FileOperation
                                {
                                    WriteObject = true,
                                    UseFlverDelegate = true,
                                    AdditionalFlverProcessing = (flver, virtualPath, name, errorLogs) =>
                                    {
                                        try
                                        {
                                            Console.WriteLine($"MTD replace delegate -> rp: {virtualPath} n: {name}");
                                            List<FLVER2.Material> flver_materials = flver.Materials;

                                            if (_flverTools.TexFinder(flver_materials, texturename))
                                            {
                                                logWriter.WriteLine($"  Texture '{texturename}' found");

                                                if (_flverTools.MTDFinder(flver_materials, texturename, mtdnamefinder))
                                                {
                                                    logWriter.WriteLine($"  MTD '{mtdnamefinder}' found");
                                                    Console.WriteLine($"Try to replace MTD (full): {virtualPath}");
                                                    logWriter.WriteLine($"  Attempting MTD replacement...");

                                                    flver_materials = _flverTools.MTDReplacerHeight(mtdList, flver_materials, texturename, mtdnamefinder, mtdnewname, heightnewname);
                                                    _flverTools.FlverMTDWriter(flver, flver_materials, virtualPath);

                                                    Console.WriteLine($"Write: {virtualPath}");
                                                    logWriter.WriteLine($"  SUCCESS: MTD replaced with '{mtdnewname}', height map '{heightnewname}'");
                                                    successCount++;
                                                }
                                                else
                                                {
                                                    logWriter.WriteLine($"  SKIPPED: MTD '{mtdnamefinder}' not found for texture '{texturename}'");
                                                    skippedCount++;
                                                }
                                            }
                                            else
                                            {
                                                logWriter.WriteLine($"  SKIPPED: Texture '{texturename}' not found");
                                                skippedCount++;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Fail: {virtualPath}");
                                            logWriter.WriteLine($"  FAILED: {ex.Message}");
                                            logWriter.WriteLine($"  Stack Trace: {ex.StackTrace}");
                                            failCount++;
                                        }
                                    }
                                };

                                binder.ProcessPaths(fileList, operation);

                                logWriter.WriteLine(); // Пустая строка для разделения записей

                                // Записываем итоговую статистику
                                logWriter.WriteLine("=========================================");
                                logWriter.WriteLine("SUMMARY:");
                                logWriter.WriteLine($"  Total files processed: {flverfilelist.Count}");
                                logWriter.WriteLine($"  Successfully replaced: {successCount}");
                                logWriter.WriteLine($"  Failed: {failCount}");
                                logWriter.WriteLine($"  Skipped: {skippedCount}");
                                logWriter.WriteLine($"  Log file saved to: {logFilePath}");

                                Console.WriteLine($"MTD replacement completed. Log saved to: {logFilePath}");
                            }
                        }

                        if (ImGui.Button("Replace mtd (only name)"))
                        {
                            List<string> fileList = flverfilelist
                                .Select(fileNode => fileNode.VirtualPath)
                                .ToList();

                            // Создаем имя файла лога с временной меткой
                            string logFileName = $"mtd_replacement_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                            string logFilePath = Path.Combine(logFileName);

                            using (StreamWriter logWriter = new StreamWriter(logFilePath, append: true))
                            {
                                logWriter.WriteLine($"MTD Replacement Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                                logWriter.WriteLine("=========================================");
                                logWriter.WriteLine();

                                int successCount = 0;
                                int failCount = 0;
                                int skippedCount = 0;

                                var binder = new FileBinders();
                                var operation = new FileOperation
                                {
                                    WriteObject = true,
                                    UseFlverDelegate = true,
                                    AdditionalFlverProcessing = (flver, virtualPath, name, errorLogs) =>
                                    {
                                        try
                                        {
                                            Console.WriteLine($"MTD replace delegate -> rp: {virtualPath} n: {name}");
                                            List<FLVER2.Material> flver_materials = flver.Materials;

                                            if (_flverTools.TexFinder(flver_materials, texturename))
                                            {
                                                logWriter.WriteLine($"  Texture '{texturename}' found");

                                                if (_flverTools.MTDFinder(flver_materials, texturename, mtdnamefinder))
                                                {
                                                    logWriter.WriteLine($"  MTD '{mtdnamefinder}' found");
                                                    Console.WriteLine($"Try to replace MTD: {virtualPath}");
                                                    logWriter.WriteLine($"  Attempting MTD name replacement...");

                                                    _flverTools.MTDReplacer(flver_materials, texturename, mtdnamefinder, mtdnewname);
                                                    _flverTools.FlverMTDWriter(flver, flver_materials, virtualPath);

                                                    Console.WriteLine($"Write: {virtualPath}");
                                                    logWriter.WriteLine($"  SUCCESS: MTD name replaced with '{mtdnewname}'");
                                                    successCount++;
                                                }
                                                else
                                                {
                                                    logWriter.WriteLine($"  SKIPPED: MTD '{mtdnamefinder}' not found for texture '{texturename}'");
                                                    skippedCount++;
                                                }
                                            }
                                            else
                                            {
                                                logWriter.WriteLine($"  SKIPPED: Texture '{texturename}' not found");
                                                skippedCount++;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Fail: {virtualPath}");
                                            logWriter.WriteLine($"  FAILED: {ex.Message}");
                                            logWriter.WriteLine($"  Stack Trace: {ex.StackTrace}");
                                            failCount++;
                                        }
                                    }
                                };

                                binder.ProcessPaths(fileList, operation);

                                logWriter.WriteLine(); // Пустая строка для разделения записей

                                // Записываем итоговую статистику
                                logWriter.WriteLine("=========================================");
                                logWriter.WriteLine("SUMMARY:");
                                logWriter.WriteLine($"  Total files processed: {flverfilelist.Count}");
                                logWriter.WriteLine($"  Successfully replaced: {successCount}");
                                logWriter.WriteLine($"  Failed: {failCount}");
                                logWriter.WriteLine($"  Skipped: {skippedCount}");
                                logWriter.WriteLine($"  Log file saved to: {logFilePath}");

                                Console.WriteLine($"MTD replacement completed. Log saved to: {logFilePath}");
                            }
                        }
                    }
                    ImGui.EndChild();
                }
                ImGui.End();
            }
        }

        private void ShowMaterial(List<MTDShortDetails> mtdList)
        {
            if (ImGui.Button("Test material"))
            {
                foreach (var m in mtdList)
                {
                    if (m.Name == mtdnamefinder)
                    {
                        Console.WriteLine(m.Name);
                        foreach (var tex in m.TexType)
                        {
                            Console.WriteLine(tex);
                        }
                    }
                }

                foreach (var n in mtdList)
                {
                    if (n.Name == mtdnewname)
                    {
                        Console.WriteLine(n.Name);
                        foreach (var tex in n.TexType)
                        {
                            Console.WriteLine(tex);
                        }
                    }
                }
            }
        }
    }
}
