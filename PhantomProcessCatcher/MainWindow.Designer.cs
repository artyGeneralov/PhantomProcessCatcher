namespace PhantomProcessCatcher
{
    partial class MainWindow
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
            this.btnStop = new System.Windows.Forms.Button();
            this.btnStart = new System.Windows.Forms.Button();
            this.gridProcs = new System.Windows.Forms.DataGridView();
            this.lstDlls = new System.Windows.Forms.ListBox();
            ((System.ComponentModel.ISupportInitialize)(this.gridProcs)).BeginInit();
            this.SuspendLayout();
            // 
            // btnStop
            // 
            this.btnStop.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnStop.Location = new System.Drawing.Point(909, 26);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(110, 32);
            this.btnStop.TabIndex = 3;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // btnStart
            // 
            this.btnStart.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnStart.Location = new System.Drawing.Point(782, 24);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(110, 34);
            this.btnStart.TabIndex = 2;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // gridProcs
            // 
            this.gridProcs.AllowUserToAddRows = false;
            this.gridProcs.AllowUserToDeleteRows = false;
            this.gridProcs.AllowUserToResizeRows = false;
            this.gridProcs.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gridProcs.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridProcs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridProcs.Location = new System.Drawing.Point(24, 24);
            this.gridProcs.Margin = new System.Windows.Forms.Padding(15, 15, 80, 30);
            this.gridProcs.MultiSelect = false;
            this.gridProcs.Name = "gridProcs";
            this.gridProcs.ReadOnly = true;
            this.gridProcs.RowHeadersVisible = false;
            this.gridProcs.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridProcs.Size = new System.Drawing.Size(728, 490);
            this.gridProcs.TabIndex = 1;
            this.gridProcs.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridProcs_CellContentClick);
            // 
            // lstDlls
            // 
            this.lstDlls.FormattingEnabled = true;
            this.lstDlls.Location = new System.Drawing.Point(818, 158);
            this.lstDlls.Name = "lstDlls";
            this.lstDlls.Size = new System.Drawing.Size(347, 290);
            this.lstDlls.TabIndex = 4;
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1291, 617);
            this.Controls.Add(this.lstDlls);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.gridProcs);
            this.Name = "MainWindow";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.gridProcs)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.DataGridView gridProcs;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.ListBox lstDlls;
    }
}

