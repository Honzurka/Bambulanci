using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq; //added
using System.Runtime.Serialization.Formatters.Binary;
using System.Drawing;
using System.Collections.Concurrent;
using System.DirectoryServices.ActiveDirectory;
using System.Net.NetworkInformation;

namespace Bambulanci
{
	enum Command { ClientLogin, ClientLogout, ClientFindServers, 
		HostFoundServer, HostMoveToWaitingRoom, HostCanceled, HostStopHosting,
		ClientStopRefreshing, HostLoginAccepted, HostLoginDeclined, HostStartGame,
		
		HostTick, ClientMove, HostPlayerMovement
	}
	public class ClientInfo //struct??
	{
		public int Id { get; }
		//private string name; //bez newline?
		//private Color color; //?
		public IPEndPoint IpEndPoint { get; }
		public Player player; //inGame descriptor
		public ClientInfo(int id, IPEndPoint ipEndPoint)
		{
			this.Id = id;
			this.IpEndPoint = ipEndPoint;
		}

	}
	
	/// <summary>
	/// Data parser for network communication.
	/// </summary>
	class Data
	{
		public Command Cmd { get; private set; }
		public string Msg { get; private set; }

		public Data(byte[] data)
		{
			//1B command
			Cmd = (Command)data[0];

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

		public static byte[] ToBytes(Command cmd)
		{
			return ToBytes(cmd, null);
		}

		public static byte[] ToBytes(Command cmd, string msg)
		{
			List<byte> result = new List<byte>();

			result.Add((byte)cmd);
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
		private formBambulanci form;
		public Host(formBambulanci form)
		{
			this.form = form;
		}

		private BackgroundWorker bwHostStarter;
		public List<ClientInfo> clientList;
		public UdpClient udpHost; //public for test purposes

		public int listenPort; //for host's self id==0 client

		//https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.backgroundworker?view=netcore-3.1
		//https://docs.microsoft.com/en-us/dotnet/framework/winforms/controls/how-to-make-thread-safe-calls-to-windows-forms-controls
		//https://docs.microsoft.com/en-us/dotnet/framework/winforms/controls/multithreading-in-windows-forms-controls

		/// <summary>
		/// Async - Starts BackgroundWorker which waits for numOfPlayers to connect to host.
		/// </summary>
		public void BWStartHost(int numOfPlayers, int listenPort)
		{
			this.listenPort = listenPort; //used for host's id==0

			ParallelBW.ActivateWorker(ref bwHostStarter, true, BW_DoWork, BW_ProgressChanged, BW_RunWorkerCompleted, new ValueTuple<int, int>(numOfPlayers, listenPort));
			//bwHostStarter.WorkerSupportsCancellation = true; //mozna neni potreba
		}
		
		/// <summary>
		/// Cancels backgroundWorker.
		/// </summary>
		public void BWCancelHost()
		{
			//bwHostStarter.CancelAsync(); //zbytecne, nevyuzivam e.CancelationPending
			byte[] cancel = Data.ToBytes(Command.HostStopHosting);
			udpHost.Send(cancel, cancel.Length, (IPEndPoint)udpHost.Client.LocalEndPoint); //maybe should be localHost, not network IPv4
		}

		/// <summary>
		/// Reports progress of backgroundWorker.
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

			IPAddress hostIP = getHostIP();
			udpHost = new UdpClient(new IPEndPoint(IPAddress.Any /*hostIP*/, listenPort)); //IPAddress.Any allows localHost send
			//udpHost.EnableBroadcast //how is it true when default is false??----

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

						//potvrzeni pro klienta
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
			if (e.Error != null)
				throw new Exception(); //Error handling??----
			else if (!e.Cancelled)//all clients are connected
			{
				byte[] moveClientToWaitingRoom = Data.ToBytes(Command.HostMoveToWaitingRoom);
				BroadcastMessage(moveClientToWaitingRoom); //bugged if used with localHost
				
				form.ChangeGameState(GameState.HostWaitingRoom);
			}
		}

		/// <summary>
		/// Returns first IPv4 address of Host -- in case of multiple IPv4 addresses might not work correctly
		/// </summary>
		private IPAddress getHostIP()
		{
			IPAddress hostIP = null;
			IPAddress[] addresses = Dns.GetHostAddresses(Dns.GetHostName());
			foreach (var address in addresses)
			{
				if (address.AddressFamily == AddressFamily.InterNetwork)
				{
					hostIP = address;
					break;
				}
			}
			return hostIP;
		}
		

		public void BroadcastMessage(byte[] message) //zalezi na poradi, z nejakeho duvodu neodesila 2. send
		{
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, Client.listenPort));
		}


		/// <summary>
		/// broadcast on network and localhost
		/// </summary>
		public void LocalhostAndBroadcastMessage(byte[] message)
		{
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, Client.listenPort));
			udpHost.Send(message, message.Length, new IPEndPoint(IPAddress.Loopback, Client.listenPort));
		}

		BackgroundWorker bwGameListener;
		public void StartGameListening()
		{
			ParallelBW.ActivateWorker(ref bwGameListener, true, GL_DoWork, GL_Progress, GL_Completed);
		}
		private void GL_DoWork(object sender, DoWorkEventArgs e)
		{
			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, listenPort);
			while (true)
			{
				Data data = new Data(udpHost.Receive(ref clientEP));
				switch (data.Cmd)
				{
					case Command.ClientMove: //moves player who sends me command
						PlayerMovement playerMovement = (PlayerMovement) byte.Parse(data.Msg);
						foreach (var client in clientList)
						{
							if (client.IpEndPoint.Equals(clientEP)) //find client who send me move command
							{
								client.player.Move(playerMovement);
								Console.WriteLine($"{playerMovement} : COORDS: {client.player.x} : {client.player.y}"); //wont receive host movement
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

		}
		private void GL_Completed(object sender, RunWorkerCompletedEventArgs e)
		{

		}

	}

	class Client
	{
		private formBambulanci form;

		public bool InGame;
		public IPEndPoint hostEPGlobal;
		//public bool InGame { get; private set; }
		//private IPEndPoint hostEPGlobal;

		public Client(formBambulanci form)
		{
			this.form = form;
		}

		private UdpClient udpClient;
		public static int listenPort = 60000; //lze ziskat i z udpClienta...

		public void StartClient(IPAddress iPAddress) //startClient for host-client : maybe wont use it
		{
			udpClient = new UdpClient(new IPEndPoint(iPAddress, listenPort));
		}

		/// <summary>
		/// Server refresh paralelism.
		/// </summary>
		private BackgroundWorker bwServerRefresher;
		public void BWServerRefresherStart(int hostPort) //unfinished
		{
			/* idea
			if (bwServerRefresher != null)
				bwServerRefresher.Dispose();
			*/
			ParallelBW.ActivateWorker(ref bwServerRefresher, true, BW_RefreshServers, BW_ServerFound, BW_RefreshCompleted, hostPort);
		}

		private void BW_RefreshServers(object sender, DoWorkEventArgs e) //DoWork
		{
			int hostPort = (int)e.Argument;
			IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, hostPort);


			IPEndPoint hostEP = new IPEndPoint(IPAddress.Any, listenPort);

			byte[] findServerMessage = Data.ToBytes(Command.ClientFindServers);
			udpClient.Send(findServerMessage, findServerMessage.Length, broadcastEP);

			bool searching = true;
			while (searching)
			{
				Data received = new Data(udpClient.Receive(ref hostEP));
				switch (received.Cmd)
				{
					case Command.HostFoundServer:
						bwServerRefresher.ReportProgress(0, hostEP); //0 is ignored
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
			var hostEP = e.UserState;
			form.lBServers.Items.Add(hostEP);
		}
		private void BW_RefreshCompleted(object sender, RunWorkerCompletedEventArgs e) //Complete work
		{
			//empty -- while loop sem nikdy nepropadne, pokud nedokoncim implementaci "ClientStopServerRefresh"
		}

		public void LoginToSelectedServer()
		{
			//send poison pill on localHost == stops server refreshing backgroundWorker
			byte[] poisonPill = Data.ToBytes(Command.ClientStopRefreshing);
			IPEndPoint localhostEP = new IPEndPoint(IPAddress.Loopback, listenPort);
			udpClient.Send(poisonPill, poisonPill.Length, localhostEP);


			IPEndPoint hostSendEP = (IPEndPoint)form.lBServers.SelectedItem;
			hostEPGlobal = hostSendEP;
			byte[] loginMessage = Data.ToBytes(Command.ClientLogin);
			
			udpClient.Send(loginMessage, loginMessage.Length, hostSendEP);

			IPEndPoint hostReceiveEP = new IPEndPoint(IPAddress.Any, listenPort);
			Data received = new Data(udpClient.Receive(ref hostReceiveEP)); //recyukluji serverEP, snad nevadi----uz nerecykluji, ale mam tu 2x hostEP??
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

			ParallelBW.ActivateWorker(ref bwHostWaiter, true, BW_ClientWaiting, BW_WaitingProgress, BW_WaitingCompleted, hostSendEP);
		}

		BackgroundWorker bwHostWaiter;

		private void BW_ClientWaiting(object sender, DoWorkEventArgs e)
		{
			IPEndPoint hostEP = (IPEndPoint)e.Argument; //po nekolikate pouzivam hostEP pod klientem, mohl bych dat globalni prom.?

			Data received = new Data(udpClient.Receive(ref hostEP)); //zmenim hostEP, mozna bych si nemusel posilat jako arg
			//waiting for everyone to connect.
			switch (received.Cmd)
			{
				case Command.HostCanceled: //not implemented yet
					bwHostWaiter.ReportProgress(-1); //not used yet??
					break;
				case Command.HostMoveToWaitingRoom:
					bwHostWaiter.ReportProgress(0);
					//mozna se chci dozvedet sve ID----
					break;
				default:
					break;
			}

			received = new Data(udpClient.Receive(ref hostEP));
			//waiting for host to start game
			switch (received.Cmd)
			{	case Command.HostCanceled: //not implemented yet
					bwHostWaiter.ReportProgress(-1);
					break;
				case Command.HostStartGame:
					bwHostWaiter.ReportProgress(1);
					break;
				default:
					break;
			}
		}

		private void BW_WaitingProgress(object sender, ProgressChangedEventArgs e)
		{
			int num = e.ProgressPercentage;
			switch (num) //hloupe pojmenovani.....----------------
			{
				case -1: //disconnect - not implemented yet
					break;
				case 0: //moved to waiting room
					form.ChangeGameState(GameState.ClientWaitingRoom);
					break;
				case 1:
					//start game----------------
					InGame = true;
					break;
				default:
					break;
			}
		}
		
		public void BW_WaitingCompleted(object sender, RunWorkerCompletedEventArgs e) //public so its usable outside for host----
		{
			//after startGame/hostCanceled
			if (InGame)
			{
				form.ChangeGameState(GameState.InGame);
				ParallelBW.ActivateWorker(ref bwInGameListener, true, IGL_DoWork, IGL_RedrawProgress, IGL_Completed);
			}

		}

		BackgroundWorker bwInGameListener;
		public struct ImageWithLocation
		{
			private Bitmap image;
			private float x;
			private float y;
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
		//list of toBeDrawn objects -----
		public ConcurrentQueue<ImageWithLocation> toBeDrawn;
		private void IGL_DoWork(object sender, DoWorkEventArgs e)
		{
			//form.Invalidate();
			toBeDrawn = new ConcurrentQueue<ImageWithLocation>();
			while (true)
			{
				Data received = new Data(udpClient.Receive(ref hostEPGlobal));
				switch (received.Cmd)
				{
					case Command.HostTick:
						bwInGameListener.ReportProgress(0); //0 not needed
						break;
					case Command.HostPlayerMovement: //string isnt as effective...
						string[] tokens = received.Msg.Split('|');
						int playerId = int.Parse(tokens[0]);
						byte direction = byte.Parse(tokens[1]);
						float x = float.Parse(tokens[2]);
						float y = float.Parse(tokens[3]);

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
			//Console.WriteLine($"Host-client: client moved:{form.playerMovement} send to: {hostEPGlobal}");
			byte[] clientMove = Data.ToBytes(Command.ClientMove, ((byte)form.playerMovement).ToString()); //shouldn't be string
			udpClient.Send(clientMove, clientMove.Length, hostEPGlobal);
		}
		private void IGL_Completed(object sender, RunWorkerCompletedEventArgs e)
		{

		}

	}

	class ParallelBW
	{
		private ParallelBW() { }

		public static void ActivateWorker(ref BackgroundWorker worker, bool reportsProgress, DoWorkEventHandler work,
			   ProgressChangedEventHandler progress, RunWorkerCompletedEventHandler completed, object runArg = null)
		{
			worker = new BackgroundWorker();
			worker.WorkerReportsProgress = reportsProgress;
			worker.DoWork += work;
			worker.ProgressChanged += progress;
			worker.RunWorkerCompleted += completed;

			worker.RunWorkerAsync(runArg);
		}
	}
}
