namespace Procurement.UI.Forms
{
    partial class NewPurchaseRequestForm
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
            this.lbl_articleId = new System.Windows.Forms.Label();
            this.txt_articleId = new System.Windows.Forms.TextBox();
            this.bt_fetchFromMax = new System.Windows.Forms.Button();
            this.lbl_manufacturer = new System.Windows.Forms.Label();
            this.txt_manufacturer = new System.Windows.Forms.TextBox();
            this.lbl_mpn = new System.Windows.Forms.Label();
            this.txt_mpn = new System.Windows.Forms.TextBox();
            this.lbl_description = new System.Windows.Forms.Label();
            this.txt_description = new System.Windows.Forms.TextBox();
            this.lbl_quantity = new System.Windows.Forms.Label();
            this.nud_quantity = new System.Windows.Forms.NumericUpDown();
            this.lbl_requiredDate = new System.Windows.Forms.Label();
            this.dtp_requiredDate = new System.Windows.Forms.DateTimePicker();
            this.lbl_packaging = new System.Windows.Forms.Label();
            this.cb_packaging = new System.Windows.Forms.ComboBox();
            this.chk_reelRequired = new System.Windows.Forms.CheckBox();
            this.lbl_warehouse = new System.Windows.Forms.Label();
            this.txt_warehouse = new System.Windows.Forms.TextBox();
            this.lbl_project = new System.Windows.Forms.Label();
            this.txt_project = new System.Windows.Forms.TextBox();
            this.lbl_priority = new System.Windows.Forms.Label();
            this.nud_priority = new System.Windows.Forms.NumericUpDown();
            this.bt_save = new System.Windows.Forms.Button();
            this.bt_cancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.nud_quantity)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud_priority)).BeginInit();
            this.SuspendLayout();
            // 
            // lbl_articleId
            // 
            this.lbl_articleId.AutoSize = true;
            this.lbl_articleId.Location = new System.Drawing.Point(12, 15);
            this.lbl_articleId.Name = "lbl_articleId";
            this.lbl_articleId.Size = new System.Drawing.Size(130, 13);
            this.lbl_articleId.TabIndex = 0;
            this.lbl_articleId.Text = "Internal Article Number:";
            // 
            // txt_articleId
            // 
            this.txt_articleId.Location = new System.Drawing.Point(170, 12);
            this.txt_articleId.Name = "txt_articleId";
            this.txt_articleId.Size = new System.Drawing.Size(140, 20);
            this.txt_articleId.TabIndex = 1;
            // 
            // bt_fetchFromMax
            // 
            this.bt_fetchFromMax.Location = new System.Drawing.Point(316, 9);
            this.bt_fetchFromMax.Name = "bt_fetchFromMax";
            this.bt_fetchFromMax.Size = new System.Drawing.Size(114, 27);
            this.bt_fetchFromMax.TabIndex = 2;
            this.bt_fetchFromMax.Text = "Fetch from MAX";
            this.bt_fetchFromMax.UseVisualStyleBackColor = true;
            this.bt_fetchFromMax.Click += new System.EventHandler(this.bt_fetchFromMax_Click);
            // 
            // lbl_manufacturer
            // 
            this.lbl_manufacturer.AutoSize = true;
            this.lbl_manufacturer.Location = new System.Drawing.Point(12, 43);
            this.lbl_manufacturer.Name = "lbl_manufacturer";
            this.lbl_manufacturer.Size = new System.Drawing.Size(130, 13);
            this.lbl_manufacturer.TabIndex = 3;
            this.lbl_manufacturer.Text = "Manufacturer:";
            // 
            // txt_manufacturer
            // 
            this.txt_manufacturer.Location = new System.Drawing.Point(170, 40);
            this.txt_manufacturer.Name = "txt_manufacturer";
            this.txt_manufacturer.Size = new System.Drawing.Size(260, 20);
            this.txt_manufacturer.TabIndex = 4;
            this.txt_manufacturer.Text = "Vishay";
            // 
            // lbl_mpn
            // 
            this.lbl_mpn.AutoSize = true;
            this.lbl_mpn.Location = new System.Drawing.Point(12, 71);
            this.lbl_mpn.Name = "lbl_mpn";
            this.lbl_mpn.Size = new System.Drawing.Size(130, 13);
            this.lbl_mpn.TabIndex = 5;
            this.lbl_mpn.Text = "Manufacturer Part Number:";
            // 
            // txt_mpn
            // 
            this.txt_mpn.Location = new System.Drawing.Point(170, 68);
            this.txt_mpn.Name = "txt_mpn";
            this.txt_mpn.Size = new System.Drawing.Size(260, 20);
            this.txt_mpn.TabIndex = 6;
            this.txt_mpn.Text = "CRCW060310K0FKEA";
            // 
            // lbl_description
            // 
            this.lbl_description.AutoSize = true;
            this.lbl_description.Location = new System.Drawing.Point(12, 99);
            this.lbl_description.Name = "lbl_description";
            this.lbl_description.Size = new System.Drawing.Size(130, 13);
            this.lbl_description.TabIndex = 7;
            this.lbl_description.Text = "Description:";
            // 
            // txt_description
            // 
            this.txt_description.Location = new System.Drawing.Point(170, 96);
            this.txt_description.Name = "txt_description";
            this.txt_description.Size = new System.Drawing.Size(260, 20);
            this.txt_description.TabIndex = 8;
            // 
            // lbl_quantity
            // 
            this.lbl_quantity.AutoSize = true;
            this.lbl_quantity.Location = new System.Drawing.Point(12, 127);
            this.lbl_quantity.Name = "lbl_quantity";
            this.lbl_quantity.Size = new System.Drawing.Size(130, 13);
            this.lbl_quantity.TabIndex = 9;
            this.lbl_quantity.Text = "Requested Quantity:";
            // 
            // nud_quantity
            // 
            this.nud_quantity.Maximum = new decimal(new int[] {
            10000000,
            0,
            0,
            0});
            this.nud_quantity.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nud_quantity.Value = new decimal(new int[] {
            12000,
            0,
            0,
            0});
            this.nud_quantity.Location = new System.Drawing.Point(170, 124);
            this.nud_quantity.Name = "nud_quantity";
            this.nud_quantity.Size = new System.Drawing.Size(260, 20);
            this.nud_quantity.TabIndex = 10;
            // 
            // lbl_requiredDate
            // 
            this.lbl_requiredDate.AutoSize = true;
            this.lbl_requiredDate.Location = new System.Drawing.Point(12, 155);
            this.lbl_requiredDate.Name = "lbl_requiredDate";
            this.lbl_requiredDate.Size = new System.Drawing.Size(130, 13);
            this.lbl_requiredDate.TabIndex = 11;
            this.lbl_requiredDate.Text = "Required Date:";
            // 
            // dtp_requiredDate
            // 
            this.dtp_requiredDate.Location = new System.Drawing.Point(170, 152);
            this.dtp_requiredDate.Name = "dtp_requiredDate";
            this.dtp_requiredDate.Size = new System.Drawing.Size(260, 20);
            this.dtp_requiredDate.TabIndex = 12;
            // 
            // lbl_packaging
            // 
            this.lbl_packaging.AutoSize = true;
            this.lbl_packaging.Location = new System.Drawing.Point(12, 183);
            this.lbl_packaging.Name = "lbl_packaging";
            this.lbl_packaging.Size = new System.Drawing.Size(130, 13);
            this.lbl_packaging.TabIndex = 13;
            this.lbl_packaging.Text = "Packaging Requirement:";
            // 
            // cb_packaging
            // 
            this.cb_packaging.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cb_packaging.FormattingEnabled = true;
            this.cb_packaging.Location = new System.Drawing.Point(170, 180);
            this.cb_packaging.Name = "cb_packaging";
            this.cb_packaging.Size = new System.Drawing.Size(260, 21);
            this.cb_packaging.TabIndex = 14;
            // 
            // chk_reelRequired
            // 
            this.chk_reelRequired.AutoSize = true;
            this.chk_reelRequired.Checked = true;
            this.chk_reelRequired.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chk_reelRequired.Location = new System.Drawing.Point(170, 210);
            this.chk_reelRequired.Name = "chk_reelRequired";
            this.chk_reelRequired.Size = new System.Drawing.Size(100, 17);
            this.chk_reelRequired.TabIndex = 15;
            this.chk_reelRequired.Text = "Reel Required";
            this.chk_reelRequired.UseVisualStyleBackColor = true;
            // 
            // lbl_warehouse
            // 
            this.lbl_warehouse.AutoSize = true;
            this.lbl_warehouse.Location = new System.Drawing.Point(12, 239);
            this.lbl_warehouse.Name = "lbl_warehouse";
            this.lbl_warehouse.Size = new System.Drawing.Size(130, 13);
            this.lbl_warehouse.TabIndex = 16;
            this.lbl_warehouse.Text = "Warehouse:";
            // 
            // txt_warehouse
            // 
            this.txt_warehouse.Location = new System.Drawing.Point(170, 236);
            this.txt_warehouse.Name = "txt_warehouse";
            this.txt_warehouse.Size = new System.Drawing.Size(260, 20);
            this.txt_warehouse.TabIndex = 17;
            this.txt_warehouse.Text = "WAREHOUSE-1";
            // 
            // lbl_project
            // 
            this.lbl_project.AutoSize = true;
            this.lbl_project.Location = new System.Drawing.Point(12, 267);
            this.lbl_project.Name = "lbl_project";
            this.lbl_project.Size = new System.Drawing.Size(130, 13);
            this.lbl_project.TabIndex = 18;
            this.lbl_project.Text = "Project:";
            // 
            // txt_project
            // 
            this.txt_project.Location = new System.Drawing.Point(170, 264);
            this.txt_project.Name = "txt_project";
            this.txt_project.Size = new System.Drawing.Size(260, 20);
            this.txt_project.TabIndex = 19;
            this.txt_project.Text = "PROTOTYPE";
            // 
            // lbl_priority
            // 
            this.lbl_priority.AutoSize = true;
            this.lbl_priority.Location = new System.Drawing.Point(12, 295);
            this.lbl_priority.Name = "lbl_priority";
            this.lbl_priority.Size = new System.Drawing.Size(130, 13);
            this.lbl_priority.TabIndex = 20;
            this.lbl_priority.Text = "Priority (1=high):";
            // 
            // nud_priority
            // 
            this.nud_priority.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.nud_priority.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nud_priority.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.nud_priority.Location = new System.Drawing.Point(170, 292);
            this.nud_priority.Name = "nud_priority";
            this.nud_priority.Size = new System.Drawing.Size(260, 20);
            this.nud_priority.TabIndex = 21;
            // 
            // bt_save
            // 
            this.bt_save.Location = new System.Drawing.Point(244, 330);
            this.bt_save.Name = "bt_save";
            this.bt_save.Size = new System.Drawing.Size(90, 27);
            this.bt_save.TabIndex = 22;
            this.bt_save.Text = "Save";
            this.bt_save.UseVisualStyleBackColor = true;
            this.bt_save.Click += new System.EventHandler(this.bt_save_Click);
            // 
            // bt_cancel
            // 
            this.bt_cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.bt_cancel.Location = new System.Drawing.Point(340, 330);
            this.bt_cancel.Name = "bt_cancel";
            this.bt_cancel.Size = new System.Drawing.Size(90, 27);
            this.bt_cancel.TabIndex = 23;
            this.bt_cancel.Text = "Cancel";
            this.bt_cancel.UseVisualStyleBackColor = true;
            // 
            // NewPurchaseRequestForm
            // 
            this.AcceptButton = this.bt_save;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.bt_cancel;
            this.ClientSize = new System.Drawing.Size(444, 369);
            this.Controls.Add(this.bt_cancel);
            this.Controls.Add(this.bt_save);
            this.Controls.Add(this.nud_priority);
            this.Controls.Add(this.lbl_priority);
            this.Controls.Add(this.txt_project);
            this.Controls.Add(this.lbl_project);
            this.Controls.Add(this.txt_warehouse);
            this.Controls.Add(this.lbl_warehouse);
            this.Controls.Add(this.chk_reelRequired);
            this.Controls.Add(this.cb_packaging);
            this.Controls.Add(this.lbl_packaging);
            this.Controls.Add(this.dtp_requiredDate);
            this.Controls.Add(this.lbl_requiredDate);
            this.Controls.Add(this.nud_quantity);
            this.Controls.Add(this.lbl_quantity);
            this.Controls.Add(this.txt_description);
            this.Controls.Add(this.lbl_description);
            this.Controls.Add(this.txt_mpn);
            this.Controls.Add(this.lbl_mpn);
            this.Controls.Add(this.txt_manufacturer);
            this.Controls.Add(this.lbl_manufacturer);
            this.Controls.Add(this.bt_fetchFromMax);
            this.Controls.Add(this.txt_articleId);
            this.Controls.Add(this.lbl_articleId);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NewPurchaseRequestForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Add New Test Request";
            ((System.ComponentModel.ISupportInitialize)(this.nud_quantity)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud_priority)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lbl_articleId;
        private System.Windows.Forms.TextBox txt_articleId;
        private System.Windows.Forms.Button bt_fetchFromMax;
        private System.Windows.Forms.Label lbl_manufacturer;
        private System.Windows.Forms.TextBox txt_manufacturer;
        private System.Windows.Forms.Label lbl_mpn;
        private System.Windows.Forms.TextBox txt_mpn;
        private System.Windows.Forms.Label lbl_description;
        private System.Windows.Forms.TextBox txt_description;
        private System.Windows.Forms.Label lbl_quantity;
        private System.Windows.Forms.NumericUpDown nud_quantity;
        private System.Windows.Forms.Label lbl_requiredDate;
        private System.Windows.Forms.DateTimePicker dtp_requiredDate;
        private System.Windows.Forms.Label lbl_packaging;
        private System.Windows.Forms.ComboBox cb_packaging;
        private System.Windows.Forms.CheckBox chk_reelRequired;
        private System.Windows.Forms.Label lbl_warehouse;
        private System.Windows.Forms.TextBox txt_warehouse;
        private System.Windows.Forms.Label lbl_project;
        private System.Windows.Forms.TextBox txt_project;
        private System.Windows.Forms.Label lbl_priority;
        private System.Windows.Forms.NumericUpDown nud_priority;
        private System.Windows.Forms.Button bt_save;
        private System.Windows.Forms.Button bt_cancel;
    }
}
