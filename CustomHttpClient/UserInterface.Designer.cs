namespace CustomHttpClient
{
    partial class UserInterface
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
            this.txtLogBox = new System.Windows.Forms.TextBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.txtBoxUrl = new System.Windows.Forms.TextBox();
            this.lblUrl = new System.Windows.Forms.Label();
            this.grpRequestParams = new System.Windows.Forms.GroupBox();
            this.btnStop = new System.Windows.Forms.Button();
            this.txtBoxPort = new System.Windows.Forms.TextBox();
            this.lblPort = new System.Windows.Forms.Label();
            this.listViewServer = new System.Windows.Forms.ListView();
            this.colHeaderTitle = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colHeaderHref = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colHeaderSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewOutbound = new System.Windows.Forms.ListView();
            this.colRemoteTitle = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colRemoteHref = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colRemoteSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.serverGroup = new System.Windows.Forms.GroupBox();
            this.lblServer = new System.Windows.Forms.Label();
            this.outBoundGroup = new System.Windows.Forms.GroupBox();
            this.lblOutbound = new System.Windows.Forms.Label();
            this.txtQueue = new System.Windows.Forms.TextBox();
            this.grpRequestParams.SuspendLayout();
            this.serverGroup.SuspendLayout();
            this.outBoundGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtLogBox
            // 
            this.txtLogBox.Location = new System.Drawing.Point(12, 504);
            this.txtLogBox.Multiline = true;
            this.txtLogBox.Name = "txtLogBox";
            this.txtLogBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLogBox.Size = new System.Drawing.Size(198, 141);
            this.txtLogBox.TabIndex = 4;
            // 
            // btnStart
            // 
            this.btnStart.Enabled = false;
            this.btnStart.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnStart.Location = new System.Drawing.Point(9, 104);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(65, 25);
            this.btnStart.TabIndex = 4;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // txtBoxUrl
            // 
            this.txtBoxUrl.Location = new System.Drawing.Point(50, 20);
            this.txtBoxUrl.Name = "txtBoxUrl";
            this.txtBoxUrl.Size = new System.Drawing.Size(99, 20);
            this.txtBoxUrl.TabIndex = 0;
            this.txtBoxUrl.Text = "tass.ru";
            this.txtBoxUrl.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txtBoxUrl.TextChanged += new System.EventHandler(this.txtBoxUrl_TextChanged);
            // 
            // lblUrl
            // 
            this.lblUrl.AutoSize = true;
            this.lblUrl.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblUrl.Location = new System.Drawing.Point(6, 23);
            this.lblUrl.Name = "lblUrl";
            this.lblUrl.Size = new System.Drawing.Size(38, 13);
            this.lblUrl.TabIndex = 3;
            this.lblUrl.Text = "Адрес";
            // 
            // grpRequestParams
            // 
            this.grpRequestParams.Controls.Add(this.btnStop);
            this.grpRequestParams.Controls.Add(this.txtBoxPort);
            this.grpRequestParams.Controls.Add(this.lblPort);
            this.grpRequestParams.Controls.Add(this.txtBoxUrl);
            this.grpRequestParams.Controls.Add(this.btnStart);
            this.grpRequestParams.Controls.Add(this.lblUrl);
            this.grpRequestParams.ForeColor = System.Drawing.SystemColors.Highlight;
            this.grpRequestParams.Location = new System.Drawing.Point(559, 504);
            this.grpRequestParams.Name = "grpRequestParams";
            this.grpRequestParams.Size = new System.Drawing.Size(168, 141);
            this.grpRequestParams.TabIndex = 5;
            this.grpRequestParams.TabStop = false;
            this.grpRequestParams.Text = "Параметры запроса";
            // 
            // btnStop
            // 
            this.btnStop.Enabled = false;
            this.btnStop.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnStop.Location = new System.Drawing.Point(84, 104);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(65, 25);
            this.btnStop.TabIndex = 7;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // txtBoxPort
            // 
            this.txtBoxPort.Location = new System.Drawing.Point(50, 48);
            this.txtBoxPort.Name = "txtBoxPort";
            this.txtBoxPort.Size = new System.Drawing.Size(99, 20);
            this.txtBoxPort.TabIndex = 1;
            this.txtBoxPort.Text = "80";
            this.txtBoxPort.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txtBoxPort.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtBoxPort_KeyPress);
            // 
            // lblPort
            // 
            this.lblPort.AutoSize = true;
            this.lblPort.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblPort.Location = new System.Drawing.Point(6, 51);
            this.lblPort.Name = "lblPort";
            this.lblPort.Size = new System.Drawing.Size(32, 13);
            this.lblPort.TabIndex = 6;
            this.lblPort.Text = "Порт";
            // 
            // listViewServer
            // 
            this.listViewServer.AutoArrange = false;
            this.listViewServer.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colHeaderTitle,
            this.colHeaderHref,
            this.colHeaderSize});
            this.listViewServer.FullRowSelect = true;
            this.listViewServer.GridLines = true;
            this.listViewServer.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listViewServer.Location = new System.Drawing.Point(6, 19);
            this.listViewServer.MultiSelect = false;
            this.listViewServer.Name = "listViewServer";
            this.listViewServer.Size = new System.Drawing.Size(715, 215);
            this.listViewServer.TabIndex = 6;
            this.listViewServer.UseCompatibleStateImageBehavior = false;
            this.listViewServer.View = System.Windows.Forms.View.Details;
            this.listViewServer.DoubleClick += new System.EventHandler(this.listViewServer_DoubleClick);
            // 
            // colHeaderTitle
            // 
            this.colHeaderTitle.Text = "Название";
            this.colHeaderTitle.Width = 140;
            // 
            // colHeaderHref
            // 
            this.colHeaderHref.Text = "Ссылка";
            this.colHeaderHref.Width = 480;
            // 
            // colHeaderSize
            // 
            this.colHeaderSize.Text = "Вес";
            this.colHeaderSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.colHeaderSize.Width = 70;
            // 
            // listViewOutbound
            // 
            this.listViewOutbound.AutoArrange = false;
            this.listViewOutbound.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colRemoteTitle,
            this.colRemoteHref,
            this.colRemoteSize});
            this.listViewOutbound.FullRowSelect = true;
            this.listViewOutbound.GridLines = true;
            this.listViewOutbound.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listViewOutbound.Location = new System.Drawing.Point(6, 19);
            this.listViewOutbound.MultiSelect = false;
            this.listViewOutbound.Name = "listViewOutbound";
            this.listViewOutbound.Size = new System.Drawing.Size(715, 215);
            this.listViewOutbound.TabIndex = 7;
            this.listViewOutbound.UseCompatibleStateImageBehavior = false;
            this.listViewOutbound.View = System.Windows.Forms.View.Details;
            this.listViewOutbound.DoubleClick += new System.EventHandler(this.listViewOutbound_DoubleClick);
            // 
            // colRemoteTitle
            // 
            this.colRemoteTitle.Text = "Название";
            this.colRemoteTitle.Width = 140;
            // 
            // colRemoteHref
            // 
            this.colRemoteHref.Text = "Ссылка";
            this.colRemoteHref.Width = 480;
            // 
            // colRemoteSize
            // 
            this.colRemoteSize.Text = "Вес";
            this.colRemoteSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.colRemoteSize.Width = 70;
            // 
            // serverGroup
            // 
            this.serverGroup.Controls.Add(this.lblServer);
            this.serverGroup.Controls.Add(this.listViewServer);
            this.serverGroup.ForeColor = System.Drawing.Color.Chocolate;
            this.serverGroup.Location = new System.Drawing.Point(12, 12);
            this.serverGroup.Name = "serverGroup";
            this.serverGroup.Size = new System.Drawing.Size(727, 241);
            this.serverGroup.TabIndex = 9;
            this.serverGroup.TabStop = false;
            this.serverGroup.Text = "Изображения расположенные на сервере";
            // 
            // lblServer
            // 
            this.lblServer.AutoSize = true;
            this.lblServer.Location = new System.Drawing.Point(628, 0);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(13, 13);
            this.lblServer.TabIndex = 8;
            this.lblServer.Text = "0";
            // 
            // outBoundGroup
            // 
            this.outBoundGroup.Controls.Add(this.listViewOutbound);
            this.outBoundGroup.Controls.Add(this.lblOutbound);
            this.outBoundGroup.ForeColor = System.Drawing.Color.Chocolate;
            this.outBoundGroup.Location = new System.Drawing.Point(12, 259);
            this.outBoundGroup.Name = "outBoundGroup";
            this.outBoundGroup.Size = new System.Drawing.Size(727, 241);
            this.outBoundGroup.TabIndex = 10;
            this.outBoundGroup.TabStop = false;
            this.outBoundGroup.Text = "Изображения на других серверах";
            // 
            // lblOutbound
            // 
            this.lblOutbound.AutoSize = true;
            this.lblOutbound.Location = new System.Drawing.Point(628, 0);
            this.lblOutbound.Name = "lblOutbound";
            this.lblOutbound.Size = new System.Drawing.Size(13, 13);
            this.lblOutbound.TabIndex = 7;
            this.lblOutbound.Text = "0";
            // 
            // txtQueue
            // 
            this.txtQueue.Location = new System.Drawing.Point(216, 506);
            this.txtQueue.Multiline = true;
            this.txtQueue.Name = "txtQueue";
            this.txtQueue.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtQueue.Size = new System.Drawing.Size(337, 141);
            this.txtQueue.TabIndex = 11;
            // 
            // UserInterface
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(750, 651);
            this.Controls.Add(this.txtQueue);
            this.Controls.Add(this.outBoundGroup);
            this.Controls.Add(this.serverGroup);
            this.Controls.Add(this.grpRequestParams);
            this.Controls.Add(this.txtLogBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "UserInterface";
            this.Text = "CustomHttpClient";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.UserInterface_FormClosing);
            this.grpRequestParams.ResumeLayout(false);
            this.grpRequestParams.PerformLayout();
            this.serverGroup.ResumeLayout(false);
            this.serverGroup.PerformLayout();
            this.outBoundGroup.ResumeLayout(false);
            this.outBoundGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtLogBox;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.TextBox txtBoxUrl;
        private System.Windows.Forms.Label lblUrl;
        private System.Windows.Forms.GroupBox grpRequestParams;
        private System.Windows.Forms.TextBox txtBoxPort;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.ListView listViewServer;
        private System.Windows.Forms.ColumnHeader colHeaderTitle;
        private System.Windows.Forms.ColumnHeader colHeaderHref;
        private System.Windows.Forms.ColumnHeader colHeaderSize;
        private System.Windows.Forms.ListView listViewOutbound;
        private System.Windows.Forms.ColumnHeader colRemoteTitle;
        private System.Windows.Forms.ColumnHeader colRemoteHref;
        private System.Windows.Forms.ColumnHeader colRemoteSize;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.GroupBox serverGroup;
        private System.Windows.Forms.GroupBox outBoundGroup;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.Label lblOutbound;
        private System.Windows.Forms.TextBox txtQueue;
    }
}

