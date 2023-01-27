using System;
using System.Windows.Forms;

namespace VMATAutoPlanMT.Prompts
{
    public partial class selectItem : Form
    {
        public bool confirm = false;
        public selectItem()
        {
            InitializeComponent();
        }

        private void confirm_Click(object sender, EventArgs e)
        {
            confirm = true;
            this.Close();
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
