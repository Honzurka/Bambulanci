using System;
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
		public int TrueHeight { get; private set; } //height without border

		public FormBambulanci()
		{
			InitializeComponent();
			client = new Client(this);
			host = new Host(this);
			ChangeGameState(GameState.Intro);

			//singlePlayer:
			//host.BWStartHostStarter(0, 45000);
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
					TrueHeight = this.Height - borderHeight;
					Game = new Game(this.Width, TrueHeight);
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

		public Game Game { get; private set; }
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

			Game.Players.Add(new Player(this, 0.5f, 0.5f, 2, ipEndPoint: new IPEndPoint(IPAddress.Any, 1111))); //test only---
			foreach (var client in host.clientList)
			{
				(float x, float y) = Game.GetSpawnCoords();
				Game.Players.Add(new Player(this, x, y, client.Id, ipEndPoint: client.IpEndPoint));
			}
			
			TimerInGame.Enabled = true;
		}

		private void TimerInGame_Tick(object sender, EventArgs e) //host only
		{
			if (currentGameState == GameState.InGame) //should be-- maybe not necessary to check
			{
				byte[] hostTick = Data.ToBytes(Command.HostTick);
				host.LocalhostAndBroadcastMessage(hostTick);
				//Console.WriteLine("#1 host: timer tick sent.");

				foreach (var player in Game.Players)
				{
					byte[] hostPlayerMovement = Data.ToBytes(Command.HostPlayerMovement, values: (player.id, (byte)player.Direction, player.X, player.Y));
					host.LocalhostAndBroadcastMessage(hostPlayerMovement);
				}
				foreach (var projectile in Game.projectiles)
				{
					Game.Move(projectile.direction, ref projectile.X, ref projectile.Y, Projectile.speed, Game.graphicsDrawer.ProjectileWidthPx, Game.graphicsDrawer.ProjectileHeightPx);
					//projectile.MoveByHost(); //move should be in parallel, but its not possible to time it well
					byte[] hostPlayerFire = Data.ToBytes(Command.HostPlayerFire, values: (projectile.Id, (byte)projectile.direction, projectile.X, projectile.Y));
					host.BroadcastMessage(hostPlayerFire); //only broadcast, otherwise paralelism problems
					//Console.WriteLine($"#6 host: projectile moved + hostPlayerFire sent by host x:{projectile.X} y:{projectile.Y} ");
				}
			}
		}

		private void FormBambulanci_Paint(object sender, PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			if (currentGameState == GameState.InGame)
			{
				Game.graphicsDrawer.DrawBackground(g);
				foreach (var player in Game.Players)
				{
					Game.graphicsDrawer.DrawPlayer(g, player);
				}
				foreach (var projectile in Game.projectiles)
				{
					Game.graphicsDrawer.DrawProjectile(g, projectile);
				}
			}
		}

		public Direction playerMovement = Direction.Stay;
		public WeaponState weaponState = WeaponState.Still;
		private void FormBambulanci_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Left:
					playerMovement = Direction.Left;
					break;
				case Keys.Right:
					playerMovement = Direction.Right;
					break;
				case Keys.Up:
					playerMovement = Direction.Up;
					break;
				case Keys.Down:
					playerMovement = Direction.Down;
					break;
				case Keys.Space:
					weaponState = WeaponState.Fired;
					break;
				default:
					break;
			}
		}

		private void FormBambulanci_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
			{
				playerMovement = Direction.Stay;
			}
			if(e.KeyCode == Keys.Space)
			{
				weaponState = WeaponState.Still;
			}
		}
	}
}
