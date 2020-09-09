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

		private readonly static Dictionary<Command, Func<byte[], Data>> Commands = new Dictionary<Command, Func<byte[], Data>>()
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
			if (Commands.TryGetValue(cmd, out Func<byte[], Data> func))
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
	class Host
	{
		private readonly FormBambulanci form;
		public Host(FormBambulanci form) => this.form = form;

		public List<ClientInfo> clientList;
		private UdpClient udpHost;
		public int ListenPort { get; private set; }

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
			udpHost.Send(cancel, cancel.Length, new IPEndPoint(IPAddress.Loopback, ListenPort));
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
						TargetMessage(hostLoginAccepted, clientEP);
						break;
					case Command.ClientFindServers:
						byte[] hostFoundServer = Data.ToBytes(Command.HostFoundServer);
						udpHost.Send(hostFoundServer, hostFoundServer.Length, clientEP);
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

		public void BroadcastMessage(byte[] message)///x3---test--------------------------------------------------------------
		{
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, Client.listenPort));
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, Client.listenPort));
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, Client.listenPort));
		}

		/// <summary>
		/// Broadcast on network and localhost.
		/// </summary>
		public void LocalhostAndBroadcastMessage(byte[] message)///x3---test--------------------------------------------------------------
		{
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, Client.listenPort));
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Loopback, Client.listenPort));

			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, Client.listenPort));
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Loopback, Client.listenPort));

			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, Client.listenPort));
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Loopback, Client.listenPort));
		}

		public void TargetMessage(byte[] message, IPEndPoint targetEP)///x3---test--------------------------------------------------------------
		{
			udpHost.Send(message, message.Length, targetEP);
			udpHost.Send(message, message.Length, targetEP);
			udpHost.Send(message, message.Length, targetEP);
		}

		private BackgroundWorker bwGameListener;
		public void StartGameListening()
		{
			ParallelBW.ActivateWorker(ref bwGameListener, false, GL_DoWork, GL_Completed);
		}

		/// <summary>
		/// Game Listener's work.
		/// Processes packets from clients in game and alters current Game state based on them.
		/// </summary>
		private void GL_DoWork(object sender, DoWorkEventArgs e)
		{
			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, ListenPort);

			while (form.GameTime > 0)
			{
				Data data = Data.GetData(udpHost.Receive(ref clientEP));
				switch (data.Cmd)
				{
					case Command.ClientMove:
						Direction playerMovement = (Direction)data.B;
						lock (form.Game.Players)
						{
							Player senderPlayer = form.Game.Players.Find(p => p.ipEndPoint.Equals(clientEP));
							if(senderPlayer != null)
								senderPlayer.MoveByHost(playerMovement, form);
						}
						break;
					case Command.ClientFire:
						WeaponState weaponState = (WeaponState)data.B;
						lock (form.Game.Players)
						{
							Player senderPlayer = form.Game.Players.Find(p => p.ipEndPoint.Equals(clientEP));
							if (senderPlayer != null)
								senderPlayer.Weapon.Fire(weaponState);
						}
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

	class Client
	{
		private readonly FormBambulanci form;
		private const int notUsed = 0;
		private const int notFound = -1;

		public bool InGame;
		private int myPlayerId;
		public IPEndPoint hostEP;

		public Client(FormBambulanci form)
		{
			this.form = form;
		}

		private UdpClient udpClient;
		public const int listenPort = 60000; //wont allow multiple servers

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
			udpClient.Send(poisonPill, poisonPill.Length, localhostEP);
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
			udpClient.Send(findServerMessage, findServerMessage.Length, broadcastEP);

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
			udpClient.Send(loginMessage, loginMessage.Length, hostEP);

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
		
		public void HW_WaitingCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (InGame)
			{
				form.ChangeGameState(GameState.InGame);
				ParallelBW.ActivateWorker(ref bwInGameListener, true, IGL_DoWork, IGL_DisplayScore, IGL_RedrawProgress);
			}
		}

		private BackgroundWorker bwInGameListener;
		private void IGL_DoWork(object sender, DoWorkEventArgs e)
		{
			Game game = form.Game;
			while (true)
			{
				Data received = Data.GetData(udpClient.Receive(ref hostEP));
				switch (received.Cmd)
				{
					case Command.HostTick:
						bwInGameListener.ReportProgress(notUsed);
						break;
					case Command.HostPlayerMovement:
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
						break;
					case Command.HostPlayerFire:
						int projectileId;
						(projectileId, direction, x, y) = received.Values;
						lock (game.Projectiles)
						{
							index = game.Projectiles.FindIndex(p => p.id == projectileId);
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
						break;
					case Command.HostDestroyProjectile:
						projectileId = received.Integer1;
						lock (game.Projectiles)
						{
							index = game.Projectiles.FindIndex(p => p.id == projectileId);
							if (index != notFound)
							{
								game.Projectiles.RemoveAt(index);
							}
						}
						break;
					case Command.HostKillPlayer:
						Player player = null;
						playerId = received.Integer1;
						int killedBy = received.Integer2;
						lock(game.Players)
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
						if(index != notFound)
						{
							lock (game.DeadPlayers)
							{
								game.DeadPlayers.Add(player);
							}
						}
						break;
					case Command.HostPlayerRespawn:
						(playerId, _, x, y) = received.Values;
						player = null;
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
						break;
					case Command.HostBoxSpawned:
						int boxId;
						byte b;
						(boxId, b, x, y) = received.Values;
						WeaponType weaponType = (WeaponType)b;
						lock (game.Boxes)
						{
							index = game.Boxes.FindIndex(b => b.Id == boxId);
							if (index == notFound)
							{
								ICollectableObject newBox = WeaponBox.Generate(boxId, x, y, form, weaponType);
								game.Boxes.Add(newBox);
							}
						}
						break;
					case Command.HostBoxCollected:
						boxId = received.Integer1;
						int collectedBy = received.Integer2;
						ICollectableObject collectedBox = null;
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
						break;
					case Command.HostGameEnded: //pokud dlouho nedostanu odpoved od serveru(skoncil), mohl bych ukazat skore sam od sebe
						return;
					default:
						break;
				}
			}
		}
		private void IGL_RedrawProgress(object sender, ProgressChangedEventArgs e)
		{
			form.Invalidate(); //redraws form

			//sends info about movement
			byte[] clientMove = Data.ToBytes(Command.ClientMove, (byte)form.playerMovement);
			udpClient.Send(clientMove, clientMove.Length, hostEP);

			//sends info about weapon
			byte[] clientFire = Data.ToBytes(Command.ClientFire, (byte)form.weaponState);
			udpClient.Send(clientFire, clientFire.Length, hostEP);
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
