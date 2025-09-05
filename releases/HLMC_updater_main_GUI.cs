using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
#pragma warning disable IL2026, CS8602

namespace HLMCUpdater
{
    public partial class MainForm : Form
    {
        // --- Configuration ---
        const string GitHubOwner = "pjampjam";
        const string GitHubRepo = "HLMC";
        const string GitHubBranch = "main";
        const string ProgramVersion = "v1.1.0.0";

        Panel welcomePanel, progressPanel, summaryPanel;
        Label titleLabel, creditLabel, versionLabel, statusLabel, downloadProgressLabel, summaryTitleLabel, summaryCountLabel;
        Button startButton, closeButton, cancelButton;
        ProgressBar downloadProgressBar;
        TextBox summaryTextBox;
        CancellationTokenSource _cts;

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

                // Fallback to default Minecraft path
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ".minecraft"
                );
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

        public MainForm()
        {
            try
            {
                _cts = new CancellationTokenSource();

                // --- Main Form Setup ---
                this.Text = "HLMC Mod Updater";
                this.Size = new Size(500, 350); // Reduced size, increased height for better button spacing
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

            titleLabel = new Label
            {
                Text = "Holy Lois Client Updater",
                Font = new Font("Arial", 24, FontStyle.Bold),
                AutoSize = true,
                ForeColor = Color.Gold
            };
            welcomePanel.Controls.Add(titleLabel);

            creditLabel = new Label
            {
                Text = "created by pjampjam ( ͡° ͜ʖ ͡°)",
                Font = new Font("Arial", 10),
                AutoSize = true,
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            welcomePanel.Controls.Add(creditLabel);

            startButton = new Button
            {
                Text = "Update Modpack",
                Size = new Size(220, 50),
                Font = new Font("Arial", 13, FontStyle.Bold),
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
                Text = ProgramVersion,
                Font = new Font("Arial", 10),
                AutoSize = true,
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            welcomePanel.Controls.Add(versionLabel);

            // --- Progress View Controls ---
            progressPanel = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(40, 40, 40) };
            this.Controls.Add(progressPanel);

            statusLabel = new Label
            {
                Text = "Initializing...",
                Font = new Font("Arial", 14, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 120),
                ForeColor = Color.Gold
            };
            progressPanel.Controls.Add(statusLabel);

            downloadProgressBar = new ProgressBar
            {
                Size = new Size(500, 25),
                Location = new Point(40, 190),
                Visible = false
            };
            progressPanel.Controls.Add(downloadProgressBar);

            downloadProgressLabel = new Label
            {
                Location = new Point(0, 220),
                Font = new Font("Arial", 9),
                Text = "",
                Visible = false,
                ForeColor = Color.White
            };
            progressPanel.Controls.Add(downloadProgressLabel);

            cancelButton = new Button
            {
                Text = "Cancel",
                Size = new Size(120, 40),
                Font = new Font("Arial", 10),
                BackColor = Color.FromArgb(180, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point((progressPanel.Width - 120) / 2, progressPanel.Height - 60)
            };
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
            cancelButton.FlatAppearance.BorderSize = 2;
            cancelButton.Click += (s, e) => _cts?.Cancel();
            progressPanel.Controls.Add(cancelButton);

            // --- Summary View Controls ---
            summaryPanel = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(40, 40, 40) };
            this.Controls.Add(summaryPanel);

            summaryTitleLabel = new Label
            {
                Text = "Update Complete!",
                Font = new Font("Arial", 16, FontStyle.Bold),
                AutoSize = true,
                ForeColor = Color.Gold
            };
            summaryPanel.Controls.Add(summaryTitleLabel);

            summaryCountLabel = new Label
            {
                Text = "",
                Font = new Font("Arial", 10),
                AutoSize = true,
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
                Location = new Point((summaryPanel.Width - 460) / 2, 80),
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
                BackColor = Color.FromArgb(60, 180, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point((summaryPanel.Width - 120) / 2, summaryPanel.Height - 60)
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
                        var response = await client.GetAsync(zipUrl);
                        response.EnsureSuccessStatusCode();

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = File.OpenWrite(tempZipPath))
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                    }
                    catch { results.Status = "WARNING"; }

                    if (File.Exists(tempZipPath))
                    {
                        string tempUnzipDir = Path.Combine(tempDir, "unzipped_config");
                        downloadProgressBar.Visible = true;
                        downloadProgressBar.Style = ProgressBarStyle.Marquee;
                        downloadProgressLabel.Visible = true;
                        downloadProgressLabel.Text = "Expanding config.zip...";

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
                summaryCountLabel.Text = "An error occurred during the update.";
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
                    summaryCountLabel.Text = "";
                    summaryTextBox.Visible = false;
                }
                else if (results.Status == "WARNING")
                {
                    summaryTitleLabel.Text = "Update Complete (with warnings)";
                    summaryTitleLabel.ForeColor = Color.Orange;
                    summaryCountLabel.Text = "Config.zip not found on repository. Other items were synced.";
                    summaryTextBox.Visible = true;
                }
                else
                {
                    summaryTitleLabel.Text = "Update Complete!";
                    summaryTitleLabel.ForeColor = Color.Green;
                    summaryCountLabel.Text = $"Mods: {addedMods} added, {removedMods} removed. Resource Packs: {addedPacks} added, {removedPacks} removed.";
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
            this.Refresh();
        }

        private async Task<List<RepoItem>> FetchRepoTree()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("HLMCUpdater");
                string apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/git/trees/{GitHubBranch}?recursive=1";
                var json = await client.GetStringAsync(apiUrl);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var doc = JsonDocument.Parse(json);
                var tree = doc.RootElement.GetProperty("tree");
                var items = new List<RepoItem>();
                foreach (var item in tree.EnumerateArray())
                {
                    var repoItem = new RepoItem
                    {
                        path = item.GetProperty("path").GetString(),
                        type = item.GetProperty("type").GetString()
                    };
                    if (!string.IsNullOrEmpty(repoItem.path))
                    {
                        items.Add(repoItem);
                    }
                }
                return items;
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
                UpdateStatus($"Downloading {itemType}: {itemName}");
                await DownloadFileWithProgress(downloadUrl, Path.Combine(localDirPath, itemName!), _cts.Token, client);
            }
        }

        private async Task DownloadFileWithProgress(string url, string destination, CancellationToken cancellationToken, HttpClient client)
        {
            downloadProgressBar.Visible = true;
            downloadProgressLabel.Visible = true;
            downloadProgressBar.Value = 0;
            downloadProgressLabel.Text = "Starting download...";

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
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        long totalRead = 0;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            fileStream.Write(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                downloadProgressBar.Value = (int)Math.Min(totalRead, downloadProgressBar.Maximum);
                                double mbRead = Math.Round(totalRead / 1048576.0, 2);
                                double mbTotal = Math.Round(totalBytes / 1048576.0, 2);
                                downloadProgressLabel.Text = $"{mbRead} MB / {mbTotal} MB downloaded";
                            }
                            else
                            {
                                downloadProgressLabel.Text = $"{Math.Round(totalRead / 1048576.0, 2)} MB downloaded";
                            }
                            this.Refresh();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Clean up partially downloaded file
                if (File.Exists(destination))
                {
                    try { File.Delete(destination); } catch { }
                }
                throw;
            }

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
                    string apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
                    var json = await client.GetStringAsync(apiUrl);
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                    if (release?.Assets?.Count > 0 && release.TagName != ProgramVersion)
                    {
                        // Show update button
                        var updateButton = new Button
                        {
                            Text = "Update Updater",
                            Size = new Size(220, 50),
                            Font = new Font("Segoe UI", 13, FontStyle.Bold),
                            BackColor = Color.FromArgb(60, 180, 75),
                            ForeColor = Color.White,
                            FlatStyle = FlatStyle.Flat,
                            Location = new Point((welcomePanel.Width - 220) / 2, 200)
                        };
                        updateButton.FlatAppearance.BorderColor = Color.FromArgb(70, 120, 70);
                        updateButton.FlatAppearance.BorderSize = 2;
                        updateButton.Click += async (s, e) =>
                        {
                            // Add null check for release.Assets[0].BrowserDownloadUrl
                            if (release.Assets != null && release.Assets.Count > 0 && !string.IsNullOrEmpty(release.Assets[0].BrowserDownloadUrl))
                            {
                                await DownloadUpdaterUpdate(release.Assets[0].BrowserDownloadUrl!);
                            }
                        };
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking for updates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DownloadUpdaterUpdate(string downloadUrl)
        {
            // Add null check for downloadUrl
            if (string.IsNullOrEmpty(downloadUrl))
            {
                UpdateStatus("Invalid download URL");
                return;
            }

            string? currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
            {
                UpdateStatus("Failed to get current executable path");
                return;
            }

            string exeDir = Path.GetDirectoryName(currentExe)!;
            string newExePath = Path.Combine(exeDir, "Holy Lois Updater new.exe");
            string backupExePath = Path.Combine(exeDir, "Holy Lois Updater.old.exe");

            try
            {
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
                    if (File.Exists(newExePath)) File.Delete(newExePath);
                }
                catch { }
            }
        }
        private async void MainForm_Load(object? sender, EventArgs e)
        {
            // Position controls - consistent centering like welcome screen
            titleLabel.Location = new Point((welcomePanel.Width - titleLabel.Width) / 2, 60);
            creditLabel.Location = new Point((welcomePanel.Width - creditLabel.Width) / 2, 120);
            startButton.Location = new Point((welcomePanel.Width - startButton.Width) / 2, 200);
            versionLabel.Location = new Point((welcomePanel.Width - versionLabel.Width) / 2, this.Height - versionLabel.Height - 50);

            // Center progress controls with consistent method
            statusLabel.Location = new Point((progressPanel.Width - statusLabel.Width) / 2, 120);
            downloadProgressBar.Location = new Point((progressPanel.Width - downloadProgressBar.Width) / 2, 173);
            downloadProgressLabel.Location = new Point((progressPanel.Width - downloadProgressLabel.Width) / 2, 208);

            // Center summary controls with consistent method
            summaryTitleLabel.Location = new Point((summaryPanel.Width - summaryTitleLabel.Width) / 2, 20);
            summaryCountLabel.Location = new Point((summaryPanel.Width - summaryCountLabel.Width) / 2, 110);
            summaryTextBox.Size = new Size(460, 150);
            summaryTextBox.Location = new Point((summaryPanel.Width - summaryTextBox.Width) / 2, 150);

            // Center entire progress group vertically
            downloadProgressBar.Location = new Point((progressPanel.Width - downloadProgressBar.Width) / 2, 173);
            downloadProgressLabel.Location = new Point(0, 208); // Better centering

            // Center entire summary group vertically
            summaryCountLabel.Location = new Point(0, 110);
            summaryTextBox.Size = new Size(460, 150);
            summaryTextBox.Location = new Point((summaryPanel.Width - summaryTextBox.Width) / 2, 150);

            // Lower cancel and close buttons
            cancelButton.Location = new Point((progressPanel.Width - 120) / 2, progressPanel.Height - 50);
            closeButton.Location = new Point((summaryPanel.Width - 120) / 2, summaryPanel.Height - 50);

            await CheckForUpdaterUpdatesOnStartup();
        }

        private async Task CheckForUpdaterUpdatesOnStartup()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("HLMCUpdater");
                    string apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
                    var json = await client.GetStringAsync(apiUrl);
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                    if (release?.Assets?.Count > 0 && release.TagName != ProgramVersion)
                    {
                        // Replace the modpack update button with updater update button
                        welcomePanel.Controls.Remove(startButton);

                        var updateButton = new Button
                        {
                            Text = "Update Updater",
                            Size = new Size(220, 50),
                            Font = new Font("Arial", 13, FontStyle.Bold),
                            BackColor = Color.FromArgb(255, 165, 0), // Orange color for urgency
                            ForeColor = Color.White,
                            FlatStyle = FlatStyle.Flat,
                            Location = new Point((welcomePanel.Width - 220) / 2, 160)
                        };
                        updateButton.FlatAppearance.BorderColor = Color.FromArgb(200, 140, 0);
                        updateButton.FlatAppearance.BorderSize = 2;
                        updateButton.Click += async (s, e) =>
                        {
                            if (release.Assets != null && release.Assets.Count > 0 && !string.IsNullOrEmpty(release.Assets[0].BrowserDownloadUrl))
                            {
                                await DownloadUpdaterUpdate(release.Assets[0].BrowserDownloadUrl!);
                            }
                        };
                        welcomePanel.Controls.Add(updateButton);
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail - just continue with normal operation
            }
        }
        // --- Helper Classes ---
        class RepoItem
        {
            public string? path { get; set; }
            public string? type { get; set; }
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