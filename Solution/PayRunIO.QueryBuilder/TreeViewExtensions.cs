namespace PayRunIO.QueryBuilder
{
    using System.Windows.Controls;

    public static class TreeViewExtensions
    {
        public static TreeViewItem ContainerFromItemRecursive(this ItemContainerGenerator root, object item)
        {
            if (root.ContainerFromItem(item) is TreeViewItem treeViewItem)
            {
                return treeViewItem;
            }

            foreach (var subItem in root.Items)
            {
                treeViewItem = root.ContainerFromItem(subItem) as TreeViewItem;

                var search = treeViewItem?.ItemContainerGenerator.ContainerFromItemRecursive(item);

                if (search != null)
                {
                    return search;
                }
            }

            return null;
        }
    }
}
