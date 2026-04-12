using System;
using System.IO;
using DSRViewer.Core;
using DSRViewer.FileProcess;
using DSRViewer.Editors.Explorer.DDSHelper;
using DSRViewer.UI.Base;
using ImGuiNET;
using SoulsFormats;

namespace DSRViewer.Editors.Explorer.Tools
{
    /// <summary>Инструмент инжекции файлов в архивы с поддержкой DDS-формата.</summary>
    public class Injector : ImGuiChild
    {
        private string _filePath = "";
        private bool _useSelectedFileName = false;
        private bool _showSuccessPopup = false;
        private bool _showErrorPopup = false;

        private readonly Action<string> _onInjectionComplete;
        public Action<string> OnInjectionComplete => _onInjectionComplete;

        public Injector(Action<string> onInjectionComplete = null)
        {
            _onInjectionComplete = onInjectionComplete;
        }

        public void Render(FileNode root, FileNode selected)
        {
            if (!ImGui.CollapsingHeader("Injector")) return;

            if (ImGui.Button("Select new file"))
                _filePath = DialogHelper.SelectFile("Select file to inject", "All files (*.*)|*.*");

            ImGui.SameLine();
            ImGui.Text("File: " + (string.IsNullOrEmpty(_filePath) ? "No file selected" : Path.GetFileName(_filePath)));

            ImGui.Spacing();

            if (ImGui.Button("Inject", new System.Numerics.Vector2(100, 30)))
            {
                try
                {
                    string newName = _useSelectedFileName
                        ? (Path.GetExtension(_filePath) == ".dds"
                            ? Path.GetFileNameWithoutExtension(_filePath)
                            : Path.GetFileName(_filePath))
                        : selected.Name;

                    bool success = Inject(selected, newName);
                    if (success)
                    {
                        _onInjectionComplete?.Invoke(selected.VirtualPath);
                        _showSuccessPopup = true;
                        ImGui.OpenPopup("InjectionSuccess");
                    }
                    else
                    {
                        _showErrorPopup = true;
                        ImGui.OpenPopup("InjectionError");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Injection error: {ex.Message}");
                    _showErrorPopup = true;
                    ImGui.OpenPopup("InjectionError");
                }
            }

            if (ImGui.RadioButton("Use selected file name", _useSelectedFileName))
                _useSelectedFileName = !_useSelectedFileName;

            if (_showSuccessPopup && ImGui.BeginPopupModal("InjectionSuccess", ref _showSuccessPopup, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("File injected successfully!");
                ImGui.Spacing();
                if (ImGui.Button("OK"))
                {
                    _showSuccessPopup = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            if (_showErrorPopup && ImGui.BeginPopupModal("InjectionError", ref _showErrorPopup, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Injection failed! Check console for details.");
                ImGui.Spacing();
                if (ImGui.Button("OK"))
                {
                    _showErrorPopup = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        private bool Inject(FileNode selected, string newName)
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                Console.WriteLine("No valid file selected for injection");
                return false;
            }

            return InjectBytes(selected, File.ReadAllBytes(_filePath), newName);
        }

        /// <summary>Инжектирует байты в целевой узел с автоматическим определением формата DDS.</summary>
        public bool InjectBytes(FileNode targetNode, byte[] newBytes, string newName)
        {
            try
            {
                FileOperation operation;

                if (targetNode.IsNestedDDS || targetNode.IsDDS)
                {
                    // Для DDS: используем делегат — он обновляет байты, формат, mipmaps, type
                    operation = new FileOperation
                    {
                        WriteObject = true,
                        UseTexDelegate = true,
                        AdditionalTextureProcessing = (texture, _) =>
                        {
                            DDSTextureApplier.Apply(texture, newBytes);
                            // Переименование если нужно
                            if (!string.IsNullOrEmpty(newName) && newName != targetNode.Name)
                                texture.Name = newName;
                        }
                    };
                }
                else
                {
                    // Для не-DDS: прямая замена байтов
                    operation = new FileOperation
                    {
                        WriteObject    = true,
                        ReplaceObject  = true,
                        NewObjectBytes = newBytes,
                        RenameObject   = !string.IsNullOrEmpty(newName) && newName != targetNode.Name,
                        NewObjectName  = newName ?? ""
                    };
                }

                new FileBinders().ProcessPaths([targetNode.VirtualPath], operation);
                Console.WriteLine("Injection completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Injection failed: {ex.Message}");
                return false;
            }
        }

        // Overload kept for TransferWindow compatibility
        public bool InjectBytes(FileNode root, FileNode targetNode, byte[] newBytes, string newName) =>
            InjectBytes(targetNode, newBytes, newName);
    }
}
