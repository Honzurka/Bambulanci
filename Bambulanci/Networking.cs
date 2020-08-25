using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Bambulanci
{
	enum Command { ClientLogin, ClientLogout, ClientFindServers, HostFoundServer, HostMoveToWaitingRoom, HostCanceled, HostStopHosting, ClientStopServerRefresh }
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
		private UdpClient host;


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
			byte[] cancel = Data.ToBytes(Command.HostStopHosting); //kind of poison pill
			host.Send(cancel, cancel.Length, (IPEndPoint)host.Client.LocalEndPoint);
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
			host = new UdpClient(new IPEndPoint(hostIP, listenPort));

			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, listenPort);
			int id = 1; //0 is host
			bool hostClosed = false;
			UpdateRemainingPlayers(numOfPlayers);
			while (!hostClosed && clientList.Count < numOfPlayers)
			{
				Data data = new Data(host.Receive(ref clientEP));
				switch (data.Cmd)
				{
					case Command.ClientLogin:
						ClientInfo clientInfo = new ClientInfo() { id = id, ipEndPoint = clientEP };
						clientList.Add(clientInfo);
						UpdateRemainingPlayers(numOfPlayers);
						id++;
						break;
					case Command.ClientLogout: //klient zatim neposila
						int clientID = Int32.Parse(data.Msg);
						clientList.RemoveAll(client => client.id == clientID);
						UpdateRemainingPlayers(numOfPlayers);
						break;
					case Command.ClientFindServers:
						byte[] serverInfo = Data.ToBytes(Command.HostFoundServer);
						host.Send(serverInfo, serverInfo.Length, clientEP);
						break;
					case Command.HostStopHosting:
						hostClosed = true;
						byte[] hostCanceledInfo = Data.ToBytes(Command.HostCanceled);
						foreach (var client in clientList) //isn't implemented on client's side
						{
							host.Send(hostCanceledInfo, hostCanceledInfo.Length, client.ipEndPoint);
						}
						host.Close();
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
				host.Send(moveClientToWaitingRoom, moveClientToWaitingRoom.Length, client.ipEndPoint);
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
					case Command.ClientStopServerRefresh: //not implemented
						searching = false;
						break;
					default:
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

		public void LoginSelectedServer()
		{
			//stop refresh worker-------
			
			IPEndPoint serverEP = (IPEndPoint)form.lBServers.SelectedItem;
			byte[] loginMessage = Data.ToBytes(Command.ClientLogin);
			udpClient.Send(loginMessage, loginMessage.Length, serverEP);
		}


		public void MoveSelfToWaitingRoom() //not done----------------------
		{
			byte[] data = new byte[1024]; //1024???
			while (true)
			{
				//clientSocket.Receive(data);
				Command command = (Command)data[0];
				if (command == Command.HostMoveToWaitingRoom)
				{
					byte id = (byte)data[1];
					//Console.WriteLine($"client id: {id}");
					form.ChangeGameState(GameState.ClientWaitingRoom);
				}
			}
		}
	}

}
