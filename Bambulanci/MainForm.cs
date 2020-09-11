using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace Bambulanci
{
	public enum GameState { Intro, HostSelect, HostWaiting, ClientWaiting, HostWaitingRoom, ClientWaitingRoom, ClientSearch, InGame, GameScore }

	public partial class FormBambulanci : Form
	{
		private GameState currentGameState;
		private readonly WaiterClient waiterClient;
		private readonly WaiterHost waiterHost;
		
		/// <summary>
		/// Form height without border.
		/// </summary>
		public FormBambulanci()
		{
			InitializeComponent();
			waiterClient = new WaiterClient(this);
			waiterHost = new WaiterHost(this);
			ChangeGameState(GameState.Intro);

			//singlePlayer---debug:
			//nGameTime.Value = 30; //in seconds
			//waiterHost.BWStartClientWaiter(0, 45000);
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

		public static int WidthStatic { get; private set; }
		public static int HeightStatic { get; private set; }
		
		private void ResizeWindow()
		{
			//this.WindowState = FormWindowState.Maximized;
			int borderHeight = this.Height - this.ClientRectangle.Height;
			FormBambulanci.WidthStatic = this.Width;
			FormBambulanci.HeightStatic = this.Height - borderHeight;
			WeaponBox.SetSize(this.Width);
			Projectile.SetSize(this.Width);
			Player.SetSize(this.Width);
		}

		/// <summary>
		/// Enables/Disables form controls.
		/// </summary>
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
					EnableControl(lPort);
					EnableControl(lGameTime);
					EnableControl(nGameTime);
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
					EnableControl(lPort);
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
					ResizeWindow();
					Game = new Game();
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
			lBNumOfPlayers.SelectedIndex = 0;
		}

		private void BCreateGame2_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostWaiting);
			int numOfPlayers = lBNumOfPlayers.SelectedIndex + 1;
			waiterHost.BWStartClientWaiter(numOfPlayers, (int)nListenPort.Value);
		}

		private void BConnect_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.ClientSearch);
			waiterClient.StartClient(IPAddress.Any);
		}

		private void BRefreshServers_Click(object sender, EventArgs e)
		{
			lBServers.Items.Clear();
			waiterClient.BWServerRefresherStart((int)nHostPort.Value);
		}

		private void BLogin_Click(object sender, EventArgs e)
		{
			waiterClient.LoginToSelectedServer();
		}

		private void BExit_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void BCancelHost_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostSelect);
			waiterHost.BWCancelClientWaiter();
		}

		private void BIntro_Click(object sender, EventArgs e)
		{
			waiterClient.StopClient();
			ChangeGameState(GameState.Intro);
		}

		public Game Game { get; private set; }
		private IngameHost ingameHost;
		public int GameTime { get; private set; }

		/// <summary>
		/// Starts client's game and add client's player to Game.
		/// </summary>
		private void AddPlayersToGame(List<WaiterHost.ClientInfo> clientList)
		{
			foreach (var client in clientList)
			{
				byte[] hostStartGame = Data.ToBytes(Command.HostStartGame, client.Id);
				waiterHost.SendMessageToTarget(hostStartGame, client.IpEndPoint);

				(float x, float y) = Game.GetSpawnCoords(rng);
				Game.Players.Add(new Player(Game, x, y, client.Id, ipEndPoint: client.IpEndPoint));
			}
		}
		
		/// <summary>
		/// Starts host's client and adds him to host's clientList.
		/// </summary>
		private void AddHostsClient(WaiterHost waiterHost)
		{
			//start host's client
			int sendPort = waiterHost.ListenPort;
			waiterClient.HostStartIngameClient(sendPort);

			//add host's client
			waiterHost.clientList.Add(new WaiterHost.ClientInfo(0, new IPEndPoint(IPAddress.Loopback, Client.listenPort)));

		}

		/// <summary>
		/// Host only.
		/// Sets up game time. Starts host's client and his parallel listener. Starts host's parallel listener. Creates Game and it's players.
		/// </summary>
		private void BStartGame_Click(object sender, EventArgs e)
		{
			GameTime = (int)nGameTime.Value * 1000 / TimerInGame.Interval;
			AddHostsClient(waiterHost);
			AddPlayersToGame(waiterHost.clientList);
			ingameHost = waiterHost.StartIngameHost();
			TimerInGame.Enabled = true;
		}

		private readonly Random rng = new Random();
		private const double probabilityOfBoxSpawn = 0.003; //cca 1x per 10sec in case of 30ms/tick

		private void MoveOrKillPlayers()
		{
			lock (Game.Players)
			{
				foreach (var player in Game.Players)
				{
					if (player.isAlive)
					{
						byte[] hostPlayerMovement = Data.ToBytes(Command.HostPlayerMovement, (player.PlayerId, (byte)player.Direction, player.X, player.Y));
						ingameHost.LocalhostAndBroadcastMessage(hostPlayerMovement);
					}
					else
					{
						byte[] hostKillPlayer = Data.ToBytes(Command.HostKillPlayer, player.PlayerId, player.killedBy);
						ingameHost.LocalhostAndBroadcastMessage(hostKillPlayer);
					}
				}
			}
		}
		private void RespawnPlayers()
		{
			lock (Game.DeadPlayers)
			{
				foreach (var player in Game.DeadPlayers)
				{
					player.respawnTimer--;
					if (player.respawnTimer < 0)
					{
						(float x, float y) = Game.GetSpawnCoords(rng);
						byte[] hostPlayerRespawn = Data.ToBytes(Command.HostPlayerRespawn, (player.PlayerId, 0, x, y));
						ingameHost.LocalhostAndBroadcastMessage(hostPlayerRespawn);
					}
				}
			}
		}
		private void MoveAddDestroyProjectiles()
		{
			lock (Game.Projectiles)
			{
				foreach (var projectile in Game.Projectiles)
				{
					Game.Move(projectile, projectile.id);
					byte[] hostPlayerFire = Data.ToBytes(Command.HostPlayerFire, (projectile.id, (byte)projectile.Direction, projectile.X, projectile.Y));
					ingameHost.BroadcastMessage(hostPlayerFire);
					if (projectile.shouldBeDestroyed)
					{
						byte[] hostDestroyProjectile = Data.ToBytes(Command.HostDestroyProjectile, projectile.id);
						ingameHost.LocalhostAndBroadcastMessage(hostDestroyProjectile);
					}
				}
			}
		}
		private (int boxId, byte weaponType, float x, float y) GenerateBoxValues()
		{
			byte randomWeaponId = (byte)rng.Next(Enum.GetNames(typeof(WeaponType)).Length);
			(float x, float y) = Game.GetSpawnCoords(rng);
			Game.boxIdCounter++;

			return (Game.boxIdCounter, randomWeaponId, x, y);
		}
		private void SpawnAndCollectBoxes()
		{
			bool addBox = rng.NextDouble() < probabilityOfBoxSpawn;
			lock (Game.Boxes)
			{
				if (addBox)
				{
					var boxValues = GenerateBoxValues();
					byte[] hostBoxSpawned = Data.ToBytes(Command.HostBoxSpawned, boxValues);
					ingameHost.LocalhostAndBroadcastMessage(hostBoxSpawned);
				}
				foreach (var box in Game.Boxes)
				{
					if (box.CollectedBy != -1)
					{
						byte[] hostBoxCollected = Data.ToBytes(Command.HostBoxCollected, box.Id, box.CollectedBy);
						ingameHost.LocalhostAndBroadcastMessage(hostBoxCollected);
					}
				}
			}
		}

		/// <summary>
		/// Host only.
		/// Moves, kills and respawns players. Creates and moves projectiles. Spawns and collects game boxes.
		/// </summary>
		private void TimerInGame_Tick(object sender, EventArgs e)
		{
			GameTime--;
			if (GameTime < 0) return;

			byte[] hostTick = Data.ToBytes(Command.HostTick);
			ingameHost.LocalhostAndBroadcastMessage(hostTick);

			MoveOrKillPlayers();
			RespawnPlayers();
			MoveAddDestroyProjectiles();
			SpawnAndCollectBoxes();
		}

		private void DrawItems(IEnumerable<IDrawable> drawableItems, Graphics g, Action<Graphics,IDrawable> draw)
		{
			lock (drawableItems)
			{
				foreach (var item in drawableItems)
				{
					draw(g, item);
				}
			}
		}

		/// <summary>
		/// In-game drawing.
		/// </summary>
		private void FormBambulanci_Paint(object sender, PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			if (currentGameState == GameState.InGame)
			{
				Game.graphicsDrawer.DrawBackground(g);
				DrawItems(Game.Boxes, g, Game.graphicsDrawer.DrawBox);
				DrawItems(Game.Players, g, Game.graphicsDrawer.DrawPlayer);
				DrawItems(Game.Projectiles, g, Game.graphicsDrawer.DrawProjectile);
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
