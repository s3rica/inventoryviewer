using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Aga.Controls.Tree;
using libsecondlife;
using System.Collections;
using Aga.Controls.Tree.NodeControls;
using System.Collections.ObjectModel;

namespace InventoryViewer
{
    public class InventoryDirectorySelector
    {
        // Fields
        private Form form = new Form();
        private InventoryTreeView treeView;

        // Methods
        public InventoryDirectorySelector(InventoryManager manager, Inventory inventory, InventoryNode root)
        {
            this.form.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.treeView = new InventoryTreeView();
            this.treeView.Model = new InventoryTreeModel(manager, inventory, root, new InventoryType[0]);
            this.treeView.SelectionMode = TreeSelectionMode.Single;
            this.form.Controls.Add(this.treeView);
            Button ok = new Button();
            ok.Text = "Select";
            this.form.AcceptButton = ok;
            this.form.Controls.Add(ok);
            Button cancel = new Button();
            cancel.Text = "Cancel";
            this.form.CancelButton = cancel;
            this.form.Controls.Add(cancel);
        }

        public InventoryFolder ShowSelector()
        {
            if (this.form.ShowDialog() == DialogResult.OK)
            {
                return (this.treeView.SelectedInventory[0] as InventoryFolder);
            }
            return null;
        }
    }

    public class InventoryTreeModel : ITreeModel
    {
        // Fields
        private Dictionary<InventoryType, object> DisplayedTypes;
        private Inventory Inventory;
        private InventoryNode InventoryRoot;
        private InventoryManager Manager;
        private Dictionary<InventoryNode, Node> ReverseMap;
        private Dictionary<InventoryFolder, object> DownloadedFolders;

        // Events
        public event EventHandler<TreeModelEventArgs> NodesChanged;

        public event EventHandler<TreeModelEventArgs> NodesInserted;

        public event EventHandler<TreeModelEventArgs> NodesRemoved;

        public event EventHandler<TreePathEventArgs> StructureChanged;
        // Methods
        public InventoryTreeModel(InventoryManager manager, Inventory inventory, InventoryNode root)
            : this(manager, inventory, root, (InventoryType[])Enum.GetValues(typeof(InventoryType)))
        {
        }

        public InventoryTreeModel(InventoryManager manager, Inventory inventory, InventoryNode root, InventoryType[] displayedTypes)
        {
            this.DisplayedTypes = new Dictionary<InventoryType, object>();
            this.DownloadedFolders = new Dictionary<InventoryFolder, object>();
            this.Manager = manager;
            this.InventoryRoot = root;
            this.Inventory = inventory;
            this.Inventory.OnInventoryRemoved += new Inventory.InventoryRemoved(this.Inventory_OnInventoryRemoved);
            inventory.OnInventoryUpdated += new Inventory.InventoryUpdated(this.inventory_OnInventoryUpdated);
            this.Inventory.OnInventoryAdded += new Inventory.InventoryAdded(this.Inventory_OnInventoryAdded);
            //this.NodeMap = new Dictionary<Node, InventoryNode>();
            this.ReverseMap = new Dictionary<InventoryNode, Node>();
            Node rootNode = new Node("My Inventory");
            rootNode.Tag = this.InventoryRoot.Data;
            //this.NodeMap.Add(rootNode, this.InventoryRoot);
            this.ReverseMap.Add(this.InventoryRoot, rootNode);
            this.DisplayedTypes = new Dictionary<InventoryType, object>(displayedTypes.Length);
            foreach (InventoryType type in displayedTypes)
            {
                this.DisplayedTypes[type] = null;
            }
        }

        public IEnumerable GetChildren(TreePath treePath)
        {
            List<Node> children = new List<Node>();
            if (treePath.IsEmpty())
            {
                children.Add(this.ReverseMap[this.InventoryRoot]);
                return children;
            }
            Node lastNode = treePath.LastNode as Node;
            InventoryBase invBase = lastNode.Tag as InventoryBase;
            InventoryNode invNode = Inventory.GetNodeFor(invBase.UUID);//this.NodeMap[treePath.LastNode as Node];
            if (invNode.Data is InventoryFolder)
            {
                if (!DownloadedFolders.ContainsKey(invNode.Data as InventoryFolder))
                {
                    Manager.BeginRequestFolderContents(invNode.Data.UUID, Inventory.Owner, true, true, true, InventorySortOrder.ByName, null, null);
                    //this.Manager.RequestFolderContents(invNode.Data.UUID, true, true, false, InventorySortOrder.ByName);
                    //DownloadedFolders.Add(invNode.Data as InventoryFolder, null);
                }
                foreach (InventoryNode child in invNode.Nodes.Values)
                {
                    if ((child.Data is InventoryItem) && !this.DisplayedTypes.ContainsKey((child.Data as InventoryItem).InventoryType))
                    {
                        continue;
                    }
                    if (!this.ReverseMap.ContainsKey(child))
                    {
                        Node treeChild = new Node(child.Data.Name);
                        treeChild.Tag = child.Data;
                        this.ReverseMap.Add(child, treeChild);
                        //this.NodeMap.Add(treeChild, child);
                        Node parent = this.ReverseMap[child.Parent];
                        parent.Nodes.Add(treeChild);
                    }
                    children.Add(this.ReverseMap[child]);
                }
            }
            return children;
        }

        private TreePath GetPath(Node node)
        {
            if (node == null)
            {
                return TreePath.Empty;
            }
            Stack<object> stack = new Stack<object>();
            while (node != null)
            {
                stack.Push(node);
                node = node.Parent;
            }
            return new TreePath(stack.ToArray());
        }

        [Obsolete]
        private void InitializeModel()
        {
            //this.NodeMap = new Dictionary<Node, InventoryNode>();
            this.ReverseMap = new Dictionary<InventoryNode, Node>();
            Node rootNode = new Node("My Inventory");
            rootNode.Tag = this.InventoryRoot.Data;
            //this.NodeMap.Add(rootNode, this.InventoryRoot);
            this.ReverseMap.Add(this.InventoryRoot, rootNode);
            this.PopulateNode(this.InventoryRoot, rootNode);
        }

        private void Inventory_OnInventoryAdded(InventoryBase obj)
        {
            Node node = new Node(obj.Name);
            node.Parent = this.NodeForUUID(obj.ParentUUID);
            node.Tag = obj;
            InventoryNode invNode = Inventory.GetNodeFor(obj.UUID);
            this.ReverseMap.Add(invNode, node);
            //this.NodeMap.Add(node, invNode);
            Console.WriteLine("Added {0}", obj.Name);
            if (this.NodesInserted != null)
            {
                this.NodesInserted(this, new TreeModelEventArgs(this.GetPath(node.Parent), new int[] { 0 }, new object[] { node }));
            }
        }

        private void Inventory_OnInventoryRemoved(InventoryBase obj)
        {
            Node node = this.NodeForUUID(obj.UUID);
            if (node != null)
            {
                InventoryNode invNode = Inventory.GetNodeFor(obj.UUID);// this.NodeMap[node];
                //this.NodeMap.Remove(node);
                this.ReverseMap.Remove(invNode);
                if (this.NodesRemoved != null)
                {
                    this.NodesRemoved(this, new TreeModelEventArgs(this.GetPath(node.Parent), new object[] { node }));
                }
            }
        }

        private void inventory_OnInventoryUpdated(InventoryBase oldObject, InventoryBase newObject)
        {
            Node node = this.NodeForUUID(oldObject.UUID);
            if (node == null)
            {
                this.Inventory_OnInventoryAdded(newObject);
            }
            else
            {
                node.Text = newObject.Name;
                node.Tag = newObject;
                if (newObject.ParentUUID != oldObject.ParentUUID)
                {
                    this.Inventory_OnInventoryRemoved(oldObject);
                    this.Inventory_OnInventoryAdded(newObject);
                }
                else if (this.NodesChanged != null)
                {
                    this.NodesChanged(this, new TreeModelEventArgs(this.GetPath(node.Parent), new object[] { node }));
                }
            }
        }

        public bool IsLeaf(TreePath treePath)
        {
            return !(((Node)treePath.LastNode).Tag is InventoryFolder);
        }

        private Node NodeForUUID(LLUUID uuid)
        {
            InventoryNode invNode = Inventory.GetNodeFor(uuid);
            Node node;
            if (!ReverseMap.TryGetValue(invNode, out node))
            {
                node = new Node(invNode.Data.Name);
                node.Tag = invNode.Data;
                ReverseMap.Add(invNode, node);
            }
            return node;
        }

        [Obsolete]
        private void PopulateNode(InventoryNode invParent, Node treeParent)
        {
            foreach (InventoryNode invChild in invParent.Nodes.Values)
            {
                Node treeChild = new Node(invChild.Data.Name);
                treeChild.Tag = invChild.Data;
                //this.NodeMap.Add(treeChild, invChild);
                this.ReverseMap.Add(invChild, treeChild);
                treeParent.Nodes.Add(treeChild);
                if (invChild.Nodes.Count > 0)
                {
                    this.PopulateNode(invChild, treeChild);
                }
            }
        }
    }



    public class InventoryTreeView : TreeViewAdv
    {
        // Fields
        public ContextMenuStrip FolderContextMenu;
        public ContextMenuStrip GeneralContextMenu;
        public ContextMenuStrip ItemContextMenu;
        public InventoryBase[] SelectedInventory;

        // Events
        public event EventHandler SelectionChanged;

        // Methods
        public InventoryTreeView()
        {
            base.LoadOnDemand = true;
            base.SelectionMode = TreeSelectionMode.Multi;
            NodeTextBox textBox = new NodeTextBox();
            textBox.DataPropertyName = "Text";
            textBox.Parent = this;
            textBox.EditEnabled = false;
            base.NodeControls.Add(textBox);
            base.SelectionChanged += new EventHandler(this.InventoryTreeView_SelectionChanged);
        }

        private void InventoryTreeView_SelectionChanged(object sender, EventArgs e)
        {
            ITreeModel model = base.Model;
            this.SelectedInventory = new InventoryBase[base.SelectedNodes.Count];
            ReadOnlyCollection<TreeNodeAdv> selectedNodes = base.SelectedNodes;
            int index = 0;
            bool folders = false;
            bool items = false;
            foreach (TreeNodeAdv node in selectedNodes)
            {
                Node innerNode = node.Tag as Node;
                this.SelectedInventory[index] = innerNode.Tag as InventoryBase;
                folders |= this.SelectedInventory[index] is InventoryFolder;
                items |= this.SelectedInventory[index] is InventoryItem;
                index++;
            }
            if (this.SelectionChanged != null)
            {
                this.SelectionChanged(this, e);
            }
            if (folders && !items)
            {
                base.BeginInvoke(new ContextMenuStripFunc(this.SetContextMenuStrip), new object[] { this.FolderContextMenu });
            }
            else if (items && !folders)
            {
                base.BeginInvoke(new ContextMenuStripFunc(this.SetContextMenuStrip), new object[] { this.ItemContextMenu });
            }
            else
            {
                base.BeginInvoke(new ContextMenuStripFunc(this.SetContextMenuStrip), new object[] { this.GeneralContextMenu });
            }
        }

        public void SetContextMenuStrip(ContextMenuStrip strip)
        {
            this.ContextMenuStrip = strip;
        }

        // Nested Types
        public delegate void ContextMenuStripFunc(ContextMenuStrip bar);
    }



    public class InventoryViewer : ApplicationContext
    {
        // Fields
        private Dictionary<LLUUID, AssetTransferAction> AssetTransfers = new Dictionary<LLUUID, AssetTransferAction>();
        private SecondLife Client = new SecondLife();
        private InventoryTreeModel inventoryModel;
        private InventoryTreeView inventoryView;
        private Form mainWindow = new Form();
        private static InventoryViewer Viewer;

        // Methods
        public InventoryViewer()
        {
            this.Client.Self.OnInstantMessage += new MainAvatar.InstantMessageCallback(this.Self_OnInstantMessage);
            this.Client.Assets.OnAssetReceived += new AssetManager.AssetReceivedCallback(this.Assets_OnAssetReceived);
            ContextMenuStrip ItemMenu = new ContextMenuStrip();
            ItemMenu.Items.Add(new ToolStripMenuItem("Get Info", null, new EventHandler(this.getInfo_Click)));
            ItemMenu.Items.Add(new ToolStripMenuItem("Print Asset", null, new EventHandler(this.printAsset_Click)));
            ItemMenu.Items.Add(new ToolStripMenuItem("Save Asset...", null, new EventHandler(this.saveAsset_Click)));
            ItemMenu.Items.Add(new ToolStripMenuItem("Delete", null, new EventHandler(this.delete_Click)));
            ContextMenuStrip FolderMenu = new ContextMenuStrip();
            FolderMenu.Items.Add(new ToolStripMenuItem("Get Info", null, new EventHandler(this.getInfo_Click)));
            FolderMenu.Items.Add(new ToolStripMenuItem("Delete", null, new EventHandler(this.delete_Click)));
            FolderMenu.Items.Add(new ToolStripMenuItem("Empty", null, new EventHandler(this.empty_Click)));
            ContextMenuStrip GeneralMenu = new ContextMenuStrip();
            GeneralMenu.Items.Add(new ToolStripMenuItem("Get Info", null, new EventHandler(this.getInfo_Click)));
            GeneralMenu.Items.Add(new ToolStripMenuItem("Delete", null, new EventHandler(this.delete_Click)));
            this.inventoryView = new InventoryTreeView();
            this.inventoryView.GeneralContextMenu = GeneralMenu;
            this.inventoryView.FolderContextMenu = FolderMenu;
            this.inventoryView.ItemContextMenu = ItemMenu;
            this.inventoryView.Dock = DockStyle.Fill;
            this.mainWindow.Controls.Add(this.inventoryView);
            this.mainWindow.FormClosing += new FormClosingEventHandler(this.Form_FormClosing);
            this.mainWindow.Show();
        }

        private void Assets_OnAssetReceived(AssetDownload transfer, Asset asset)
        {
            AssetTransferAction action;
            if (this.AssetTransfers.TryGetValue(transfer.ID, out action))
            {
                if (asset != null)
                {
                    action(asset);
                }
                else
                {
                    Console.WriteLine("Asset transfer failed: {0}", transfer.Status);
                }
                this.AssetTransfers.Remove(transfer.ID);
            }
        }

        #region Context Menu Handlers
        private void delete_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Delete not implemented yet.");
        }

        private void empty_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Empty not implemented yet.");
        }

        private void getInfo_Click(object sender, EventArgs e)
        {
            foreach (InventoryBase inv in this.inventoryView.SelectedInventory)
            {
                Console.WriteLine("{0}: {1} ({2})", (inv is InventoryFolder) ? "Folder" : "Item", inv.Name, inv.UUID);
                Console.WriteLine("\tOwner: {0}", inv.OwnerID);
                Console.WriteLine("\tParent: {0}", inv.ParentUUID);
                if (inv is InventoryItem)
                {
                    InventoryItem item = inv as InventoryItem;
                    Console.WriteLine("\tAsset Type: {0}", item.AssetType);
                    Console.WriteLine("\tAsset UUID: {0}", item.AssetUUID);
                    Console.WriteLine("\tCreation date: {0}", item.CreationDate);
                    Console.WriteLine("\tCreator ID: {0}", item.CreatorID);
                    Console.WriteLine("\tDescription: {0}", item.Description);
                    Console.WriteLine("\tGroup ID: {0}", item.GroupID);
                    Console.WriteLine("\tGroup owned: {0}", item.GroupOwned);
                    Console.WriteLine("\tInventory Type: {0}", item.InventoryType);
                    Console.WriteLine("\tPermissions: {0}", item.Permissions);
                    Console.WriteLine("\tSale Type: {0}", item.SaleType);
                    Console.WriteLine("\tSale Price: {0}", item.SalePrice);
                }
                else if (inv is InventoryFolder)
                {
                    InventoryFolder folder = inv as InventoryFolder;
                    Console.WriteLine("\tDescendants: {0}", folder.DescendentCount);
                    Console.WriteLine("\tPrefered Type: {0}", folder.PreferredType);
                    Console.WriteLine("\tVersion: {0}", folder.Version);
                }
                else
                {
                    Console.WriteLine("Unknown inventory type {0}", inv.GetType());
                }
            }
        }

        private void printAsset_Click(object sender, EventArgs e)
        {
            foreach (InventoryBase inv in this.inventoryView.SelectedInventory)
            {
                if (inv is InventoryItem)
                {
                    InventoryItem item = inv as InventoryItem;
                    LLUUID transferID = this.Client.Assets.RequestInventoryAsset(item.AssetUUID, item.UUID, LLUUID.Zero, item.OwnerID, item.AssetType, false);
                    this.AssetTransfers.Add(transferID, new AssetTransferAction(this.PrintAsset));
                }
            }
        }

        private void saveAsset_Click(object sender, EventArgs e)
        {
            foreach (InventoryBase inv in this.inventoryView.SelectedInventory)
            {
                if (inv is InventoryItem)
                {
                    InventoryItem item = inv as InventoryItem;
                    LLUUID transferID = this.Client.Assets.RequestInventoryAsset(item.AssetUUID, item.UUID, LLUUID.Zero, item.OwnerID, item.AssetType, false);
                    this.AssetTransfers.Add(transferID, new AssetTransferAction(this.SaveAsset));
                }
            }
        }
        #endregion

        private delegate void AssetTransferAction(Asset asset);

        public void PrintAsset(Asset asset)
        {
            if (asset is AssetScriptText)
            {
                AssetScriptText script = asset as AssetScriptText;
                Console.WriteLine(script.Source);
            }
            else if (asset is AssetNotecard)
            {
                AssetNotecard note = asset as AssetNotecard;
                Console.WriteLine(note.Text);
            }
            else
            {
                Console.WriteLine("Unable to handle asset type {0}", asset.GetType());
            }
        }

        public void SaveAsset(Asset asset)
        {
            Console.WriteLine("SaveAsset not implemented yet!");
        }


        public void ShowInventory()
        {
            Console.WriteLine("Creating base model...");
            this.inventoryModel = new InventoryTreeModel(this.Client.Inventory, this.Client.Inventory.Store, this.Client.Inventory.Store.RootNode);
            this.inventoryView.Model = this.inventoryModel;
            Console.WriteLine("Done!");
        }

        private void Self_OnInstantMessage(LLUUID fromAgentID, string fromAgentName, LLUUID toAgentID, uint parentEstateID, LLUUID regionID, LLVector3 position, MainAvatar.InstantMessageDialog dialog, bool groupIM, LLUUID imSessionID, DateTime timestamp, string message, MainAvatar.InstantMessageOnline offline, byte[] binaryBucket, Simulator simulator)
        {
            Console.WriteLine("IM ({0}): {1}", fromAgentName, message);
        }

        public bool Login(string first, string last, string password, out string message)
        {
            bool success = this.Client.Network.Login(first, last, password, "InventoryViewer", "Christopher Omega");
            message = this.Client.Network.LoginMessage;
            return success;
        }

        public void Logout()
        {
            this.Client.Network.Logout();
            base.ExitThread();
        }

        private void Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Logout();
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Viewer.Logout();
        }

        private static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: InventoryViewer first last password");
            }
            else
            {
                string message;
                Console.CancelKeyPress += new ConsoleCancelEventHandler(InventoryViewer.Console_CancelKeyPress);
                Viewer = new InventoryViewer();
                if (Viewer.Login(args[0], args[1], args[2], out message))
                {
                    Viewer.ShowInventory();
                }
                else
                {
                    Console.WriteLine("Login failed: {0}", message);
                }
                Application.Run(Viewer);
            }
        }
    }
}