namespace BaiTapLopLapTrinhMang
{
	partial class TypeOptionForm
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
			serverRbtn = new RadioButton();
			clientRBtn = new RadioButton();
			label1 = new Label();
			XacNhanBtn = new Button();
			SuspendLayout();
			// 
			// serverRbtn
			// 
			serverRbtn.AutoSize = true;
			serverRbtn.Location = new Point(117, 47);
			serverRbtn.Name = "serverRbtn";
			serverRbtn.Size = new Size(71, 24);
			serverRbtn.TabIndex = 0;
			serverRbtn.TabStop = true;
			serverRbtn.Text = "Server";
			serverRbtn.UseVisualStyleBackColor = true;
			// 
			// clientRBtn
			// 
			clientRBtn.AutoSize = true;
			clientRBtn.Location = new Point(117, 89);
			clientRBtn.Name = "clientRBtn";
			clientRBtn.Size = new Size(68, 24);
			clientRBtn.TabIndex = 1;
			clientRBtn.TabStop = true;
			clientRBtn.Text = "Client";
			clientRBtn.UseVisualStyleBackColor = true;
			// 
			// label1
			// 
			label1.AutoSize = true;
			label1.Location = new Point(103, 9);
			label1.Name = "label1";
			label1.Size = new Size(192, 20);
			label1.TabIndex = 2;
			label1.Text = "Choose your computer type";
			// 
			// XacNhanBtn
			// 
			XacNhanBtn.Location = new Point(117, 149);
			XacNhanBtn.Name = "XacNhanBtn";
			XacNhanBtn.Size = new Size(94, 29);
			XacNhanBtn.TabIndex = 3;
			XacNhanBtn.Text = "Accept";
			XacNhanBtn.UseVisualStyleBackColor = true;
			XacNhanBtn.Click += XacNhanBtn_Click;
			// 
			// TypeOptionForm
			// 
			AutoScaleDimensions = new SizeF(8F, 20F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(379, 263);
			Controls.Add(XacNhanBtn);
			Controls.Add(label1);
			Controls.Add(clientRBtn);
			Controls.Add(serverRbtn);
			Name = "TypeOptionForm";
			Text = "TypeOptionForm";
			ResumeLayout(false);
			PerformLayout();
		}

		#endregion

		private RadioButton serverRbtn;
		private RadioButton clientRBtn;
		private Label label1;
		private Button XacNhanBtn;
	}
}