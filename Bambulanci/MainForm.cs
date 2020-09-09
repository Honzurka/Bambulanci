using System;
using System.Drawing;
using System.Net;
using System.Windows.Forms;

namespace Bambulanci
{
	public enum GameState { Intro, HostSelect, HostWaiting, ClientWaiting, HostWaitingRoom, ClientWaitingRoom, ClientSearch, InGame, GameScore }

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
			host.BWStartHostStarter(0, 45000);
			ChangeGameState(GameState.HostWaitingRoom);
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
				case GameState.GameScore:
					TimerInGame.Stop();
					EnableControl(lScore);
					EnableControl(bExit);
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
		public int GameTime { get; private set; }
		private void BStartGame_Click(object sender, EventArgs e) //host only
		{
			GameTime = 20000;

			//create host's client
			client.StartClient(IPAddress.Loopback);
			client.hostEP = new IPEndPoint(IPAddress.Loopback, host.ListenPort);
			client.InGame = true;
			client.BW_WaitingCompleted(null, null);
			host.clientList.Add(new Host.ClientInfo(0, new IPEndPoint(IPAddress.Loopback, Client.listenPort)));
			
			byte[] hostStartGame = Data.ToBytes(Command.HostStartGame);
			host.BroadcastMessage(hostStartGame);
			host.StartGameListening();
			ChangeGameState(GameState.InGame);

			foreach (var client in host.clientList)
			{
				(float x, float y) = Game.GetSpawnCoords();
				Game.Players.Add(new Player(this, x, y, client.Id, ipEndPoint: client.IpEndPoint));
			}
			TimerInGame.Enabled = true;
		}

		Random rng = new Random();
		private void TimerInGame_Tick(object sender, EventArgs e) //host only
		{
			GameTime--;
			if(GameTime < 0)
			{
				//ChangeGameState(GameState.GameScore);
				Console.WriteLine("Konec hry-----------------");
				return;
			}

			//if (currentGameState == GameState.InGame) //should be-- maybe not necessary to check
			{
				byte[] hostTick = Data.ToBytes(Command.HostTick);
				host.LocalhostAndBroadcastMessage(hostTick);

				lock (Game.Players)
				{
					foreach (var player in Game.Players)
					{
						if (player.isAlive)
						{
							byte[] hostPlayerMovement = Data.ToBytes(Command.HostPlayerMovement, values: (player.PlayerId, (byte)player.Direction, player.X, player.Y));
							host.LocalhostAndBroadcastMessage(hostPlayerMovement);
						}
						else
						{
							byte[] hostKillPlayer = Data.ToBytes(Command.HostKillPlayer, integer: player.PlayerId, integer2: player.killedBy);
							host.LocalhostAndBroadcastMessage(hostKillPlayer);
						}
					}
				}
				lock (Game.DeadPlayers)
				{
					foreach (var player in Game.DeadPlayers)
					{
						player.respawnTimer--;
						if (player.respawnTimer < 0) //i might want to set players weapon to pistol------
						{
							(float x, float y) = Game.GetSpawnCoords();
							byte[] hostPlayerRespawn = Data.ToBytes(Command.HostPlayerRespawn, values: (player.PlayerId, 0, x, y));
							host.LocalhostAndBroadcastMessage(hostPlayerRespawn);
						}
					}
				}
				lock (Game.Projectiles)
				{
					foreach (var projectile in Game.Projectiles)
					{
						Game.Move(projectile, projectile.id);
						byte[] hostPlayerFire = Data.ToBytes(Command.HostPlayerFire, values: (projectile.id, (byte)projectile.Direction, projectile.X, projectile.Y));
						host.BroadcastMessage(hostPlayerFire);
						if (projectile.shouldBeDestroyed)
						{
							byte[] hostDestroyProjectile = Data.ToBytes(Command.HostDestroyProjectile, integer: projectile.id);
							host.LocalhostAndBroadcastMessage(hostDestroyProjectile);
						}
					}
				}
				bool addBox = rng.Next(0, 1000) > 996; //1x per 10sec----constant.....
				lock (Game.Boxes)
				{
					if (addBox)
					{
						int numOfWeapons = Enum.GetNames(typeof(WeaponType)).Length;
						byte weaponNum = (byte)rng.Next(numOfWeapons);
						WeaponType weaponType = (WeaponType)weaponNum;
						(float x, float y) = Game.GetSpawnCoords();
						ICollectableObject newBox = null;
						newBox = WeaponBox.Generate(Game.boxIdCounter, x, y, this, weaponType);
						byte[] hostBoxSpawned = Data.ToBytes(Command.HostBoxSpawned, values: (Game.boxIdCounter, (byte)weaponType, x, y));
						host.LocalhostAndBroadcastMessage(hostBoxSpawned);
						Game.boxIdCounter++;
					}
					foreach (var box in Game.Boxes)
					{
						if(box.CollectedBy != -1)
						{
							byte[] hostBoxCollected = Data.ToBytes(Command.HostBoxCollected, integer: box.Id, integer2: box.CollectedBy);
							host.LocalhostAndBroadcastMessage(hostBoxCollected);
						}
					}
				}
			}
		}

		private void FormBambulanci_Paint(object sender, PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			if (currentGameState == GameState.InGame)
			{
				Game.graphicsDrawer.DrawBackground(g);
				lock (Game.Boxes)
				{
					foreach (var box in Game.Boxes)
					{
						Game.graphicsDrawer.DrawBox(g, box);
					}
				}
				lock (Game.Players)
				{
					foreach (var player in Game.Players)
					{
						Game.graphicsDrawer.DrawPlayer(g, player);
					}
				}
				lock (Game.Projectiles)
				{
					foreach (var projectile in Game.Projectiles)
					{
						Game.graphicsDrawer.DrawProjectile(g, projectile);
					}
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
