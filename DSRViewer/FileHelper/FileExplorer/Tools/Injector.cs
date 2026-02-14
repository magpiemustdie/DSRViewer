using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DSRViewer.FileHelper;
using DSRViewer.FileHelper.FileExplorer.DDSHelper;
using DSRViewer.FileHelper.FileExplorer.TreeBuilder;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using SoulsFormats;

namespace DSRViewer.FileHelper.FileExplorer.Tools
{
    public class Injector : ImGuiChild
    {
        private string _filePath = "";
        private bool _useSelectedFileName = false;
        private bool _success = false;
        private Action<string> _onInjectionComplete; // Колбэк для обновления дерева

        public Action<string> OnInjectionComplete => _onInjectionComplete;

        public Injector(Action<string> onInjectionComplete = null)
        {
            _onInjectionComplete = onInjectionComplete;
        }

        public void Render(FileNode root, FileNode selected)
        {
            if (ImGui.CollapsingHeader("Injector"))
            {
                if (ImGui.Button("Select new file"))
                {
                    _filePath = SelectNewFile();
                }
                ImGui.SameLine();
                ImGui.Text("File: " + (_filePath != "" ? Path.GetFileName(_filePath) : "No file selected"));

                ImGui.Spacing();

                if (ImGui.Button("Inject", new System.Numerics.Vector2(100, 30)))
                {
                    try
                    {
                        if (!_useSelectedFileName)
                            _success = Inject(root, selected, selected.Name);
                        else
                        {
                            if (Path.GetExtension(_filePath) == ".dds")
                                _success = Inject(root, selected, Path.GetFileNameWithoutExtension(_filePath));
                            else
                                _success = Inject(root, selected, Path.GetFileName(_filePath));
                        }
                            

                        if (_success)
                        {
                            _onInjectionComplete?.Invoke(selected.VirtualPath);

                            ImGui.OpenPopup($"InjectionSuccess - {selected.VirtualPath}");
                        }
                        else
                        {
                            ImGui.OpenPopup("InjectionFailed");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Injection error: {ex.Message}");
                        ImGui.OpenPopup("InjectionError");
                    }
                }

                if (ImGui.RadioButton("Use selected file name", _useSelectedFileName))
                    _useSelectedFileName = !_useSelectedFileName;


                // Попап сообщения об успехе
                if (ImGui.BeginPopupModal("InjectionSuccess", ref _success, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text("File injected successfully!");
                    ImGui.Spacing();
                    if (ImGui.Button("OK"))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }

                // Попап сообщения об ошибке
                bool showError = true;
                if (ImGui.BeginPopupModal("InjectionError", ref showError, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text("Injection failed! Check console for details.");
                    ImGui.Spacing();
                    if (ImGui.Button("OK"))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
            }
        }

        private bool Inject(FileNode root, FileNode selected, string newName)
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                Console.WriteLine("No valid file selected for injection");
                return false;
            }

            byte[] newBytes = File.ReadAllBytes(_filePath);
            bool success;
            try
            {
                if (selected.IsNestedDDS)
                {
                    byte imageFlag = 128;
                    string imageFormat = DDSTools.ReadDDSImageFormat(newBytes);
                    if (DDS_FlagFormatList.DDSFlagListSet.ContainsKey(imageFormat))
                        imageFlag = Convert.ToByte(DDS_FlagFormatList.DDSFlagListSet[imageFormat]);

                    var binder = new FileBinders();
                    var operation = new FileOperation
                    {
                        WriteObject = true,
                        ReplaceObject = true,
                        ChangeTextureFormat = true,
                        RenameObject = true,
                        NewObjectBytes = newBytes,
                        NewTextureFormat = imageFlag,
                        NewObjectName = newName
                    };
                    binder.ProcessPaths(new[] {selected.VirtualPath}, operation);
                    success = true;
                }

                else if (selected.IsFlver || selected.IsNestedFlver)
                {
                    var binder = new FileBinders();
                    var operation = new FileOperation
                    {
                        WriteObject = true,
                        ReplaceObject = true,
                        NewObjectBytes = newBytes,
                    };
                    binder.ProcessPaths(new[] { selected.VirtualPath }, operation); ;
                    success = true;
                }
                else
                {
                    var binder = new FileBinders();
                    var operation = new FileOperation
                    {
                        WriteObject = true,
                        ReplaceObject = true,
                        NewObjectBytes = newBytes
                    };
                    binder.ProcessPaths(new[] { selected.VirtualPath }, operation); ;
                    success = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Injection failed: {ex.Message}");
                success = false;
            }

            Console.WriteLine($"Injection {(success ? "completed successfully" : "failed")}");
            return success;
        }

        public bool InjectBytes(FileNode root, FileNode targetNode, byte[] newBytes, string newName)
        {
            try
            {
                if (targetNode.IsNestedDDS)
                {
                    byte imageFlag = 128;
                    string imageFormat = DDSTools.ReadDDSImageFormat(newBytes);
                    if (DDS_FlagFormatList.DDSFlagListSet.ContainsKey(imageFormat))
                        imageFlag = Convert.ToByte(DDS_FlagFormatList.DDSFlagListSet[imageFormat]);

                    var binder = new FileBinders();
                    var operation = new FileOperation
                    {
                        WriteObject = true,
                        ReplaceObject = true,
                        ChangeTextureFormat = true,
                        RenameObject = true,
                        NewObjectBytes = newBytes,
                        NewTextureFormat = imageFlag,
                        NewObjectName = newName
                    };
                    binder.ProcessPaths(new[] { targetNode.VirtualPath }, operation);
                    return true;
                }
                else if (targetNode.IsFlver || targetNode.IsNestedFlver)
                {
                    var binder = new FileBinders();
                    var operation = new FileOperation
                    {
                        WriteObject = true,
                        ReplaceObject = true,
                        NewObjectBytes = newBytes,
                    };
                    binder.ProcessPaths(new[] { targetNode.VirtualPath }, operation);
                    return true;
                }
                else
                {
                    var binder = new FileBinders();
                    var operation = new FileOperation
                    {
                        WriteObject = true,
                        ReplaceObject = true,
                        NewObjectBytes = newBytes
                    };
                    binder.ProcessPaths(new[] { targetNode.VirtualPath }, operation);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Injection failed: {ex.Message}");
                return false;
            }
        }

        private string SelectNewFile()
        {
            var file = "";
            var thread = new Thread(() =>
            {
                using (var fileDialog = new OpenFileDialog())
                {
                    fileDialog.Title = "Select file to inject";
                    fileDialog.Filter = "All files (*.*)|*.*";
                    fileDialog.Multiselect = false;

                    if (fileDialog.ShowDialog() == DialogResult.OK)
                    {
                        file = fileDialog.FileName;
                    }
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return file;
        }
    }
}