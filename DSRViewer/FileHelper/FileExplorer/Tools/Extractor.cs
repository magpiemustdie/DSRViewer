using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.Core;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using SoulsFormats;

namespace DSRViewer.FileHelper.FileExplorer.Tools
{
    public class Extractor : ImGuiChild
    {
        private Config _config;

        // Изменяем конструктор для получения конфига
        public Extractor(Config config)
        {
            _config = config;
        }

        public void Render(FileNode selected)
        {
            if (ImGui.CollapsingHeader("Extractor"))
            {
                // Используем папку из конфига
                if (ImGui.Button("Select extract folder"))
                    SetExtractFolder();
                if (ImGui.Button("Open extract folder"))
                    OpenExtractFolder(_config.ExtractFolder);

                // Показываем текущую папку из конфига
                if (!string.IsNullOrEmpty(_config.ExtractFolder))
                    ImGui.Text("Dir: " + _config.ExtractFolder.Split("\\").Last());
                else
                    ImGui.Text("Dir: Not set");

                if (ImGui.Button("Extract"))
                {
                    if (string.IsNullOrEmpty(_config.ExtractFolder))
                    {
                        Console.WriteLine("Extract folder not set!");
                        return;
                    }

                    ExtractFile(selected, _config.ExtractFolder);
                }
            }
        }

        public static void ExtractFile(FileNode selected, string outputDir)
        {
            
            var binder = new FileBinders();
            var operation = new FileOperation { GetObject = true };

            binder.ProcessPaths(new[] { selected.VirtualPath }, operation);
            var obj = binder.GetObject();

            if (obj is BinderFile file)
            {
                File.WriteAllBytes(Path.Combine(outputDir, selected.Name.Split("\\").Last()), file.Bytes);
            }
            else if (obj is TPF.Texture texture)
            {
                File.WriteAllBytes(Path.Combine(outputDir, selected.Name.Split("\\").Last() + ".dds"), texture.Bytes);
            }
            else if (obj is FLVER2 flver)
            {
                flver.Write(Path.Combine(outputDir, selected.Name.Split("\\").Last()));
            }
            else if (obj is TPF tpf)
            {
                tpf.Write(Path.Combine(outputDir, selected.Name.Split("\\").Last()));
            }
            else if (obj is BND bnd)
            {
                bnd.Write(Path.Combine(outputDir, selected.Name.Split("\\").Last()));
            }
        }

        /*
        private void Extract(FileNode selected)
        {
            Console.WriteLine($"Start extraction...{selected.VirtualPath}");
            FileBinders binder = new();
            binder.SetGetObjectOnly();
            binder.Read(selected.VirtualPath, operation);
            binder.ExtractFile(_config.ExtractFolder, selected.Name);
            Console.WriteLine($"Done...{selected.VirtualPath}");
        }
        */

        public void SetExtractFolder()
        {
            _config.SelectExtractFolder();
        }

        private void OpenExtractFolder(string selectedFolder)
        {
            if (!string.IsNullOrEmpty(selectedFolder) && Directory.Exists(selectedFolder))
            {
                Process.Start("explorer.exe", selectedFolder);
            }
        }
    }
}