namespace Procurement.UI.Forms
{
    partial class ApprovalForm
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
            this.dgv_approvals = new System.Windows.Forms.DataGridView();
            this.lbl_comment = new System.Windows.Forms.Label();
            this.txt_comment = new System.Windows.Forms.TextBox();
            this.bt_approve = new System.Windows.Forms.Button();
            this.bt_reject = new System.Windows.Forms.Button();
            this.lbl_status = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_approvals)).BeginInit();
            this.SuspendLayout();
            // 
            // dgv_approvals
            // 
            this.dgv_approvals.AllowUserToAddRows = false;
            this.dgv_approvals.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_approvals.BackgroundColor = System.Drawing.Color.White;
            this.dgv_approvals.Location = new System.Drawing.Point(12, 12);
            this.dgv_approvals.MultiSelect = false;
            this.dgv_approvals.Name = "dgv_approvals";
            this.dgv_approvals.ReadOnly = true;
            this.dgv_approvals.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.RowHeaderSelect;
            this.dgv_approvals.Size = new System.Drawing.Size(960, 400);
            this.dgv_approvals.TabIndex = 0;
            // 
            // lbl_comment
            // 
            this.lbl_comment.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lbl_comment.AutoSize = true;
            this.lbl_comment.Location = new System.Drawing.Point(12, 428);
            this.lbl_comment.Name = "lbl_comment";
            this.lbl_comment.Size = new System.Drawing.Size(54, 13);
            this.lbl_comment.TabIndex = 1;
            this.lbl_comment.Text = "Comment:";
            // 
            // txt_comment
            // 
            this.txt_comment.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.txt_comment.Location = new System.Drawing.Point(72, 425);
            this.txt_comment.Name = "txt_comment";
            this.txt_comment.Size = new System.Drawing.Size(400, 20);
            this.txt_comment.TabIndex = 2;
            // 
            // bt_approve
            // 
            this.bt_approve.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.bt_approve.Location = new System.Drawing.Point(482, 421);
            this.bt_approve.Name = "bt_approve";
            this.bt_approve.Size = new System.Drawing.Size(90, 27);
            this.bt_approve.TabIndex = 3;
            this.bt_approve.Text = "Approve";
            this.bt_approve.UseVisualStyleBackColor = true;
            this.bt_approve.Click += new System.EventHandler(this.bt_approve_Click);
            // 
            // bt_reject
            // 
            this.bt_reject.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.bt_reject.Location = new System.Drawing.Point(578, 421);
            this.bt_reject.Name = "bt_reject";
            this.bt_reject.Size = new System.Drawing.Size(90, 27);
            this.bt_reject.TabIndex = 4;
            this.bt_reject.Text = "Reject";
            this.bt_reject.UseVisualStyleBackColor = true;
            this.bt_reject.Click += new System.EventHandler(this.bt_reject_Click);
            // 
            // lbl_status
            // 
            this.lbl_status.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lbl_status.Location = new System.Drawing.Point(12, 460);
            this.lbl_status.Name = "lbl_status";
            this.lbl_status.Size = new System.Drawing.Size(960, 20);
            this.lbl_status.TabIndex = 5;
            // 
            // ApprovalForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 491);
            this.Controls.Add(this.dgv_approvals);
            this.Controls.Add(this.lbl_comment);
            this.Controls.Add(this.txt_comment);
            this.Controls.Add(this.bt_approve);
            this.Controls.Add(this.bt_reject);
            this.Controls.Add(this.lbl_status);
            this.MinimumSize = new System.Drawing.Size(720, 300);
            this.Name = "ApprovalForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Approvals";
            ((System.ComponentModel.ISupportInitialize)(this.dgv_approvals)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dgv_approvals;
        private System.Windows.Forms.Label lbl_comment;
        private System.Windows.Forms.TextBox txt_comment;
        private System.Windows.Forms.Button bt_approve;
        private System.Windows.Forms.Button bt_reject;
        private System.Windows.Forms.Label lbl_status;
    }
}
