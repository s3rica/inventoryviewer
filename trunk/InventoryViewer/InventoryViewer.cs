using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

        private List<InventoryBase> Inventory;
        private InventoryManager Manager;
        private AssetManager AssetManager;
        private SecondLife Client;
        private Dictionary<LLUUID, InventoryItem> AssetTransfers = new Dictionary<LLUUID, InventoryItem>();
        private Dictionary<LLUUID, InventoryItem> PrintTransfers = new Dictionary<LLUUID, InventoryItem>();
        public InventoryContextMenu(SecondLife client, InventoryManager manager, AssetManager assets, List<InventoryBase> inventory)
        {
            AllOptions = new ToolStripMenuItem[] {
                new ToolStripMenuItem("Delete", null, new EventHandler(DeleteItems)),
                new ToolStripMenuItem("General Info", null, new EventHandler(GenericInfo)),
            };

            ItemOptions = new ToolStripMenuItem[] {
                new ToolStripMenuItem("Print Asset", null, new EventHandler(PrintAsset)),
                new ToolStripMenuItem("Save Asset...", null, new EventHandler(SaveItems)),
                new ToolStripMenuItem("Item Info", null, new EventHandler(ItemInfo)),
            };

            FolderOptions = new ToolStripMenuItem[] {
                //new ToolStripMenuItem("Info", null, new EventHandler(GenericInfo)),
            };

            this.Inventory = inventory;
            this.Manager = manager;
            this.AssetManager = assets;
            this.Client = client;
            bool hasItems = false;
            bool hasFolders = false;
            foreach (InventoryBase obj in inventory)
            {
                hasFolders = hasFolders || obj is InventoryFolder;
                hasItems = hasItems || obj is InventoryItem;
            }
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

        void AssetManager_OnAssetReceived(AssetDownload transfer, Asset asset)
        {
            lock (AssetTransfers)
            {
                lock (PrintTransfers)
                {
                    if (PrintTransfers.ContainsKey(transfer.ID))
                    {
                        InventoryItem item = PrintTransfers[transfer.ID];
                        if (transfer.Success)
                        {
                            Console.WriteLine("Received asset data for {0} ({1})", item.Name, item.UUID);
                            if (asset is AssetScriptText)
                            {
                                AssetScriptText script = asset as AssetScriptText;
                                Console.WriteLine("Script source:");
                                Console.WriteLine(script.Source);
                            }
                            else if (asset is AssetNotecard)
                            {
                                AssetNotecard notecard = asset as AssetNotecard;
                                Console.WriteLine("Notecard text:");
                                Console.WriteLine(notecard.Text);
                            }
                            else if (asset != null)
                            {
                                Console.WriteLine("Unknown asset type: {0}", asset.GetType());
                            }
                            else
                            {
                                Console.WriteLine("Asset is null!");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Asset data retreival failed for {0} ({1})", item.Name, item.UUID);
                            Console.WriteLine("Status code: {0}", transfer.Status);
                        }

                        PrintTransfers.Remove(transfer.ID);
                    }
                }
                lock (SaveTransfers)
                {
                    if (SaveTransfers.ContainsKey(transfer.ID))
                    {
                        DirectoryInfo directory = SaveTransfers[transfer.ID];
                        if (transfer.Success)
                        {
                            if (asset is AssetScriptText)
                            {
                                AssetScriptText script = asset as AssetScriptText;
                                StreamWriter writer = File.CreateText(Path.Combine(directory.FullName, AssetTransfers[transfer.ID].Name + ".lsl"));
                                writer.Write(script.Source);
                                writer.Close();
                                Console.WriteLine(AssetTransfers[transfer.ID].Name + ".lsl saved.");
                            }
                            else if (asset is AssetNotecard)
                            {
                                AssetNotecard note = asset as AssetNotecard;
                                FileStream stream = File.Create(Path.Combine(directory.FullName, AssetTransfers[transfer.ID].Name + ".txt"));
                                byte[] data = note.GetEncodedData();
                                stream.Write(data, 0, data.Length);
                                stream.Close();
                                Console.WriteLine(AssetTransfers[transfer.ID].Name + ".txt saved.");
                            }
                            else if (asset != null)
                            {
                                Console.WriteLine("Unknown asset type: {0}", asset.GetType());
                            }
                            else
                            {
                                Console.WriteLine("Asset is null!");
                            }
                        }
                        SaveTransfers.Remove(transfer.ID);
                    }
                }
                AssetTransfers.Remove(transfer.ID);
            }
        }

        public void PrintAsset(object sender, EventArgs args)
        {
            foreach (InventoryBase obj in Inventory)
            {
                lock (PrintTransfers)
                {
                    InventoryItem item = obj as InventoryItem;
                    LLUUID transferID = AssetManager.RequestInventoryAsset(item.AssetUUID, item.UUID, LLUUID.Zero, item.OwnerID, item.AssetType, false);
                    PrintTransfers.Add(transferID, item);
                }
            }
        }

        private Dictionary<LLUUID, DirectoryInfo> SaveTransfers = new Dictionary<LLUUID, DirectoryInfo>();
        public void SaveItems(object sender, EventArgs args)
        {
            string path = Path.Combine(Environment.CurrentDirectory, "inv");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            DirectoryInfo info = new DirectoryInfo(path);
            foreach (InventoryBase obj in Inventory)
            {
                lock (SaveTransfers)
                    lock (AssetTransfers)
                    {
                        InventoryItem item = (InventoryItem)obj;
                        LLUUID transferID = AssetManager.RequestInventoryAsset(item.AssetUUID, item.UUID, LLUUID.Zero, item.OwnerID, item.AssetType, false);
                        SaveTransfers.Add(transferID, info);
                        AssetTransfers.Add(transferID, item);
                    }
            }
        }

        public void DeleteItems(object sender, EventArgs args)
        {
            Console.WriteLine("Deleting:");
            foreach (InventoryBase obj in Inventory)
                Console.WriteLine(obj.Name);
            Manager.Remove(Inventory);
        }

        public void ItemInfo(object sender, EventArgs args)
        {
            Console.WriteLine("Object info:");
            List<LLUUID> itemIDs = new List<LLUUID>(Inventory.Count);
            foreach (InventoryBase obj in Inventory)
            {
                itemIDs.Add(obj.UUID);
            }
            Console.WriteLine("Pre-Fetch!");
            Manager.FetchInventory(itemIDs);
            Console.WriteLine("Post-Fetch!");
            foreach (InventoryBase obj in Inventory) {
                Console.WriteLine("\tType: {0}", (obj is InventoryFolder ? "Folder" : (obj as InventoryItem).InventoryType.ToString()));
                Console.WriteLine("\tName: {0}", obj.Name);
                Console.WriteLine("\tItemID: {0}", obj.UUID);
                Console.WriteLine("\tParent: {0}", obj.ParentUUID);
                Console.WriteLine("\tOwner: {0}", obj.OwnerID);
            }
        }

        public void GenericInfo(object sender, EventArgs args)
        {
            foreach (InventoryBase obj in Inventory)
            {
                Console.WriteLine("\tType: {0}", (obj is InventoryFolder ? "Folder" : (obj as InventoryItem).InventoryType.ToString()));
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
            
            Inventory.OnInventoryObjectUpdated += new Inventory.InventoryObjectUpdated(Inventory_OnInventoryBaseUpdated);
            Inventory.OnInventoryObjectRemoved += new Inventory.InventoryObjectRemoved(Inventory_OnInventoryBaseRemoved);
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
            
            Form.AutoSize = true;
            Form.Controls.Add(Tree);
            Tree.MouseClick += new MouseEventHandler(Tree_MouseUp);
            //Tree.MouseUp += new MouseEventHandler(Tree_MouseUp);
            
            Form.Show();
        }

        delegate void VoidDelegate();

        void Inventory_OnInventoryBaseUpdated(InventoryBase oldObject, InventoryBase newObject)
        {
            Console.WriteLine("Inventory updated: {0} ({1})", newObject.Name, newObject.UUID);
            if (Form.Visible)
                Form.BeginInvoke(new VoidDelegate(ShowInventory));
        }

        void Inventory_OnInventoryBaseRemoved(InventoryBase obj)
        {
            Console.WriteLine("Inventory removed: {0} ({1})", obj.Name, obj.UUID);
            if (Form.Visible)
                Form.BeginInvoke(new VoidDelegate(ShowInventory));
        }

        void  Tree_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) {

                List<InventoryBase> selectedInventory = new List<InventoryBase>();
                foreach (TreeNodeAdv node in Tree.SelectedNodes) {
                    Node modelNode = node.Tag as Node;
                    InventoryBase inv = modelNode.Tag as InventoryBase;
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
    public class InventoryViewer : ApplicationContext
    {
        private SecondLife Client;
        public InventoryViewer()
        {
            Client = new SecondLife();
            Client.Self.OnInstantMessage += new MainAvatar.InstantMessageCallback(Self_OnInstantMessage);
        }

        void Self_OnInstantMessage(LLUUID fromAgentID, string fromAgentName, LLUUID toAgentID, uint parentEstateID, LLUUID regionID, LLVector3 position, MainAvatar.InstantMessageDialog dialog, bool groupIM, LLUUID imSessionID, DateTime timestamp, string message, MainAvatar.InstantMessageOnline offline, byte[] binaryBucket, Simulator simulator)
        {
            Console.WriteLine("IM ({0}): {1}", fromAgentName, message);
        }

        public bool Login(string first, string last, string password, out string message)
        {
            bool success = Client.Network.Login(first, last, password, "InventoryViewer", "Christopher Omega");
            message = Client.Network.LoginMessage;
            return success;
        }

        public void ShowInventory()
        {
            // Populate inventory with nodes:
            Console.WriteLine("Populating inventory...");
            Client.Inventory.RequestFolderContents(Client.Inventory.Store.RootFolder.UUID, Client.Network.AgentID, true, true, true, InventorySortOrder.ByName);
            //IAsyncResult req = Client.Inventory.BeginFindObjects(Client.Inventory.Store.RootFolder.UUID, ".*", true, true, null, null);
            //Client.Inventory.EndFindObjects(req);
            Console.WriteLine("Inventory populated, displaying...");

            InventoryWindow window = new InventoryWindow(Client);
            window.ShowInventory();
            window.Form.FormClosing += new FormClosingEventHandler(Form_FormClosing);
        }

        void Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            Logout();
        }

        public void Logout()
        {
            Client.Network.Logout();
            ExitThread();
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
            Application.Run(Viewer);
        }

        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Viewer.Logout();
        }
    }
}
