namespace Procurement.UI.Forms
{
    partial class LoginForm
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
            this.lbl_user = new System.Windows.Forms.Label();
            this.lbl_userName = new System.Windows.Forms.Label();
            this.lbl_company = new System.Windows.Forms.Label();
            this.cmbAdministrations = new System.Windows.Forms.ComboBox();
            this.cb_testMode = new System.Windows.Forms.CheckBox();
            this.bt_LogIn = new System.Windows.Forms.Button();
            this.lbl_status = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lbl_user
            // 
            this.lbl_user.AutoSize = true;
            this.lbl_user.Location = new System.Drawing.Point(12, 12);
            this.lbl_user.Name = "lbl_user";
            this.lbl_user.Size = new System.Drawing.Size(29, 13);
            this.lbl_user.TabIndex = 4;
            this.lbl_user.Text = "User";
            // 
            // lbl_userName
            // 
            this.lbl_userName.Location = new System.Drawing.Point(69, 12);
            this.lbl_userName.Name = "lbl_userName";
            this.lbl_userName.Size = new System.Drawing.Size(182, 13);
            this.lbl_userName.TabIndex = 5;
            this.lbl_userName.Text = "(unknown)";
            // 
            // lbl_company
            // 
            this.lbl_company.AutoSize = true;
            this.lbl_company.Location = new System.Drawing.Point(12, 64);
            this.lbl_company.Name = "lbl_company";
            this.lbl_company.Size = new System.Drawing.Size(51, 13);
            this.lbl_company.TabIndex = 2;
            this.lbl_company.Text = "Company";
            // 
            // cmbAdministrations
            // 
            this.cmbAdministrations.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbAdministrations.FormattingEnabled = true;
            this.cmbAdministrations.Location = new System.Drawing.Point(69, 61);
            this.cmbAdministrations.Name = "cmbAdministrations";
            this.cmbAdministrations.Size = new System.Drawing.Size(143, 21);
            this.cmbAdministrations.TabIndex = 0;
            // 
            // cb_testMode
            // 
            this.cb_testMode.AutoSize = true;
            this.cb_testMode.Location = new System.Drawing.Point(69, 88);
            this.cb_testMode.Name = "cb_testMode";
            this.cb_testMode.Size = new System.Drawing.Size(76, 17);
            this.cb_testMode.TabIndex = 3;
            this.cb_testMode.Text = "Test Mode";
            this.cb_testMode.UseVisualStyleBackColor = true;
            // 
            // bt_LogIn
            // 
            this.bt_LogIn.Location = new System.Drawing.Point(94, 113);
            this.bt_LogIn.Name = "bt_LogIn";
            this.bt_LogIn.Size = new System.Drawing.Size(90, 27);
            this.bt_LogIn.TabIndex = 1;
            this.bt_LogIn.Text = "Log In";
            this.bt_LogIn.UseVisualStyleBackColor = true;
            this.bt_LogIn.Click += new System.EventHandler(this.bt_LogIn_Click);
            // 
            // lbl_status
            // 
            this.lbl_status.ForeColor = System.Drawing.Color.DarkRed;
            this.lbl_status.Location = new System.Drawing.Point(12, 148);
            this.lbl_status.Name = "lbl_status";
            this.lbl_status.Size = new System.Drawing.Size(239, 32);
            this.lbl_status.TabIndex = 6;
            // 
            // LoginForm
            // 
            this.AcceptButton = this.bt_LogIn;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(263, 185);
            this.Controls.Add(this.lbl_status);
            this.Controls.Add(this.bt_LogIn);
            this.Controls.Add(this.cb_testMode);
            this.Controls.Add(this.cmbAdministrations);
            this.Controls.Add(this.lbl_company);
            this.Controls.Add(this.lbl_userName);
            this.Controls.Add(this.lbl_user);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Login";
            this.Load += new System.EventHandler(this.LoginForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lbl_user;
        private System.Windows.Forms.Label lbl_userName;
        private System.Windows.Forms.Label lbl_company;
        private System.Windows.Forms.ComboBox cmbAdministrations;
        private System.Windows.Forms.CheckBox cb_testMode;
        private System.Windows.Forms.Button bt_LogIn;
        private System.Windows.Forms.Label lbl_status;
    }
}
