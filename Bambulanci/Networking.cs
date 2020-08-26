using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bambulanci
{
	enum Command { ClientLogin, ClientLogout, ClientFindServers, 
		HostFoundServer, HostMoveToWaitingRoom, HostCanceled, HostPoisonPill,
		ClientPoisonPill, HostLoginAccepted, HostLoginDeclined, HostStartGame
	}
	struct ClientInfo
	{
		public int id;
		//private string name; //bez newline?
		//private Color color; //?
		public IPEndPoint ipEndPoint;

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
			//Console.WriteLine($"message translated: {Cmd}----------");
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
		private List<ClientInfo> clientList;
		private UdpClient udpHost;


		//https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.backgroundworker?view=netcore-3.1
		//https://docs.microsoft.com/en-us/dotnet/framework/winforms/controls/how-to-make-thread-safe-calls-to-windows-forms-controls
		//https://docs.microsoft.com/en-us/dotnet/framework/winforms/controls/multithreading-in-windows-forms-controls

		/// <summary>
		/// Async - Starts BackgroundWorker which waits for numOfPlayers to connect to host.
		/// </summary>
		public void BWStartHost(int numOfPlayers, int listenPort)
		{
			bwHostStarter = new BackgroundWorker();
			bwHostStarter.WorkerReportsProgress = true;
			//bwHostStarter.WorkerSupportsCancellation = true; //mozna neni potreba

			bwHostStarter.DoWork += new DoWorkEventHandler(BW_DoWork);
			bwHostStarter.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BW_RunWorkerCompleted);
			bwHostStarter.ProgressChanged += new ProgressChangedEventHandler(BW_ProgressChanged);

			bwHostStarter.RunWorkerAsync(new ValueTuple<int, int>(numOfPlayers, listenPort));
		}
		
		/// <summary>
		/// Cancels backgroundWorker.
		/// </summary>
		public void BWCancelHost()
		{
			//bwHostStarter.CancelAsync(); //zbytecne, nevyuzivam e.CancelationPending
			byte[] cancel = Data.ToBytes(Command.HostPoisonPill);
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
			udpHost = new UdpClient(new IPEndPoint(hostIP, listenPort));

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
						//Console.WriteLine("Host received ClientLogin");
						ClientInfo clientInfo = new ClientInfo() { id = id, ipEndPoint = clientEP };
						clientList.Add(clientInfo);
						UpdateRemainingPlayers(numOfPlayers);
						id++;

						//potvrzeni pro klienta
						byte[] loginConfirmed = Data.ToBytes(Command.HostLoginAccepted);

						//Console.WriteLine($"Host sent HostLoginAccepted on ${clientEP}");
						udpHost.Send(loginConfirmed, loginConfirmed.Length, clientEP);
						break;
					case Command.ClientLogout: //klient zatim neposila
						int clientID = Int32.Parse(data.Msg);
						clientList.RemoveAll(client => client.id == clientID);
						UpdateRemainingPlayers(numOfPlayers);
						break;
					case Command.ClientFindServers:
						byte[] serverInfo = Data.ToBytes(Command.HostFoundServer);
						udpHost.Send(serverInfo, serverInfo.Length, clientEP);
						break;
					case Command.HostPoisonPill:
						hostClosed = true;
						byte[] hostCanceledInfo = Data.ToBytes(Command.HostCanceled);
						foreach (var client in clientList)
						{
							udpHost.Send(hostCanceledInfo, hostCanceledInfo.Length, client.ipEndPoint);
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
				MoveClientsToWaitingRoom();
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
		
		public void MoveClientsToWaitingRoom()
		{
			foreach (var client in clientList)
			{
				byte[] moveClientToWaitingRoom = Data.ToBytes(Command.HostMoveToWaitingRoom, client.id.ToString());
				udpHost.Send(moveClientToWaitingRoom, moveClientToWaitingRoom.Length, client.ipEndPoint);
				//Console.WriteLine($"Host sent HostMoveToWaitingRoom on ${client.ipEndPoint}");
			}
		}
	}

	class Client
	{
		private formBambulanci form;
		public Client(formBambulanci form)
		{
			this.form = form;
		}

		private UdpClient udpClient;
		private int listenPort; //lze ziskat i z udpClienta...

		public void StartClient()
		{
			Random rnd = new Random();
			listenPort = rnd.Next(60000, 65536); //random port to be able to have 2 clients on 1 PC---------
			udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, listenPort));
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
			bwServerRefresher = new BackgroundWorker();
			bwServerRefresher.WorkerReportsProgress = true;

			bwServerRefresher.DoWork += new DoWorkEventHandler(BW_RefreshServers);
			bwServerRefresher.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BW_RefreshCompleted);
			bwServerRefresher.ProgressChanged += new ProgressChangedEventHandler(BW_ServerFound);
			
			bwServerRefresher.RunWorkerAsync(hostPort);
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
					case Command.ClientPoisonPill:
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
			byte[] poisonPill = Data.ToBytes(Command.ClientPoisonPill);
			IPEndPoint localhostEP = new IPEndPoint(IPAddress.Loopback, listenPort);
			udpClient.Send(poisonPill, poisonPill.Length, localhostEP);


			IPEndPoint hostSendEP = (IPEndPoint)form.lBServers.SelectedItem;
			byte[] loginMessage = Data.ToBytes(Command.ClientLogin);
			System.Diagnostics.Debug.Print("test");

			udpClient.Send(loginMessage, loginMessage.Length, hostSendEP);


			//Console.WriteLine($"client listenPort: {listenPort}");
			IPEndPoint hostReceiveEP = new IPEndPoint(IPAddress.Any, listenPort);
			Data received = new Data(udpClient.Receive(ref hostReceiveEP)); //recyukluji serverEP, snad nevadi----uz nerecykluji, ale mam tu 2x hostEP??
			//Console.WriteLine($"client received some message; {received.Cmd} AND {received.Msg}");
			switch (received.Cmd)//sem nedojdu??-----------------------------------------------------------------------------
			{
				case Command.HostLoginAccepted:
					form.ChangeGameState(GameState.ClientWaiting);
					Console.WriteLine("client received HostLoginAccepted.");
					break;
				case Command.HostLoginDeclined:
					//not implemented yet
					break;
				default:
					break;
			}

			//worker pro async cekani na MoveToWaitingRoom+HostStartGame --- ripadne chci mit moznost disconnect buttonu
			bwHostWaiter = new BackgroundWorker();
			bwHostWaiter.WorkerReportsProgress = true;

			bwHostWaiter.DoWork += BW_ClientWaiting;
			bwHostWaiter.ProgressChanged += BW_WaitingProgress;
			bwHostWaiter.RunWorkerCompleted += BW_WaitingCompleted;

			bwHostWaiter.RunWorkerAsync(hostSendEP);
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
				case Command.HostStartGame: //not implemented yet
					bwHostWaiter.ReportProgress(1);
					break;
			//pripadne posloucham zmeny nastaveni => loop
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
					//start game
					break;
				default:
					break;
			}
		}
		
		private void BW_WaitingCompleted(object sender, RunWorkerCompletedEventArgs e)
		{

		}

	}

}
