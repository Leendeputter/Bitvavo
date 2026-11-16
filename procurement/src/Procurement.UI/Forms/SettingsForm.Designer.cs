namespace Procurement.UI.Forms
{
    partial class SettingsForm
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
            this.tab_settings = new System.Windows.Forms.TabControl();
            this.tab_suppliers = new System.Windows.Forms.TabPage();
            this.tab_supplierPreferences = new System.Windows.Forms.TabPage();
            this.tab_packagingPolicy = new System.Windows.Forms.TabPage();
            this.tab_approvalPolicy = new System.Windows.Forms.TabPage();
            this.tab_capabilities = new System.Windows.Forms.TabPage();
            this.lbl_suppliersHint = new System.Windows.Forms.Label();
            this.dgv_suppliers = new System.Windows.Forms.DataGridView();
            this.bt_saveSuppliers = new System.Windows.Forms.Button();
            this.bt_editShipping = new System.Windows.Forms.Button();
            this.bt_authorizeOrdering = new System.Windows.Forms.Button();
            this.dgv_supplierPreferences = new System.Windows.Forms.DataGridView();
            this.bt_savePreferences = new System.Windows.Forms.Button();
            this.lbl_packagingHint = new System.Windows.Forms.Label();
            this.dgv_packagingPolicies = new System.Windows.Forms.DataGridView();
            this.bt_savePackaging = new System.Windows.Forms.Button();
            this.lbl_maxOrderValue = new System.Windows.Forms.Label();
            this.nud_maxOrderValue = new System.Windows.Forms.NumericUpDown();
            this.lbl_maxPriceVariance = new System.Windows.Forms.Label();
            this.nud_maxPriceVariance = new System.Windows.Forms.NumericUpDown();
            this.lbl_allowExternalSupplier = new System.Windows.Forms.Label();
            this.chk_allowExternalSupplier = new System.Windows.Forms.CheckBox();
            this.lbl_allowNonOriginalPackaging = new System.Windows.Forms.Label();
            this.chk_allowNonOriginalPackaging = new System.Windows.Forms.CheckBox();
            this.lbl_allowAlternativePart = new System.Windows.Forms.Label();
            this.chk_allowAlternativePart = new System.Windows.Forms.CheckBox();
            this.bt_saveApproval = new System.Windows.Forms.Button();
            this.dgv_capabilities = new System.Windows.Forms.DataGridView();
            this.tab_settings.SuspendLayout();
            this.tab_suppliers.SuspendLayout();
            this.tab_supplierPreferences.SuspendLayout();
            this.tab_packagingPolicy.SuspendLayout();
            this.tab_approvalPolicy.SuspendLayout();
            this.tab_capabilities.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_suppliers)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_supplierPreferences)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_packagingPolicies)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud_maxOrderValue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud_maxPriceVariance)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_capabilities)).BeginInit();
            this.SuspendLayout();
            // 
            // tab_settings
            // 
            this.tab_settings.Controls.Add(this.tab_suppliers);
            this.tab_settings.Controls.Add(this.tab_supplierPreferences);
            this.tab_settings.Controls.Add(this.tab_packagingPolicy);
            this.tab_settings.Controls.Add(this.tab_approvalPolicy);
            this.tab_settings.Controls.Add(this.tab_capabilities);
            this.tab_settings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tab_settings.Location = new System.Drawing.Point(0, 0);
            this.tab_settings.Name = "tab_settings";
            this.tab_settings.SelectedIndex = 0;
            this.tab_settings.Size = new System.Drawing.Size(884, 561);
            this.tab_settings.TabIndex = 0;
            // 
            // tab_suppliers
            // 
            this.tab_suppliers.Controls.Add(this.lbl_suppliersHint);
            this.tab_suppliers.Controls.Add(this.dgv_suppliers);
            this.tab_suppliers.Controls.Add(this.bt_saveSuppliers);
            this.tab_suppliers.Controls.Add(this.bt_editShipping);
            this.tab_suppliers.Controls.Add(this.bt_authorizeOrdering);
            this.tab_suppliers.Location = new System.Drawing.Point(4, 22);
            this.tab_suppliers.Name = "tab_suppliers";
            this.tab_suppliers.Padding = new System.Windows.Forms.Padding(3);
            this.tab_suppliers.Size = new System.Drawing.Size(876, 535);
            this.tab_suppliers.TabIndex = 0;
            this.tab_suppliers.Text = "Suppliers";
            // 
            // tab_supplierPreferences
            // 
            this.tab_supplierPreferences.Controls.Add(this.dgv_supplierPreferences);
            this.tab_supplierPreferences.Controls.Add(this.bt_savePreferences);
            this.tab_supplierPreferences.Location = new System.Drawing.Point(4, 22);
            this.tab_supplierPreferences.Name = "tab_supplierPreferences";
            this.tab_supplierPreferences.Padding = new System.Windows.Forms.Padding(3);
            this.tab_supplierPreferences.Size = new System.Drawing.Size(876, 535);
            this.tab_supplierPreferences.TabIndex = 1;
            this.tab_supplierPreferences.Text = "Supplier preferences";
            // 
            // tab_packagingPolicy
            // 
            this.tab_packagingPolicy.Controls.Add(this.lbl_packagingHint);
            this.tab_packagingPolicy.Controls.Add(this.dgv_packagingPolicies);
            this.tab_packagingPolicy.Controls.Add(this.bt_savePackaging);
            this.tab_packagingPolicy.Location = new System.Drawing.Point(4, 22);
            this.tab_packagingPolicy.Name = "tab_packagingPolicy";
            this.tab_packagingPolicy.Padding = new System.Windows.Forms.Padding(3);
            this.tab_packagingPolicy.Size = new System.Drawing.Size(876, 535);
            this.tab_packagingPolicy.TabIndex = 2;
            this.tab_packagingPolicy.Text = "Packaging policy";
            // 
            // tab_approvalPolicy
            // 
            this.tab_approvalPolicy.Controls.Add(this.lbl_maxOrderValue);
            this.tab_approvalPolicy.Controls.Add(this.nud_maxOrderValue);
            this.tab_approvalPolicy.Controls.Add(this.lbl_maxPriceVariance);
            this.tab_approvalPolicy.Controls.Add(this.nud_maxPriceVariance);
            this.tab_approvalPolicy.Controls.Add(this.lbl_allowExternalSupplier);
            this.tab_approvalPolicy.Controls.Add(this.chk_allowExternalSupplier);
            this.tab_approvalPolicy.Controls.Add(this.lbl_allowNonOriginalPackaging);
            this.tab_approvalPolicy.Controls.Add(this.chk_allowNonOriginalPackaging);
            this.tab_approvalPolicy.Controls.Add(this.lbl_allowAlternativePart);
            this.tab_approvalPolicy.Controls.Add(this.chk_allowAlternativePart);
            this.tab_approvalPolicy.Controls.Add(this.bt_saveApproval);
            this.tab_approvalPolicy.Location = new System.Drawing.Point(4, 22);
            this.tab_approvalPolicy.Name = "tab_approvalPolicy";
            this.tab_approvalPolicy.Padding = new System.Windows.Forms.Padding(3);
            this.tab_approvalPolicy.Size = new System.Drawing.Size(876, 535);
            this.tab_approvalPolicy.TabIndex = 3;
            this.tab_approvalPolicy.Text = "Approval policy";
            // 
            // tab_capabilities
            // 
            this.tab_capabilities.Controls.Add(this.dgv_capabilities);
            this.tab_capabilities.Location = new System.Drawing.Point(4, 22);
            this.tab_capabilities.Name = "tab_capabilities";
            this.tab_capabilities.Padding = new System.Windows.Forms.Padding(3);
            this.tab_capabilities.Size = new System.Drawing.Size(876, 535);
            this.tab_capabilities.TabIndex = 4;
            this.tab_capabilities.Text = "Supplier capabilities";
            // 
            // lbl_suppliersHint
            // 
            this.lbl_suppliersHint.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lbl_suppliersHint.Location = new System.Drawing.Point(6, 6);
            this.lbl_suppliersHint.Name = "lbl_suppliersHint";
            this.lbl_suppliersHint.Size = new System.Drawing.Size(860, 28);
            this.lbl_suppliersHint.TabIndex = 0;
            this.lbl_suppliersHint.Text = "VendorId: MAX Part_Vendor.VENID_07 for this supplier (e.g. Farnell = 0349, DigiKey = 10194) — used to translate the Part_Vendor cross-reference table into recognized supplier mappings.";
            // 
            // dgv_suppliers
            // 
            this.dgv_suppliers.AllowUserToAddRows = false;
            this.dgv_suppliers.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_suppliers.BackgroundColor = System.Drawing.Color.White;
            this.dgv_suppliers.Location = new System.Drawing.Point(6, 38);
            this.dgv_suppliers.Name = "dgv_suppliers";
            this.dgv_suppliers.Size = new System.Drawing.Size(860, 452);
            this.dgv_suppliers.TabIndex = 1;
            // 
            // bt_saveSuppliers
            // 
            this.bt_saveSuppliers.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.bt_saveSuppliers.Location = new System.Drawing.Point(6, 500);
            this.bt_saveSuppliers.Name = "bt_saveSuppliers";
            this.bt_saveSuppliers.Size = new System.Drawing.Size(90, 27);
            this.bt_saveSuppliers.TabIndex = 2;
            this.bt_saveSuppliers.Text = "Save";
            this.bt_saveSuppliers.UseVisualStyleBackColor = true;
            this.bt_saveSuppliers.Click += new System.EventHandler(this.bt_saveSuppliers_Click);
            // 
            // bt_editShipping
            // 
            this.bt_editShipping.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.bt_editShipping.Location = new System.Drawing.Point(102, 500);
            this.bt_editShipping.Name = "bt_editShipping";
            this.bt_editShipping.Size = new System.Drawing.Size(150, 27);
            this.bt_editShipping.TabIndex = 3;
            this.bt_editShipping.Text = "Edit Shipping Details...";
            this.bt_editShipping.UseVisualStyleBackColor = true;
            this.bt_editShipping.Click += new System.EventHandler(this.bt_editShipping_Click);
            // 
            // bt_authorizeOrdering
            // 
            this.bt_authorizeOrdering.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.bt_authorizeOrdering.Location = new System.Drawing.Point(258, 500);
            this.bt_authorizeOrdering.Name = "bt_authorizeOrdering";
            this.bt_authorizeOrdering.Size = new System.Drawing.Size(150, 27);
            this.bt_authorizeOrdering.TabIndex = 4;
            this.bt_authorizeOrdering.Text = "Authorize Ordering...";
            this.bt_authorizeOrdering.UseVisualStyleBackColor = true;
            this.bt_authorizeOrdering.Click += new System.EventHandler(this.bt_authorizeOrdering_Click);
            // 
            // dgv_supplierPreferences
            // 
            this.dgv_supplierPreferences.AllowUserToAddRows = true;
            this.dgv_supplierPreferences.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_supplierPreferences.BackgroundColor = System.Drawing.Color.White;
            this.dgv_supplierPreferences.Location = new System.Drawing.Point(6, 6);
            this.dgv_supplierPreferences.Name = "dgv_supplierPreferences";
            this.dgv_supplierPreferences.Size = new System.Drawing.Size(860, 484);
            this.dgv_supplierPreferences.TabIndex = 0;
            // 
            // bt_savePreferences
            // 
            this.bt_savePreferences.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.bt_savePreferences.Location = new System.Drawing.Point(6, 500);
            this.bt_savePreferences.Name = "bt_savePreferences";
            this.bt_savePreferences.Size = new System.Drawing.Size(90, 27);
            this.bt_savePreferences.TabIndex = 1;
            this.bt_savePreferences.Text = "Save";
            this.bt_savePreferences.UseVisualStyleBackColor = true;
            this.bt_savePreferences.Click += new System.EventHandler(this.bt_savePreferences_Click);
            // 
            // lbl_packagingHint
            // 
            this.lbl_packagingHint.AutoSize = true;
            this.lbl_packagingHint.Location = new System.Drawing.Point(6, 10);
            this.lbl_packagingHint.Name = "lbl_packagingHint";
            this.lbl_packagingHint.Size = new System.Drawing.Size(474, 13);
            this.lbl_packagingHint.TabIndex = 0;
            this.lbl_packagingHint.Text = "AllowedPackaging: comma-separated list of enum names, e.g. OriginalReel,CutTape";
            // 
            // dgv_packagingPolicies
            // 
            this.dgv_packagingPolicies.AllowUserToAddRows = true;
            this.dgv_packagingPolicies.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_packagingPolicies.BackgroundColor = System.Drawing.Color.White;
            this.dgv_packagingPolicies.Location = new System.Drawing.Point(6, 30);
            this.dgv_packagingPolicies.Name = "dgv_packagingPolicies";
            this.dgv_packagingPolicies.Size = new System.Drawing.Size(860, 460);
            this.dgv_packagingPolicies.TabIndex = 1;
            // 
            // bt_savePackaging
            // 
            this.bt_savePackaging.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.bt_savePackaging.Location = new System.Drawing.Point(6, 500);
            this.bt_savePackaging.Name = "bt_savePackaging";
            this.bt_savePackaging.Size = new System.Drawing.Size(90, 27);
            this.bt_savePackaging.TabIndex = 2;
            this.bt_savePackaging.Text = "Save";
            this.bt_savePackaging.UseVisualStyleBackColor = true;
            this.bt_savePackaging.Click += new System.EventHandler(this.bt_savePackaging_Click);
            // 
            // lbl_maxOrderValue
            // 
            this.lbl_maxOrderValue.AutoSize = true;
            this.lbl_maxOrderValue.Location = new System.Drawing.Point(12, 19);
            this.lbl_maxOrderValue.Name = "lbl_maxOrderValue";
            this.lbl_maxOrderValue.Size = new System.Drawing.Size(204, 13);
            this.lbl_maxOrderValue.TabIndex = 0;
            this.lbl_maxOrderValue.Text = "Max order value for auto-approval:";
            // 
            // nud_maxOrderValue
            // 
            this.nud_maxOrderValue.DecimalPlaces = 2;
            this.nud_maxOrderValue.Maximum = new decimal(new int[] {
            1000000,
            0,
            0,
            0});
            this.nud_maxOrderValue.Location = new System.Drawing.Point(230, 16);
            this.nud_maxOrderValue.Name = "nud_maxOrderValue";
            this.nud_maxOrderValue.Size = new System.Drawing.Size(150, 20);
            this.nud_maxOrderValue.TabIndex = 1;
            // 
            // lbl_maxPriceVariance
            // 
            this.lbl_maxPriceVariance.AutoSize = true;
            this.lbl_maxPriceVariance.Location = new System.Drawing.Point(12, 47);
            this.lbl_maxPriceVariance.Name = "lbl_maxPriceVariance";
            this.lbl_maxPriceVariance.Size = new System.Drawing.Size(138, 13);
            this.lbl_maxPriceVariance.TabIndex = 2;
            this.lbl_maxPriceVariance.Text = "Max price variance (%):";
            // 
            // nud_maxPriceVariance
            // 
            this.nud_maxPriceVariance.DecimalPlaces = 1;
            this.nud_maxPriceVariance.Maximum = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.nud_maxPriceVariance.Location = new System.Drawing.Point(230, 44);
            this.nud_maxPriceVariance.Name = "nud_maxPriceVariance";
            this.nud_maxPriceVariance.Size = new System.Drawing.Size(150, 20);
            this.nud_maxPriceVariance.TabIndex = 3;
            // 
            // lbl_allowExternalSupplier
            // 
            this.lbl_allowExternalSupplier.AutoSize = true;
            this.lbl_allowExternalSupplier.Location = new System.Drawing.Point(12, 75);
            this.lbl_allowExternalSupplier.Name = "lbl_allowExternalSupplier";
            this.lbl_allowExternalSupplier.Size = new System.Drawing.Size(156, 13);
            this.lbl_allowExternalSupplier.TabIndex = 4;
            this.lbl_allowExternalSupplier.Text = "External supplier allowed:";
            // 
            // chk_allowExternalSupplier
            // 
            this.chk_allowExternalSupplier.AutoSize = true;
            this.chk_allowExternalSupplier.Location = new System.Drawing.Point(230, 75);
            this.chk_allowExternalSupplier.Name = "chk_allowExternalSupplier";
            this.chk_allowExternalSupplier.Size = new System.Drawing.Size(15, 14);
            this.chk_allowExternalSupplier.TabIndex = 5;
            this.chk_allowExternalSupplier.UseVisualStyleBackColor = true;
            // 
            // lbl_allowNonOriginalPackaging
            // 
            this.lbl_allowNonOriginalPackaging.AutoSize = true;
            this.lbl_allowNonOriginalPackaging.Location = new System.Drawing.Point(12, 103);
            this.lbl_allowNonOriginalPackaging.Name = "lbl_allowNonOriginalPackaging";
            this.lbl_allowNonOriginalPackaging.Size = new System.Drawing.Size(186, 13);
            this.lbl_allowNonOriginalPackaging.TabIndex = 6;
            this.lbl_allowNonOriginalPackaging.Text = "Non-original packaging allowed:";
            // 
            // chk_allowNonOriginalPackaging
            // 
            this.chk_allowNonOriginalPackaging.AutoSize = true;
            this.chk_allowNonOriginalPackaging.Location = new System.Drawing.Point(230, 103);
            this.chk_allowNonOriginalPackaging.Name = "chk_allowNonOriginalPackaging";
            this.chk_allowNonOriginalPackaging.Size = new System.Drawing.Size(15, 14);
            this.chk_allowNonOriginalPackaging.TabIndex = 7;
            this.chk_allowNonOriginalPackaging.UseVisualStyleBackColor = true;
            // 
            // lbl_allowAlternativePart
            // 
            this.lbl_allowAlternativePart.AutoSize = true;
            this.lbl_allowAlternativePart.Location = new System.Drawing.Point(12, 131);
            this.lbl_allowAlternativePart.Name = "lbl_allowAlternativePart";
            this.lbl_allowAlternativePart.Size = new System.Drawing.Size(150, 13);
            this.lbl_allowAlternativePart.TabIndex = 8;
            this.lbl_allowAlternativePart.Text = "Alternative part allowed:";
            // 
            // chk_allowAlternativePart
            // 
            this.chk_allowAlternativePart.AutoSize = true;
            this.chk_allowAlternativePart.Location = new System.Drawing.Point(230, 131);
            this.chk_allowAlternativePart.Name = "chk_allowAlternativePart";
            this.chk_allowAlternativePart.Size = new System.Drawing.Size(15, 14);
            this.chk_allowAlternativePart.TabIndex = 9;
            this.chk_allowAlternativePart.UseVisualStyleBackColor = true;
            // 
            // bt_saveApproval
            // 
            this.bt_saveApproval.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
            this.bt_saveApproval.Location = new System.Drawing.Point(12, 160);
            this.bt_saveApproval.Name = "bt_saveApproval";
            this.bt_saveApproval.Size = new System.Drawing.Size(90, 27);
            this.bt_saveApproval.TabIndex = 10;
            this.bt_saveApproval.Text = "Save";
            this.bt_saveApproval.UseVisualStyleBackColor = true;
            this.bt_saveApproval.Click += new System.EventHandler(this.bt_saveApproval_Click);
            // 
            // dgv_capabilities
            // 
            this.dgv_capabilities.AllowUserToAddRows = false;
            this.dgv_capabilities.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_capabilities.BackgroundColor = System.Drawing.Color.White;
            this.dgv_capabilities.Location = new System.Drawing.Point(6, 6);
            this.dgv_capabilities.Name = "dgv_capabilities";
            this.dgv_capabilities.ReadOnly = true;
            this.dgv_capabilities.Size = new System.Drawing.Size(860, 523);
            this.dgv_capabilities.TabIndex = 0;
            // 
            // SettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(884, 561);
            this.Controls.Add(this.tab_settings);
            this.MinimumSize = new System.Drawing.Size(600, 400);
            this.Name = "SettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Settings";
            this.tab_settings.ResumeLayout(false);
            this.tab_suppliers.ResumeLayout(false);
            this.tab_suppliers.PerformLayout();
            this.tab_supplierPreferences.ResumeLayout(false);
            this.tab_supplierPreferences.PerformLayout();
            this.tab_packagingPolicy.ResumeLayout(false);
            this.tab_packagingPolicy.PerformLayout();
            this.tab_approvalPolicy.ResumeLayout(false);
            this.tab_approvalPolicy.PerformLayout();
            this.tab_capabilities.ResumeLayout(false);
            this.tab_capabilities.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_suppliers)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_supplierPreferences)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_packagingPolicies)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud_maxOrderValue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud_maxPriceVariance)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_capabilities)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tab_settings;
        private System.Windows.Forms.TabPage tab_suppliers;
        private System.Windows.Forms.TabPage tab_supplierPreferences;
        private System.Windows.Forms.TabPage tab_packagingPolicy;
        private System.Windows.Forms.TabPage tab_approvalPolicy;
        private System.Windows.Forms.TabPage tab_capabilities;
        private System.Windows.Forms.Label lbl_suppliersHint;
        private System.Windows.Forms.DataGridView dgv_suppliers;
        private System.Windows.Forms.Button bt_saveSuppliers;
        private System.Windows.Forms.Button bt_editShipping;
        private System.Windows.Forms.Button bt_authorizeOrdering;
        private System.Windows.Forms.DataGridView dgv_supplierPreferences;
        private System.Windows.Forms.Button bt_savePreferences;
        private System.Windows.Forms.Label lbl_packagingHint;
        private System.Windows.Forms.DataGridView dgv_packagingPolicies;
        private System.Windows.Forms.Button bt_savePackaging;
        private System.Windows.Forms.Label lbl_maxOrderValue;
        private System.Windows.Forms.NumericUpDown nud_maxOrderValue;
        private System.Windows.Forms.Label lbl_maxPriceVariance;
        private System.Windows.Forms.NumericUpDown nud_maxPriceVariance;
        private System.Windows.Forms.Label lbl_allowExternalSupplier;
        private System.Windows.Forms.CheckBox chk_allowExternalSupplier;
        private System.Windows.Forms.Label lbl_allowNonOriginalPackaging;
        private System.Windows.Forms.CheckBox chk_allowNonOriginalPackaging;
        private System.Windows.Forms.Label lbl_allowAlternativePart;
        private System.Windows.Forms.CheckBox chk_allowAlternativePart;
        private System.Windows.Forms.Button bt_saveApproval;
        private System.Windows.Forms.DataGridView dgv_capabilities;
    }
}
