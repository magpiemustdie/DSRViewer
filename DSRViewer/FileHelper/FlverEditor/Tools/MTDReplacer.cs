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
        public void Render(List<FileNode> flverfilelist, List<MTDShortDetails> _mtdList)
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
                        ImGui.InputText($"mtd_replacer", ref mtdnewname, 100);
                        ImGui.SetNextItemWidth(300);
                        ImGui.InputText($"new_height", ref heightnewname, 100);

                        if (ImGui.Button("Replace mtd (full)"))
                        {
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

                                foreach (var file in flverfilelist)
                                {
                                    try
                                    {
                                        logWriter.WriteLine($"Processing: {file.VirtualPath}");
                                        logWriter.WriteLine($"File: {file.Name}");

                                        var binder = new FileBinders();
                                        var operation = new FileOperation
                                        {
                                            GetObject = true
                                        };
                                        binder.ProcessPaths(new[] { file.VirtualPath }, operation);
                                        FLVER2 flver_main = (FLVER2)binder.GetObject();
                                        List<FLVER2.Material> flver_materials = flver_main.Materials;

                                        if (_flverTools.TexFinder(flver_materials, texturename))
                                        {
                                            logWriter.WriteLine($"  Texture '{texturename}' found");

                                            if (_flverTools.MTDFinder(flver_materials, texturename, mtdnamefinder))
                                            {
                                                logWriter.WriteLine($"  MTD '{mtdnamefinder}' found");
                                                Console.WriteLine($"Try to replace MTD (full): {file.VirtualPath}");
                                                logWriter.WriteLine($"  Attempting MTD replacement...");

                                                flver_materials = _flverTools.MTDReplacerHeight(_mtdList, flver_materials, texturename, mtdnamefinder, mtdnewname, heightnewname);
                                                _flverTools.FlverWriter(flver_main, flver_materials, file.VirtualPath);

                                                Console.WriteLine($"Write: {file.Name}");
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
                                        Console.WriteLine($"Fail: {file.Name}");
                                        logWriter.WriteLine($"  FAILED: {ex.Message}");
                                        logWriter.WriteLine($"  Stack Trace: {ex.StackTrace}");
                                        failCount++;
                                    }

                                    logWriter.WriteLine(); // Пустая строка для разделения записей
                                }

                                // Записываем итоговую статистику
                                logWriter.WriteLine("=========================================");
                                logWriter.WriteLine("SUMMARY:");
                                logWriter.WriteLine($"  Total files processed: {flverfilelist.Count}");
                                logWriter.WriteLine($"  Successfully replaced: {successCount}");
                                logWriter.WriteLine($"  Failed: {failCount}");
                                logWriter.WriteLine($"  Skipped: {skippedCount}");
                                logWriter.WriteLine($"  Log file saved to: {logFilePath}");
                            }

                            // Также выводим итог в консоль
                            Console.WriteLine($"MTD replacement completed. Log saved to: {logFilePath}");
                        }


                        if (ImGui.Button("Replace mtd (only name)"))
                        {
                            // Создаем имя файла лога с временной меткой
                            string logFileName = $"mtd_name_replacement_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logFileName);

                            using (StreamWriter logWriter = new StreamWriter(logFilePath, append: true))
                            {
                                logWriter.WriteLine($"MTD Name Replacement Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                                logWriter.WriteLine("=========================================");
                                logWriter.WriteLine($"Search texture: {texturename}");
                                logWriter.WriteLine($"Find MTD: {mtdnamefinder}");
                                logWriter.WriteLine($"Replace with MTD: {mtdnewname}");
                                logWriter.WriteLine();

                                int successCount = 0;
                                int failCount = 0;
                                int skippedCount = 0;

                                foreach (var file in flverfilelist)
                                {
                                    try
                                    {
                                        logWriter.WriteLine($"Processing: {file.VirtualPath}");
                                        logWriter.WriteLine($"File: {file.Name}");

                                        var binder = new FileBinders();
                                        var operation = new FileOperation
                                        {
                                            GetObject = true
                                        };
                                        binder.ProcessPaths(new[] { file.VirtualPath }, operation);
                                        FLVER2 flver_main = (FLVER2)binder.GetObject();
                                        List<FLVER2.Material> flver_materials = flver_main.Materials;

                                        if (_flverTools.TexFinder(flver_materials, texturename))
                                        {
                                            logWriter.WriteLine($"  Texture '{texturename}' found");

                                            if (_flverTools.MTDFinder(flver_materials, texturename, mtdnamefinder))
                                            {
                                                logWriter.WriteLine($"  MTD '{mtdnamefinder}' found");
                                                Console.WriteLine($"Try to replace MTD: {file.VirtualPath}");
                                                logWriter.WriteLine($"  Attempting MTD name replacement...");

                                                _flverTools.MTDReplacer(flver_materials, texturename, mtdnamefinder, mtdnewname);
                                                _flverTools.FlverWriter(flver_main, flver_materials, file.VirtualPath);

                                                Console.WriteLine($"Write: {file.Name}");
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
                                        Console.WriteLine($"Fail: {file.Name}");
                                        logWriter.WriteLine($"  FAILED: {ex.Message}");
                                        logWriter.WriteLine($"  Stack Trace: {ex.StackTrace}");
                                        failCount++;
                                    }

                                    logWriter.WriteLine(); // Пустая строка
                                }

                                // Записываем итоговую статистику
                                logWriter.WriteLine("=========================================");
                                logWriter.WriteLine("SUMMARY:");
                                logWriter.WriteLine($"  Total files processed: {flverfilelist.Count}");
                                logWriter.WriteLine($"  Successfully replaced: {successCount}");
                                logWriter.WriteLine($"  Failed: {failCount}");
                                logWriter.WriteLine($"  Skipped: {skippedCount}");
                                logWriter.WriteLine($"  Log file saved to: {logFilePath}");
                            }

                            // Также выводим итог в консоль
                            Console.WriteLine($"MTD name replacement completed. Log saved to: {logFilePath}");
                        }
                    }
                    ImGui.EndChild();
                }
                ImGui.End();
            }
        }
    }
}
