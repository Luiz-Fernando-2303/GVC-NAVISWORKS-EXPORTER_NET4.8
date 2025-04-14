using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Linq;
using GVC_EXPORTER_PLUGIN.Functions;
using GVC_EXPORTER_PLUGIN.Plugins.Render;
using System.Windows.Forms;
using D = System.Drawing;

namespace GVC_EXPORTER_PLUGIN.Plugins.Ui
{
    public class ChunkManagerDockPane : DockPanePlugin
    {
        private ChunkManagerHost _host;

        public override Control CreateControlPane()
        {
            _host = new ChunkManagerHost();
            return _host;
        }

        public override void DestroyControlPane(Control pane)
        {
            RenderController.Enabled = false;
            _host = null;
        }
    }

    public class ChunkManagerHost : UserControl
    {
        private ListBox chunkListBox;
        private Panel chunkOptionsPanel;
        private TableLayoutPanel mainPanel;
        private NumericUpDown inputX, inputY, inputZ, inputThreads;

        public ChunkManagerHost()
        {
            this.Dock = DockStyle.Fill;

            mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var topPanel = CreateTopControls();
            var bodyPanel = CreateMainInterface();

            mainPanel.Controls.Add(topPanel, 0, 0);
            mainPanel.Controls.Add(bodyPanel, 0, 1);

            this.Controls.Add(mainPanel);
        }

        private Control CreateTopControls()
        {
            var topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                AutoSize = true
            };

            inputX = CreateNumericInput("X");
            inputY = CreateNumericInput("Y");
            inputZ = CreateNumericInput("Z");
            inputThreads = CreateNumericInput("Threads");
            inputThreads.Value = 4;

            var btnLoad = new Button
            {
                Text = "Load Chunks",
                AutoSize = true
            };
            btnLoad.Click += (s, e) =>
            {
                int x = (int)inputX.Value;
                int y = (int)inputY.Value;
                int z = (int)inputZ.Value;
                int threads = (int)inputThreads.Value;

                ContextInitializer.InitializeContext(x, y, z, threads);
                RefreshChunks();
            };

            topPanel.Controls.AddRange(new Control[]
            {
            new Label { Text = "X:", AutoSize = true, TextAlign = D.ContentAlignment.MiddleLeft }, inputX,
            new Label { Text = "Y:", AutoSize = true, TextAlign = D.ContentAlignment.MiddleLeft }, inputY,
            new Label { Text = "Z:", AutoSize = true, TextAlign = D.ContentAlignment.MiddleLeft }, inputZ,
            new Label { Text = "Threads:", AutoSize = true, TextAlign = D.ContentAlignment.MiddleLeft }, inputThreads,
            btnLoad
            });

            return topPanel;
        }

        private NumericUpDown CreateNumericInput(string name)
        {
            return new NumericUpDown
            {
                Width = 50,
                Minimum = 1,
                Maximum = 1000,
                Value = 1
            };
        }

        private Control CreateMainInterface()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            chunkListBox = new ListBox
            {
                Dock = DockStyle.Fill
            };
            chunkListBox.SelectedIndexChanged += ChunkListBox_SelectionChanged;
            //chunkListBox.SelectedIndexChanged += hideChunks;

            chunkOptionsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = System.Drawing.Color.LightGray
            };
            var placeholder = new Label
            {
                Text = "Selecione um chunk à esquerda.",
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                ForeColor = System.Drawing.Color.DimGray
            };
            chunkOptionsPanel.Controls.Add(placeholder);

            layout.Controls.Add(chunkListBox, 0, 0);
            layout.Controls.Add(chunkOptionsPanel, 1, 0);

            return layout;
        }

        public void ShowChunks(Chunk c)
        {
            RenderController.chunk = c;
            RenderController.Enabled = !RenderController.Enabled;
        }

        public void RefreshChunks()
        {
            chunkListBox.Items.Clear();

            if (Context.Instance.chunks == null || Context.Instance.chunks.Count == 0)
                return;

            foreach (var chunk in Context.Instance.chunks)
                chunkListBox.Items.Add(chunk.name);
        }

        private void ChunkListBox_SelectionChanged(object sender, EventArgs e)
        {
            var selectedName = chunkListBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedName)) return;

            var selectedChunk = Context.Instance.chunks.FirstOrDefault(c => c.name == selectedName);
            RenderController.Enabled = false;

            if (selectedChunk != null)
                ShowChunks(selectedChunk);
                RenderChunkOptions(selectedChunk);
        }

        private void RenderChunkOptions(Chunk chunk)
        {
            chunkOptionsPanel.Controls.Clear();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); 
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); 

            var contentPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10)
            };

            var nameBox = new TextBox
            {
                Text = chunk.name,
                Width = 280,
                Margin = new Padding(0, 0, 0, 10)
            };
            nameBox.TextChanged += (s, e) =>
            {
                chunk.name = nameBox.Text;
                int index = chunkListBox.SelectedIndex;
                if (index >= 0)
                    chunkListBox.Items[index] = chunk.name;
            };

            contentPanel.Controls.Add(nameBox);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10),
                AutoSize = true
            };

            //var btnRenderChunk = new Button
            //{
            //    Text = "Toggle chunk view",
            //    Width = 130,
            //    Height = 30,
            //};
            //btnRenderChunk.Click += (s, e) =>
            //{
            //    RenderController.chunk = chunk;
            //    RenderController.Enabled = !RenderController.Enabled;
            //};

            var btnAddToSet = new Button
            {
                Text = "Adicionar ao Set",
                Width = 130,
                Height = 30,
            };
            btnAddToSet.Click += (s, e) =>
            {
                ContextInitializer.ChunskToSets(chunk);
            };

            //buttonPanel.Controls.Add(btnRenderChunk);
            buttonPanel.Controls.Add(btnAddToSet);

            // Monta tudo no layout
            layout.Controls.Add(contentPanel, 0, 0);
            layout.Controls.Add(buttonPanel, 0, 1);

            // Adiciona ao painel principal
            chunkOptionsPanel.Controls.Add(layout);
        }
    }
}