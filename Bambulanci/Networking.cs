using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Bambulanci
{
	enum Command { Login, Logout, FindServers, FoundServer, MoveToWaitingRoom, HostingCanceled }
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
			bwHostStarter.WorkerSupportsCancellation = true;

			bwHostStarter.DoWork += new DoWorkEventHandler(bw_DoWork);
			bwHostStarter.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
			bwHostStarter.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
			
			bwHostStarter.RunWorkerAsync(new ValueTuple<int, int>(numOfPlayers, listenPort));
		}
		
		/// <summary>
		/// Cancels backgroundWorker.
		/// </summary>
		public void BWCancelHost()
		{
			bwHostStarter.CancelAsync();
			host.Close(); //zkousim zavrit spojeni, pravdepodobne pri poslouchani == vyhodi exception
		}

		/// <summary>
		/// Reports progress of backgroundWorker.
		/// </summary>
		private void UpdateRemainingPlayers(int numOfPlayers)
		{
			int remainingPlayers = numOfPlayers - clientList.Count;
			bwHostStarter.ReportProgress(remainingPlayers);
		}

		private void bw_DoWork(object sender, DoWorkEventArgs e)
		{
			(int numOfPlayers, int listenPort) = (ValueTuple<int, int>)e.Argument;
			clientList = new List<ClientInfo>();
			int id = 1; //0 is host
			IPAddress hostIP = getHostIP();

			host = new UdpClient(new IPEndPoint(hostIP, listenPort));
			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, listenPort);

			UpdateRemainingPlayers(numOfPlayers);
			try
			{
				while (clientList.Count < numOfPlayers)
				{
					Data data = new Data(host.Receive(ref clientEP));
					switch (data.Cmd)
					{
						case Command.Login:
							ClientInfo clientInfo = new ClientInfo() { id = id, ipEndPoint = clientEP };
							clientList.Add(clientInfo);
							UpdateRemainingPlayers(numOfPlayers);
							id++;
							break;
						case Command.Logout:
							int clientID = Int32.Parse(data.Msg);
							clientList.RemoveAll(client => client.id == clientID);
							UpdateRemainingPlayers(numOfPlayers);
							break;
						case Command.FindServers:
							//-------------klient zatim neumi prijimat-----------------------------------
							//byte[] serverInfo = Data.ToBytes(Command.FindServers, host.Client.LocalEndPoint.ToString());
							byte[] serverInfo = Data.ToBytes(Command.FoundServer);
							host.Send(serverInfo, serverInfo.Length, clientEP);
							Console.WriteLine($"broadcast received from {clientEP}");

							//byte[] serverInfo = Encoding.ASCII.GetBytes(host.Client.LocalEndPoint.ToString());
							//host.Send(serverInfo, serverInfo.Length, clientEP);
							break;
						default:
							break;
					}
				}
			}
			catch (SocketException)
			{
				if (bwHostStarter.CancellationPending) //catching intended exception from BWCancelHost
					e.Cancel = true;
				else
					throw;
			}
			finally
			{
				host.Close();
			}
		}

		private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			int remainingPlayers = e.ProgressPercentage;
			form.lWaiting.Text = $"Čekám na {remainingPlayers} hráče";
		}

		private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (e.Error != null)
				throw new Exception(); //Error handling??----
			else if (e.Cancelled) //neni implementovano u klienta -----
			{
				byte[] hostCanceledInfo = Data.ToBytes(Command.HostingCanceled);
				foreach (var client in clientList)
				{
					host.Send(hostCanceledInfo, hostCanceledInfo.Length, client.ipEndPoint);
				}
			}
			else //all clients are connected
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
				byte[] moveClientToWaitingRoom = Data.ToBytes(Command.MoveToWaitingRoom, client.id.ToString());
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
		
		//public Socket clientSocket; //asi bych chtel spise private??--
		public UdpClient client;

		public void MoveSelfToWaitingRoom()
		{
			byte[] data = new byte[1024]; //1024???
			while (true)
			{
				//clientSocket.Receive(data);
				Command command = (Command)data[0];
				if (command == Command.MoveToWaitingRoom)
				{
					byte id = (byte)data[1];
					Console.WriteLine($"client id: {id}");
					form.ChangeGameState(GameState.ClientWaitingRoom);
				}
			}
		}

		public void StartClient()
		{
			//clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			//clientSocket.EnableBroadcast = true;
		}

	}

}
