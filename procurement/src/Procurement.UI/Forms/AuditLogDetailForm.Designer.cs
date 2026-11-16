namespace Procurement.UI.Forms
{
    partial class AuditLogDetailForm
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
            this.txt_detail = new System.Windows.Forms.TextBox();
            this.bt_copy = new System.Windows.Forms.Button();
            this.bt_close = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // txt_detail
            // 
            this.txt_detail.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txt_detail.BackColor = System.Drawing.SystemColors.Window;
            this.txt_detail.Font = new System.Drawing.Font("Consolas", 9F);
            this.txt_detail.Location = new System.Drawing.Point(12, 12);
            this.txt_detail.Multiline = true;
            this.txt_detail.Name = "txt_detail";
            this.txt_detail.ReadOnly = true;
            this.txt_detail.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txt_detail.Size = new System.Drawing.Size(660, 454);
            this.txt_detail.TabIndex = 0;
            // 
            // bt_copy
            // 
            this.bt_copy.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bt_copy.Location = new System.Drawing.Point(456, 474);
            this.bt_copy.Name = "bt_copy";
            this.bt_copy.Size = new System.Drawing.Size(120, 27);
            this.bt_copy.TabIndex = 1;
            this.bt_copy.Text = "Copy to Clipboard";
            this.bt_copy.UseVisualStyleBackColor = true;
            this.bt_copy.Click += new System.EventHandler(this.bt_copy_Click);
            // 
            // bt_close
            // 
            this.bt_close.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bt_close.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.bt_close.Location = new System.Drawing.Point(582, 474);
            this.bt_close.Name = "bt_close";
            this.bt_close.Size = new System.Drawing.Size(90, 27);
            this.bt_close.TabIndex = 2;
            this.bt_close.Text = "Close";
            this.bt_close.UseVisualStyleBackColor = true;
            // 
            // AuditLogDetailForm
            // 
            this.AcceptButton = this.bt_close;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.bt_close;
            this.ClientSize = new System.Drawing.Size(684, 511);
            this.Controls.Add(this.bt_close);
            this.Controls.Add(this.bt_copy);
            this.Controls.Add(this.txt_detail);
            this.MinimumSize = new System.Drawing.Size(400, 250);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AuditLogDetailForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Audit Log Detail";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txt_detail;
        private System.Windows.Forms.Button bt_copy;
        private System.Windows.Forms.Button bt_close;
    }
}
