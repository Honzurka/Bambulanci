using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms.VisualStyles;

namespace Bambulanci
{
	/// <summary>
	/// Client___ - commands sent by client.
	/// Host___ - commands sent by host.
	/// </summary>
	enum Command { ClientLogin, ClientFindServers, ClientStopRefreshing,
		HostFoundServer, HostMoveToWaitingRoom, HostStopHosting,
		HostLoginAccepted, HostStartGame,
		
		//InGame
		HostTick,
		ClientMove, HostPlayerMovement,
		ClientFire, HostPlayerFire,
		HostDestroyProjectile, HostKillPlayer, HostPlayerRespawn,
		HostBoxSpawned, HostBoxCollected,
		HostGameEnded, HostScore
	}
	
	/// <summary>
	/// Data parser for network communication.
	/// </summary>
	class Data
	{
		public Command Cmd { get; private set; }
		public byte B { get; private set; }
		public int Integer1 { get; private set; }
		public int Integer2 { get; private set; }
		public ValueTuple<int, byte, float, float> Values { get; private set; }

		private readonly static Dictionary<Command, Func<byte[], Data>> FuncByCommand = new Dictionary<Command, Func<byte[], Data>>()
		{
			{Command.HostPlayerMovement, ReconstructValues },
			{Command.HostPlayerFire, ReconstructValues },
			{Command.HostPlayerRespawn, ReconstructValues },
			{Command.HostBoxSpawned, ReconstructValues },
			{Command.ClientMove, ReconstructByte },
			{Command.ClientFire, ReconstructByte },
			{Command.HostDestroyProjectile, ReconstructInt },
			{Command.HostStartGame, ReconstructInt},
			{Command.HostBoxCollected, ReconstructInts},
			{Command.HostKillPlayer, ReconstructInts }
		};

		private static Data ReconstructValues(byte[] data)
		{
			Data result = new Data(data[0]);

			int id = BitConverter.ToInt32(data, 1);
			byte enumData = data[5];
			float x = BitConverter.ToSingle(data, 6);
			float y = BitConverter.ToSingle(data, 10);

			result.Values = (id, enumData, x, y);
			return result;
		}
		private static Data ReconstructByte(byte[] data)
		{
			Data result = new Data(data[0]);
			result.B = data[1];
			return result;
		}
		private static Data ReconstructInt(byte[] data)
		{
			Data result = new Data(data[0]);
			result.Integer1 = BitConverter.ToInt32(data, 1);
			return result;
		}
		private static Data ReconstructInts(byte[] data)
		{
			Data result = new Data(data[0]);
			result.Integer1 = BitConverter.ToInt32(data, 1);
			result.Integer2 = BitConverter.ToInt32(data, 5);
			return result;
		}

		private Data(byte command)
		{
			Cmd = (Command)command;
		}

		public static Data GetData(byte[] data)
		{
			Command cmd = (Command)data[0];
			if (FuncByCommand.TryGetValue(cmd, out Func<byte[], Data> func))
			{
				return func(data);
			}
			else
			{
				return new Data(data[0]);
			}

		}

		public static byte[] ToBytes(Command cmd)
		{
			byte[] result = new byte[] { (byte)cmd };
			return result;
		}
		public static byte[] ToBytes(Command cmd, (int id, byte enumData, float x, float y) values)
		{
			List<byte> result = new List<byte>() { (byte)cmd };
			result.AddRange(BitConverter.GetBytes(values.id));
			result.Add(values.enumData);
			result.AddRange(BitConverter.GetBytes(values.x));
			result.AddRange(BitConverter.GetBytes(values.y));
			return result.ToArray();
		}
		public static byte[] ToBytes(Command cmd, byte b)
		{
			List<byte> result = new List<byte>() { (byte)cmd };
			result.Add(b);
			return result.ToArray();
		}
		public static byte[] ToBytes(Command cmd, int integer)
		{
			List<byte> result = new List<byte>() { (byte)cmd };
			result.AddRange(BitConverter.GetBytes(integer));
			return result.ToArray();
		}
		public static byte[] ToBytes(Command cmd, int integer1, int integer2)
		{
			List<byte> result = new List<byte>() { (byte)cmd };
			result.AddRange(BitConverter.GetBytes(integer1));
			result.AddRange(BitConverter.GetBytes(integer2));
			return result.ToArray();

		}
		public static byte[] ToBytes(Command cmd, string msg)
		{
			List<byte> result = new List<byte>() { (byte)cmd };
			result.AddRange(BitConverter.GetBytes(msg.Length));
			result.AddRange(Encoding.ASCII.GetBytes(msg));
			return result.ToArray();
		}

	}
	abstract class Host
	{
		protected readonly FormBambulanci form;
		protected UdpClient udpHost;
		public int ListenPort { get; protected set; }
		public Host(FormBambulanci form) => this.form = form;

		public void BroadcastMessage(byte[] message)///x3---test--------------------------------------------------------------
		{
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, WaiterClient.listenPort));
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, WaiterClient.listenPort));
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, WaiterClient.listenPort));
		}

		/// <summary>
		/// Broadcast on network and localhost.
		/// </summary>
		public void LocalhostAndBroadcastMessage(byte[] message)
		{
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, WaiterClient.listenPort));
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Loopback, WaiterClient.listenPort));
		}

		public void SendMessageToTarget(byte[] message, IPEndPoint targetEP)///x3---test--------------------------------------------------------------
		{
			udpHost.Send(message, message.Length, targetEP);
			udpHost.Send(message, message.Length, targetEP);
			udpHost.Send(message, message.Length, targetEP);
		}

	}
	class WaiterHost : Host
	{
		public WaiterHost(FormBambulanci form) : base(form) { }

		public List<ClientInfo> clientList;
		
		public class ClientInfo
		{
			public int Id { get; }
			public IPEndPoint IpEndPoint { get; }
			public ClientInfo(int id, IPEndPoint ipEndPoint)
			{
				this.Id = id;
				this.IpEndPoint = ipEndPoint;
			}
		}

		private BackgroundWorker bwClientWaiter;
		
		public void BWStartClientWaiter(int numOfPlayers, int listenPort)
		{
			ListenPort = listenPort;
			ParallelBW.ActivateWorker(ref bwClientWaiter, true, CW_WaitForClients, CW_Completed, CW_UpdateRemainingPlayers, (numOfPlayers, listenPort));
		}
		
		public void BWCancelClientWaiter()
		{
			byte[] cancel = Data.ToBytes(Command.HostStopHosting);
			IPEndPoint localHost = new IPEndPoint(IPAddress.Loopback, ListenPort);
			SendMessageToTarget(cancel, localHost);
		}

		/// <summary>
		/// Reports progress of bwClientWaiter.
		/// </summary>
		private void UpdateRemainingPlayers(int numOfPlayers)
		{
			int remainingPlayers = numOfPlayers - clientList.Count;
			bwClientWaiter.ReportProgress(remainingPlayers);
		}

		/// <summary>
		/// Waits for numOfPlayers to connect to host.
		/// </summary>
		private void CW_WaitForClients(object sender, DoWorkEventArgs e)
		{
			(int numOfPlayers, int listenPort) = (ValueTuple<int, int>)e.Argument;
			clientList = new List<ClientInfo>();
			udpHost = new UdpClient(new IPEndPoint(IPAddress.Any, listenPort));

			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, listenPort);
			int id = 1; //0 is host
			bool hostClosed = false;
			UpdateRemainingPlayers(numOfPlayers);
			while (!hostClosed && clientList.Count < numOfPlayers)
			{
				Data data = Data.GetData(udpHost.Receive(ref clientEP));
				switch (data.Cmd)
				{
					case Command.ClientLogin:
						ClientInfo clientInfo = new ClientInfo(id, clientEP);
						clientList.Add(clientInfo);
						UpdateRemainingPlayers(numOfPlayers);
						id++;
						byte[] hostLoginAccepted = Data.ToBytes(Command.HostLoginAccepted);
						SendMessageToTarget(hostLoginAccepted, clientEP);
						break;
					case Command.ClientFindServers:
						byte[] hostFoundServer = Data.ToBytes(Command.HostFoundServer);
						SendMessageToTarget(hostFoundServer, clientEP);
						break;
					case Command.HostStopHosting:
						hostClosed = true;
						udpHost.Close();
						e.Cancel = true;
						break;
					default:
						break;
				}
			}
		}

		private void CW_UpdateRemainingPlayers(object sender, ProgressChangedEventArgs e)
		{
			int remainingPlayers = e.ProgressPercentage;
			form.lWaiting.Text = $"Čekám na {remainingPlayers} hráče";
		}

		/// <summary>
		/// Moves connected clients to waiting room.
		/// </summary>
		private void CW_Completed(object sender, RunWorkerCompletedEventArgs e)
		{
			if(e.Error == null && !e.Cancelled)
			{
				byte[] moveClientToWaitingRoom = Data.ToBytes(Command.HostMoveToWaitingRoom);
				BroadcastMessage(moveClientToWaitingRoom);
				form.ChangeGameState(GameState.HostWaitingRoom);
			}
		}

		public IngameHost StartIngameHost()
		{
			return new IngameHost(form, udpHost, ListenPort);
		}
	}

	class IngameHost : Host
	{
		public IngameHost(FormBambulanci form, UdpClient udpHost, int listenport) : base(form)
		{
			this.udpHost = udpHost;
			this.ListenPort = listenport;
			StartGameListening();
		}

		private BackgroundWorker bwGameListener;
		private void StartGameListening()
		{
			ParallelBW.ActivateWorker(ref bwGameListener, false, GL_ProcessClientsAction, GL_Completed);
		}

		private void PlayerAct(IPEndPoint playerEP, Enum state, Action<Player,Enum> action)
		{
			lock (form.Game.Players)
			{
				Player senderPlayer = form.Game.Players.Find(p => p.ipEndPoint.Equals(playerEP));
				if (senderPlayer != null)
					action(senderPlayer, state);
					
			}
		}

		/// <summary>
		/// Game Listener's work.
		/// Processes datagrams from clients in game and alters current Game state based on them.
		/// </summary>
		private void GL_ProcessClientsAction(object sender, DoWorkEventArgs e)
		{
			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, ListenPort);

			while (form.GameTime > 0)
			{
				Data data = Data.GetData(udpHost.Receive(ref clientEP));
				switch (data.Cmd)
				{
					case Command.ClientMove:
						Direction playerMovement = (Direction)data.B;
						PlayerAct(clientEP, playerMovement, Player.CallMoveByHost);
						break;
					case Command.ClientFire:
						WeaponState weaponState = (WeaponState)data.B;
						PlayerAct(clientEP, weaponState, Player.CallWeaponFire);
						break;
					default:
						break;
				}
			}
		}

		/// <summary>
		/// Ends game. Inform clients about game score. Stops clients backgroundWorkers.
		/// </summary>
		private void GL_Completed(object sender, RunWorkerCompletedEventArgs e)
		{
			byte[] hostGameEnded = Data.ToBytes(Command.HostGameEnded);
			LocalhostAndBroadcastMessage(hostGameEnded);
		}

	}

	abstract class Client
	{
		protected const int notUsed = 0;
		protected const int notFound = -1;
		public const int listenPort = 60000; //wont allow multiple servers

		protected readonly FormBambulanci form;
		protected UdpClient udpClient;
		public IPEndPoint hostEP;

		protected int myPlayerId = 0;

		public Client(FormBambulanci form) => this.form = form;

		public void SendMessageToTarget(byte[] message, IPEndPoint targetEP)
		{
			udpClient.Send(message, message.Length, targetEP);
		}

	}

	class WaiterClient : Client
	{
		public bool InGame;

		public WaiterClient(FormBambulanci form) : base(form) { }

		public void StartClient(IPAddress iPAddress) => udpClient = new UdpClient(new IPEndPoint(iPAddress, listenPort));
		public void StopClient()
		{
			if (udpClient != null)
			{
				if (bwServerRefresher != null)
				{
					ServerRefresherStop();
				}
				udpClient.Close();
				udpClient = null;
			}
		}

		private BackgroundWorker bwServerRefresher;
		/// <summary>
		/// Sends "poison pill" on localHost => stops server refreshing backgroundWorker.
		/// </summary>
		private void ServerRefresherStop()
		{
			byte[] poisonPill = Data.ToBytes(Command.ClientStopRefreshing);
			IPEndPoint localhostEP = new IPEndPoint(IPAddress.Loopback, listenPort);
			SendMessageToTarget(poisonPill, localhostEP);
		}
		public void BWServerRefresherStart(int hostPort)
		{
			if (bwServerRefresher != null)
			{
				ServerRefresherStop();
			}
			ParallelBW.ActivateWorker(ref bwServerRefresher, true, SR_RefreshServers, null, SR_ServerFound, hostPort);
		}

		/// <summary>
		/// Sends broadcast and waits for host's response.
		/// Each responding host is then added to server listBox
		/// </summary>
		private void SR_RefreshServers(object sender, DoWorkEventArgs e)
		{
			int hostPort = (int)e.Argument;
			IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, hostPort);

			var hostEPVar = new IPEndPoint(IPAddress.Any, listenPort);

			byte[] findServerMessage = Data.ToBytes(Command.ClientFindServers);
			SendMessageToTarget(findServerMessage, broadcastEP);

			bool searching = true;
			while (searching)
			{
				Data received = Data.GetData(udpClient.Receive(ref hostEPVar));
				switch (received.Cmd)
				{
					case Command.HostFoundServer:
						bwServerRefresher.ReportProgress(notUsed, hostEPVar);
						break;
					case Command.ClientStopRefreshing:
						searching = false;
						break;
					default:
						break;
				}
			}
		}

		private void SR_ServerFound(object sender, ProgressChangedEventArgs e)
		{
			var newHostEP = e.UserState;
			form.lBServers.Items.Add(newHostEP);
			form.bLogin.Enabled = true;
			form.lBServers.SelectedIndex = 0;
		}

		/// <summary>
		/// Loops until right command is received.
		/// </summary>
		private Data WaitForCommand(Command command)
		{
			Data received = Data.GetData(udpClient.Receive(ref hostEP));
			while (received.Cmd != command)
			{
				received = Data.GetData(udpClient.Receive(ref hostEP));
			}
			return received;
		}

		/// <summary>
		/// Logging to server stops refreshing servers in backgroud.
		/// </summary>
		public void LoginToSelectedServer()
		{
			ServerRefresherStop();

			hostEP = (IPEndPoint)form.lBServers.SelectedItem;
			byte[] loginMessage = Data.ToBytes(Command.ClientLogin);			
			SendMessageToTarget(loginMessage, hostEP);

			WaitForCommand(Command.HostLoginAccepted);
			form.ChangeGameState(GameState.ClientWaiting);

			ParallelBW.ActivateWorker(ref bwHostWaiter, true, HW_ClientWaiting, HW_WaitingCompleted, HW_WaitingProgress);
		}

		private BackgroundWorker bwHostWaiter;
		private void HW_ClientWaiting(object sender, DoWorkEventArgs e)
		{
			WaitForCommand(Command.HostMoveToWaitingRoom);
			bwHostWaiter.ReportProgress((int)Command.HostMoveToWaitingRoom);

			Data received = WaitForCommand(Command.HostStartGame);
			int myPlayerId = received.Integer1;
			bwHostWaiter.ReportProgress((int)Command.HostStartGame, myPlayerId);
		}

		private void HW_WaitingProgress(object sender, ProgressChangedEventArgs e)
		{
			Command cmd = (Command)e.ProgressPercentage;
			switch (cmd)
			{
				case Command.HostMoveToWaitingRoom:
					form.ChangeGameState(GameState.ClientWaitingRoom);
					break;
				case Command.HostStartGame:
					InGame = true;
					myPlayerId = (int)e.UserState;
					break;
				default:
					break;
			}
		}
		
		private void HW_WaitingCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (InGame)
			{
				StartIngameClient();
			}
		}
		private void StartIngameClient()
		{
			form.ChangeGameState(GameState.InGame);
			IngameClient.StartClient(form, udpClient, hostEP, myPlayerId);
		}
		public void HostStartIngameClient(int sendPort)
		{
			form.ChangeGameState(GameState.InGame);
			UdpClient udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, listenPort));
			IPEndPoint hostEP = new IPEndPoint(IPAddress.Loopback, sendPort);
			IngameClient.StartClient(form, udpClient, hostEP, myPlayerId);
		}
	}

	class IngameClient : Client
	{
		Game game;
		private IngameClient(FormBambulanci form) : base(form) { }
		public static void StartClient(FormBambulanci form, UdpClient udpClient, IPEndPoint hostEP, int myPlayerId)
		{
			IngameClient client = new IngameClient(form);
			client.udpClient = udpClient;
			client.hostEP = hostEP;
			client.myPlayerId = myPlayerId;
			client.game = form.Game;
			client.StartGameListening();
		}

		private void StartGameListening()
		{
			ParallelBW.ActivateWorker(ref bwInGameListener, true, IGL_ProcessHostCommands, IGL_DisplayScore, IGL_RedrawProgress);
		}

		private void HostTick(Data ignored)
		{
			bwInGameListener.ReportProgress(notUsed);
		}
		private void MoveOrAddPlayer(Data received)
		{
			(int playerId, byte direction, float x, float y) = received.Values;
			int index;
			lock (game.Players)
			{
				index = game.Players.FindIndex(p => p.PlayerId == playerId);
				if (index == notFound)
				{
					game.Players.Add(new Player(form, x, y, playerId, (Direction)direction));
				}
				else
				{
					game.Players[index].MoveByClient((Direction)direction, x, y);
				}
			}
		}
		private void MoveOrAddProjectile(Data received)
		{
			(int projectileId, int direction, float x, float y) = received.Values;
			lock (game.Projectiles)
			{
				int index = game.Projectiles.FindIndex(p => p.id == projectileId);
				if (index == notFound)
				{
					game.Projectiles.Add(new Projectile(x, y, (Direction)direction, projectileId, form));
				}
				else
				{
					game.Projectiles[index].X = x;
					game.Projectiles[index].Y = y;
				}
			}
		}
		private void DestroyProjectile(Data received)
		{
			int projectileId = received.Integer1;
			lock (game.Projectiles)
			{
				int index = game.Projectiles.FindIndex(p => p.id == projectileId);
				if (index != notFound)
				{
					game.Projectiles.RemoveAt(index);
				}
			}
		}
		private void KillPlayer(Data received)
		{
			Player player = null;
			int playerId = received.Integer1;
			int killedBy = received.Integer2;
			int index = notFound;
			lock (game.Players)
			{
				index = game.Players.FindIndex(p => p.PlayerId == playerId);
				if (index != notFound)
				{
					player = game.Players[index];
					player.deaths++;
					game.Players.RemoveAt(index);

					int killedByIndex = game.Players.FindIndex(p => p.PlayerId == killedBy);
					game.Players[killedByIndex].kills++;
				}
			}
			if (index != notFound)
			{
				lock (game.DeadPlayers)
				{
					game.DeadPlayers.Add(player);
				}
			}
		}
		private void RespawnPlayer(Data received)
		{
			(int playerId, _, float x, float y) = received.Values;
			Player player = null;
			int index = notFound;
			lock (game.DeadPlayers)
			{
				index = game.DeadPlayers.FindIndex(p => p.PlayerId == playerId);
				if (index != notFound)
				{
					player = game.DeadPlayers[index];
					game.DeadPlayers.RemoveAt(index);
				}
			}
			if (index != notFound)
			{
				player.isAlive = true;
				player.X = x;
				player.Y = y;
				lock (game.Players)
				{
					game.Players.Add(player);
				}
			}
		}
		private void SpawnBox(Data received)
		{
			(int boxId, byte b, float x, float y) = received.Values;
			WeaponType weaponType = (WeaponType)b;
			lock (game.Boxes)
			{
				int index = game.Boxes.FindIndex(b => b.Id == boxId);
				if (index == notFound)
				{
					ICollectableObject newBox = WeaponBox.Generate(boxId, x, y, form, weaponType);
					game.Boxes.Add(newBox);
				}
			}
		}
		private void CollectBox(Data received)
		{
			int boxId = received.Integer1;
			int collectedBy = received.Integer2;
			ICollectableObject collectedBox = null;
			int index = notFound;
			lock (form.Game.Boxes)
			{
				index = form.Game.Boxes.FindIndex(b => b.Id == boxId);
				if (index != notFound)
				{
					collectedBox = form.Game.Boxes[index];
					form.Game.Boxes.RemoveAt(index);
				}
			}
			if (collectedBox != null)
			{
				lock (game.Players)
				{
					index = form.Game.Players.FindIndex(p => p.PlayerId == collectedBy);
					if (index != notFound)
					{
						form.Game.Players[index].ChangeWeapon(collectedBox.WeaponContained);
					}
				}
			}
		}

		private BackgroundWorker bwInGameListener;

		private void ActionByCommandInitializer()
		{
			ActionByCommand = new Dictionary<Command, Action<Data>>()
			{
				{ Command.HostTick, HostTick },
				{ Command.HostPlayerMovement, MoveOrAddPlayer },
				{ Command.HostPlayerFire, MoveOrAddProjectile },
				{ Command.HostDestroyProjectile, DestroyProjectile },
				{ Command.HostKillPlayer, KillPlayer},
				{ Command.HostPlayerRespawn, RespawnPlayer },
				{ Command.HostBoxSpawned, SpawnBox },
				{ Command.HostBoxCollected, CollectBox }
			};

		}
		private Dictionary<Command, Action<Data>> ActionByCommand;

		/// <summary>
		/// Alters game state based commands sent by host.
		/// </summary>
		private void IGL_ProcessHostCommands(object sender, DoWorkEventArgs e) //pokud dlouho nedostanu odpoved od serveru(skoncil), mohl bych ukazat skore sam od sebe
		{
			ActionByCommandInitializer();
			while (true)
			{
				Data received = Data.GetData(udpClient.Receive(ref hostEP));
				if (ActionByCommand.TryGetValue(received.Cmd, out Action<Data> action))
				{
					action(received);
				}
				else if (received.Cmd == Command.HostGameEnded) return;
				
			}
		}
		/// <summary>
		/// Redraws form. Sends playerMovement and weaponState info to host.
		/// </summary>
		private void IGL_RedrawProgress(object sender, ProgressChangedEventArgs e)
		{
			form.Invalidate(); //redraws form

			//sends info about movement
			byte[] clientMove = Data.ToBytes(Command.ClientMove, (byte)form.playerMovement);
			SendMessageToTarget(clientMove, hostEP);

			//sends info about weapon
			byte[] clientFire = Data.ToBytes(Command.ClientFire, (byte)form.weaponState);
			SendMessageToTarget(clientFire, hostEP);
		}

		private void IGL_DisplayScore(object sender, RunWorkerCompletedEventArgs e)
		{
			form.ChangeGameState(GameState.GameScore);
			form.BackColor = Color.White;
			form.Invalidate();

			List<Player> allPlayers = form.Game.Players.Concat(form.Game.DeadPlayers).ToList();
			string score = "";
			foreach (var player in allPlayers)
			{
				if (player.PlayerId == myPlayerId)
				{
					score = $"(TY) id:{player.PlayerId} kills:{player.kills} deaths:{player.deaths} \n" + score;
				}
				else
				{
					score += $"id:{player.PlayerId} kills:{player.kills} deaths:{player.deaths} \n";
				}
			}
			form.lScore.Text = score;
		}

	}

	static class ParallelBW
	{
		public static void ActivateWorker(ref BackgroundWorker worker, bool reportsProgress, DoWorkEventHandler work,
			RunWorkerCompletedEventHandler completed, ProgressChangedEventHandler progress = null, object runArg = null)
		{
			worker = new BackgroundWorker() { WorkerReportsProgress = reportsProgress };
			worker.DoWork += work;
			if (progress != null)
			{
				worker.ProgressChanged += progress;
			}
			worker.RunWorkerCompleted += completed;
			worker.RunWorkerAsync(runArg);
		}
	}
}
