namespace Bambulanci
{
	partial class Form1
	{
		/// <summary>
		///  Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		///  Clean up any resources being used.
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
		///  Required method for Designer support - do not modify
		///  the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.bCreateGame = new System.Windows.Forms.Button();
			this.bConnect = new System.Windows.Forms.Button();
			this.listBox12 = new System.Windows.Forms.ListBox();
			this.numericUpDown1 = new System.Windows.Forms.NumericUpDown();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).BeginInit();
			this.SuspendLayout();
			// 
			// bCreateGame
			// 
			this.bCreateGame.Location = new System.Drawing.Point(316, 109);
			this.bCreateGame.Name = "bCreateGame";
			this.bCreateGame.Size = new System.Drawing.Size(167, 46);
			this.bCreateGame.TabIndex = 0;
			this.bCreateGame.Text = "Vytvořit hru";
			this.bCreateGame.UseVisualStyleBackColor = true;
			this.bCreateGame.Click += new System.EventHandler(this.bCreateGame_Click);
			// 
			// bConnect
			// 
			this.bConnect.Location = new System.Drawing.Point(316, 191);
			this.bConnect.Name = "bConnect";
			this.bConnect.Size = new System.Drawing.Size(167, 49);
			this.bConnect.TabIndex = 1;
			this.bConnect.Text = "Připojit se";
			this.bConnect.UseVisualStyleBackColor = true;
			// 
			// listBox12
			// 
			this.listBox12.FormattingEnabled = true;
			this.listBox12.ItemHeight = 15;
			this.listBox12.Items.AddRange(new object[] {
            "1 hrac",
            "2 hraci",
            "3 hraci",
            "4 hraci"});
			this.listBox12.Location = new System.Drawing.Point(546, 12);
			this.listBox12.Name = "listBox12";
			this.listBox12.Size = new System.Drawing.Size(120, 94);
			this.listBox12.TabIndex = 2;
			// 
			// numericUpDown1
			// 
			this.numericUpDown1.Location = new System.Drawing.Point(546, 112);
			this.numericUpDown1.Maximum = new decimal(new int[] {
            4,
            0,
            0,
            0});
			this.numericUpDown1.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
			this.numericUpDown1.Name = "numericUpDown1";
			this.numericUpDown1.Size = new System.Drawing.Size(120, 23);
			this.numericUpDown1.TabIndex = 3;
			this.numericUpDown1.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(800, 450);
			this.Controls.Add(this.numericUpDown1);
			this.Controls.Add(this.listBox12);
			this.Controls.Add(this.bConnect);
			this.Controls.Add(this.bCreateGame);
			this.Name = "Form1";
			this.Text = "Form1";
			((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Button bCreateGame;
		private System.Windows.Forms.Button bConnect;
		private System.Windows.Forms.ListBox listBox12;
		private System.Windows.Forms.NumericUpDown numericUpDown1;
	}
}

