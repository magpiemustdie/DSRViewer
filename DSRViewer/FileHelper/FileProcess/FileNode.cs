using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSRViewer.FileHelper
{
    public class FileNodeOld
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
        public List<FileNodeOld> Children { get; set; } = [];

        public string gettype() => this switch
        {
            _ when IsFolder => "Folder",
            _ when IsNestedBndArchive => "Nested BND Archive",
            _ when IsBndArchive => "BND Archive",
            _ when IsNestedTpfArchive => "Nested TPF Archive",
            _ when IsTpfArchive => "TPF Archive",
            _ when IsBxfArchive && IsNestedBxfArchive => "Nested BXF Archive",
            _ when IsBxfArchive => "BXF Archive",
            _ when IsFlver && IsNestedFlver => "Nested FLVER",
            _ when IsFlver => "FLVER",
            _ when IsDDS && IsNestedDDS => "Nested DDS",
            _ when IsDDS => "DDS",
            _ => "Unknown"
        };
    }

    public class FileNode
    {
        public int ID { get; set; }
        public string Name { get; set; } = "";
        public string ShortName { get; set; } = "";
        public string VirtualPath { get; set; } = "";
        public string ShortVirtualPath { get; set; } = "";

        public NodeType Type { get; set; } = NodeType.Unknown;

        public int DDSFormatFlag { get; set; }
        public string DDSFormat { get; set; } = "";

        public int ArchiveDepth { get; set; }
        public int Size { get; set; }
        public string Parent { get; set; } = "";
        public List<FileNode> Children { get; set; } = [];

        public bool IsFolder => Type == NodeType.Folder;
        public bool IsBndArchive => Type == NodeType.BndArchive || Type == NodeType.NestedBndArchive;
        public bool IsNestedBndArchive => Type == NodeType.NestedBndArchive;
        public bool IsTpfArchive => Type == NodeType.TpfArchive || Type == NodeType.NestedTpfArchive;
        public bool IsNestedTpfArchive => Type == NodeType.NestedTpfArchive;
        public bool IsBxfArchive => Type == NodeType.BxfArchive || Type == NodeType.NestedBxfArchive;
        public bool IsNestedBxfArchive => Type == NodeType.NestedBxfArchive;
        public bool IsFlver => Type == NodeType.Flver || Type == NodeType.NestedFlver;
        public bool IsNestedFlver => Type == NodeType.NestedFlver;
        public bool IsDDS => Type == NodeType.Dds || Type == NodeType.NestedDds;
        public bool IsNestedDDS => Type == NodeType.NestedDds;

        // --- Метод gettype (теперь просто отдаёт имя типа) ---
        public string GetType() => Type.ToString();
    }

    public enum NodeType
    {
        Unknown,
        Folder,
        BndArchive,
        NestedBndArchive,
        TpfArchive,
        NestedTpfArchive,
        BxfArchive,
        NestedBxfArchive,
        Flver,
        NestedFlver,
        Dds,
        NestedDds
    }
}
