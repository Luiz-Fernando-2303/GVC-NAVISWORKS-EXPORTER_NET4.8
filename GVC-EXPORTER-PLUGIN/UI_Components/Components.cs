using System;
using System.Windows.Forms;

namespace GVC_EXPORTER_PLUGIN.UI_Components
{
    /// <summary>
    /// Form to track the progress of chunking/rendering operations.
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
            // Form configuration
            this.Text = "Progress";
            this.Width = 300;
            this.Height = 140;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Timer label
            lblTimer = new Label
            {
                Left = 10,
                Top = 10,
                Width = 270,
                Height = 20,
                Text = "00:00:00"
            };

            // Current process state
            lblState = new Label
            {
                Left = 10,
                Top = 35,
                Width = 270,
                Height = 25,
                Text = "Waiting..."
            };

            // Progress bar
            progressBar = new ProgressBar
            {
                Left = 10,
                Top = 65,
                Width = 270,
                Minimum = 0,
                Maximum = 100,
                Visible = false
            };

            // Add controls
            this.Controls.Add(lblTimer);
            this.Controls.Add(lblState);
            this.Controls.Add(progressBar);

            // Initialize time variables
            startTime = DateTime.Now;
            lastUpdateTime = DateTime.Now;

            // Timer that updates the interface every 100ms
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 100;
            updateTimer.Tick += (s, e) => UpdateProgress();
            updateTimer.Start();
        }

        /// <summary>
        /// Updates the progress bar, state, and timer based on the context metadata.
        /// </summary>
        private void UpdateProgress()
        {
            TimeSpan elapsed = DateTime.Now - startTime;
            lblTimer.Text = $"{elapsed.Hours}:{elapsed.Minutes}:{elapsed.Seconds}";

            if (Context.Instance._state.TryGetValue("state", out var state))
            {
                var (name, total, current) = ((string, int, int))state;

                bool changed = name != lastName || total != lastTotal || current != lastCurrent;

                if (changed)
                {
                    lastName = name;
                    lastTotal = total;
                    lastCurrent = current;
                    lastUpdateTime = DateTime.Now;

                    if (total == 0 && current == 0)
                    {
                        progressBar.Visible = false;
                        lblState.Text = $"{name}";
                        if (!this.Visible) this.Show();
                        else if (name == "done") this.Hide();

                        return;
                    }

                    progressBar.Visible = true;
                    int percent = Math.Min(100, (int)((double)current / total * 100));
                    progressBar.Value = percent;
                    lblState.Text = $"{name} ({current}/{total})";

                    if (!this.Visible) this.Show();
                }
            }
        }
    }
}
