﻿namespace Bambulanci
{
	partial class FormBambulanci
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
			this.components = new System.ComponentModel.Container();
			this.bCreateGame = new System.Windows.Forms.Button();
			this.bConnect = new System.Windows.Forms.Button();
			this.lBNumOfPlayers = new System.Windows.Forms.ListBox();
			this.bCreateGame2 = new System.Windows.Forms.Button();
			this.lWaiting = new System.Windows.Forms.Label();
			this.nListenPort = new System.Windows.Forms.NumericUpDown();
			this.lBServers = new System.Windows.Forms.ListBox();
			this.bLogin = new System.Windows.Forms.Button();
			this.nHostPort = new System.Windows.Forms.NumericUpDown();
			this.bRefreshServers = new System.Windows.Forms.Button();
			this.bExit = new System.Windows.Forms.Button();
			this.bCancelHost = new System.Windows.Forms.Button();
			this.bIntro = new System.Windows.Forms.Button();
			this.lWaitingRoom = new System.Windows.Forms.Label();
			this.bStartGame = new System.Windows.Forms.Button();
			this.TimerInGame = new System.Windows.Forms.Timer(this.components);
			this.lScore = new System.Windows.Forms.Label();
			this.lPort = new System.Windows.Forms.Label();
			this.nGameTime = new System.Windows.Forms.NumericUpDown();
			this.lGameTime = new System.Windows.Forms.Label();
			((System.ComponentModel.ISupportInitialize)(this.nListenPort)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.nHostPort)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.nGameTime)).BeginInit();
			this.SuspendLayout();
			// 
			// bCreateGame
			// 
			this.bCreateGame.Enabled = false;
			this.bCreateGame.Location = new System.Drawing.Point(360, 180);
			this.bCreateGame.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.bCreateGame.Name = "bCreateGame";
			this.bCreateGame.Size = new System.Drawing.Size(191, 61);
			this.bCreateGame.TabIndex = 0;
			this.bCreateGame.Text = "Vytvořit hru";
			this.bCreateGame.UseVisualStyleBackColor = true;
			this.bCreateGame.Visible = false;
			this.bCreateGame.Click += new System.EventHandler(this.BCreateGame_Click);
			// 
			// bConnect
			// 
			this.bConnect.Enabled = false;
			this.bConnect.Location = new System.Drawing.Point(360, 260);
			this.bConnect.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.bConnect.Name = "bConnect";
			this.bConnect.Size = new System.Drawing.Size(191, 65);
			this.bConnect.TabIndex = 1;
			this.bConnect.Text = "Připojit se";
			this.bConnect.UseVisualStyleBackColor = true;
			this.bConnect.Visible = false;
			this.bConnect.Click += new System.EventHandler(this.BConnect_Click);
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
			this.lBNumOfPlayers.Location = new System.Drawing.Point(365, 196);
			this.lBNumOfPlayers.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.lBNumOfPlayers.Name = "lBNumOfPlayers";
			this.lBNumOfPlayers.Size = new System.Drawing.Size(137, 104);
			this.lBNumOfPlayers.TabIndex = 2;
			this.lBNumOfPlayers.Visible = false;
			// 
			// bCreateGame2
			// 
			this.bCreateGame2.Enabled = false;
			this.bCreateGame2.Location = new System.Drawing.Point(168, 264);
			this.bCreateGame2.Name = "bCreateGame2";
			this.bCreateGame2.Size = new System.Drawing.Size(150, 29);
			this.bCreateGame2.TabIndex = 3;
			this.bCreateGame2.Text = "Vytvorit";
			this.bCreateGame2.UseVisualStyleBackColor = true;
			this.bCreateGame2.Visible = false;
			this.bCreateGame2.Click += new System.EventHandler(this.BCreateGame2_Click);
			// 
			// lWaiting
			// 
			this.lWaiting.AutoSize = true;
			this.lWaiting.Enabled = false;
			this.lWaiting.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
			this.lWaiting.Location = new System.Drawing.Point(295, 496);
			this.lWaiting.Name = "lWaiting";
			this.lWaiting.Size = new System.Drawing.Size(334, 41);
			this.lWaiting.TabIndex = 4;
			this.lWaiting.Text = "Čekám na ostatní hráče.";
			this.lWaiting.Visible = false;
			// 
			// nListenPort
			// 
			this.nListenPort.Enabled = false;
			this.nListenPort.Location = new System.Drawing.Point(168, 198);
			this.nListenPort.Maximum = new decimal(new int[] {
            59999,
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
			this.lBServers.Location = new System.Drawing.Point(365, 196);
			this.lBServers.Name = "lBServers";
			this.lBServers.Size = new System.Drawing.Size(150, 104);
			this.lBServers.TabIndex = 8;
			this.lBServers.Visible = false;
			// 
			// bLogin
			// 
			this.bLogin.Enabled = false;
			this.bLogin.Location = new System.Drawing.Point(168, 264);
			this.bLogin.Name = "bLogin";
			this.bLogin.Size = new System.Drawing.Size(150, 29);
			this.bLogin.TabIndex = 9;
			this.bLogin.Text = "Připojit";
			this.bLogin.UseVisualStyleBackColor = true;
			this.bLogin.Visible = false;
			this.bLogin.Click += new System.EventHandler(this.BLogin_Click);
			// 
			// nHostPort
			// 
			this.nHostPort.Enabled = false;
			this.nHostPort.Location = new System.Drawing.Point(168, 196);
			this.nHostPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
			this.nHostPort.Minimum = new decimal(new int[] {
            49152,
            0,
            0,
            0});
			this.nHostPort.Name = "nHostPort";
			this.nHostPort.Size = new System.Drawing.Size(150, 27);
			this.nHostPort.TabIndex = 7;
			this.nHostPort.Value = new decimal(new int[] {
            49152,
            0,
            0,
            0});
			this.nHostPort.Visible = false;
			// 
			// bRefreshServers
			// 
			this.bRefreshServers.Enabled = false;
			this.bRefreshServers.Location = new System.Drawing.Point(168, 229);
			this.bRefreshServers.Name = "bRefreshServers";
			this.bRefreshServers.Size = new System.Drawing.Size(150, 29);
			this.bRefreshServers.TabIndex = 10;
			this.bRefreshServers.Text = "Vyhledat servery";
			this.bRefreshServers.UseVisualStyleBackColor = true;
			this.bRefreshServers.Visible = false;
			this.bRefreshServers.Click += new System.EventHandler(this.BRefreshServers_Click);
			// 
			// bExit
			// 
			this.bExit.Enabled = false;
			this.bExit.Location = new System.Drawing.Point(360, 340);
			this.bExit.Name = "bExit";
			this.bExit.Size = new System.Drawing.Size(191, 67);
			this.bExit.TabIndex = 11;
			this.bExit.Text = "Ukončit hru";
			this.bExit.UseVisualStyleBackColor = true;
			this.bExit.Visible = false;
			this.bExit.Click += new System.EventHandler(this.BExit_Click);
			// 
			// bCancelHost
			// 
			this.bCancelHost.Enabled = false;
			this.bCancelHost.Location = new System.Drawing.Point(625, 508);
			this.bCancelHost.Name = "bCancelHost";
			this.bCancelHost.Size = new System.Drawing.Size(94, 29);
			this.bCancelHost.TabIndex = 12;
			this.bCancelHost.Text = "Zpět";
			this.bCancelHost.UseVisualStyleBackColor = true;
			this.bCancelHost.Visible = false;
			this.bCancelHost.Click += new System.EventHandler(this.BCancelHost_Click);
			// 
			// bIntro
			// 
			this.bIntro.Enabled = false;
			this.bIntro.Location = new System.Drawing.Point(625, 509);
			this.bIntro.Name = "bIntro";
			this.bIntro.Size = new System.Drawing.Size(94, 28);
			this.bIntro.TabIndex = 13;
			this.bIntro.Text = "Zpět";
			this.bIntro.UseVisualStyleBackColor = true;
			this.bIntro.Visible = false;
			this.bIntro.Click += new System.EventHandler(this.BIntro_Click);
			// 
			// lWaitingRoom
			// 
			this.lWaitingRoom.AutoSize = true;
			this.lWaitingRoom.Enabled = false;
			this.lWaitingRoom.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
			this.lWaitingRoom.Location = new System.Drawing.Point(390, 40);
			this.lWaitingRoom.Name = "lWaitingRoom";
			this.lWaitingRoom.Size = new System.Drawing.Size(125, 41);
			this.lWaitingRoom.TabIndex = 14;
			this.lWaitingRoom.Text = "Čekárna";
			this.lWaitingRoom.Visible = false;
			// 
			// bStartGame
			// 
			this.bStartGame.Enabled = false;
			this.bStartGame.Location = new System.Drawing.Point(360, 180);
			this.bStartGame.Name = "bStartGame";
			this.bStartGame.Size = new System.Drawing.Size(191, 67);
			this.bStartGame.TabIndex = 15;
			this.bStartGame.Text = "Spustit Hru";
			this.bStartGame.UseVisualStyleBackColor = true;
			this.bStartGame.Visible = false;
			this.bStartGame.Click += new System.EventHandler(this.BStartGame_Click);
			// 
			// TimerInGame
			// 
			this.TimerInGame.Interval = 30;
			this.TimerInGame.Tick += new System.EventHandler(this.TimerInGame_Tick);
			// 
			// lScore
			// 
			this.lScore.AutoSize = true;
			this.lScore.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
			this.lScore.Location = new System.Drawing.Point(310, 81);
			this.lScore.Name = "lScore";
			this.lScore.Size = new System.Drawing.Size(192, 35);
			this.lScore.TabIndex = 16;
			this.lScore.Text = "Endgame score.";
			// 
			// lPort
			// 
			this.lPort.AutoSize = true;
			this.lPort.Location = new System.Drawing.Point(124, 196);
			this.lPort.Name = "lPort";
			this.lPort.Size = new System.Drawing.Size(38, 20);
			this.lPort.TabIndex = 17;
			this.lPort.Text = "Port:";
			// 
			// nGameTime
			// 
			this.nGameTime.Location = new System.Drawing.Point(168, 231);
			this.nGameTime.Maximum = new decimal(new int[] {
            300,
            0,
            0,
            0});
			this.nGameTime.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            0});
			this.nGameTime.Name = "nGameTime";
			this.nGameTime.Size = new System.Drawing.Size(150, 27);
			this.nGameTime.TabIndex = 18;
			this.nGameTime.Value = new decimal(new int[] {
            60,
            0,
            0,
            0});
			// 
			// lGameTime
			// 
			this.lGameTime.AutoSize = true;
			this.lGameTime.Location = new System.Drawing.Point(72, 233);
			this.lGameTime.Name = "lGameTime";
			this.lGameTime.Size = new System.Drawing.Size(90, 20);
			this.lGameTime.TabIndex = 19;
			this.lGameTime.Text = "Délka hry(s):";
			// 
			// FormBambulanci
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(914, 600);
			this.Controls.Add(this.lGameTime);
			this.Controls.Add(this.nGameTime);
			this.Controls.Add(this.lPort);
			this.Controls.Add(this.lScore);
			this.Controls.Add(this.bStartGame);
			this.Controls.Add(this.lWaitingRoom);
			this.Controls.Add(this.bIntro);
			this.Controls.Add(this.bCancelHost);
			this.Controls.Add(this.bExit);
			this.Controls.Add(this.bRefreshServers);
			this.Controls.Add(this.nHostPort);
			this.Controls.Add(this.bLogin);
			this.Controls.Add(this.lBServers);
			this.Controls.Add(this.nListenPort);
			this.Controls.Add(this.lWaiting);
			this.Controls.Add(this.bCreateGame2);
			this.Controls.Add(this.lBNumOfPlayers);
			this.Controls.Add(this.bConnect);
			this.Controls.Add(this.bCreateGame);
			this.DoubleBuffered = true;
			this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.Name = "FormBambulanci";
			this.Text = "Bambulanci";
			this.Paint += new System.Windows.Forms.PaintEventHandler(this.FormBambulanci_Paint);
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FormBambulanci_KeyDown);
			this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.FormBambulanci_KeyUp);
			((System.ComponentModel.ISupportInitialize)(this.nListenPort)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.nHostPort)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.nGameTime)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Button bCreateGame;
		private System.Windows.Forms.Button bConnect;
		private System.Windows.Forms.ListBox lBNumOfPlayers;
		private System.Windows.Forms.Button bCreateGame2;
		private System.Windows.Forms.NumericUpDown nListenPort;
		private System.Windows.Forms.NumericUpDown nHostPort;
		private System.Windows.Forms.Button bRefreshServers;
		private System.Windows.Forms.Button bCancelHost;
		private System.Windows.Forms.Button bIntro;
		public System.Windows.Forms.Label lWaiting;
		private System.Windows.Forms.Label lWaitingRoom;
		public System.Windows.Forms.ListBox lBServers;
		private System.Windows.Forms.Button bStartGame;
		private System.Windows.Forms.Timer TimerInGame;
		public System.Windows.Forms.Button bLogin;
		public System.Windows.Forms.Label lScore;
		private System.Windows.Forms.Label lPort;
		private System.Windows.Forms.NumericUpDown nGameTime;
		private System.Windows.Forms.Label lGameTime;
		public System.Windows.Forms.Button bExit;
	}
}

