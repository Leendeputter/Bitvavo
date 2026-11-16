namespace Procurement.UI.Forms
{
    partial class FarnellShipToAddressPickerForm
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
            this.lbl_hint = new System.Windows.Forms.Label();
            this.lst_addresses = new System.Windows.Forms.ListBox();
            this.bt_ok = new System.Windows.Forms.Button();
            this.bt_cancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lbl_hint
            // 
            this.lbl_hint.AutoSize = true;
            this.lbl_hint.Location = new System.Drawing.Point(12, 12);
            this.lbl_hint.Name = "lbl_hint";
            this.lbl_hint.Size = new System.Drawing.Size(313, 13);
            this.lbl_hint.TabIndex = 0;
            this.lbl_hint.Text = "This Farnell order needs a shipping address — pick one below:";
            // 
            // lst_addresses
            // 
            this.lst_addresses.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lst_addresses.IntegralHeight = false;
            this.lst_addresses.Location = new System.Drawing.Point(12, 34);
            this.lst_addresses.Name = "lst_addresses";
            this.lst_addresses.Size = new System.Drawing.Size(440, 262);
            this.lst_addresses.TabIndex = 1;
            this.lst_addresses.DoubleClick += new System.EventHandler(this.lst_addresses_DoubleClick);
            // 
            // bt_ok
            // 
            this.bt_ok.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bt_ok.Location = new System.Drawing.Point(266, 304);
            this.bt_ok.Name = "bt_ok";
            this.bt_ok.Size = new System.Drawing.Size(90, 27);
            this.bt_ok.TabIndex = 2;
            this.bt_ok.Text = "OK";
            this.bt_ok.UseVisualStyleBackColor = true;
            this.bt_ok.Click += new System.EventHandler(this.bt_ok_Click);
            // 
            // bt_cancel
            // 
            this.bt_cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bt_cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.bt_cancel.Location = new System.Drawing.Point(362, 304);
            this.bt_cancel.Name = "bt_cancel";
            this.bt_cancel.Size = new System.Drawing.Size(90, 27);
            this.bt_cancel.TabIndex = 3;
            this.bt_cancel.Text = "Cancel";
            this.bt_cancel.UseVisualStyleBackColor = true;
            // 
            // FarnellShipToAddressPickerForm
            // 
            this.AcceptButton = this.bt_ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.bt_cancel;
            this.ClientSize = new System.Drawing.Size(464, 341);
            this.Controls.Add(this.bt_cancel);
            this.Controls.Add(this.bt_ok);
            this.Controls.Add(this.lst_addresses);
            this.Controls.Add(this.lbl_hint);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FarnellShipToAddressPickerForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Choose Farnell Shipping Address";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lbl_hint;
        private System.Windows.Forms.ListBox lst_addresses;
        private System.Windows.Forms.Button bt_ok;
        private System.Windows.Forms.Button bt_cancel;
    }
}
