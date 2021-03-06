﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Bambulanci
{
	/// <summary>
	/// Client___ - commands sent by client.
	/// Host___ - commands sent by host.
	/// </summary>
	enum Command {
		
		//Connecter commands
		ClientLogin, ClientFindServers, ClientStopRefreshing,
		HostFoundServer, HostMoveToWaitingRoom, HostStopBroadcasting,
		HostLoginAccepted, HostStartGame,
		
		//InGame commands
		HostTick,
		ClientMove, HostPlayerMovement,
		ClientFire, HostPlayerFire,
		HostDestroyProjectile, HostKillPlayer, HostPlayerRespawn,
		HostBoxSpawned, HostBoxCollected,
		HostGameEnded, HostScore
	}
	
	/// <summary>
	/// Data serializer and deserializer for network communication.
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
			Data result = new Data(data[0])
			{
				B = data[1]
			};
			return result;
		}
		private static Data ReconstructInt(byte[] data)
		{
			Data result = new Data(data[0])
			{
				Integer1 = BitConverter.ToInt32(data, 1)
			};
			return result;
		}
		private static Data ReconstructInts(byte[] data)
		{
			Data result = new Data(data[0])
			{
				Integer1 = BitConverter.ToInt32(data, 1),
				Integer2 = BitConverter.ToInt32(data, 5)
			};
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

		public static Data GetDataTcp(NetworkStream stream)
		{
			byte[] bytes = new byte[64];
			stream.Read(bytes, 0, bytes.Length);
			return GetData(bytes);
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

		public void BroadcastMessage(byte[] message)
		{
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, Client.listenPort));
		}

		/// <summary>
		/// Broadcast on network and localhost.
		/// </summary>
		public void LocalhostAndBroadcastMessage(byte[] message)
		{
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, Client.listenPort));
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Loopback, Client.listenPort));
		}

		public void SendMessageToTarget(byte[] message, IPEndPoint targetEP)
		{
			udpHost.Send(message, message.Length, targetEP);
		}
		public void SendMessageToTargetTCP(NetworkStream stream, byte[] msg)
		{
			stream.Write(msg, 0, msg.Length);
		}

	}
	public struct ClientInfo
	{
		public TcpClient TcpClient { get; set; }
		public IPEndPoint IpEndPoint { get; set; }
		public int Id { get; set; }
		public NetworkStream Stream { get; set; }
		public ClientInfo(TcpClient client, IPEndPoint ipEndPoint, int id, NetworkStream stream)
		{
			this.TcpClient = client;
			this.IpEndPoint = ipEndPoint;
			this.Id = id;
			this.Stream = stream;
		}
	}

	class HostConnecter : Host
	{
		public HostConnecter(FormBambulanci form) : base(form) { }

		private TcpListener tcpHost;
		public List<ClientInfo> ConnectedClients { get; set; }

		private BackgroundWorker bwBroadcastResponder;

		/// <summary>
		/// Sends message to client who sent broadcast.
		/// </summary>
		private void BR_RespondToBroadcast(object sender, DoWorkEventArgs e)
		{
			bool broadcastStopped = false;
			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, ListenPort);
			while (!broadcastStopped)
			{
				Data data = Data.GetData(udpHost.Receive(ref clientEP));
				switch (data.Cmd)
				{
					case Command.ClientFindServers:
						byte[] hostFoundServer = Data.ToBytes(Command.HostFoundServer);
						SendMessageToTarget(hostFoundServer, clientEP);
						break;
					case Command.HostStopBroadcasting:
						broadcastStopped = true;
						break;
					default:
						break;
				}
			}
		}

		private BackgroundWorker bwClientWaiter;

		/// <summary>
		/// Closes existing udpHost and starts listening for incomming clients on selected port.
		/// </summary>
		/// <param name="numOfPlayers"> Selected number of players in game. </param>
		/// <param name="listenPort"> Selected listening port. </param>
		public void BWStartClientWaiter(int numOfPlayers, int listenPort)
		{
			if(udpHost != null)
			{
				udpHost.Close();
				udpHost = null;
			}
			ListenPort = listenPort;
			ParallelBW.ActivateWorker(ref bwClientWaiter, true, CW_WaitForClients, CW_Completed, CW_UpdateRemainingPlayers, (numOfPlayers));
		}

		/// <summary>
		/// Stops backgroundWorkers broadcastingResponder and clientWaiter.
		/// </summary>
		public void BWCancelHost()
		{
			BWCancelBroadcastResponder();
			BWCancelClientWaiter();
		}

		private void BWCancelBroadcastResponder()
		{
			byte[] hostStopBroadcasting = Data.ToBytes(Command.HostStopBroadcasting);
			IPEndPoint localHost = new IPEndPoint(IPAddress.Loopback, ListenPort);
			SendMessageToTarget(hostStopBroadcasting, localHost);
		}

		/// <summary>
		/// Throws exception on ClientWaiter.
		/// </summary>
		private void BWCancelClientWaiter()
		{
			tcpHost.Stop();	
		}

		/// <summary>
		/// Waits for numOfPlayers to connect to host.
		/// Information about each connected client is saved under ConnectedClients list.
		/// 
		/// Catching socket exception stops this worker and closes all tcp connections.
		/// </summary>
		private void CW_WaitForClients(object sender, DoWorkEventArgs e)
		{
			int numOfPlayers = (int)e.Argument;

			ConnectedClients = new List<ClientInfo>();
			IPEndPoint hostEP = new IPEndPoint(IPAddress.Any, ListenPort);
			udpHost = new UdpClient(hostEP);
			tcpHost = new TcpListener(hostEP);
			tcpHost.Start();

			ParallelBW.ActivateWorker(ref bwBroadcastResponder, false, BR_RespondToBroadcast, null);

			int id = FormBambulanci.hostId + 1;
			try
			{
				while (ConnectedClients.Count < numOfPlayers)
				{
					TcpClient client = tcpHost.AcceptTcpClient();
					NetworkStream stream = client.GetStream();
					Data data = Data.GetDataTcp(stream);
					if (data.Cmd == Command.ClientLogin)
					{
						ClientInfo clientInfo = new ClientInfo(client, (IPEndPoint)client.Client.RemoteEndPoint, id, stream);
						ConnectedClients.Add(clientInfo);
						UpdateRemainingPlayers(numOfPlayers);
						id++;
						byte[] hostLoginAccepted = Data.ToBytes(Command.HostLoginAccepted);
						SendMessageToTargetTCP(stream, hostLoginAccepted);
					}
				}
			}
			catch (SocketException)
			{
				e.Cancel = true;
				foreach (var client in ConnectedClients)
				{
					client.Stream.Close();
					client.TcpClient.Close();
				}
			}
		}

		/// <summary>
		/// Reports progress of bwClientWaiter.
		/// </summary>
		private void UpdateRemainingPlayers(int numOfPlayers)
		{
			int remainingPlayers = numOfPlayers - ConnectedClients.Count;
			bwClientWaiter.ReportProgress(remainingPlayers);
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
			if (e.Error == null && !e.Cancelled)
			{
				BWCancelBroadcastResponder();
				byte[] moveClientsToWaitingRoom = Data.ToBytes(Command.HostMoveToWaitingRoom);
				foreach (var client in ConnectedClients)
				{
					client.Stream.Write(moveClientsToWaitingRoom, 0, moveClientsToWaitingRoom.Length);
				}
				form.ChangeGameState(GameState.HostWaitingRoom);
			}
		}

		public HostInGame StartIngameHost()
		{
			return new HostInGame(form, udpHost, ListenPort);
		}
	}

	class HostInGame : Host
	{
		public HostInGame(FormBambulanci form, UdpClient udpHost, int listenport) : base(form)
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

		private void FirePlayersWeapon(WeaponState weaponState, IPEndPoint clientEP)
		{
			Player senderPlayer;
			lock (form.Game.Players)
			{
				senderPlayer = form.Game.Players.Find(p => p.ipEndPoint.Equals(clientEP));
			}
			if (senderPlayer != null)
			{
				senderPlayer.FireWeapon(weaponState);
			}
		}
		private void MovePlayer(Direction playerMovement, IPEndPoint clientEP)
		{
			Player senderPlayer;
			lock (form.Game.Players)
			{
				senderPlayer = form.Game.Players.Find(p => p.ipEndPoint.Equals(clientEP));
			}
			if (senderPlayer != null && playerMovement != Direction.Stay)
			{
				senderPlayer.MoveByHost(playerMovement);
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
						MovePlayer(playerMovement, clientEP);
						break;
					case Command.ClientFire:
						WeaponState weaponState = (WeaponState)data.B;
						FirePlayersWeapon(weaponState, clientEP);
						break;
					default:
						break;
				}
			}
		}

		/// <summary>
		/// Ends game. Inform clients about game score. Stops clients backgroundWorkers.
		/// 
		/// Datagram might not be received by client, because it's send over UDP.
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

		/// <summary>
		/// Setting client's port static for all clients forbids running multiple servers at one time.
		/// However static port enables broadcasting from host.
		/// </summary>
		public const int listenPort = 60000;

		protected readonly FormBambulanci form;
		protected UdpClient udpClient;
		public IPEndPoint hostEP;

		protected int myPlayerId;

		public Client(FormBambulanci form) => this.form = form;

		public void SendMessageToTarget(byte[] message, IPEndPoint targetEP)
		{
			udpClient.Send(message, message.Length, targetEP);
		}

	}

	class ClientConnecter : Client
	{
		private TcpClient tcpClient;
		private NetworkStream streamToHost;

		public ClientConnecter(FormBambulanci form) : base(form) { }

		public void StartUdpClient(IPAddress iPAddress) => udpClient = new UdpClient(new IPEndPoint(iPAddress, listenPort));
		
		
		public void StopUdpClient()
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
		/// Each responding host is then added to server listBox.
		/// </summary>
		private void SR_RefreshServers(object sender, DoWorkEventArgs e)
		{
			int hostPort = (int)e.Argument;
			IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, hostPort);
			IPEndPoint hostEPVar = new IPEndPoint(IPAddress.Any, listenPort);

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

		/// <summary>
		/// Add server to listbox.
		/// Select first server on the list to avoid "server not selected" login errors.
		/// </summary>
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
		private Data WaitForCommandTCP(Command command)
		{
			Data data = Data.GetDataTcp(streamToHost);
			while (data.Cmd != command)
			{
				data = Data.GetDataTcp(streamToHost);
			}
			return data;
		}

		/// <summary>
		/// Stops serverRefresher in backgroud.
		/// </summary>
		public void LoginToSelectedServer()
		{
			ServerRefresherStop();

			hostEP = (IPEndPoint)form.lBServers.SelectedItem;

			tcpClient = new TcpClient();
			tcpClient.Connect(hostEP);
			streamToHost = tcpClient.GetStream();

			byte[] loginMessage = Data.ToBytes(Command.ClientLogin);
			streamToHost.Write(loginMessage, 0, loginMessage.Length);

			WaitForCommandTCP(Command.HostLoginAccepted);
			form.ChangeGameState(GameState.ClientWaiting);
			
			ParallelBW.ActivateWorker(ref bwHostWaiter, true, HW_ClientWaiting, null, HW_WaitingProgress);
		}

		private BackgroundWorker bwHostWaiter;

		/// <summary>
		/// Waiting for other players to connect and for host to start game.
		/// Closes TCP connection.
		/// </summary>
		private void HW_ClientWaiting(object sender, DoWorkEventArgs e)
		{
			WaitForCommandTCP(Command.HostMoveToWaitingRoom);
			bwHostWaiter.ReportProgress((int)Command.HostMoveToWaitingRoom);

			Data received = WaitForCommandTCP(Command.HostStartGame);
			streamToHost.Close();
			tcpClient.Close();
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
					myPlayerId = (int)e.UserState;
					StartIngameClient();
					break;
				default:
					break;
			}
		}
		
		private void StartIngameClient()
		{
			form.ChangeGameState(GameState.InGame);
			ClientInGame.StartClient(form, udpClient, hostEP, myPlayerId);
		}

		/// <summary>
		/// Start host's in-game client.
		/// </summary>
		public void HostStartIngameClient(int sendPort)
		{
			form.ChangeGameState(GameState.InGame);
			UdpClient udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, listenPort));
			IPEndPoint hostEP = new IPEndPoint(IPAddress.Loopback, sendPort);
			ClientInGame.StartClient(form, udpClient, hostEP, myPlayerId);
		}
	}

	class ClientInGame : Client
	{
		Game game;
		private ClientInGame(FormBambulanci form) : base(form) { }
		public static void StartClient(FormBambulanci form, UdpClient udpClient, IPEndPoint hostEP, int myPlayerId)
		{
			ClientInGame client = new ClientInGame(form)
			{
				udpClient = udpClient,
				hostEP = hostEP,
				myPlayerId = myPlayerId,
				game = form.Game
			};
			client.StartGameListening();
		}

		private void StartGameListening()
		{
			ParallelBW.ActivateWorker(ref bwGameListener, true, IGL_ProcessHostCommands, IGL_DisplayScore, IGL_RedrawProgress);
		}


		private Dictionary<Command, Action<Data>> ActionByCommand;
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

		private void HostTick(Data ignored)
		{
			bwGameListener.ReportProgress(notUsed);
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
					game.Players.Add(new Player(form.Game, x, y, playerId, (Direction)direction));
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
					game.Projectiles.Add(new Projectile(game, x, y, (Direction)direction, projectileId));
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
			Player deadPlayer = null;
			Player killingPlayer = null;
			int playerId = received.Integer1;
			int killedBy = received.Integer2;
			lock (game.Players)
			{
				int index = game.Players.FindIndex(p => p.PlayerId == playerId);
				deadPlayer = game.Players[index];
				if (deadPlayer != null)
				{
					deadPlayer.Deaths++;
				}
				game.Players.RemoveAt(index);
				lock (game.DeadPlayers)
				{
					game.DeadPlayers.Add(deadPlayer);
				}

				killingPlayer = game.Players.Find(p => p.PlayerId == killedBy);
				if (killingPlayer != null) 
				{
					killingPlayer.Kills++;
				}
				else
				{
					lock (game.DeadPlayers)
					{
						killingPlayer = game.DeadPlayers.Find(p => p.PlayerId == killedBy);
						if(killingPlayer != null)
						{
							killingPlayer.Kills++;
						}
					}
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
				player.IsAlive = true;
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
			(int boxId, _, _, _) = received.Values;
			lock (game.Boxes)
			{
				int index = game.Boxes.FindIndex(b => b.Id == boxId);
				if (index == notFound)
				{
					ICollectableObject newBox = WeaponBox.Generate(received);
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

		private BackgroundWorker bwGameListener;

		/// <summary>
		/// Alters game state based commands sent by host.
		/// </summary>
		private void IGL_ProcessHostCommands(object sender, DoWorkEventArgs e)
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

		/// <summary>
		/// End of game.
		/// </summary>
		private void IGL_DisplayScore(object sender, RunWorkerCompletedEventArgs e)
		{
			form.ChangeGameState(GameState.GameScore);
			form.BackColor = Color.White;
			form.Invalidate();

			string score = "";
			foreach (var player in AllPlayers())
			{
				if (player.PlayerId == myPlayerId)
				{
					score = $"(TY) id:{player.PlayerId} zabití:{player.Kills} úmrtí:{player.Deaths} \n" + score;
				}
				else
				{
					score += $"\n id:{player.PlayerId} zabití:{player.Kills} úmrtí:{player.Deaths} \n";
				}
			}
			form.lScore.Text = score;

			//center score controls.
			int lScoreX = (FormBambulanci.WidthStatic - form.lScore.Width) / 2;
			int lScoreY = FormBambulanci.HeightStatic / 8;
			int bExitX = (FormBambulanci.WidthStatic - form.bExit.Width) / 2;
			int bExitY = FormBambulanci.HeightStatic * 4 / 5;
			form.lScore.Location = new Point(lScoreX, lScoreY);
			form.bExit.Location = new Point(bExitX, bExitY);
		}

		private IEnumerable<Player> AllPlayers()
		{
			foreach (var player in form.Game.Players)
			{
				yield return player;
			}
			foreach (var deadPlayer in form.Game.DeadPlayers)
			{
				yield return deadPlayer;
			}
		}
	}

	/// <summary>
	/// Class for activating background workers.
	/// </summary>
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
