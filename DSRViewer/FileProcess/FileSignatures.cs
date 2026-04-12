namespace DSRViewer.FileProcess
{
    /// <summary>Определяет типы файлов по расширению и сигнатуре байт.</summary>
    public static class FileSignatures
    {
        private static readonly string[] BndExtensions =
        [
            ".chrbnd", ".partsbnd", ".ffxbnd", ".rumblebnd", ".objbnd",
            ".fgbnd", ".msgbnd", ".mtdbnd", ".anibnd", ".chresdbnd",
            ".remobnd", ".shaderbnd", ".parambnd"
        ];

        /// <summary>Проверяет, является ли файл BND-архивом по расширению.</summary>
        public static bool IsBnd(string path)   => HasExtension(path, BndExtensions);
        /// <summary>Проверяет, является ли файл TPF-архивом по расширению.</summary>
        public static bool IsTpf(string path)   => HasExtension(path, ".tpf");
        /// <summary>Проверяет, является ли файл FLVER-моделью по расширению.</summary>
        public static bool IsFlver(string path) => HasExtension(path, ".flver");
        /// <summary>Проверяет, является ли файл BXF-архивом по расширению.</summary>
        public static bool IsBxf(string path)   => HasExtension(path, ".tpfbhd");

        /// <summary>Проверяет, является ли файл поддерживаемым игровым форматом.</summary>
        public static bool IsValidGameFile(string path) =>
            IsBnd(path) || IsTpf(path) || IsFlver(path) || IsBxf(path);

        /// <summary>Проверяет сигнатуру байт BND3.</summary>
        public static bool IsBndData(byte[] b)  => b.Length >= 4 && b[0] == 'B' && b[1] == 'N' && b[2] == 'D' && b[3] == '3';
        /// <summary>Проверяет сигнатуру байт TPF.</summary>
        public static bool IsTpfData(byte[] b)  => b.Length >= 3 && b[0] == 'T' && b[1] == 'P' && b[2] == 'F';
        /// <summary>Проверяет сигнатуру байт BXF3.</summary>
        public static bool IsBxfData(byte[] b)  => b.Length >= 4 && b[0] == 'B' && b[1] == 'H' && b[2] == 'F' && b[3] == '3';
        /// <summary>Проверяет сигнатуру байт FLVER.</summary>
        public static bool IsFlvData(byte[] b)  => b.Length >= 5 && b[0] == 'F' && b[1] == 'L' && b[2] == 'V' && b[3] == 'E' && b[4] == 'R';
        /// <summary>Проверяет сигнатуру байт DCX.</summary>
        public static bool IsDcxData(byte[] b)  => b.Length >= 3 && b[0] == 'D' && b[1] == 'C' && b[2] == 'X';
        /// <summary>Проверяет сигнатуру байт DDS.</summary>
        public static bool IsDdsData(byte[] b)  => b.Length >= 3 && b[0] == 'D' && b[1] == 'D' && b[2] == 'S';

        private static bool HasExtension(string path, params string[] extensions)
        {
            var lower = path.ToLowerInvariant();
            return extensions.Any(ext =>
                lower.EndsWith(ext) || lower.EndsWith(ext + ".dcx"));
        }
    }
}
