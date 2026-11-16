namespace Procurement.UI.Forms
{
    partial class OrdersForm
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
            this.bt_refresh = new System.Windows.Forms.Button();
            this.gb_include = new Procurement.UI.Support.ClassicStyle.ClassicGroupBox();
            this.chk_includeCompleted = new System.Windows.Forms.CheckBox();
            this.chk_includeDeleted = new System.Windows.Forms.CheckBox();
            this.split_orders = new System.Windows.Forms.SplitContainer();
            this.lbl_orders = new System.Windows.Forms.Label();
            this.dgv_orders = new System.Windows.Forms.DataGridView();
            this.lbl_lines = new System.Windows.Forms.Label();
            this.dgv_lines = new System.Windows.Forms.DataGridView();
            this.lbl_status = new System.Windows.Forms.Label();
            this.gb_include.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.split_orders)).BeginInit();
            this.split_orders.Panel1.SuspendLayout();
            this.split_orders.Panel2.SuspendLayout();
            this.split_orders.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_orders)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_lines)).BeginInit();
            this.SuspendLayout();
            // 
            // bt_refresh
            // 
            this.bt_refresh.Location = new System.Drawing.Point(12, 22);
            this.bt_refresh.Name = "bt_refresh";
            this.bt_refresh.Size = new System.Drawing.Size(90, 27);
            this.bt_refresh.TabIndex = 0;
            this.bt_refresh.Text = "Refresh";
            this.bt_refresh.UseVisualStyleBackColor = true;
            this.bt_refresh.Click += new System.EventHandler(this.bt_refresh_Click);
            // 
            // gb_include
            // 
            this.gb_include.Controls.Add(this.chk_includeCompleted);
            this.gb_include.Controls.Add(this.chk_includeDeleted);
            this.gb_include.Location = new System.Drawing.Point(114, 6);
            this.gb_include.Name = "gb_include";
            this.gb_include.Size = new System.Drawing.Size(220, 62);
            this.gb_include.TabIndex = 1;
            this.gb_include.TabStop = false;
            this.gb_include.Text = "Include";
            // 
            // chk_includeCompleted
            // 
            this.chk_includeCompleted.AutoSize = true;
            this.chk_includeCompleted.Location = new System.Drawing.Point(12, 18);
            this.chk_includeCompleted.Name = "chk_includeCompleted";
            this.chk_includeCompleted.Size = new System.Drawing.Size(163, 17);
            this.chk_includeCompleted.TabIndex = 0;
            this.chk_includeCompleted.Text = "Status 4 Orders (Completed)";
            this.chk_includeCompleted.UseVisualStyleBackColor = true;
            this.chk_includeCompleted.CheckedChanged += new System.EventHandler(this.chk_includeCompleted_CheckedChanged);
            // 
            // chk_includeDeleted
            // 
            this.chk_includeDeleted.AutoSize = true;
            this.chk_includeDeleted.Location = new System.Drawing.Point(12, 39);
            this.chk_includeDeleted.Name = "chk_includeDeleted";
            this.chk_includeDeleted.Size = new System.Drawing.Size(99, 17);
            this.chk_includeDeleted.TabIndex = 1;
            this.chk_includeDeleted.Text = "Deleted in MAX";
            this.chk_includeDeleted.UseVisualStyleBackColor = true;
            this.chk_includeDeleted.CheckedChanged += new System.EventHandler(this.chk_includeDeleted_CheckedChanged);
            // 
            // split_orders
            // 
            this.split_orders.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.split_orders.Location = new System.Drawing.Point(12, 74);
            this.split_orders.Name = "split_orders";
            this.split_orders.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // split_orders.Panel1
            // 
            this.split_orders.Panel1.Controls.Add(this.lbl_orders);
            this.split_orders.Panel1.Controls.Add(this.dgv_orders);
            // 
            // split_orders.Panel2
            // 
            this.split_orders.Panel2.Controls.Add(this.lbl_lines);
            this.split_orders.Panel2.Controls.Add(this.dgv_lines);
            this.split_orders.Size = new System.Drawing.Size(1110, 505);
            this.split_orders.SplitterDistance = 270;
            this.split_orders.TabIndex = 2;
            // 
            // lbl_orders
            // 
            this.lbl_orders.AutoSize = true;
            this.lbl_orders.Location = new System.Drawing.Point(0, 3);
            this.lbl_orders.Name = "lbl_orders";
            this.lbl_orders.Size = new System.Drawing.Size(105, 13);
            this.lbl_orders.TabIndex = 0;
            this.lbl_orders.Text = "All Purchase Orders:";
            // 
            // dgv_orders
            // 
            this.dgv_orders.AllowUserToAddRows = false;
            this.dgv_orders.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_orders.BackgroundColor = System.Drawing.Color.White;
            this.dgv_orders.Location = new System.Drawing.Point(0, 20);
            this.dgv_orders.MultiSelect = false;
            this.dgv_orders.Name = "dgv_orders";
            this.dgv_orders.ReadOnly = true;
            this.dgv_orders.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.RowHeaderSelect;
            this.dgv_orders.Size = new System.Drawing.Size(1110, 250);
            this.dgv_orders.TabIndex = 1;
            // 
            // lbl_lines
            // 
            this.lbl_lines.AutoSize = true;
            this.lbl_lines.Location = new System.Drawing.Point(0, 3);
            this.lbl_lines.Name = "lbl_lines";
            this.lbl_lines.Size = new System.Drawing.Size(110, 13);
            this.lbl_lines.TabIndex = 0;
            this.lbl_lines.Text = "Lines of Selected PO:";
            // 
            // dgv_lines
            // 
            this.dgv_lines.AllowUserToAddRows = false;
            this.dgv_lines.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_lines.BackgroundColor = System.Drawing.Color.White;
            this.dgv_lines.Location = new System.Drawing.Point(0, 20);
            this.dgv_lines.MultiSelect = false;
            this.dgv_lines.Name = "dgv_lines";
            this.dgv_lines.ReadOnly = true;
            this.dgv_lines.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.RowHeaderSelect;
            this.dgv_lines.Size = new System.Drawing.Size(1110, 211);
            this.dgv_lines.TabIndex = 1;
            this.dgv_orders.SelectionChanged += new System.EventHandler(this.dgv_orders_SelectionChanged);
            // 
            // lbl_status
            // 
            this.lbl_status.BackColor = System.Drawing.SystemColors.ControlLight;
            this.lbl_status.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lbl_status.Location = new System.Drawing.Point(0, 587);
            this.lbl_status.Name = "lbl_status";
            this.lbl_status.Padding = new System.Windows.Forms.Padding(4);
            this.lbl_status.Size = new System.Drawing.Size(1134, 24);
            this.lbl_status.TabIndex = 3;
            // 
            // OrdersForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1134, 611);
            this.Controls.Add(this.split_orders);
            this.Controls.Add(this.gb_include);
            this.Controls.Add(this.bt_refresh);
            this.Controls.Add(this.lbl_status);
            this.MinimumSize = new System.Drawing.Size(700, 400);
            this.Name = "OrdersForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Orders";
            this.gb_include.ResumeLayout(false);
            this.gb_include.PerformLayout();
            this.split_orders.Panel1.ResumeLayout(false);
            this.split_orders.Panel1.PerformLayout();
            this.split_orders.Panel2.ResumeLayout(false);
            this.split_orders.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.split_orders)).EndInit();
            this.split_orders.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgv_orders)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_lines)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button bt_refresh;
        private Procurement.UI.Support.ClassicStyle.ClassicGroupBox gb_include;
        private System.Windows.Forms.CheckBox chk_includeCompleted;
        private System.Windows.Forms.CheckBox chk_includeDeleted;
        private System.Windows.Forms.SplitContainer split_orders;
        private System.Windows.Forms.Label lbl_orders;
        private System.Windows.Forms.DataGridView dgv_orders;
        private System.Windows.Forms.Label lbl_lines;
        private System.Windows.Forms.DataGridView dgv_lines;
        private System.Windows.Forms.Label lbl_status;
    }
}
