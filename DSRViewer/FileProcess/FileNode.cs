using System;
using System.Collections.Generic;

namespace DSRViewer.FileProcess
{
    /// <summary>Узел файлового дерева, представляющий файл или архив в виртуальной иерархии.</summary>
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

        /// <summary>
        /// Lazy loading: true — содержимое уже загружено, false — ещё нет.
        /// Для папок и файлов без вложений всегда true.
        /// </summary>
        public bool IsLoaded { get; set; } = true;

        /// <summary>
        /// Делегат для ленивой загрузки содержимого.
        /// Устанавливается построителем дерева в режиме Lazy.
        /// </summary>
        public Action? LoadChildren { get; set; }

        /// <summary>
        /// Загружает содержимое если ещё не загружено.
        /// </summary>
        public void EnsureLoaded()
        {
            if (IsLoaded) return;
            try
            {
                LoadChildren?.Invoke();
                IsLoaded = true;
            }
            catch
            {
                // Не помечаем IsLoaded = true при ошибке — следующий вызов повторит попытку
                throw;
            }
        }

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

        /// <summary>Возвращает строковое представление типа узла.</summary>
        public string GetNodeType() => Type.ToString();
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
