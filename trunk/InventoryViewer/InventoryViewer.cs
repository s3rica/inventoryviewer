using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Aga.Controls.Tree;
using Aga.Controls.Tree.NodeControls;
using libsecondlife;

namespace InventoryViewer
{
    public class InventoryContextMenu : ContextMenuStrip
    {
        private ToolStripMenuItem[] AllOptions;
        private ToolStripMenuItem[] ItemOptions;
        private ToolStripMenuItem[] FolderOptions;

        private List<InventoryObject> Inventory;
        private InventoryManager Manager;
        private AssetManager AssetManager;
        private SecondLife Client;

        private Dictionary<LLUUID, InventoryItem> AssetTransfers;
        public InventoryContextMenu(SecondLife client, InventoryManager manager, AssetManager assets, List<InventoryObject> inventory)
        {
            AllOptions = new ToolStripMenuItem[] {
                new ToolStripMenuItem("Info", null, new EventHandler(ItemInfo)),
                new ToolStripMenuItem("Delete", null, new EventHandler(DeleteItems)),
            };

            ItemOptions = new ToolStripMenuItem[] {
                new ToolStripMenuItem("Print Asset", null, new EventHandler(PrintAsset)),
            };

            FolderOptions = new ToolStripMenuItem[] {
            };

            this.Inventory = inventory;
            this.Manager = manager;
            this.AssetManager = assets;
            this.Client = client;
            bool hasItems = false;
            bool hasFolders = false;
            foreach (InventoryObject obj in inventory)
            {
                hasFolders = hasFolders || obj is InventoryFolder;
                hasItems = hasItems || obj is InventoryItem;
            }

            AssetTransfers = new Dictionary<LLUUID, InventoryItem>(Inventory.Count);

            if (inventory.Count > 0)
            {
                List<ToolStripMenuItem> Options = new List<ToolStripMenuItem>(AllOptions.Length + ItemOptions.Length + FolderOptions.Length);
                Options.AddRange(AllOptions);
                if (hasFolders && hasItems)
                {
                    Console.WriteLine("Showing context menu for multiple item types");
                }
                else if (hasFolders)
                {
                    Console.WriteLine("Adding context items for folders");
                    Options.AddRange(FolderOptions);
                }
                else if (hasItems)
                {
                    Console.WriteLine("Adding context items for items.");
                    Options.AddRange(ItemOptions);
                }

                Items.AddRange(Options.ToArray());
                AssetManager.OnAssetReceived += new AssetManager.AssetReceivedCallback(AssetManager_OnAssetReceived);
            }
        }

        void AssetManager_OnAssetReceived(AssetDownload transfer)
        {
            InventoryItem item;
            if (AssetTransfers.TryGetValue(transfer.ID, out item))
            {
                if (transfer.Success)
                {
                    Console.WriteLine("Received asset data for {0} ({1})", item.Name, item.UUID);
                    switch (item.AssetType)
                    {
                        case AssetType.LSLText:
                            AssetScript script = new AssetScript(transfer.AssetID);
                            script.SetEncodedData(transfer.AssetData);
                            Console.WriteLine("Script source:");
                            Console.WriteLine(script.Source);
                            break;
                        case AssetType.Notecard:
                            AssetNotecard note = new AssetNotecard(transfer.AssetID);
                            note.SetEncodedData(transfer.AssetData);
                            Console.WriteLine("Notecard text:");
                            Console.WriteLine(note.Text);
                            break;
                    }
                    AssetTransfers.Remove(transfer.ID);
                }
                else
                {
                    Console.WriteLine("Asset data retreival failed for {0} ({1})", item.Name, item.UUID);
                    Console.WriteLine("Status code: {0}", transfer.Status);
                }
            }
        }

        public void PrintAsset(object sender, EventArgs args)
        {
            foreach (InventoryObject obj in Inventory)
            {
                InventoryItem item = obj as InventoryItem;
                LLUUID transferID = AssetManager.RequestInventoryAsset(item.AssetUUID, item.UUID, LLUUID.Zero, item.OwnerID, item.AssetType, false);
                AssetTransfers.Add(transferID, item);
            }
        }

        public void DeleteItems(object sender, EventArgs args)
        {
            Console.WriteLine("Deleting:");
            foreach (InventoryObject obj in Inventory)
                Console.WriteLine(obj.Name);
            Manager.Remove(Inventory);
        }

        public void ItemInfo(object sender, EventArgs args)
        {
            Console.WriteLine("Object info:");
            foreach (InventoryObject obj in Inventory)
            {
                Console.WriteLine("\tType: {0}", (obj is InventoryFolder ? "Folder" : "Item"));
                Console.WriteLine("\tName: {0}", obj.Name);
                Console.WriteLine("\tItemID: {0}", obj.UUID);
                Console.WriteLine("\tParent: {0}", obj.ParentUUID);
                Console.WriteLine("\tOwner: {0}", obj.OwnerID);
            }
        }
    }

    
    public class InventoryWindow
    {
        public Form Form {
            get { return form; }
        }
        private Form form;
        private Dictionary<Node, InventoryNode> NodeMap;

        public ContextMenu ItemContextMenu;
        public ContextMenu FolderContextMenu;

        private TreeViewAdv Tree;
        private Inventory Inventory;
        private InventoryManager Manager;
        private AssetManager AssetManager;
        private SecondLife Client;
        public InventoryWindow(SecondLife client)
        {
            Client = client;
            Manager = client.Inventory;
            AssetManager = client.Assets;
            Inventory = Manager.Store;
            
            Inventory.OnInventoryObjectUpdated += new Inventory.InventoryObjectUpdated(Inventory_OnInventoryObjectUpdated);
            Inventory.OnInventoryObjectRemoved += new Inventory.InventoryObjectRemoved(Inventory_OnInventoryObjectRemoved);
            form = new Form();
        }

        public void ShowInventory()
        {
            if (Tree != null)
                Form.Controls.Remove(Tree);
            TreeModel model = ConstructTreeModel();
            Tree = new TreeViewAdv();
            NodeTextBox textBox = new NodeTextBox();
            textBox.DataPropertyName = "Text";
            textBox.Parent = Tree;
            textBox.EditEnabled = false; // Rename not supported yet.
            Tree.NodeControls.Add(textBox);

            Tree.Dock = DockStyle.Fill;
            Tree.Model = model;
            Tree.SelectionMode = Aga.Controls.Tree.TreeSelectionMode.Multi;
            
            foreach (TreeNodeAdv node in Tree.AllNodes) {
                
            }

            Form.AutoSize = true;
            Form.Controls.Add(Tree);
            Tree.MouseClick += new MouseEventHandler(Tree_MouseUp);
            //Tree.MouseUp += new MouseEventHandler(Tree_MouseUp);
            
            Form.Show();
        }

        delegate void VoidDelegate();

        void Inventory_OnInventoryObjectUpdated(InventoryObject oldObject, InventoryObject newObject)
        {
            Console.WriteLine("Inventory updated: {0} ({1})", newObject.Name, newObject.UUID);
            if (Form.Visible)
                Form.BeginInvoke(new VoidDelegate(ShowInventory));
        }

        void Inventory_OnInventoryObjectRemoved(InventoryObject obj)
        {
            Console.WriteLine("Inventory removed: {0} ({1})", obj.Name, obj.UUID);
            if (Form.Visible)
                Form.BeginInvoke(new VoidDelegate(ShowInventory));
        }

        void  Tree_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) {

                List<InventoryObject> selectedInventory = new List<InventoryObject>();
                foreach (TreeNodeAdv node in Tree.SelectedNodes) {
                    Node modelNode = node.Tag as Node;
                    InventoryObject inv = modelNode.Tag as InventoryObject;
                    selectedInventory.Add(inv);
                }
                InventoryContextMenu menu = new InventoryContextMenu(Client, Manager, AssetManager, selectedInventory);
                menu.Show(Tree, e.Location);
            }
        }

        private TreeModel ConstructTreeModel()
        {
            NodeMap = new Dictionary<Node, InventoryNode>();

            TreeModel model = new TreeModel();
            PopulateNode(Inventory.RootNode, model.Root);
            return model;
        }

        private void PopulateNode(InventoryNode invParent, Node treeParent)
        {
            foreach (InventoryNode invChild in invParent.Nodes.Values)
            {
                Node treeChild = new Node(invChild.Data.Name);
                treeChild.Tag = invChild.Data;
                NodeMap.Add(treeChild, invChild);
                treeParent.Nodes.Add(treeChild);
                //treeParent.Nodes.Add(treeChild);
                if (invChild.Nodes.Count > 0)
                    PopulateNode(invChild, treeChild);
            }
        }
    }
    public class InventoryViewer
    {
        private SecondLife Client;
        public InventoryViewer()
        {
            Client = new SecondLife();
            Client.Network.OnConnected += new NetworkManager.ConnectedCallback(Network_OnConnected);
        }


        private ManualResetEvent ConnectedEvent = new ManualResetEvent(false);
        public bool Login(string first, string last, string password, out string message)
        {
            bool success = Client.Network.Login(first, last, password, "InventoryViewer", "Christopher Omega");
            message = Client.Network.LoginMessage;
            ConnectedEvent.WaitOne();
            return success;
        }

        void Network_OnConnected(object sender)
        {
            Console.WriteLine("Connected.");
            ConnectedEvent.Set();
        }

        public void ShowInventory()
        {
            // Populate inventory with nodes:
            Console.WriteLine("Populating inventory...");
            //FIXME: Make this more direct.
            Client.Inventory.RequestFolderContents(Client.Inventory.Store.RootFolder.UUID, Client.Network.AgentID, true, true, true, InventorySortOrder.ByName);
            //IAsyncResult req = Client.Inventory.BeginFindObjects(Client.Inventory.Store.RootFolder.UUID, ".*", true, true, null, null);
            //Client.Inventory.EndFindObjects(req);
            Console.WriteLine("Inventory populated, displaying...");

            InventoryWindow window = new InventoryWindow(Client);
            window.ShowInventory();
            window.Form.FormClosing += new FormClosingEventHandler(Form_FormClosing);
            Application.Run(window.Form);
        }

        void Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            Logout();
        }

        public void Logout()
        {
            Client.Network.Logout();
            Application.Exit();
        }



        static InventoryViewer Viewer;
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: InventoryViewer first last password");
                return;
            }

            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);

            Viewer = new InventoryViewer();

            string message;
            if (Viewer.Login(args[0], args[1], args[2], out message))
            {
                Viewer.ShowInventory(); 
            }
            else
            {
                Console.WriteLine("Login failed: {0}", message);
            }

        }

        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Viewer.Logout();
        }
    }
}
