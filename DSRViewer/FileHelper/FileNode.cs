using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSRViewer.FileHelper
{
    public class FileNode
    {
        public int ID { get; set; }
        public string Name { get; set; } = "";
        public string ShortName { get; set; } = "";
        public string VirtualPath { get; set; } = "";
        public string ShortVirtualPath { get; set; } = "";
        public bool IsFolder { get; set; } = false;
        public bool IsBndArchive { get; set; }
        public bool IsNestedBndArchive { get; set; }
        public bool IsTpfArchive { get; set; }
        public bool IsNestedTpfArchive { get; set; }
        public bool IsBxfArchive { get; set; }
        public bool IsNestedBxfArchive { get; set; }
        public bool IsFlver { get; set; }
        public bool IsNestedFlver { get; set; }
        public bool IsDDS { get; set; }
        public bool IsNestedDDS { get; set; }
        public int DDSFormatFlag { get; set; }
        public string DDSFormat { get; set; } = "";
        public int ArchiveDepth { get; set; }
        public int Size { get; set; }
        public string Parent { get; set; } = "";
        public List<FileNode> Children { get; set; } = [];
    }
}
