namespace BaiTapLopLapTrinhMang
{
	partial class ServerForm
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
			components = new System.ComponentModel.Container();
			groupBox1 = new GroupBox();
			ipAddressCbx = new ComboBox();
			portNup = new NumericUpDown();
			label2 = new Label();
			label1 = new Label();
			ClientInfoDgv = new DataGridView();
			StartStopBtn = new Button();
			backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
			errorProvider1 = new ErrorProvider(components);
			timer1 = new System.Windows.Forms.Timer(components);
			tatMayTinhBtn = new Button();
			button1 = new Button();
			groupBox1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)portNup).BeginInit();
			((System.ComponentModel.ISupportInitialize)ClientInfoDgv).BeginInit();
			((System.ComponentModel.ISupportInitialize)errorProvider1).BeginInit();
			SuspendLayout();
			// 
			// groupBox1
			// 
			groupBox1.Controls.Add(ipAddressCbx);
			groupBox1.Controls.Add(portNup);
			groupBox1.Controls.Add(label2);
			groupBox1.Controls.Add(label1);
			groupBox1.Location = new Point(11, 21);
			groupBox1.Name = "groupBox1";
			groupBox1.Size = new Size(776, 125);
			groupBox1.TabIndex = 0;
			groupBox1.TabStop = false;
			// 
			// ipAddressCbx
			// 
			ipAddressCbx.FormattingEnabled = true;
			ipAddressCbx.Location = new Point(155, 15);
			ipAddressCbx.Name = "ipAddressCbx";
			ipAddressCbx.Size = new Size(193, 28);
			ipAddressCbx.TabIndex = 4;
			// 
			// portNup
			// 
			portNup.Location = new Point(155, 62);
			portNup.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
			portNup.Minimum = new decimal(new int[] { 1024, 0, 0, 0 });
			portNup.Name = "portNup";
			portNup.Size = new Size(193, 27);
			portNup.TabIndex = 3;
			portNup.Value = new decimal(new int[] { 1024, 0, 0, 0 });
			// 
			// label2
			// 
			label2.AutoSize = true;
			label2.Location = new Point(18, 69);
			label2.Name = "label2";
			label2.Size = new Size(35, 20);
			label2.TabIndex = 2;
			label2.Text = "Port";
			// 
			// label1
			// 
			label1.AutoSize = true;
			label1.Location = new Point(18, 23);
			label1.Name = "label1";
			label1.Size = new Size(77, 20);
			label1.TabIndex = 0;
			label1.Text = "Ip address";
			// 
			// ClientInfoDgv
			// 
			ClientInfoDgv.AllowUserToAddRows = false;
			ClientInfoDgv.AllowUserToDeleteRows = false;
			ClientInfoDgv.AllowUserToResizeColumns = false;
			ClientInfoDgv.AllowUserToResizeRows = false;
			ClientInfoDgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			ClientInfoDgv.Location = new Point(11, 199);
			ClientInfoDgv.Name = "ClientInfoDgv";
			ClientInfoDgv.RowHeadersWidth = 51;
			ClientInfoDgv.Size = new Size(553, 239);
			ClientInfoDgv.TabIndex = 1;
			// 
			// StartStopBtn
			// 
			StartStopBtn.BackColor = SystemColors.ActiveCaption;
			StartStopBtn.Location = new Point(12, 152);
			StartStopBtn.Name = "StartStopBtn";
			StartStopBtn.Size = new Size(98, 29);
			StartStopBtn.TabIndex = 2;
			StartStopBtn.Text = "Start";
			StartStopBtn.UseVisualStyleBackColor = false;
			StartStopBtn.Click += StartStopBtn_Click;
			// 
			// errorProvider1
			// 
			errorProvider1.ContainerControl = this;
			// 
			// timer1
			// 
			timer1.Enabled = true;
			timer1.Interval = 1000;
			timer1.Tick += KeepAliveTimer_Tick;
			// 
			// tatMayTinhBtn
			// 
			tatMayTinhBtn.BackColor = SystemColors.ButtonFace;
			tatMayTinhBtn.Location = new Point(601, 248);
			tatMayTinhBtn.Name = "tatMayTinhBtn";
			tatMayTinhBtn.Size = new Size(148, 29);
			tatMayTinhBtn.TabIndex = 6;
			tatMayTinhBtn.Text = "Turn off computer";
			tatMayTinhBtn.UseVisualStyleBackColor = false;
			tatMayTinhBtn.Click += tatMayTinhBtn_Click;
			// 
			// button1
			// 
			button1.Location = new Point(601, 199);
			button1.Name = "button1";
			button1.Size = new Size(148, 29);
			button1.TabIndex = 7;
			button1.Text = "Disconect";
			button1.UseVisualStyleBackColor = true;
			button1.Click += button1_Click;
			// 
			// ServerForm
			// 
			AutoScaleDimensions = new SizeF(8F, 20F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(780, 451);
			Controls.Add(button1);
			Controls.Add(tatMayTinhBtn);
			Controls.Add(StartStopBtn);
			Controls.Add(ClientInfoDgv);
			Controls.Add(groupBox1);
			Name = "ServerForm";
			Text = "ServerForm";
			Load += ServerForm_Load_1;
			groupBox1.ResumeLayout(false);
			groupBox1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)portNup).EndInit();
			((System.ComponentModel.ISupportInitialize)ClientInfoDgv).EndInit();
			((System.ComponentModel.ISupportInitialize)errorProvider1).EndInit();
			ResumeLayout(false);
		}

		#endregion

		private GroupBox groupBox1;
		private Label label1;
		private NumericUpDown portNup;
		private Label label2;
		private ComboBox ipAddressCbx;
		private DataGridView ClientInfoDgv;
		private Button StartStopBtn;
		private System.ComponentModel.BackgroundWorker backgroundWorker1;
		private ErrorProvider errorProvider1;
		private System.Windows.Forms.Timer timer1;
		private Button tatMayTinhBtn;
		private Button button1;
	}
}