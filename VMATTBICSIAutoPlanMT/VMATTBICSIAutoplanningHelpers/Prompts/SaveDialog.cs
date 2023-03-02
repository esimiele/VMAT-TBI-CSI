using System;
using System.Windows.Forms;

namespace VMATTBICSIAutoplanningHelpers.Prompts
{
    public partial class SaveDialog : Form
    {
        public bool save = false;
        public SaveDialog()
        {
            InitializeComponent();
        }

        private void Confirm_Click(object sender, EventArgs e)
        {
            save = true;
            this.Close();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
