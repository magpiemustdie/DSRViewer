using System;
using System.IO;
using System.Linq;
using DSRViewer.FileHelper.DDSHelper;
using SoulsFormats;

namespace DSRViewer.FileHelper
{
    public class FileBinders
    {
        private object? _mainObject;
        private bool _get, _write, _replace;
        private byte[] _newBytes = [];
        private bool _flvWrite, _flvReplace;
        private FLVER2 _newFlv = new();
        private bool _ddsAdd, _ddsRemove, _ddsReplace;
        private byte[] _ddsBytes = [];
        private DDS _newDDS = new();
        private byte _ddsFlag;
        private string _ddsName = "";

        // Общая конфигурация
        public void SetCommon(bool get = false, bool write = false, bool replace = false, byte[]? newBytes = null)
        {
            _get = get;
            _write = write;
            _replace = replace;
            if (newBytes != null) _newBytes = newBytes;
        }

        // Конфигурация FLVER
        public void SetFlver(bool write = false, bool replace = false, FLVER2? newFlv = null)
        {
            _flvWrite = write;
            _flvReplace = replace;
            if (newFlv != null) _newFlv = newFlv;
        }

        // Конфигурация DDS/TPF
        public void SetDds(bool add = false, bool remove = false, bool replace = false, byte flag = 0, string name = "", byte[]? bytes = null)
        {
            _ddsAdd = add;
            _ddsRemove = remove;
            _ddsReplace = replace;
            if (bytes != null) _ddsBytes = bytes;
            _ddsFlag = flag;
            _ddsName = name;
        }

        // Быстрые методы для часто используемых операций
        /*
        public void SetGetObjectOnly() => _get = true;
        public void SetWriteOnly() => _write = true;
        public void SetReplaceOnly(byte[] newBytes) => (_replace, _newBytes) = (true, newBytes);
        public void SetFlverReplace(FLVER2 newFlv) => (_flvReplace, _newFlv) = (true, newFlv);
        */
        public void SetGetObjectOnly() => _get = true;
        public object? GetObject() => _mainObject;
        public void Clear() => _mainObject = null;

        public void Read(string vPath)
        {
            var parts = vPath.Split('|');
            var path = parts[0];
            var idxs = parts.Skip(1).Where(s => int.TryParse(s, out _)).Select(int.Parse).ToArray();

            if (IsBnd(path)) ReadBnd(path, idxs);
            else if (IsTpf(path)) ReadTpf(path, idxs);
            else if (IsFlver(path)) ReadFlv(path);
            else if (IsBxf(path)) ReadBxf(path, idxs);
        }

        private void ReadBnd(string path, int[] idxs)
        {
            try
            {
                var bnd = BND3.Read(path);
                if (_get && idxs.Length == 0) _mainObject = bnd;
                if (idxs.Length > 0) Process(bnd.Files[idxs[0]], path, idxs, 0);
                if (_replace && idxs.Length == 0) bnd = BND3.Read(_newBytes);
                if (_write) bnd.Write(path);
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        private void ReadTpf(string path, int[] idxs)
        {
            try
            {
                var tpf = TPF.Read(path);
                if (_get && idxs.Length == 0) _mainObject = tpf;
                if (idxs.Length > 0) ProcessTex(tpf, idxs);
                if (_ddsAdd && idxs.Length == 0) AddTex(tpf);
                if (_replace && idxs.Length == 0) tpf = TPF.Read(_newBytes);
                if (_write) tpf.Write(path);
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        private void ReadFlv(string path)
        {
            try
            {
                var flv = FLVER2.Read(path);
                if (_get) _mainObject = flv;
                if (_flvReplace) flv = _newFlv;
                if (_flvWrite) WriteSafe(flv, path);
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        private void ReadBxf(string bhd, int[] idxs)
        {
            var bdt = bhd.Replace(".tpfbhd", ".tpfbdt", StringComparison.OrdinalIgnoreCase);

            if (!File.Exists(bdt)) return;

            try
            {
                var bxf = BXF3.Read(bhd, bdt);
                if (idxs.Length > 0 && IsTpfData(DCX.Decompress(bxf.Files[idxs[0]].Bytes)))
                    Process(bxf.Files[idxs[0]], bdt, idxs, 0);
                if (_write) bxf.Write(bhd, bdt);
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        private void Process(BinderFile f, string p, int[] i, int idx)
        {
            if (idx >= i.Length) return;

            if (IsBndData(f.Bytes)) ProcessBnd(f, i, idx);
            else if (IsTpfData(f.Bytes)) ProcessTpf(f, i, idx);
            else if (IsBxfData(f.Bytes)) ProcessBxf(f, p, i, idx);
            else if (IsFlvData(f.Bytes)) ProcessFlv(f, i, idx); 
            else if (IsDcxData(f.Bytes)) ProcessTpf(f, i, idx);
        }

        private void ProcessTex(TPF t, int[] i)
        {
            Console.WriteLine("Process tpf");

            if (i.Length == 0) return;
            var tex = t.Textures[i[0]];

            if (_get && i.Length == 1) _mainObject = tex;
            if (_ddsReplace && i.Length == 1)
            {
                tex.Bytes = _ddsBytes;
                tex.Format = _ddsFlag;
            }
            if (_ddsRemove && i.Length == 1) t.Textures.RemoveAt(i[0]);

            //if (i.Length > 1) ProcessTex(t, i.Skip(1).ToArray());
        }

        private void ProcessBnd(BinderFile f, int[] i, int idx)
        {
            var bnd = BND3.Read(f.Bytes);
            if (_get && idx == i.Length - 1) _mainObject = bnd;

            if (idx + 1 < i.Length) Process(bnd.Files[i[idx + 1]], "", i, idx + 1);

            if (_replace && idx == i.Length - 1) bnd = BND3.Read(_newBytes);
            if (_write || (_replace && idx == i.Length - 1)) f.Bytes = bnd.Write();
        }

        private void ProcessTpf(BinderFile f, int[] i, int idx)
        {
            var tpf = TPF.Read(f.Bytes);
            if (_get && idx == i.Length - 1) _mainObject = tpf;

            if (idx + 1 < i.Length) ProcessTex(tpf, i.Skip(idx + 1).ToArray());

            if (_ddsAdd && idx == i.Length - 1) AddTex(tpf);
            if (_replace && idx == i.Length - 1) tpf = TPF.Read(_newBytes);
            if (_write || (_replace && idx == i.Length - 1)) f.Bytes = tpf.Write();
        }

        private void ProcessFlv(BinderFile f, int[] i, int idx)
        {
            var flv = FLVER2.Read(f.Bytes);
            if (_get && idx == i.Length - 1) _mainObject = flv;
            if (_flvReplace && idx == i.Length - 1) flv = _newFlv;
            if (_flvWrite && idx == i.Length - 1) f.Bytes = WriteSafe(flv, f.Name, f.Bytes);
        }

        private void ProcessBxf(BinderFile f, string p, int[] i, int idx)
        {
            Console.WriteLine("Process bxf");
            var bdt = p.Replace(".chrbnd.dcx", ".chrtpfbdt", StringComparison.OrdinalIgnoreCase);
            if (!File.Exists(bdt)) return;

            var bxf = BXF3.Read(f.Bytes, bdt);
            if (idx + 1 < i.Length) Process(bxf.Files[i[idx + 1]], bdt, i, idx + 1);

            if (_write)
            {
                bxf.Write(out var bhd, out var bdtData);
                f.Bytes = bhd;
                File.WriteAllBytes(bdt, bdtData);
            }
        }

        private static void AddTex(TPF t)
        {
            t.Textures.Add(new TPF.Texture
            { Name = "New", Platform = TPF.TPFPlatform.PC, Bytes = DDSTools.fatcat });
            Console.WriteLine("New texture added!");
        }

        private static byte[] WriteSafe(FLVER2 f, string n, byte[] b)
        {
            try { return f.Write(); }
            catch { return b; }
        }

        private static void WriteSafe(FLVER2 f, string p)
        {
            var b = File.ReadAllBytes(p);
            try { f.Write(p); }
            catch { File.WriteAllBytes(p, b); }
        }

        // Вспомогательные методы для проверки типов файлов с учетом DCX
        private static bool IsBnd(string p) => HasExtension(p,
            ".chrbnd", ".partsbnd", ".ffxbnd", ".rumblebnd", ".objbnd", ".fgbnd", ".msgbnd", ".mtdbnd", ".anibnd", ".chresdbnd", ".remobnd", ".shaderbnd", ".parambnd");

        private static bool IsTpf(string p) => HasExtension(p, ".tpf");
        private static bool IsFlver(string p) => HasExtension(p, ".flver");
        private static bool IsBxf(string p) => HasExtension(p, ".tpfbhd");

        private static bool HasExtension(string path, params string[] extensions)
        {
            var pathLower = path.ToLowerInvariant();
            return extensions.Any(ext =>
                pathLower.EndsWith(ext.ToLowerInvariant()) ||
                pathLower.EndsWith(ext.ToLowerInvariant() + ".dcx"));
        }

        private static bool IsBndData(byte[] b) => b.Length >= 4 && b[0] == 'B' && b[1] == 'N' && b[2] == 'D' && b[3] == '3';
        private static bool IsTpfData(byte[] b) => b.Length >= 3 && b[0] == 'T' && b[1] == 'P' && b[2] == 'F';
        private static bool IsBxfData(byte[] b) => b.Length >= 4 && b[0] == 'B' && b[1] == 'H' && b[2] == 'F' && b[3] == '3';
        private static bool IsFlvData(byte[] b) => b.Length >= 5 && b[0] == 'F' && b[1] == 'L' && b[2] == 'V' && b[3] == 'E' && b[4] == 'R';
        private static bool IsDcxData(byte[] b) => b.Length >= 3 && b[0] == 'D' && b[1] == 'C' && b[2] == 'X';

        public void Extract(string dir, string name, string? vPath = null)
        {
            var p = Path.Combine(dir, Path.GetFileName(name));
            switch (_mainObject)
            {
                case BND3 b: b.Write(p); break;
                case TPF t: t.Write(p); break;
                case TPF.Texture d:
                    var n = vPath != null ? $"{vPath.Replace("\\", "#").Replace("|", "~")};{Path.GetFileName(name)}.dds" : p + ".dds";
                    File.WriteAllBytes(Path.Combine(dir, n), d.Bytes); break;
                case FLVER2 f: f.Write(p); break;
            }
        }
    }
}