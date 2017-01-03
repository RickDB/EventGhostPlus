using System;
using System.Windows.Forms;
using MediaPortal.Configuration;
using MediaPortal.InputDevices;

namespace EventGhostPlus
{
    public partial class FormSettings : Form
    {
        //public const string ENTRY_NAME_NUMBER_OF_DEVICES = "numberOfDevices";
        private bool _pwdEncrypted;

        public FormSettings()
        {
            InitializeComponent();
        }
        
        
        private void DisplayDeviceSettings()
        {
            using (var xmlReader = new MediaPortal.Profile.Settings(Config.GetFile(Config.Dir.Config, "MediaPortal.xml")))
            {
                egPath_textBox.Text = xmlReader.GetValueAsString(EventGhostPlus.PLUGIN_NAME, "egPath", "");
                egPart2_comboBox.Text = xmlReader.GetValueAsString(EventGhostPlus.PLUGIN_NAME, "egPart2", "Type");
                egPart3_comboBox.Text = xmlReader.GetValueAsString(EventGhostPlus.PLUGIN_NAME, "egPart3", "Status");
                egPayload_checkBox.Checked = xmlReader.GetValueAsBool(EventGhostPlus.PLUGIN_NAME, "egPayload", false);
                WindowChange_checkBox.Checked = xmlReader.GetValueAsBool(EventGhostPlus.PLUGIN_NAME, "WindowChange", true);

                mediaDuration_textBox.Text = (xmlReader.GetValueAsInt(EventGhostPlus.PLUGIN_NAME, "setLevelForMediaDuration", 10)).ToString();

                tcpip_radioButton.Checked = xmlReader.GetValueAsBool(EventGhostPlus.PLUGIN_NAME, "tcpipIsEnabled", false);
                host_textBox.Text = xmlReader.GetValueAsString(EventGhostPlus.PLUGIN_NAME, "tcpipHost", "");
                port_textBox.Text = xmlReader.GetValueAsString(EventGhostPlus.PLUGIN_NAME, "tcpipPort", "1024");
                _pwdEncrypted = xmlReader.GetValueAsBool(EventGhostPlus.PLUGIN_NAME, "PWDEncrypted", false);
                if (_pwdEncrypted)
                    password_textBox.Text = Dpapi.DecryptString(xmlReader.GetValueAsString(EventGhostPlus.PLUGIN_NAME, "tcpipPassword", ""));
                else
                    password_textBox.Text = xmlReader.GetValueAsString(EventGhostPlus.PLUGIN_NAME, "tcpipPassword", "");

                rcvport_textBox.Text = xmlReader.GetValueAsString(EventGhostPlus.PLUGIN_NAME, "ReceivePort", "1023");
                rcvpassword_textBox.Text = Dpapi.DecryptString(xmlReader.GetValueAsString(EventGhostPlus.PLUGIN_NAME, "ReceivePassword", ""));

                debug_checkBox.Checked = xmlReader.GetValueAsBool(EventGhostPlus.PLUGIN_NAME, "DebugMode", false);
                
            }
            TcpipStatusEnabler();
            
        }
        private void FormSettings_Load(object sender, EventArgs e)
        {

            DisplayDeviceSettings();
        }

        public static void Main()
        {
            FormSettings frm = new FormSettings();
            if (frm.ShowDialog() == DialogResult.OK)
            {
            }
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            using (MediaPortal.Profile.Settings xmlWriter = new MediaPortal.Profile.Settings(Config.GetFile(Config.Dir.Config, "MediaPortal.xml")))
            {
                xmlWriter.SetValue(EventGhostPlus.PLUGIN_NAME, "egPath", egPath_textBox.Text);
                xmlWriter.SetValue(EventGhostPlus.PLUGIN_NAME, "egPart2", egPart2_comboBox.Text);
                xmlWriter.SetValue(EventGhostPlus.PLUGIN_NAME, "egPart3", egPart3_comboBox.Text);
                xmlWriter.SetValueAsBool(EventGhostPlus.PLUGIN_NAME, "egPayload", egPayload_checkBox.Checked);
                xmlWriter.SetValueAsBool(EventGhostPlus.PLUGIN_NAME, "WindowChange", WindowChange_checkBox.Checked);

                xmlWriter.SetValue(EventGhostPlus.PLUGIN_NAME, "setLevelForMediaDuration", Convert.ToInt32(mediaDuration_textBox.Text));

                xmlWriter.SetValueAsBool(EventGhostPlus.PLUGIN_NAME, "tcpipIsEnabled", tcpip_radioButton.Checked);
                xmlWriter.SetValue(EventGhostPlus.PLUGIN_NAME, "tcpipHost", host_textBox.Text);
                xmlWriter.SetValue(EventGhostPlus.PLUGIN_NAME, "tcpipPort", port_textBox.Text);
                xmlWriter.SetValue(EventGhostPlus.PLUGIN_NAME, "tcpipPassword", Dpapi.EncryptString(password_textBox.Text));
                xmlWriter.SetValueAsBool(EventGhostPlus.PLUGIN_NAME, "PWDEncrypted", true);

                xmlWriter.SetValue(EventGhostPlus.PLUGIN_NAME, "ReceivePort", rcvport_textBox.Text);
                xmlWriter.SetValue(EventGhostPlus.PLUGIN_NAME, "ReceivePassword", Dpapi.EncryptString(rcvpassword_textBox.Text));

                xmlWriter.SetValueAsBool(EventGhostPlus.PLUGIN_NAME, "DebugMode", debug_checkBox.Checked);
            }
        }

        private void imageFileName_Button_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                egPath_textBox.Text = dialog.SelectedPath;
            }
        }

        private void imageFileName_Editbox_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        private void part2_comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            egPart3_comboBox.SelectedIndex = 1 - egPart2_comboBox.SelectedIndex;
        }

        private void part3_comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            
            egPart2_comboBox.SelectedIndex = 1 - egPart3_comboBox.SelectedIndex;
        }

        private void tcpip_radioButton_CheckedChanged(object sender, EventArgs e)
        {
            TcpipStatusEnabler();
        }
        private void TcpipStatusEnabler()
        {
            host_textBox.Enabled = tcpip_radioButton.Checked;
            port_textBox.Enabled = tcpip_radioButton.Checked;
            password_textBox.Enabled = tcpip_radioButton.Checked;
            egPath_textBox.Enabled = local_radioButton.Checked;
            egPath_Button.Enabled = local_radioButton.Checked;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            InputMappingForm inputMappingForm = new InputMappingForm("EventGhostPlus");
            inputMappingForm.ShowDialog();
        }
    }
}