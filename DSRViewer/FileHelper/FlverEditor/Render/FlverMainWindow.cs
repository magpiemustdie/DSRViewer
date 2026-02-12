using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DSRViewer.FileHelper.MTDEditor;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using SoulsFormats;
using Veldrid;
using DSRViewer.FileHelper.FlverEditor.Tools;
using DSRViewer.FileHelper.MTDEditor.Render;
using DSRViewer.FileHelper.flverTools.Tools;
using DSRViewer.FileHelper.FlverEditor.Tools.FlverTexFinder;
using DSRViewer.Core;
using DSRViewer.FileHelper.FileExplorer.TreeBuilder;

namespace DSRViewer.FileHelper.FlverEditor.Render
{
    public class FMW : ImGuiWindow
    {
        public FMW(string windowName, bool showWindow)
        {
            _windowName = windowName;
            _showWindow = showWindow;
            _minSize = new(550, 900);
            _maxSize = new(1500, 900);


            _windowFlags |= ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.MenuBar;

            _fileListViewer.OnFlverSelected += OnFlverFileSelected;
            _flverMaterialList.OnMaterialSelected += OnMaterialSelected;
            _flverTextureList.ClickHandlerMatTexture += OnTextureSelected;

            _flverMTDFinder = new FlverMTDFinder(_windowName + " - MTDFinder (console)", false);
            _flverNameCorrector = new FlverNameCorrector(_windowName + " - Name corrector", false);
            _flverMTDReplacer = new FlverMTDReplacer(_windowName + " - MTDReplacer", false);
            _flverTexFinder = new FlverTexFinder(_windowName + " - Flver texture finder", false, _mtdList);

            if (_config.MtdFolder == "")
                _flverEditorMTDWindow = new MTDWindow(_windowName + " - MTDEditor", false);
        }

        public FMW(string windowName, bool showWindow, Config config, List<MTDShortDetails> mtdList) : this(windowName, showWindow)
        {
            _config = config;
            _mtdList = mtdList;
            _flverEditorMTDWindow = new MTDWindow(_windowName + " - MTDEditor", false, _config);
        }

        FlverMTDFinder _flverMTDFinder;
        FlverNameCorrector _flverNameCorrector;
        FlverMTDReplacer _flverMTDReplacer;
        FlverTexFinder _flverTexFinder;

        FlverFileList _fileListViewer = new();
        FlverMaterialList _flverMaterialList = new();
        FlverTextureList _flverTextureList = new();

        FileNode _selectedFile = new();
        FLVER2.Material _selectedMaterial = null;
        FLVER2.Texture _selectedTexture = null;
        Config _config = new();
        List<MTDShortDetails> _mtdList = [];
        MTDWindow _flverEditorMTDWindow;

        private FLVER2 _currentFlver;

        // Редактируемые поля
        private string _editingMTD = "";
        private string _editingTexturePath = "";
        private string _editingTextureType = "";
        private string _addingTextureType = "";
        private bool _isEditingMTD = false;
        private bool _isEditingTexturePath = false;
        private bool _isEditingTextureType = false;
        private bool _isAddingTextureType = false;

        // Предопределенные типы текстур для выпадающего списка
        private readonly List<string> _commonTextureTypes =
        [
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
            "g_Lightmap"
        ];

        public override void Render()
        {
            if (_showWindow)
            {
                ImGui.SetNextWindowSizeConstraints(_minSize, _maxSize);
                ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
                {
                    //Меню
                    MenuBarConfig();
                    MenuBarToolsRender();

                    // Верхняя панель с кнопками
                    OpenNewFileButton();
                    ImGui.SameLine();
                    ClearAllButton();
                    ImGui.SameLine();
                    SaveChangesButton();

                    // Разделяем окно на три колонки
                    //ImGui.Columns(3, "FlverWindowColumns", true);

                    ImGui.BeginChild("FLVER Files:", _fileListViewer.GetChildSize(), _childFlags);
                    {
                        // Первая колонка - список файлов
                        ImGui.Text("FLVER Files:");
                        ImGui.Separator();
                        _fileListViewer.Render();
                    }
                    ImGui.EndChild();

                    //ImGui.NextColumn();
                    ImGui.SameLine();

                    ImGui.BeginChild("MTD", _flverMaterialList.GetChildSize(), _childFlags);
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
                                ImGui.SetNextItemWidth(300);
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

                    //ImGui.NextColumn();
                    ImGui.SameLine();

                    ImGui.BeginChild("Textures", _flverTextureList.GetChildSize(), _childFlags);
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
                                ImGui.SetNextItemWidth(300);
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

                            if (!_isAddingTextureType)
                            {
                                ImGui.Text($"Type: Add");
                                if (ImGui.Button("Add Type"))
                                {
                                    _addingTextureType = "New";
                                    _isAddingTextureType = true;
                                }
                            }
                            else
                            {
                                ImGui.Text("New Type:");

                                // Показываем выпадающий список с предопределенными типами
                                if (ImGui.BeginCombo("##TextureAddType", _addingTextureType))
                                {
                                    foreach (var type in _commonTextureTypes)
                                    {
                                        bool isSelected = (_addingTextureType == type);
                                        if (ImGui.Selectable(type, isSelected))
                                        {
                                            _addingTextureType = type;
                                        }
                                        if (isSelected)
                                        {
                                            ImGui.SetItemDefaultFocus();
                                        }
                                    }

                                    // Позволяем ввод кастомного типа
                                    ImGui.Separator();
                                    ImGui.Text("Add Custom Type:");
                                    ImGui.SetNextItemWidth(300);
                                    ImGui.InputText("##AddCustomType", ref _addingTextureType, 256);

                                    ImGui.EndCombo();
                                }

                                ImGui.BeginGroup();
                                if (ImGui.Button("Apply##AddType"))
                                {
                                    ApplyTextureTypeAdd();
                                }
                                ImGui.SameLine();
                                if (ImGui.Button("Cancel##AddType"))
                                {
                                    _isAddingTextureType = false;
                                    _addingTextureType = "New";
                                }
                                ImGui.EndGroup();
                            }

                            // Информация о текстуре
                            ImGui.Separator();
                            ImGui.Text("Texture Info:");
                            ImGui.Text($"Scale: X={_selectedTexture.Scale.X}, Y={_selectedTexture.Scale.Y}");
                        }
                    }
                    ImGui.EndChild();
                    
                    //ImGui.Columns(1); // Сбрасываем колонки
                }
                ImGui.End();
            }
        }

        private void MenuBarConfig()
        {
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("Tools"))
                {
                    if (ImGui.MenuItem("MTD finder"))
                    {
                        _flverMTDFinder.ShowWindow(true);
                    }
                    if (ImGui.MenuItem("Name corrector"))
                    {
                        _flverNameCorrector.ShowWindow(true);
                    }
                    if (ImGui.MenuItem("MTD Replacer"))
                    {
                        _flverMTDReplacer.ShowWindow(true);
                    }
                    if (ImGui.MenuItem("Tex finder"))
                    {
                        _flverTexFinder.ShowWindow(true);
                    }
                    if (ImGui.MenuItem("Test flvers"))
                    {
                        TestSave();
                    }

                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("MTD editor"))
                {
                    if (ImGui.MenuItem("Show MTD editor"))
                    {
                        _flverEditorMTDWindow.ShowWindow(true);
                    }
                    
                    ImGui.EndMenu();
                }
                ImGui.EndMenuBar();
            }
        }

        private void TestSave()
        {
            List<string> _fileList = _fileListViewer.GetFileList()
            .Select(fileNode => fileNode.VirtualPath)
            .ToList();

            FileBinders binder = new();
            var operation = new FileOperation
            {
                UseFlverDelegate = true,
                AdditionalFlverProcessing = (flver, realPath, path) =>
                {
                    
                    List<FLVER2.Material> flver_materials = flver.Materials;
                    try
                    {
                        var bytes = flver.Write();
                    }
                    catch
                    {
                        Console.WriteLine($"Find save errors -> rp: {realPath} p: {path}");
                    }
                }
            };
            binder.ProcessPaths(_fileList, operation);
        }

        private void MenuBarToolsRender()
        {
            _flverMTDFinder.Render(_fileListViewer.GetFileList());
            _flverNameCorrector.Render(_fileListViewer.GetFileList());
            _flverMTDReplacer.Render(_fileListViewer.GetFileList(), _mtdList);
            _flverTexFinder.Render(_fileListViewer.GetFileList(), _mtdList);
            _flverEditorMTDWindow.Render();
        }

        public void SetNewItem(FileNode fileNode)
        {
            _fileListViewer.AddItemToList(fileNode);
        }

        public void SetNewItemList(List<FileNode> fileNodes)
        {
            _fileListViewer.UpdateList(fileNodes);
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
        private void ApplyTextureTypeAdd()
        {
            if (!string.IsNullOrEmpty(_addingTextureType))
            {

                _flverTextureList.AddTexture(_addingTextureType);
                _isAddingTextureType = false;

                Console.WriteLine($"Updated texture type to: {_editingTextureType}");
            }
        }

        private void LoadFlverMaterials(FileNode fileNode)
        {
            if (fileNode == null)
                return;

            if (fileNode.VirtualPath.Split("|").Last() != null) //wut??
            {
                var binder = new FileBinders();
                var operation = new FileOperation
                {
                    GetObject = true
                };
                binder.ProcessPaths(new[] { fileNode.VirtualPath }, operation);

                _currentFlver = (FLVER2)binder.GetObject();
                _flverMaterialList.UpdateList(_currentFlver.Materials);
                _flverTextureList.ClearList();
                _selectedMaterial = null;
                _selectedTexture = null;
                _selectedFile = fileNode;
                Console.WriteLine($"Loaded {_currentFlver.Materials.Count} materials from {fileNode.Name}");
            }
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
                var binder = new FileBinders();
                var operation = new FileOperation
                {
                    WriteObject = true,
                    WriteFlver = true,
                    ReplaceFlver = true,
                    NewFlver = _currentFlver
                };
                binder.ProcessPaths(new[] { filePath }, operation);
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