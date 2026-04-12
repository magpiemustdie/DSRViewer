using DSRViewer.FileProcess;
using DSRViewer.Editors.MTDEditor;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static SoulsFormats.FLVER2.Texture;

namespace DSRViewer.Editors.FlverEditor.Tools
{
    /// <summary>Вспомогательные методы для работы с FLVER-материалами и текстурами.</summary>
    internal class FlverTools
    {
        // Вспомогательный метод — имя файла из пути с обратными слешами
        private static string FileName(string path) =>
            path.Replace('/', '\\').Split('\\').Last();

        /// <summary>Записывает изменённые материалы обратно в FLVER.</summary>
        public void FlverMTDWriter(FLVER2 flver, List<FLVER2.Material> materials, string virtualPath)
        {
            if (flver == null || materials == null) return;
            if (flver.Materials.Count != materials.Count)
            {
                Console.WriteLine($"[FlverMTDWriter] Material count mismatch: flver={flver.Materials.Count}, list={materials.Count} at {virtualPath}");
                return;
            }

            for (int i = 0; i < flver.Materials.Count; i++)
            {
                flver.Materials[i].MTD      = materials[i].MTD;
                flver.Materials[i].Name     = materials[i].Name;
                flver.Materials[i].GXIndex  = materials[i].GXIndex;
                flver.Materials[i].Textures = materials[i].Textures;
            }
        }

        /// <summary>Проверяет наличие текстуры с указанным именем в материалах.</summary>
        public bool TexFinder(List<FLVER2.Material> materials, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            foreach (var mat in materials)
                foreach (var tex in mat.Textures)
                    if (!string.IsNullOrEmpty(tex.Path) &&
                        FileName(tex.Path).Equals(pattern, StringComparison.OrdinalIgnoreCase))
                        return true;
            return false;
        }

        /// <summary>Проверяет наличие материала с указанным MTD-именем.</summary>
        public bool MTDFinder(List<FLVER2.Material> materials, string mtdpattern)
        {
            if (string.IsNullOrEmpty(mtdpattern)) return false;
            foreach (var mat in materials)
                if (!string.IsNullOrEmpty(mat.MTD) &&
                    FileName(mat.MTD).Equals(mtdpattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public bool MTDFinder(List<FLVER2.Material> materials, string texpattern, string mtdpattern)
        {
            if (string.IsNullOrEmpty(texpattern) || string.IsNullOrEmpty(mtdpattern)) return false;
            foreach (var mat in materials)
                foreach (var tex in mat.Textures)
                    if (!string.IsNullOrEmpty(tex.Path) && !string.IsNullOrEmpty(mat.MTD) &&
                        FileName(tex.Path).Equals(texpattern, StringComparison.OrdinalIgnoreCase) &&
                        FileName(mat.MTD).Equals(mtdpattern, StringComparison.OrdinalIgnoreCase))
                        return true;
            return false;
        }

        public void MTDFinderAll(List<FLVER2.Material> materials, List<string> allMaterials)
        {
            foreach (var mat in materials)
                if (!string.IsNullOrEmpty(mat.MTD))
                    allMaterials.Add(FileName(mat.MTD).ToLower());
        }

        public void TexFinderAll(List<FLVER2.Material> materials, List<string> allTextures)
        {
            foreach (var mat in materials)
                foreach (var tex in mat.Textures)
                    if (!string.IsNullOrEmpty(tex.Path))
                        allTextures.Add(FileName(tex.Path).ToLower());
        }

        public void MTDFinderList(List<FLVER2.Material> materials, string pattern, List<string> mtd_list)
        {
            if (string.IsNullOrEmpty(pattern)) return;
            foreach (var mat in materials)
                foreach (var tex in mat.Textures)
                    if (!string.IsNullOrEmpty(tex.Path) &&
                        FileName(tex.Path).Equals(pattern, StringComparison.OrdinalIgnoreCase))
                        mtd_list.Add(mat.MTD ?? "");
        }

        /// <summary>Проверяет наличие текстурного слота с указанным ParamName в материалах.</summary>
        public bool TexSlotFinder(List<FLVER2.Material> materials, string paramName)
        {
            if (string.IsNullOrEmpty(paramName)) return true;
            foreach (var mat in materials)
                foreach (var tex in mat.Textures)
                    if (!string.IsNullOrEmpty(tex.ParamName) &&
                        tex.ParamName.Equals(paramName, StringComparison.OrdinalIgnoreCase))
                        return true;
            return false;
        }
        public void MTDReplacer(List<FLVER2.Material> materials, string texpattern, string mtdpattern, string mtdnewpattern)
        {
            if (string.IsNullOrEmpty(mtdpattern)) return;
            foreach (var mat in materials)
            {
                if (string.IsNullOrEmpty(mat.MTD)) continue;
                if (!FileName(mat.MTD).Equals(mtdpattern, StringComparison.OrdinalIgnoreCase)) continue;

                // Если texpattern задан — заменяем только материалы содержащие эту текстуру
                if (!string.IsNullOrEmpty(texpattern) &&
                    !mat.Textures.Any(t => !string.IsNullOrEmpty(t.Path) &&
                        FileName(t.Path).Equals(texpattern, StringComparison.OrdinalIgnoreCase)))
                    continue;

                int lastSlash = mat.MTD.LastIndexOf('\\');
                mat.MTD = lastSlash >= 0
                    ? mat.MTD[..(lastSlash + 1)] + mtdnewpattern
                    : mtdnewpattern;
            }
        }

        /// <summary>Заменяет MTD и перестраивает список текстур с добавлением слота высоты.</summary>
        public List<FLVER2.Material> MTDReplacerHeight(List<MTDShortDetails> mtdList, List<FLVER2.Material> materials,
            string texpattern, string mtdpattern, string mtdnewname, string heightnewname,
            AutoFillOptions opts = null)
        {
            if (string.IsNullOrEmpty(mtdpattern)) return materials;
            opts ??= new AutoFillOptions();

            for (int i = 0; i < materials.Count; i++)
            {
                if (string.IsNullOrEmpty(materials[i].MTD)) continue;
                if (!FileName(materials[i].MTD).Equals(mtdpattern, StringComparison.OrdinalIgnoreCase)) continue;

                // Если texpattern задан — заменяем только материалы содержащие эту текстуру
                if (!string.IsNullOrEmpty(texpattern) &&
                    !materials[i].Textures.Any(t =>
                        !string.IsNullOrEmpty(t.Path) &&
                        FileName(t.Path).Equals(texpattern, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Заменяем только имя файла в конце пути MTD
                int lastSlash = materials[i].MTD.LastIndexOf('\\');
                materials[i].MTD = lastSlash >= 0
                    ? materials[i].MTD[..(lastSlash + 1)] + mtdnewname
                    : mtdnewname;

                materials[i].Textures = G_List_Changer(mtdList, materials[i].MTD, materials[i].Textures, heightnewname, opts);
            }
            return materials;
        }

        /// <summary>Управляет автозаполнением текстурных слотов при замене MTD.</summary>
        public class AutoFillOptions
        {
            public bool Specular      { get; set; } = true;
            public bool Bumpmap       { get; set; } = true;
            public bool Height        { get; set; } = true;
            public bool Subsurf       { get; set; } = true;
            public bool DetailBumpmap { get; set; } = true;
            public bool Lightmap      { get; set; } = true;
        }

        private List<FLVER2.Texture> G_List_Changer(List<MTDShortDetails> mtdList, string mtdName,
            List<FLVER2.Texture> textures, string heightnewname, AutoFillOptions opts = null)
        {
            string mtdFileName = FileName(mtdName);

            foreach (var mtd in mtdList)
            {
                string mtdEntryName = FileName(mtd.Name);
                if (!mtdFileName.Equals(mtdEntryName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var temp = new List<FLVER2.Texture>();
                foreach (var t in mtd.TexType)
                    temp.Add(new FLVER2.Texture(t, "", new Vector2(1, 1), TilingType.Repeat, TilingType.Repeat, 0, 0, 0));

                // Переносим существующие текстуры по ParamName
                for (int i = 0; i < temp.Count; i++)
                    for (int j = 0; j < textures.Count; j++)
                        if (string.Equals(textures[j].ParamName, temp[i].ParamName, StringComparison.OrdinalIgnoreCase))
                            temp[i] = textures[j];

                // Автозаполнение пустых слотов на основе существующих путей
                AutoFillTexturePaths(temp, heightnewname, opts ?? new AutoFillOptions());

                return temp;
            }

            // MTD не найден — возвращаем оригинальный список без изменений
            Console.WriteLine($"[G_List_Changer] MTD not found: {mtdFileName}, keeping original textures");
            return textures;
        }

        /// <summary>
        /// Автоматически заполняет пустые текстурные слоты выводя пути из уже заполненных.
        /// Логика:
        ///   g_Specular / g_Specular_2   ← g_Diffuse / g_Diffuse_2  + "_s"
        ///   g_Bumpmap  / g_Bumpmap_2/3  ← g_Diffuse / g_Diffuse_2  + "_n"
        ///   g_Height                    ← g_Diffuse                 + "_h"  (или heightFallback)
        ///   g_Subsurf                   ← g_Diffuse                 + "_t"
        ///   g_DetailBumpmap             ← g_Bumpmap                 (копия нормали)
        ///   g_Lightmap                  ← g_Diffuse_2               + "_n"  (если есть UV2)
        /// Слот заполняется только если он пустой после переноса.
        /// </summary>
        private static void AutoFillTexturePaths(List<FLVER2.Texture> slots, string heightFallback, AutoFillOptions opts)
        {
            string Get(string param) =>
                slots.FirstOrDefault(t => t.ParamName.Equals(param, StringComparison.OrdinalIgnoreCase))?.Path ?? "";

            string Derive(string basePath, string suffix)
            {
                if (string.IsNullOrEmpty(basePath)) return "";
                string noExt = basePath.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)
                    ? basePath[..basePath.LastIndexOf('.')]
                    : basePath;
                foreach (var s in new[] { "_s", "_n", "_h", "_t" })
                    if (noExt.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                    { noExt = noExt[..^s.Length]; break; }
                return noExt + suffix + ".tga";
            }

            void Fill(string param, string derivedPath)
            {
                if (string.IsNullOrEmpty(derivedPath)) return;
                var slot = slots.FirstOrDefault(t =>
                    t.ParamName.Equals(param, StringComparison.OrdinalIgnoreCase));
                if (slot != null && string.IsNullOrEmpty(slot.Path))
                    slot.Path = derivedPath;
            }

            string diffuse     = Get("g_Diffuse");
            string diffuse2    = Get("g_Diffuse_2");
            string bumpmap     = Get("g_Bumpmap");
            string diffuseBase = !string.IsNullOrEmpty(diffuse) ? diffuse : diffuse2;

            if (opts.Specular)
            {
                Fill("g_Specular",   Derive(diffuseBase, "_s"));
                Fill("g_Specular_2", Derive(diffuse2,    "_s"));
            }
            if (opts.Bumpmap)
            {
                Fill("g_Bumpmap",   Derive(diffuseBase, "_n"));
                Fill("g_Bumpmap_2", Derive(diffuse2,    "_n"));
                Fill("g_Bumpmap_3", Derive(diffuse2,    "_n"));
            }
            if (opts.Subsurf)
                Fill("g_Subsurf", Derive(diffuseBase, "_t"));
            if (opts.DetailBumpmap)
                Fill("g_DetailBumpmap", string.IsNullOrEmpty(bumpmap) ? Derive(diffuseBase, "_n") : bumpmap);
            if (opts.Lightmap)
                Fill("g_Lightmap", Derive(diffuse2, "_n"));

            if (opts.Height)
            {
                var heightSlot = slots.FirstOrDefault(t =>
                    t.ParamName.Equals("g_Height", StringComparison.OrdinalIgnoreCase));
                if (heightSlot != null && string.IsNullOrEmpty(heightSlot.Path))
                    heightSlot.Path = !string.IsNullOrEmpty(diffuseBase)
                        ? Derive(diffuseBase, "_h")
                        : heightFallback;
            }
        }

        public List<string> TexCorrectorFinder(List<FLVER2.Material> materials, string virtualPath,
            string file, List<string> bugList)
        {
            foreach (var mat in materials)
            {
                foreach (var tex in mat.Textures)
                {
                    if (string.IsNullOrEmpty(tex.Path)) continue;

                    string p    = tex.Path.ToLower();
                    string slot = tex.ParamName ?? "";

                    bool err = slot switch
                    {
                        "g_Diffuse" or "g_Diffuse_2" =>
                            p.EndsWith("_s.tga") || p.EndsWith("_n.tga") || p.EndsWith("_t.tga") || p.Contains("lit"),
                        "g_Specular" or "g_Specular_2" => !p.EndsWith("_s.tga"),
                        "g_Height"   => !p.EndsWith("_h.tga"),
                        "g_Bumpmap" or "g_Bumpmap_2" or "g_Bumpmap_3" => !p.EndsWith("_n.tga"),
                        "g_DetailBumpmap" => !p.EndsWith("_n.tga"),
                        "g_Subsurf"  => !p.EndsWith("_t.tga"),
                        "g_Lightmap" => !p.Contains("lit"),
                        _ => false
                    };

                    if (err)
                        bugList.Add($"{virtualPath} --> {file} --> {slot}: {tex.Path}{Environment.NewLine}");
                }
            }
            return bugList;
        }

        // Упрощённые сигнатуры — принимают FLVER2 напрямую
        public void TexCorrectorReplacer(FLVER2 flver, string gType, string oldTex, string newTex)
        {
            if (string.IsNullOrEmpty(gType) || string.IsNullOrEmpty(oldTex)) return;
            foreach (var mat in flver.Materials)
                foreach (var tex in mat.Textures)
                    if (tex.ParamName == gType && !string.IsNullOrEmpty(tex.Path))
                        tex.Path = tex.Path.Replace(oldTex, newTex);
        }

        public bool TexCorrectorFinderToLower(FLVER2 flver)
        {
            foreach (var mat in flver.Materials)
                foreach (var tex in mat.Textures)
                {
                    if (string.IsNullOrEmpty(tex.Path)) continue;
                    string p = tex.Path.ToLower();
                    if ((p.EndsWith("_s.tga") || p.EndsWith("_n.tga")) && !IsLower(tex.Path))
                        return true;
                }
            return false;
        }

        public void TexCorrectorToLower(FLVER2 flver)
        {
            foreach (var mat in flver.Materials)
                foreach (var tex in mat.Textures)
                {
                    if (string.IsNullOrEmpty(tex.Path)) continue;
                    string lower = tex.Path.ToLower();
                    // Заменяем суффикс на lowercase сохраняя остальной путь
                    if (lower.EndsWith("_s.tga"))
                        tex.Path = tex.Path[..^"_s.tga".Length] + "_s.tga";
                    else if (lower.EndsWith("_n.tga"))
                        tex.Path = tex.Path[..^"_n.tga".Length] + "_n.tga";
                }
        }

        // Обратная совместимость — старые сигнатуры с List<Material>
        public void TexCorrectorReplacer(FLVER2 flver, List<FLVER2.Material> _, string gType, string oldTex, string newTex)
            => TexCorrectorReplacer(flver, gType, oldTex, newTex);

        public bool TexCorrectorFinderToLower(List<FLVER2.Material> materials)
            => materials.SelectMany(m => m.Textures)
                .Any(t => !string.IsNullOrEmpty(t.Path) &&
                          (t.Path.ToLower().EndsWith("_s.tga") || t.Path.ToLower().EndsWith("_n.tga"))
                          && !IsLower(t.Path));

        public void TexCorrectorToLower(FLVER2 flver, List<FLVER2.Material> _)
            => TexCorrectorToLower(flver);

        private static bool IsLower(string value)
        {
            int start = Math.Max(0, value.Length - 6);
            for (int i = value.Length - 1; i >= start; i--)
                if (char.IsUpper(value[i])) return false;
            return true;
        }
    }
}
