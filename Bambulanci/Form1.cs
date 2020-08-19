using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Bambulanci
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void disableButton(Button b)
		{
			b.Enabled = false;
			b.Visible = false;
		}

		private void bCreateGame_Click(object sender, EventArgs e)
		{
			disableButton(bCreateGame);
			disableButton(bConnect);

			numericUpDown1.Value
			Console.WriteLine(listBox12.SelectedItem);
		}
	}
}
