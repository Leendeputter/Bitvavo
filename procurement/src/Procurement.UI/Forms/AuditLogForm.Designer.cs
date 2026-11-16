namespace Procurement.UI.Forms
{
    partial class AuditLogForm
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
            this.lbl_entityType = new System.Windows.Forms.Label();
            this.txt_entityType = new System.Windows.Forms.TextBox();
            this.lbl_eventType = new System.Windows.Forms.Label();
            this.txt_eventType = new System.Windows.Forms.TextBox();
            this.lbl_supplier = new System.Windows.Forms.Label();
            this.txt_supplier = new System.Windows.Forms.TextBox();
            this.chk_from = new System.Windows.Forms.CheckBox();
            this.dtp_from = new System.Windows.Forms.DateTimePicker();
            this.chk_to = new System.Windows.Forms.CheckBox();
            this.dtp_to = new System.Windows.Forms.DateTimePicker();
            this.bt_search = new System.Windows.Forms.Button();
            this.dgv_auditlog = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_auditlog)).BeginInit();
            this.SuspendLayout();
            // 
            // lbl_entityType
            // 
            this.lbl_entityType.AutoSize = true;
            this.lbl_entityType.Location = new System.Drawing.Point(12, 16);
            this.lbl_entityType.Name = "lbl_entityType";
            this.lbl_entityType.Size = new System.Drawing.Size(62, 13);
            this.lbl_entityType.TabIndex = 0;
            this.lbl_entityType.Text = "EntityType:";
            // 
            // txt_entityType
            // 
            this.txt_entityType.Location = new System.Drawing.Point(80, 13);
            this.txt_entityType.Name = "txt_entityType";
            this.txt_entityType.Size = new System.Drawing.Size(130, 20);
            this.txt_entityType.TabIndex = 1;
            // 
            // lbl_eventType
            // 
            this.lbl_eventType.AutoSize = true;
            this.lbl_eventType.Location = new System.Drawing.Point(222, 16);
            this.lbl_eventType.Name = "lbl_eventType";
            this.lbl_eventType.Size = new System.Drawing.Size(62, 13);
            this.lbl_eventType.TabIndex = 2;
            this.lbl_eventType.Text = "EventType:";
            // 
            // txt_eventType
            // 
            this.txt_eventType.Location = new System.Drawing.Point(290, 13);
            this.txt_eventType.Name = "txt_eventType";
            this.txt_eventType.Size = new System.Drawing.Size(160, 20);
            this.txt_eventType.TabIndex = 3;
            // 
            // lbl_supplier
            // 
            this.lbl_supplier.AutoSize = true;
            this.lbl_supplier.Location = new System.Drawing.Point(462, 16);
            this.lbl_supplier.Name = "lbl_supplier";
            this.lbl_supplier.Size = new System.Drawing.Size(48, 13);
            this.lbl_supplier.TabIndex = 4;
            this.lbl_supplier.Text = "Supplier:";
            // 
            // txt_supplier
            // 
            this.txt_supplier.Location = new System.Drawing.Point(516, 13);
            this.txt_supplier.Name = "txt_supplier";
            this.txt_supplier.Size = new System.Drawing.Size(100, 20);
            this.txt_supplier.TabIndex = 5;
            // 
            // chk_from
            // 
            this.chk_from.AutoSize = true;
            this.chk_from.Location = new System.Drawing.Point(630, 15);
            this.chk_from.Name = "chk_from";
            this.chk_from.Size = new System.Drawing.Size(52, 17);
            this.chk_from.TabIndex = 6;
            this.chk_from.Text = "From:";
            this.chk_from.UseVisualStyleBackColor = true;
            this.chk_from.CheckedChanged += new System.EventHandler(this.chk_from_CheckedChanged);
            // 
            // dtp_from
            // 
            this.dtp_from.Enabled = false;
            this.dtp_from.Location = new System.Drawing.Point(686, 13);
            this.dtp_from.Name = "dtp_from";
            this.dtp_from.Size = new System.Drawing.Size(130, 20);
            this.dtp_from.TabIndex = 7;
            // 
            // chk_to
            // 
            this.chk_to.AutoSize = true;
            this.chk_to.Location = new System.Drawing.Point(828, 15);
            this.chk_to.Name = "chk_to";
            this.chk_to.Size = new System.Drawing.Size(42, 17);
            this.chk_to.TabIndex = 8;
            this.chk_to.Text = "To:";
            this.chk_to.UseVisualStyleBackColor = true;
            this.chk_to.CheckedChanged += new System.EventHandler(this.chk_to_CheckedChanged);
            // 
            // dtp_to
            // 
            this.dtp_to.Enabled = false;
            this.dtp_to.Location = new System.Drawing.Point(874, 13);
            this.dtp_to.Name = "dtp_to";
            this.dtp_to.Size = new System.Drawing.Size(130, 20);
            this.dtp_to.TabIndex = 9;
            // 
            // bt_search
            // 
            this.bt_search.Location = new System.Drawing.Point(1016, 9);
            this.bt_search.Name = "bt_search";
            this.bt_search.Size = new System.Drawing.Size(90, 27);
            this.bt_search.TabIndex = 10;
            this.bt_search.Text = "Search";
            this.bt_search.UseVisualStyleBackColor = true;
            this.bt_search.Click += new System.EventHandler(this.bt_search_Click);
            // 
            // dgv_auditlog
            // 
            this.dgv_auditlog.AllowUserToAddRows = false;
            this.dgv_auditlog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_auditlog.BackgroundColor = System.Drawing.Color.White;
            this.dgv_auditlog.Location = new System.Drawing.Point(12, 45);
            this.dgv_auditlog.MultiSelect = false;
            this.dgv_auditlog.Name = "dgv_auditlog";
            this.dgv_auditlog.ReadOnly = true;
            this.dgv_auditlog.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.RowHeaderSelect;
            this.dgv_auditlog.Size = new System.Drawing.Size(1160, 554);
            this.dgv_auditlog.TabIndex = 11;
            this.dgv_auditlog.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgv_auditlog_CellDoubleClick);
            // 
            // AuditLogForm
            // 
            this.AcceptButton = this.bt_search;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1184, 611);
            this.Controls.Add(this.dgv_auditlog);
            this.Controls.Add(this.bt_search);
            this.Controls.Add(this.dtp_to);
            this.Controls.Add(this.chk_to);
            this.Controls.Add(this.dtp_from);
            this.Controls.Add(this.chk_from);
            this.Controls.Add(this.txt_supplier);
            this.Controls.Add(this.lbl_supplier);
            this.Controls.Add(this.txt_eventType);
            this.Controls.Add(this.lbl_eventType);
            this.Controls.Add(this.txt_entityType);
            this.Controls.Add(this.lbl_entityType);
            this.MinimumSize = new System.Drawing.Size(800, 300);
            this.Name = "AuditLogForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Audit Log";
            ((System.ComponentModel.ISupportInitialize)(this.dgv_auditlog)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lbl_entityType;
        private System.Windows.Forms.TextBox txt_entityType;
        private System.Windows.Forms.Label lbl_eventType;
        private System.Windows.Forms.TextBox txt_eventType;
        private System.Windows.Forms.Label lbl_supplier;
        private System.Windows.Forms.TextBox txt_supplier;
        private System.Windows.Forms.CheckBox chk_from;
        private System.Windows.Forms.DateTimePicker dtp_from;
        private System.Windows.Forms.CheckBox chk_to;
        private System.Windows.Forms.DateTimePicker dtp_to;
        private System.Windows.Forms.Button bt_search;
        private System.Windows.Forms.DataGridView dgv_auditlog;
    }
}
