using System;
using System.Windows.Forms;

namespace TaskbarAutoHider
{
    public partial class SettingsForm : Form
    {
        public event Action<int> TimeoutChanged;

        private NumericUpDown timeoutNumeric;
        private Label instructionLabel;
        private Button applyButton;
        private Button cancelButton;

        public SettingsForm(int currentTimeout)
        {
            InitializeComponent();

            // Validate input
            if (currentTimeout < 5 || currentTimeout > 3600)
                currentTimeout = 300; // Default to 5 minutes if invalid

            timeoutNumeric.Value = currentTimeout;
        }

        private void InitializeComponent()
        {
            this.Text = "Taskbar Auto-Hider Settings";
            this.Size = new System.Drawing.Size(350, 180);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;

            // Instruction label
            instructionLabel = new Label()
            {
                Text = "Set the idle time (in seconds) before auto-hiding the taskbar:",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(300, 40),
                AutoSize = false
            };

            // Numeric input with validation
            timeoutNumeric = new NumericUpDown()
            {
                Location = new System.Drawing.Point(20, 70),
                Size = new System.Drawing.Size(100, 25),
                Minimum = 5,      // Minimum 5 seconds
                Maximum = 3600,   // Maximum 1 hour (3600 seconds)
                Value = 300       // Default 5 minutes (300 seconds)
            };

            // Seconds label
            Label secondsLabel = new Label()
            {
                Text = "seconds",
                Location = new System.Drawing.Point(130, 73),
                Size = new System.Drawing.Size(50, 20)
            };

            // Apply button
            applyButton = new Button()
            {
                Text = "Apply",
                Location = new System.Drawing.Point(180, 110),
                Size = new System.Drawing.Size(70, 30)
            };
            applyButton.Click += ApplyButton_Click;

            // Cancel button
            cancelButton = new Button()
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(260, 110),
                Size = new System.Drawing.Size(70, 30)
            };
            cancelButton.Click += (s, e) => this.Hide();

            // Add controls
            this.Controls.AddRange(new Control[]
            {
                instructionLabel,
                timeoutNumeric,
                secondsLabel,
                applyButton,
                cancelButton
            });
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            try
            {
                int timeout = (int)timeoutNumeric.Value;
                TimeoutChanged?.Invoke(timeout);
                MessageBox.Show($"Timeout set to {timeout} seconds", "Settings Applied",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying settings: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value);
            if (value)
            {
                this.WindowState = FormWindowState.Normal;
            }
        }
    }
}
