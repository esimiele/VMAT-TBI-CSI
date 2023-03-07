using System;
using System.Windows.Forms;

namespace VMATTBICSIAutoplanningHelpers.Prompts
{
    public partial class confirmUI : Form
    {
        public bool confirm = false;
        public confirmUI()
        {
            InitializeComponent();
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void confirm_Click(object sender, EventArgs e)
        {
            confirm = true;
            this.Close();
        }
    }
}
