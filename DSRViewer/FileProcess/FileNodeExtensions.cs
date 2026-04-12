namespace DSRViewer.FileProcess
{
    /// <summary>Методы расширения для рекурсивного обхода дерева FileNode.</summary>
    public static class FileNodeExtensions
    {
        /// <summary>Рекурсивно находит все узлы, удовлетворяющие предикату.</summary>
        public static List<FileNode> FindAll(this FileNode root, Func<FileNode, bool> predicate)
        {
            var result = new List<FileNode>();
            Traverse(root, predicate, result);
            return result;
        }

        /// <summary>Рекурсивно находит первый узел, удовлетворяющий предикату.</summary>
        public static FileNode? FindFirst(this FileNode root, Func<FileNode, bool> predicate)
        {
            root.EnsureLoaded();
            foreach (var child in root.Children)
            {
                if (predicate(child)) return child;
                child.EnsureLoaded();
                var found = child.FindFirst(predicate);
                if (found != null) return found;
            }
            return null;
        }

        private static void Traverse(FileNode node, Func<FileNode, bool> predicate, List<FileNode> result)
        {
            node.EnsureLoaded();
            foreach (var child in node.Children)
            {
                if (predicate(child))
                    result.Add(child);
                Traverse(child, predicate, result);
            }
        }
    }
}
