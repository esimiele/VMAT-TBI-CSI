using System;
using System.Windows.Forms;

namespace VMATAutoPlanMT.Prompts
{
    public partial class enterMissingInfo : Form
    {
        public bool confirm = false;
        public enterMissingInfo(string titleString, string infoString)
        {
            InitializeComponent();
            title.Text = titleString;
            info.Text = infoString;
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
