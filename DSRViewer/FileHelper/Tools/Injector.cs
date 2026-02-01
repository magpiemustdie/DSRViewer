using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DSRViewer.FileHelper;
using DSRViewer.FileHelper.DDSHelper;
using DSRViewer.FileHelper.FileExplorer.TreeBuilder;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using SoulsFormats;

namespace DSRFileViewer.FilesHelper
{
    public class Injector : ImGuiChild
    {
        private string _filePath = "";
        private bool _success = false;
        private Action<string> _onInjectionComplete; // Колбэк для обновления дерева

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
                        _success = Inject(root, selected);

                        if (_success)
                        {
                            // Вызываем колбэк для обновления дерева
                            _onInjectionComplete?.Invoke(selected.VirtualPath.Split('|')[0]);

                            ImGui.OpenPopup("InjectionSuccess");
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

        private bool Inject(FileNode root, FileNode selected)
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                Console.WriteLine("No valid file selected for injection");
                return false;
            }

            byte[] newBytes = File.ReadAllBytes(_filePath);
            bool success = false;

            try
            {
                if (selected.IsNestedDDS)
                {
                    byte imageFlag = 128;
                    string imageFormat = DDSTools.ReadDDSImageFormat(newBytes);
                    if (DDS_FlagFormatList.DDSFlagListSet.ContainsKey(imageFormat))
                        imageFlag = Convert.ToByte(DDS_FlagFormatList.DDSFlagListSet[imageFormat]);

                    FileBinders binder = new();
                    binder.SetCommon(false, true, false);
                    binder.SetDds(false, false, true, imageFlag, selected.Name, newBytes);
                    binder.Read(selected.VirtualPath);
                    success = true;
                }
                // Раскомментируйте остальные условия по мере необходимости
                
                else if (selected.IsFlver || selected.IsNestedFlver)
                {
                    FileBinders binder = new();
                    binder.SetCommon(false, true, false);
                    binder.SetFlver(true, true, FLVER2.Read(newBytes));
                    binder.Read(selected.VirtualPath);
                    success = true;
                }
                else
                {
                    FileBinders binder = new();
                    binder.SetCommon(false, true, true, newBytes);
                    binder.Read(selected.VirtualPath);
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