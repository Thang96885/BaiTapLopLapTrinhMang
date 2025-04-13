using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BaiTapLopLapTrinhMang
{
	public partial class TypeOptionForm : Form
	{
		public TypeOptionForm()
		{
			InitializeComponent();
		}

		private void XacNhanBtn_Click(object sender, EventArgs e)
		{
			if(serverRbtn.Checked)
			{
				var serverFrom = new ServerForm();
				serverFrom.ShowDialog();
			}
			if(clientRBtn.Checked)
			{
				var clientFrom = new ClientForm();
				clientFrom.ShowDialog();
			}
		}
	}
}
