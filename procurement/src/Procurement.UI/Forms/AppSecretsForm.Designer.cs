namespace Procurement.UI.Forms
{
    partial class AppSecretsForm
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
            this.split_main = new System.Windows.Forms.SplitContainer();
            this.lbl_instructions = new System.Windows.Forms.Label();
            this.pnl_secrets = new System.Windows.Forms.Panel();
            this.bt_save = new System.Windows.Forms.Button();
            this.bt_close = new System.Windows.Forms.Button();
            this.gb_access = new Procurement.UI.Support.ClassicStyle.ClassicGroupBox();
            this.dgv_access = new System.Windows.Forms.DataGridView();
            this.lbl_copyFrom = new System.Windows.Forms.Label();
            this.cb_copyFrom = new System.Windows.Forms.ComboBox();
            this.lbl_copyTo = new System.Windows.Forms.Label();
            this.txt_copyTo = new System.Windows.Forms.TextBox();
            this.bt_copyAccess = new System.Windows.Forms.Button();
            this.bt_saveAccess = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.split_main)).BeginInit();
            this.split_main.Panel1.SuspendLayout();
            this.split_main.Panel2.SuspendLayout();
            this.split_main.SuspendLayout();
            this.gb_access.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_access)).BeginInit();
            this.SuspendLayout();
            //
            // split_main
            //
            this.split_main.Dock = System.Windows.Forms.DockStyle.Fill;
            this.split_main.Location = new System.Drawing.Point(0, 0);
            this.split_main.Name = "split_main";
            //
            // split_main.Panel1
            //
            this.split_main.Panel1.Controls.Add(this.lbl_instructions);
            this.split_main.Panel1.Controls.Add(this.pnl_secrets);
            this.split_main.Panel1.Controls.Add(this.bt_save);
            this.split_main.Panel1.Controls.Add(this.bt_close);
            //
            // split_main.Panel2
            //
            this.split_main.Panel2.Controls.Add(this.gb_access);
            this.split_main.Size = new System.Drawing.Size(1080, 560);
            this.split_main.SplitterDistance = 590;
            this.split_main.TabIndex = 0;
            //
            // lbl_instructions
            //
            this.lbl_instructions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lbl_instructions.Location = new System.Drawing.Point(12, 9);
            this.lbl_instructions.Name = "lbl_instructions";
            this.lbl_instructions.Size = new System.Drawing.Size(566, 40);
            this.lbl_instructions.TabIndex = 0;
            this.lbl_instructions.Text = "Leave a field empty to keep the current value unchanged. Filled-in values are e" +
    "ncrypted and saved (SharedSecrets database, shared with UniPro), and will never be shown on this screen again.";
            //
            // pnl_secrets
            //
            this.pnl_secrets.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnl_secrets.AutoScroll = true;
            this.pnl_secrets.BackColor = System.Drawing.Color.White;
            this.pnl_secrets.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnl_secrets.Location = new System.Drawing.Point(12, 52);
            this.pnl_secrets.Name = "pnl_secrets";
            this.pnl_secrets.Size = new System.Drawing.Size(566, 458);
            this.pnl_secrets.TabIndex = 1;
            //
            // bt_save
            //
            this.bt_save.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bt_save.Location = new System.Drawing.Point(392, 521);
            this.bt_save.Name = "bt_save";
            this.bt_save.Size = new System.Drawing.Size(90, 27);
            this.bt_save.TabIndex = 2;
            this.bt_save.Text = "Save";
            this.bt_save.UseVisualStyleBackColor = true;
            this.bt_save.Click += new System.EventHandler(this.bt_save_Click);
            //
            // bt_close
            //
            this.bt_close.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bt_close.Location = new System.Drawing.Point(488, 521);
            this.bt_close.Name = "bt_close";
            this.bt_close.Size = new System.Drawing.Size(90, 27);
            this.bt_close.TabIndex = 3;
            this.bt_close.Text = "Close";
            this.bt_close.UseVisualStyleBackColor = true;
            this.bt_close.Click += new System.EventHandler(this.bt_close_Click);
            //
            // gb_access
            //
            this.gb_access.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gb_access.Controls.Add(this.dgv_access);
            this.gb_access.Controls.Add(this.lbl_copyFrom);
            this.gb_access.Controls.Add(this.cb_copyFrom);
            this.gb_access.Controls.Add(this.lbl_copyTo);
            this.gb_access.Controls.Add(this.txt_copyTo);
            this.gb_access.Controls.Add(this.bt_copyAccess);
            this.gb_access.Controls.Add(this.bt_saveAccess);
            this.gb_access.Location = new System.Drawing.Point(6, 9);
            this.gb_access.Name = "gb_access";
            this.gb_access.Size = new System.Drawing.Size(468, 539);
            this.gb_access.TabIndex = 0;
            this.gb_access.TabStop = false;
            this.gb_access.Text = "Secret Access per user - who may use which setting";
            //
            // dgv_access
            //
            this.dgv_access.AllowUserToResizeRows = false;
            this.dgv_access.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_access.AutoGenerateColumns = false;
            this.dgv_access.BackgroundColor = System.Drawing.Color.White;
            this.dgv_access.Location = new System.Drawing.Point(10, 22);
            this.dgv_access.Name = "dgv_access";
            this.dgv_access.RowHeadersWidth = 20;
            this.dgv_access.Size = new System.Drawing.Size(448, 438);
            this.dgv_access.TabIndex = 0;
            //
            // lbl_copyFrom
            //
            this.lbl_copyFrom.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lbl_copyFrom.AutoSize = true;
            this.lbl_copyFrom.Location = new System.Drawing.Point(10, 475);
            this.lbl_copyFrom.Name = "lbl_copyFrom";
            this.lbl_copyFrom.Size = new System.Drawing.Size(33, 13);
            this.lbl_copyFrom.TabIndex = 1;
            this.lbl_copyFrom.Text = "From:";
            //
            // cb_copyFrom
            //
            this.cb_copyFrom.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cb_copyFrom.Location = new System.Drawing.Point(48, 471);
            this.cb_copyFrom.Name = "cb_copyFrom";
            this.cb_copyFrom.Size = new System.Drawing.Size(130, 21);
            this.cb_copyFrom.TabIndex = 2;
            //
            // lbl_copyTo
            //
            this.lbl_copyTo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lbl_copyTo.AutoSize = true;
            this.lbl_copyTo.Location = new System.Drawing.Point(186, 475);
            this.lbl_copyTo.Name = "lbl_copyTo";
            this.lbl_copyTo.Size = new System.Drawing.Size(23, 13);
            this.lbl_copyTo.TabIndex = 3;
            this.lbl_copyTo.Text = "To:";
            //
            // txt_copyTo
            //
            this.txt_copyTo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.txt_copyTo.Location = new System.Drawing.Point(214, 471);
            this.txt_copyTo.Name = "txt_copyTo";
            this.txt_copyTo.Size = new System.Drawing.Size(130, 20);
            this.txt_copyTo.TabIndex = 4;
            //
            // bt_copyAccess
            //
            this.bt_copyAccess.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bt_copyAccess.Location = new System.Drawing.Point(368, 468);
            this.bt_copyAccess.Name = "bt_copyAccess";
            this.bt_copyAccess.Size = new System.Drawing.Size(90, 27);
            this.bt_copyAccess.TabIndex = 5;
            this.bt_copyAccess.Text = "Copy";
            this.bt_copyAccess.UseVisualStyleBackColor = true;
            this.bt_copyAccess.Click += new System.EventHandler(this.bt_copyAccess_Click);
            //
            // bt_saveAccess
            //
            this.bt_saveAccess.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bt_saveAccess.Location = new System.Drawing.Point(368, 503);
            this.bt_saveAccess.Name = "bt_saveAccess";
            this.bt_saveAccess.Size = new System.Drawing.Size(90, 27);
            this.bt_saveAccess.TabIndex = 6;
            this.bt_saveAccess.Text = "Save rights";
            this.bt_saveAccess.UseVisualStyleBackColor = true;
            this.bt_saveAccess.Click += new System.EventHandler(this.bt_saveAccess_Click);
            //
            // AppSecretsForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1080, 560);
            this.Controls.Add(this.split_main);
            this.MinimumSize = new System.Drawing.Size(820, 420);
            this.Name = "AppSecretsForm";
            this.Text = "App Secrets";
            this.split_main.Panel1.ResumeLayout(false);
            this.split_main.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.split_main)).EndInit();
            this.split_main.ResumeLayout(false);
            this.gb_access.ResumeLayout(false);
            this.gb_access.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_access)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer split_main;
        private System.Windows.Forms.Label lbl_instructions;
        private System.Windows.Forms.Panel pnl_secrets;
        private System.Windows.Forms.Button bt_save;
        private System.Windows.Forms.Button bt_close;
        private Procurement.UI.Support.ClassicStyle.ClassicGroupBox gb_access;
        private System.Windows.Forms.DataGridView dgv_access;
        private System.Windows.Forms.Label lbl_copyFrom;
        private System.Windows.Forms.ComboBox cb_copyFrom;
        private System.Windows.Forms.Label lbl_copyTo;
        private System.Windows.Forms.TextBox txt_copyTo;
        private System.Windows.Forms.Button bt_copyAccess;
        private System.Windows.Forms.Button bt_saveAccess;
    }
}
