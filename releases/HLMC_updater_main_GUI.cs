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
        const string GitHubToken = "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"; // Provided for testing purposes
        private static readonly string EmbeddedVersion = Assembly.GetExecutingAssembly().GetName().Version!.ToString();

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
                                validFolder = true;
                                break;
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
                // Save to config file in exe directory
                string exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                string configPath = Path.Combine(exeDir, "config.json");
                var config = new Dictionary<string, string>
                {
                    ["MinecraftPath"] = value
                };
                File.WriteAllText(configPath, JsonSerializer.Serialize(config));
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
                Size = new Size(120, 40),
                Font = new Font("Arial", 10),
                Anchor = AnchorStyles.Bottom,
                BackColor = Color.FromArgb(60, 180, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point((summaryPanel.Width - 120) / 2, summaryPanel.Height)
            };
            closeButton.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
            closeButton.FlatAppearance.BorderSize = 2;
            closeButton.Click += (s, e) => Application.Exit();
            summaryPanel.Controls.Add(closeButton);

            this.FormClosing += (s, e) =>
            {
                _cts?.Cancel();
                Application.Exit();
            };

            this.Load += MainForm_Load;
            this.Resize += new EventHandler(this.MainForm_Resize);
        }
        private static void LogException(Exception ex)
        {
            try
            {
                string logPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, "updater_error.log");
                File.WriteAllText(logPath, $"{DateTime.Now}: {ex.ToString()}");
            }
            catch
            {
                // If logging fails, ignore
            }
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

        private void CleanupOrphanedBackupFiles()
        {
            try
            {
                string exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                string? currentExe = Environment.ProcessPath;

                if (string.IsNullOrEmpty(currentExe)) return;

                string currentExeName = Path.GetFileName(currentExe);
                string backupPattern = Path.GetFileNameWithoutExtension(currentExeName) + ".old.exe";
                string backupPath = Path.Combine(exeDir, backupPattern);

                // Check if backup file exists
                if (File.Exists(backupPath))
                {
                    try
                    {
                        // Verify the backup file is older than current exe
                        FileInfo currentFi = new FileInfo(currentExe);
                        FileInfo backupFi = new FileInfo(backupPath);

                        // Only clean up if backup is older (successful update scenario)
                        if (backupFi.LastWriteTime < currentFi.LastWriteTime)
                        {
                            File.Delete(backupPath);
                        }
                        // If backup is newer, it might be from a failed update - keep it for manual inspection
                    }
                    catch { } // Ignore cleanup errors
                }
            }
            catch { } // Ignore all errors during cleanup
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

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(30); // Allow longer downloads for large files
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
                File.WriteAllText(Path.Combine(scriptRoot, "HLMC_updater_error.txt"), results.Error);
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
                summaryTextBox.Text = results.Error + "\r\n\r\nError details saved to HLMC_updater_error.txt";
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
            string exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string cacheFile = Path.Combine(exeDir, "repoTreeCache.json");
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
            var githubItems = repoTree.Where(x =>
                x.path?.StartsWith(folderName + "/") == true &&
                x.path?.Count(c => c == '/') == 1 &&
                x.type == "blob").ToList();

            if (!githubItems.Any()) return;

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
                string itemType = folderName.ToUpper().TrimEnd('S');
                results.Added.Add($"{itemType}: {itemName}");
                string itemPath = $"{folderName}/{itemName}";
                string downloadUrl = $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubRepo}/{GitHubBranch}/{itemPath}";
                string destination = Path.Combine(localDirPath, itemName!);
                currentDownloads.Add(destination);
                UpdateStatus($"Downloading {itemType}: {itemName}");
                await DownloadFileWithProgress(downloadUrl, destination, _cts.Token, client);
            }
        }

        private async Task DownloadFileWithProgress(string url, string destination, CancellationToken cancellationToken, HttpClient client)
        {
            downloadProgressBar.Visible = true;
            downloadProgressLabel.Visible = true;
            downloadProgressBar.Value = 0;
            downloadProgressLabel.Text = "Starting download...";
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
                                    downloadProgressLabel.Text = $"{mbRead:F2} MB / {mbTotal:F2} MB downloaded";
                                    CenterControlX(downloadProgressLabel, progressPanel);
                                }
                                else
                                {
                                    downloadProgressLabel.Text = string.Format("{0:F2} MB downloaded", Math.Round(totalRead / 1048576.0, 2));
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
                    string downloadUrl = $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubRepo}/{GitHubBranch}/{Uri.EscapeDataString(exeName)}";

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

            // Show welcome panel immediately with checking status
            welcomePanel.Visible = true;
            updateStatusLabel.Text = "🔄 Checking for updater updates...";
            updateStatusLabel.ForeColor = Color.Gold;
            updateStatusLabel.Visible = true;
            CenterControlX(updateStatusLabel, welcomePanel);

            // Cleanup any orphaned backup files from previous updates
            CleanupOrphanedBackupFiles();

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
                            var downloadUrl = $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubRepo}/{GitHubBranch}/{Uri.EscapeDataString(exeName)}";
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

            // Write results to txt file (disabled by default - uncomment if needed for debugging)
            // string matchText = remoteVersion.Contains("could not get") ? "could not determine" : (string.Equals(currentVersion, remoteVersion, StringComparison.Ordinal) ? "True" : "False");
            // string results = $"Current Version: {currentVersion}\nRemote Version: {remoteVersion}\nMatch: {matchText}\n\nSTEP LOG:\n{string.Join("\n", steps)}";
            // File.WriteAllText(resultsFile, results);

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
    }
}
