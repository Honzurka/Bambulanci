namespace Bambulanci
{
	partial class formBambulanci
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
			this.lBNumOfPlayers = new System.Windows.Forms.ListBox();
			this.bCreateGame2 = new System.Windows.Forms.Button();
			this.lWaiting = new System.Windows.Forms.Label();
			this.nListenPort = new System.Windows.Forms.NumericUpDown();
			this.lBServers = new System.Windows.Forms.ListBox();
			this.bLogin = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)(this.nListenPort)).BeginInit();
			this.SuspendLayout();
			// 
			// bCreateGame
			// 
			this.bCreateGame.Enabled = false;
			this.bCreateGame.Location = new System.Drawing.Point(361, 145);
			this.bCreateGame.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.bCreateGame.Name = "bCreateGame";
			this.bCreateGame.Size = new System.Drawing.Size(191, 61);
			this.bCreateGame.TabIndex = 0;
			this.bCreateGame.Text = "Vytvořit hru";
			this.bCreateGame.UseVisualStyleBackColor = true;
			this.bCreateGame.Visible = false;
			this.bCreateGame.Click += new System.EventHandler(this.bCreateGame_Click);
			// 
			// bConnect
			// 
			this.bConnect.Enabled = false;
			this.bConnect.Location = new System.Drawing.Point(361, 255);
			this.bConnect.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.bConnect.Name = "bConnect";
			this.bConnect.Size = new System.Drawing.Size(191, 65);
			this.bConnect.TabIndex = 1;
			this.bConnect.Text = "Připojit se";
			this.bConnect.UseVisualStyleBackColor = true;
			this.bConnect.Visible = false;
			this.bConnect.Click += new System.EventHandler(this.bConnect_Click);
			// 
			// lBNumOfPlayers
			// 
			this.lBNumOfPlayers.Enabled = false;
			this.lBNumOfPlayers.FormattingEnabled = true;
			this.lBNumOfPlayers.ItemHeight = 20;
			this.lBNumOfPlayers.Items.AddRange(new object[] {
            "2 hraci",
            "3 hraci",
            "4 hraci"});
			this.lBNumOfPlayers.Location = new System.Drawing.Point(625, 100);
			this.lBNumOfPlayers.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.lBNumOfPlayers.Name = "lBNumOfPlayers";
			this.lBNumOfPlayers.Size = new System.Drawing.Size(137, 124);
			this.lBNumOfPlayers.TabIndex = 2;
			this.lBNumOfPlayers.Visible = false;
			// 
			// bCreateGame2
			// 
			this.bCreateGame2.Enabled = false;
			this.bCreateGame2.Location = new System.Drawing.Point(640, 245);
			this.bCreateGame2.Name = "bCreateGame2";
			this.bCreateGame2.Size = new System.Drawing.Size(94, 29);
			this.bCreateGame2.TabIndex = 3;
			this.bCreateGame2.Text = "Vytvorit";
			this.bCreateGame2.UseVisualStyleBackColor = true;
			this.bCreateGame2.Visible = false;
			this.bCreateGame2.Click += new System.EventHandler(this.bCreateGame2_Click);
			// 
			// lWaiting
			// 
			this.lWaiting.AutoSize = true;
			this.lWaiting.Enabled = false;
			this.lWaiting.Location = new System.Drawing.Point(361, 68);
			this.lWaiting.Name = "lWaiting";
			this.lWaiting.Size = new System.Drawing.Size(166, 20);
			this.lWaiting.TabIndex = 4;
			this.lWaiting.Text = "Čekám na ostatní hráče.";
			this.lWaiting.Visible = false;
			// 
			// nListenPort
			// 
			this.nListenPort.Enabled = false;
			this.nListenPort.Location = new System.Drawing.Point(768, 145);
			this.nListenPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
			this.nListenPort.Minimum = new decimal(new int[] {
            49152,
            0,
            0,
            0});
			this.nListenPort.Name = "nListenPort";
			this.nListenPort.Size = new System.Drawing.Size(150, 27);
			this.nListenPort.TabIndex = 7;
			this.nListenPort.Value = new decimal(new int[] {
            49152,
            0,
            0,
            0});
			this.nListenPort.Visible = false;
			// 
			// lBServers
			// 
			this.lBServers.Enabled = false;
			this.lBServers.FormattingEnabled = true;
			this.lBServers.ItemHeight = 20;
			this.lBServers.Location = new System.Drawing.Point(27, 255);
			this.lBServers.Name = "lBServers";
			this.lBServers.Size = new System.Drawing.Size(150, 104);
			this.lBServers.TabIndex = 8;
			this.lBServers.Visible = false;
			// 
			// bLogin
			// 
			this.bLogin.Enabled = false;
			this.bLogin.Location = new System.Drawing.Point(82, 380);
			this.bLogin.Name = "bLogin";
			this.bLogin.Size = new System.Drawing.Size(94, 29);
			this.bLogin.TabIndex = 9;
			this.bLogin.Text = "Připojit";
			this.bLogin.UseVisualStyleBackColor = true;
			this.bLogin.Visible = false;
			this.bLogin.Click += new System.EventHandler(this.bLogin_Click);
			// 
			// formBambulanci
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(914, 600);
			this.Controls.Add(this.bLogin);
			this.Controls.Add(this.lBServers);
			this.Controls.Add(this.nListenPort);
			this.Controls.Add(this.lWaiting);
			this.Controls.Add(this.bCreateGame2);
			this.Controls.Add(this.lBNumOfPlayers);
			this.Controls.Add(this.bConnect);
			this.Controls.Add(this.bCreateGame);
			this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.Name = "formBambulanci";
			this.Text = "Bambulanci";
			((System.ComponentModel.ISupportInitialize)(this.nListenPort)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Button bCreateGame;
		private System.Windows.Forms.Button bConnect;
		private System.Windows.Forms.ListBox lBNumOfPlayers;
		private System.Windows.Forms.Button bCreateGame2;
		private System.Windows.Forms.Label lWaiting;
		private System.Windows.Forms.NumericUpDown nListenPort;
		private System.Windows.Forms.ListBox lBServers;
		private System.Windows.Forms.Button bLogin;
	}
}

