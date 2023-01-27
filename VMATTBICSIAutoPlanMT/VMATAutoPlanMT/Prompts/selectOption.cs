using System;
using System.Windows.Forms;

namespace VMATAutoPlanMT.Prompts
{
    public partial class selectOption : Form
    {
        public bool isVMATTBI = false;
        public bool isVMATCSI = false;
        public selectOption(bool showOpt = false)
        {
            if (showOpt) launchOptBtn.Visible = true;
            InitializeComponent();
        }

        private void VMATTBI_btn_Click(object sender, EventArgs e)
        {
            isVMATTBI = true;
            this.Close();
        }

        private void VMATCSI_btn_Click(object sender, EventArgs e)
        {
            isVMATCSI = true;
            this.Close();
        }
    }
}
