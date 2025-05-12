namespace BaiTapLopLapTrinhMang
{
	partial class ClientForm
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
			label1 = new Label();
			serverIpAddressTbx = new TextBox();
			ConnectBtn = new Button();
			backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
			errorProvider1 = new ErrorProvider(components);
			label2 = new Label();
			serverPortNud = new NumericUpDown();
			thongBaoRtbx = new RichTextBox();
			label3 = new Label();
			tenMayTbx = new TextBox();
			((System.ComponentModel.ISupportInitialize)errorProvider1).BeginInit();
			((System.ComponentModel.ISupportInitialize)serverPortNud).BeginInit();
			SuspendLayout();
			// 
			// label1
			// 
			label1.AutoSize = true;
			label1.Location = new Point(24, 25);
			label1.Name = "label1";
			label1.Size = new Size(122, 20);
			label1.TabIndex = 0;
			label1.Text = "Server ip address";
			// 
			// serverIpAddressTbx
			// 
			serverIpAddressTbx.Location = new Point(166, 19);
			serverIpAddressTbx.Name = "serverIpAddressTbx";
			serverIpAddressTbx.Size = new Size(329, 27);
			serverIpAddressTbx.TabIndex = 1;
			// 
			// ConnectBtn
			// 
			ConnectBtn.Location = new Point(24, 120);
			ConnectBtn.Name = "ConnectBtn";
			ConnectBtn.Size = new Size(94, 29);
			ConnectBtn.TabIndex = 2;
			ConnectBtn.Text = "Connect";
			ConnectBtn.UseVisualStyleBackColor = true;
			ConnectBtn.Click += ConnectBtn_Click;
			// 
			// backgroundWorker1
			// 
			backgroundWorker1.WorkerReportsProgress = true;
			backgroundWorker1.WorkerSupportsCancellation = true;
			// 
			// errorProvider1
			// 
			errorProvider1.ContainerControl = this;
			// 
			// label2
			// 
			label2.AutoSize = true;
			label2.Location = new Point(24, 68);
			label2.Name = "label2";
			label2.Size = new Size(35, 20);
			label2.TabIndex = 3;
			label2.Text = "Port";
			// 
			// serverPortNud
			// 
			serverPortNud.Location = new Point(127, 68);
			serverPortNud.Margin = new Padding(3, 4, 3, 4);
			serverPortNud.Maximum = new decimal(new int[] { 65125, 0, 0, 0 });
			serverPortNud.Minimum = new decimal(new int[] { 1024, 0, 0, 0 });
			serverPortNud.Name = "serverPortNud";
			serverPortNud.Size = new Size(137, 27);
			serverPortNud.TabIndex = 4;
			serverPortNud.Value = new decimal(new int[] { 1024, 0, 0, 0 });
			// 
			// thongBaoRtbx
			// 
			thongBaoRtbx.Location = new Point(12, 155);
			thongBaoRtbx.Name = "thongBaoRtbx";
			thongBaoRtbx.Size = new Size(670, 227);
			thongBaoRtbx.TabIndex = 5;
			thongBaoRtbx.Text = "";
			// 
			// label3
			// 
			label3.AutoSize = true;
			label3.Location = new Point(339, 75);
			label3.Name = "label3";
			label3.Size = new Size(64, 20);
			label3.TabIndex = 6;
			label3.Text = "Tên máy";
			// 
			// tenMayTbx
			// 
			tenMayTbx.Location = new Point(435, 72);
			tenMayTbx.Name = "tenMayTbx";
			tenMayTbx.Size = new Size(160, 27);
			tenMayTbx.TabIndex = 7;
			// 
			// ClientForm
			// 
			AutoScaleDimensions = new SizeF(8F, 20F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(691, 394);
			Controls.Add(tenMayTbx);
			Controls.Add(label3);
			Controls.Add(thongBaoRtbx);
			Controls.Add(serverPortNud);
			Controls.Add(label2);
			Controls.Add(ConnectBtn);
			Controls.Add(serverIpAddressTbx);
			Controls.Add(label1);
			Name = "ClientForm";
			Text = "ClientForm";
			((System.ComponentModel.ISupportInitialize)errorProvider1).EndInit();
			((System.ComponentModel.ISupportInitialize)serverPortNud).EndInit();
			ResumeLayout(false);
			PerformLayout();
		}

		#endregion

		private Label label1;
		private TextBox serverIpAddressTbx;
		private Button ConnectBtn;
		private System.ComponentModel.BackgroundWorker backgroundWorker1;
		private ErrorProvider errorProvider1;
		private NumericUpDown serverPortNud;
		private Label label2;
		private RichTextBox thongBaoRtbx;
		private TextBox tenMayTbx;
		private Label label3;
	}
}