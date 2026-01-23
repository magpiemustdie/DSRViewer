using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.FileHelper.FileExplorer.Render;
using DSRViewer.FileHelper.MTDEditor;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using SoulsFormats;
using Veldrid;
using DSRViewer.FileHelper.FileExplorer.TreeBuilder;

namespace DSRViewer.FileHelper.FlverEditor.Render
{
    public class FMW : ImGuiWindow
    {
        FlverFileList _fileListViewer = new();
        FileNode _prevFileNode = new();
        FileNode _selected = new();
        bool _isOpen = false;

        public bool IsWindowOpen() => _isOpen;

        public override void Render()
        {
            if (_showWindow)
            {
                ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
                {
                    OpenNewFileButton();
                    _fileListViewer.Render();
                }
                ImGui.End();
            }
        }

        /*
        public override void Render(FileNode selected)
        {
            if (_prevFileNode != selected)
            {
                _selected = selected;

            }
            if (_showWindow)
            {
                ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
                {
                    _fileListViewer.Render();
                    ImGui.Text("Render flver");
                }
                ImGui.End();
            }
        }
        */

        public void SetNewItem(FileNode fileNode)
        {
            _fileListViewer.AddItemToList(fileNode);
        }

        private void OpenNewFileButton()
        {
            if (ImGui.Button("Open new file..."))
            {
                SetFile();
            }
        }
        private void SetFile()
        {
            using (var fileDialog = new OpenFileDialog()) //Windows dialog
            {
                var thread = new Thread(() =>
                {
                    if (fileDialog.ShowDialog() == DialogResult.OK)
                    {
                        var file = fileDialog.FileName;
                        Console.WriteLine("open file: ...");
                        FileTreeNodeBuilder builder = new();
                        FileNode fileNode = builder.BuildTree(file);
                        _fileListViewer.AddItemToList(fileNode);
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            }
        }
    }
}

