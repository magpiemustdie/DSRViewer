using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.FileHelper.FileExplorer.Render;
using DSRViewer.ImGuiHelper;
using ImGuiNET;

namespace DSRViewer.FileHelper.Tools
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
                    Extract(selected);
                }
            }
        }

        private void Extract(FileNode selected)
        {
            Console.WriteLine($"Start extraction...{selected.VirtualPath}");
            FileBinders binder = new();
            binder.SetGetObjectOnly();
            binder.Read(selected.VirtualPath);
            binder.Extract(_config.ExtractFolder, selected.Name);
            Console.WriteLine($"Done...{selected.VirtualPath}");
        }

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