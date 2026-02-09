using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;

namespace DSRViewer.FileHelper.FileExplorer.Tools
{
    public class Finder
    {
        string inputText = "";
        public void Render(FileNode root)
        {
            if (ImGui.CollapsingHeader("Finder"))
            {
                ImGui.SeparatorText("Finder");

                ImGui.InputText("##Input", ref inputText, 256);

                if (ImGui.Button("Find by full name"))
                {
                    Console.WriteLine("New search:...");

                    FileFinderByFullName(root, inputText);
                }

                if (ImGui.Button("Find by name part"))
                {
                    Console.WriteLine("New search:...");

                    FileFinderByNamePart(root, inputText);
                }
            }
        }

        private void FileFinderByFullName(FileNode root, string inputText)
        {
            foreach (var file in root.Children)
            {
                if (file.Name.ToLower() == inputText.ToLower())
                {
                    Console.WriteLine(file.VirtualPath);
                }
                else
                {
                    FileFinderByFullName(file, inputText);
                }
            }
        }

        private void FileFinderByNamePart(FileNode root, string inputText)
        {
            foreach (var file in root.Children)
            {
                if (file.Name.ToLower().Contains(inputText.ToLower()))
                {
                    Console.WriteLine(file.VirtualPath);
                }
                else
                {
                    FileFinderByNamePart(file, inputText);
                }
            }
        }
    }
}
