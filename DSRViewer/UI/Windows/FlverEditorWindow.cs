using DSRViewer.Core;
using DSRViewer.FileProcess;
using DSRViewer.Editors.Explorer.DDSHelper;
using DSRViewer.Editors.FlverEditor;
using DSRViewer.Editors.Explorer.TreeBuilder;
using DSRViewer.Editors.FlverEditor.Tools;
using DSRViewer.Editors.FlverEditor.Tools.FlverTexFinder;
using DSRViewer.Editors.MTDEditor;
using DSRViewer.UI.Base;
using ImGuiNET;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;

namespace DSRViewer.UI.Windows
{
    /// <summary>Окно редактора FLVER-файлов: материалы, текстуры и инструменты.</summary>
    public class FMW : ImGuiWindow
    {
        // ── Конструкторы ─────────────────────────────────────────────────

        public FMW(string windowName, bool showWindow)
            : this(windowName, showWindow, new Config(), []) { }

        public FMW(string windowName, bool showWindow, Config config, List<MTDShortDetails> mtdList)
            : base(windowName, showWindow)
        {
            _minSize = new(700, 500);
            _maxSize = new(float.MaxValue, float.MaxValue);
            _windowFlags |= ImGuiWindowFlags.MenuBar;

            _config  = config;
            _mtdList = mtdList;

            _flverMTDFinder    = new FlverMTDFinder   (_windowName + " - MTDFinder",    false);
            _flverNameCorrector= new FlverNameCorrector(_windowName + " - NameCorrector",false);
            _flverMTDReplacer  = new FlverMTDReplacer  (_windowName + " - MTDReplacer",  false);
            _flverTexFinder    = new FlverTexFinder    (_windowName + " - TexFinder",    false, _mtdList);
            _flverEditorMTDWindow = new MTDWindow      (_windowName + " - MTDEditor",    false, _config);
        }

        // ── Инструменты ──────────────────────────────────────────────────

        private readonly FlverMTDFinder     _flverMTDFinder;
        private readonly FlverNameCorrector _flverNameCorrector;
        private readonly FlverMTDReplacer   _flverMTDReplacer;
        private readonly FlverTexFinder     _flverTexFinder;
        private readonly MTDWindow          _flverEditorMTDWindow;

        // ── Данные ───────────────────────────────────────────────────────

        private readonly FlverFileList     _fileListViewer   = new();
        private readonly FlverMaterialList _flverMaterialList= new();
        private readonly FlverTextureList  _flverTextureList = new();

        private FileNode          _selectedFile     = null;
        private FLVER2.Material   _selectedMaterial = null;
        private FLVER2.Texture    _selectedTexture  = null;
        private FLVER2            _currentFlver     = null;
        private Config            _config           = new();
        private List<MTDShortDetails> _mtdList      = [];

        // ── Inline-редактирование ────────────────────────────────────────

        private string _editMTD          = "";
        private string _editTexPath      = "";
        private string _editTexType      = "";
        private string _addTexType       = "";
        private bool   _editingMTD       = false;
        private bool   _editingTexPath   = false;
        private bool   _editingTexType   = false;
        private bool   _addingTexType    = false;

        private bool _useBytePatchFallback = true;
        private string _mtdUvWarning = "";
        private string _mtdFilter    = "";
        private string _texTypeFilter = "";
        private string _testSaveResult = "";
        private string _saveStatus = "";
        private float  _saveStatusTimer = 0f;

        // ── Поиск текстуры в Explorer ────────────────────────────────────

        /// <summary>
        /// Делегат поиска текстуры по имени в открытых Explorer-деревьях.
        /// Устанавливается из WindowsManager или TreeChild.
        /// Возвращает список FileNode с совпадающим именем.
        /// </summary>
        public Func<string, List<FileNode>> ExplorerSearchDelegate { get; set; }

        /// <summary>
        /// Делегат открытия превью текстуры. Устанавливается из WindowsManager/TreeChild.
        /// Принимает FileNode с текстурой и открывает pop-out окно.
        /// </summary>
        public Action<FileNode> ShowTexturePreviewDelegate { get; set; }

        /// <summary>Дополнительный рендер вызывается каждый кадр после основного (для pop-out окон).</summary>
        public Action OnRenderExtra { get; set; }

        private List<FileNode> _texSearchResults  = [];  // для Find
        private List<FileNode> _previewResults    = [];  // для Preview
        private bool _showTexSearchPopup = false;

        private static readonly string[] _commonTexTypes =
        [
            "g_Diffuse", "g_Diffuse_2",
            "g_Specular", "g_Specular_2",
            "g_Bumpmap", "g_Bumpmap_2", "g_Bumpmap_3",
            "g_DetailBumpmap",
            "g_Height", "g_Subsurf", "g_Lightmap"
        ];

        // ── Render ───────────────────────────────────────────────────────

        public override void Render()
        {
            if (!_showWindow) return;

            ApplySizeConstraints();
            ImGui.Begin(_windowName, ref _showWindow, _windowFlags);

            RenderMenuBar();
            RenderToolbar();
            RenderMainLayout();

            ImGui.End();

            // Дочерние окна инструментов
            _flverMTDFinder   .Render(_fileListViewer.GetFileList(), _mtdList);
            _flverNameCorrector.Render(_fileListViewer.GetFileList());
            _flverMTDReplacer .Render(_fileListViewer.GetFileList(), _mtdList);
            _flverTexFinder   .Render(_fileListViewer.GetFileList(), _mtdList);
            _flverEditorMTDWindow.Render();

            // Предпросмотр текстуры — делегируем в WindowsManager/TreeChild
            // (они имеют доступ к GraphicsDevice и ImGuiController)
            OnRenderExtra?.Invoke();
        }

        // ── Меню ─────────────────────────────────────────────────────────

        private void RenderMenuBar()
        {
            if (!ImGui.BeginMenuBar()) return;

            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("Open FLVER..."))   SetFile();
                if (ImGui.MenuItem("Save"))            SaveChanges();
                ImGui.Separator();
                ImGui.Checkbox("Byte patch fallback", ref _useBytePatchFallback);
                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(
                        "On Write() failure, patches FLVER bytes directly.\n" +
                        "Works for renaming MTD, textures, materials.\n" +
                        "New name must not be longer than the old one.");
                ImGui.Separator();
                if (ImGui.MenuItem("Clear all"))       ClearAll();
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Tools"))
            {
                if (ImGui.MenuItem("FLVER Stats Export"))  _flverMTDFinder.ShowWindow(true);
                if (ImGui.MenuItem("Texture Path Editor")) _flverNameCorrector.ShowWindow(true);
                if (ImGui.MenuItem("Batch MTD Replace"))   _flverMTDReplacer.ShowWindow(true);
                if (ImGui.MenuItem("Find Texture Usage"))  _flverTexFinder.ShowWindow(true);
                ImGui.Separator();
                if (ImGui.MenuItem("Test Save"))       TestSave();
                if (!string.IsNullOrEmpty(_testSaveResult))
                    ImGui.TextDisabled(_testSaveResult);
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("MTD Editor"))
            {
                if (ImGui.MenuItem("Open MTD Editor")) _flverEditorMTDWindow.ShowWindow(true);
                ImGui.EndMenu();
            }

            ImGui.EndMenuBar();
        }

        // ── Тулбар ───────────────────────────────────────────────────────

        private void RenderToolbar()
        {
            if (ImGui.Button("Open"))    SetFile();
            ImGui.SameLine();
            if (ImGui.Button("Save"))    SaveChanges();
            ImGui.SameLine();
            if (ImGui.Button("Clear"))   ClearAll();

            if (_currentFlver != null && _selectedFile != null)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"  {_selectedFile.ShortName}  |  {_currentFlver.Materials.Count} mat  |  {_selectedMaterial?.Textures.Count ?? 0} tex");
            }

            // Статус сохранения — исчезает через 4 секунды
            if (!string.IsNullOrEmpty(_saveStatus))
            {
                _saveStatusTimer -= ImGui.GetIO().DeltaTime;
                if (_saveStatusTimer <= 0f)
                {
                    _saveStatus = "";
                }
                else
                {
                    ImGui.SameLine();
                    bool isError = _saveStatus.StartsWith("✗");
                    var color = isError
                        ? new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f)
                        : new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f);
                    ImGui.TextColored(color, _saveStatus);
                }
            }
        }

        // ── Основной layout: три колонки ─────────────────────────────────

        private void RenderMainLayout()
        {
            if (!ImGui.BeginTable("##fmw_layout", 3,
                ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp))
                return;

            ImGui.TableSetupColumn("Files",     ImGuiTableColumnFlags.WidthStretch, 0.25f);
            ImGui.TableSetupColumn("Materials", ImGuiTableColumnFlags.WidthStretch, 0.35f);
            ImGui.TableSetupColumn("Textures",  ImGuiTableColumnFlags.WidthStretch, 0.40f);
            ImGui.TableHeadersRow();

            ImGui.TableNextColumn();
            RenderFileColumn();

            ImGui.TableNextColumn();
            RenderMaterialColumn();

            ImGui.TableNextColumn();
            RenderTextureColumn();

            ImGui.EndTable();
        }

        // ── Колонка файлов ───────────────────────────────────────────────

        private void RenderFileColumn()
        {
            float totalH = ImGui.GetContentRegionAvail().Y;
            if (ImGui.BeginChild("##files_list", new Vector2(-1, totalH), ImGuiChildFlags.None))
            {
                _fileListViewer.OnFlverSelected = OnFlverFileSelected;
                _fileListViewer.Render();
            }
            ImGui.EndChild();
        }

        // ── Колонка материалов ───────────────────────────────────────────

        private void RenderMaterialColumn()
        {
            float totalH = ImGui.GetContentRegionAvail().Y;
            // Список материалов — верхняя часть
            float editH = _selectedMaterial != null ? 130f : 0f;
            float listH = totalH - editH - 4f;
            if (listH < 60) listH = 60;

            if (ImGui.BeginChild("##mat_list", new Vector2(-1, listH), ImGuiChildFlags.Borders))
            {
                _flverMaterialList.SetMtdList(_mtdList);
                _flverMaterialList.OnMaterialSelected = OnMaterialSelected;
                _flverMaterialList.Render();
            }
            ImGui.EndChild();

            if (_selectedMaterial == null) return;

            // Панель редактирования материала
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.4f, 0.9f, 1f, 1f), "Material");
            ImGui.Separator();

            if (!_editingMTD)
            {
                ImGui.TextUnformatted(_selectedMaterial.MTD ?? "(no MTD)");
                ImGui.SameLine();
                if (ImGui.SmallButton("Edit##mtd"))
                {
                    _editMTD    = _selectedMaterial.MTD ?? "";
                    _editingMTD = true;
                }
            }
            else
            {
                // Комбо со списком MTD из _mtdList + ручной ввод
                float comboW = Math.Min(ImGui.GetContentRegionAvail().X - 60, 400);
                ImGui.SetNextItemWidth(comboW);
                ImGui.SetNextWindowSize(new Vector2(comboW, 0), ImGuiCond.Always);
                if (ImGui.BeginCombo("##mtd_combo", _editMTD.Split('\\').LastOrDefault() ?? _editMTD,
                    ImGuiComboFlags.HeightLarge))
                {
                    // Отдельное поле фильтра — не зависит от выбранного значения
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputText("##mtd_filter", ref _mtdFilter, 128);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("X##mf")) _mtdFilter = "";
                    ImGui.Separator();

                    string filter = _mtdFilter.ToLower();
                    foreach (var m in _mtdList)
                    {
                        string mName = m.Name.Split('\\').Last();
                        if (!string.IsNullOrEmpty(filter) &&
                            !mName.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

                        bool sel = _editMTD.EndsWith(mName, StringComparison.OrdinalIgnoreCase);
                        if (ImGui.Selectable($"{mName} [MW{m.MW}]##mtd_{mName}", sel))
                        {
                            int slash = _selectedMaterial.MTD?.LastIndexOf('\\') ?? -1;
                            string prefix = slash >= 0 ? _selectedMaterial.MTD![..(slash + 1)] : "";
                            _editMTD = prefix + mName;
                            _mtdUvWarning = CheckUvCompatibility(_selectedMaterial.MTD, m);
                        }
                        if (sel) ImGui.SetItemDefaultFocus();
                    }
                    ImGui.Separator();
                    ImGui.TextDisabled("Custom:");
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputText("##mtd_custom", ref _editMTD, 512);
                    ImGui.EndCombo();
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("OK##mtd"))   ApplyMTDEdit();
                ImGui.SameLine();
                if (ImGui.SmallButton("X##mtd"))
                {
                    _editingMTD   = false;
                    _mtdUvWarning = "";
                    _mtdFilter    = "";
                }

                if (!string.IsNullOrEmpty(_mtdUvWarning))
                    ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), _mtdUvWarning);
            }

            ImGui.TextDisabled($"Name: {_selectedMaterial.Name}   GX: {_selectedMaterial.GXIndex}");

            // MW из MTD-списка
            if (_mtdList.Count > 0 && !string.IsNullOrEmpty(_selectedMaterial.MTD))
            {
                string mtdName = _selectedMaterial.MTD.Split('\\').Last();
                var mtdInfo = _mtdList.FirstOrDefault(m =>
                    m.Name.Equals(mtdName, StringComparison.OrdinalIgnoreCase));
                if (mtdInfo != null)
                    ImGui.TextDisabled($"MW: {mtdInfo.MW}");
                else
                    ImGui.TextDisabled("MW: unknown");
            }
        }

        // ── Колонка текстур ──────────────────────────────────────────────

        private void RenderTextureColumn()
        {
            float totalH = ImGui.GetContentRegionAvail().Y;
            float editH = _selectedTexture != null ? 160f : (_selectedMaterial != null ? 28f : 0f);
            float listH = totalH - editH - 4f;
            if (listH < 60) listH = 60;

            if (ImGui.BeginChild("##tex_list", new Vector2(-1, listH), ImGuiChildFlags.Borders))
            {
                _flverTextureList.ClickHandlerMatTexture = OnTextureSelected;
                _flverTextureList.Render();
            }
            ImGui.EndChild();

            if (_selectedMaterial != null)
            {
                ImGui.Spacing();
                if (ImGui.SmallButton("+ Add texture"))
                {
                    _addTexType    = "g_Diffuse";
                    _addingTexType = true;
                }
                if (_addingTexType)
                    RenderAddTexture();
            }

            if (_selectedTexture == null) return;

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.4f, 0.9f, 1f, 1f), "Texture");
            ImGui.Separator();

            RenderTexturePathEdit();
            ImGui.Spacing();
            RenderTextureTypeEdit();

            ImGui.TextDisabled($"Scale: {_selectedTexture.TilingScale.X:F2} x {_selectedTexture.TilingScale.Y:F2}");

            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.15f, 0.15f, 1f));
            if (ImGui.SmallButton("Delete##tex"))
                DeleteSelectedTexture();
            ImGui.PopStyleColor();
        }

        private void RenderTexturePathEdit()
        {
            ImGui.TextDisabled("Path:");
            ImGui.SameLine();
            if (!_editingTexPath)
            {
                ImGui.TextUnformatted(_selectedTexture.Path ?? "(empty)");
                ImGui.SameLine();
                if (ImGui.SmallButton("Edit##path"))
                {
                    _editTexPath   = _selectedTexture.Path ?? "";
                    _editingTexPath = true;
                }

                // Кнопка поиска в Explorer
                if (ExplorerSearchDelegate != null && !string.IsNullOrEmpty(_selectedTexture.Path))
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Find##explorer"))
                    {
                        string texName = _selectedTexture.Path.Replace('/', '\\').Split('\\').Last();
                        if (texName.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                            texName = texName[..^4];
                        _texSearchResults = ExplorerSearchDelegate(texName) ?? [];
                        _showTexSearchPopup = true;
                        ImGui.OpenPopup("##tex_search_results");
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Find this texture in open Explorer trees");
                }

                // Кнопка Preview — независима от Find
                if (ShowTexturePreviewDelegate != null && ExplorerSearchDelegate != null
                    && !string.IsNullOrEmpty(_selectedTexture.Path))
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Preview##tex"))
                    {
                        string texName = _selectedTexture.Path.Replace('/', '\\').Split('\\').Last();
                        if (texName.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                            texName = texName[..^4];

                        _previewResults = ExplorerSearchDelegate.Invoke(texName) ?? [];

                        if (_previewResults.Count == 1)
                        {
                            ShowTexturePreviewDelegate(_previewResults[0]);
                        }
                        else if (_previewResults.Count > 1)
                        {
                            ImGui.OpenPopup("##preview_pick");
                        }
                        // 0 результатов — ничего не делаем, tooltip объяснит
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Preview texture from Explorer");

                    // Popup выбора вхождения
                    ImGui.SetNextWindowSizeConstraints(new Vector2(350, 60), new Vector2(700, 400));
                    if (ImGui.BeginPopup("##preview_pick"))
                    {
                        ImGui.TextDisabled($"Found {_previewResults.Count} locations — pick one:");
                        ImGui.Separator();
                        foreach (var node in _previewResults)
                        {
                            string label = $"{node.VirtualPath}  [{node.DDSFormat}]";
                            if (ImGui.Selectable(label))
                            {
                                ShowTexturePreviewDelegate(node);
                                ImGui.CloseCurrentPopup();
                            }
                        }
                        ImGui.EndPopup();
                    }
                }
            }
            else
            {
                ImGui.SetNextItemWidth(-60);
                if (ImGui.InputText("##path_edit", ref _editTexPath, 512,
                    ImGuiInputTextFlags.EnterReturnsTrue))
                    ApplyTexturePathEdit();
                ImGui.SameLine();
                if (ImGui.SmallButton("OK##path")) ApplyTexturePathEdit();
                ImGui.SameLine();
                if (ImGui.SmallButton("X##path"))  _editingTexPath = false;
            }

            // Popup с результатами поиска
            if (_showTexSearchPopup)
            {
                ImGui.SetNextWindowSizeConstraints(new Vector2(400, 60), new Vector2(800, 400));
                if (ImGui.BeginPopup("##tex_search_results"))
                {
                    if (_texSearchResults.Count == 0)
                    {
                        ImGui.TextDisabled("Not found in any open Explorer.");
                    }
                    else
                    {
                        ImGui.TextDisabled($"{_texSearchResults.Count} result(s):");
                        ImGui.Separator();
                        foreach (var node in _texSearchResults)
                        {
                            ImGui.TextUnformatted(node.VirtualPath);
                            ImGui.SameLine();
                            ImGui.TextDisabled($"  [{node.DDSFormat}]");
                        }
                    }
                    ImGui.Separator();
                    if (ImGui.SmallButton("Close"))
                    {
                        _showTexSearchPopup = false;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
                else
                {
                    _showTexSearchPopup = false;
                }
            }
        }

        private void RenderTextureTypeEdit()
        {
            ImGui.TextDisabled("Type:");
            ImGui.SameLine();
            if (!_editingTexType)
            {
                ImGui.TextUnformatted(_selectedTexture.ParamName ?? "(none)");
                ImGui.SameLine();
                if (ImGui.SmallButton("Edit##type"))
                {
                    _editTexType    = _selectedTexture.ParamName ?? "";
                    _editingTexType = true;
                }
            }
            else
            {
                // Типы из MTD текущего материала (если есть)
                IEnumerable<string> mtdTypes = [];
                if (_selectedMaterial != null && !string.IsNullOrEmpty(_selectedMaterial.MTD) && _mtdList.Count > 0)
                {
                    string mtdName = _selectedMaterial.MTD.Split('\\').Last();
                    var info = _mtdList.FirstOrDefault(m =>
                        m.Name.Equals(mtdName, StringComparison.OrdinalIgnoreCase));
                    if (info != null)
                        mtdTypes = info.TexType;
                }

                // Объединяем: сначала типы из MTD, потом общие (без дублей)
                var allTypes = mtdTypes
                    .Concat(_commonTexTypes.Where(t => !mtdTypes.Contains(t)))
                    .ToList();

                float typeComboW = Math.Min(ImGui.GetContentRegionAvail().X - 60, 360);
                ImGui.SetNextItemWidth(typeComboW);
                ImGui.SetNextWindowSize(new Vector2(typeComboW, 0), ImGuiCond.Always);
                if (ImGui.BeginCombo("##type_combo", _editTexType, ImGuiComboFlags.HeightLarge))
                {
                    // Отдельное поле фильтра
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputText("##textype_filter", ref _texTypeFilter, 64);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("X##ttf")) _texTypeFilter = "";
                    ImGui.Separator();

                    string filter = _texTypeFilter.ToLower();
                    foreach (var t in allTypes)
                    {
                        if (!string.IsNullOrEmpty(filter) &&
                            !t.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                        if (ImGui.Selectable(t, _editTexType == t))
                            _editTexType = t;
                    }
                    ImGui.Separator();
                    ImGui.TextDisabled("Custom:");
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputText("##type_custom", ref _editTexType, 256);
                    ImGui.EndCombo();
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("OK##type")) { ApplyTextureTypeEdit(); _texTypeFilter = ""; }
                ImGui.SameLine();
                if (ImGui.SmallButton("X##type"))  { _editingTexType = false; _texTypeFilter = ""; }
            }
        }

        private void RenderAddTexture()
        {
            // Типы из MTD текущего материала
            IEnumerable<string> mtdTypes = [];
            if (_selectedMaterial != null && !string.IsNullOrEmpty(_selectedMaterial.MTD) && _mtdList.Count > 0)
            {
                string mtdName = _selectedMaterial.MTD.Split('\\').Last();
                var info = _mtdList.FirstOrDefault(m =>
                    m.Name.Equals(mtdName, StringComparison.OrdinalIgnoreCase));
                if (info != null)
                    mtdTypes = info.TexType;
            }
            var allTypes = mtdTypes
                .Concat(_commonTexTypes.Where(t => !mtdTypes.Contains(t)))
                .ToList();

            ImGui.SetNextItemWidth(200);
            if (ImGui.BeginCombo("##add_type_combo", _addTexType, ImGuiComboFlags.HeightLarge))
            {
                foreach (var t in allTypes)
                    if (ImGui.Selectable(t, _addTexType == t))
                        _addTexType = t;
                ImGui.Separator();
                ImGui.TextDisabled("Custom:");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##add_custom", ref _addTexType, 256);
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Add##confirm"))
            {
                _flverTextureList.AddTexture(_addTexType);
                _addingTexType = false;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("X##add"))
                _addingTexType = false;
        }

        // ── Обработчики событий ──────────────────────────────────────────

        private void OnFlverFileSelected(FileNode fileNode) => LoadFlverMaterials(fileNode);

        private void OnMaterialSelected(FLVER2.Material material)
        {
            if (material == null) return;
            _selectedMaterial = material;
            _editingMTD       = false;
            _editMTD          = material.MTD ?? "";
            LoadTexturesForMaterial(material);
        }

        private void OnTextureSelected(FLVER2.Texture texture, int index)
        {
            if (texture == null) return;
            _selectedTexture  = texture;
            _editingTexPath   = false;
            _editingTexType   = false;
            _editTexPath      = texture.Path ?? "";
            _editTexType      = texture.ParamName ?? "";
        }

        // ── Применение изменений ─────────────────────────────────────────

        private void ApplyMTDEdit()
        {
            if (_selectedMaterial == null || string.IsNullOrEmpty(_editMTD)) return;
            _selectedMaterial.MTD = _editMTD;
            _editingMTD   = false;
            _mtdUvWarning = "";
        }

        private void ApplyTexturePathEdit()
        {
            if (_selectedTexture == null) return;
            _selectedTexture.Path = _editTexPath;
            _editingTexPath = false;
        }

        private void ApplyTextureTypeEdit()
        {
            if (_selectedTexture == null || string.IsNullOrEmpty(_editTexType)) return;
            _selectedTexture.ParamName = _editTexType;
            _editingTexType = false;
        }

        private void DeleteSelectedTexture()
        {
            if (_selectedMaterial == null || _selectedTexture == null) return;
            int idx = _flverTextureList.GetSelectedIndex();
            if (idx < 0 || idx >= _selectedMaterial.Textures.Count) return;
            _selectedMaterial.Textures.RemoveAt(idx);
            _flverTextureList.UpdateList(_selectedMaterial.Textures);
            _selectedTexture  = null;
            _editingTexPath   = false;
            _editingTexType   = false;
        }

        // ── Загрузка данных ──────────────────────────────────────────────

        private void LoadFlverMaterials(FileNode fileNode)
        {
            if (fileNode == null || string.IsNullOrEmpty(fileNode.VirtualPath)) return;

            // Если уже загружен другой файл — сбрасываем состояние редактирования
            if (_selectedFile != null &&
                !string.Equals(_selectedFile.VirtualPath, fileNode.VirtualPath, StringComparison.OrdinalIgnoreCase))
            {
                _editingMTD     = false;
                _editingTexPath = false;
                _editingTexType = false;
                _addingTexType  = false;
            }

            var binder = new FileBinders();
            binder.ProcessPaths([fileNode.VirtualPath], new FileOperation { GetObject = true });

            _currentFlver = binder.GetObject() switch
            {
                FlverWithFallback fb  => fb.Flver,
                FLVER2            flver => flver,
                BinderFile        bf    => FLVER2.Read(bf.Bytes),
                byte[]            b     => FLVER2.Read(b),
                _                       => null
            };

            if (_currentFlver == null)
            {
                Console.WriteLine($"[FMW] Cannot load FLVER: {fileNode.Name}");
                return;
            }

            _flverMaterialList.UpdateList(_currentFlver.Materials);
            _flverTextureList.ClearList();
            _selectedMaterial = null;
            _selectedTexture  = null;
            _selectedFile     = fileNode;
            Console.WriteLine($"[FMW] Loaded {_currentFlver.Materials.Count} materials from {fileNode.Name}");
        }

        private void LoadTexturesForMaterial(FLVER2.Material material)
        {
            _flverTextureList.UpdateList(material?.Textures ?? []);
            _selectedTexture  = null;
            _editingTexPath   = false;
            _editingTexType   = false;
        }

        // ── Файловые операции ────────────────────────────────────────────

        private void SetFile()
        {
            var files = DialogHelper.SelectFiles("Select FLVER file",
                "FLVER files|*.flver;*.flver.dcx|All files|*.*");
            var builder = new FileTreeNodeBuilder();
            foreach (var f in files)
                _fileListViewer.AddItemToList(builder.BuildTree(f));
        }

        private void SaveChanges()
        {
            if (_currentFlver == null || string.IsNullOrEmpty(_selectedFile?.VirtualPath))
            { SetStatus("✗ Nothing to save", error: true); return; }

            byte[] bytes;
            try
            {
                bytes = _currentFlver.Write();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FMW] Write() failed: {ex.Message}");

                if (!_useBytePatchFallback)
                { SetStatus("✗ Write failed (byte patch disabled)", error: true); return; }

                byte[] originalBytes = LoadOriginalBytes();
                if (originalBytes == null)
                { SetStatus("✗ Cannot load original bytes", error: true); return; }

                var fb = new FlverWithFallback(_currentFlver, originalBytes);
                var patched = fb.TryBytePatch(msg => Console.WriteLine($"[FMW] {msg}"));

                if (ReferenceEquals(patched, originalBytes))
                { SetStatus("✗ Byte patch: no applicable changes (structural change?)", error: true); return; }

                bytes = patched;
                Console.WriteLine("[FMW] Byte patch succeeded");
            }

            try
            {
                new FileBinders().ProcessPaths([_selectedFile.VirtualPath], new FileOperation
                {
                    WriteObject    = true,
                    ReplaceObject  = true,
                    NewObjectBytes = bytes
                });
                SetStatus($"✓ Saved: {_selectedFile.ShortName}");
                Console.WriteLine($"[FMW] Saved: {_selectedFile.ShortName}");
            }
            catch (Exception ex)
            {
                SetStatus($"✗ Save error: {ex.Message}", error: true);
                Console.WriteLine($"[FMW] Save error: {ex.Message}");
            }
        }

        private void SetStatus(string msg, bool error = false)
        {
            _saveStatus = msg;
            _saveStatusTimer = 4f;
            _ = error; // цвет определяется по содержимому строки (✗/✓)
        }

        private byte[] LoadOriginalBytes()
        {
            try
            {
                var binder = new FileBinders();
                binder.ProcessPaths([_selectedFile.VirtualPath], new FileOperation { GetObject = true });
                return binder.GetObject() switch
                {
                    FlverWithFallback fb => fb.OriginalBytes,
                    BinderFile bf        => bf.Bytes,
                    byte[] b             => b,
                    _                    => null
                };
            }
            catch { return null; }
        }

        private void TestSave()
        {
            var paths = _fileListViewer.GetFileList().Select(n => n.VirtualPath).ToList();
            int ok = 0, fail = 0;

            new FileBinders().ProcessPaths(paths, new FileOperation
            {
                UseFlverDelegate = true,
                AdditionalFlverProcessing = (flver, virtualPath, path, errorLogs) =>
                {
                    try   { flver.Write(); ok++; }
                    catch (Exception ex)
                    {
                        fail++;
                        errorLogs.Add($"Save error: {path} — {ex.Message}");
                        DumpFailedFlver(flver, virtualPath, path, ex.Message);
                    }
                }
            });

            _testSaveResult = $"Test: {ok} OK, {fail} failed";
            if (fail > 0) _testSaveResult += " (see flver_dump/)";
            Console.WriteLine($"[FMW] {_testSaveResult}");
        }

        private static void DumpFailedFlver(FLVER2 flver, string virtualPath, string name, string errorMessage)
        {
            try
            {
                string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string fileName   = Path.GetFileName(name);
                string folderName = $"{timestamp}_{Path.GetFileNameWithoutExtension(fileName)}";

                string dumpDir = Path.Combine(AppContext.BaseDirectory, "flver_dump", folderName);
                Directory.CreateDirectory(dumpDir);

                string flverPath = Path.Combine(dumpDir, fileName);
                try { File.WriteAllBytes(flverPath, flver.Write()); }
                catch { flverPath = "(write failed)"; }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== FLVER Write Error Dump ===");
                sb.AppendLine($"Time:         {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"File name:    {fileName}");
                sb.AppendLine($"Virtual path: {virtualPath}");
                sb.AppendLine($"Dump file:    {flverPath}");
                sb.AppendLine($"Error:        {errorMessage}");
                sb.AppendLine();
                sb.AppendLine($"--- Materials ({flver.Materials.Count}) ---");
                for (int i = 0; i < flver.Materials.Count; i++)
                {
                    var m = flver.Materials[i];
                    sb.AppendLine($"  [{i}] MTD={m.MTD}  Name={m.Name}  Textures={m.Textures.Count}");
                    foreach (var t in m.Textures)
                        sb.AppendLine($"       {t.ParamName}: {t.Path}");
                }
                File.WriteAllText(Path.Combine(dumpDir, "report.txt"), sb.ToString(), System.Text.Encoding.UTF8);
                Console.WriteLine($"[FlverDump] {dumpDir}");
            }
            catch (Exception dumpEx)
            {
                Console.WriteLine($"[FlverDump] Failed: {dumpEx.Message}");
            }
        }

        /// <summary>Очищает все загруженные данные и сбрасывает состояние редактора.</summary>
        public void ClearAll()
        {
            _fileListViewer.ClearList();
            _flverMaterialList.ClearList();
            _flverTextureList.ClearList();
            _currentFlver     = null;
            _selectedFile     = null;
            _selectedMaterial = null;
            _selectedTexture  = null;
            _editingMTD       = false;
            _editingTexPath   = false;
            _editingTexType   = false;
            _addingTexType    = false;
        }

        // ── Публичный API ────────────────────────────────────────────────

        /// <summary>Добавляет FileNode в список файлов редактора.</summary>
        public void SetNewItem(FileNode fileNode)     => _fileListViewer.AddItemToList(fileNode);
        /// <summary>Заменяет список файлов редактора.</summary>
        public void SetNewItemList(List<FileNode> nodes) => _fileListViewer.UpdateList(nodes);

        public FLVER2            CurrentFlver        => _currentFlver;
        public FileNode          GetSelectedFile()   => _selectedFile;
        public FLVER2.Material   GetSelectedMaterial() => _selectedMaterial;
        public FLVER2.Texture    GetSelectedTexture()  => _selectedTexture;

        // ── UV-совместимость ─────────────────────────────────────────────

        /// <summary>
        /// Считает количество UV-сетов для материала с индексом matIndex в FLVER.
        /// UV-сеты определяются по LayoutMember с Semantic.UV в вершинных буферах мешей.
        /// </summary>
        private static int CountUvSetsInFlver(FLVER2 flver, int matIndex)
        {
            int maxUv = 0;
            foreach (var mesh in flver.Meshes)
            {
                if (mesh.MaterialIndex != matIndex) continue;
                foreach (var vb in mesh.VertexBuffers)
                {
                    if (vb.LayoutIndex < 0 || vb.LayoutIndex >= flver.BufferLayouts.Count) continue;
                    var layout = flver.BufferLayouts[vb.LayoutIndex];
                    int uvCount = layout.Count(m => m.Semantic == FLVER.LayoutSemantic.UV);
                    if (uvCount > maxUv) maxUv = uvCount;
                }
            }
            return maxUv;
        }

        /// <summary>
        /// Считает UV-сеты нового MTD по его типам текстур как приближение.
        /// Используется только когда нет загруженного FLVER для сравнения.
        /// </summary>
        private static int CountUvSetsFromTexTypes(IEnumerable<string> texTypes)
        {
            // Каждый уникальный UV-индекс в имени слота = отдельный UV-сет
            // _2 суффикс = UV2, g_Lightmap = UV3, остальное = UV1
            var uvIndices = new HashSet<int>();
            foreach (var t in texTypes)
            {
                if (t.EndsWith("_2", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("_2_", StringComparison.OrdinalIgnoreCase))
                    uvIndices.Add(2);
                else if (t.Equals("g_Lightmap", StringComparison.OrdinalIgnoreCase) ||
                         t.EndsWith("_3", StringComparison.OrdinalIgnoreCase))
                    uvIndices.Add(3);
                else
                    uvIndices.Add(1);
            }
            return uvIndices.Count > 0 ? uvIndices.Max() : 1;
        }

        /// <summary>
        /// Сравнивает UV-сеты текущего материала (из FLVER) с новым MTD (из TexType).
        /// Возвращает предупреждение если количество UV не совпадает.
        /// </summary>
        private string CheckUvCompatibility(string oldMtdPath, MTDShortDetails newMtd)
        {
            if (_currentFlver == null || string.IsNullOrEmpty(oldMtdPath)) return "";

            // Находим индекс текущего материала в FLVER
            int matIndex = _currentFlver.Materials.FindIndex(m =>
                string.Equals(m.MTD, oldMtdPath, StringComparison.OrdinalIgnoreCase));
            if (matIndex < 0) return "";

            int oldUv = CountUvSetsInFlver(_currentFlver, matIndex);
            int newUv = CountUvSetsFromTexTypes(newMtd.TexType);

            if (oldUv == 0) return ""; // нет мешей — не можем сравнить
            if (oldUv == newUv) return "";

            return $"⚠ UV mismatch: mesh has {oldUv} UV, new MTD expects ~{newUv} — mesh may break!";
        }
    }
}
