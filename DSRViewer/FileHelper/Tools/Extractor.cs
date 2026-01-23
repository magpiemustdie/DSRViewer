using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSRFileViewer;
using DSRViewer.ImGuiHelper;
using ImGuiNET;

namespace DSRViewer.FileHelper.Tools
{
    public class Extractor : ImGuiChild
    {
        string extractFolder = "";
        public void Render(FileNode selected)
        {
            if (ImGui.CollapsingHeader("Extractor"))
            {
                //ImGui.SeparatorText("Extractor");

                if (ImGui.Button("Select extract folder"))
                    SetExtractFolder();
                if (ImGui.Button("Open extract folder"))
                    OpenExtractFolder(extractFolder);

                ImGui.Text("Dir: " + extractFolder.Split("\\").Last());

                if (ImGui.Button("Extract"))
                {
                    Extract(selected);
                }
            }
        }

        private void Extract(FileNode selected)
        {
            FileBinders binder = new();
            binder.SetGetObjectOnly();
            binder.Read(selected.VirtualPath);
            binder.Extract(extractFolder, selected.Name);
        }

        public void SetExtractFolder()
        {
            var thread = new Thread(() =>
            {
                using (var folderDialog = new FolderBrowserDialog()) //Windows dialog
                {
                    folderDialog.Description = "Select a directory";
                    folderDialog.UseDescriptionForTitle = true;
                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        extractFolder = folderDialog.SelectedPath;
                    }
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        private void OpenExtractFolder(string selectedFolder)
        {
            Process.Start("explorer.exe", selectedFolder);
        }
    }
}
