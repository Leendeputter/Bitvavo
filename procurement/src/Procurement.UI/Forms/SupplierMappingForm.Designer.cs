namespace Procurement.UI.Forms
{
    partial class SupplierMappingForm
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
            this.lbl_filter = new System.Windows.Forms.Label();
            this.cb_filter = new System.Windows.Forms.ComboBox();
            this.dgv_mappings = new System.Windows.Forms.DataGridView();
            this.bt_verify = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_mappings)).BeginInit();
            this.SuspendLayout();
            // 
            // lbl_filter
            // 
            this.lbl_filter.AutoSize = true;
            this.lbl_filter.Location = new System.Drawing.Point(12, 15);
            this.lbl_filter.Name = "lbl_filter";
            this.lbl_filter.Size = new System.Drawing.Size(137, 13);
            this.lbl_filter.TabIndex = 0;
            this.lbl_filter.Text = "Filter by match confidence:";
            // 
            // cb_filter
            // 
            this.cb_filter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cb_filter.Location = new System.Drawing.Point(155, 12);
            this.cb_filter.Name = "cb_filter";
            this.cb_filter.Size = new System.Drawing.Size(150, 21);
            this.cb_filter.TabIndex = 1;
            this.cb_filter.SelectedIndexChanged += new System.EventHandler(this.cb_filter_SelectedIndexChanged);
            // 
            // dgv_mappings
            // 
            this.dgv_mappings.AllowUserToAddRows = false;
            this.dgv_mappings.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_mappings.BackgroundColor = System.Drawing.Color.White;
            this.dgv_mappings.Location = new System.Drawing.Point(12, 42);
            this.dgv_mappings.MultiSelect = false;
            this.dgv_mappings.Name = "dgv_mappings";
            this.dgv_mappings.ReadOnly = true;
            this.dgv_mappings.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.RowHeaderSelect;
            this.dgv_mappings.Size = new System.Drawing.Size(860, 424);
            this.dgv_mappings.TabIndex = 2;
            // 
            // bt_verify
            // 
            this.bt_verify.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bt_verify.Location = new System.Drawing.Point(742, 474);
            this.bt_verify.Name = "bt_verify";
            this.bt_verify.Size = new System.Drawing.Size(130, 27);
            this.bt_verify.TabIndex = 3;
            this.bt_verify.Text = "Mark as Verified";
            this.bt_verify.UseVisualStyleBackColor = true;
            this.bt_verify.Click += new System.EventHandler(this.bt_verify_Click);
            // 
            // SupplierMappingForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(884, 511);
            this.Controls.Add(this.lbl_filter);
            this.Controls.Add(this.cb_filter);
            this.Controls.Add(this.dgv_mappings);
            this.Controls.Add(this.bt_verify);
            this.MinimumSize = new System.Drawing.Size(500, 300);
            this.Name = "SupplierMappingForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Supplier Mapping";
            ((System.ComponentModel.ISupportInitialize)(this.dgv_mappings)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lbl_filter;
        private System.Windows.Forms.ComboBox cb_filter;
        private System.Windows.Forms.DataGridView dgv_mappings;
        private System.Windows.Forms.Button bt_verify;
    }
}
