using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Bambulanci
{
	enum Command { ClientLogin, ClientLogout, ClientFindServers, ClientStopRefreshing,
		HostFoundServer, HostMoveToWaitingRoom, HostCanceled, HostStopHosting,
		HostLoginAccepted, HostLoginDeclined, HostStartGame,
		
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
		public string Msg { get; private set; }
		public byte b;
		public int integer;
		public int integer2;
		public ValueTuple<int, byte, float, float> ibffInfo;
		public Data(byte[] data)
		{
			//1B command
			Cmd = (Command)data[0];
			if (Cmd == Command.HostPlayerMovement || Cmd == Command.HostPlayerFire || Cmd == Command.HostPlayerRespawn || Cmd == Command.HostBoxSpawned)
			{
				int id = BitConverter.ToInt32(data, 1);
				byte direction = data[5]; //not only direction, also used for weaponType...------------
				float x = BitConverter.ToSingle(data, 6);
				float y = BitConverter.ToSingle(data, 10);
				ibffInfo = (id, direction, x, y);
			}
			else if (Cmd == Command.ClientMove || Cmd == Command.ClientFire)
			{
				b = data[1];
			}
			else if (Cmd == Command.HostDestroyProjectile)
			{
				integer = BitConverter.ToInt32(data, 1);
			}
			else if (Cmd == Command.HostBoxCollected || Cmd == Command.HostKillPlayer)
			{
				integer = BitConverter.ToInt32(data, 1);
				integer2 = BitConverter.ToInt32(data, 5);
			}
			else
			{
				if (data.Length > 1)
				{
					//4B msg length
					int msgLen = BitConverter.ToInt32(data, 1);

					//rest is message
					if (msgLen > 0)
					{
						Msg = Encoding.ASCII.GetString(data, 5, msgLen);
					}
				}
			}
		}

		public static byte[] ToBytes(Command cmd, string msg = null, byte b = 0, int integer = 0, int integer2 = 0, (int id, byte direction, float x, float y) values = default)
		{
			List<byte> result = new List<byte>() { (byte)cmd };
			if (cmd == Command.HostPlayerMovement || cmd == Command.HostPlayerFire || cmd == Command.HostPlayerRespawn || cmd == Command.HostBoxSpawned)
			{
				result.AddRange(BitConverter.GetBytes(values.id));
				result.Add(values.direction);
				result.AddRange(BitConverter.GetBytes(values.x));
				result.AddRange(BitConverter.GetBytes(values.y));
			}

			if (cmd == Command.ClientMove || cmd == Command.ClientFire)
			{
				result.Add(b);
			}

			if(cmd == Command.HostDestroyProjectile)
			{
				result.AddRange(BitConverter.GetBytes(integer));
			}
			if (cmd == Command.HostBoxCollected || cmd == Command.HostKillPlayer)
			{
				result.AddRange(BitConverter.GetBytes(integer));
				result.AddRange(BitConverter.GetBytes(integer2));
			}

			if (msg != null)
			{
				result.AddRange(BitConverter.GetBytes(msg.Length));
				result.AddRange(Encoding.ASCII.GetBytes(msg));
			}

			return result.ToArray();
		}
	}
	class Host
	{
		private readonly FormBambulanci form;
		public Host(FormBambulanci form) => this.form = form;

		private BackgroundWorker bwHostStarter;
		public List<ClientInfo> clientList;
		public class ClientInfo
		{
			public int Id { get; }
			public IPEndPoint IpEndPoint { get; }
			//public Player player; //inGame descriptor
			public ClientInfo(int id, IPEndPoint ipEndPoint)
			{
				this.Id = id;
				this.IpEndPoint = ipEndPoint;
			}

		}
		private UdpClient udpHost;

		public int ListenPort { get; private set; }

		/// <summary>
		/// Async - Starts BackgroundWorker who waits for numOfPlayers to connect to host.
		/// </summary>
		public void BWStartHostStarter(int numOfPlayers, int listenPort)
		{
			ListenPort = listenPort;
			ParallelBW.ActivateWorker(ref bwHostStarter, true, BW_DoWork, BW_RunWorkerCompleted, BW_ProgressChanged, new ValueTuple<int, int>(numOfPlayers, listenPort));
		}
		
		public void BWCancelHostStarter()
		{
			byte[] cancel = Data.ToBytes(Command.HostStopHosting);
			udpHost.Send(cancel, cancel.Length, new IPEndPoint(IPAddress.Loopback, ListenPort));
		}

		/// <summary>
		/// Reports progress of bwHostStarter.
		/// </summary>
		private void UpdateRemainingPlayers(int numOfPlayers)
		{
			int remainingPlayers = numOfPlayers - clientList.Count;
			bwHostStarter.ReportProgress(remainingPlayers);
		}

		private void BW_DoWork(object sender, DoWorkEventArgs e)
		{
			(int numOfPlayers, int listenPort) = (ValueTuple<int, int>)e.Argument;
			clientList = new List<ClientInfo>();
			udpHost = new UdpClient(new IPEndPoint(IPAddress.Any, listenPort));
			//udpHost.EnableBroadcast is false here, but im broadcasting -- where do i enable it???

			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, listenPort);
			int id = 1; //0 is host
			bool hostClosed = false;
			UpdateRemainingPlayers(numOfPlayers);
			while (!hostClosed && clientList.Count < numOfPlayers)
			{
				Data data = new Data(udpHost.Receive(ref clientEP));
				switch (data.Cmd)
				{
					case Command.ClientLogin:
						ClientInfo clientInfo = new ClientInfo(id, clientEP);
						clientList.Add(clientInfo);
						UpdateRemainingPlayers(numOfPlayers);
						id++;

						//confirmation for clients
						byte[] loginConfirmed = Data.ToBytes(Command.HostLoginAccepted);
						udpHost.Send(loginConfirmed, loginConfirmed.Length, clientEP);
						break;
					case Command.ClientLogout: //klient zatim neposila
						int clientID = Int32.Parse(data.Msg);
						clientList.RemoveAll(client => client.Id == clientID);
						UpdateRemainingPlayers(numOfPlayers);
						break;
					case Command.ClientFindServers:
						byte[] serverInfo = Data.ToBytes(Command.HostFoundServer);
						udpHost.Send(serverInfo, serverInfo.Length, clientEP);
						break;
					case Command.HostStopHosting:
						hostClosed = true;
						byte[] hostCanceledInfo = Data.ToBytes(Command.HostCanceled);
						foreach (var client in clientList)
						{
							udpHost.Send(hostCanceledInfo, hostCanceledInfo.Length, client.IpEndPoint);
						}
						udpHost.Close();
						e.Cancel = true;
						break;
					default:
						break;
				}
			}
		}

		private void BW_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			int remainingPlayers = e.ProgressPercentage;
			form.lWaiting.Text = $"Čekám na {remainingPlayers} hráče";
		}

		private void BW_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if(e.Error == null && !e.Cancelled)
			{
				byte[] moveClientToWaitingRoom = Data.ToBytes(Command.HostMoveToWaitingRoom);
				BroadcastMessage(moveClientToWaitingRoom); //bugged if sent to localHost also
				form.ChangeGameState(GameState.HostWaitingRoom);
			}
		}

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

		private BackgroundWorker bwGameListener;
		public void StartGameListening()
		{
			ParallelBW.ActivateWorker(ref bwGameListener, false, GL_DoWork, GL_Completed);
		}

		/// <summary>
		/// Game Listener's work.
		/// Processes packets from clients in game.
		/// </summary>
		private void GL_DoWork(object sender, DoWorkEventArgs e)
		{
			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, ListenPort);

			while (form.GameTime > 0)
			{
				Data data = new Data(udpHost.Receive(ref clientEP));
				switch (data.Cmd)
				{
					case Command.ClientMove: //moves player who sends me command
						Direction playerMovement = (Direction)data.b;
						lock (form.Game.Players)
						{
							foreach (var player in form.Game.Players)
							{
								if (player.ipEndPoint.Equals(clientEP)) //find client who send me move command
								{
									player.MoveByHost(playerMovement, form.Game.graphicsDrawer, form);
								}
							}
						}
						break;
					case Command.ClientFire:
						WeaponState weaponState = (WeaponState)data.b;
						lock (form.Game.Players)
						{
							foreach (var player in form.Game.Players)
							{
								if (player.ipEndPoint.Equals(clientEP))
								{
									player.Weapon.Fire(weaponState);
								}
							}
						}
						break;
					default:
						break;
				}
			}
		}
		private void GL_Completed(object sender, RunWorkerCompletedEventArgs e)
		{
			byte[] hostGameEnded = Data.ToBytes(Command.HostGameEnded);
			LocalhostAndBroadcastMessage(hostGameEnded); //stops clients BW

			//respawn dead players
			foreach (var deadPlayer in form.Game.DeadPlayers)
			{
				form.Game.Players.Add(deadPlayer);
			}

			//inform clients + DisplayScore();
			//send msg - base it of IpEndPoint
			

			string[] score = new string[form.Game.Players.Count];
			for (int i = 0; i < score.Length; i++)
			{
				Player player = form.Game.Players[i];
				score[i] = $"id:{player.PlayerId} kills:{player.kills} deaths:{player.deaths}";
			}

			for (int playerIndex = 0; playerIndex < form.Game.Players.Count; playerIndex++)
			{
				for (int msgIndex = 0; msgIndex < score.Length; msgIndex++)
				{
					string msg;
					if(playerIndex == msgIndex)
					{
						msg = "(You) : " + score[msgIndex];
					}
					else
					{
						msg = score[msgIndex];
					}
					byte[] msgBytes = Data.ToBytes(Command.HostScore, msg);
					udpHost.Send(msgBytes, msgBytes.Length, form.Game.Players[playerIndex].ipEndPoint);
					//Console.WriteLine(msg);
				}
			}


		}

	}

	class Client
	{
		private readonly FormBambulanci form;

		public bool InGame;
		public IPEndPoint hostEP;
		//public bool InGame { get; private set; }
		//private IPEndPoint hostEPGlobal;

		public Client(FormBambulanci form)
		{
			this.form = form;
		}

		private UdpClient udpClient;
		public const int listenPort = 60000;

		public void StartClient(IPAddress iPAddress)
		{
			udpClient = new UdpClient(new IPEndPoint(iPAddress, listenPort));
		}

		/// <summary>
		/// Server refresh paralelism.
		/// </summary>
		private BackgroundWorker bwServerRefresher;
		public void BWServerRefresherStart(int hostPort)
		{
			ParallelBW.ActivateWorker(ref bwServerRefresher, true, BW_RefreshServers, BW_RefreshCompleted, BW_ServerFound, hostPort);
		}

		private void BW_RefreshServers(object sender, DoWorkEventArgs e) //nekdy vyzkouset vice serveru - kvuli hostEP / co kdyz zrovna broadcastuje jiny server...
		{
			int hostPort = (int)e.Argument;
			IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, hostPort);

			var hostEPVar = new IPEndPoint(IPAddress.Any, listenPort);

			byte[] findServerMessage = Data.ToBytes(Command.ClientFindServers);
			udpClient.Send(findServerMessage, findServerMessage.Length, broadcastEP);

			bool searching = true;
			while (searching)
			{
				Data received = new Data(udpClient.Receive(ref hostEPVar));
				switch (received.Cmd)
				{
					case Command.HostFoundServer:
						bwServerRefresher.ReportProgress(0, hostEPVar); //0 is ignored
						break;
					case Command.ClientStopRefreshing:
						searching = false;
						break;
					default:
						//Console.WriteLine("my message got EATEN!!!------------------------");
						break;
				}
			}
		}

		private void BW_ServerFound(object sender, ProgressChangedEventArgs e) //progress
		{
			var newHostEP = e.UserState;
			form.lBServers.Items.Add(newHostEP);
			form.bLogin.Enabled = true;
			form.lBServers.SelectedIndex = 0;
			
		}
		private void BW_RefreshCompleted(object sender, RunWorkerCompletedEventArgs e) //not used 
		{

		}

		/// <summary>
		/// Logging to server stops refreshing servers in backgroud.
		/// </summary>
		public void LoginToSelectedServer()
		{
			//send poison pill on localHost == stops server refreshing backgroundWorker
			byte[] poisonPill = Data.ToBytes(Command.ClientStopRefreshing);
			IPEndPoint localhostEP = new IPEndPoint(IPAddress.Loopback, listenPort);
			udpClient.Send(poisonPill, poisonPill.Length, localhostEP);

			hostEP = (IPEndPoint)form.lBServers.SelectedItem;
			byte[] loginMessage = Data.ToBytes(Command.ClientLogin);			
			udpClient.Send(loginMessage, loginMessage.Length, hostEP);

			Data received = new Data(udpClient.Receive(ref hostEP));
			switch (received.Cmd)
			{
				case Command.HostLoginAccepted:
					form.ChangeGameState(GameState.ClientWaiting);
					break;
				case Command.HostLoginDeclined:
					//not implemented yet
					break;
				default:
					break;
			}

			ParallelBW.ActivateWorker(ref bwHostWaiter, true, BW_ClientWaiting, BW_WaitingCompleted, BW_WaitingProgress);
		}

		BackgroundWorker bwHostWaiter;

		private void BW_ClientWaiting(object sender, DoWorkEventArgs e) //hostCanceled is not implemented yet
		{
			Data received = new Data(udpClient.Receive(ref hostEP));

			//waiting for everyone to connect.
			while (received.Cmd != Command.HostCanceled && received.Cmd != Command.HostMoveToWaitingRoom) //while, but host sends cmd only 1x.
			{
				received = new Data(udpClient.Receive(ref hostEP));
			}
			bwHostWaiter.ReportProgress((int)received.Cmd);

			//waiting for host to start game
			while (received.Cmd != Command.HostCanceled && received.Cmd != Command.HostStartGame)
			{
				received = new Data(udpClient.Receive(ref hostEP));
			}
			bwHostWaiter.ReportProgress((int)received.Cmd);
		}

		private void BW_WaitingProgress(object sender, ProgressChangedEventArgs e)
		{
			Command cmd = (Command)e.ProgressPercentage;
			switch (cmd)
			{
				case Command.HostCanceled: //disconnect - not implemented yet
					break;
				case Command.HostMoveToWaitingRoom:
					form.ChangeGameState(GameState.ClientWaitingRoom);
					break;
				case Command.HostStartGame:
					InGame = true;
					break;
				default:
					break;
			}
		}
		
		public void BW_WaitingCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			//after startGame/hostCanceled
			if (InGame)
			{
				form.ChangeGameState(GameState.InGame);
				ParallelBW.ActivateWorker(ref bwInGameListener, true, IGL_DoWork, IGL_Completed, IGL_RedrawProgress);
			}
			//else==hostCanceled not implemented

		}

		BackgroundWorker bwInGameListener;
		private void IGL_DoWork(object sender, DoWorkEventArgs e)
		{
			while (true)
			{
				Data received = new Data(udpClient.Receive(ref hostEP));
				switch (received.Cmd)
				{
					case Command.HostTick:
						bwInGameListener.ReportProgress(0); //0 not needed
						break;
					case Command.HostPlayerMovement:
						(int playerId, byte direction, float x, float y) = received.ibffInfo;
						int index;
						lock (form.Game.Players)
						{
							index = form.Game.Players.FindIndex(p => p.PlayerId == playerId); //form.Game -- maybe should be Game. -----
							if (index == -1)
							{
								form.Game.Players.Add(new Player(form, x, y, playerId, (Direction)direction));
							}
							else
							{
								form.Game.Players[index].MoveByClient((Direction)direction, x, y);
							}
						}
						break;
					case Command.HostPlayerFire:
						int projectileId;
						(projectileId, direction, x, y) = received.ibffInfo;
						lock (form.Game.Projectiles)
						{
							index = form.Game.Projectiles.FindIndex(p => p.id == projectileId);
							if (index == -1)
							{
								form.Game.Projectiles.Add(new Projectile(x, y, (Direction)direction, projectileId, form));
							}
							else
							{
								form.Game.Projectiles[index].X = x;
								form.Game.Projectiles[index].Y = y;
							}
						}
						break;
					case Command.HostDestroyProjectile:
						projectileId = received.integer;
						lock (form.Game.Projectiles)
						{
							index = form.Game.Projectiles.FindIndex(p => p.id == projectileId);
							form.Game.Projectiles.RemoveAt(index);
						}
						break;
					case Command.HostKillPlayer:
						Player player;
						playerId = received.integer;
						int killedBy = received.integer2;
						index = form.Game.Players.FindIndex(p => p.PlayerId == playerId); //not locked...---
						if (index != -1)
						{
							lock (form.Game.Players)
							{
								player = form.Game.Players[index];
								player.deaths++;
								form.Game.Players.RemoveAt(index);

								int killedByIndex = form.Game.Players.FindIndex(p => p.PlayerId == killedBy);
								form.Game.Players[killedByIndex].kills++;
							}
							//prob lock deadPlayers too
							lock (form.Game.DeadPlayers)
							{
								form.Game.DeadPlayers.Add(player);
							}
						}
						break;
					case Command.HostPlayerRespawn:
						(playerId, _, x, y) = received.ibffInfo;
						index = form.Game.DeadPlayers.FindIndex(p => p.PlayerId == playerId); //not locked...
						if(index != -1)
						{
							lock (form.Game.DeadPlayers)
							{
								player = form.Game.DeadPlayers[index];
								form.Game.DeadPlayers.RemoveAt(index);
							}
							player.isAlive = true;
							player.X = x;
							player.Y = y;
							lock(form.Game.Players)
							{
								form.Game.Players.Add(player);
							}
						}
						break;
					case Command.HostBoxSpawned:
						int boxId;
						byte b;
						(boxId, b, x, y) = received.ibffInfo; //add implicit cast for WeaponType,Direction,....---------
						WeaponType weaponType = (WeaponType)b;

						lock (form.Game.Boxes)
						{
							//box should be spawned only once
							ICollectableObject newBox = WeaponBox.Generate(boxId,x,y,form,weaponType);
							form.Game.Boxes.Add(newBox);
						}
						break;
					case Command.HostBoxCollected:
						boxId = received.integer;
						int collectedBy = received.integer2;
						ICollectableObject collectedBox = null;
						lock (form.Game.Boxes)
						{
							index = form.Game.Boxes.FindIndex(b => b.Id == boxId);
							if (index != -1)
							{
								collectedBox = form.Game.Boxes[index];
								form.Game.Boxes.RemoveAt(index);
							}
						}
						if (collectedBox != null)
						{
							lock (form.Game.Players)
							{
								int playerIndex = form.Game.Players.FindIndex(p => p.PlayerId == collectedBy);
								form.Game.Players[playerIndex].ChangeWeapon(collectedBox.weaponContained);
							}
						}
						break;
					case Command.HostGameEnded:
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
			byte[] clientMove = Data.ToBytes(Command.ClientMove, b: (byte)form.playerMovement);
			udpClient.Send(clientMove, clientMove.Length, hostEP);

			//sends info about weapon
			byte[] clientFire = Data.ToBytes(Command.ClientFire, b: (byte)form.weaponState);
			udpClient.Send(clientFire, clientFire.Length, hostEP);
		}
		private void IGL_Completed(object sender, RunWorkerCompletedEventArgs e)//not used yet -due to infinite ingame loop
		{
			form.ChangeGameState(GameState.GameScore);
			string score = "";
			int numOfMessages = form.Game.Players.Count + form.Game.DeadPlayers.Count;
			while (numOfMessages > 0)
			{
				Data received = new Data(udpClient.Receive(ref hostEP));
				if (received.Cmd == Command.HostScore)
				{
					score += received.Msg + "\n";
					numOfMessages--;
				}
			}
			form.lScore.Text = score;
		}

	}

	class ParallelBW
	{
		private ParallelBW() { }

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
