
namespace VMATAutoPlanMT
{
    partial class selectOption
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.VMATTBI_btn = new System.Windows.Forms.Button();
            this.VMATCSI_btn = new System.Windows.Forms.Button();
            this.launchOptBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(56, 20);
            this.label1.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(491, 51);
            this.label1.TabIndex = 0;
            this.label1.Text = "Please select an option";
            // 
            // VMATTBI_btn
            // 
            this.VMATTBI_btn.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.VMATTBI_btn.Location = new System.Drawing.Point(225, 132);
            this.VMATTBI_btn.Margin = new System.Windows.Forms.Padding(7, 7, 7, 7);
            this.VMATTBI_btn.Name = "VMATTBI_btn";
            this.VMATTBI_btn.Size = new System.Drawing.Size(175, 51);
            this.VMATTBI_btn.TabIndex = 1;
            this.VMATTBI_btn.Text = "VMAT TBI";
            this.VMATTBI_btn.UseVisualStyleBackColor = true;
            this.VMATTBI_btn.Click += new System.EventHandler(this.VMATTBI_btn_Click);
            // 
            // VMATCSI_btn
            // 
            this.VMATCSI_btn.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.VMATCSI_btn.Location = new System.Drawing.Point(225, 241);
            this.VMATCSI_btn.Margin = new System.Windows.Forms.Padding(7, 7, 7, 7);
            this.VMATCSI_btn.Name = "VMATCSI_btn";
            this.VMATCSI_btn.Size = new System.Drawing.Size(175, 51);
            this.VMATCSI_btn.TabIndex = 2;
            this.VMATCSI_btn.Text = "VMAT CSI";
            this.VMATCSI_btn.UseVisualStyleBackColor = true;
            this.VMATCSI_btn.Click += new System.EventHandler(this.VMATCSI_btn_Click);
            // 
            // launchOptBtn
            // 
            this.launchOptBtn.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.launchOptBtn.Location = new System.Drawing.Point(125, 340);
            this.launchOptBtn.Margin = new System.Windows.Forms.Padding(7);
            this.launchOptBtn.Name = "launchOptBtn";
            this.launchOptBtn.Size = new System.Drawing.Size(375, 51);
            this.launchOptBtn.TabIndex = 3;
            this.launchOptBtn.Text = "Launch Optimization Loop";
            this.launchOptBtn.UseVisualStyleBackColor = true;
            this.launchOptBtn.Visible = false;
            // 
            // selectOption
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(14F, 29F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(674, 440);
            this.Controls.Add(this.launchOptBtn);
            this.Controls.Add(this.VMATCSI_btn);
            this.Controls.Add(this.VMATTBI_btn);
            this.Controls.Add(this.label1);
            this.Margin = new System.Windows.Forms.Padding(7, 7, 7, 7);
            this.Name = "selectOption";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "selectOption";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button VMATTBI_btn;
        private System.Windows.Forms.Button VMATCSI_btn;
        private System.Windows.Forms.Button launchOptBtn;
    }
}