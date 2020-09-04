﻿using System;
using System.Drawing;
using System.Net;
using System.Windows.Forms;

namespace Bambulanci
{
	public enum GameState { Intro, HostSelect, HostWaiting, ClientWaiting, HostWaitingRoom, ClientWaitingRoom, ClientSearch, InGame }

	public partial class FormBambulanci : Form
	{
		private GameState currentGameState;
		private readonly Client client;
		private readonly Host host;

		public FormBambulanci()
		{
			InitializeComponent();
			client = new Client(this);
			host = new Host(this);
			ChangeGameState(GameState.Intro);
		}

		private void DisableControl(Control c)
		{
			c.Enabled = false;
			c.Visible = false;
		}

		private void EnableControl(Control c)
		{
			c.Enabled = true;
			c.Visible = true;
		}

		private void DisableAllControls()
		{
			foreach (var control in this.Controls)
			{
				DisableControl((Control)control);
			}
		}

		public void ChangeGameState(GameState newState)
		{
			switch (newState)
			{
				case GameState.Intro:;
					DisableAllControls();
					EnableControl(bCreateGame);
					EnableControl(bConnect);
					EnableControl(bExit);
					break;
				case GameState.HostSelect:
					DisableAllControls();
					EnableControl(lBNumOfPlayers);
					EnableControl(bCreateGame2);
					EnableControl(nListenPort);
					EnableControl(bIntro);
					break;
				case GameState.HostWaiting:
					DisableAllControls();
					EnableControl(bCancelHost);
					EnableControl(lWaiting);
					break;
				case GameState.HostWaitingRoom:
					DisableAllControls();
					EnableControl(lWaitingRoom);
					EnableControl(bStartGame);
					break;
				case GameState.ClientSearch:
					DisableAllControls();
					EnableControl(lBServers);
					bLogin.Visible = true;
					EnableControl(nHostPort);
					EnableControl(bRefreshServers);
					EnableControl(bIntro);
					break;
				case GameState.ClientWaiting:
					DisableAllControls();
					EnableControl(lWaiting);
					break;
				case GameState.ClientWaitingRoom:
					DisableAllControls();
					EnableControl(lWaitingRoom);
					bStartGame.Visible = true;
					break;
				case GameState.InGame:
					DisableAllControls();
					//this.WindowState = FormWindowState.Maximized; //fullscreen
					int borderHeight = this.Height - this.ClientRectangle.Height;
					graphicsDrawer = new GraphicsDrawer(this.Width, this.Height - borderHeight);
					break;
				default:
					break;
			}
			currentGameState = newState;
		}

		private void BCreateGame_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostSelect);
			lBNumOfPlayers.SelectedIndex = 0; //default select
		}

		private void BCreateGame2_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostWaiting);
			int numOfPlayers = lBNumOfPlayers.SelectedIndex + 1;
			host.BWStartHostStarter(numOfPlayers, (int)nListenPort.Value);
		}

		private void BConnect_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.ClientSearch);
			client.StartClient(IPAddress.Any);
		}

		private void BRefreshServers_Click(object sender, EventArgs e)
		{
			lBServers.Items.Clear();
			client.BWServerRefresherStart((int)nHostPort.Value);
		}

		private void BLogin_Click(object sender, EventArgs e)
		{
			client.LoginToSelectedServer();
		}

		private void BExit_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void BCancelHost_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostSelect);
			host.BWCancelHostStarter();
		}

		private void BIntro_Click(object sender, EventArgs e) //zatim spise nepouzivat, potrebuju zvlast pro clienta i hosta
		{
			ChangeGameState(GameState.Intro);
			//close client/host socket
		}

		public GraphicsDrawer graphicsDrawer;
		private void BStartGame_Click(object sender, EventArgs e) //host only
		{
			//create host's client
			client.StartClient(IPAddress.Loopback);
			client.hostEP = new IPEndPoint(IPAddress.Loopback, host.ListenPort);
			client.InGame = true;
			client.BW_WaitingCompleted(null, null);
			host.clientList.Add(new Host.ClientInfo(0, new IPEndPoint(IPAddress.Loopback, Client.listenPort)));
			
			byte[] hostStartGame = Data.ToBytes(Command.HostStartGame);
			host.LocalhostAndBroadcastMessage(hostStartGame); //localHost not needed
			host.StartGameListening();
			ChangeGameState(GameState.InGame);
			
			Random rng = new Random();
			foreach (var client in host.clientList)
			{
				client.player = new Player((float)rng.NextDouble(), (float)rng.NextDouble()); //spawn on tiles instead of pixels?
			}

			TimerInGame.Enabled = true;
		}

		private void TimerInGame_Tick(object sender, EventArgs e)
		{
			if (currentGameState == GameState.InGame) //should be-- maybe not necessary to check
			{
				byte[] hostTick = Data.ToBytes(Command.HostTick);
				host.LocalhostAndBroadcastMessage(hostTick);
				foreach (var client1 in host.clientList)
				{
					byte[] hostPlayerMovement = Data.ToBytes(Command.HostPlayerMovement, $"{client1.Id}|{(byte)client1.player.direction}|{client1.player.X}|{client1.player.Y}");
					host.LocalhostAndBroadcastMessage(hostPlayerMovement);
				}
			}
		}

		private void FormBambulanci_Paint(object sender, PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			if (currentGameState == GameState.InGame)
			{
				graphicsDrawer.DrawBackground(g);

				while (client.toBeDrawn != null && client.toBeDrawn.Count > 0) //was throwing null ref errors--quick fix
				{
					bool b = client.toBeDrawn.TryDequeue(out Client.ImageWithLocation imageWithLocation);
					while (!b) //spravna implementace??--------------------------------------------------
					{
						b = client.toBeDrawn.TryDequeue(out imageWithLocation);
						//Console.WriteLine("unable to dequeue image");
					}
					imageWithLocation.Draw(g,this.Width,this.Height);
				}
			}
		}

		public PlayerMovement playerMovement = PlayerMovement.Stay;
		private void FormBambulanci_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Left:
					playerMovement = PlayerMovement.Left;
					break;
				case Keys.Right:
					playerMovement = PlayerMovement.Right;
					break;
				case Keys.Up:
					playerMovement = PlayerMovement.Up;
					break;
				case Keys.Down:
					playerMovement = PlayerMovement.Down;
					break;
				case Keys.Space: //shoot
					break;
				default:
					break;
			}
		}

		private void FormBambulanci_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
			{
				playerMovement = PlayerMovement.Stay;
			}
		}
	}
}
