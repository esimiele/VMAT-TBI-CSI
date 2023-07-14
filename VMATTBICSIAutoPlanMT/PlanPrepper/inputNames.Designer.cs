namespace planPrepper
{
    partial class inputNames
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
            this.title = new System.Windows.Forms.Label();
            this.iso1 = new System.Windows.Forms.Label();
            this.iso2 = new System.Windows.Forms.Label();
            this.iso3 = new System.Windows.Forms.Label();
            this.iso4 = new System.Windows.Forms.Label();
            this.isoName1 = new System.Windows.Forms.TextBox();
            this.isoName2 = new System.Windows.Forms.TextBox();
            this.isoName3 = new System.Windows.Forms.TextBox();
            this.isoName4 = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // title
            // 
            this.title.AutoSize = true;
            this.title.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.title.Location = new System.Drawing.Point(41, 9);
            this.title.Name = "title";
            this.title.Size = new System.Drawing.Size(278, 20);
            this.title.TabIndex = 0;
            this.title.Text = "Please enter the isocenter names";
            this.title.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // iso1
            // 
            this.iso1.AutoSize = true;
            this.iso1.Location = new System.Drawing.Point(87, 50);
            this.iso1.Name = "iso1";
            this.iso1.Size = new System.Drawing.Size(63, 13);
            this.iso1.TabIndex = 1;
            this.iso1.Text = "Isocenter 1:";
            // 
            // iso2
            // 
            this.iso2.AutoSize = true;
            this.iso2.Location = new System.Drawing.Point(87, 83);
            this.iso2.Name = "iso2";
            this.iso2.Size = new System.Drawing.Size(63, 13);
            this.iso2.TabIndex = 2;
            this.iso2.Text = "Isocenter 2:";
            // 
            // iso3
            // 
            this.iso3.AutoSize = true;
            this.iso3.Location = new System.Drawing.Point(87, 118);
            this.iso3.Name = "iso3";
            this.iso3.Size = new System.Drawing.Size(63, 13);
            this.iso3.TabIndex = 3;
            this.iso3.Text = "Isocenter 3:";
            this.iso3.Visible = false;
            // 
            // iso4
            // 
            this.iso4.AutoSize = true;
            this.iso4.Location = new System.Drawing.Point(87, 153);
            this.iso4.Name = "iso4";
            this.iso4.Size = new System.Drawing.Size(63, 13);
            this.iso4.TabIndex = 4;
            this.iso4.Text = "Isocenter 4:";
            this.iso4.Visible = false;
            // 
            // isoName1
            // 
            this.isoName1.Location = new System.Drawing.Point(159, 47);
            this.isoName1.Name = "isoName1";
            this.isoName1.Size = new System.Drawing.Size(100, 20);
            this.isoName1.TabIndex = 5;
            // 
            // isoName2
            // 
            this.isoName2.Location = new System.Drawing.Point(159, 80);
            this.isoName2.Name = "isoName2";
            this.isoName2.Size = new System.Drawing.Size(100, 20);
            this.isoName2.TabIndex = 6;
            // 
            // isoName3
            // 
            this.isoName3.Location = new System.Drawing.Point(159, 115);
            this.isoName3.Name = "isoName3";
            this.isoName3.Size = new System.Drawing.Size(100, 20);
            this.isoName3.TabIndex = 7;
            this.isoName3.Visible = false;
            // 
            // isoName4
            // 
            this.isoName4.Location = new System.Drawing.Point(159, 150);
            this.isoName4.Name = "isoName4";
            this.isoName4.Size = new System.Drawing.Size(100, 20);
            this.isoName4.TabIndex = 8;
            this.isoName4.Visible = false;
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button1.Location = new System.Drawing.Point(51, 197);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 9;
            this.button1.Text = "Confirm";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.confirm_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(244, 197);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 10;
            this.button2.Text = "Cancel";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.cancel_Click);
            // 
            // inputNames
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(364, 232);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.isoName4);
            this.Controls.Add(this.isoName3);
            this.Controls.Add(this.isoName2);
            this.Controls.Add(this.isoName1);
            this.Controls.Add(this.iso4);
            this.Controls.Add(this.iso3);
            this.Controls.Add(this.iso2);
            this.Controls.Add(this.iso1);
            this.Controls.Add(this.title);
            this.Name = "inputNames";
            this.Text = "inputNames";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public System.Windows.Forms.Label title;
        public System.Windows.Forms.Label iso1;
        public System.Windows.Forms.Label iso2;
        public System.Windows.Forms.Label iso3;
        public System.Windows.Forms.Label iso4;
        public System.Windows.Forms.TextBox isoName1;
        public System.Windows.Forms.TextBox isoName2;
        public System.Windows.Forms.TextBox isoName3;
        public System.Windows.Forms.TextBox isoName4;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
    }
}