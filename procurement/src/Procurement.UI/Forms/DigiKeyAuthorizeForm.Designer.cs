namespace Procurement.UI.Forms
{
    partial class DigiKeyAuthorizeForm
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
            this.lbl_instructions = new System.Windows.Forms.Label();
            this.txt_redirectUrl = new System.Windows.Forms.TextBox();
            this.bt_ok = new System.Windows.Forms.Button();
            this.bt_cancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lbl_instructions
            // 
            this.lbl_instructions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lbl_instructions.Location = new System.Drawing.Point(12, 12);
            this.lbl_instructions.Name = "lbl_instructions";
            this.lbl_instructions.Size = new System.Drawing.Size(480, 70);
            this.lbl_instructions.TabIndex = 0;
            this.lbl_instructions.Text = "A browser window has just opened to log in to DigiKey and give consent. After approving, DigiKey redirects the browser to \"https://localhost/...\" — that page won't load (expected, nothing runs there), but the address bar will contain the full URL. Copy that complete URL from the address bar and paste it below.";
            // 
            // txt_redirectUrl
            // 
            this.txt_redirectUrl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txt_redirectUrl.Location = new System.Drawing.Point(12, 88);
            this.txt_redirectUrl.Multiline = true;
            this.txt_redirectUrl.Name = "txt_redirectUrl";
            this.txt_redirectUrl.Size = new System.Drawing.Size(480, 60);
            this.txt_redirectUrl.TabIndex = 1;
            // 
            // bt_ok
            // 
            this.bt_ok.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bt_ok.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.bt_ok.Location = new System.Drawing.Point(306, 160);
            this.bt_ok.Name = "bt_ok";
            this.bt_ok.Size = new System.Drawing.Size(90, 27);
            this.bt_ok.TabIndex = 2;
            this.bt_ok.Text = "OK";
            this.bt_ok.UseVisualStyleBackColor = true;
            // 
            // bt_cancel
            // 
            this.bt_cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bt_cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.bt_cancel.Location = new System.Drawing.Point(402, 160);
            this.bt_cancel.Name = "bt_cancel";
            this.bt_cancel.Size = new System.Drawing.Size(90, 27);
            this.bt_cancel.TabIndex = 3;
            this.bt_cancel.Text = "Cancel";
            this.bt_cancel.UseVisualStyleBackColor = true;
            // 
            // DigiKeyAuthorizeForm
            // 
            this.AcceptButton = this.bt_ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.bt_cancel;
            this.ClientSize = new System.Drawing.Size(504, 199);
            this.Controls.Add(this.bt_cancel);
            this.Controls.Add(this.bt_ok);
            this.Controls.Add(this.txt_redirectUrl);
            this.Controls.Add(this.lbl_instructions);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DigiKeyAuthorizeForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Authorize DigiKey Ordering";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lbl_instructions;
        private System.Windows.Forms.TextBox txt_redirectUrl;
        private System.Windows.Forms.Button bt_ok;
        private System.Windows.Forms.Button bt_cancel;
    }
}
