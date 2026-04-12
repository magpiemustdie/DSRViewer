using DSRViewer.UI.Base;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace DSRViewer.Editors.MTDEditor
{
    /// <summary>
    /// Окно динамического редактирования параметров MTD.
    /// Позволяет выбрать параметры и задать им произвольные значения перед применением.
    /// </summary>
    public class MTDParamEditor : ImGuiWindow
    {
        private MTDTools _mtdTools;
        private string _mtdDir = "";

        // Параметры с галками и полями значений
        private bool _applyLightingType   = false;
        private int  _lightingType        = 3;

        private bool    _applyDiffuseColor = false;
        private Vector3 _diffuseColor      = Vector3.One;

        private bool    _applySpecularColor = false;
        private Vector3 _specularColor      = Vector3.One;

        private bool  _applyDiffusePower   = false;
        private float _diffusePower        = 1f;

        private bool  _applySpecularPower  = false;
        private float _specularPower       = 1f;

        private bool  _applySpecularExp    = false;
        private float _specularExp         = 1f;

        private bool  _applyShadowMul      = false;
        private float _shadowMul           = 1f;

        public MTDParamEditor(string windowName, bool showWindow) : base(windowName, showWindow)
        {
            _minSize = new Vector2(360, 380);
            _maxSize = new Vector2(600, 800);
            _mtdTools = new MTDTools();
        }

        public void SetMtdDir(string dir) => _mtdDir = dir;

        public override void Render()
        {
            if (!_showWindow) return;

            ApplySizeConstraints();
            ImGui.Begin(_windowName, ref _showWindow);

            ImGui.TextDisabled("Select parameters to apply and set their values.");
            ImGui.Spacing();
            ImGui.Separator();

            // g_LightingType
            ImGui.Checkbox("##lt", ref _applyLightingType); ImGui.SameLine();
            using (new Disabled(!_applyLightingType))
            {
                ImGui.SetNextItemWidth(120);
                ImGui.InputInt("g_LightingType", ref _lightingType);
            }

            ImGui.Spacing();

            // g_DiffuseMapColor
            ImGui.Checkbox("##dc", ref _applyDiffuseColor); ImGui.SameLine();
            using (new Disabled(!_applyDiffuseColor))
            {
                ImGui.SetNextItemWidth(200);
                ImGui.InputFloat3("g_DiffuseMapColor", ref _diffuseColor);
            }

            // g_SpecularMapColor
            ImGui.Checkbox("##sc", ref _applySpecularColor); ImGui.SameLine();
            using (new Disabled(!_applySpecularColor))
            {
                ImGui.SetNextItemWidth(200);
                ImGui.InputFloat3("g_SpecularMapColor", ref _specularColor);
            }

            ImGui.Spacing();

            // g_DiffuseMapColorPower
            ImGui.Checkbox("##dp", ref _applyDiffusePower); ImGui.SameLine();
            using (new Disabled(!_applyDiffusePower))
            {
                ImGui.SetNextItemWidth(120);
                ImGui.InputFloat("g_DiffuseMapColorPower", ref _diffusePower);
            }

            // g_SpecularMapColorPower
            ImGui.Checkbox("##sp", ref _applySpecularPower); ImGui.SameLine();
            using (new Disabled(!_applySpecularPower))
            {
                ImGui.SetNextItemWidth(120);
                ImGui.InputFloat("g_SpecularMapColorPower", ref _specularPower);
            }

            // g_SpecularPower
            ImGui.Checkbox("##se", ref _applySpecularExp); ImGui.SameLine();
            using (new Disabled(!_applySpecularExp))
            {
                ImGui.SetNextItemWidth(120);
                ImGui.InputFloat("g_SpecularPower", ref _specularExp);
            }

            // g_ShadowPowMul
            ImGui.Checkbox("##sh", ref _applyShadowMul); ImGui.SameLine();
            using (new Disabled(!_applyShadowMul))
            {
                ImGui.SetNextItemWidth(120);
                ImGui.InputFloat("g_ShadowPowMul", ref _shadowMul);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            bool anySelected = _applyLightingType || _applyDiffuseColor || _applySpecularColor
                            || _applyDiffusePower || _applySpecularPower || _applySpecularExp
                            || _applyShadowMul;

            if (!anySelected)
                ImGui.TextDisabled("Select at least one parameter.");
            else if (ImGui.Button("Apply to all MTDs"))
                Apply();

            ImGui.SameLine();
            if (ImGui.SmallButton("Reset all to 1"))
                ResetToOne();

            ImGui.End();
        }

        private void Apply()
        {
            if (string.IsNullOrEmpty(_mtdDir)) return;

            // Захватываем значения в локальные переменные для лямбды
            bool   applyLT  = _applyLightingType;  int   lt  = _lightingType;
            bool   applyDC  = _applyDiffuseColor;  var   dc  = new float[] { _diffuseColor.X,  _diffuseColor.Y,  _diffuseColor.Z };
            bool   applySC  = _applySpecularColor; var   sc  = new float[] { _specularColor.X, _specularColor.Y, _specularColor.Z };
            bool   applyDP  = _applyDiffusePower;  float dp  = _diffusePower;
            bool   applySP  = _applySpecularPower; float sp  = _specularPower;
            bool   applySE  = _applySpecularExp;   float se  = _specularExp;
            bool   applySH  = _applyShadowMul;     float sh  = _shadowMul;

            _mtdTools.ApplyParams(_mtdDir, mtd =>
            {
                if (applyLT) SetIfExists(mtd, "g_LightingType",          lt);
                if (applyDC) SetIfExists(mtd, "g_DiffuseMapColor",       dc);
                if (applySC) SetIfExists(mtd, "g_SpecularMapColor",      sc);
                if (applyDP) SetIfExists(mtd, "g_DiffuseMapColorPower",  dp);
                if (applySP) SetIfExists(mtd, "g_SpecularMapColorPower", sp);
                if (applySE) SetIfExists(mtd, "g_SpecularPower",         se);
                if (applySH) SetIfExists(mtd, "g_ShadowPowMul",         sh);
            });
        }

        private void ResetToOne()
        {
            _lightingType  = 3;
            _diffuseColor  = Vector3.One;
            _specularColor = Vector3.One;
            _diffusePower  = 1f;
            _specularPower = 1f;
            _specularExp   = 1f;
            _shadowMul     = 1f;
        }

        private static void SetIfExists(SoulsFormats.MTD mtd, string name, object value)
        {
            var prm = mtd.Params.Find(p => p.Name == name);
            if (prm != null) prm.Value = value;
        }

        // Вспомогательный scope для ImGui disabled state
        private readonly struct Disabled : IDisposable
        {
            public Disabled(bool disabled)
            {
                if (disabled) ImGui.BeginDisabled();
                _disabled = disabled;
            }
            private readonly bool _disabled;
            public void Dispose() { if (_disabled) ImGui.EndDisabled(); }
        }
    }
}
