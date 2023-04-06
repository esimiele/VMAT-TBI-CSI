using System;
using System.Windows.Forms;

namespace VMATAutoPlanMT.Prompts
{
    public partial class selectOption : Form
    {
        public bool isVMATTBI = false;
        public bool isVMATCSI = false;
        public bool launchOptimization = false;
        public selectOption(bool showOpt = false)
        {
            InitializeComponent();
            if (showOpt) launchOptBtn.Visible = true;
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

        private void launchOptBtn_Click(object sender, EventArgs e)
        {
            launchOptimization = true;
            this.Close();
        }
    }
}
