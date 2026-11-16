namespace Procurement.UI.Forms
{
    partial class OrderConfirmationExceptionsForm
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
            this.dgv_exceptions = new System.Windows.Forms.DataGridView();
            this.bt_close = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_exceptions)).BeginInit();
            this.SuspendLayout();
            // 
            // dgv_exceptions
            // 
            this.dgv_exceptions.AllowUserToAddRows = false;
            this.dgv_exceptions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_exceptions.BackgroundColor = System.Drawing.Color.White;
            this.dgv_exceptions.Location = new System.Drawing.Point(12, 12);
            this.dgv_exceptions.MultiSelect = false;
            this.dgv_exceptions.Name = "dgv_exceptions";
            this.dgv_exceptions.ReadOnly = true;
            this.dgv_exceptions.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.RowHeaderSelect;
            this.dgv_exceptions.Size = new System.Drawing.Size(960, 400);
            this.dgv_exceptions.TabIndex = 0;
            // 
            // bt_close
            // 
            this.bt_close.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bt_close.Location = new System.Drawing.Point(882, 422);
            this.bt_close.Name = "bt_close";
            this.bt_close.Size = new System.Drawing.Size(90, 27);
            this.bt_close.TabIndex = 1;
            this.bt_close.Text = "Close";
            this.bt_close.UseVisualStyleBackColor = true;
            this.bt_close.Click += new System.EventHandler(this.bt_close_Click);
            // 
            // OrderConfirmationExceptionsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.bt_close;
            this.ClientSize = new System.Drawing.Size(984, 461);
            this.Controls.Add(this.bt_close);
            this.Controls.Add(this.dgv_exceptions);
            this.MinimumSize = new System.Drawing.Size(600, 250);
            this.Name = "OrderConfirmationExceptionsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Order Confirmation – Deviations";
            ((System.ComponentModel.ISupportInitialize)(this.dgv_exceptions)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dgv_exceptions;
        private System.Windows.Forms.Button bt_close;
    }
}
