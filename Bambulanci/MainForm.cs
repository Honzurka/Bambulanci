using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace Bambulanci
{
	public enum GameState { Intro, HostSelect, HostWaiting, ClientWaiting, HostWaitingRoom, ClientWaitingRoom, ClientSearch, ClientInGame }

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
			


			//test only:
			//ChangeGameState(GameState.HostWaitingRoom);
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
			currentGameState = newState;
			switch (currentGameState)
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
				case GameState.ClientInGame:
					DisableAllControls();
					int borderHeight = this.Height - this.ClientRectangle.Height;
					game = new Game(this.Width, this.Height - borderHeight, null); //shouldnt be null
					break;
				default:
					break;
			}
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

		//start game by host----------------------------------------------------------
		public Game game; //public for client to access
		private void bStartGame_Click(object sender, EventArgs e) //host only
		{
			//poslat info vsem klientum o zacatku hry, host je normalni hrac jen s ID == 0 a posila data na localHost
			//not working ------------
			//host.clientList.Add(new ClientInfo(0, new IPEndPoint(IPAddress.Loopback, host.listenPort))); //hopefully right port
			


			//klienti zacnout poslouchat a podle prijatych informaci prekreslovat obrazovku - prace s ID, aby klient vedel, koho ma prekreslovat
			host.StartClientGame();
			host.StartGameListening();
			

			DisableAllControls();
			TimerInGame.Enabled = true;

			//this.WindowState = FormWindowState.Maximized; //fullscreen ----------------------
			
			
			int borderHeight = this.Height - this.ClientRectangle.Height;
			game = new Game(this.Width, this.Height - borderHeight, host.clientList);
			//wait some time so everyone can setup game--
		}

		private void TimerInGame_Tick(object sender, EventArgs e) //host only--komunikace s klienty zde -- potrebuji poslouchat prichozi zpravy paralelne
		{
			//Invalidate(); //redraw

			if (game != null)
			{
				host.RedrawClients();
				host.MoveClients();
			}
		}

		private void formBambulanci_Paint(object sender, PaintEventArgs e) //probably only host should be moving objects-----------------
		{
			Graphics g = e.Graphics;
			//all graphics events have to be called from here
			if (game != null)
			{
				game.DrawBackground(g);

				if (client.toBeDrawn != null)//what about host??-----------------------
				{
					Console.WriteLine($"redraw @ {client.toBeDrawn.Count}"); //----------------------------------toBeDrawn becomes null somehow????

					while (client.toBeDrawn.Count > 0)
					{
						Client.ImageWithLocation imageWithLocation;
						bool b = client.toBeDrawn.TryDequeue(out imageWithLocation);
						while (!b) //spravna implementace??--------------------------------------------------
						{
							b = client.toBeDrawn.TryDequeue(out imageWithLocation);
							Console.WriteLine("unable to dequeue image ---------------------------");
						}
						Console.WriteLine($"client: image toBeDrawn dequeued");
						//var image = client.toBeDrawn.Dequeue();
						imageWithLocation.Draw(g,this.Width,this.Height);
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
