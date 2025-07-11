using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BazaarPlannerModInstaller
{
    public partial class InstallerForm : Form
    {
        private TextBox installPathTextBox;
        private static string DEFAULT_INSTALL_PATH => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tempo Launcher - Beta",
            "game",
            "bazaarwinprodlatest"
        );
        private Button installButton;
        private Label instructionsLabel;
        private System.Windows.Forms.Timer configCheckTimer;

        public InstallerForm()
        {
            InitializeComponent();
            
            // Set form properties
            Text = "Bazaar Planner Mod Installer";
            ClientSize = new Size(550, 500);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            
            // Add logo
            PictureBox logoPictureBox = new PictureBox
            {
                Image = Image.FromStream(GetType().Assembly.GetManifestResourceStream("BazaarPlannerModInstaller.logo.png")),
                SizeMode = PictureBoxSizeMode.Zoom,
                Height = 300,
                Width = ClientSize.Width,
                Location = new Point(0, 10)
            };
            Controls.Add(logoPictureBox);

            // Add install path input
            Label pathLabel = new Label
            {
                Text = "Installation Directory:",
                Location = new Point(20, logoPictureBox.Bottom + 10),
                AutoSize = true
            };
            Controls.Add(pathLabel);

            installPathTextBox = new TextBox
            {
                Text = DEFAULT_INSTALL_PATH,
                Location = new Point(20, pathLabel.Bottom + 5),
                Width = 380
            };
            Controls.Add(installPathTextBox);

            Button browseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(installPathTextBox.Right + 10, installPathTextBox.Top - 2),
                AutoSize = true
            };
            browseButton.Click += BrowseButton_Click;
            Controls.Add(browseButton);

            // Initialize the instructions label and install button (but don't add them yet)
            instructionsLabel = new Label
            {
                Text = "Please click the gear on the top right of bazaarplanner.com\n" +
                      "Login if you are not yet logged in, and then click the 'Export BazaarPlanner.config' button\n" +
                      "which will download a file. Put this file in the same directory as your\n" +
                      "BazaarPlanner 3rd party installation tool, so it can be installed correctly\n" +
                      "and make requests on behalf of your bazaarplanner user.",
                Location = new Point(20, installPathTextBox.Bottom + 20),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter
            };

            installButton = new Button
            {
                Text = "Install Mod",
                Width = 180,
                Height = 30,
                Location = new Point((ClientSize.Width - 180) / 2, installPathTextBox.Bottom + 20),
                Visible = false
            };
            installButton.Click += InstallButton_Click;

            Controls.Add(instructionsLabel);
            Controls.Add(installButton);

            // Setup and start the timer to check for config file
            configCheckTimer = new System.Windows.Forms.Timer();
            configCheckTimer.Interval = 1000; // Check every second
            configCheckTimer.Tick += ConfigCheckTimer_Tick;
            configCheckTimer.Start();

            // Do initial check
            CheckForConfigFile();

            // Add FormClosing event handler
            this.FormClosing += InstallerForm_FormClosing;
        }

        private void InstallerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            configCheckTimer?.Stop();
            configCheckTimer?.Dispose();
        }

        private void ConfigCheckTimer_Tick(object sender, EventArgs e)
        {
            CheckForConfigFile();
        }

        private void CheckForConfigFile()
        {
            string workingConfigPath = Path.Combine(Application.StartupPath, "BazaarPlanner.config");
            string targetConfigPath = Path.Combine(installPathTextBox.Text, "BepInEx", "config", "BazaarPlanner.cfg");
            
            bool workingConfigExists = File.Exists(workingConfigPath);
            bool targetConfigExists = File.Exists(targetConfigPath);

            // If target config exists but working config doesn't, copy it
            if (targetConfigExists && !workingConfigExists)
            {
                try
                {
                    File.Copy(targetConfigPath, workingConfigPath);
                    workingConfigExists = true;
                }
                catch (Exception)
                {
                    // Silently fail if we can't copy the file
                }
            }
            
            // Only update UI if the state has changed
            if (workingConfigExists != installButton.Visible)
            {
                instructionsLabel.Visible = !workingConfigExists;
                installButton.Visible = workingConfigExists;
            }
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = installPathTextBox.Text;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    installPathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private bool HasWriteAccessToPath(string path)
        {
            try
            {
                // Try to create a temporary file to test write permissions
                string testFile = Path.Combine(path, "write_test.tmp");
                using (FileStream fs = File.Create(testFile))
                {
                    fs.Close();
                }
                File.Delete(testFile);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void InstallButton_Click(object sender, EventArgs e)
        {
            try
            {
                string installPath = installPathTextBox.Text;
                if (!Directory.Exists(installPath))
                {
                    MessageBox.Show("The specified installation directory does not exist.");
                    return;
                }

                // Check write access
                if (!HasWriteAccessToPath(installPath))
                {
                    MessageBox.Show(
                        "The installer needs administrator privileges to write to the game directory.\n\n" +
                        "Please restart the installer as administrator.",
                        "Admin Rights Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Check for config file existence
                string sourceConfigFile = Path.Combine(Application.StartupPath, "BazaarPlanner.config");
                if (!File.Exists(sourceConfigFile))
                {
                    DialogResult result = MessageBox.Show(
                        "You haven't generated a user config file yet. Your runs will not be tracked at all, but you can still press 'b' to load your board into the website. Would you like to proceed with the installation?",
                        "Missing Config File",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.No)
                    {
                        return;
                    }
                }

                // Extract BepInEx from embedded resource
                string tempBepinexZip = Path.GetTempFileName();
                using (var stream = GetType().Assembly.GetManifestResourceStream("BazaarPlannerModInstaller.BepInEx_win_x64_5.4.23.2.zip"))
                using (var fileStream = File.Create(tempBepinexZip))
                {
                    stream.CopyTo(fileStream);
                }
                System.IO.Compression.ZipFile.ExtractToDirectory(tempBepinexZip, installPath, true);
                File.Delete(tempBepinexZip);

                // Create plugins directory
                string pluginsPath = Path.Combine(installPath, "BepInEx", "plugins");
                Directory.CreateDirectory(pluginsPath);

                // Extract mod DLL from embedded resource
                string targetDll = Path.Combine(pluginsPath, "BazaarPlannerMod.dll");
                using (var stream = GetType().Assembly.GetManifestResourceStream("BazaarPlannerModInstaller.BazaarPlannerMod.dll"))
                using (var fileStream = File.Create(targetDll))
                {
                    stream.CopyTo(fileStream);
                }

                // Copy config file if it exists
                if (File.Exists(sourceConfigFile))
                {
                    string configPath = Path.Combine(installPath, "BepInEx", "config");
                    Directory.CreateDirectory(configPath);
                    string targetConfigFile = Path.Combine(configPath, "BazaarPlanner.cfg");
                    File.Copy(sourceConfigFile, targetConfigFile, true);
                }

                MessageBox.Show("Installation completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Application.Exit(); // Close the application after user clicks OK
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during installation: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
} 