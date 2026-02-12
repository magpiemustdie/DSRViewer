using System;
using System.Collections.Generic;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using DSRViewer.FileHelper.FileExplorer.DDSHelper;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Utilities.IO.Pem;
using SoulsFormats;
using Vortice.Direct3D11;
using static System.Windows.Forms.LinkLabel;

namespace DSRViewer.FileHelper
{
    public class FileBindersOld
    {
        object? _main_object = new();
        bool _getobject = false;

        bool _writer = false;
        bool _replacer = false;
        byte[] _new_bytes = [];

        bool _flv_replace = false;
        bool _flv_writer = false;
        FLVER2 _new_flver = new();

        bool _dds_add = false;
        bool _dds_remove = false;
        bool _dds_replace_bytes = false;
        bool _dds_replace_flag = false;
        bool _dds_replace_name = false;
        byte[] _dds_new_bytes = [];
        byte _dds_new_flag = 0;
        string _dds_new_name = "";

        //all
        public void SetAllWriter(bool writer)
        {
            _writer = writer;
        }
        public void SetAllReplace(byte[] new_bytes, bool replacer)
        {
            _replacer = replacer;
            _new_bytes = new_bytes;
        }

        //flver
        public void SetFlverWriter(bool flv_writer)
        {
            _flv_writer = flv_writer;
        }
        public void SetFlverReplace(FLVER2 new_flver, bool flv_replace)
        {
            _new_flver = new_flver;
            _flv_replace = flv_replace;
        }

        //tpf and dds
        public void SetDDSReplace(byte[] dds_new_bytes, byte dds_new_flag,
            bool dds_replace_bytes, bool dds_replace_flag)
        {
            _dds_replace_bytes = dds_replace_bytes;
            _dds_new_bytes = dds_new_bytes;
            _dds_replace_flag = dds_replace_flag;
            _dds_new_flag = dds_new_flag;
        }
        public void SetDDSReplaceFlag(byte dds_new_flag, bool dds_replace_flag)
        {
            _dds_replace_flag = dds_replace_flag;
            _dds_new_flag = dds_new_flag;
        }
        public void SetDDSReplaceName(string dds_new_name, bool dds_replace_name)
        {
            _dds_replace_name = dds_replace_name;
            _dds_new_name = dds_new_name;
        }

        public void SetDDSAdd(bool dds_add)
        {
            _dds_add = dds_add;
        }
        public void SetDDSRemove(bool dds_remove)
        {
            _dds_remove = dds_remove;
        }

        public void SetGetObject(bool getobject)
        {
            _getobject = getobject;
        }

        public object GetObject() => _main_object;
        public void CleanObject() { _main_object = null; }

        public void ReadFile(string VirtualPath)
        {
            string path = VirtualPath.Split("|")[0];
            int[] v_path = VirtualPath.Split("|").Skip(1).Where(s => int.TryParse(s, out _)).Select(int.Parse).ToArray();

            Console.WriteLine($"Read file: {VirtualPath}");

            if (IsBndFile(path))
            {
                OpenBndFile(path, v_path);
            }
            else if (IsTpfFile(path))
            {
                OpenTPFFile(path, v_path);
            }
            else if (IsFlverFile(path))
            {
                OpenFlvFile(path);
            }
            else if (IsBxfFile(path))
            {
                OpenBxfFile(path, v_path);
            }
            else
            {
                Console.WriteLine("Unknown file type");
            }
        }

        private void OpenBndFile(string bndPath, int[] v_path)
        {
            try
            {
                var bnd = BND3.Read(bndPath);
                if (_getobject & v_path.Length == 0)
                {
                    _main_object = bnd;
                }

                if (v_path.Length > 0)
                {
                    if (IsBndData(bnd.Files[v_path[0]].Bytes))
                    {
                        ReadNestedBnd(bnd.Files[v_path[0]], v_path, 0);
                    }
                    else if (IsTpfData(bnd.Files[v_path[0]].Bytes))
                    {
                        ReadNestedTPF(bnd.Files[v_path[0]], v_path, 0);
                    }
                    else if (IsBxfData(bnd.Files[v_path[0]].Bytes))
                    {
                        ReadNestedBXF(bnd.Files[v_path[0]], bndPath, v_path, 0);
                    }
                    else if (IsFlvData(bnd.Files[v_path[0]].Bytes))
                    {
                        ReadNestedFlver(bnd.Files[v_path[0]], v_path, 0);
                    }
                }

                if (_replacer & v_path.Length == 0)
                {
                    Console.WriteLine($"Replace: {bndPath}");
                    bnd = BND3.Read(_new_bytes);
                }

                if (_writer)
                {
                    bnd.Write(bndPath);
                }
            }
            catch (Exception ex) { Console.WriteLine(ex); }
        }

        private void OpenTPFFile(string tpfPath, int[] v_path)
        {
            try
            {
                var tpf = TPF.Read(tpfPath);

                if (_getobject & v_path.Length == 0)
                {
                    _main_object = tpf;
                }

                if (v_path.Length > 0)
                {
                    if (IsDdsData(tpf.Textures[v_path[0]].Bytes))
                    {
                        ReadNestedDDS(tpf.Textures[v_path[0]], v_path, 0);
                    }

                    if (_dds_remove & v_path.Length == 0)
                    {
                        tpf.Textures.Remove(tpf.Textures[v_path[0]]);
                        Console.WriteLine("Removed");
                    }
                }

                if (_dds_add)
                {
                    tpf.Textures.Add(new TPF.Texture());
                    tpf.Textures.Last().Name = "New_texture";
                    tpf.Textures.Last().Platform = TPF.TPFPlatform.PC;
                    tpf.Textures.Last().Bytes = DDSTools.fatcat;
                    Console.WriteLine("Added new file");
                }

                if (_replacer & v_path.Length == 0)
                {
                    Console.WriteLine($"Replace: {tpfPath}");
                    tpf = TPF.Read(_new_bytes);
                }

                if (_writer)
                {
                    tpf.Write(tpfPath);
                }
            }
            catch (Exception ex) { Console.WriteLine(ex); }
        }


        private void OpenFlvFile(string path)
        {
            FLVER2 flver = new();
            try
            {
                Console.WriteLine($"Read flver: {path}");

                flver = FLVER2.Read(path);
            }
            catch (Exception ex) { Console.WriteLine(ex); }

            if (_getobject)
            {
                _main_object = flver;
            }

            if (_flv_replace)
            {
                Console.WriteLine($"Replace: {path}");
                flver = _new_flver;
            }

            if (_flv_writer)
            {
                byte[] bak = File.ReadAllBytes(path);
                try
                {
                    Console.WriteLine($"Write flver: {path}");
                    flver.Write(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Console.WriteLine("Return flver backup");
                    File.WriteAllBytes(path, bak);
                }
            }
        }

        private void OpenBxfFile(string bhdPath, int[] v_path) //bxf archive with virtual path
        {
            string bdtPath = bhdPath.Replace(".tpfbhd", ".tpfbdt");

            try
            {
                if (File.Exists(bdtPath))
                {
                    var bxf = BXF3.Read(bhdPath, bdtPath);

                    if (bxf.Files[v_path[0]].Name.EndsWith(".tpf.dcx") || bxf.Files[v_path[0]].Name.EndsWith(".tpf") || IsTpfData(DCX.Decompress(bxf.Files[v_path[0]].Bytes)))
                    {
                        ReadNestedTPF(bxf.Files[v_path[0]], v_path, 0);
                    }

                    if (_writer)
                    {
                        bxf.Write(bhdPath, bdtPath);
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex); }
        }

        private void ReadNestedBnd(BinderFile bndData, int[] v_path, int index) //Bnd inside archive with virtual path
        {
            if (index < v_path.Length)
            {
                try
                {
                    var bnd = BND3.Read(bndData.Bytes);
                    if (_getobject & index == v_path.Length - 1)
                    {
                        _main_object = bnd;
                    }

                    {
                        index++;
                        if (index < v_path.Length)
                        {
                            if (IsBndData(bnd.Files[v_path[index]].Bytes))
                            {
                                ReadNestedBnd(bnd.Files[v_path[index]], v_path, index);
                            }
                            else if (IsTpfData(bnd.Files[v_path[index]].Bytes))
                            {
                                ReadNestedTPF(bnd.Files[v_path[index]], v_path, index);
                            }
                            else if (IsFlvData(bnd.Files[v_path[index]].Bytes))
                            {
                                ReadNestedFlver(bnd.Files[v_path[index]], v_path, index);
                            }
                        }
                        index--;
                    }

                    if (_replacer & index == v_path.Length - 1)
                    {
                        Console.WriteLine($"Replace: {bndData.Name}");
                        bnd = BND3.Read(_new_bytes);
                    }

                    if (_writer)
                    {
                        bndData.Bytes = bnd.Write();
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex); }
            }

        }

        private void ReadNestedBXF(BinderFile bndData, string bndPath, int[] v_path, int index)
        {
            if (index < v_path.Length)
            {
                string bdtPath = bndPath.Replace(".chrbnd.dcx", ".chrtpfbdt");
                if (File.Exists(bdtPath))
                {
                    try
                    {
                        BXF3 nestedBXF = BXF3.Read(bndData.Bytes, bdtPath);
                        {
                            index++;
                            if (index < v_path.Length)
                            {
                                if (nestedBXF.Files[v_path[index]].Name.EndsWith(".tpf.dcx") || nestedBXF.Files[v_path[index]].Name.EndsWith(".tpf") || IsTpfData(DCX.Decompress(nestedBXF.Files[v_path[index]].Bytes)))
                                {
                                    ReadNestedTPF(nestedBXF.Files[v_path[index]], v_path, index);
                                }
                            }
                            index--;
                        }

                        if (_writer)
                        {
                            byte[] bhdData = [];
                            byte[] bdtData = [];
                            nestedBXF.Write(out bhdData, out bdtData);
                            bndData.Bytes = bhdData;
                            File.WriteAllBytes(bdtPath, bdtData);
                        }
                    }
                    catch (Exception ex) { Console.WriteLine(ex); }
                }
            }
        }

        private void ReadNestedFlver(BinderFile bndData, int[] v_path, int index)
        {
            if (index < v_path.Length)
            {
                FLVER2 flver = new();
                try
                {
                    flver = FLVER2.Read(bndData.Bytes);
                    Console.WriteLine($"Nested flver read: {bndData.Name}");
                }
                catch (Exception ex) { Console.WriteLine(ex); }

                if (_getobject & index == v_path.Length - 1)
                {
                    _main_object = flver;
                }

                if (_flv_replace & index == v_path.Length - 1)
                {
                    flver = _new_flver;
                }

                if (_flv_writer & index == v_path.Length - 1)
                {
                    byte[] bak = bndData.Bytes;
                    try
                    {
                        Console.WriteLine($"Nested flver write: {bndData.Name}");
                        bndData.Bytes = flver.Write();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        Console.WriteLine("Return flver backup");
                        bndData.Bytes = bak;
                    }
                }
            }
        }

        private void ReadNestedTPF(BinderFile bndData, int[] v_path, int index)
        {
            if (index < v_path.Length)
            {
                try
                {
                    var tpf = TPF.Read(bndData.Bytes);

                    if (_getobject & index == v_path.Length - 1)
                    {
                        _main_object = tpf;
                    }

                    {
                        index++;
                        if (index < v_path.Length)
                        {
                            if (IsDdsData(tpf.Textures[v_path[index]].Bytes))
                            {
                                ReadNestedDDS(tpf.Textures[v_path[index]], v_path, index);
                            }

                            if (_dds_remove & index == v_path.Length - 1)
                            {
                                tpf.Textures.Remove(tpf.Textures[v_path[index]]);
                                Console.WriteLine($"Removed: {bndData.Name}");
                            }
                        }
                        index--;
                    }

                    if (_dds_add & index == v_path.Length - 1)
                    {
                        tpf.Textures.Add(new TPF.Texture());
                        tpf.Textures.Last().Name = "New_texture";
                        tpf.Textures.Last().Platform = TPF.TPFPlatform.PC;
                        tpf.Textures.Last().Bytes = DDSTools.fatcat;
                    }

                    if (_replacer & index == v_path.Length - 1)
                    {
                        tpf = TPF.Read(_new_bytes);
                    }

                    if (_writer)
                    {
                        bndData.Bytes = tpf.Write();
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex); }
            }
        }

        private void ReadNestedDDS(TPF.Texture tpfData, int[] v_path, int index)
        {
            if (index < v_path.Length)
            {
                try
                {
                    TPF.Texture dds = tpfData;

                    if (_getobject & index == v_path.Length - 1)
                    {
                        _main_object = dds;
                    }

                    Console.WriteLine($"Read nested texture: {dds.Name}");

                    if (_dds_replace_bytes)
                    {
                        dds.Bytes = _dds_new_bytes;
                    }

                    if (_dds_replace_flag)
                    {
                        dds.Format = _dds_new_flag;
                    }

                    if (_dds_replace_name)
                    {
                        dds.Name = _dds_new_name;
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex); }
            }
        }

        private bool IsValidFile(string path)
        {
            HashSet<string> _fileExtensions = new() { ".partsbnd", ".tpf", ".flver", ".tpfbhd", ".chrbnd", ".ffxbnd", ".rumblebnd", ".objbnd", ".fgbnd", ".msgbnd", ".remobnd", ".shaderbnd" };
            if (_fileExtensions.Any(name => path.EndsWith(name) | path.EndsWith(name + ".dcx")))
                return true;
            return false;
        }

        private bool IsBndFile(string path)
        {
            HashSet<string> _fileExtensions = new() { ".partsbnd", ".chrbnd", ".ffxbnd", ".rumblebnd", ".objbnd", ".fgbnd", ".msgbnd", ".remobnd", ".shaderbnd" };
            if (_fileExtensions.Any(name => path.EndsWith(name) | path.EndsWith(name + ".dcx")))
                return true;
            return false;
        }

        private bool IsBxfFile(string path)
        {
            HashSet<string> _fileExtensions = new() { ".tpfbhd" };
            if (_fileExtensions.Any(name => path.EndsWith(name) | path.EndsWith(name + ".dcx")))
                return true;
            return false;
        }

        private bool IsTpfFile(string path)
        {
            HashSet<string> _fileExtensions = new() { ".tpf" };
            if (_fileExtensions.Any(name => path.EndsWith(name) | path.EndsWith(name + ".dcx")))
                return true;
            return false;
        }

        private bool IsFlverFile(string path)
        {
            HashSet<string> _fileExtensions = new() { ".flver" };
            if (_fileExtensions.Any(name => path.EndsWith(name) | path.EndsWith(name + ".dcx")))
                return true;
            return false;
        }

        private bool IsBndData(byte[] data)
        {
            if (data.Length < 4) return false;
            return data[0] == 'B' && data[1] == 'N' && data[2] == 'D' && data[3] == '3';
        }

        private bool IsTpfData(byte[] data)
        {
            if (data.Length < 3) return false;
            return data[0] == 'T' && data[1] == 'P' && data[2] == 'F';
        }

        private bool IsBxfData(byte[] data)
        {
            if (data.Length < 4) return false;
            return data[0] == 'B' && data[1] == 'H' && data[2] == 'F' && data[3] == '3';
        }

        private bool IsFlvData(byte[] data)
        {
            if (data.Length < 4) return false;
            return data[0] == 'F' && data[1] == 'L' && data[2] == 'V' && data[3] == 'E' && data[4] == 'R';
        }

        private bool IsDdsData(byte[] data)
        {
            if (data.Length < 3) return false;
            return data[0] == 'D' && data[1] == 'D' && data[2] == 'S';
        }

        public void Extract(string path, string filename)
        {
            switch (_main_object)
            {
                case BND3:
                    {
                        Console.WriteLine($"BND - extract");
                        BND3 bnd = (BND3)_main_object;
                        bnd.Write(Path.Combine(path, filename.Split("\\").Last()));
                        Console.WriteLine($"BND - extract done");
                        break;
                    }

                case TPF:
                    {
                        Console.WriteLine($"TPF - extract");
                        TPF tpf = (TPF)_main_object;
                        tpf.Write(Path.Combine(path, filename.Split("\\").Last()));
                        Console.WriteLine($"TPF - extract done");
                        break;
                    }

                case TPF.Texture:
                    {
                        Console.WriteLine($"DDS - extract");
                        TPF.Texture dds = (TPF.Texture)_main_object;
                        File.WriteAllBytes(Path.Combine(path, filename.Split("\\").Last() + ".dds"), dds.Bytes);
                        Console.WriteLine($"DDS - extract done");
                        break;
                    }

                case FLVER2:
                    {
                        Console.WriteLine("FLVER - extract");
                        FLVER2 flv = (FLVER2)_main_object;
                        try
                        {
                            flv.Write(Path.Combine(path, filename.Split("\\").Last()));
                            Console.WriteLine("FLVER - extract done");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                            Console.WriteLine("Broken flver");
                        }
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }

        public void Extract(string path, string virtualPath, string filename)
        {
            switch (_main_object)
            {
                case BND3:
                    {
                        Console.WriteLine($"BND - extract");
                        BND3 bnd = (BND3)_main_object;
                        bnd.Write(Path.Combine(path, filename.Split("\\").Last()));
                        Console.WriteLine($"BND - extract done");
                        break;
                    }

                case TPF:
                    {
                        Console.WriteLine($"TPF - extract");
                        TPF tpf = (TPF)_main_object;
                        tpf.Write(Path.Combine(path, filename.Split("\\").Last()));
                        Console.WriteLine($"TPF - extract done");
                        break;
                    }

                case TPF.Texture:
                    {
                        Console.WriteLine($"DDS - extract");
                        TPF.Texture dds = (TPF.Texture)_main_object;
                        File.WriteAllBytes(Path.Combine(path, virtualPath.Replace("\\", "#").Replace("|", "~") + ";" + filename.Split("\\").Last() + ".dds"), dds.Bytes);
                        Console.WriteLine($"DDS - extract done");
                        break;
                    }

                case FLVER2:
                    {
                        Console.WriteLine("FLVER - extract");
                        FLVER2 flv = (FLVER2)_main_object;
                        try
                        {
                            flv.Write(Path.Combine(path, filename.Split("\\").Last()));
                            Console.WriteLine("FLVER - extract done");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                            Console.WriteLine("Broken flver");
                        }
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }
    }
}
