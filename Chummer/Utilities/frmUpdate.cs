/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */
﻿using System;
using System.ComponentModel;
using System.Diagnostics;
 using System.Globalization;
 using System.IO;
 using System.IO.Compression;
 using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
﻿using System.Windows;
﻿using Application = System.Windows.Forms.Application;
﻿using MessageBox = System.Windows.Forms.MessageBox;

namespace Chummer
{
    public partial class frmUpdate : Form
    {

        private bool _blnSilentMode;
        private bool _blnSilentCheck;
        private bool _blnUnBlocked;
        private string _strDownloadFile = string.Empty;
        private string _strLatestVersion = string.Empty;
        private string _strCurrentVersion = string.Empty;
        private string _strTempPath = string.Empty;
        private readonly string _strAppPath = Application.StartupPath;
        private bool _blnPreferNightly = false;
        public frmUpdate()
        {
            Log.Info("frmUpdate");
            InitializeComponent();
            LanguageManager.Load(GlobalOptions.Language, this);
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            _strCurrentVersion = $"{version.Major}.{version.Minor}.{version.Build}";
            _blnPreferNightly = GlobalOptions.PreferNightlyBuilds;
        }

        private void frmUpdate_Load(object sender, EventArgs e)
        {
            Log.Info("frmUpdate_Load");
            _blnUnBlocked = CheckConnection("https://raw.githubusercontent.com/chummer5a/chummer5a/master/Chummer/changelog.txt");

            if (_blnUnBlocked)
            {
                GetChummerVersion();
                if (!_blnSilentMode)
                {
                    WebClient wc = new WebClient();
                    IWebProxy wp = WebRequest.DefaultWebProxy;
                    wp.Credentials = CredentialCache.DefaultCredentials;
                    wc.Proxy = wp;
                    wc.Encoding = Encoding.UTF8;
                    Log.Info("Download the changelog");
                    wc.DownloadFile("https://raw.githubusercontent.com/chummer5a/chummer5a/" + LatestVersion + "/Chummer/changelog.txt",
                        Path.Combine(Application.StartupPath, "changelog.txt"));
                    webNotes.DocumentText = "<font size=\"-1\" face=\"Courier New,Serif\">" +
                                            File.ReadAllText(Path.Combine(Application.StartupPath, "changelog.txt"))
                                                .Replace("&", "&amp;")
                                                .Replace("<", "&lt;")
                                                .Replace(">", "&gt;")
                                                .Replace("\n", "<br />") + "</font>";
                }

                Log.Info("Check Global Mutex for duplicate");
                bool blnHasDuplicate = !Program.GlobalChummerMutex.WaitOne(0, false);
                Log.Info("blnHasDuplicate = " + blnHasDuplicate.ToString());
                // If there is more than 1 instance running, do not let the application be updated.
                if (blnHasDuplicate)
                {
                    Log.Info("More than one instance, exiting");
                    if (!_blnSilentMode && !_blnSilentCheck)
                        MessageBox.Show(LanguageManager.GetString("Message_Update_MultipleInstances"),
                            LanguageManager.GetString("Title_Update"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    Log.Info("frmUpdate_Load");
                    Close();
                }
            }
            else
            {
                MessageBox.Show(LanguageManager.GetString("Warning_Update_CouldNotConnect"), "Chummer5",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log.Exit("frmUpdate_Load");
                Close();
            }
            Log.Exit("frmUpdate_Load");
        }

        private bool CheckConnection(string strURL)
        {
            Uri uriConnectionAddress;
            if (Uri.TryCreate(strURL, UriKind.Absolute, out uriConnectionAddress))
            {
                HttpWebRequest request = WebRequest.Create(uriConnectionAddress) as HttpWebRequest;

                if (request != null)
                {
                    //if (request.Proxy != null)
                    //request.Proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
                    request.Timeout = 5000;
                    request.Credentials = CredentialCache.DefaultNetworkCredentials;
                    HttpWebResponse response = request.GetResponse() as HttpWebResponse;

                    if (response != null)
                        return response.StatusCode == HttpStatusCode.OK;
                }
            }
            return false;
        }

        private void GetChummerVersion()
        {
            if (_blnUnBlocked)
            {
                string strUpdateLocation = "https://api.github.com/repos/chummer5a/chummer5a/releases/latest";
                if (_blnPreferNightly)
                {
                    strUpdateLocation = "https://api.github.com/repos/chummer5a/chummer5a/releases";
                }
                HttpWebRequest request = WebRequest.Create(strUpdateLocation) as HttpWebRequest;
                if (request == null)
                    return;
                request.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)";
                request.Accept = "application/json";
                // Get the response.

                HttpWebResponse response = request.GetResponse() as HttpWebResponse;

                // Get the stream containing content returned by the server.
                Stream dataStream = response?.GetResponseStream();
                if (dataStream == null)
                    return;
                // Open the stream using a StreamReader for easy access.
                StreamReader reader = new StreamReader(dataStream);
                // Read the content.

                string responseFromServer = reader.ReadToEnd();
                string[] stringSeparators = new string[] {","};
                string[] result = responseFromServer.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);

                bool blnFoundTag = false;
                bool blnFoundArchive = false;
                foreach (string line in result)
                {
                    if (!blnFoundTag && line.Contains("tag_name"))
                    {
                        _strLatestVersion = line.Split(':')[1];
                        LatestVersion = _strLatestVersion.Split('}')[0].FastEscape('\"');
                        blnFoundTag = true;
                        if (blnFoundArchive)
                            break;
                    }
                    if (!blnFoundArchive && line.Contains("browser_download_url"))
                    {
                        _strDownloadFile = line.Split(':')[2];
                        _strDownloadFile = _strDownloadFile.Substring(2);
                        _strDownloadFile = _strDownloadFile.Split('}')[0].FastEscape('\"');
                        _strDownloadFile = "https://" + _strDownloadFile;
                        blnFoundArchive = true;
                        if (blnFoundTag)
                            break;
                    }
                }
                // Cleanup the streams and the response.
                reader.Close();
                dataStream.Close();
                response.Close();
            }
            else
            {
                LatestVersion = LanguageManager.GetString("String_No_Update_Found");
            }
            DoVersionTextUpdate();
        }

        /// <summary>
        /// When checking if a new version is available, don't show the update window.
        /// </summary>
        public bool SilentCheck
        {
            get
            {
                return _blnSilentCheck;
            }
            set
            {
                _blnSilentCheck = value;
            }
        }

        /// <summary>
        /// When running in silent mode, the update window will not be shown.
        /// </summary>
        public bool SilentMode
        {
            get
            {
                return _blnSilentMode;
            }
            set
            {
                _blnSilentMode = value;
            }
        }

        /// <summary>
        /// Latest release build number located on Github.
        /// </summary>
        public string LatestVersion
        {
            get
            {
                return _strLatestVersion;
            }
            set
            {
                _strLatestVersion = value;
                _strTempPath = Path.Combine(Path.GetTempPath(), "chummer" + _strLatestVersion + ".zip");
            }
        }

        /// <summary>
        /// Latest release build number located on Github.
        /// </summary>
        public string CurrentVersion
        {
            get
            {
                return _strCurrentVersion;
            }
        }

        public void DoVersionTextUpdate()
        {
            string strLatestVersion = LatestVersion.Trim();
            lblUpdaterStatus.Left = lblUpdaterStatusLabel.Left + lblUpdaterStatusLabel.Width + 6;
            if (strLatestVersion == LanguageManager.GetString("String_No_Update_Found").Trim())
            {
                lblUpdaterStatus.Text = LanguageManager.GetString("Warning_Update_CouldNotConnect");
                cmdUpdate.Enabled = false;
                return;
            }

            string strCurrentVersion = CurrentVersion.Trim().TrimStart("Nightly-v");
            string[] strCurrentVersionNumbers = strCurrentVersion.Split('.');
            strLatestVersion = strLatestVersion.TrimStart("Nightly-v");
            string[] strLatestVersionNumbers = strLatestVersion.Split('.');

            bool blnNeedsUpdate = false;
            int intLatestTemp = 0;
            int intCurrentTemp = 0;
            // Note: this value only matters if blnNeedsUpdate is false, otherwise the relevant code will not run anyway
            bool blnDisableDownloadButton = true;
            for (int i = 0; i < strLatestVersionNumbers.Length; ++i)
            {
                if (strCurrentVersion.Length <= i)
                {
                    blnNeedsUpdate = true;
                    break;
                }
                if (int.TryParse(strLatestVersionNumbers[i], out intLatestTemp) && int.TryParse(strCurrentVersionNumbers[i], out intCurrentTemp))
                {
                    if (intLatestTemp != intCurrentTemp)
                    {
                        if (intLatestTemp > intCurrentTemp)
                            blnNeedsUpdate = true;
                        else
                            blnDisableDownloadButton = false;
                        break;
                    }
                }
            }

            if (blnNeedsUpdate)
            {
                lblUpdaterStatus.Text = LanguageManager.GetString("String_Update_Available").Replace("{0}", strLatestVersion).Replace("{1}", strCurrentVersion);
            }
            else
            {
                lblUpdaterStatus.Text = LanguageManager.GetString("String_Up_To_Date").Replace("{0}", strCurrentVersion).Replace("{1}", LanguageManager.GetString(_blnPreferNightly ? "String_Nightly" : "String_Stable")).Replace("{2}", strLatestVersion);
                if (blnDisableDownloadButton)
                {
                    cmdUpdate.Text = LanguageManager.GetString("Button_Up_To_Date");
                    cmdUpdate.Enabled = false;
                }
                else
                    cmdUpdate.Text = LanguageManager.GetString("Button_Redownload");
            }
            if (_blnPreferNightly)
                lblUpdaterStatus.Text += " " + LanguageManager.GetString("String_Nightly_Changelog_Warning");
        }

        private void cmdDownload_Click(object sender, EventArgs e)
        {
            Log.Info("cmdUpdate_Click");
            Log.Info("Download updates");
            DownloadUpdates();
        }

        private void cmdRestart_Click(object sender, EventArgs e)
        {
            Log.Info("cmdRestart_Click");
            if (Directory.Exists(_strAppPath) && File.Exists(_strTempPath))
            {
                cmdUpdate.Enabled = false;
                cmdRestart.Enabled = false;
                //Create a backup file in the temp directory. 
                string strBackupZipPath = Path.Combine(Path.GetTempPath(), "chummer" + CurrentVersion + ".zip");
                Log.Info("Creating archive from application path: ", _strAppPath);
                try
                {
                    if (!File.Exists(strBackupZipPath))
                    {
                        ZipFile.CreateFromDirectory(_strAppPath, strBackupZipPath, CompressionLevel.Fastest, true);
                    }
                    // Delete the old Chummer5 executables, libraries, and other files whose current versions are in use, then rename the current versions.
                    foreach (string strLoopExeName in Directory.GetFiles(_strAppPath, "*.exe", SearchOption.AllDirectories))
                    {
                        if (File.Exists(strLoopExeName + ".old"))
                            File.Delete(strLoopExeName + ".old");
                        File.Move(strLoopExeName, strLoopExeName + ".old");
                    }
                    foreach (string strLoopDllName in Directory.GetFiles(_strAppPath, "*.dll", SearchOption.AllDirectories))
                    {
                        if (File.Exists(strLoopDllName + ".old"))
                            File.Delete(strLoopDllName + ".old");
                        File.Move(strLoopDllName, strLoopDllName + ".old");
                    }
                    foreach (string strLoopPdbName in Directory.GetFiles(_strAppPath, "*.pdb", SearchOption.AllDirectories))
                    {
                        if (File.Exists(strLoopPdbName + ".old"))
                            File.Delete(strLoopPdbName + ".old");
                        File.Move(strLoopPdbName, strLoopPdbName + ".old");
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show(LanguageManager.GetString("Message_Insufficient_Permissions_Warning"));
                    return;
                }

                // Copy over the archive from the temp directory.
                Log.Info("Extracting downloaded archive into application path: ", _strTempPath);
                using (ZipArchive archive = ZipFile.Open(_strTempPath, ZipArchiveMode.Read, Encoding.GetEncoding(850)))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        // Skip directories because they already get handled with Directory.CreateDirectory
                        if (entry.FullName.Length > 0 && entry.FullName[entry.FullName.Length - 1] == '/')
                            continue;
                        string strLoopPath = Path.Combine(_strAppPath, entry.FullName);
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(strLoopPath));
                            entry.ExtractToFile(strLoopPath, true);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            MessageBox.Show(LanguageManager.GetString("Message_Insufficient_Permissions_Warning"));
                            break;
                        }
                    }
                }
                Log.Info("Restart Chummer");
                Application.Restart();
                cmdUpdate.Enabled = true;
                cmdRestart.Enabled = true;
            }
        }

        private void DownloadUpdates()
        {
            Uri uriDownloadFileAddress;
            if (!Uri.TryCreate(_strDownloadFile, UriKind.Absolute, out uriDownloadFileAddress))
                return;
            Log.Enter("DownloadUpdates");
            cmdUpdate.Enabled = false;
            cmdRestart.Enabled = false;
            if (File.Exists(_strTempPath))
                File.Delete(_strTempPath);
            WebClient client = new WebClient();
            client.DownloadProgressChanged += wc_DownloadProgressChanged;
            client.DownloadFileCompleted += wc_DownloadCompleted;
            client.DownloadFileAsync(uriDownloadFileAddress, _strTempPath);
        }

        #region AsyncDownload Events
        /// <summary>
        /// Update the download progress for the file.
        /// </summary>
        private void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            int intTmp;
            if (int.TryParse((e.BytesReceived * 100 / e.TotalBytesToReceive).ToString(), out intTmp))
                pgbOverallProgress.Value = intTmp;
        }


        /// <summary>
        /// The EXE file is down downloading, so replace the old file with the new one.
        /// </summary>
        private void wc_DownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            cmdUpdate.Text = LanguageManager.GetString("Button_Redownload");
            cmdUpdate.Enabled = true;
            cmdRestart.Enabled = true;
            Log.Info("wc_DownloadExeFileCompleted");
            Log.Exit("wc_DownloadExeFileCompleted");
        }

        #endregion
    }
}
