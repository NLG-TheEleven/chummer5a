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
 using System;
 using System.Collections.Generic;
 using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Reflection;
 using System.Text.RegularExpressions;
 using System.Windows;
 using System.Windows.Shapes;
 using Chummer.Backend.Equipment;
 using Chummer.Skills;
 using Application = System.Windows.Forms.Application;
 using DataFormats = System.Windows.Forms.DataFormats;
 using DragDropEffects = System.Windows.Forms.DragDropEffects;
 using DragEventArgs = System.Windows.Forms.DragEventArgs;
 using MessageBox = System.Windows.Forms.MessageBox;
 using Path = System.IO.Path;
 using Point = System.Drawing.Point;
 using Rectangle = System.Drawing.Rectangle;
 using Size = System.Drawing.Size;

namespace Chummer
{
    public partial class frmMain : Form
    {
        private frmOmae _frmOmae;
        private frmDiceRoller _frmRoller;
        private frmUpdate _frmUpdate;
        private List<Character> _lstCharacters = new List<Character>();
        #region Control Events
        public frmMain()
        {
            InitializeComponent();
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            string strCurrentVersion = $"{version.Major}.{version.Minor}.{version.Build}";

            Text = string.Format("Chummer 5a - Version " + strCurrentVersion);

#if DEBUG
            Text += " DEBUG BUILD";
#endif

            LanguageManager.Load(GlobalOptions.Language, this);

            /** Dashboard **/
            //this.toolsMenu.DropDownItems.Add("GM Dashboard").Click += this.dashboardToolStripMenuItem_Click;
            /** End Dashboard **/

            // If Automatic Updates are enabled, check for updates immediately.

#if RELEASE
            if (Utils.GitUpdateAvailable() > 0)
            {
                if (GlobalOptions.AutomaticUpdate)
                {
                    frmUpdate frmAutoUpdate = new frmUpdate();
                    frmAutoUpdate.SilentMode = true;
                    frmAutoUpdate.Visible = false;
                    frmAutoUpdate.ShowDialog(this);
                }
                else
                {
                    this.Text += String.Format(" - Update {0} now available!", Utils.GitVersion());
                }
            }
#endif

            GlobalOptions.MRUChanged += PopulateMRU;

            // Delete the old executable if it exists (created by the update process).
            foreach (string strLoopOldFilePath in Directory.GetFiles(Application.StartupPath, "*.old"))
            {
                if (File.Exists(strLoopOldFilePath))
                    File.Delete(strLoopOldFilePath);
            }

            // Populate the MRU list.
            PopulateMRU();

            GlobalOptions.MainForm = this;

            // Set the Tag for each ToolStrip item so it can be translated.
            foreach (ToolStripMenuItem objItem in menuStrip.Items.OfType<ToolStripMenuItem>())
            {
                if (objItem.Tag != null)
                {
                    objItem.Text = LanguageManager.GetString(objItem.Tag.ToString());
                }
            }

            // ToolStrip Items.
            foreach (ToolStrip objToolStrip in Controls.OfType<ToolStrip>())
            {
                foreach (ToolStripButton objButton in objToolStrip.Items.OfType<ToolStripButton>())
                {
                    if (objButton.Tag != null)
                        objButton.Text = LanguageManager.GetString(objButton.Tag.ToString());
                }
            }

            // Attempt to cache all XML files that are used the most.
            Timekeeper.Start("cache_load");
            XmlManager.Load("armor.xml");
            XmlManager.Load("bioware.xml");
            XmlManager.Load("books.xml");
            XmlManager.Load("complexforms.xml");
            XmlManager.Load("contacts.xml");
            XmlManager.Load("critters.xml");
            XmlManager.Load("critterpowers.xml");
            XmlManager.Load("cyberware.xml");
            // XmlManager.Load("drugcomponents.xml"); TODO: Re-enable when Custom Drugs branch is merged
            XmlManager.Load("echoes.xml");
            XmlManager.Load("gameplayoptions.xml");
            XmlManager.Load("gear.xml");
            XmlManager.Load("improvements.xml");
            XmlManager.Load("licenses.xml");
            XmlManager.Load("lifemodules.xml");
            XmlManager.Load("lifestyles.xml");
            XmlManager.Load("martialarts.xml");
            XmlManager.Load("mentors.xml");
            XmlManager.Load("metamagic.xml");
            XmlManager.Load("metatypes.xml");
            XmlManager.Load("options.xml");
            XmlManager.Load("packs.xml");
            XmlManager.Load("powers.xml");
            XmlManager.Load("priorities.xml");
            XmlManager.Load("programs.xml");
            XmlManager.Load("qualities.xml");
            XmlManager.Load("ranges.xml");
            XmlManager.Load("skills.xml");
            XmlManager.Load("spells.xml");
            XmlManager.Load("spiritpowers.xml");
            XmlManager.Load("traditions.xml");
            XmlManager.Load("vehicles.xml");
            XmlManager.Load("weapons.xml");
            Timekeeper.Finish("cache_load");

            frmCharacterRoster frmCharacter = new frmCharacterRoster();
            frmCharacter.MdiParent = this;

            // Retrieve the arguments passed to the application. If more than 1 is passed, we're being given the name of a file to open.
            string[] strArgs = Environment.GetCommandLineArgs();
            if (strArgs.GetUpperBound(0) > 0)
            {
                if (strArgs[1] != "/debug")
                    LoadCharacter(strArgs[1]);
                if (strArgs.Length > 2)
                {
                    if (strArgs[2] == "/test")
                    {
                        frmTest frmTestData = new frmTest();
                        frmTestData.Show();
                    }
                }
            }

            frmCharacter.WindowState = FormWindowState.Maximized;
            frmCharacter.Show();
        }

        /*
        public sealed override string Text
        {
            get { return base.Text; }
            set { base.Text = value; }
        }
        */

        private void ExitToolsStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void dashboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmGMDashboard.Instance.Show();
        }

        private void CloseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form childForm in MdiChildren)
            {
                childForm.Close();
            }
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmOptions frmOptions = new frmOptions();
            frmOptions.ShowDialog(this);
        }

        private void mnuToolsUpdate_Click(object sender, EventArgs e)
        {
            // Only a single instance of the updater can be open, so either find the current instance and focus on it, or create a new one.
            if (_frmUpdate == null)
            {
                _frmUpdate = new frmUpdate();
                _frmUpdate.Show();
            }
            else
            {
                _frmUpdate.Focus();
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmAbout frmShowAbout = new frmAbout();
            frmShowAbout.ShowDialog(this);
        }

        private void contentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://www.chummergen.com/chummer/wiki/");
        }

        private void mnuHelpDumpshock_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/chummer5a/chummer5a/issues/");
        }

        private void mnuFilePrintMultiple_Click(object sender, EventArgs e)
        {
            frmPrintMultiple frmPrintMultipleCharacters = new frmPrintMultiple();
            frmPrintMultipleCharacters.ShowDialog(this);
        }

        private void mnuHelpRevisionHistory_Click(object sender, EventArgs e)
        {
            frmHistory frmShowHistory = new frmHistory();
            frmShowHistory.ShowDialog(this);
        }

        private void mnuNewCritter_Click(object sender, EventArgs e)
        {
            Character objCharacter = new Character();
            string settingsPath = Path.Combine(Application.StartupPath, "settings");
            string[] settingsFiles = Directory.GetFiles(settingsPath, "*.xml");

            if (settingsFiles.Length > 1)
            {
                frmSelectSetting frmPickSetting = new frmSelectSetting();
                frmPickSetting.ShowDialog(this);

                if (frmPickSetting.DialogResult == DialogResult.Cancel)
                    return;

                objCharacter.SettingsFile = frmPickSetting.SettingsFile;
            }
            else
            {
                string strSettingsFile = settingsFiles[0];
                objCharacter.SettingsFile = Path.GetFileName(strSettingsFile);
            }

            // Override the defaults for the setting.
            objCharacter.IgnoreRules = true;
            objCharacter.IsCritter = true;
            objCharacter.Created = true;
            objCharacter.BuildMethod = CharacterBuildMethod.Karma;
            objCharacter.BuildPoints = 0;

            // Show the Metatype selection window.
            frmKarmaMetatype frmSelectMetatype = new frmKarmaMetatype(objCharacter);
            frmSelectMetatype.XmlFile = "critters.xml";
            frmSelectMetatype.ShowDialog();

            if (frmSelectMetatype.DialogResult == DialogResult.Cancel)
                return;
            Cursor.Current = Cursors.WaitCursor;

            // Add the Unarmed Attack Weapon to the character.
            XmlDocument objXmlDocument = XmlManager.Load("weapons.xml");
            XmlNode objXmlWeapon = objXmlDocument.SelectSingleNode("/chummer/weapons/weapon[name = \"Unarmed Attack\"]");
            if (objXmlWeapon != null)
            {
                TreeNode objDummy = new TreeNode();
                Weapon objWeapon = new Weapon(objCharacter);
                objWeapon.Create(objXmlWeapon, objDummy, null, null);
                objCharacter.Weapons.Add(objWeapon);
            }

            frmCareer frmNewCharacter = new frmCareer(objCharacter);
            frmNewCharacter.MdiParent = this;
            frmNewCharacter.WindowState = FormWindowState.Maximized;
            frmNewCharacter.Show();

            objCharacter.CharacterNameChanged += objCharacter_CharacterNameChanged;
            Cursor.Current = Cursors.Default;
        }

        private void mnuMRU_Click(object sender, EventArgs e)
        {
            string strFileName = ((ToolStripMenuItem)sender).Text;
            strFileName = strFileName.Substring(3, strFileName.Length - 3).Trim();
            LoadCharacter(strFileName);
        }

        private void mnuMRU_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                string strFileName = ((ToolStripMenuItem)sender).Text;
                strFileName = strFileName.Substring(3, strFileName.Length - 3).Trim();

                GlobalOptions.RemoveFromMRUList(strFileName);
                GlobalOptions.AddToMRUList(strFileName, "stickymru");
            }
        }

        private void mnuStickyMRU_Click(object sender, EventArgs e)
        {
            string strFileName = ((ToolStripMenuItem)sender).Text;
            LoadCharacter(strFileName);
        }

        private void mnuStickyMRU_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                string strFileName = ((ToolStripMenuItem)sender).Text;

                GlobalOptions.RemoveFromMRUList(strFileName, "stickymru");
                GlobalOptions.AddToMRUList(strFileName);
            }
        }

        private void frmMain_MdiChildActivate(object sender, EventArgs e)
        {
            // If there are no child forms, hide the tab control.
            if (ActiveMdiChild != null)
            {
                ActiveMdiChild.WindowState = FormWindowState.Maximized;

                // If this is a new child form and does not have a tab page, create one.
                if (ActiveMdiChild.Tag == null)
                {
                    TabPage tp = new TabPage();
                    // Add a tab page.
                    tp.Tag = ActiveMdiChild;
                    tp.Parent = tabForms;
                    
                    if (ActiveMdiChild.GetType() == typeof(frmCareer))
                    {
                        tp.Text = ((frmCareer)ActiveMdiChild).CharacterName;
                    }
                    else if (ActiveMdiChild.GetType() == typeof(frmCreate))
                    {
                        tp.Text = ((frmCreate)ActiveMdiChild).CharacterName;
                    }
                    else if (ActiveMdiChild.GetType() == typeof(frmCharacterRoster))
                    {
                        tp.Text = LanguageManager.GetString("String_CharacterRoster");
                    }

                    tabForms.SelectedTab = tp;

                    ActiveMdiChild.Tag = tp;
                    ActiveMdiChild.FormClosed += ActiveMdiChild_FormClosed;
                }
            }
            // Don't show the tab control if there is only one window open.
            if (tabForms.TabCount > 1)
                tabForms.Visible = true;
            else
                tabForms.Visible = false;
        }

        private void ActiveMdiChild_FormClosed(object sender, FormClosedEventArgs e)
        {
            Form objForm = sender as Form;
            if (objForm != null)
            {
                objForm.FormClosed -= ActiveMdiChild_FormClosed;
                objForm.Dispose();
                (objForm.Tag as TabPage)?.Dispose();
            }

            // Don't show the tab control if there is only one window open.
            if (tabForms.TabCount <= 1)
                tabForms.Visible = false;
        }

        private void tabForms_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabForms.SelectedTab != null && tabForms.SelectedTab.Tag != null)
                (tabForms.SelectedTab.Tag as Form)?.Select();
        }

        private void objCharacter_CharacterNameChanged(Object sender)
        {
            // Change the TabPage's text to match the character's name (or "Unnamed Character" if they are currently unnamed).
            if (tabForms.TabCount > 0 && tabForms.SelectedTab != null)
            {
                Character objCharacter = sender as Character;
                if (objCharacter != null)
                {
                    string strTitle = objCharacter.Name;
                    if (!string.IsNullOrEmpty(objCharacter.Alias.Trim()))
                    {
                        strTitle = objCharacter.Alias.Trim();
                    }
                    else if (string.IsNullOrEmpty(strTitle))
                    {
                        strTitle = LanguageManager.GetString("String_UnnamedCharacter");
                    }

                    tabForms.SelectedTab.Text = strTitle;
                }
            }
        }

        private void mnuToolsDiceRoller_Click(object sender, EventArgs e)
        {
            if (GlobalOptions.SingleDiceRoller)
            {
                // Only a single instance of the Dice Roller window is allowed, so either find the existing one and focus on it, or create a new one.
                if (_frmRoller == null)
                {
                    _frmRoller = new frmDiceRoller(this);
                    _frmRoller.Show();
                }
                else
                {
                    _frmRoller.Focus();
                }
            }
            else
            {
                // No limit on the number of Dice Roller windows, so just create a new one.
                frmDiceRoller frmRoller = new frmDiceRoller(this);
                frmRoller.Show();
            }
        }

        private void mnuToolsOmae_Click(object sender, EventArgs e)
        {
            // Only a single instance of Omae can be open, so either find the current instance and focus on it, or create a new one.
            if (_frmOmae == null)
            {
                _frmOmae = new frmOmae(this);
                _frmOmae.Show();
            }
            else
            {
                _frmOmae.Focus();
            }
        }

        private void Menu_DropDownOpening(object sender, EventArgs e)
        {
            // Translate the items in the menu by finding their Tags in the translation file.
            foreach (ToolStripMenuItem objItem in ((ToolStripMenuItem)sender).DropDownItems.OfType<ToolStripMenuItem>())
            {
                if (objItem.Tag != null)
                {
                    objItem.Text = LanguageManager.GetString(objItem.Tag.ToString());
                }
            }
        }

        private void menuStrip_ItemAdded(object sender, ToolStripItemEventArgs e)
        {
            // Translate the items in the menu by finding their Tags in the translation file.
            foreach (ToolStripMenuItem objItem in menuStrip.Items.OfType<ToolStripMenuItem>())
            {
                if (objItem.Tag != null)
                {
                    objItem.Text = LanguageManager.GetString(objItem.Tag.ToString());
                }
            }
        }

        private void toolStrip_ItemAdded(object sender, ToolStripItemEventArgs e)
        {
            // ToolStrip Items.
            foreach (ToolStrip objToolStrip in Controls.OfType<ToolStrip>())
            {
                foreach (ToolStripButton objButton in objToolStrip.Items.OfType<ToolStripButton>())
                {
                    if (objButton.Tag != null)
                        objButton.Text = LanguageManager.GetString(objButton.Tag.ToString());
                }
            }
        }

        private void toolStrip_ItemRemoved(object sender, ToolStripItemEventArgs e)
        {
            // ToolStrip Items.
            foreach (ToolStrip objToolStrip in Controls.OfType<ToolStrip>())
            {
                foreach (ToolStripButton objButton in objToolStrip.Items.OfType<ToolStripButton>())
                {
                    if (objButton.Tag != null)
                        objButton.Text = LanguageManager.GetString(objButton.Tag.ToString());
                }
            }
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.Size.Width == 0 || Properties.Settings.Default.Size.Height == 0 || !IsVisibleOnAnyScreen())
            {
                Size = new Size(1191, 752);
                StartPosition = FormStartPosition.CenterScreen;
            }
            else
            {
                WindowState = Properties.Settings.Default.WindowState;

                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;

                Location = Properties.Settings.Default.Location;
                Size = Properties.Settings.Default.Size;
            }

            if (GlobalOptions.StartupFullscreen)
                WindowState = FormWindowState.Maximized;

            mnuToolsOmae.Visible = GlobalOptions.OmaeEnabled;

    //        if (GlobalOptions.UseLogging)
    //        {
                //CommonFunctions objFunctions = new CommonFunctions();
    //        }
        }

        private bool IsVisibleOnAnyScreen()
        {
            return Screen.AllScreens.Any(screen => screen.WorkingArea.Contains(Properties.Settings.Default.Location));
        }

        private void frmMain_DragDrop(object sender, DragEventArgs e)
        {
            // Open each file that has been dropped into the window.
            string[] s = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            foreach (string strFileName in s)
                LoadCharacter(strFileName);
        }

        private void frmMain_DragEnter(object sender, DragEventArgs e)
        {
            // Only use a drop effect if a file is being dragged into the window.
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        private void trySkillToolStripMenuItem_Click(object sender, EventArgs e, Character objCharacter)
        {
            Skill objImpersonation = objCharacter?.SkillsSection?.GetActiveSkill("Impersonation");
            if (objImpersonation != null)
                MessageBox.Show(objImpersonation.Rating.ToString());
        }

        #endregion

        #region Methods
        /// <summary>
        /// Create a new character and show the Create Form.
        /// </summary>
        private void ShowNewForm(object sender, EventArgs e)
        {
            string strFilePath = Path.Combine(Application.StartupPath, "settings", "default.xml");
            if (!File.Exists(strFilePath))
            {
                if (MessageBox.Show(LanguageManager.GetString("Message_CharacterOptions_OpenOptions"), LanguageManager.GetString("MessageTitle_CharacterOptions_OpenOptions"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    frmOptions frmOptions = new frmOptions();
                    frmOptions.ShowDialog();
                }
            }
            Character objCharacter = new Character();
            string settingsPath = Path.Combine(Application.StartupPath, "settings");
            string[] settingsFiles = Directory.GetFiles(settingsPath, "*.xml");

            if (settingsFiles.Length > 1)
            {
                frmSelectSetting frmPickSetting = new frmSelectSetting();
                frmPickSetting.ShowDialog(this);

                if (frmPickSetting.DialogResult == DialogResult.Cancel)
                    return;

                objCharacter.SettingsFile = frmPickSetting.SettingsFile;
            }
            else
            {
                string strSettingsFile = settingsFiles[0];
                objCharacter.SettingsFile = Path.GetFileName(strSettingsFile);
            }

            // Show the BP selection window.
            frmSelectBuildMethod frmBP = new frmSelectBuildMethod(objCharacter);
            frmBP.ShowDialog();

            if (frmBP.DialogResult == DialogResult.Cancel)
                return;
            if (objCharacter.BuildMethod == CharacterBuildMethod.Karma || objCharacter.BuildMethod == CharacterBuildMethod.LifeModule)
            {
                frmKarmaMetatype frmSelectMetatype = new frmKarmaMetatype(objCharacter);
                frmSelectMetatype.ShowDialog();

                if (frmSelectMetatype.DialogResult == DialogResult.Cancel)
                { return; }
            }
            // Show the Metatype selection window.
            else if (objCharacter.BuildMethod == CharacterBuildMethod.Priority || objCharacter.BuildMethod == CharacterBuildMethod.SumtoTen)
            {
                frmPriorityMetatype frmSelectMetatype = new frmPriorityMetatype(objCharacter);
                frmSelectMetatype.ShowDialog();

                if (frmSelectMetatype.DialogResult == DialogResult.Cancel)
                { return; }
            }
            Cursor.Current = Cursors.WaitCursor;

            // Add the Unarmed Attack Weapon to the character.
            XmlDocument objXmlDocument = XmlManager.Load("weapons.xml");
            XmlNode objXmlWeapon = objXmlDocument.SelectSingleNode("/chummer/weapons/weapon[name = \"Unarmed Attack\"]");
            if (objXmlWeapon != null)
            {
                TreeNode objDummy = new TreeNode();
                Weapon objWeapon = new Weapon(objCharacter);
                objWeapon.Create(objXmlWeapon, objDummy, null, null);
                objCharacter.Weapons.Add(objWeapon);
            }

            frmCreate frmNewCharacter = new frmCreate(objCharacter);
            frmNewCharacter.MdiParent = this;
            frmNewCharacter.WindowState = FormWindowState.Maximized;
            frmNewCharacter.Show();

            OpenCharacters.Add(objCharacter);
            objCharacter.CharacterNameChanged += objCharacter_CharacterNameChanged;
            Cursor.Current = Cursors.Default;
        }

        /// <summary>
        /// Show the Open File dialogue, then load the selected character.
        /// </summary>
        private void OpenFile(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Chummer5 Files (*.chum5)|*.chum5|All Files (*.*)|*.*";
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                Timekeeper.Start("load_sum");
                foreach (string strFileName in openFileDialog.FileNames)
                {
                    LoadCharacter(strFileName);
                    Timekeeper.Start("load_event_time");
                    Application.DoEvents();
                    Timekeeper.Finish("load_event_time");
                }
                Timekeeper.Finish("load_sum");
                Timekeeper.Log();
            }
        }

        /// <summary>
        /// Load a Character and open the correct window.
        /// </summary>
        /// <param name="strFileName">File to load.</param>
        /// <param name="blnIncludeInMRU">Whether or not the file should appear in the MRU list.</param>
        /// <param name="strNewName">New name for the character.</param>
        /// <param name="blnClearFileName">Whether or not the name of the save file should be cleared.</param>
        public void LoadCharacter(string strFileName, bool blnIncludeInMRU = true, string strNewName = "", bool blnClearFileName = false)
        {
            if (File.Exists(strFileName) && strFileName.EndsWith("chum5"))
            {
                Timekeeper.Start("loading");
                Cursor.Current = Cursors.WaitCursor;
                bool blnLoaded = false;
                Character objCharacter = new Character();
                objCharacter.FileName = strFileName;

                XmlDocument objXmlDocument = new XmlDocument();
                //StreamReader is used to prevent encoding errors
                using (StreamReader sr = new StreamReader(strFileName, true))
                {
                    try
                    {
                        objXmlDocument.Load(sr);
                    }
                    catch (XmlException ex)
                    {
                        MessageBox.Show(LanguageManager.GetString("Message_FailedLoad").Replace("{0}", ex.Message), LanguageManager.GetString("MessageTitle_FailedLoad"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                XmlNode objXmlCharacter = objXmlDocument.SelectSingleNode("/character");
                if (!string.IsNullOrEmpty(objXmlCharacter?["appversion"]?.InnerText))
                {
                    Version verSavedVersion;
                    Version verCorrectedVersion;
                    string strVersion = objXmlCharacter["appversion"].InnerText;
                    if (strVersion.StartsWith("0."))
                    {
                        strVersion = strVersion.Substring(2);
                    }
                    Version.TryParse(strVersion, out verSavedVersion);
                    Version.TryParse("5.188.34", out verCorrectedVersion);
                    if (verCorrectedVersion != null && verSavedVersion != null)
                    {
                        int intResult = verSavedVersion.CompareTo(verCorrectedVersion);
                        //Check for typo in Corrupter quality and correct it
                        if (intResult == -1)
                        {
                            File.WriteAllText(strFileName, Regex.Replace(File.ReadAllText(strFileName), "Corruptor", "Corrupter"));
                        }
                    }
                }

                Timekeeper.Start("load_file");
                blnLoaded = objCharacter.Load();
                Timekeeper.Finish("load_file");
                Timekeeper.Start("load_free");
                if (!blnLoaded)
                {
                    Cursor.Current = Cursors.Default;
                    return;
                }

                // If a new name is given, set the character's name to match (used in cloning).
                if (!string.IsNullOrEmpty(strNewName))
                    objCharacter.Name = strNewName;
                // Clear the File Name field so that this does not accidentally overwrite the original save file (used in cloning).
                if (blnClearFileName)
                    objCharacter.FileName = string.Empty;

                // Show the character form.
                if (!objCharacter.Created)
                {
                    frmCreate frmCharacter = new frmCreate(objCharacter)
                    {
                        MdiParent = this,
                        WindowState = FormWindowState.Maximized,
                        Loading = true
                    };
                    frmCharacter.Show();
                }
                else
                {
                    frmCareer frmCharacter = new frmCareer(objCharacter)
                    {
                        MdiParent = this,
                        WindowState = FormWindowState.Maximized,
                        Loading = true
                    };
                    frmCharacter.DiceRollerOpened += objCareer_DiceRollerOpened;
                    frmCharacter.DiceRollerOpenedInt += objCareer_DiceRollerOpenedInt;
                    frmCharacter.Show();
                }

                if (blnIncludeInMRU)
                    GlobalOptions.AddToMRUList(strFileName);

                objCharacter.CharacterNameChanged += objCharacter_CharacterNameChanged;
                objCharacter_CharacterNameChanged(objCharacter);
                Cursor.Current = Cursors.Default;
            }
            else
            {
                MessageBox.Show(LanguageManager.GetString("Message_FileNotFound").Replace("{0}", strFileName), LanguageManager.GetString("MessageTitle_FileNotFound"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Populate the MRU items.
        /// </summary>
        public void PopulateMRU()
        {
            List<string> strStickyMRUList = GlobalOptions.ReadMRUList("stickymru");
            List<string> strMRUList = GlobalOptions.ReadMRUList();

            for (int i = 0; i < 10; i++)
            {
                ToolStripMenuItem objStickyItem;
                ToolStripMenuItem objItem;
                switch (i)
                {
                    case 0:
                        objStickyItem = mnuStickyMRU0;
                        objItem = mnuMRU0;
                        break;
                    case 1:
                        objStickyItem = mnuStickyMRU1;
                        objItem = mnuMRU1;
                        break;
                    case 2:
                        objStickyItem = mnuStickyMRU2;
                        objItem = mnuMRU2;
                        break;
                    case 3:
                        objStickyItem = mnuStickyMRU3;
                        objItem = mnuMRU3;
                        break;
                    case 4:
                        objStickyItem = mnuStickyMRU4;
                        objItem = mnuMRU4;
                        break;
                    case 5:
                        objStickyItem = mnuStickyMRU5;
                        objItem = mnuMRU5;
                        break;
                    case 6:
                        objStickyItem = mnuStickyMRU6;
                        objItem = mnuMRU6;
                        break;
                    case 7:
                        objStickyItem = mnuStickyMRU7;
                        objItem = mnuMRU7;
                        break;
                    case 8:
                        objStickyItem = mnuStickyMRU8;
                        objItem = mnuMRU8;
                        break;
                    case 9:
                        objStickyItem = mnuStickyMRU9;
                        objItem = mnuMRU9;
                        break;
                    default:
                        continue;
                }

                if (i < strStickyMRUList.Count)
                {
                    objStickyItem.Visible = true;
                    objStickyItem.Text = strStickyMRUList[i];
                    mnuFileMRUSeparator.Visible = true;
                }
                else
                {
                    objStickyItem.Visible = false;
                }
                if (i < strMRUList.Count)
                {
                    objItem.Visible = true;
                    if (i == 9)
                        objItem.Text = "1&0 " + strMRUList[i];
                    else
                        objItem.Text = "&" + (i + 1).ToString() + " " + strMRUList[i];
                    mnuFileMRUSeparator.Visible = true;
                }
                else
                {
                    objItem.Visible = false;
                }
            }
        }

        private void objCareer_DiceRollerOpened(Object sender)
        {
            MessageBox.Show("This feature is currently disabled. Please open a ticket if this makes the world burn, otherwise it will get re-enabled when somebody gets around to it");
            //TODO: IMPLEMENT THIS SHIT
        }

        private void objCareer_DiceRollerOpenedInt(Character objCharacter, int intDice)
        {
            if (GlobalOptions.SingleDiceRoller)
            {
                if (_frmRoller == null)
                {
                    _frmRoller = new frmDiceRoller(this, objCharacter.Qualities, intDice);
                    _frmRoller.Show();
                }
                else
                {
                    _frmRoller.Dice = intDice;
                    _frmRoller.Qualities = objCharacter.Qualities;
                    _frmRoller.Focus();
                }
            }
            else
            {
                frmDiceRoller frmRoller = new frmDiceRoller(this, objCharacter.Qualities, intDice);
                frmRoller.Show();
            }
        }

        private void mnuClearUnpinnedItems_Click(object sender, EventArgs e)
        {
            foreach (string strFile in GlobalOptions.ReadMRUList())
            {
                GlobalOptions.RemoveFromMRUList(strFile);
            }
        }

        private void mnuRestart_Click(object sender, EventArgs e)
        {
            Utils.RestartApplication();
        }
        #endregion

        #region Application Properties
        /// <summary>
        /// The frmOmae window being used by the application.
        /// </summary>
        public frmOmae OmaeWindow
        {
            get
            {
                return _frmOmae;
            }
            set
            {
                _frmOmae = value;
            }
        }

        /// <summary>
        /// The frmDiceRoller window being used by the application.
        /// </summary>
        public frmDiceRoller RollerWindow
        {
            get
            {
                return _frmRoller;
            }
            set
            {
                _frmRoller = value;
            }
        }

        public List<Character> OpenCharacters
        {
            get { return _lstCharacters; }
            set { _lstCharacters = value; }
        }
        #endregion

        private void frmMain_Closing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.WindowState = WindowState;
            if (WindowState == FormWindowState.Normal)
            {
                Properties.Settings.Default.Location = Location;
                Properties.Settings.Default.Size = Size;
            }
            else
            {
                Properties.Settings.Default.Location = RestoreBounds.Location;
                Properties.Settings.Default.Size = RestoreBounds.Size;
            }

            Properties.Settings.Default.Save();
        }
    }
}
