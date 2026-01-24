using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
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
        FlverMaterialList _flverMaterialList = new();
        FlverTextureList _flverTextureList = new();

        FileNode _selectedFile = new();
        FLVER2.Material _selectedMaterial = null;
        FLVER2.Texture _selectedTexture = null;
        bool _isOpen = false;

        private FLVER2 _currentFlver;

        // Редактируемые поля
        private string _editingMTD = "";
        private string _editingTexturePath = "";
        private string _editingTextureType = "";
        private bool _isEditingMTD = false;
        private bool _isEditingTexturePath = false;
        private bool _isEditingTextureType = false;

        // Предопределенные типы текстур для выпадающего списка
        private readonly List<string> _commonTextureTypes = new()
        {
            "g_Diffuse",
            "g_Diffuse_2",
            "g_Specular",
            "g_Specular_2",
            "g_Bumpmap",
            "g_Bumpmap_2",
            "g_Bumpmap_3",
            "g_DetailBumpmap",
            "g_Height",
            "g_Subsurf",
            "g_Emission",
            "g_Lightmap"
        };

        public bool IsWindowOpen() => _showWindow;
        public override void Render()
        {
            if (_showWindow)
            {
                ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
                {
                    // Верхняя панель с кнопками
                    OpenNewFileButton();
                    ImGui.SameLine();
                    ClearAllButton();
                    ImGui.SameLine();
                    SaveChangesButton();

                    // Разделяем окно на три колонки
                    ImGui.Columns(3, "FlverWindowColumns", true);

                    ImGui.BeginChild("FLVER Files:");
                    {
                        // Первая колонка - список файлов
                        ImGui.Text("FLVER Files:");
                        ImGui.Separator();
                        _fileListViewer.Render();
                    }
                    ImGui.EndChild();

                    ImGui.NextColumn();

                    ImGui.BeginChild("MTD");
                    {
                        // Вторая колонка - список материалов и редактирование
                        ImGui.Text("Materials (by MTD):");
                        ImGui.Separator();
                        _flverMaterialList.Render();

                        // Редактирование MTD
                        if (_selectedMaterial != null)
                        {
                            ImGui.Separator();
                            ImGui.TextColored(new Vector4(0, 1, 1, 1), "Edit Material:");

                            if (!_isEditingMTD)
                            {
                                ImGui.Text($"MTD: {_selectedMaterial.MTD ?? "No MTD"}");
                                if (ImGui.Button("Edit MTD"))
                                {
                                    _editingMTD = _selectedMaterial.MTD ?? "";
                                    _isEditingMTD = true;
                                }
                            }
                            else
                            {
                                ImGui.Text("New MTD:");
                                ImGui.InputText("##MTDEdit", ref _editingMTD, 256);

                                ImGui.BeginGroup();
                                if (ImGui.Button("Apply"))
                                {
                                    ApplyMTDEdit();
                                }
                                ImGui.SameLine();
                                if (ImGui.Button("Cancel"))
                                {
                                    _isEditingMTD = false;
                                    _editingMTD = "";
                                }
                                ImGui.EndGroup();
                            }

                            ImGui.Separator();
                            ImGui.Text("Material Info:");
                            ImGui.Text($"Name: {_selectedMaterial.Name}");
                            ImGui.Text($"GX Index: {_selectedMaterial.GXIndex}");
                        }
                    }
                    ImGui.EndChild();
                    
                    ImGui.NextColumn();

                    ImGui.BeginChild("Textures");
                    {
                        // Третья колонка - список текстур и редактирование
                        ImGui.Text("Textures:");
                        ImGui.Separator();
                        _flverTextureList.Render();

                        // Редактирование текстуры
                        if (_selectedTexture != null)
                        {
                            ImGui.Separator();
                            ImGui.TextColored(new Vector4(0, 1, 1, 1), "Edit Texture:");

                            // Редактирование пути текстуры
                            if (!_isEditingTexturePath)
                            {
                                ImGui.Text($"Path: {_selectedTexture.Path ?? "No path"}");
                                if (ImGui.Button("Edit Path"))
                                {
                                    _editingTexturePath = _selectedTexture.Path ?? "";
                                    _isEditingTexturePath = true;
                                }
                            }
                            else
                            {
                                ImGui.Text("New Path:");
                                ImGui.InputText("##PathEdit", ref _editingTexturePath, 512);

                                ImGui.BeginGroup();
                                if (ImGui.Button("Apply##Path"))
                                {
                                    ApplyTexturePathEdit();
                                }
                                ImGui.SameLine();
                                if (ImGui.Button("Cancel##Path"))
                                {
                                    _isEditingTexturePath = false;
                                    _editingTexturePath = "";
                                }
                                ImGui.EndGroup();
                            }

                            ImGui.Spacing();

                            // Редактирование типа текстуры
                            if (!_isEditingTextureType)
                            {
                                ImGui.Text($"Type: {_selectedTexture.Type}");
                                if (ImGui.Button("Edit Type"))
                                {
                                    _editingTextureType = _selectedTexture.Type;
                                    _isEditingTextureType = true;
                                }
                            }
                            else
                            {
                                ImGui.Text("New Type:");

                                // Показываем выпадающий список с предопределенными типами
                                if (ImGui.BeginCombo("##TextureType", _editingTextureType))
                                {
                                    foreach (var type in _commonTextureTypes)
                                    {
                                        bool isSelected = (_editingTextureType == type);
                                        if (ImGui.Selectable(type, isSelected))
                                        {
                                            _editingTextureType = type;
                                        }
                                        if (isSelected)
                                        {
                                            ImGui.SetItemDefaultFocus();
                                        }
                                    }

                                    // Позволяем ввод кастомного типа
                                    ImGui.Separator();
                                    ImGui.Text("Custom Type:");
                                    ImGui.InputText("##CustomType", ref _editingTextureType, 256);

                                    ImGui.EndCombo();
                                }

                                ImGui.BeginGroup();
                                if (ImGui.Button("Apply##Type"))
                                {
                                    ApplyTextureTypeEdit();
                                }
                                ImGui.SameLine();
                                if (ImGui.Button("Cancel##Type"))
                                {
                                    _isEditingTextureType = false;
                                    _editingTextureType = _selectedTexture.Type;
                                }
                                ImGui.EndGroup();
                            }

                            // Информация о текстуре
                            ImGui.Separator();
                            ImGui.Text("Texture Info:");
                            ImGui.Text($"Scale: X={_selectedTexture.Scale.X}, Y={_selectedTexture.Scale.Y}");
                            ImGui.Text($"Unk10: {_selectedTexture.Unk10}");
                        }
                    }
                    ImGui.EndChild();
                    
                    ImGui.Columns(1); // Сбрасываем колонки
                }
                ImGui.End();
            }
        }

        public FMW()
        {
            _windowName = "FLVER Material Editor";
            _windowFlags = ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.MenuBar;

            _fileListViewer.OnFlverSelected += OnFlverFileSelected;
            _flverMaterialList.OnMaterialSelected += OnMaterialSelected;
            _flverTextureList.CurrentClickHandlerMatTexture += OnTextureSelected;
        }

        public void SetNewItem(FileNode fileNode)
        {
            _fileListViewer.AddItemToList(fileNode);
        }

        private void OpenNewFileButton()
        {
            if (ImGui.Button("Open FLVER"))
            {
                SetFile();
            }
        }

        private void ClearAllButton()
        {
            if (ImGui.Button("Clear All"))
            {
                ClearAll();
            }
        }

        private void SaveChangesButton()
        {
            if (ImGui.Button("Save Changes"))
            {
                SaveChanges();
            }
        }

        private void SetFile()
        {
            using (var fileDialog = new OpenFileDialog())
            {
                fileDialog.Filter = "FLVER files|*.flver; *.flver.dcx|All files|*.*";
                fileDialog.Title = "Select FLVER file";
                fileDialog.Multiselect = true;

                var thread = new Thread(() =>
                {
                    if (fileDialog.ShowDialog() == DialogResult.OK)
                    {
                        //var file = fileDialog.FileName;
                        foreach (var file in fileDialog.FileNames)
                        {
                            Console.WriteLine($"Opening file: {file}");
                            FileTreeNodeBuilder builder = new();
                            FileNode fileNode = builder.BuildTree(file);
                            _fileListViewer.AddItemToList(fileNode);

                            // Автоматически выбираем первый файл
                            if (_fileListViewer.GetSelectedIndex() == -1 && _fileListViewer.GetItemCount() > 0)
                            {
                                OnFlverFileSelected(fileNode);
                            }
                        }
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            }
        }

        private void OnFlverFileSelected(FileNode fileNode)
        {
            LoadFlverMaterials(fileNode);
        }

        private void OnMaterialSelected(FLVER2.Material material)
        {
            if (material == null)
                return;

            _selectedMaterial = material;
            _isEditingMTD = false;
            _editingMTD = material.MTD ?? "";

            LoadTexturesForMaterial(material);

            Console.WriteLine($"Selected material: {material.Name} (MTD: {material.MTD})");
        }

        private void OnTextureSelected(FLVER2.Texture texture, int index)
        {
            if (texture == null)
                return;

            _selectedTexture = texture;
            _isEditingTexturePath = false;
            _isEditingTextureType = false;
            _editingTexturePath = texture.Path ?? "";
            _editingTextureType = texture.Type;

            Console.WriteLine($"Selected texture [{index}]: {texture.Type} - {texture.Path}");
        }

        private void ApplyMTDEdit()
        {
            if (_selectedMaterial != null && !string.IsNullOrEmpty(_editingMTD))
            {
                int materialIndex = _flverMaterialList.GetSelectedIndex();
                _flverMaterialList.UpdateMaterialMTD(materialIndex, _editingMTD);
                _selectedMaterial.MTD = _editingMTD;
                _isEditingMTD = false;

                Console.WriteLine($"Updated MTD to: {_editingMTD}");
            }
        }

        private void ApplyTexturePathEdit()
        {
            if (_selectedTexture != null)
            {
                int textureIndex = _flverTextureList.GetSelectedIndex();
                _flverTextureList.UpdateTexture(textureIndex, _editingTexturePath, null);
                _selectedTexture.Path = _editingTexturePath;
                _isEditingTexturePath = false;

                Console.WriteLine($"Updated texture path to: {_editingTexturePath}");
            }
        }

        private void ApplyTextureTypeEdit()
        {
            if (_selectedTexture != null && !string.IsNullOrEmpty(_editingTextureType))
            {
                int textureIndex = _flverTextureList.GetSelectedIndex();
                _flverTextureList.UpdateTexture(textureIndex, null, _editingTextureType);
                _selectedTexture.Type = _editingTextureType;
                _isEditingTextureType = false;

                Console.WriteLine($"Updated texture type to: {_editingTextureType}");
            }
        }

        private void LoadFlverMaterials(FileNode fileNode)
        {
            if (fileNode == null)
                return;

            if (fileNode.VirtualPath.Split("|").Last() != null)
            {
                FileBinders binder = new();
                binder.SetGetObjectOnly();
                binder.Read(fileNode.VirtualPath);
                
                _currentFlver = (FLVER2)binder.GetObject();
                _flverMaterialList.UpdateList(_currentFlver.Materials);
                _flverTextureList.ClearList();
                _selectedMaterial = null;
                _selectedTexture = null;
                _selectedFile = fileNode;
                Console.WriteLine($"Loaded {_currentFlver.Materials.Count} materials from {fileNode.Name}");
            }

            /*
            try
            {
                string filePath = fileNode.VirtualPath;

                if (!System.IO.File.Exists(filePath))
                {
                    Console.WriteLine($"File not found: {filePath}");
                    return;
                }

                _currentFlver = FLVER2.Read(filePath);
                _flverMaterialList.UpdateList(_currentFlver.Materials);
                _flverTextureList.ClearList();
                _selectedMaterial = null;
                _selectedTexture = null;
                _selectedFile = fileNode;

                Console.WriteLine($"Loaded {_currentFlver.Materials.Count} materials from {fileNode.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading FLVER file: {ex.Message}");
                _flverMaterialList.ClearList();
                _flverTextureList.ClearList();
                _selectedMaterial = null;
                _selectedTexture = null;
            }
            */
        }

        private void LoadTexturesForMaterial(FLVER2.Material material)
        {
            if (material == null)
            {
                _flverTextureList.ClearList();
                _selectedTexture = null;
                return;
            }

            try
            {
                _flverTextureList.UpdateList(material.Textures);
                _selectedTexture = null;
                _isEditingTexturePath = false;
                _isEditingTextureType = false;

                Console.WriteLine($"Loaded {material.Textures.Count} textures for material");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading textures: {ex.Message}");
                _flverTextureList.ClearList();
                _selectedTexture = null;
            }
        }

        private void SaveChanges()
        {
            if (_currentFlver == null || _selectedFile == null)
            {
                Console.WriteLine("No file loaded to save");
                return;
            }

            try
            {
                string filePath = _selectedFile.VirtualPath;
                _currentFlver.Write(filePath);
                Console.WriteLine($"Successfully saved changes to: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving file: {ex.Message}");
            }
        }

        public void ClearAll()
        {
            _fileListViewer.ClearList();
            _flverMaterialList.ClearList();
            _flverTextureList.ClearList();
            _currentFlver = null;
            _selectedFile = null;
            _selectedMaterial = null;
            _selectedTexture = null;
            _isEditingMTD = false;
            _isEditingTexturePath = false;
            _isEditingTextureType = false;

            Console.WriteLine("Cleared all FLVER data");
        }

        public FLVER2 CurrentFlver => _currentFlver;
        public FileNode GetSelectedFile() => _selectedFile;
        public FLVER2.Material GetSelectedMaterial() => _selectedMaterial;
        public FLVER2.Texture GetSelectedTexture() => _selectedTexture;
    }
}