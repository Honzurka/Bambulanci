﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using System.Windows.Forms;

namespace Bambulanci
{
	public enum GameState { Intro, HostSelect, HostWaiting, ClientWaiting, HostWaitingRoom, ClientWaitingRoom, ClientSearch, InGame }

	public partial class formBambulanci : Form
	{
		private GameState currentGameState;
		private Client client;
		private Host host;

		public formBambulanci()
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
					EnableControl(bLogin);
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
					bStartGame.Visible = true; //not necessary
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

		private void bCreateGame_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostSelect);
			lBNumOfPlayers.SelectedIndex = 0; //default select
		}

		private void bCreateGame2_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostWaiting);
			int numOfPlayers = lBNumOfPlayers.SelectedIndex + 1;
			host.BWStartHost(numOfPlayers, (int)nListenPort.Value);
		}

		private void bConnect_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.ClientSearch);
			client.StartClient(IPAddress.Any);
		}

		private void bRefreshServers_Click(object sender, EventArgs e)
		{
			lBServers.Items.Clear();
			client.BWServerRefresherStart((int)nHostPort.Value);
		}

		private void bLogin_Click(object sender, EventArgs e) //throws errors if no server is chosen -- disable button before refreshing...
		{
			client.LoginToSelectedServer();
		}

		private void bExit_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void bCancelHost_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostSelect);
			host.BWCancelHost();
		}

		private void bIntro_Click(object sender, EventArgs e) //zatim spise nepouzivat, potrebuju zvlast pro clienta i hosta
		{
			ChangeGameState(GameState.Intro);
			//close client/host socket
		}

		public GraphicsDrawer graphicsDrawer; //public for client to generate designs of players
		private void bStartGame_Click(object sender, EventArgs e) //host only
		{
			//create host's client
			client.StartClient(IPAddress.Loopback);
			client.hostEPGlobal = new IPEndPoint(IPAddress.Loopback, host.listenPort);
			client.InGame = true;
			client.BW_WaitingCompleted(null, null); //will it work??----
			host.clientList.Add(new ClientInfo(0, new IPEndPoint(IPAddress.Loopback, Client.listenPort)));
			
			byte[] hostStartGame = Data.ToBytes(Command.HostStartGame);
			host.LocalhostAndBroadcastMessage(hostStartGame); //localHost not needed
			host.StartGameListening();
			ChangeGameState(GameState.InGame);
			
			//set each client's player -- prob will be different in the future...
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
				//host.BroadcastMessage(hostTick);
				//working if written here, not working if under BroadcastMessage -_-
				
				//host.udpHost.Send(hostTick, hostTick.Length, new IPEndPoint(IPAddress.Loopback, Client.listenPort)); //...
				foreach (var client1 in host.clientList)
				{
					byte[] hostPlayerMovement = Data.ToBytes(Command.HostPlayerMovement, $"{client1.Id}|{(byte)client1.player.direction}|{client1.player.x}|{client1.player.y}");
					host.LocalhostAndBroadcastMessage(hostPlayerMovement);
					//host.BroadcastMessage(hostPlayerMovement);
					//host.udpHost.Send(hostPlayerMovement, hostPlayerMovement.Length, new IPEndPoint(IPAddress.Loopback, Client.listenPort));//...
				}
			}
		}

		private void formBambulanci_Paint(object sender, PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			//all graphics events have to be called from here
			if (currentGameState == GameState.InGame)
			{
				graphicsDrawer.DrawBackground(g);

				while (client.toBeDrawn != null && client.toBeDrawn.Count > 0) //was throwing null ref errors--quick fix
				{
					Client.ImageWithLocation imageWithLocation;
					bool b = client.toBeDrawn.TryDequeue(out imageWithLocation);
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
		private void formBambulanci_KeyDown(object sender, KeyEventArgs e)
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

		private void formBambulanci_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
			{
				playerMovement = PlayerMovement.Stay;
			}
		}
	}
}
