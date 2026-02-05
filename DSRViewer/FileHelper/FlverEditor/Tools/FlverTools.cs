using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
using System.IO;
using System.Diagnostics;
using System.Drawing.Text;
using Veldrid;
using Vortice.Direct3D11;
using System.Text.RegularExpressions;
using System.Collections;
using System.Xml.Linq;
using System.Numerics;
using DSRViewer.FileHelper;
using DSRViewer.FileHelper.MTDEditor.Render;

namespace DSRViewer.FileHelper.FlverEditor.Tools
{
    internal class FlverTools
    {
        /*
        public List<FLVER2.Material> GetFlverMat(FLVER2 flver) //Get flver materials
        {
            Console.WriteLine("Creating material list...");
            List<FLVER2.Material> Materials = flver.Materials;
            Console.WriteLine("Creating material list - Done");
            return Materials;
        }
        */

        public void FlverWriter(FLVER2 flver, List<FLVER2.Material> materials, string virtualPath) //Flver writer
        {
            FileBinders binders = new();
            if (!(flver == null | materials == null))
            {


                for (int i = 0; i < flver.Materials.Count; i++)
                {
                    flver.Materials[i].MTD = materials[i].MTD;

                    if (flver.Materials[i].Textures.Count < materials[i].Textures.Count)
                    {
                        int difference = materials[i].Textures.Count - flver.Materials[i].Textures.Count;
                        for (int k = 0; k < difference; k++)
                        {
                            flver.Materials[i].Textures.Add(new FLVER2.Texture());
                        }
                    }

                    flver.Materials[i].Textures = materials[i].Textures;
                }
                binders.SetFlver(true, true, flver);
                binders.SetCommon(false, true);
                binders.Read(virtualPath);
                Console.WriteLine("Write Done");
            }
        }

        public bool TexFinder(List<FLVER2.Material> materials, string pattern) //Find model by texture name
        {
            bool found = false;
            foreach (var mat in materials)
            {
                foreach (var tex in mat.Textures)
                {
                    if (tex.Path.Split("\\").Last().ToLower() == pattern.ToLower())
                    {
                        return found = true;
                    }
                }
            }
            return found;
        }

        public bool MTDFinder(List<FLVER2.Material> materials, string mtdpattern) //Find mtds by texture name
        {
            bool found = false;
            foreach (var mat in materials)
            {
                if (mat.MTD.Split("\\").Last().ToLower() == mtdpattern.ToLower())
                {
                    return found = true;
                }
            }
            return found;
        }

        public bool MTDFinder(List<FLVER2.Material> materials, string texpattern, string mtdpattern) //Find mtds by texture name
        {
            bool found = false;
            foreach (var mat in materials)
            {
                foreach (var tex in mat.Textures)
                {
                    if (tex.Path.Split("\\").Last().ToLower() == texpattern.ToLower())
                    {
                        if (mat.MTD.Split("\\").Last().ToLower() == mtdpattern.ToLower())
                        {
                            return found = true;
                        }
                    }
                }
            }
            return found;
        }

        public void MTDFinderAll(List<FLVER2.Material> materials, List<string> allMaterials) //Find all mtds
        {
            foreach (var mat in materials)
            {
                allMaterials.Add(mat.MTD.Split("\\").Last().ToLower());
            }
        }

        public void MTDFinderList(List<FLVER2.Material> materials, string pattern, List<string> mtd_list) //Find mtds by texture name
        {
            foreach (var mat in materials)
            {
                foreach (var tex in mat.Textures)
                {
                    if (tex.Path.Split("\\").Last().ToLower() == pattern.ToLower())
                    {
                        mtd_list.Add(mat.MTD);
                    }
                }
            }
        }

        public void MTDReplacer(List<FLVER2.Material> materials, string texpattern, string mtdpattern, string mtdnewpattern) //Find mtds by texture name
        {
            foreach (var mat in materials)
            {
                foreach (var tex in mat.Textures)
                {
                    if (tex.Path.Split("\\").Last().ToLower() == texpattern.ToLower())
                    {
                        if (mat.MTD.Split("\\").Last().ToLower() == mtdpattern.ToLower())
                        {
                            mat.MTD = mat.MTD.Replace(mtdpattern, mtdnewpattern);
                        }
                    }
                }
            }
        }

        
        public List<FLVER2.Material> MTDReplacerHeight(List<MTDShortDetails> mtdList, List<FLVER2.Material> materials, string texpattern, string mtdpattern, string mtdnewname, string heightnewname) //Find mtds by texture name
        {
            for (int i = 0; i < materials.Count; i++)
            {
                for (int j = 0; j < materials[i].Textures.Count; j++)
                {
                    if (materials[i].Textures[j].Path.Split("\\").Last().ToLower() == texpattern.ToLower())
                    {
                        if (materials[i].MTD.Split("\\").Last().ToLower() == mtdpattern.ToLower())
                        {
                            materials[i].MTD = materials[i].MTD.Replace(mtdpattern, mtdnewname);

                            materials[i].Textures = G_List_Changer(mtdList, materials[i].MTD, materials[i].Textures, heightnewname);
                        }
                    }
                }
            }

            return materials;
        }


        private List<FLVER2.Texture> G_List_Changer(List<MTDShortDetails> mtdList, string mtdName, List<FLVER2.Texture> textures, string heightnewname)
        {
            List<FLVER2.Texture> temp_texList = [];

            foreach (var mtd in mtdList)
            {
                if (mtdName.Split("\\").Last().Equals(mtd.Name, StringComparison.CurrentCultureIgnoreCase))
                {
                    foreach (var mtdTex in mtd.TexType)
                    {
                        temp_texList.Add(new FLVER2.Texture(mtdTex, "", new Vector2(1, 1), 1, true, 0, 0, 0));
                    }

                    for (int i = 0; i < temp_texList.Count; i++)
                    {
                        for (int j = 0; j < textures.Count; j++)
                        {
                            if (textures[j].Type == temp_texList[i].Type)
                            {
                                temp_texList[i] = textures[j];
                            }
                        }
                    }

                    string temp_texPath = string.Empty;

                    foreach (var tex in temp_texList)
                    {
                        if (tex.Type == "g_Diffuse")
                        {
                            temp_texPath = tex.Path;
                        }
                        if (tex.Type == "g_Height")
                        {
                            if (temp_texPath.ToLower().EndsWith(".tga"))
                            {
                                tex.Path = temp_texPath.Split(".")[^2] + "_h.tga";
                            }
                            else
                            {
                                tex.Path = heightnewname;
                            }
                        }
                    }
                }
            }

            return temp_texList;
        }

        public List<string> GetGType(List<FLVER2.Material> materials, string file, List<string> gTypeList)
        {
            foreach (var mat in materials)
            {
                foreach (var tex in mat.Textures)
                {
                    gTypeList.Add(tex.Type);
                }
            }
            return gTypeList;
        }

        public List<string> TexCorrectorFinder(List<FLVER2.Material> materials, string virtualPath, string file, List<string> bugList) //Name bug parser
        {
            foreach (var mat in materials)
            {
                foreach (var tex in mat.Textures)
                {
                    switch (tex.Type)
                    {
                        case "g_Diffuse":
                            {
                                if (tex.Path.ToLower().EndsWith("_s.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Diffuse: {tex.Path}" + Environment.NewLine);
                                }

                                if (tex.Path.ToLower().EndsWith("_n.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Diffuse: {tex.Path}" + Environment.NewLine);
                                }

                                if (tex.Path.ToLower().Contains("_t.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Diffuse: {tex.Path}" + Environment.NewLine);
                                }

                                if (tex.Path.ToLower().Contains("lit"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Diffuse: {tex.Path}" + Environment.NewLine);
                                }
                                break;
                            }

                        case "g_Diffuse_2":
                            {
                                if (tex.Path.ToLower().EndsWith("_s.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Diffuse_2: {tex.Path}" + Environment.NewLine);
                                }

                                if (tex.Path.ToLower().EndsWith("_n.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Diffuse_2: {tex.Path}" + Environment.NewLine);
                                }

                                if (tex.Path.ToLower().Contains("_t.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Diffuse_2: {tex.Path}" + Environment.NewLine);
                                }

                                if (tex.Path.ToLower().Contains("lit"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Diffuse_2: {tex.Path}" + Environment.NewLine);
                                }
                                break;
                            }

                        case "g_Specular":
                            {
                                if (!tex.Path.EndsWith("_s.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Specular: {tex.Path}" + Environment.NewLine);
                                }
                                break;
                            }

                        case "g_Specular_2":
                            {
                                if (!tex.Path.EndsWith("_s.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Specular_2: {tex.Path}" + Environment.NewLine);
                                }
                                break;
                            }

                        case "g_Height":
                            {
                                if (!tex.Path.EndsWith("_h.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Height: {tex.Path}" + Environment.NewLine);
                                }
                                break;
                            }

                        case "g_Bumpmap":
                            {
                                if (!tex.Path.EndsWith("_n.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Bumpmap: {tex.Path}" + Environment.NewLine);
                                }
                                break;
                            }

                        case "g_Bumpmap_2":
                            {
                                if (!tex.Path.EndsWith("_n.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Bumpmap_2: {tex.Path}" + Environment.NewLine);
                                }
                                break;
                            }

                        case "g_Bumpmap_3":
                            {
                                if (!tex.Path.EndsWith("_n.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Bumpmap_3: {tex.Path}" + Environment.NewLine);
                                }
                                break;
                            }

                        case "g_DetailBumpmap":
                            {
                                if (tex.Path != "")
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_DetailBumpmap: {tex.Path}" + Environment.NewLine);
                                }
                                break;
                            }

                        case "g_Subsurf":
                            {
                                if (!tex.Path.EndsWith("_t.tga"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Subsurf: {tex.Path}" + Environment.NewLine);
                                }
                                break;
                            }

                        case "g_Lightmap":
                            {
                                if (!tex.Path.ToLower().Contains("lit"))
                                {
                                    bugList.Add($"{virtualPath} --> {file} --> g_Lightmap: {tex.Path}" + Environment.NewLine);
                                }
                                break;
                            }
                    }
                }
            }
            return bugList;
        }

        public void TexCorrectorReplacer(FLVER2 flver, List<FLVER2.Material> materials, string gType, string old_texture, string new_texture) //Name bug parser
        {
            foreach (var mat in materials)
            {
                foreach (var tex in mat.Textures)
                {
                    if (tex.Type == gType)
                    {
                        tex.Path = tex.Path.Replace(old_texture, new_texture);
                    }
                }
            }

            flver.Materials = materials;
        }

        public bool TexCorrectorFinderToLower(List<FLVER2.Material> materials)
        {
            foreach (var mat in materials)
            {
                foreach (var tex in mat.Textures)
                {
                    if (tex.Path.ToLower().EndsWith("_s.tga"))
                    {
                        if (!IsLower(tex.Path))
                        {
                            return true;
                        }
                    }
                    if (tex.Path.ToLower().EndsWith("_n.tga"))
                    {
                        if (!IsLower(tex.Path))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void TexCorrectorToLower(FLVER2 flver, List<FLVER2.Material> materials)
        {
            foreach (var mat in materials)
            {
                foreach (var tex in mat.Textures)
                {
                    if (tex.Path.ToLower().EndsWith("_s.tga"))
                    {
                        Console.WriteLine($"...Upper to lower: {tex.Path}");
                        tex.Path = Regex.Split(tex.Path, "_s.tga", RegexOptions.IgnoreCase)[0] + "_s.tga";
                    }
                    if (tex.Path.ToLower().EndsWith("_n.tga"))
                    {
                        Console.WriteLine($"...Upper to lower: {tex.Path}");
                        tex.Path = Regex.Split(tex.Path, "_n.tga", RegexOptions.IgnoreCase)[0] + "_n.tga";
                    }
                }
            }

            flver.Materials = materials;
        }

        private bool IsLower(string value)
        {
            for (int i = value.Length - 1; i >= value.Length - 6; i--)
            {
                if (char.IsUpper(value[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
