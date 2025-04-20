using Autodesk.Navisworks.Api.Plugins;
using GVC_EXPORTER_PLUGIN.Functions._Binary;
using System.Windows.Forms;
using System;
using System.Threading;

namespace GVC_EXPORTER_PLUGIN
{
    /// <summary>
    /// Dialogo de entrada para parâmetros de divisão em chunks (blocos espaciais).
    /// </summary>
    public partial class PackingDialog : Form
    {
        public double SizeX => (double)numericX.Value;
        public double SizeY => (double)numericY.Value;
        public double SizeZ => (double)numericZ.Value;
        public double Angle => (double)numericAngle.Value;

        private NumericUpDown numericX, numericY, numericZ, numericAngle;

        public PackingDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Inicializa os componentes da interface gráfica do diálogo.
        /// </summary>
        private void InitializeComponent()
        {
            this.Text = "Enter Chunk Parameters";
            this.Width = 300;
            this.Height = 250;

            // Rótulos
            var labelX = new Label { Text = "Width (X):", Top = 20, Left = 10 };
            var labelY = new Label { Text = "Height (Y):", Top = 50, Left = 10 };
            var labelZ = new Label { Text = "Depth (Z):", Top = 80, Left = 10 };
            var labelAngle = new Label { Text = "Correction Angle:", Top = 110, Left = 10 };

            // Campos numéricos
            numericX = new NumericUpDown { Left = 120, Top = 20, Minimum = 1, Maximum = 1000, Value = 20 };
            numericY = new NumericUpDown { Left = 120, Top = 50, Minimum = 1, Maximum = 1000, Value = 20 };
            numericZ = new NumericUpDown { Left = 120, Top = 80, Minimum = 1, Maximum = 1000, Value = 20 };
            numericAngle = new NumericUpDown { Left = 120, Top = 110, Minimum = 0, Maximum = 360, Value = 0 };

            // Botão OK
            var btnOk = new Button { Text = "OK", Left = 100, Width = 80, Top = 150 };
            btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; Close(); };

            // Adiciona controles ao formulário
            this.Controls.AddRange(new Control[] { labelX, labelY, labelZ, labelAngle, numericX, numericY, numericZ, numericAngle, btnOk });
        }
    }

    /// <summary>
    /// Formulário para acompanhar o progresso das operações de chunking/renderização.
    /// </summary>
    public class ProgressMonitor : Form
    {
        private Label lblTimer;
        private Label lblState;
        private ProgressBar progressBar;
        private System.Windows.Forms.Timer updateTimer;

        private string lastName = "";
        private int lastTotal = -1;
        private int lastCurrent = -1;

        private DateTime startTime;
        private DateTime lastUpdateTime;

        public ProgressMonitor()
        {
            // Configuração do formulário
            this.Text = "Progress";
            this.Width = 300;
            this.Height = 140;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Timer geral
            lblTimer = new Label
            {
                Left = 10,
                Top = 10,
                Width = 270,
                Height = 20,
                Text = "Tempo: 00:00"
            };

            // Estado atual do processo
            lblState = new Label
            {
                Left = 10,
                Top = 35,
                Width = 270,
                Height = 25,
                Text = "Waiting..."
            };

            // Barra de progresso
            progressBar = new ProgressBar
            {
                Left = 10,
                Top = 65,
                Width = 270,
                Minimum = 0,
                Maximum = 100,
                Visible = false
            };

            // Adiciona controles
            this.Controls.Add(lblTimer);
            this.Controls.Add(lblState);
            this.Controls.Add(progressBar);

            // Inicializa tempos
            startTime = DateTime.Now;
            lastUpdateTime = DateTime.Now;

            // Timer que atualiza a interface a cada 100ms
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 100;
            updateTimer.Tick += (s, e) => UpdateProgress();
            updateTimer.Start();
        }

        /// <summary>
        /// Atualiza a barra de progresso, estado e cronômetro com base nos metadados do contexto.
        /// </summary>
        private void UpdateProgress()
        {
            TimeSpan elapsed = DateTime.Now - startTime;
            lblTimer.Text = $"Tempo: {elapsed:mm\\:ss}";

            if (Functions._Binary.Context.Instance._metadata.TryGetValue("state", out var state))
            {
                var (name, total, current) = ((string, int, int))state;

                bool changed = name != lastName || total != lastTotal || current != lastCurrent;

                // Progresso normal atualizado
                if (changed)
                {
                    lastName = name;
                    lastTotal = total;
                    lastCurrent = current;
                    lastUpdateTime = DateTime.Now;

                    if (total == 0 && current == 0)
                    {
                        progressBar.Visible = false;
                        lblState.Text = $"Estado: {name}";
                        if (!this.Visible) this.Show();
                        return;
                    }

                    if (name == "done")
                    {
                        if (this.Visible) this.Hide();
                        return;
                    }

                    progressBar.Visible = true;
                    int percent = Math.Min(100, (int)((double)current / total * 100));
                    progressBar.Value = percent;
                    lblState.Text = $"Estado: {name} ({current}/{total})";

                    if (!this.Visible) this.Show();
                }
                else
                {
                    // Sem progresso recente — mostrar "aguarde..."
                    TimeSpan sinceLastUpdate = DateTime.Now - lastUpdateTime;
                    if (sinceLastUpdate.TotalSeconds > 10)
                    {
                        lblState.Text = $"Aguardando... ({elapsed:mm\\:ss})";
                    }
                }
            }
        }
    }

    /// <summary>
    /// Plugin de teste de renderização (pode ser usado para debug ou testes manuais).
    /// </summary>
    [Plugin("RenderModule", "ADSK", DisplayName = "RenderModule", ToolTip = "RenderModule")]
    public class RENDER : OBB_Render { }

    /// <summary>
    /// Plugin principal para inicializar chunks e acompanhar o progresso.
    /// </summary>
    [Plugin("ChunkMonitorPlugin", "ADSK", DisplayName = "Chunk Setup", ToolTip = "Initialize chunks and monitor progress")]
    public class ChunkMonitorPlugin : AddInPlugin
    {
        private static Thread monitorThread;
        private static ProgressMonitor monitorInstance;

        public override int Execute(params string[] parameters)
        {
            Autodesk.Navisworks.Api.Application.ActiveDocumentChanged += (s, e) => Unload();
            Autodesk.Navisworks.Api.Application.DocumentAdded += (s, e) => Unload();
            Autodesk.Navisworks.Api.Application.DocumentRemoved += (s, e) => Unload();
            Autodesk.Navisworks.Api.Application.MainDocumentChanged += (s, e) => Unload();

            var dialog = new PackingDialog();
            if (dialog.ShowDialog() != DialogResult.OK) return 0;

            double sizeX = dialog.SizeX;
            double sizeY = dialog.SizeY;
            double sizeZ = dialog.SizeZ;
            double angle = dialog.Angle;

            if (monitorThread == null || !monitorThread.IsAlive)
            {
                monitorThread = new Thread(() =>
                {
                    monitorInstance = new ProgressMonitor();
                    Application.Run(monitorInstance);
                });

                monitorThread.SetApartmentState(ApartmentState.STA);
                monitorThread.IsBackground = true;
                monitorThread.Start();
            }

            OBB_ContextInitializer.InitializeContext(sizeX, sizeY, sizeZ, angle);
            ContextToModel.ApplyChunksToItems();

            return 0;
        }

        /// <summary>
        /// Limpa o contexto armazenado.
        /// </summary>
        private void Unload()
        {
            Context.Instance.Clear();

            if (monitorInstance != null && !monitorInstance.IsDisposed)
            {
                monitorInstance.Invoke(new Action(() =>
                {
                    monitorInstance.Close();
                }));
            }

            monitorInstance = null;
            monitorThread = null;
        }
    }
}
