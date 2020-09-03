using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
			client.StartClient();
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
			/*
			//create host's client
			client.hostEPGlobal = new IPEndPoint(IPAddress.Loopback, host.listenPort);
			client.InGame = true;

			client.ActivateWorker(client.bwInGameListener, true, client.IGL_DoWork, client.IGL_RedrawProgress, client.IGL_Completed);

			host.clientList.Add(new ClientInfo(0, new IPEndPoint(IPAddress.Loopback, Client.listenPort)));
			//------------
			*/

			byte[] hostStartGame = Data.ToBytes(Command.HostStartGame);
			host.BroadcastMessage(hostStartGame);

			host.StartGameListening();

			ChangeGameState(GameState.InGame);
			

			//set each client's player -- prob will be different in the future...
			Random rng = new Random();
			foreach (var client in host.clientList)
			{
				client.player = new Player((float)rng.NextDouble(), (float)rng.NextDouble()); //spawn on tiles instead of pixels?
			}

			TimerInGame.Enabled = true;
			//poslat info vsem klientum o zacatku hry, host je normalni hrac jen s ID == 0 a posila data na localHost
			//host.clientList.Add(new ClientInfo(0, new IPEndPoint(IPAddress.Loopback, host.listenPort))); //not working
		}

		private void TimerInGame_Tick(object sender, EventArgs e) //host only--komunikace s klienty zde -- potrebuji poslouchat prichozi zpravy paralelne
		{
			//Invalidate(); //redraw--disable, Invalidate should be called from client

			if (currentGameState == GameState.InGame) //should be-- maybe not necessary to check
			{
				byte[] hostTick = Data.ToBytes(Command.HostTick);
				host.BroadcastMessage(hostTick);

				foreach (var client in host.clientList)
				{
					byte[] hostPlayerMovement = Data.ToBytes(Command.HostPlayerMovement, $"{client.Id}|{(byte)client.player.direction}|{client.player.x}|{client.player.y}");
					host.BroadcastMessage(hostPlayerMovement);
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

				if (client.InGame)//client only
				{
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
				else //host only -- do budoucna na hostovy asi vytvorim klienta, sjednotim to tedy s ostatnimi klienty a nasledujici nebude potreba
				{
					foreach (var client in host.clientList) //draw clients on host's form
					{
						int id = client.Id;
						byte direction = (byte)client.player.direction;
						float x = client.player.x;
						float y = client.player.y;

						Bitmap playerDesign = graphicsDrawer.GetPlayerDesign(id, direction);
						g.DrawImage(playerDesign, x * this.Width, y * this.Height);
					}
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
