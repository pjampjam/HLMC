using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
#pragma warning disable IL2026, CS8602

namespace HLMCUpdater
{
    public partial class MainForm : Form
    {
        // --- Configuration ---
        const string GitHubOwner = "pjampjam";
        const string GitHubRepo = "HLMC";
        const string GitHubBranch = "main";
        private static readonly string EmbeddedVersion = Assembly.GetExecutingAssembly().GetName().Version!.ToString();

        private string? _gitHubToken;
        private bool? _debugLoggingEnabled;
        private bool DebugLoggingEnabled
        {
            get
            {
                if (_debugLoggingEnabled == null)
                {
                    _debugLoggingEnabled = true;
                    // Load from config
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string appDataDir = Path.Combine(appData, "HLMCUpdater");
                    string configPath = Path.Combine(appDataDir, "config.json");
                    if (File.Exists(configPath))
                    {
                        try
                        {
                            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(configPath));
                            if (config != null && config.TryGetValue("DebugLoggingEnabled", out var debugEnabled) && bool.TryParse(debugEnabled, out bool result))
                            {
                                _debugLoggingEnabled = result;
                            }
                        }
                        catch { }
                    }
                }
                return _debugLoggingEnabled.Value;
            }
            set
            {
                _debugLoggingEnabled = value;
                // Save to config
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appDataDir = Path.Combine(appData, "HLMCUpdater");
                if (!Directory.Exists(appDataDir))
                {
                    Directory.CreateDirectory(appDataDir);
                }
                string configPath = Path.Combine(appDataDir, "config.json");
                var config = new Dictionary<string, string>();
                if (File.Exists(configPath))
                {
                    try
                    {
                        config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(configPath)) ?? new Dictionary<string, string>();
                    }
                    catch { }
                }
                config["GitHubToken"] = _gitHubToken ?? "";
                config["MinecraftPath"] = MinecraftPath;
                config["DebugLoggingEnabled"] = value.ToString();
                config["DiscordWebhookUrl"] = DiscordWebhookUrl;
                try
                {
                    File.WriteAllText(configPath, JsonSerializer.Serialize(config));
                }
                catch { }
            }
        }

        private string GitHubToken
        {
            get
            {
                if (_gitHubToken == null)
                {
                    _gitHubToken = "";
                    // Load from config
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string appDataDir = Path.Combine(appData, "HLMCUpdater");
                    string configPath = Path.Combine(appDataDir, "config.json");
                    if (File.Exists(configPath))
                    {
                        try
                        {
                            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(configPath));
                            if (config != null && config.TryGetValue("GitHubToken", out var token) && !string.IsNullOrEmpty(token))
                            {
                                _gitHubToken = token;
                            }
                        }
                        catch { }
                    }
                }
                return _gitHubToken;
            }
            set
            {
                _gitHubToken = value ?? "";
                // Save to config
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appDataDir = Path.Combine(appData, "HLMCUpdater");
                if (!Directory.Exists(appDataDir))
                {
                    Directory.CreateDirectory(appDataDir);
                }
                string configPath = Path.Combine(appDataDir, "config.json");
                var config = new Dictionary<string, string>();
                if (File.Exists(configPath))
                {
                    try
                    {
                        config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(configPath)) ?? new Dictionary<string, string>();
                    }
                    catch { }
                }
                config["GitHubToken"] = _gitHubToken;
                config["MinecraftPath"] = MinecraftPath; // Ensure MinecraftPath is also saved
                config["DebugLoggingEnabled"] = DebugLoggingEnabled.ToString();
                config["DiscordWebhookUrl"] = DiscordWebhookUrl;
                try
                {
                    File.WriteAllText(configPath, JsonSerializer.Serialize(config));
                }
                catch { }
            }
        }

        private bool PromptForGitHubToken()
{
    var result = MessageBox.Show(
        "GitHub API requests rate limit exceeded.\n\n" +
        "To ensure smooth updates, please provide a GitHub Personal Access Token.\n\n" +
        "This token will be stored securely in your app settings.\n\n" +
        "Would you like to enter a token now?",
        "Rate Limit Exceeded",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Warning
    );

            if (result == DialogResult.Yes)
            {
                string token = "";
                bool valid = false;
                while (!valid)
                {
                    using (var inputForm = new Form())
                    {
                        inputForm.Text = "Enter GitHub Personal Access Token";
                        inputForm.Size = new Size(400, 200);
                        inputForm.StartPosition = FormStartPosition.CenterParent;
                        inputForm.BackColor = Color.FromArgb(30, 30, 30);
                        inputForm.ForeColor = Color.White;
                        inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                        inputForm.MaximizeBox = false;
                        inputForm.MinimizeBox = false;

                        var label = new Label
                        {
                            Text = "Paste your GitHub Personal Access Token:\n\n" +
                                   "Create token at: https://github.com/settings/tokens\n\n" +
                                   "• Create an account or Log-in and then 'Generate new token'",
                            AutoSize = true,
                            Location = new Point(10, 10),
                            ForeColor = Color.White
                        };
                        inputForm.Controls.Add(label);

                        var textBox = new TextBox
                        {
                            UseSystemPasswordChar = true,
                            Location = new Point(10, 90),
                            Size = new Size(360, 20),
                            BackColor = Color.FromArgb(50, 50, 50),
                            ForeColor = Color.White,
                            BorderStyle = BorderStyle.FixedSingle
                        };
                        inputForm.Controls.Add(textBox);

                        var okButton = new Button
                        {
                            Text = "Save Token",
                            DialogResult = DialogResult.OK,
                            Location = new Point(180, 120),
                            Size = new Size(100, 30)
                        };
                        inputForm.Controls.Add(okButton);
                        inputForm.AcceptButton = okButton;

                        var cancelButton = new Button
                        {
                            Text = "Skip",
                            DialogResult = DialogResult.Cancel,
                            Location = new Point(290, 120),
                            Size = new Size(80, 30)
                        };
                        inputForm.Controls.Add(cancelButton);
                        inputForm.CancelButton = cancelButton;

                        if (inputForm.ShowDialog() == DialogResult.OK)
                        {
                            token = textBox.Text.Trim();
                            if (!string.IsNullOrEmpty(token) && token.Length >= 40) // GitHub tokens are typically 40+ chars
                            {
                                GitHubToken = token;
                                valid = true;
                                return true;
                            }
                            else
                            {
                                MessageBox.Show("Invalid token format. Please enter a valid GitHub Personal Access Token.", "Invalid Token", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        else
                        {
                            valid = true; // User cancelled, break loop
                        }
                    }
                }
            }
            return false;
        }

        private string? _discordWebhookUrl;
        private string DiscordWebhookUrl
        {
            get
            {
                if (_discordWebhookUrl == null)
                {
                    _discordWebhookUrl = "https://discord.com/api/webhooks/1414627892788334762/15hNMv6Bjc6UTz1ad54mwomTL0CN6mX93q_W2OUKsfMq-JQMayjZzSqeMnfXyeo7qh0g";
                    // Load from config
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string appDataDir = Path.Combine(appData, "HLMCUpdater");
                    string configPath = Path.Combine(appDataDir, "config.json");
                    if (File.Exists(configPath))
                    {
                        try
                        {
                            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(configPath));
                            if (config != null && config.TryGetValue("DiscordWebhookUrl", out var url) && !string.IsNullOrEmpty(url))
                            {
                                _discordWebhookUrl = url;
                            }
                        }
                        catch { }
                    }
                }
                return _discordWebhookUrl;
            }
            set
            {
                _discordWebhookUrl = value ?? "";
                // Save to config
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appDataDir = Path.Combine(appData, "HLMCUpdater");
                if (!Directory.Exists(appDataDir))
                {
                    Directory.CreateDirectory(appDataDir);
                }
                string configPath = Path.Combine(appDataDir, "config.json");
                var config = new Dictionary<string, string>();
                if (File.Exists(configPath))
                {
                    try
                    {
                        config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(configPath)) ?? new Dictionary<string, string>();
                    }
                    catch { }
                }
                config["GitHubToken"] = GitHubToken;
                config["MinecraftPath"] = MinecraftPath;
                config["DebugLoggingEnabled"] = DebugLoggingEnabled.ToString();
                config["DiscordWebhookUrl"] = _discordWebhookUrl;
                try
                {
                    File.WriteAllText(configPath, JsonSerializer.Serialize(config));
                }
                catch { }
            }
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        #pragma warning disable CS8618
        private static string _programVersion;
        private static string ProgramVersion => _programVersion;
        #pragma warning restore CS8618

        Panel welcomePanel, progressPanel, summaryPanel;
        Label titleLabel, creditLabel, versionLabel, statusLabel, downloadProgressLabel, summaryTitleLabel, summaryCountLabel;
        Label updateStatusLabel;
        Button startButton, closeButton, cancelButton;
        ProgressBar downloadProgressBar;
        TextBox summaryTextBox;
        CancellationTokenSource _cts;
        private List<string> currentDownloads = new List<string>();

        private string GetWindowsVersion()
        {
            // Method 1: Check build number first (most reliable for Win11 detection)
            try
            {
                var osVersion = Environment.OSVersion.Version;
                if (osVersion.Build >= 22000) return "Win11";
                if (osVersion.Build >= 10240 && osVersion.Major == 10) return "Win10";
                if (osVersion.Build >= 9200) return "Win81";
                if (osVersion.Build >= 7600) return "Win7";
            }
            catch { }

            try
            {
                // Method 2: Check registry for additional version info
                Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (key != null)
                {
                    // Get Windows version from registry as fallback
                    string? productName = key.GetValue("ProductName") as string;
                    if (!string.IsNullOrEmpty(productName))
                    {
                        if (productName.Contains("11")) return "Win11";
                        if (productName.Contains("10")) return "Win10";
                        if (productName.Contains("8.1") || productName.Contains("8.1")) return "Win81";
                        if (productName.Contains("8")) return "Win8";
                        if (productName.Contains("7")) return "Win7";
                        if (productName.Contains("Vista")) return "Vista";
                        if (productName.Contains("XP")) return "XP";
                    }

                    // Check CurrentMajorVersionNumber for more reliable detection
                    int? majorVersion = key.GetValue("CurrentMajorVersionNumber") as int?;
                    int? minorVersion = key.GetValue("CurrentMinorVersionNumber") as int?;
                    string? currentBuild = key.GetValue("CurrentBuild") as string;

                    // Windows 11 has major version 10, but build >= 22000
                    if (majorVersion.HasValue && majorVersion.Value == 10 && !string.IsNullOrEmpty(currentBuild))
                    {
                        if (int.TryParse(currentBuild, out int buildNum))
                        {
                            if (buildNum >= 22000) return "Win11";
                            return "Win10";
                        }
                    }
                }
            }
            catch { }

            // Skip WMI method for compatibility

            // Last resort
            try
            {
                string osInfo = Environment.OSVersion.VersionString.ToLowerInvariant();
                if (osInfo.Contains("11")) return "Win11";
                if (osInfo.Contains("10")) return "Win10";
                if (osInfo.Contains("8.1")) return "Win81";
                if (osInfo.Contains("8")) return "Win8";
                if (osInfo.Contains("7")) return "Win7";
                return "Win" + Environment.OSVersion.Version.Major.ToString();
            }
            catch { }

            return "WinUnknown";
        }

        private string DeviceIdentifier
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appDataDir = Path.Combine(appData, "HLMCUpdater");
                string deviceIdFile = Path.Combine(appDataDir, ".device_id");

                if (File.Exists(deviceIdFile))
                {
                    try
                    {
                        string stored = File.ReadAllText(deviceIdFile).Trim();
                        // If stored ID has a prefix (old format), extract just the letters/numbers part
                        if (stored.Contains('_'))
                        {
                            string[] parts = stored.Split('_');
                            if (parts.Length >= 2 && parts[1].Length == 8 && parts[1].All(c => char.IsLetterOrDigit(c)))
                            {
                                return parts[1];
                            }
                        }
                        return stored;
                    }
                    catch { }
                }

                // Generate new device ID with 8 alphanumeric characters
                string randomCombo = Guid.NewGuid().ToString().Substring(0, 8).ToUpperInvariant();
                char[] validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
                string deviceId = "";
                foreach (char c in randomCombo)
                {
                    if (validChars.Contains(c))
                    {
                        deviceId += c;
                    }
                }
                if (deviceId.Length < 8)
                {
                    // Pad with random valid characters if needed
                    Random rnd = new Random();
                    while (deviceId.Length < 8)
                    {
                        deviceId += validChars[rnd.Next(validChars.Length)];
                    }
                }

                // Store it for subsequent log files
                try
                {
                    if (!Directory.Exists(appDataDir))
                    {
                        Directory.CreateDirectory(appDataDir);
                    }
                    File.WriteAllText(deviceIdFile, deviceId);
                }
                catch { }

                return deviceId;
            }
        }

        private string MinecraftPath
        {
            get
            {
                string exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

                // Auto-detect if exe is in a Minecraft folder (has mods and config dirs)
                if (Directory.Exists(Path.Combine(exeDir, "mods")) && Directory.Exists(Path.Combine(exeDir, "config")))
                {
                    return exeDir;
                }

                // Try to get from preferred config location (appdata)
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appDataDir = Path.Combine(appData, "HLMCUpdater");
                string configPath = Path.Combine(appDataDir, "config.json");
                if (File.Exists(configPath))
                {
                    try
                    {
                        var config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(configPath));
                        if (config != null && config.TryGetValue("MinecraftPath", out var path) && !string.IsNullOrEmpty(path) && Directory.Exists(path))
                        {
                            // Validate the saved path still exists
                            if (Directory.Exists(Path.Combine(path, "mods")) && Directory.Exists(Path.Combine(path, "config")))
                            {
                                return path;
                            }
                        }
                    }
                    catch { } // Ignore deserialization errors
                }

                // Try legacy config in exe directory
                string legacyConfigPath = Path.Combine(exeDir, "config.json");
                if (File.Exists(legacyConfigPath))
                {
                    try
                    {
                        var config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(legacyConfigPath));
                        if (config != null && config.TryGetValue("MinecraftPath", out var path) && !string.IsNullOrEmpty(path) && Directory.Exists(path))
                        {
                            if (Directory.Exists(Path.Combine(path, "mods")) && Directory.Exists(Path.Combine(path, "config")))
                            {
                                return path;
                            }
                        }
                    }
                    catch { }
                }

                // Prompt user to select the Minecraft modpack folder
                using (var folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select your Minecraft modpack folder";
                    folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                    string selectedPath = "";
                    bool validFolder = false;

                    do
                    {
                        if (folderDialog.ShowDialog() == DialogResult.OK)
                        {
                            selectedPath = folderDialog.SelectedPath;
                            if (Directory.Exists(Path.Combine(selectedPath, "mods")) && Directory.Exists(Path.Combine(selectedPath, "config")))
                            {
                                // Additional validation: check if it's writable
                                try
                                {
                                    string testFile = Path.Combine(selectedPath, "HLMC_test.tmp");
                                    File.WriteAllText(testFile, "test");
                                    File.Delete(testFile);
                                    validFolder = true;
                                    break;
                                }
                                catch
                                {
                                    MessageBox.Show("The selected folder is not writable. Please select a different folder.", "Invalid Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                            }
                            else
                            {
                                MessageBox.Show("The selected folder does not appear to be a valid Minecraft modpack folder. Please select a folder containing 'mods' and 'config' directories.", "Invalid Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Minecraft path is required. Application will exit.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                            return "";
                        }
                    } while (!validFolder);

                    // Save the selected path to config
                    this.MinecraftPath = selectedPath;
                    return selectedPath;
                }
            }
            set
            {
                // Save to preferred config location (appdata)
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string configDir = Path.Combine(appData, "HLMCUpdater");
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                string configPath = Path.Combine(configDir, "config.json");
                var config = new Dictionary<string, string>
                {
                    ["GitHubToken"] = GitHubToken,
                    ["MinecraftPath"] = value,
                    ["DebugLoggingEnabled"] = DebugLoggingEnabled.ToString(),
                    ["DiscordWebhookUrl"] = DiscordWebhookUrl
                };
                try
                {
                    File.WriteAllText(configPath, JsonSerializer.Serialize(config));
                }
                catch
                {
                    // Try fallback to exe directory
                    string exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                    string fallbackConfig = Path.Combine(exeDir, "config.json");
                    try
                    {
                        File.WriteAllText(fallbackConfig, JsonSerializer.Serialize(config));
                    }
                    catch { } // If fallback also fails, ignore
                }
            }
        }

        private string GetMinecraftPathWithoutPrompt()
        {
            string exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

            // Auto-detect if exe is in a Minecraft folder (has mods and config dirs)
            if (Directory.Exists(Path.Combine(exeDir, "mods")) && Directory.Exists(Path.Combine(exeDir, "config")))
            {
                return exeDir;
            }

            // Try to get from config file
            string configPath = Path.Combine(exeDir, "config.json");
            if (File.Exists(configPath))
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(configPath));
                if (config != null && config.TryGetValue("MinecraftPath", out var path) && !string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }

            return exeDir; // Default to exe dir
        }

        public MainForm()
        {
            try
            {
                _cts = new CancellationTokenSource();

                // --- Main Form Setup ---
                this.Text = "HLMC Mod Updater";
                this.Size = new Size(650, 350); // Reduced size, increased height for better button spacing
                this.StartPosition = FormStartPosition.CenterScreen;
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.MaximizeBox = false;
                this.BackColor = Color.FromArgb(30, 30, 30);
            }
            catch (Exception ex)
            {
                LogException(ex);
                throw;
            }

            // --- Welcome View Controls ---
            welcomePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(40, 40, 40) };
            this.Controls.Add(welcomePanel);
            welcomePanel.Visible = false; // Hide until update check is done

            titleLabel = new Label
            {
                Text = "Holy Lois Client Updater",
                Font = new Font("Arial", 24, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Top,
                ForeColor = Color.Gold
            };
            welcomePanel.Controls.Add(titleLabel);

            creditLabel = new Label
            {
                Text = "created by pjampjam ( ͡° ͜ʖ ͡°)",
                Font = new Font("Arial", 10),
                AutoSize = true,
                Anchor = AnchorStyles.Top,
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            welcomePanel.Controls.Add(creditLabel);

            startButton = new Button
            {
                Text = "Update Modpack",
                Size = new Size(220, 50),
                Font = new Font("Arial", 13, FontStyle.Bold),
                Anchor = AnchorStyles.Top,
                BackColor = Color.FromArgb(60, 180, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            startButton.FlatAppearance.BorderColor = Color.FromArgb(70, 120, 70);
            startButton.FlatAppearance.BorderSize = 2;
            startButton.Click += StartButton_Click;
            welcomePanel.Controls.Add(startButton);

            // Removed checkUpdaterButton and browseButton for simplified UI

            versionLabel = new Label
            {
                Text = EmbeddedVersion,
                Font = new Font("Arial", 10),
                AutoSize = true,
                Anchor = AnchorStyles.Bottom,
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            welcomePanel.Controls.Add(versionLabel);

            updateStatusLabel = new Label
            {
                Text = "",
                Font = new Font("Arial", 12, FontStyle.Bold),
                AutoSize = true,
                ForeColor = Color.Gold,
                Location = new Point(10, 10),
                Anchor = AnchorStyles.Top
            };
            welcomePanel.Controls.Add(updateStatusLabel);
            CenterControlX(updateStatusLabel, welcomePanel);

            // --- Progress View Controls ---
            progressPanel = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(40, 40, 40) };
            this.Controls.Add(progressPanel);

            statusLabel = new Label
            {
                Text = "Initializing...",
                Font = new Font("Arial", 14, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Top,
                Location = new Point(0, 100),
                ForeColor = Color.Gold
            };
            progressPanel.Controls.Add(statusLabel);

            downloadProgressBar = new ProgressBar
            {
                Size = new Size(500, 25),
                Anchor = AnchorStyles.Top,
                Location = new Point(40, 380),
                Visible = false
            };
            progressPanel.Controls.Add(downloadProgressBar);

            downloadProgressLabel = new Label
            {
                Anchor = AnchorStyles.Top,
                Location = new Point(0, 200),
                Font = new Font("Arial", 9),
                Text = "",
                Visible = false,
                ForeColor = Color.White,
                AutoSize = true
            };
            progressPanel.Controls.Add(downloadProgressLabel);

            cancelButton = new Button
            {
                Text = "Cancel",
                Size = new Size(120, 40),
                Font = new Font("Arial", 10),
                Anchor = AnchorStyles.Bottom,
                BackColor = Color.FromArgb(180, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point((progressPanel.Width - 120) / 2, progressPanel.Height)
            };
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
            cancelButton.FlatAppearance.BorderSize = 2;
            cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 80, 80);
            cancelButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(150, 40, 40);
            cancelButton.MouseEnter += (s, e) => cancelButton.BackColor = Color.FromArgb(200, 80, 80);
            cancelButton.MouseLeave += (s, e) => cancelButton.BackColor = Color.FromArgb(180, 60, 60);
            cancelButton.Click += async (s, e) => await CancelDownload(s, e);
            progressPanel.Controls.Add(cancelButton);

            // --- Summary View Controls ---
            summaryPanel = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(40, 40, 40) };
            this.Controls.Add(summaryPanel);

            summaryTitleLabel = new Label
            {
                Text = "Update Complete!",
                Font = new Font("Arial", 16, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Top,
                Location = new Point(0, 20),
                ForeColor = Color.Gold
            };
            summaryPanel.Controls.Add(summaryTitleLabel);

            summaryCountLabel = new Label
            {
                Text = "",
                Font = new Font("Arial", 10),
                AutoSize = true,
                Anchor = AnchorStyles.Top,
                Location = new Point(0, 50),
                ForeColor = Color.White
            };
            summaryPanel.Controls.Add(summaryCountLabel);

            summaryTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Arial", 9),
                Size = new Size(460, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point((summaryPanel.Width - 460) / 2, 200),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40), // Match panel background
                BorderStyle = BorderStyle.None // Remove border
            };
            summaryPanel.Controls.Add(summaryTextBox);

            closeButton = new Button
            {
                Text = "Close",
                Size = new Size(150, 40),
                Font = new Font("Arial", 10),
                Anchor = AnchorStyles.Bottom,
                BackColor = Color.FromArgb(60, 180, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point((summaryPanel.Width - 120) / 2, summaryPanel.Height)
            };
            closeButton.Click += CloseButton_Click;
            closeButton.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
            closeButton.FlatAppearance.BorderSize = 2;
            summaryPanel.Controls.Add(closeButton);

            this.FormClosing += (s, e) =>
            {
                try
                {
                    // Final cleanup before shutdown
                    CleanupOrphanedFiles();

                    // Clean up temp directory if exists
                    string tempDir = Path.Combine(Path.GetTempPath(), "HLMC_Updater_Temp");
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }

                _cts?.Cancel();
                Application.Exit();
            };

            this.Load += MainForm_Load;
            this.Resize += new EventHandler(this.MainForm_Resize);
        }
        private void LogException(Exception ex)
        {
            // Check debug logging setting
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appDataDir = Path.Combine(appData, "HLMCUpdater");
                string configPath = Path.Combine(appDataDir, "config.json");
                if (File.Exists(configPath))
                {
                    var config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(configPath));
                    if (config != null && config.TryGetValue("DebugLoggingEnabled", out var debugEnabled) && bool.TryParse(debugEnabled, out bool result) && result)
                    {
                        string deviceId = DeviceIdentifier;
                        string logPath = Path.Combine(appDataDir, $"{deviceId}_error.log");
                        File.WriteAllText(logPath, $"{DateTime.Now}: {ex.ToString()}");
                        return;
                    }
                }
            }
            catch
            {
                // If logging fails, ignore
            }
            // If debug logging is disabled or loading failed, don't log
        }

        private async Task CancelDownload(object? sender, EventArgs e)
        {
            cancelButton.Enabled = false;
            UpdateStatus("Stopping...");
            _cts?.Cancel();

            // Delete currently downloading files with retry
            foreach (var file in currentDownloads)
            {
                UpdateStatus("Deleting " + Path.GetFileName(file));
                int attempts = 0;
                while (attempts < 10 && File.Exists(file))
                {
                    try
                    {
                        File.Delete(file);
                        break;
                    }
                    catch
                    {
                        attempts++;
                        if (attempts < 10)
                        {
                            await Task.Delay(200);
                        }
                    }
                }
            }
            currentDownloads.Clear();

            UpdateStatus("Deletion successful");

            // Force immediate cleanup
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "HLMC_Updater_Temp");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }

                // Also clean up potentially corrupted downloads in main directories
                string scriptRoot = MinecraftPath;
                if (!string.IsNullOrEmpty(scriptRoot))
                {
                    CleanupIncompleteDownloads(scriptRoot, "mods", "*.jar");
                    CleanupIncompleteDownloads(scriptRoot, "resourcepacks", "*.zip");
                }
            }
            catch { }

            // Give cleanup operations time to complete before exiting
            await Task.Delay(500);

            Application.Exit();
        }

        private void CleanupIncompleteDownloads(string minecraftPath, string folderName, string filePattern)
        {
            try
            {
                string targetDir = Path.Combine(minecraftPath, folderName);
                if (!Directory.Exists(targetDir)) return;

                var files = Directory.GetFiles(targetDir, filePattern);
                foreach (var file in files)
                {
                    try
                    {
                        // Check if file seems incomplete (very small size or recently modified)
                        FileInfo fi = new FileInfo(file);
                        long fileSize = fi.Length;

                        // If file is very small (< 100KB) or was modified in the last 30 seconds, it's likely incomplete
                        if (fileSize < 100 * 1024 || (DateTime.Now - fi.LastWriteTime).TotalSeconds < 30)
                        {
                            File.Delete(file);
                        }
                    }
                    catch { } // Ignore errors for individual files
                }
            }
            catch { } // Ignore directory-level errors
        }

        private string GitHubUriEncodeFileName(string fileName)
        {
            // Use proper URL encoding for GitHub URLs - this handles all special characters safely
            // GitHub raw and API URLs need proper encoding for special characters in filenames
            return Uri.EscapeDataString(fileName);
        }

        private void CleanupOrphanedFiles()
        {
            try
            {
                string exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                string? currentExe = Environment.ProcessPath;

                if (string.IsNullOrEmpty(currentExe)) return;

                string currentExeName = Path.GetFileName(currentExe);

                // Clean up old backup exe
                string backupPattern = Path.GetFileNameWithoutExtension(currentExeName) + ".old.exe";
                string backupPath = Path.Combine(exeDir, backupPattern);
                if (File.Exists(backupPath))
                {
                    try
                    {
                        FileInfo currentFi = new FileInfo(currentExe);
                        FileInfo backupFi = new FileInfo(backupPath);
                        if (backupFi.LastWriteTime < currentFi.LastWriteTime)
                        {
                            File.Delete(backupPath);
                        }
                    }
                    catch { }
                }

                // Clean up partial new exe files
                string newPattern = Path.GetFileNameWithoutExtension(currentExeName) + ".new.exe";
                string newPath = Path.Combine(exeDir, newPattern);
                if (File.Exists(newPath))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(newPath);
                        // Delete if older than 1 hour (left from failed update)
                        if ((DateTime.Now - fi.LastWriteTime).TotalHours > 1)
                        {
                            File.Delete(newPath);
                        }
                    }
                    catch { }
                }

                // Clean up temp files in exe directory
                try
                {
                    foreach (var file in Directory.GetFiles(exeDir, "*.temp"))
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(file);
                            if ((DateTime.Now - fi.LastWriteTime).TotalHours > 1)
                            {
                                File.Delete(file);
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                // Clean up corrupted or incomplete downloads
                try
                {
                    foreach (var file in Directory.GetFiles(exeDir, "*.tmp"))
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(file);
                            if (fi.Length == 0 || (DateTime.Now - fi.LastWriteTime).TotalHours > 1)
                            {
                                File.Delete(file);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            catch { } // Ignore all errors during cleanup
        }

        private async Task<RateLimitInfo> CheckGitHubRateLimit()
        {
            var info = new RateLimitInfo();

            try
            {
                using (var testClient = new HttpClient())
                {
                    testClient.DefaultRequestHeaders.UserAgent.ParseAdd("HLMCUpdater-RateLimitCheck");

                    // Make a simple test request to get rate limit headers
                    var response = await testClient.GetAsync("https://api.github.com/rate_limit");
                    if (response != null)
                    {
                        var limits = response.Content.ReadAsStringAsync().Result;

                        try
                        {
                            var rateLimitData = JsonSerializer.Deserialize<JsonElement>(limits);
                            var resources = rateLimitData.GetProperty("resources");

                            if (resources.TryGetProperty("core", out var core))
                            {
                                if (core.TryGetProperty("limit", out var limit))
                                    info.Limit = limit.GetInt32();

                                if (core.TryGetProperty("remaining", out var remaining))
                                    info.Remaining = remaining.GetInt32();

                                if (core.TryGetProperty("reset", out var reset))
                                {
                                    // Unix timestamp
                                    info.ResetTime = DateTimeOffset.FromUnixTimeSeconds(reset.GetInt64());
                                }
                            }
                        }
                        catch (Exception parseEx)
                        {
                            LogException(new Exception($"Failed to parse rate limit response: {parseEx.Message}"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogException(new Exception($"Rate limit check failed: {ex.Message}"));
                // If we can't check, assume no rate limit
                info.Remaining = 1000;
                info.Limit = 5000;
            }

            return info;
        }

        private void MigrateOldConfigFiles()
        {
            try
            {
                string exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string configDir = Path.Combine(appData, "HLMCUpdater");

                // Migrate old config.json from exe directory
                string oldConfig = Path.Combine(exeDir, "config.json");
                string newConfig = Path.Combine(configDir, "config.json");
                if (File.Exists(oldConfig) && !File.Exists(newConfig))
                {
                    if (!Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }
                    File.Move(oldConfig, newConfig);
                }

                // Migrate old repoTreeCache.json from exe directory
                string oldCache = Path.Combine(exeDir, "repoTreeCache.json");
                string newCache = Path.Combine(configDir, "repoTreeCache.json");
                if (File.Exists(oldCache) && !File.Exists(newCache))
                {
                    File.Move(oldCache, newCache);
                }
            }
            catch { } // Ignore migration errors
        }

        private bool IsDotNet90DesktopRuntimeInstalled()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd",
                        Arguments = "/c dotnet --list-runtimes",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output.Contains("Microsoft.WindowsDesktop.App 9.0");
            }
            catch
            {
                return true; // If dotnet is not found, assume it's available since the app is running
            }
        }

        private async Task EnsureDotNetRuntime()
        {
            if (!IsDotNet90DesktopRuntimeInstalled())
            {
                DialogResult result = MessageBox.Show("The .NET 9.0 Desktop Runtime is required but not installed. Would you like to download and install it now?", "Missing .NET Runtime", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    string downloadUrl = "https://download.visualstudio.microsoft.com/download/pr/106f8636-9352-4d71-99f3-0f3b4fe7a2d6/64b40fa1d5fe2850c0cf5e9aaa1b5e9/windowsdesktop-runtime-9.0.8-win-x64.exe"; // v9.0.8
                    string installerPath = Path.Combine(Path.GetTempPath(), "net90-desktop-runtime.exe");
                    try
                    {
                        UpdateStatus("Downloading .NET Runtime...");
                        using (var client2 = new HttpClient())
                        {
                            var response = await client2.GetAsync(downloadUrl);
                            response.EnsureSuccessStatusCode();
                            using (var stream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = File.OpenWrite(installerPath))
                            {
                                await stream.CopyToAsync(fileStream);
                            }
                        }
                        UpdateStatus("Installing .NET Runtime... (This may take a few minutes)");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = installerPath,
                            Arguments = "/quiet /norestart",
                            UseShellExecute = true
                        }).WaitForExit();
                        MessageBox.Show("Installation completed. Please restart the application.", "Installation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Application.Exit();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to install .NET Runtime: {ex.Message}. Please download manually from https://dotnet.microsoft.com/download/dotnet/9.0", "Installation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("The application cannot continue without the required runtime. Exiting.", "Runtime Required", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                }
            }
        }

        private async Task AnimateStatusSlideUp()
        {
            const int slideDistance = 50; // Move up by 50 pixels
            const int animationDuration = 800; // 800ms animation
            const int steps = 20; // Number of animation steps
            const int stepDelay = animationDuration / steps;

            int originalY = updateStatusLabel.Location.Y;
            int targetY = originalY - slideDistance;

            // Animate upward movement
            for (int i = 0; i < steps; i++)
            {
                double progress = (double)i / (steps - 1);
                int currentY = (int)(originalY - (slideDistance * progress));

                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        updateStatusLabel.Location = new Point(updateStatusLabel.Location.X, currentY);
                    }));
                }
                else
                {
                    updateStatusLabel.Location = new Point(updateStatusLabel.Location.X, currentY);
                }

                await Task.Delay(stepDelay);
            }

            // Ensure final position
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    updateStatusLabel.Location = new Point(updateStatusLabel.Location.X, targetY);
                }));
            }
            else
            {
                updateStatusLabel.Location = new Point(updateStatusLabel.Location.X, targetY);
            }

            // Brief pause before hiding
            await Task.Delay(200);
        }

        private void CloseButton_Click(object? sender, EventArgs e)
        {
            // Check if this is an error report
            if (closeButton.Text == "Report and Close")
            {
                GenerateErrorReport();
            }

            Application.Exit();
        }

        private async Task<bool> SendViaDiscordWebhook(List<string> errorFiles, string deviceId, string timestamp, string errorDetails)
        {
            if (string.IsNullOrEmpty(DiscordWebhookUrl))
            {
                return false;
            }

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10); // 10 second timeout

                    var payload = new
                    {
                        embeds = new[]
                        {
                            new
                            {
                                title = "HLMC Updater Error Report",
                                color = 16711680, // Red color
                                fields = new[]
                                {
                                    new { name = "Device ID", value = deviceId, inline = true },
                                    new { name = "Timestamp", value = timestamp, inline = true },
                                    new { name = "Error Details", value = errorDetails.Length > 500 ? errorDetails.Substring(0, 500) + "..." : errorDetails, inline = false }
                                }
                            }
                        }
                    };

                    string jsonPayload = JsonSerializer.Serialize(payload);

                    using (var content = new MultipartFormDataContent())
                    {
                        content.Add(new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json"), "payload_json");

                        // Attach error files
                        int fileIndex = 0;
                        foreach (var filePath in errorFiles.Where(File.Exists))
                        {
                            var fileName = Path.GetFileName(filePath);
                            var fileStream = File.OpenRead(filePath);
                            var fileContent = new StreamContent(fileStream);
                            content.Add(fileContent, $"files[{fileIndex}]", fileName);
                            fileIndex++;
                        }

                        var response = await httpClient.PostAsync(DiscordWebhookUrl, content);
                        response.EnsureSuccessStatusCode();

                        // Clean up file streams
                        foreach (var item in content)
                        {
                            if (item is StreamContent streamContent)
                            {
                                await streamContent.ReadAsStreamAsync().ContinueWith(t =>
                                {
                                    if (t.IsCompletedSuccessfully && t.Result != null)
                                    {
                                        t.Result.Dispose();
                                    }
                                });
                            }
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                LogException(ex);
                return false;
            }
        }

        private async void GenerateErrorReport()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appDataDir = Path.Combine(appData, "HLMCUpdater");

                List<string> errorFiles = new List<string>();
                string deviceId = DeviceIdentifier;
                string[] possibleFiles = {
                    $"{deviceId}_error.log",
                    $"{deviceId}_main_error.txt",
                    $"{deviceId}_update_check_results.txt",
                    $"main_error.txt", // Keep old format for backward compatibility
                    $"update_check_results.txt"
                };

                // Check for debug files as well
                if (Directory.Exists(appDataDir))
                {
                    foreach (var file in possibleFiles)
                    {
                        string filePath = Path.Combine(appDataDir, file);
                        if (File.Exists(filePath))
                        {
                            errorFiles.Add(filePath);
                        }
                    }

                    // Also add sync debug files with device prefix
                    foreach (var file in Directory.GetFiles(appDataDir, $"{deviceId}_sync_debug_*.txt"))
                    {
                        errorFiles.Add(file);
                    }
                }

                // Attempt Discord webhook send
                bool webhookSent = false;
                if (errorFiles.Any())
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string errorDetails = $"Error report generated. Debug logging found {errorFiles.Count()} file(s): {string.Join(", ", errorFiles.Select(Path.GetFileName))}";

                    // Use default webhook URL if not set
                    if (string.IsNullOrEmpty(DiscordWebhookUrl))
                    {
                        // Just use provided webhook - no prompt needed since it will just fail silently if invalid
                        DiscordWebhookUrl = "https://discord.com/api/webhooks/1414627892788334762/15hNMv6Bjc6UTz1ad54mwomTL0CN6mX93q_W2OUKsfMq-JQMayjZzSqeMnfXyeo7qh0g";
                    }

                    // Send via webhook if URL is configured
                    if (!string.IsNullOrEmpty(DiscordWebhookUrl))
                    {
                        // Show sending progress
                        using (var progressForm = new Form())
                        {
                            progressForm.Text = "Sending Report";
                            progressForm.Size = new Size(300, 100);
                            progressForm.StartPosition = FormStartPosition.CenterParent;
                            progressForm.BackColor = Color.FromArgb(30, 30, 30);
                            progressForm.ForeColor = Color.White;
                            progressForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                            progressForm.ControlBox = false;

                            var label = new Label
                            {
                                Text = "Sending report to Discord...",
                                AutoSize = true,
                                Location = new Point(20, 20),
                                ForeColor = Color.White
                            };
                            progressForm.Controls.Add(label);

                            progressForm.Show();
                            progressForm.Refresh();

                            webhookSent = await SendViaDiscordWebhook(errorFiles, deviceId, timestamp, errorDetails);

                            progressForm.Hide();
                        }
                    }

                    if (webhookSent)
                    {
                        MessageBox.Show(
                            "✅ Error report successfully sent to Discord webhook!\n\n" +
                            $"📍 Location: {appDataDir}\n\n" +
                            "The debug logs and files have been automatically shared with the development team.",
                            "Report Sent Successfully",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );

                        // Still open the directory for user's reference
                        try
                        {
                            Process.Start("explorer.exe", appDataDir);
                        }
                        catch { }
                    }
                    else if (errorFiles.Any())
                    {
                        MessageBox.Show(
                            "Error files found!\n\n" +
                            "Please share the following files for diagnosis:\n" +
                            $"- {string.Join("\n- ", errorFiles.Select(Path.GetFileName))}\n\n" +
                            $"📍 Location: {appDataDir}\n\n" +
                            "📋 Publishing Options:\n" +
                            "• Create a new GitHub Issue:\n" +
                            "   https://github.com/pjampjam/HLMC/issues/new\n" +
                            "• Anonymous file upload sites (no sign-in required):\n" +
                            "   • https://pastebin.com/ (API-friendly)\n" +
                            "   • https://0bin.net/ (encrypted, anonymous)\n" +
                            "   • https://gromble.io/ (file hosting)\n" +
                            "• Free paste sites:\n" +
                            "   • https://hastebin.com/\n" +
                            "   • https://gist.github.com/\n\n" +
                            "☕️ Help support the project by helping with bug reports!",
                            "Error Report Generated",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );

                        // Open the directory for user to easily access files
                        try
                        {
                            Process.Start("explorer.exe", appDataDir);
                        }
                        catch { }
                    }
                    else
                    {
                        MessageBox.Show(
                            "Debug logging disabled - no error files generated.\n\n" +
                            "To enable detailed error reporting:\n" +
                            "1. Open config.json\n" +
                            "2. Set \"DebugLoggingEnabled\": \"true\"\n" +
                            "3. Restart and try again\n\n" +
                            $"📍 Config location: {appDataDir}",
                            "Debug Logging Disabled",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error generating report: {ex.Message}\n\n" +
                    "Please manually locate error files in:\n" +
                    $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\HLMCUpdater",
                    "Report Generation Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts?.Dispose();
            }
            base.Dispose(disposing);
        }
        private async void StartButton_Click(object? sender, EventArgs e)
        {
            _cts = new CancellationTokenSource();

            welcomePanel.Visible = false;
            progressPanel.Visible = true;
            startButton.Enabled = false;

            string scriptRoot = MinecraftPath;
            if (string.IsNullOrEmpty(scriptRoot))
            {
                MessageBox.Show("Minecraft path is not set. Please set the Minecraft folder first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string tempDir = Path.Combine(Path.GetTempPath(), "HLMC_Updater_Temp");
            var results = new SyncResults();

            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                UpdateStatus("Fetching repository file list...");
                var repoTree = await FetchRepoTree();

                // Only check rate limit if we suspect it might be an issue (<= 10 requests)
                RateLimitInfo? initialRateLimitInfo = null;
                if (!string.IsNullOrEmpty(GitHubToken) || new Random().Next(10) == 0) // 10% chance check without token
                {
                    UpdateStatus("🔍 Checking GitHub rate limit status...");
                    initialRateLimitInfo = await CheckGitHubRateLimit();

                    if (initialRateLimitInfo.Remaining.HasValue && initialRateLimitInfo.Limit.HasValue)
                    {
                        if (initialRateLimitInfo.Remaining.Value <= 10) // Only show warning when critically low
                        {
                            string rateDisplay = $"GitHub API: {initialRateLimitInfo.Remaining.Value}/{initialRateLimitInfo.Limit.Value} requests ⚠️ CRITICAL!";
                            UpdateStatus(rateDisplay);
                            await Task.Delay(1000); // Show briefly
                        }
                    }
                }

                if (initialRateLimitInfo != null && (initialRateLimitInfo.IsLimited || (initialRateLimitInfo.Remaining.HasValue && initialRateLimitInfo.Remaining.Value == 0)))
                {
                    // Handle fully exhausted rate limit same way as existing code
                    UpdateStatus("🚫 RATE LIMITED! GitHub API requests exhausted");

                    if (initialRateLimitInfo.ResetTime.HasValue)
                    {
                        var resetTime = initialRateLimitInfo.ResetTime.Value;
                        var waitTime = resetTime - DateTimeOffset.Now;

                        if (waitTime.TotalSeconds > 0)
                        {
                            DialogResult addTokenResult = MessageBox.Show(
                                $"🔥 GitHub has blocked your requests due to rate limiting!\n\n" +
                                $"• Rate limit will reset at: {resetTime:G}\n" +
                                $"• Estimated wait time: {Math.Ceiling(waitTime.TotalMinutes):F0} minutes\n\n" +
                                $"SOLUTION: Add a GitHub Personal Access Token to avoid rate limits\n" +
                                $"📋 Create at: https://github.com/settings/tokens\n\n" +
                                "Would you like to add a token now?",
                                "GitHub Rate Limit Exceeded!",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning
                            );

                            if (addTokenResult == DialogResult.Yes)
                            {
                                if (PromptForGitHubToken())
                                {
                                    UpdateStatus("Token added - continuing update...");
                                    // Don't return, continue with the update
                                }
                                else
                                {
                                    Application.Exit();
                                    return;
                                }
                            }
                            else
                            {
                                Application.Exit();
                                return;
                            }
                        }
                    }

                    // Fallback if no reset time available
                    DialogResult fallbackResult = MessageBox.Show(
                        "🚫 GitHub API rate limit exceeded!\n\n" +
                        "This is likely why you're getting error 404.\n\n" +
                        "SOLUTIONS:\n" +
                        "• Add a GitHub Personal Access Token (recommended)\n" +
                        "• Wait 1 hour for automatic reset\n" +
                        "• Check GitHub status at status.github.com\n\n" +
                        "Would you like to add a GitHub token now?",
                        "GitHub Rate Limit - Action Required!",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Exclamation
                    );

                    if (fallbackResult == DialogResult.Yes)
                    {
                        if (PromptForGitHubToken())
                        {
                            UpdateStatus("Token added - continuing update...");
                            // Don't return, continue with the update
                        }
                        else
                        {
                            Application.Exit();
                            return;
                        }
                    }
                    else
                    {
                        Application.Exit();
                        return;
                    }
                }

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(30); // Allow longer downloads for large files

                    // Test repository access and verify it exists
                    UpdateStatus("Verifying repository accessibility...");
                    try
                    {
                        string testApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}";
                        using (var testClient = new HttpClient())
                        {
                            testClient.DefaultRequestHeaders.UserAgent.ParseAdd("HLMCUpdater");
                            if (!string.IsNullOrEmpty(GitHubToken))
                            {
                                testClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GitHubToken);
                            }
                            var testResponse = await testClient.GetAsync(testApiUrl);
                            if (!testResponse.IsSuccessStatusCode)
                            {
                                if (testResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                                {
                                    throw new Exception("Repository not found. The repository may have been moved, renamed, or deleted.");
                                }
                                else if (testResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                                {
                                    throw new Exception($"Authentication failed. Your GitHub token may be invalid or expired.\n\nPlease create a new token at: https://github.com/settings/tokens");
                                }
                                else
                                {
                                    throw new Exception($"Repository not accessible: {testResponse.StatusCode} - {testResponse.ReasonPhrase}\n\nIf you're using a GitHub token, it may have expired or lack required permissions.\nCreate a new token at: https://github.com/settings/tokens");
                                }
                            }
                            var json = await testResponse.Content.ReadAsStringAsync();
                            var repoData = JsonSerializer.Deserialize<GitHubRepoInfo>(json);
                            if (repoData?.Private == true && string.IsNullOrEmpty(GitHubToken))
                            {
                                throw new Exception("Repository is private but no authentication token is configured. Please set a GitHub token.");
                            }

                            // Verify the default branch exists
                            string defaultBranch = repoData?.DefaultBranch ?? "main";
                            string branchTestUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/git/trees/{defaultBranch}";
                            var branchResponse = await testClient.GetAsync(branchTestUrl);
                            if (!branchResponse.IsSuccessStatusCode)
                            {
                                throw new Exception($"Default branch '{defaultBranch}' not found or not accessible.");
                            }
                        }
                        UpdateStatus("Repository and branch access confirmed.");
                    }
                    catch (Exception ex)
                    {
                        results.Status = "FAILED";
                        results.Error = "Could not access GitHub repository. This may be due to:\n" +
                                       "• No internet connection\n" +
                                       "• Repository is private and no authentication token is set\n" +
                                       "• Repository doesn't exist, has been moved, or renamed\n" +
                                       "• Default branch is incorrect\n" +
                                       $"• Repository URL: https://github.com/{GitHubOwner}/{GitHubRepo}\n\n" +
                                       $"Technical details: {ex.Message}";
                        throw;
                    }

                    // --- Sync all folders ---
                    await SyncFolder("mods", "*.jar", repoTree, results, scriptRoot, client);
                    await SyncFolder("resourcepacks", "*.zip", repoTree, results, scriptRoot, client);

                    // --- Sync Configs ---
                    UpdateStatus("Synchronizing configs...");
                    string configDir = Path.Combine(scriptRoot, "config");
                    string zipUrl = $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubRepo}/{GitHubBranch}/config.zip";
                    string tempZipPath = Path.Combine(tempDir, "config.zip");

                    try
                    {
                        currentDownloads.Add(tempZipPath);
                        var response = await client.GetAsync(zipUrl);
                        response.EnsureSuccessStatusCode();

                        using (var stream = await response.Content.ReadAsStreamAsync(_cts.Token))
                        using (var fileStream = File.OpenWrite(tempZipPath))
                        {
                            await stream.CopyToAsync(fileStream, _cts.Token);
                        }
                        currentDownloads.Remove(tempZipPath);
                    }
                    catch { results.Status = "WARNING"; }

                    if (File.Exists(tempZipPath))
                    {
                        string tempUnzipDir = Path.Combine(tempDir, "unzipped_config");
                        downloadProgressBar.Visible = true;
                        downloadProgressBar.Style = ProgressBarStyle.Marquee;
                        downloadProgressLabel.Visible = true;
                        
                        downloadProgressLabel.Text = "Expanding config.zip...";
                        CenterControlX(downloadProgressLabel, progressPanel);

                        await Task.Run(() => ZipFile.ExtractToDirectory(tempZipPath, tempUnzipDir, true));

                        downloadProgressBar.Visible = false;
                        downloadProgressBar.Style = ProgressBarStyle.Blocks;
                        downloadProgressLabel.Visible = false;

                        foreach (var sourceFile in Directory.GetFiles(tempUnzipDir, "*", SearchOption.AllDirectories))
                        {
                            string relativePath = sourceFile.Substring(tempUnzipDir.Length + 1);
                            string destinationPath = Path.Combine(configDir, relativePath);
                            if (!File.Exists(destinationPath))
                            {
                                string? directoryPath = Path.GetDirectoryName(destinationPath);
                                if (!string.IsNullOrEmpty(directoryPath))
                                {
                                    Directory.CreateDirectory(directoryPath);
                                }
                                File.Copy(sourceFile, destinationPath);
                                results.Added.Add("CONFIG: " + relativePath);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Update cancelled by user");
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
                Application.Exit();
                return;
            }
            catch (Exception ex)
            {
                results.Status = "FAILED";
                results.Error = ex.ToString();

                // Save error to HLMCUpdater appdata directory if debug enabled
                if (DebugLoggingEnabled)
                {
                    try
                    {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string appDataDir = Path.Combine(appData, "HLMCUpdater");
                        if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir);
                        string deviceId = DeviceIdentifier;
                        File.WriteAllText(Path.Combine(appDataDir, $"{deviceId}_main_error.txt"), results.Error);
                    }
                    catch { }
                }
            }

            // --- Show Summary ---
            progressPanel.Visible = false;
            summaryPanel.Visible = true;

            if (results.Status == "FAILED")
            {
                summaryTitleLabel.Text = "Update Failed!";
                summaryTitleLabel.ForeColor = Color.Red;
                CenterControlX(summaryTitleLabel, summaryPanel);
                summaryCountLabel.Text = "An error occurred during the update.";
                CenterControlX(summaryCountLabel, summaryPanel);
                summaryTextBox.Text = results.Error;

                // Change close button text and adjust UI based on button content
                bool hasError = results.Status == "FAILED";
                if (hasError)
                {
                    closeButton.Text = "Report and Close";
                    // Adjust text box size for longer error messages
                    summaryTextBox.Height = 120;
                    summaryTextBox.Size = new Size(summaryPanel.Width - 40, 120);
                    CenterControlX(summaryTextBox, summaryPanel);
                }
                else
                {
                    closeButton.Text = "Close";
                    // Use smaller size for success messages
                    if (results.Added.Count > 0 || results.Removed.Count > 0)
                    {
                        summaryTextBox.Height = 100;
                        summaryTextBox.Size = new Size(summaryPanel.Width - 40, 100);
                    }
                    else
                    {
                        // Minimal height for "no updates" message
                        summaryTextBox.Height = 30;
                        summaryTextBox.Size = new Size(summaryPanel.Width - 40, 30);
                    }
                    CenterControlX(summaryTextBox, summaryPanel);
                }
                summaryTextBox.Visible = true;
            }
            else
            {
                int addedMods = results.Added.Count(x => x.StartsWith("MOD:"));
                int removedMods = results.Removed.Count(x => x.StartsWith("MOD:"));
                int addedPacks = results.Added.Count(x => x.StartsWith("RESOURCEPACK:"));
                int removedPacks = results.Removed.Count(x => x.StartsWith("RESOURCEPACK:"));

                if (results.Added.Count == 0 && results.Removed.Count == 0)
                {
                    summaryTitleLabel.Text = "No updates were found.";
                    summaryTitleLabel.ForeColor = Color.Gray;
                    CenterControlX(summaryTitleLabel, summaryPanel);
                    summaryCountLabel.Text = "";
                    CenterControlX(summaryCountLabel, summaryPanel);
                    summaryTextBox.Visible = false;
                }
                else if (results.Status == "WARNING")
                {
                    summaryTitleLabel.Text = "Update Complete (with warnings)";
                    summaryTitleLabel.ForeColor = Color.Orange;
                    CenterControlX(summaryTitleLabel, summaryPanel);
                    summaryCountLabel.Text = "Config.zip not found on repository. Other items were synced.";
                    CenterControlX(summaryCountLabel, summaryPanel);
                    summaryTextBox.Visible = true;
                }
                else
                {
                    summaryTitleLabel.Text = "Update Complete!";
                    summaryTitleLabel.ForeColor = Color.Green;
                    CenterControlX(summaryTitleLabel, summaryPanel);
                    summaryCountLabel.Text = $"Mods: {addedMods} added, {removedMods} removed. Resource Packs: {addedPacks} added, {removedPacks} removed.";
                    CenterControlX(summaryCountLabel, summaryPanel);
                    summaryTextBox.Visible = true;
                }

                if (results.Added.Count > 0 || results.Removed.Count > 0)
                {
                    var logText = "--- CHANGELOG ---\r\n";
                    if (results.Added.Count > 0)
                    {
                        logText += "\r\n[ADDED]\r\n";
                        logText += string.Join("\r\n", results.Added.Select(x => "- " + x));
                    }
                    if (results.Removed.Count > 0)
                    {
                        logText += "\r\n[REMOVED]\r\n";
                        logText += string.Join("\r\n", results.Removed.Select(x => "- " + x));
                    }
                    summaryTextBox.Text = logText;
                }
            }

            // --- Cleanup temp folder after update ---
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        private void UpdateStatus(string text)
        {
            statusLabel.Text = text;
            CenterControlX(statusLabel, progressPanel);
            this.Refresh();
        }

        private async Task<List<RepoItem>> FetchRepoTree()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string cacheDir = Path.Combine(appData, "HLMCUpdater");
            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }
            string cacheFile = Path.Combine(cacheDir, "repoTreeCache.json");
            if (File.Exists(cacheFile))
            {
                try
                {
                    string cacheJson = File.ReadAllText(cacheFile);
                    var cache = JsonSerializer.Deserialize<CacheData>(cacheJson);
                    if (cache != null && cache.Items != null)
                    {
                        UpdateStatus("Using cached repository data.");
                        return cache.Items!;
                    }
                }
                catch { } // ignore corrupted cache
            }

            // Fetch from GitHub API
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("HLMCUpdater");
                if (!string.IsNullOrEmpty(GitHubToken))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GitHubToken);
                }
                string apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/git/trees/{GitHubBranch}?recursive=true";
                var httpResponse = await client.GetAsync(apiUrl);
                string json = await httpResponse.Content.ReadAsStringAsync();
                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"GitHub API error {httpResponse.StatusCode}: {json}");
                }
                var response = JsonSerializer.Deserialize<GitHubTree>(json);
                if (response?.Tree != null)
                {
                    var cache = new CacheData { LastUpdate = DateTime.Now, Items = response.Tree };
                    File.WriteAllText(cacheFile, JsonSerializer.Serialize(cache));
                    UpdateStatus("Fetched repository data.");
                    return response.Tree;
                }
                else
                {
                    throw new Exception("Failed to fetch repository tree - no items found. JSON: " + json);
                }
            }
        }

        private async Task SyncFolder(string folderName, string fileFilter, List<RepoItem> repoTree, SyncResults results, string scriptRoot, HttpClient client)
        {
            UpdateStatus($"Synchronizing {folderName}...");

            // DEBUG: Create detailed log for troubleshooting
            var debugLog = new List<string>();
            if (DebugLoggingEnabled)
            {
                debugLog.Add($"=== SYNC FOLDER DEBUG LOG: {folderName} ===");
                debugLog.Add($"Started at: {DateTime.Now:G}");
                debugLog.Add($"Script root: {scriptRoot}");
                debugLog.Add($"File filter: {fileFilter}");
                debugLog.Add($"Total repo items: {repoTree.Count}");

                debugLog.Add("\n--- REPO TREE ANALYSIS ---");
                foreach (var item in repoTree.Where(x => x.path != null))
                {
                    debugLog.Add($"Repo item: {item.path} -> Type: {item.type}");
                }

                debugLog.Add($"\n--- FILTERING FOR {folderName} ---");
            }

            var githubItems = repoTree.Where(x =>
                x.path?.StartsWith(folderName + "/") == true &&
                x.path?.Count(c => c == '/') == 1 &&
                x.type == "blob").ToList();

            if (DebugLoggingEnabled)
            {
                debugLog.Add($"{githubItems.Count} items found for {folderName}");

                foreach (var item in githubItems.Where(x => x.path != null))
                {
                    debugLog.Add($"  - Filtered item: {item.path} -> Type: {item.type}");
                }
            }

            if (!githubItems.Any())
            {
                if (DebugLoggingEnabled)
                {
                    debugLog.Add($"NO ITEMS TO PROCESS in {folderName}");
                    // Save to appdata directory instead of script root
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string appDataDir = Path.Combine(appData, "HLMCUpdater");
                    if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir);
                    string deviceId = DeviceIdentifier;
                    System.IO.File.WriteAllText(Path.Combine(appDataDir, $"{deviceId}_sync_debug_{folderName}.txt"), string.Join("\n", debugLog));
                }
                return;
            }

            string localDirPath = Path.Combine(scriptRoot, folderName);

            // Simplified null check and directory creation
            if (string.IsNullOrEmpty(localDirPath))
            {
                UpdateStatus($"Invalid local directory path for {folderName}");
                return;
            }

            if (!Directory.Exists(localDirPath))
            {
                Directory.CreateDirectory(localDirPath);
            }

            var localFileNames = Directory.GetFiles(localDirPath, fileFilter).Select(Path.GetFileName).ToList();
            var githubFileNames = githubItems.Select(x => x.path?.Substring(folderName.Length + 1)).Where(x => !string.IsNullOrEmpty(x)).ToList();

            var added = githubFileNames.Except(localFileNames).ToList();
            var removed = localFileNames.Except(githubFileNames).ToList();

            foreach (var itemName in removed.Where(x => !string.IsNullOrEmpty(x)))
            {
                string itemType = folderName.ToUpper().TrimEnd('S');
                results.Removed.Add($"{itemType}: {itemName}");
                UpdateStatus($"Removing old {itemType}: {itemName}");
                File.Delete(Path.Combine(localDirPath, itemName!));
            }

            foreach (var itemName in added.Where(x => !string.IsNullOrEmpty(x)))
            {
                if (DebugLoggingEnabled)
                {
                    debugLog.Add($"\n--- PROCESSING FILE: {itemName} ---");
                }

                string itemType = folderName.ToUpper().TrimEnd('S');
                results.Added.Add($"{itemType}: {itemName}");

                if (DebugLoggingEnabled)
                {
                    debugLog.Add($"Item type: {itemType}");
                    debugLog.Add($"Original filename: {itemName}");

                    // Check if file exists in repo tree (double verification)
                    var matchingRepoItem = githubItems.FirstOrDefault(x => x.path == $"{folderName}/{itemName}");
                    debugLog.Add($"Found in repo tree: {matchingRepoItem != null}");
                }

                string encodedFileName = GitHubUriEncodeFileName(itemName!);
                if (DebugLoggingEnabled)
                {
                    debugLog.Add($"Encoded filename: {encodedFileName}");
                }

                string downloadUrl = $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubRepo}/{GitHubBranch}/{folderName}/{encodedFileName}";
                string destination = Path.Combine(localDirPath, itemName!);
                currentDownloads.Add(destination);

                if (DebugLoggingEnabled)
                {
                    debugLog.Add($"Download URL: {downloadUrl}");
                    debugLog.Add($"Destination: {destination}");
                    debugLog.Add($"Download start time: {DateTime.Now:G}");
                }

                // Debug: Log the download URL and check repository structure
                UpdateStatus($"Downloading {itemType}: {itemName} from {downloadUrl}");
                if (DebugLoggingEnabled)
                {
                    LogException(new Exception($"Download URL: {downloadUrl}"));
                }

                try
                {
                    bool downloadSuccess = await DownloadFileWithRobustHandling(downloadUrl, destination, _cts.Token, client, itemName!);
                    if (downloadSuccess)
                    {
                        if (DebugLoggingEnabled)
                        {
                            debugLog.Add($"✅ DOWNLOAD SUCCESSFUL: {itemName}");
                        }
                    }
                    else
                    {
                        if (DebugLoggingEnabled)
                        {
                            debugLog.Add($"❌ DOWNLOAD FAILED: {itemName} - Skipping");
                        }
                        results.Added.Remove($"{itemType}: {itemName}");
                        currentDownloads.Remove(destination);
                        continue; // Skip to next file instead of crashing entire process
                    }
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("404"))
                {
                    if (DebugLoggingEnabled)
                    {
                        debugLog.Add($"❌ 404 ERROR FOR: {itemName}");
                        debugLog.Add($"Full exception: {ex.Message}");
                        debugLog.Add($"URL attempted: {downloadUrl}");
                        debugLog.Add($"Step 1/8: Starting 404 diagnostic process");
                    }

                    // Check rate limit only if we suspect it might be exhausted
                    var rateLimitInfo = new RateLimitInfo();
                    if (DebugLoggingEnabled)
                    {
                        debugLog.Add($"Step 2/8: Checking GitHub rate limit status");
                    }
                    rateLimitInfo = await CheckGitHubRateLimit();
                    if (DebugLoggingEnabled)
                    {
                        debugLog.Add($"Step 3/8: Rate limit check complete - {rateLimitInfo.Remaining}/{rateLimitInfo.Limit} requests remaining");
                    }

                    if (rateLimitInfo.Remaining <= 0)
                    {
                        if (DebugLoggingEnabled)
                        {
                            debugLog.Add($"Step 4/8: RATE LIMIT EXHAUSTED - This is likely the cause of 404");
                        }
                        UpdateStatus("❌ 404 error - GitHub rate limit exhausted (0 requests remaining)");
                    }
                    else if (rateLimitInfo.Remaining <= 10)
                    {
                        if (DebugLoggingEnabled)
                        {
                            debugLog.Add($"Step 4/8: RATE LIMIT CRITICAL - Only {rateLimitInfo.Remaining} requests left");
                        }
                        UpdateStatus($"❌ 404 error - GitHub rate limit very low ({rateLimitInfo.Remaining} requests remaining)");
                    }
                    else if (DebugLoggingEnabled)
                    {
                        debugLog.Add($"Step 4/8: Network/file availability issue - Rate limit appears fine");
                    }

                    // Show current rate limit status only if critically low
                    if (rateLimitInfo.Remaining.HasValue && rateLimitInfo.Limit.HasValue && rateLimitInfo.Remaining.Value <= 10)
                    {
                        string rateStatus = $"GitHub API Status: {rateLimitInfo.Remaining.Value}/{rateLimitInfo.Limit.Value} ⚠️ CRITICAL!";
                        UpdateStatus(rateStatus);
                        await Task.Delay(1500); // Show rate limit status for 1.5 seconds
                    }

                    if (rateLimitInfo.IsLimited || (rateLimitInfo.Remaining.HasValue && rateLimitInfo.Remaining.Value == 0))
                    {
                        debugLog.Add($"Step 5/8: Rate limit condition met, handling rate limit scenario");
                        UpdateStatus("🚫 RATE LIMITED! GitHub API requests exhausted");

                        if (rateLimitInfo.ResetTime.HasValue)
                        {
                            var resetTime = rateLimitInfo.ResetTime.Value;
                            var waitTime = resetTime - DateTimeOffset.Now;
                            debugLog.Add($"Step 6/8: Rate reset time available - {Math.Ceiling(waitTime.TotalMinutes):F0} minutes remaining");

                            if (waitTime.TotalSeconds > 0)
                            {
                                DialogResult addTokenResult = MessageBox.Show(
                                    $"🔥 GitHub has blocked your requests due to rate limiting!\n\n" +
                                    $"• Rate limit will reset at: {resetTime:G}\n" +
                                    $"• Estimated wait time: {Math.Ceiling(waitTime.TotalMinutes):F0} minutes\n\n" +
                                    $"SOLUTION: Add a GitHub Personal Access Token to avoid rate limits\n" +
                                    $"📋 Create at: https://github.com/settings/tokens\n\n" +
                                    "Would you like to add a token now?",
                                    "GitHub Rate Limit Exceeded!",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Warning
                                );

                                if (addTokenResult == DialogResult.Yes)
                                {
                                    debugLog.Add($"Step 7/8: User chose to add token");
                                    if (PromptForGitHubToken())
                                    {
                                        debugLog.Add($"Step 8/8: Token added successfully - continuing");
                                        UpdateStatus("Token added - continuing update...");
                                        // Don't return, continue with the download using the token
                                    }
                                    else
                                    {
                                        debugLog.Add($"Step 8/8: User cancelled token prompt - exiting");
                                        Application.Exit();
                                        return;
                                    }
                                }
                                else
                                {
                                    debugLog.Add($"Step 7/8: User declined to add token - exiting");
                                    Application.Exit();
                                    return;
                                }
                            }
                        }
                        else
                        {
                            debugLog.Add($"Step 6/8: No rate reset time available - using fallback");
                        }

                        // Fallback if no reset time available
                        DialogResult fallbackResult = MessageBox.Show(
                            "🚫 GitHub API rate limit exceeded!\n\n" +
                            "This is likely why you're getting error 404.\n\n" +
                            "SOLUTIONS:\n" +
                            "• Add a GitHub Personal Access Token (recommended)\n" +
                            "• Wait 1 hour for automatic reset\n" +
                            "• Check GitHub status at status.github.com\n\n" +
                            "Would you like to add a GitHub token now?",
                            "GitHub Rate Limit - Action Required!",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Exclamation
                        );

                        if (fallbackResult == DialogResult.Yes)
                        {
                            debugLog.Add($"Step 7/8: User chose to add token (fallback)");
                            if (PromptForGitHubToken())
                            {
                                debugLog.Add($"Step 8/8: Token added successfully (fallback) - continuing");
                                UpdateStatus("Token added - continuing update...");
                                // Don't return, continue with the download
                            }
                            else
                            {
                                debugLog.Add($"Step 8/8: User cancelled token prompt (fallback) - exiting");
                                Application.Exit();
                                return;
                            }
                        }
                        else
                        {
                            debugLog.Add($"Step 7/8: User declined to add token (fallback) - exiting");
                            Application.Exit();
                            return;
                        }
                    }

                    // Advanced diagnostic checks if not rate limited
                    if (DebugLoggingEnabled)
                    {
                        debugLog.Add($"Step 5/8: Starting advanced diagnostics - checking alternative branches");
                    }
                    try
                    {
                        List<string> branchesToTry = ["main", "master"];

                        foreach (string branch in branchesToTry)
                        {
                            if (itemName != null)
                            {
                                if (DebugLoggingEnabled)
                                {
                                    debugLog.Add($"Step 6/8: Testing branch '{branch}' for file existence");
                                }
                                string apiTestUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/contents/{folderName}/{Uri.EscapeDataString(itemName)}?ref={branch}";
                                using (var testClient = new HttpClient())
                                {
                                    // Use different User-Agent to avoid rate limit sharing
                                    testClient.DefaultRequestHeaders.UserAgent.ParseAdd("HLMCUpdater-Diagnostics");
                                    if (!string.IsNullOrEmpty(GitHubToken))
                                    {
                                        testClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GitHubToken);
                                    }

                                    var testResponse = await testClient.GetAsync(apiTestUrl);
                                    if (testResponse.IsSuccessStatusCode)
                                    {
                                        // File exists in API, try different approach
                                        string workingUrl = $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubRepo}/{branch}/{folderName}/{encodedFileName}";
                                        UpdateStatus($"Found file on branch '{branch}', trying alternative approach...");
                                        if (DebugLoggingEnabled)
                                        {
                                            debugLog.Add($"Step 7/8: File found on branch '{branch}' - attempting alternative download");
                                            debugLog.Add($"Step 8/8: Alternative URL: {workingUrl}");
                                            LogException(new Exception($"BRANCH DISCOVERY: File exists on '{branch}' but main branch failed"));
                                        }
                                    }
                                    else if (DebugLoggingEnabled)
                                    {
                                        debugLog.Add($"Step 7/8: File not found on branch '{branch}' - HttpStatus {testResponse.StatusCode}");
                                    }
                                }
                            }
                        }
                        if (DebugLoggingEnabled)
                        {
                            debugLog.Add($"Step 8/8: Advanced diagnostics complete - no alternative branch found");
                        }
                    }
                    catch (Exception diagEx)
                    {
                        if (DebugLoggingEnabled)
                        {
                            debugLog.Add($"Step 8/8: Diagnostic check failed with exception: {diagEx.Message}");
                            LogException(new Exception($"Diagnostic check failed: {diagEx.Message}"));
                        }
                    }

                    // Error logging handled separately
   
                        if (DebugLoggingEnabled)
                        {
                            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                            string appDataDir = Path.Combine(appData, "HLMCUpdater");
                            if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir);
                            string deviceId = DeviceIdentifier;
                            string debugFileName = Path.Combine(appDataDir, $"{deviceId}_sync_debug_{folderName}.txt");
                            try
                            {
                                debugLog.Add($"Step 8/8: Writing diagnostic log to file");
                                File.WriteAllText(debugFileName, string.Join("\n", debugLog));
                                debugLog.Add($"Diagnostic log written to: {debugFileName}");
                            }
                            catch { }
                        }
   
                        if (DebugLoggingEnabled)
                        {
                            string errorMsg = $"404 Not Found for: {downloadUrl}\nGitHub Repo: {GitHubOwner}/{GitHubRepo}\nBranch: {GitHubBranch}\nFile: {itemName}";
                            LogException(new Exception(errorMsg, ex));
                        }
   
                        // Clean up old error log files
                        try
                        {
                            string oldErrorLog = Path.Combine(scriptRoot, "HLMC_updater_error.txt");
                            string updaterErrorLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HLMCUpdater", "updater_error.log");
                            if (File.Exists(oldErrorLog)) File.Delete(oldErrorLog);
                            if (File.Exists(updaterErrorLog)) File.Delete(updaterErrorLog);
                        }
                        catch { }
   
                        results.Status = "FAILED";
                        results.Error = $"File download failed (404): {itemName}\n\nPossible causes:\n" +
                                        "• File has been removed from repository\n" +
                                        "• Repository access issues\n" +
                                        $"{(rateLimitInfo.Remaining <= 10 ? "• GitHub API rate limit low" : "")}\n\n" +
                                        $"Failed URL: {downloadUrl}\n" +
                                        $"Repository: https://github.com/{GitHubOwner}/{GitHubRepo}";
                        throw;
                    }
            }
        }

        private async Task<bool> DownloadFileWithRobustHandling(string url, string destination, CancellationToken cancellationToken, HttpClient client, string itemName)
        {
            const int maxRetries = 3;
            const int baseDelayMillis = 2000; // 2 seconds initial delay

            for (int retryCount = 0; retryCount < maxRetries; retryCount++)
            {
                try
                {
                    // First, check if file is accessible with HEAD request
                    if (retryCount == 0)
                    {
                        try
                        {
                            UpdateStatus($"Checking accessibility of {itemName}...");
                            using (var headRequest = new HttpRequestMessage(HttpMethod.Head, url))
                            {
                                using (var headResponse = await client.SendAsync(headRequest, cancellationToken))
                                {
                                    if (!headResponse.IsSuccessStatusCode)
                                    {
                                        UpdateStatus($"❌ File accessibility check failed (HTTP {headResponse.StatusCode}): {itemName}");

                                        if (DebugLoggingEnabled)
                                        {
                                            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                                            string appDataDir = Path.Combine(appData, "HLMCUpdater");
                                            string deviceId = DeviceIdentifier;

                                            using (var logWriter = File.AppendText(Path.Combine(appDataDir, $"{deviceId}_sync_debug_mods.txt")))
                                            {
                                                logWriter.WriteLine($"--- HEAD REQUEST CHECK FAILED ---");
                                                logWriter.WriteLine($"URL: {url}");
                                                logWriter.WriteLine($"Status Code: {headResponse.StatusCode}");
                                                logWriter.WriteLine($"Reason: {headResponse.ReasonPhrase}");
                                                logWriter.WriteLine($"Retry attempt: {retryCount + 1}/{maxRetries}");
                                                logWriter.WriteLine($"Time: {DateTime.Now:G}");
                                            }
                                        }

                                        if (headResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                                        {
                                            // If HEAD request fails with 404, try GET immediately instead of retrying
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        UpdateStatus($"✅ File accessible - proceeding with download of {itemName}...");
                                    }
                                }
                            }
                        }
                        catch (Exception headEx)
                        {
                            if (DebugLoggingEnabled)
                            {
                                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                                string appDataDir = Path.Combine(appData, "HLMCUpdater");
                                string deviceId = DeviceIdentifier;

                                using (var logWriter = File.AppendText(Path.Combine(appDataDir, $"{deviceId}_sync_debug_mods.txt")))
                                {
                                    logWriter.WriteLine($"--- HEAD REQUEST EXCEPTION ---");
                                    logWriter.WriteLine($"URL: {url}");
                                    logWriter.WriteLine($"Exception: {headEx.Message}");
                                    logWriter.WriteLine($"Continuing with GET request...");
                                }
                            }
                        }
                    }

                    // Attempt download with robust progress handling
                    await DownloadFileWithProgress(url, destination, cancellationToken, client, itemName, retryCount);

                    // If we get here, download was successful
                    return true;

                }
                catch (HttpRequestException ex) when (ex.Message.Contains("404"))
                {
                    UpdateStatus($"❌ Network 404 error for {itemName} (attempt {retryCount + 1}/{maxRetries})");

                    if (DebugLoggingEnabled)
                    {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string appDataDir = Path.Combine(appData, "HLMCUpdater");
                        string deviceId = DeviceIdentifier;

                        using (var logWriter = File.AppendText(Path.Combine(appDataDir, $"{deviceId}_sync_debug_mods.txt")))
                        {
                            logWriter.WriteLine($"--- 404 RETRY ATTEMPT {retryCount + 1} ---");
                            logWriter.WriteLine($"URL: {url}");
                            logWriter.WriteLine($"Exception: {ex.Message}");
                            logWriter.WriteLine($"Time: {DateTime.Now:G}");
                            logWriter.WriteLine($"Next retry will be in {(baseDelayMillis * Math.Pow(2, retryCount)) / 1000} seconds");
                        }
                    }

                    // Exponential backoff for retries
                    if (retryCount < maxRetries - 1)
                    {
                        int delayMs = (int)(baseDelayMillis * Math.Pow(2, retryCount));
                        await Task.Delay(delayMs, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // Re-throw cancellation
                }
                catch (Exception ex)
                {
                    UpdateStatus($"❌ Download error for {itemName}: {ex.Message} (attempt {retryCount + 1}/{maxRetries})");

                    if (DebugLoggingEnabled)
                    {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string appDataDir = Path.Combine(appData, "HLMCUpdater");
                        string deviceId = DeviceIdentifier;

                        using (var logWriter = File.AppendText(Path.Combine(appDataDir, $"{deviceId}_sync_debug_mods.txt")))
                        {
                            logWriter.WriteLine($"--- GENERAL RETRY ATTEMPT {retryCount + 1} ---");
                            logWriter.WriteLine($"URL: {url}");
                            logWriter.WriteLine($"Exception: {ex.Message}");
                            logWriter.WriteLine($"Type: {ex.GetType().Name}");
                        }
                    }

                    // Clean up partial files
                    try { if (File.Exists(destination)) File.Delete(destination); } catch { }

                    // Exponential backoff for retries
                    if (retryCount < maxRetries - 1)
                    {
                        int delayMs = (int)(baseDelayMillis * Math.Pow(2, retryCount));
                        await Task.Delay(delayMs, cancellationToken);
                    }
                }
            }

            // All retries exhausted
            UpdateStatus($"❌ Download failed permanently for {itemName} - skipping");
            return false;
        }

        private async Task DownloadFileWithProgress(string url, string destination, CancellationToken cancellationToken, HttpClient client)
        {
            await DownloadFileWithProgress(url, destination, cancellationToken, client, Path.GetFileName(destination), 0);
        }

        private async Task DownloadFileWithProgress(string url, string destination, CancellationToken cancellationToken, HttpClient client, string itemName, int retryAttempt = 0)
        {
            downloadProgressBar.Visible = true;
            downloadProgressLabel.Visible = true;
            downloadProgressBar.Value = 0;

            string retryText = retryAttempt > 0 ? $" (Retry {retryAttempt})" : "";
            downloadProgressLabel.Text = $"Starting download of {itemName}{retryText}...";
            CenterControlX(downloadProgressLabel, progressPanel);

            try
            {
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    downloadProgressBar.Maximum = totalBytes > 0 ? (int)Math.Min(totalBytes, int.MaxValue) : 100;

                    using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                    using (var fileStream = File.Open(destination, FileMode.Create))
                    {
                        byte[] buffer = new byte[65536]; // Increased buffer for faster downloads
                        int bytesRead;
                        long totalRead = 0;
                        var lastUpdateTime = DateTime.Now;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            fileStream.Write(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            // Throttle UI updates to once every 2ms (1/10 second)
                            if ((DateTime.Now - lastUpdateTime).TotalMilliseconds >= 2)
                            {
                                if (totalBytes > 0)
                                {
                                    downloadProgressBar.Value = (int)Math.Min(totalRead, downloadProgressBar.Maximum);
                                    double mbRead = Math.Round(totalRead / 1048576.0, 2);
                                    double mbTotal = Math.Round(totalBytes / 1048576.0, 2);
                                    downloadProgressLabel.Text = $"Downloading {itemName}{retryText}: {mbRead:F2} MB / {mbTotal:F2} MB";
                                    CenterControlX(downloadProgressLabel, progressPanel);
                                }
                                else
                                {
                                    downloadProgressLabel.Text = string.Format("Downloading {0}{1}: {2:F2} MB", itemName, retryText, Math.Round(totalRead / 1048576.0, 2));
                                    CenterControlX(downloadProgressLabel, progressPanel);
                                }
                                this.Refresh();
                                lastUpdateTime = DateTime.Now;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Clean up partially downloaded file with retry
                int attempts = 0;
                while (attempts < 10 && File.Exists(destination))
                {
                    try
                    {
                        File.Delete(destination);
                        break;
                    }
                    catch
                    {
                        attempts++;
                        if (attempts < 10)
                        {
                            Thread.Sleep(200);
                        }
                    }
                }
                throw;
            }
            catch (Exception)
            {
                // Clean up partial files on any other error
                try { if (File.Exists(destination)) File.Delete(destination); } catch { }
            }

            currentDownloads.Remove(destination);
            downloadProgressBar.Visible = false;
            downloadProgressLabel.Visible = false;
        }

        private void BrowseForMinecraftFolder()
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select your Minecraft installation folder";
                folderDialog.SelectedPath = MinecraftPath;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    MinecraftPath = folderDialog.SelectedPath;
                    MessageBox.Show($"Minecraft folder set to: {MinecraftPath}", "Path Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        
        private async Task CheckForUpdaterUpdates()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("HLMCUpdater");
                    if (!string.IsNullOrEmpty(GitHubToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GitHubToken);
                    }
                    string apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}";
                    var json = await client.GetStringAsync(apiUrl);
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                    if (release?.Assets?.Count > 0)
                    {
                        string tagVersion = release.TagName?.TrimStart('v') ?? "";
                        if (tagVersion != EmbeddedVersion)
                        {
                            // Show update button
                            var updateButton = new Button
                            {
                                Text = "Update Updater",
                                Size = new Size(220, 50),
                                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                                Anchor = AnchorStyles.Top,
                                BackColor = Color.FromArgb(60, 180, 75),
                                ForeColor = Color.White,
                                FlatStyle = FlatStyle.Flat,
                                Location = new Point((welcomePanel.Width - 220) / 2, 400)
                            };
                            updateButton.FlatAppearance.BorderColor = Color.FromArgb(70, 120, 70);
                            updateButton.FlatAppearance.BorderSize = 2;
                            updateButton.Click += async (s, e) => await DownloadUpdaterUpdate();
                            welcomePanel.Controls.Add(updateButton);

                            // Remove the regular check button
                            welcomePanel.Controls.Remove(startButton);
                        }
                        else
                        {
                            MessageBox.Show("You are already using the latest version of the updater.", "Up to date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking for updates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DownloadUpdaterUpdate(string? localSource = null)
        {
            string? currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
            {
                UpdateStatus("Failed to get current executable path");
                return;
            }

            string exeDir = Path.GetDirectoryName(currentExe)!;
            string newExePath = localSource ?? Path.Combine(exeDir, "Holy Lois Updater new.exe");
            string backupExePath = Path.Combine(exeDir, "Holy Lois Updater.old.exe");

            try
            {
                if (localSource != null)
                {
                    if (!File.Exists(localSource))
                    {
                        UpdateStatus("Local update file not found");
                        return;
                    }
                    UpdateStatus("Using local updater update...");
                }
                else
                {
                    UpdateStatus("Fetching repository data for updater...");
                    var Items = await FetchRepoTree();
                    var exeItem = Items.FirstOrDefault(x => x.type == "blob" && !x.path!.Contains('/') && x.path.EndsWith(".exe"));
                    if (exeItem?.path == null)
                    {
                        UpdateStatus("No updater exe found in repository");
                        return;
                    }

                    string exeName = exeItem.path;
                    string encodedExeName = exeName != null ? Uri.EscapeDataString(exeName) : "";
                    string downloadUrl = $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubRepo}/{GitHubBranch}/{encodedExeName}";

                    UpdateStatus("Downloading updater update...");

                    using (var client = new HttpClient())
                    {
                        var response = await client.GetAsync(downloadUrl);
                        response.EnsureSuccessStatusCode();

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = File.OpenWrite(newExePath))
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                    }
                }

                // Wait briefly for file operations
                await Task.Delay(500);

                // Backup current exe
                File.Move(currentExe, backupExePath, true);

                // Replace current exe with new one
                File.Move(newExePath, currentExe, true);

                // Start the new exe
                Process.Start(new ProcessStartInfo
                {
                    FileName = currentExe,
                    UseShellExecute = true
                });

                // Delete backup in background
                _ = Task.Run(() =>
                {
                    try
                    {
                        Thread.Sleep(2000); // Wait for new exe to launch
                        if (File.Exists(backupExePath))
                        {
                            File.Delete(backupExePath);
                        }
                    }
                    catch { }
                });

                Application.Exit();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Update failed: {ex.Message}");
                // Clean up on error
                try
                {
                    if (File.Exists(newExePath) && localSource == null) File.Delete(newExePath);
                }
                catch { }
            }
        }
        private async void MainForm_Load(object? sender, EventArgs e)
        {
            _programVersion = "v" + EmbeddedVersion;

            await EnsureDotNetRuntime();

            // Show welcome panel immediately with checking status
            welcomePanel.Visible = true;
            updateStatusLabel.Text = "🔄 Checking for updater updates...";
            updateStatusLabel.ForeColor = Color.Gold;
            updateStatusLabel.Visible = true;
            CenterControlX(updateStatusLabel, welcomePanel);

            // Cleanup any orphaned files from previous operations
            CleanupOrphanedFiles();

            // Clean up old error logs
            try
            {
                string minecraftDir = GetMinecraftPathWithoutPrompt();
                string oldErrorLog = Path.Combine(minecraftDir, "HLMC_updater_error.txt");
                string updaterErrorLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HLMCUpdater", "updater_error.log");
                if (File.Exists(oldErrorLog)) File.Delete(oldErrorLog);
                if (File.Exists(updaterErrorLog)) File.Delete(updaterErrorLog);
            }
            catch { }

            // Migrated old config and cache files to appdata (won't overwrite if already migrated)
            MigrateOldConfigFiles();

            MainForm_Resize(sender, e);
            string mcPath = GetMinecraftPathWithoutPrompt();
            bool updateAvailable = await CheckForUpdaterUpdatesOnStartup(mcPath);

            // Update status based on results
            Color statusColor = updateAvailable ? Color.Green : Color.Gray;
            updateStatusLabel.Text = updateAvailable ? "! Update for updater available !" : "✓ No updates for updater available";
            updateStatusLabel.ForeColor = statusColor;
            CenterControlX(updateStatusLabel, welcomePanel);

            await Task.Delay(2000); // 2 second delay before animation/hiding

            if (!updateAvailable) // Only animate and hide for "No updates" message
            {
                await AnimateStatusSlideUp();
                updateStatusLabel.Visible = false;
            }
            else
            {
                // Keep the update available message visible
                await Task.Delay(1000); // Brief pause before staying visible
                // Don't set Visible=false for update available messages
            }
        }

        private void MainForm_Resize(object? sender, EventArgs e)
        {
            // Welcome Panel
            titleLabel.Location = new Point((welcomePanel.Width - titleLabel.Width) / 2, 80);
            creditLabel.Location = new Point((welcomePanel.Width - creditLabel.Width) / 2, titleLabel.Bottom + 10);
            startButton.Location = new Point((welcomePanel.Width - startButton.Width) / 2, creditLabel.Bottom + 50);
            versionLabel.Location = new Point((welcomePanel.Width - versionLabel.Width) / 2, startButton.Bottom + 40);

            // Progress Panel
            CenterControlX(statusLabel, progressPanel);
            downloadProgressBar.Location = new Point((progressPanel.Width - downloadProgressBar.Width) / 2, statusLabel.Bottom + 40);
            CenterControlX(downloadProgressLabel, progressPanel);
            cancelButton.Location = new Point((progressPanel.Width - cancelButton.Width) / 2, progressPanel.Height - 70);

            // Summary Panel
            CenterControlX(summaryTitleLabel, summaryPanel);
            CenterControlX(summaryCountLabel, summaryPanel);
            summaryTextBox.Location = new Point(20, summaryCountLabel.Bottom + 20);
            summaryTextBox.Size = new Size(summaryPanel.Width - 40, 150);
            closeButton.Location = new Point((summaryPanel.Width - closeButton.Width) / 2, summaryPanel.Height - 50);
        }

        private void CenterControlX(Control control, Panel panel)
        {
            int x = Math.Max(0, (panel.Width - control.Width) / 2);
            control.Location = new Point(x, control.Location.Y);
        }

        private async Task<bool> CheckForUpdaterUpdatesOnStartup(string mcPath)
        {
            string exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string tempDir = Path.Combine(Path.GetTempPath(), "HLMC_Updater_Temp");
            string tempExePath = Path.Combine(tempDir, "Holy Lois Updater.exe");
            string resultsFile = Path.Combine(exeDir, "update_check_results.txt");

            // Get versions
            string currentVersion = EmbeddedVersion;
            string remoteVersion = "could not get";
            List<string> steps = new List<string>();
            steps.Add($"Start: Checking for updates (Current Version: {currentVersion})");

            try
            {
                // Create temp dir
                steps.Add("Step 1: Creating temp directory");
                Directory.CreateDirectory(tempDir);
                steps.Add("Step 1: Temp directory created successfully");

                // Download exe from GitHub repository tree (public repo, no auth needed)
                using (var client = new HttpClient())
                {
                    steps.Add("Step 2: Setting up HTTP client");
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("HLMCUpdater");
                    steps.Add("Step 2: Public repository - skipping authentication");
                    // Note: Removed authorization header for public repository call
                    string apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/git/trees/{GitHubBranch}?recursive=true";
                    steps.Add($"Step 3: Fetching GitHub tree from {apiUrl}");

                    // Update UI status for user feedback
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() =>
                        {
                            updateStatusLabel.Text = "🔄 Fetching latest version info...";
                            CenterControlX(updateStatusLabel, welcomePanel);
                        }));
                    }
                    else
                    {
                        updateStatusLabel.Text = "🔄 Fetching latest version info...";
                        CenterControlX(updateStatusLabel, welcomePanel);
                    }

                    var json = await client.GetStringAsync(apiUrl);
                    steps.Add($"Step 3: Received JSON response ({json.Length} characters)");
                    var tree = JsonSerializer.Deserialize<GitHubTree>(json);
                    steps.Add($"Step 3: Deserialized tree - Items count: {tree?.Tree?.Count ?? 0}");

                    if (tree?.Tree != null)
                    {
                        steps.Add("Step 4: Looking for exe file in repository");
                        var exeItem = tree.Tree.FirstOrDefault(x => x.type == "blob" && !x.path!.Contains('/') && x.path.EndsWith(".exe"));
                        if (exeItem?.path != null)
                        {
                            string exeName = exeItem.path;
                            string encodedExeName = exeName != null ? Uri.EscapeDataString(exeName) : "";
                            var downloadUrl = $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubRepo}/{GitHubBranch}/{encodedExeName}";
                            steps.Add($"Step 4: Found exe at {downloadUrl}");

                            steps.Add("Step 5: Starting exe download");

                            // Update UI status for download
                            if (InvokeRequired)
                            {
                                Invoke(new Action(() =>
                                {
                                    updateStatusLabel.Text = "⬇️ Downloading update...";
                                    CenterControlX(updateStatusLabel, welcomePanel);
                                }));
                            }
                            else
                            {
                                updateStatusLabel.Text = "⬇️ Downloading update...";
                                CenterControlX(updateStatusLabel, welcomePanel);
                            }

                            using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                            {
                                response.EnsureSuccessStatusCode();
                                using (var stream = await response.Content.ReadAsStreamAsync())
                                using (var fileStream = File.OpenWrite(tempExePath))
                                {
                                    await stream.CopyToAsync(fileStream);
                                }
                            }
                            steps.Add($"Step 5: Download complete - File size: {new FileInfo(tempExePath).Length}");

                            steps.Add("Step 6: Extracting version info");
                            try
                            {
                                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(tempExePath);
                                remoteVersion = fvi.FileVersion ?? fvi.ProductVersion ?? "";
                                if (string.IsNullOrWhiteSpace(remoteVersion))
                                {
                                    remoteVersion = $"could not get (FileVersion: '{fvi.FileVersion}', ProductVersion: '{fvi.ProductVersion}', FileSize: {new FileInfo(tempExePath).Length})";
                                }
                                steps.Add($"Step 6: Version extracted - {remoteVersion}");
                            }
                            catch (Exception ex)
                            {
                                remoteVersion = $"could not get - exception: {ex.Message}";
                                steps.Add($"Step 6: Version extraction failed - {ex.Message}");
                            }
                        }
                        else
                        {
                            steps.Add("Step 4: No exe file found in repository root");
                            remoteVersion = "could not get - no exe found";
                        }
                    }
                    else
                    {
                        steps.Add("Step 3: No tree data received");
                        remoteVersion = "could not get - no tree data";
                    }
                }
            }
            catch (Exception ex)
            {
                // Remote version remains "could not get"
                steps.Add($"ERROR: Exception occurred - {ex.Message}");
                remoteVersion = "could not get";
            }

            // Write results to appdata directory if debug enabled
            if (DebugLoggingEnabled)
            {
                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string appDataDir = Path.Combine(appData, "HLMCUpdater");
                    if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir);
                    string deviceId = DeviceIdentifier;
                    string matchText = remoteVersion.Contains("could not get") ? "could not determine" : (string.Equals(currentVersion, remoteVersion, StringComparison.Ordinal) ? "True" : "False");
                    string resultsLog = $"Current Version: {currentVersion}\nRemote Version: {remoteVersion}\nMatch: {matchText}\n\nSTEP LOG:\n{string.Join("\n", steps)}";
                    File.WriteAllText(Path.Combine(appDataDir, $"{deviceId}_update_check_results.txt"), resultsLog);
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(remoteVersion) && remoteVersion != "could not get" && remoteVersion != currentVersion)
            {
                // Replace the modpack update button with updater update button
                welcomePanel.Controls.Remove(startButton);

                var updateButton = new Button
                {
                    Text = "Update Updater",
                    Size = new Size(220, 50),
                    Font = new Font("Arial", 13, FontStyle.Bold),
                    Anchor = AnchorStyles.Top,
                    BackColor = Color.FromArgb(255, 165, 0), // Orange color for urgency
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Location = new Point((welcomePanel.Width - 220) / 2, creditLabel.Bottom + 50) // Same position as original start button
                };
                updateButton.FlatAppearance.BorderColor = Color.FromArgb(200, 140, 0);
                updateButton.FlatAppearance.BorderSize = 2;
                updateButton.Click += async (s, e) => await DownloadUpdaterUpdate(tempExePath);
                welcomePanel.Controls.Add(updateButton);
                return true;
            }
            else if (remoteVersion == currentVersion && File.Exists(Path.Combine(exeDir, "FORCE_UPDATE_TEST")))
            {
                // Debug mode: Force show update button for testing even when versions match
                welcomePanel.Controls.Remove(startButton);

                var updateButton = new Button
                {
                    Text = "[TEST] Update Updater",
                    Size = new Size(220, 50),
                    Font = new Font("Arial", 13, FontStyle.Bold),
                    Anchor = AnchorStyles.Top,
                    BackColor = Color.FromArgb(255, 0, 255), // Magenta for test mode
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Location = new Point((welcomePanel.Width - 220) / 2, creditLabel.Bottom + 50)
                };
                updateButton.FlatAppearance.BorderColor = Color.FromArgb(200, 0, 200);
                updateButton.FlatAppearance.BorderSize = 2;
                updateButton.Click += async (s, e) => await DownloadUpdaterUpdate(tempExePath);
                welcomePanel.Controls.Add(updateButton);
                return true;
            }
            else
            {
                // Delete temp
                try { Directory.Delete(tempDir, true); } catch { }
                return false;
            }
        }
        // --- Helper Classes ---
                class GitHubTree
                {
                    public string? Sha { get; set; }
                    [System.Text.Json.Serialization.JsonPropertyName("tree")]
                    public List<RepoItem>? Tree { get; set; }
                }
        
                class RepoItem
                {
                    public string? path { get; set; }
                    public string? type { get; set; }
                }
        
                class CacheData
                {
                    public DateTime LastUpdate { get; set; }
                    public List<RepoItem>? Items { get; set; }
                }

        class SyncResults
        {
            public List<string> Added { get; } = new List<string>();
            public List<string> Removed { get; } = new List<string>();
            public string Status { get; set; } = "SUCCESS";
            public string? Error { get; set; }
        }

        class GitHubRelease
        {
            public string? TagName { get; set; }
            public List<GitHubAsset>? Assets { get; set; }
        }

        class GitHubAsset
        {
            public string? BrowserDownloadUrl { get; set; }
        }

        class GitHubRepoInfo
        {
            [System.Text.Json.Serialization.JsonPropertyName("default_branch")]
            public string? DefaultBranch { get; set; }
            public bool Private { get; set; }
        }

        class RateLimitInfo
        {
            public int? Limit { get; set; }
            public int? Remaining { get; set; }
            public DateTimeOffset? ResetTime { get; set; }
            public bool IsLimited => Remaining <= 0;
        }
    }
}
