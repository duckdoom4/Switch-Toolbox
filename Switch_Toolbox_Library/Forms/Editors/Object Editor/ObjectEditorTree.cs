﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GL_EditorFramework.Interfaces;
using GL_EditorFramework.EditorDrawables;
using System.Text.RegularExpressions;
using Toolbox.Library.Animations;
using Toolbox.Library.IO;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Toolbox.Library.Forms
{
    public partial class ObjectEditorTree : UserControl
    {
        private bool SuppressAfterSelectEvent = false;

        private bool IsSearchPanelDocked
        {
            get
            {
                return dockSearchListToolStripMenuItem.Checked;
            }
            set
            {
                dockSearchListToolStripMenuItem.Checked = value;
            }
        }

        private enum TreeNodeSize
        {
            Small,
            Normal,
            Large,
            ExtraLarge,
        }

        public ObjectEditor ObjectEditor;

        public void BeginUpdate() { treeViewCustom1.BeginUpdate(); }
        public void EndUpdate() { treeViewCustom1.EndUpdate(); }

        public void AddIArchiveFile(IFileFormat FileFormat)
        {
            var FileRoot = new ArchiveRootNodeWrapper(FileFormat.FileName, (IArchiveFile)FileFormat);
            FileRoot.FillTreeNodes();
            AddNode(FileRoot);

            if (FileFormat is TreeNode) //It can still be both, so add all it's nodes
            {
                foreach (TreeNode n in ((TreeNode)FileFormat).Nodes)
                    FileRoot.Nodes.Add(n);
            }
        }

        public void AddNodeCollection(TreeNodeCollection nodes, bool ClearNodes)
        {
            // Invoke the treeview to add the nodes
            treeViewCustom1.Invoke((Action)delegate ()
            {
                treeViewCustom1.BeginUpdate(); // No visual updates until we say 
                if (ClearNodes)
                    treeViewCustom1.Nodes.Clear(); // Remove existing nodes

                foreach (TreeNode node in nodes)
                    treeViewCustom1.Nodes.Add(node); // Add the new nodes

                treeViewCustom1.EndUpdate(); // Allow the treeview to update visually
            });

        }

        public TreeNodeCollection GetNodes() { return treeViewCustom1.Nodes; }

        public void AddNode(TreeNode node, bool ClearAllNodes = false)
        {
            if (treeViewCustom1.InvokeRequired)
            {
                // Invoke the treeview to add the nodes
                treeViewCustom1.Invoke((Action)delegate ()
                {
                    AddNodes(node, ClearAllNodes);
                });
            }
            else
            {
                AddNodes(node, ClearAllNodes);
            }
        }

        private void AddNodes(TreeNode node, bool ClearAllNodes = false)
        {
            treeViewCustom1.BeginUpdate(); // No visual updates until we say 
            if (ClearAllNodes)
                ClearNodes();
            treeViewCustom1.Nodes.Add(node); // Add the new nodes
            treeViewCustom1.EndUpdate(); // Allow the treeview to update visually

            if (node is ISingleTextureIconLoader) {
                LoadGenericTextureIcons((ISingleTextureIconLoader)node);
            }
        }

        public void ClearNodes()
        {
            treeViewCustom1.Nodes.Clear();
        }

        public bool AddFilesToActiveEditor
        {
            get
            {
                return activeEditorChkBox.Checked;
            }
            set
            {
                activeEditorChkBox.Checked = value;
                Runtime.AddFilesToActiveObjectEditor = value;
            }
        }

        public ObjectEditorTree(ObjectEditor objectEditor)
        {
            InitializeComponent();

            UpdateSearchPanelDockState();

            ObjectEditor = objectEditor;

            if (Runtime.ObjectEditor.ListPanelWidth > 0)
                stPanel1.Width = Runtime.ObjectEditor.ListPanelWidth;

            treeViewCustom1.BackColor = FormThemes.BaseTheme.ObjectEditorBackColor;

            AddFilesToActiveEditor = Runtime.AddFilesToActiveObjectEditor;

            foreach (TreeNodeSize nodeSize in (TreeNodeSize[])Enum.GetValues(typeof(TreeNodeSize)))
                nodeSizeCB.Items.Add(nodeSize);

            nodeSizeCB.SelectedIndex = 1;
        }

        public Viewport GetViewport() => viewport;

        //Attatch a viewport instance here if created.
        //If the editor gets switched, we can keep the previous viewed area when switched back
        Viewport viewport = null;

        bool IsLoaded = false;
        public void LoadViewport(Viewport Viewport)
        {
            viewport = Viewport;

            IsLoaded = true;
        }

        public IFileFormat GetActiveFile()
        {
            if (treeViewCustom1.Nodes.Count == 0)
                return null;

            if (treeViewCustom1.Nodes[0] is IFileFormat)
                return (IFileFormat)treeViewCustom1.Nodes[0];
            if (treeViewCustom1.Nodes[0] is ArchiveBase)
                return (IFileFormat)((ArchiveBase)treeViewCustom1.Nodes[0]).ArchiveFile;
            return null;
        }

        public void LoadEditor(Control control)
        {
            foreach (var ctrl in stPanel2.Controls)
            {
                if (ctrl is STUserControl)
                    ((STUserControl)ctrl).OnControlClosing();
            }

            stPanel2.Controls.Clear();
            stPanel2.Controls.Add(control);
        }

        bool RenderedObjectWasSelected = false;
        private void treeViewCustom1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (SuppressAfterSelectEvent)
                return;

            var node = treeViewCustom1.SelectedNode;

            //Set the current index used determine what bone is selected.
            //Update viewport for selection viewing
            if (node is STBone)
            {
                Runtime.SelectedBoneIndex = ((STBone)node).GetIndex();
            }
            else
                Runtime.SelectedBoneIndex = -1;

            //Set click events for custom nodes
            if (node is TreeNodeCustom)
            {
                ((TreeNodeCustom)node).OnClick(treeViewCustom1);
            }

            //Check if it is renderable for updating the viewport
            if (IsRenderable(node))
            {
                LibraryGUI.UpdateViewport();
                RenderedObjectWasSelected = true;
            }
            else
            {
                //Check if the object was previously selected
                //This will disable selection view and other things
                if (RenderedObjectWasSelected)
                {
                    LibraryGUI.UpdateViewport();
                    RenderedObjectWasSelected = false;
                }
            }
        }

        public bool IsRenderable(TreeNode obj)
        {
            if (obj is STGenericModel)
                return true;
            if (obj is STGenericObject)
                return true;
            if (obj is STBone)
                return true;
            if (obj is STSkeleton)
                return true;
            if (obj is STGenericMaterial)
                return true;

            return false;
        }

        private void treeViewCustom1_DoubleClick(object sender, EventArgs e)
        {
            if (treeViewCustom1.SelectedNode is TreeNodeCustom)
            {
                ((TreeNodeCustom)treeViewCustom1.SelectedNode).OnDoubleMouseClick(treeViewCustom1);
            }
        }

        public void UpdateTextureIcon(ISingleTextureIconLoader texturIcon, Image image) {
            treeViewCustom1.ReloadTextureIcons(texturIcon, image);
        }

        public List<Control> GetEditors()
        {
            List<Control> controls = new List<Control>();
            foreach (Control ctrl in stPanel2.Controls)
                controls.Add(ctrl);
            return controls;
        }

        public void FormClosing()
        {
            if (searchForm != null)
            {
                searchForm.OnControlClosing();
                searchForm.Dispose();
            }

            foreach (var control in stPanel2.Controls)
            {
                if (control is STUserControl)
                    ((STUserControl)control).OnControlClosing();
            }

            foreach (var node in TreeViewExtensions.Collect(treeViewCustom1.Nodes))
            {
                if (node is IFileFormat)
                {
                    ((IFileFormat)node).Unload();
                }
            }
            ClearNodes();
        }

        private void treeViewCustom1_MouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (e.Node is IContextMenuNode)
                {
                    bool IsRoot = e.Node.Parent == null;

                    treeNodeContextMenu.Items.Clear();
                    if (IsRoot)
                    {
                        foreach (var item in ((IContextMenuNode)e.Node).GetContextMenuItems())
                        {
                            if (item.Text != "Delete" || item.Text != "Remove")
                                treeNodeContextMenu.Items.Add(item);
                        }
                        treeNodeContextMenu.Items.Add(new ToolStripMenuItem("Delete", null, DeleteAction, Keys.Control | Keys.Delete));
                    }
                    else
                    {
                        treeNodeContextMenu.Items.AddRange(((IContextMenuNode)e.Node).GetContextMenuItems());
                    }
                    treeNodeContextMenu.Show(Cursor.Position);

                    //Select the node without the evemt
                    //We don't want editors displaying on only right clicking
                    SuppressAfterSelectEvent = true;
                    treeViewCustom1.SelectedNode = e.Node;
                    SuppressAfterSelectEvent = false;
                }
            }
            else
            {
                OnAnimationSelected(e.Node);
            }
        }

        private void DeleteAction(object sender, EventArgs args)
        {
            var node = treeViewCustom1.SelectedNode;
            if (node != null)
            {
                var result = MessageBox.Show("If you remove this file, any unsaved progress will be lost! Continue?",
                    "Remove Dialog", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    if (node is IFileFormat)
                    {
                        ((IFileFormat)node).Unload();
                    }

                    treeViewCustom1.Nodes.Remove(node);
                    ResetEditor();

                    //Force garbage collection.
                    GC.Collect();

                    // Wait for all finalizers to complete before continuing.
                    GC.WaitForPendingFinalizers();

                    ((IUpdateForm)Runtime.MainForm).UpdateForm();
                }
            }
        }

        private void ResetEditor()
        {
            foreach (Control control in stPanel2.Controls)
            {
                if (control is STUserControl)
                    ((STUserControl)control).OnControlClosing();

                control.Dispose();
            }

            stPanel2.Controls.Clear();
        }

        private void OnAnimationSelected(TreeNode Node)
        {
            if (Node is Animation)
            {
                Viewport viewport = LibraryGUI.GetActiveViewport();
                if (viewport == null)
                    return;

                if (((Animation)Node).Bones.Count <= 0)
                    ((Animation)Node).OpenAnimationData();

                string AnimName = Node.Text;
                AnimName = Regex.Match(AnimName, @"([A-Z][0-9][0-9])(.*)").Groups[0].ToString();
                if (AnimName.Length > 3)
                    AnimName = AnimName.Substring(3);

                Animation running = new Animation(AnimName);
                running.ReplaceMe((Animation)Node);
                running.Tag = Node;

                Queue<TreeNode> NodeQueue = new Queue<TreeNode>();
                foreach (TreeNode n in treeViewCustom1.Nodes)
                {
                    NodeQueue.Enqueue(n);
                }
                while (NodeQueue.Count > 0)
                {
                    try
                    {
                        TreeNode n = NodeQueue.Dequeue();
                        string NodeName = Regex.Match(n.Text, @"([A-Z][0-9][0-9])(.*)").Groups[0].ToString();
                        if (NodeName.Length <= 3)
                            Console.WriteLine(NodeName);
                        else
                            NodeName = NodeName.Substring(3);
                        if (n is Animation)
                        {
                            if (n == Node)
                                continue;
                            if (NodeName.Equals(AnimName))
                            {
                                running.Children.Add(n);
                            }
                        }
                        if (n is AnimationGroupNode)
                        {
                            foreach (TreeNode tn in n.Nodes)
                                NodeQueue.Enqueue(tn);
                        }
                    }
                    catch
                    {

                    }
                }

                if (LibraryGUI.GetAnimationPanel() != null)
                {
                    LibraryGUI.GetAnimationPanel().CurrentAnimation = running;
                }
            }
        }

        public void RemoveFile(TreeNode File)
        {
            if (File is IFileFormat)
            {
                ((IFileFormat)File).Unload();
            }

            treeViewCustom1.Nodes.Remove(File);
        }

        public void ResetControls()
        {
            treeViewCustom1.Nodes.Clear();
            Text = "";

            ResetEditor();
        }

        bool UpdateViewport = false;
        bool IsModelChecked = false;
        private void treeViewCustom1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            UpdateViewport = false;

            if (e.Node is STGenericModel)
            {
                IsModelChecked = true;
                CheckChildNodes(e.Node, e.Node.Checked);
                IsModelChecked = false;
            }
            else if (e.Node is STGenericObject && !IsModelChecked)
            {
                UpdateViewport = true;
            }
            else if (e.Node is STBone && !IsModelChecked)
            {
                UpdateViewport = true;
            }

            if (UpdateViewport)
            {
                LibraryGUI.UpdateViewport();
            }
        }

        private void CheckChildNodes(TreeNode node, bool IsChecked)
        {
            foreach (TreeNode n in node.Nodes)
            {
                n.Checked = IsChecked;
                if (n.Nodes.Count > 0)
                {
                    CheckChildNodes(n, IsChecked);
                }
            }

            UpdateViewport = true; //Update viewport on the last node checked
        }

        private void treeViewCustom1_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            e.DrawDefault = true;
            bool IsCheckable = (e.Node is STGenericObject || e.Node is STGenericModel
                                                          || e.Node is STBone);
            if (!IsCheckable)
                TreeViewExtensions.HideCheckBox(e.Node);
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e) {
            AddNewFile();
        }

        private void toolStripButton1_Click(object sender, EventArgs e) {
            AddNewFile();
        }

        private void AddNewFile()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = Utils.GetAllFilters(FileManager.GetFileFormats());
            ofd.Multiselect = true;

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                foreach (string file in ofd.FileNames)
                    OpenFile(file);

                Cursor.Current = Cursors.Default;
            }
        }

        private void OpenFile(string FileName)
        {
            object file = STFileLoader.OpenFileFormat(FileName);

            if (file is TreeNode)
            {
                var node = (TreeNode)file;
                AddNode(node);
            }
            else if (file is IArchiveFile)
            {
                AddIArchiveFile((IFileFormat)file);
            }
            else
            {
                STErrorDialog.Show("Invalid file type. Cannot add file to object list.", "Object List", "");
            }

            ((IUpdateForm)Runtime.MainForm).UpdateForm();
        }

        private void sortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeViewCustom1.Sort();
        }

        private void splitter1_Resize(object sender, EventArgs e)
        {
        }

        private void splitter1_LocationChanged(object sender, EventArgs e)
        {
        }

        private void stPanel1_Resize(object sender, EventArgs e)
        {
            Runtime.ObjectEditor.ListPanelWidth = stPanel1.Width;
        }

        private void activeEditorChkBox_CheckedChanged(object sender, EventArgs e)
        {
            AddFilesToActiveEditor = activeEditorChkBox.Checked;
            Console.WriteLine("AddFilesToActiveObjectEditor " + Runtime.AddFilesToActiveObjectEditor);
        }

        private void treeViewCustom1_DragDrop(object sender, DragEventArgs e)
        {
            Point pt = treeViewCustom1.PointToClient(new Point(e.X, e.Y));
            treeViewCustom1.SelectedNode = treeViewCustom1.GetNodeAt(pt.X, pt.Y);
            bool IsFile = treeViewCustom1.SelectedNode is ArchiveFileWrapper && treeViewCustom1.SelectedNode.Parent != null;

            var archiveFile = GetActiveArchive();

            //Use the parent folder for files if it has any
            if (IsFile)
                TreeHelper.AddFiles(treeViewCustom1.SelectedNode.Parent, archiveFile, e.Data.GetData(DataFormats.FileDrop) as string[]);
            else
                TreeHelper.AddFiles(treeViewCustom1.SelectedNode, archiveFile, e.Data.GetData(DataFormats.FileDrop) as string[]);
        }

        private void treeViewCustom1_DragOver(object sender, DragEventArgs e)
        {
            var root = GetActiveArchive();
            if (root == null || !root.ArchiveFile.CanReplaceFiles)
                return;

            Point pt = treeViewCustom1.PointToClient(new Point(e.X, e.Y));
            TreeNode node = treeViewCustom1.GetNodeAt(pt.X, pt.Y);
            treeViewCustom1.SelectedNode = node;
            bool IsRoot = node is ArchiveRootNodeWrapper;
            bool IsFolder = node is ArchiveFolderNodeWrapper;
            bool IsFile = node is ArchiveFileWrapper && node.Parent != null;

            if (IsFolder || IsRoot || IsFile)
                e.Effect = DragDropEffects.Link;
         //   else
             //   e.Effect = DragDropEffects.None;
        }

        private ArchiveRootNodeWrapper GetActiveArchive()
        {
            var node = treeViewCustom1.SelectedNode;
            if (node != null && node is ArchiveRootNodeWrapper)
                return (ArchiveRootNodeWrapper)node;
            if (node != null && node is ArchiveFileWrapper)
                return ((ArchiveFileWrapper)node).RootNode;
            if (node != null && node is ArchiveFolderNodeWrapper)
                return ((ArchiveFolderNodeWrapper)node).RootNode;

            return null;
        }

        private void treeViewCustom1_KeyPress(object sender, KeyEventArgs e)
        {
            if (treeViewCustom1.SelectedNode != null && treeViewCustom1.SelectedNode is IContextMenuNode)
            {
                IContextMenuNode node = (IContextMenuNode)treeViewCustom1.SelectedNode;

                var Items = node.GetContextMenuItems();
                foreach (ToolStripItem toolstrip in Items)
                {
                    if (toolstrip is ToolStripMenuItem)
                    {
                        if (((ToolStripMenuItem)toolstrip).ShortcutKeys == e.KeyData)
                            toolstrip.PerformClick();
                    }
                }
            }
        }

        private SearchNodePanel searchForm;
        private void searchFormToolStrip_Click(object sender, EventArgs e)
        {
            searchForm = new SearchNodePanel(treeViewCustom1);
            searchForm.Dock = DockStyle.Fill;
            STForm form = new STForm();

            var panel = new STPanel() { Dock = DockStyle.Fill };
            panel.Controls.Add(searchForm);
            form.AddControl(panel);
            form.Show(this);
        }

        private void dockSearchListToolStripMenuItem_Click(object sender, EventArgs e) {
            UpdateSearchPanelDockState();
        }

        private void UpdateSearchPanelDockState()
        {
            if (IsSearchPanelDocked)
            {
                splitContainer1.Panel1Collapsed = false;
                splitContainer1.Panel1.Controls.Clear();

                searchForm = new SearchNodePanel(treeViewCustom1);
                searchForm.Dock = DockStyle.Fill;
                splitContainer1.Panel1.Controls.Add(searchForm);
            }
            else
            {
                splitContainer1.Panel1Collapsed = true;
            }
        }

        public void LoadGenericTextureIcons(ITextureIconLoader iconList) {
            treeViewCustom1.TextureIcons.Add(iconList);
            treeViewCustom1.ReloadTextureIcons(iconList);
        }

        public void LoadGenericTextureIcons(ISingleTextureIconLoader iconTex) {
            treeViewCustom1.SingleTextureIcons.Add(iconTex);
            treeViewCustom1.ReloadTextureIcons(iconTex);
        }

        private void nodeSizeCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            var nodeSize = nodeSizeCB.SelectedItem;
            if (nodeSize != null)
            {
                int Size = 22;

                switch ((TreeNodeSize)nodeSize)
                {
                    case TreeNodeSize.Small: Size = 18;
                        break;
                    case TreeNodeSize.Normal: Size = 22;
                        break;
                    case TreeNodeSize.Large: Size = 30;
                        break;
                    case TreeNodeSize.ExtraLarge: Size = 35;
                        break;
                }

                treeViewCustom1.ItemHeight = Size;
                treeViewCustom1.ReloadImages(Size, Size);
                treeViewCustom1.ReloadTextureIcons();
            }
        }

        private void treeViewCustom1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node != null && e.Node is ITextureIconLoader) {
                LoadGenericTextureIcons((ITextureIconLoader)e.Node);
            }
            if (e.Node != null && e.Node is ISingleTextureIconLoader) {
                LoadGenericTextureIcons((ISingleTextureIconLoader)e.Node);
            }
        }
    }
}
