namespace Procurement.UI.Forms
{
    partial class MainForm
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
            this.bt_newRequest = new System.Windows.Forms.Button();
            this.bt_startSourcing = new System.Windows.Forms.Button();
            this.bt_sourceAll = new System.Windows.Forms.Button();
            this.bt_placeOrders = new System.Windows.Forms.Button();
            this.bt_processConfirmations = new System.Windows.Forms.Button();
            this.gb_query = new Procurement.UI.Support.ClassicStyle.ClassicGroupBox();
            this.chk_statusPlanned = new System.Windows.Forms.CheckBox();
            this.chk_statusApproved = new System.Windows.Forms.CheckBox();
            this.bt_query = new System.Windows.Forms.Button();
            this.gb_dueDateRange = new Procurement.UI.Support.ClassicStyle.ClassicGroupBox();
            this.chk_dueDateEnable = new System.Windows.Forms.CheckBox();
            this.lbl_startDate = new System.Windows.Forms.Label();
            this.dtp_dueDateStart = new System.Windows.Forms.DateTimePicker();
            this.lbl_endDate = new System.Windows.Forms.Label();
            this.dtp_dueDateEnd = new System.Windows.Forms.DateTimePicker();
            this.gb_filter = new Procurement.UI.Support.ClassicStyle.ClassicGroupBox();
            this.lbl_selectBy = new System.Windows.Forms.Label();
            this.cb_rangeField = new System.Windows.Forms.ComboBox();
            this.lbl_rangeStart = new System.Windows.Forms.Label();
            this.txt_rangeStart = new System.Windows.Forms.TextBox();
            this.lbl_rangeEnd = new System.Windows.Forms.Label();
            this.txt_rangeEnd = new System.Windows.Forms.TextBox();
            this.gb_display = new Procurement.UI.Support.ClassicStyle.ClassicGroupBox();
            this.chk_statusPending = new System.Windows.Forms.CheckBox();
            this.chk_statusSourcing = new System.Windows.Forms.CheckBox();
            this.chk_statusWaitingApproval = new System.Windows.Forms.CheckBox();
            this.chk_statusReadyToOrder = new System.Windows.Forms.CheckBox();
            this.chk_statusException = new System.Windows.Forms.CheckBox();
            this.chk_showTestRequests = new System.Windows.Forms.CheckBox();
            this.dgv_requests = new System.Windows.Forms.DataGridView();
            this.lbl_offers = new System.Windows.Forms.Label();
            this.dgv_offers = new System.Windows.Forms.DataGridView();
            this.lbl_status = new System.Windows.Forms.Label();
            this.gb_query.SuspendLayout();
            this.gb_dueDateRange.SuspendLayout();
            this.gb_filter.SuspendLayout();
            this.gb_display.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_requests)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_offers)).BeginInit();
            this.SuspendLayout();
            // 
            // bt_newRequest
            // 
            this.bt_newRequest.Location = new System.Drawing.Point(12, 12);
            this.bt_newRequest.Name = "bt_newRequest";
            this.bt_newRequest.Size = new System.Drawing.Size(140, 27);
            this.bt_newRequest.TabIndex = 0;
            this.bt_newRequest.Text = "Add New Test Request";
            this.bt_newRequest.UseVisualStyleBackColor = true;
            this.bt_newRequest.Click += new System.EventHandler(this.bt_newRequest_Click);
            // 
            // bt_startSourcing
            // 
            this.bt_startSourcing.Location = new System.Drawing.Point(158, 12);
            this.bt_startSourcing.Name = "bt_startSourcing";
            this.bt_startSourcing.Size = new System.Drawing.Size(160, 27);
            this.bt_startSourcing.TabIndex = 1;
            this.bt_startSourcing.Text = "Start Sourcing (Selection)";
            this.bt_startSourcing.UseVisualStyleBackColor = true;
            this.bt_startSourcing.Click += new System.EventHandler(this.bt_startSourcing_Click);
            // 
            // bt_sourceAll
            // 
            this.bt_sourceAll.Location = new System.Drawing.Point(324, 12);
            this.bt_sourceAll.Name = "bt_sourceAll";
            this.bt_sourceAll.Size = new System.Drawing.Size(90, 27);
            this.bt_sourceAll.TabIndex = 2;
            this.bt_sourceAll.Text = "Source All";
            this.bt_sourceAll.UseVisualStyleBackColor = true;
            this.bt_sourceAll.Click += new System.EventHandler(this.bt_sourceAll_Click);
            // 
            // bt_placeOrders
            // 
            this.bt_placeOrders.Location = new System.Drawing.Point(420, 12);
            this.bt_placeOrders.Name = "bt_placeOrders";
            this.bt_placeOrders.Size = new System.Drawing.Size(150, 27);
            this.bt_placeOrders.TabIndex = 3;
            this.bt_placeOrders.Text = "Place Order (Selection)";
            this.bt_placeOrders.UseVisualStyleBackColor = true;
            this.bt_placeOrders.Click += new System.EventHandler(this.bt_placeOrders_Click);
            // 
            // bt_processConfirmations
            // 
            this.bt_processConfirmations.Location = new System.Drawing.Point(576, 12);
            this.bt_processConfirmations.Name = "bt_processConfirmations";
            this.bt_processConfirmations.Size = new System.Drawing.Size(170, 27);
            this.bt_processConfirmations.TabIndex = 4;
            this.bt_processConfirmations.Text = "Process Order Confirmations";
            this.bt_processConfirmations.UseVisualStyleBackColor = true;
            this.bt_processConfirmations.Click += new System.EventHandler(this.bt_processConfirmations_Click);
            // 
            // gb_query
            // 
            this.gb_query.Controls.Add(this.chk_statusPlanned);
            this.gb_query.Controls.Add(this.chk_statusApproved);
            this.gb_query.Controls.Add(this.bt_query);
            this.gb_query.Location = new System.Drawing.Point(12, 46);
            this.gb_query.Name = "gb_query";
            this.gb_query.Size = new System.Drawing.Size(150, 152);
            this.gb_query.TabIndex = 5;
            this.gb_query.TabStop = false;
            this.gb_query.Text = "Fetch Orders from MAX";
            // 
            // chk_statusPlanned
            // 
            this.chk_statusPlanned.AutoSize = true;
            this.chk_statusPlanned.Location = new System.Drawing.Point(12, 20);
            this.chk_statusPlanned.Name = "chk_statusPlanned";
            this.chk_statusPlanned.Size = new System.Drawing.Size(90, 17);
            this.chk_statusPlanned.TabIndex = 0;
            this.chk_statusPlanned.Text = "1 - Planned";
            this.chk_statusPlanned.UseVisualStyleBackColor = true;
            // 
            // chk_statusApproved
            // 
            this.chk_statusApproved.AutoSize = true;
            this.chk_statusApproved.Checked = true;
            this.chk_statusApproved.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chk_statusApproved.Location = new System.Drawing.Point(12, 42);
            this.chk_statusApproved.Name = "chk_statusApproved";
            this.chk_statusApproved.Size = new System.Drawing.Size(96, 17);
            this.chk_statusApproved.TabIndex = 1;
            this.chk_statusApproved.Text = "2 - Approved";
            this.chk_statusApproved.UseVisualStyleBackColor = true;
            // 
            // bt_query
            // 
            this.bt_query.Location = new System.Drawing.Point(12, 70);
            this.bt_query.Name = "bt_query";
            this.bt_query.Size = new System.Drawing.Size(90, 27);
            this.bt_query.TabIndex = 2;
            this.bt_query.Text = "Query";
            this.bt_query.UseVisualStyleBackColor = true;
            this.bt_query.Click += new System.EventHandler(this.bt_query_Click);
            // 
            // gb_dueDateRange
            // 
            this.gb_dueDateRange.Controls.Add(this.chk_dueDateEnable);
            this.gb_dueDateRange.Controls.Add(this.lbl_startDate);
            this.gb_dueDateRange.Controls.Add(this.dtp_dueDateStart);
            this.gb_dueDateRange.Controls.Add(this.lbl_endDate);
            this.gb_dueDateRange.Controls.Add(this.dtp_dueDateEnd);
            this.gb_dueDateRange.Location = new System.Drawing.Point(168, 46);
            this.gb_dueDateRange.Name = "gb_dueDateRange";
            this.gb_dueDateRange.Size = new System.Drawing.Size(200, 152);
            this.gb_dueDateRange.TabIndex = 6;
            this.gb_dueDateRange.TabStop = false;
            this.gb_dueDateRange.Text = "Due Date Range";
            // 
            // chk_dueDateEnable
            // 
            this.chk_dueDateEnable.AutoSize = true;
            this.chk_dueDateEnable.Location = new System.Drawing.Point(12, 20);
            this.chk_dueDateEnable.Name = "chk_dueDateEnable";
            this.chk_dueDateEnable.Size = new System.Drawing.Size(60, 17);
            this.chk_dueDateEnable.TabIndex = 0;
            this.chk_dueDateEnable.Text = "Enable";
            this.chk_dueDateEnable.UseVisualStyleBackColor = true;
            this.chk_dueDateEnable.CheckedChanged += new System.EventHandler(this.chk_dueDateEnable_CheckedChanged);
            // 
            // lbl_startDate
            // 
            this.lbl_startDate.AutoSize = true;
            this.lbl_startDate.Location = new System.Drawing.Point(12, 48);
            this.lbl_startDate.Name = "lbl_startDate";
            this.lbl_startDate.Size = new System.Drawing.Size(60, 13);
            this.lbl_startDate.TabIndex = 1;
            this.lbl_startDate.Text = "Start Date";
            // 
            // dtp_dueDateStart
            // 
            this.dtp_dueDateStart.Enabled = false;
            this.dtp_dueDateStart.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtp_dueDateStart.Location = new System.Drawing.Point(80, 44);
            this.dtp_dueDateStart.Name = "dtp_dueDateStart";
            this.dtp_dueDateStart.Size = new System.Drawing.Size(105, 20);
            this.dtp_dueDateStart.TabIndex = 2;
            // 
            // lbl_endDate
            // 
            this.lbl_endDate.AutoSize = true;
            this.lbl_endDate.Location = new System.Drawing.Point(12, 76);
            this.lbl_endDate.Name = "lbl_endDate";
            this.lbl_endDate.Size = new System.Drawing.Size(48, 13);
            this.lbl_endDate.TabIndex = 3;
            this.lbl_endDate.Text = "End Date";
            // 
            // dtp_dueDateEnd
            // 
            this.dtp_dueDateEnd.Enabled = false;
            this.dtp_dueDateEnd.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtp_dueDateEnd.Location = new System.Drawing.Point(80, 72);
            this.dtp_dueDateEnd.Name = "dtp_dueDateEnd";
            this.dtp_dueDateEnd.Size = new System.Drawing.Size(105, 20);
            this.dtp_dueDateEnd.TabIndex = 4;
            // 
            // gb_filter
            // 
            this.gb_filter.Controls.Add(this.lbl_selectBy);
            this.gb_filter.Controls.Add(this.cb_rangeField);
            this.gb_filter.Controls.Add(this.lbl_rangeStart);
            this.gb_filter.Controls.Add(this.txt_rangeStart);
            this.gb_filter.Controls.Add(this.lbl_rangeEnd);
            this.gb_filter.Controls.Add(this.txt_rangeEnd);
            this.gb_filter.Location = new System.Drawing.Point(374, 46);
            this.gb_filter.Name = "gb_filter";
            this.gb_filter.Size = new System.Drawing.Size(200, 152);
            this.gb_filter.TabIndex = 7;
            this.gb_filter.TabStop = false;
            this.gb_filter.Text = "Filter";
            // 
            // lbl_selectBy
            // 
            this.lbl_selectBy.AutoSize = true;
            this.lbl_selectBy.Location = new System.Drawing.Point(12, 24);
            this.lbl_selectBy.Name = "lbl_selectBy";
            this.lbl_selectBy.Size = new System.Drawing.Size(54, 13);
            this.lbl_selectBy.TabIndex = 0;
            this.lbl_selectBy.Text = "Select By";
            // 
            // cb_rangeField
            // 
            this.cb_rangeField.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cb_rangeField.FormattingEnabled = true;
            this.cb_rangeField.Items.AddRange(new object[] {
            "Order Number",
            "Customer",
            "Part"});
            this.cb_rangeField.Location = new System.Drawing.Point(80, 20);
            this.cb_rangeField.Name = "cb_rangeField";
            this.cb_rangeField.Size = new System.Drawing.Size(105, 21);
            this.cb_rangeField.TabIndex = 1;
            // 
            // lbl_rangeStart
            // 
            this.lbl_rangeStart.AutoSize = true;
            this.lbl_rangeStart.Location = new System.Drawing.Point(12, 52);
            this.lbl_rangeStart.Name = "lbl_rangeStart";
            this.lbl_rangeStart.Size = new System.Drawing.Size(30, 13);
            this.lbl_rangeStart.TabIndex = 2;
            this.lbl_rangeStart.Text = "Start";
            // 
            // txt_rangeStart
            // 
            this.txt_rangeStart.Location = new System.Drawing.Point(80, 48);
            this.txt_rangeStart.Name = "txt_rangeStart";
            this.txt_rangeStart.Size = new System.Drawing.Size(105, 20);
            this.txt_rangeStart.TabIndex = 3;
            // 
            // lbl_rangeEnd
            // 
            this.lbl_rangeEnd.AutoSize = true;
            this.lbl_rangeEnd.Location = new System.Drawing.Point(12, 80);
            this.lbl_rangeEnd.Name = "lbl_rangeEnd";
            this.lbl_rangeEnd.Size = new System.Drawing.Size(18, 13);
            this.lbl_rangeEnd.TabIndex = 4;
            this.lbl_rangeEnd.Text = "End";
            // 
            // txt_rangeEnd
            // 
            this.txt_rangeEnd.Location = new System.Drawing.Point(80, 76);
            this.txt_rangeEnd.Name = "txt_rangeEnd";
            this.txt_rangeEnd.Size = new System.Drawing.Size(105, 20);
            this.txt_rangeEnd.TabIndex = 5;
            // 
            // gb_display
            // 
            this.gb_display.Controls.Add(this.chk_statusPending);
            this.gb_display.Controls.Add(this.chk_statusSourcing);
            this.gb_display.Controls.Add(this.chk_statusWaitingApproval);
            this.gb_display.Controls.Add(this.chk_statusReadyToOrder);
            this.gb_display.Controls.Add(this.chk_statusException);
            this.gb_display.Controls.Add(this.chk_showTestRequests);
            this.gb_display.Location = new System.Drawing.Point(580, 46);
            this.gb_display.Name = "gb_display";
            this.gb_display.Size = new System.Drawing.Size(170, 152);
            this.gb_display.TabIndex = 8;
            this.gb_display.TabStop = false;
            this.gb_display.Text = "Display";
            // 
            // chk_statusPending
            // 
            this.chk_statusPending.AutoSize = true;
            this.chk_statusPending.Checked = true;
            this.chk_statusPending.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chk_statusPending.Location = new System.Drawing.Point(12, 20);
            this.chk_statusPending.Name = "chk_statusPending";
            this.chk_statusPending.Size = new System.Drawing.Size(66, 17);
            this.chk_statusPending.TabIndex = 0;
            this.chk_statusPending.Text = "Pending";
            this.chk_statusPending.UseVisualStyleBackColor = true;
            this.chk_statusPending.CheckedChanged += new System.EventHandler(this.chk_displayFilter_CheckedChanged);
            // 
            // chk_statusSourcing
            // 
            this.chk_statusSourcing.AutoSize = true;
            this.chk_statusSourcing.Checked = true;
            this.chk_statusSourcing.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chk_statusSourcing.Location = new System.Drawing.Point(12, 41);
            this.chk_statusSourcing.Name = "chk_statusSourcing";
            this.chk_statusSourcing.Size = new System.Drawing.Size(72, 17);
            this.chk_statusSourcing.TabIndex = 1;
            this.chk_statusSourcing.Text = "Sourcing";
            this.chk_statusSourcing.UseVisualStyleBackColor = true;
            this.chk_statusSourcing.CheckedChanged += new System.EventHandler(this.chk_displayFilter_CheckedChanged);
            // 
            // chk_statusWaitingApproval
            // 
            this.chk_statusWaitingApproval.AutoSize = true;
            this.chk_statusWaitingApproval.Checked = true;
            this.chk_statusWaitingApproval.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chk_statusWaitingApproval.Location = new System.Drawing.Point(12, 62);
            this.chk_statusWaitingApproval.Name = "chk_statusWaitingApproval";
            this.chk_statusWaitingApproval.Size = new System.Drawing.Size(114, 17);
            this.chk_statusWaitingApproval.TabIndex = 2;
            this.chk_statusWaitingApproval.Text = "WaitingApproval";
            this.chk_statusWaitingApproval.UseVisualStyleBackColor = true;
            this.chk_statusWaitingApproval.CheckedChanged += new System.EventHandler(this.chk_displayFilter_CheckedChanged);
            // 
            // chk_statusReadyToOrder
            // 
            this.chk_statusReadyToOrder.AutoSize = true;
            this.chk_statusReadyToOrder.Checked = true;
            this.chk_statusReadyToOrder.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chk_statusReadyToOrder.Location = new System.Drawing.Point(12, 83);
            this.chk_statusReadyToOrder.Name = "chk_statusReadyToOrder";
            this.chk_statusReadyToOrder.Size = new System.Drawing.Size(96, 17);
            this.chk_statusReadyToOrder.TabIndex = 3;
            this.chk_statusReadyToOrder.Text = "ReadyToOrder";
            this.chk_statusReadyToOrder.UseVisualStyleBackColor = true;
            this.chk_statusReadyToOrder.CheckedChanged += new System.EventHandler(this.chk_displayFilter_CheckedChanged);
            // 
            // chk_statusException
            // 
            this.chk_statusException.AutoSize = true;
            this.chk_statusException.Checked = true;
            this.chk_statusException.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chk_statusException.Location = new System.Drawing.Point(12, 104);
            this.chk_statusException.Name = "chk_statusException";
            this.chk_statusException.Size = new System.Drawing.Size(78, 17);
            this.chk_statusException.TabIndex = 4;
            this.chk_statusException.Text = "Exception";
            this.chk_statusException.UseVisualStyleBackColor = true;
            this.chk_statusException.CheckedChanged += new System.EventHandler(this.chk_displayFilter_CheckedChanged);
            // 
            // chk_showTestRequests
            // 
            this.chk_showTestRequests.AutoSize = true;
            this.chk_showTestRequests.Location = new System.Drawing.Point(12, 128);
            this.chk_showTestRequests.Name = "chk_showTestRequests";
            this.chk_showTestRequests.Size = new System.Drawing.Size(132, 17);
            this.chk_showTestRequests.TabIndex = 5;
            this.chk_showTestRequests.Text = "Show Test Requests";
            this.chk_showTestRequests.UseVisualStyleBackColor = true;
            this.chk_showTestRequests.CheckedChanged += new System.EventHandler(this.chk_displayFilter_CheckedChanged);
            // 
            // dgv_requests
            // 
            this.dgv_requests.AllowUserToAddRows = false;
            this.dgv_requests.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_requests.BackgroundColor = System.Drawing.Color.White;
            this.dgv_requests.Location = new System.Drawing.Point(12, 206);
            this.dgv_requests.Name = "dgv_requests";
            this.dgv_requests.ReadOnly = true;
            this.dgv_requests.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dgv_requests.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.RowHeaderSelect;
            this.dgv_requests.Size = new System.Drawing.Size(1260, 371);
            this.dgv_requests.TabIndex = 9;
            this.dgv_requests.SelectionChanged += new System.EventHandler(this.dgv_requests_SelectionChanged);
            // 
            // lbl_offers
            // 
            this.lbl_offers.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lbl_offers.AutoSize = true;
            this.lbl_offers.Location = new System.Drawing.Point(12, 584);
            this.lbl_offers.Name = "lbl_offers";
            this.lbl_offers.Size = new System.Drawing.Size(468, 13);
            this.lbl_offers.TabIndex = 10;
            this.lbl_offers.Text = "Offers for the selected request (tick \"Select\" to select/deselect an offer):";
            // 
            // dgv_offers
            // 
            this.dgv_offers.AllowUserToAddRows = false;
            this.dgv_offers.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgv_offers.BackgroundColor = System.Drawing.Color.White;
            this.dgv_offers.Location = new System.Drawing.Point(12, 601);
            this.dgv_offers.MultiSelect = false;
            this.dgv_offers.Name = "dgv_offers";
            this.dgv_offers.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dgv_offers.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.RowHeaderSelect;
            this.dgv_offers.Size = new System.Drawing.Size(1260, 190);
            this.dgv_offers.TabIndex = 11;
            this.dgv_offers.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgv_offers_CellValueChanged);
            this.dgv_offers.CurrentCellDirtyStateChanged += new System.EventHandler(this.dgv_offers_CurrentCellDirtyStateChanged);
            // 
            // lbl_status
            // 
            this.lbl_status.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lbl_status.Location = new System.Drawing.Point(0, 797);
            this.lbl_status.Name = "lbl_status";
            this.lbl_status.Padding = new System.Windows.Forms.Padding(4);
            this.lbl_status.Size = new System.Drawing.Size(1284, 24);
            this.lbl_status.TabIndex = 12;
            this.lbl_status.Text = "Ready. Click \"Query\" to fetch orders from MAX.";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1284, 821);
            this.Controls.Add(this.lbl_status);
            this.Controls.Add(this.dgv_offers);
            this.Controls.Add(this.lbl_offers);
            this.Controls.Add(this.dgv_requests);
            this.Controls.Add(this.gb_display);
            this.Controls.Add(this.gb_filter);
            this.Controls.Add(this.gb_dueDateRange);
            this.Controls.Add(this.gb_query);
            this.Controls.Add(this.bt_processConfirmations);
            this.Controls.Add(this.bt_placeOrders);
            this.Controls.Add(this.bt_sourceAll);
            this.Controls.Add(this.bt_startSourcing);
            this.Controls.Add(this.bt_newRequest);
            this.MinimumSize = new System.Drawing.Size(800, 500);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Purchase Requests";
            this.gb_query.ResumeLayout(false);
            this.gb_query.PerformLayout();
            this.gb_dueDateRange.ResumeLayout(false);
            this.gb_dueDateRange.PerformLayout();
            this.gb_filter.ResumeLayout(false);
            this.gb_filter.PerformLayout();
            this.gb_display.ResumeLayout(false);
            this.gb_display.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_requests)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_offers)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button bt_newRequest;
        private System.Windows.Forms.Button bt_startSourcing;
        private System.Windows.Forms.Button bt_sourceAll;
        private System.Windows.Forms.Button bt_placeOrders;
        private System.Windows.Forms.Button bt_processConfirmations;
        private Procurement.UI.Support.ClassicStyle.ClassicGroupBox gb_query;
        private System.Windows.Forms.CheckBox chk_statusPlanned;
        private System.Windows.Forms.CheckBox chk_statusApproved;
        private System.Windows.Forms.Button bt_query;
        private Procurement.UI.Support.ClassicStyle.ClassicGroupBox gb_dueDateRange;
        private System.Windows.Forms.CheckBox chk_dueDateEnable;
        private System.Windows.Forms.Label lbl_startDate;
        private System.Windows.Forms.DateTimePicker dtp_dueDateStart;
        private System.Windows.Forms.Label lbl_endDate;
        private System.Windows.Forms.DateTimePicker dtp_dueDateEnd;
        private Procurement.UI.Support.ClassicStyle.ClassicGroupBox gb_filter;
        private System.Windows.Forms.Label lbl_selectBy;
        private System.Windows.Forms.ComboBox cb_rangeField;
        private System.Windows.Forms.Label lbl_rangeStart;
        private System.Windows.Forms.TextBox txt_rangeStart;
        private System.Windows.Forms.Label lbl_rangeEnd;
        private System.Windows.Forms.TextBox txt_rangeEnd;
        private Procurement.UI.Support.ClassicStyle.ClassicGroupBox gb_display;
        private System.Windows.Forms.CheckBox chk_statusPending;
        private System.Windows.Forms.CheckBox chk_statusSourcing;
        private System.Windows.Forms.CheckBox chk_statusWaitingApproval;
        private System.Windows.Forms.CheckBox chk_statusReadyToOrder;
        private System.Windows.Forms.CheckBox chk_statusException;
        private System.Windows.Forms.CheckBox chk_showTestRequests;
        private System.Windows.Forms.DataGridView dgv_requests;
        private System.Windows.Forms.Label lbl_offers;
        private System.Windows.Forms.DataGridView dgv_offers;
        private System.Windows.Forms.Label lbl_status;
    }
}
