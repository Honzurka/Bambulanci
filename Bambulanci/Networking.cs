using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Drawing;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;

namespace Bambulanci
{
	enum Command { ClientLogin, ClientLogout, ClientFindServers, ClientStopRefreshing,
		HostFoundServer, HostMoveToWaitingRoom, HostCanceled, HostStopHosting,
		HostLoginAccepted, HostLoginDeclined, HostStartGame,
		
		HostTick, ClientMove, HostPlayerMovement
	}
	
	/// <summary>
	/// Data parser for network communication.
	/// </summary>
	class Data //should be more sofisticated---------------------------
	{
		public Command Cmd { get; private set; }
		public string Msg { get; private set; }
		public byte b;
		public ValueTuple<int, byte, float, float> movementInfo;
		public Data(byte[] data)
		{
			//1B command
			Cmd = (Command)data[0];
			if (Cmd == Command.HostPlayerMovement)
			{
				int id = BitConverter.ToInt32(data, 1); //idea: shots could have negative id so i can reuse code
				byte direction = data[5];
				float x = BitConverter.ToSingle(data, 6);
				float y = BitConverter.ToSingle(data, 10);
				movementInfo = (id, direction, x, y);
			}
			else if (Cmd == Command.ClientMove)
			{
				b = data[1];
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

		public static byte[] ToBytes(Command cmd, string msg = null, byte b = 0, (int id, byte direction, float x, float y) values = default)
		{
			List<byte> result = new List<byte>() { (byte)cmd };
			if (cmd == Command.HostPlayerMovement)
			{
				result.AddRange(BitConverter.GetBytes(values.id));
				result.Add(values.direction);
				result.AddRange(BitConverter.GetBytes(values.x));
				result.AddRange(BitConverter.GetBytes(values.y));
			}

			if (cmd == Command.ClientMove)
			{
				result.Add(b);
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
			public Player player; //inGame descriptor
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
			ParallelBW.ActivateWorker(ref bwHostStarter, true, BW_DoWork, BW_ProgressChanged, BW_RunWorkerCompleted, new ValueTuple<int, int>(numOfPlayers, listenPort));
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

		public void BroadcastMessage(byte[] message) //used only by BW_RUnWOrkerCompleted
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
			ParallelBW.ActivateWorker(ref bwGameListener, true, GL_DoWork, GL_Progress, GL_Completed);
		}

		/// <summary>
		/// Game Listener's work.
		/// Processes packets from clients in game.
		/// </summary>
		private void GL_DoWork(object sender, DoWorkEventArgs e)
		{
			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, ListenPort);
			while (true)
			{
				Data data = new Data(udpHost.Receive(ref clientEP));
				switch (data.Cmd)
				{
					case Command.ClientMove: //moves player who sends me command
						PlayerMovement playerMovement = (PlayerMovement)data.b;
						foreach (var client in clientList)
						{
							if (client.IpEndPoint.Equals(clientEP)) //find client who send me move command
							{
								client.player.Move(playerMovement);
							}
						}
						break;
					default:
						break;
				}
			}
		}
		private void GL_Progress(object sender, ProgressChangedEventArgs e)
		{

		} //not used
		private void GL_Completed(object sender, RunWorkerCompletedEventArgs e)
		{

		} //not used

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
			ParallelBW.ActivateWorker(ref bwServerRefresher, true, BW_RefreshServers, BW_ServerFound, BW_RefreshCompleted, hostPort);
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

			ParallelBW.ActivateWorker(ref bwHostWaiter, true, BW_ClientWaiting, BW_WaitingProgress, BW_WaitingCompleted);
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
				ParallelBW.ActivateWorker(ref bwInGameListener, true, IGL_DoWork, IGL_RedrawProgress, IGL_Completed);
			}
			//else==hostCanceled not implemented

		}

		BackgroundWorker bwInGameListener;
		public struct ImageWithLocation
		{
			private readonly Bitmap image;
			private readonly float x;
			private readonly float y;
			public ImageWithLocation(Bitmap image, float x, float y)
			{
				this.image = image;
				this.x = x;
				this.y = y;
			}
			public void Draw(Graphics g, int formWidth, int formHeight)
			{
				g.DrawImage(image, x * formWidth, y * formHeight);
			}
		}
		public ConcurrentQueue<ImageWithLocation> toBeDrawn;
		private void IGL_DoWork(object sender, DoWorkEventArgs e)
		{
			toBeDrawn = new ConcurrentQueue<ImageWithLocation>();
			while (true)
			{
				Data received = new Data(udpClient.Receive(ref hostEP));
				switch (received.Cmd)
				{
					case Command.HostTick:
						bwInGameListener.ReportProgress(0); //0 not needed
						break;
					case Command.HostPlayerMovement:
						(int playerId, byte direction, float x, float y) = received.movementInfo;

						Bitmap playerDesign = form.graphicsDrawer.GetPlayerDesign(playerId, direction);
						toBeDrawn.Enqueue(new ImageWithLocation(playerDesign, x, y));
						break;
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
		}
		private void IGL_Completed(object sender, RunWorkerCompletedEventArgs e)//not used yet -due to infinite ingame loop
		{

		}

	}

	class ParallelBW
	{
		private ParallelBW() { }

		public static void ActivateWorker(ref BackgroundWorker worker, bool reportsProgress, DoWorkEventHandler work,
			   ProgressChangedEventHandler progress, RunWorkerCompletedEventHandler completed, object runArg = null)
		{
			worker = new BackgroundWorker() { WorkerReportsProgress = reportsProgress };
			worker.DoWork += work;
			worker.ProgressChanged += progress;
			worker.RunWorkerCompleted += completed;

			worker.RunWorkerAsync(runArg);
		}
	}
}
