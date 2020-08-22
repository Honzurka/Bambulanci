using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Bambulanci
{
	
	//networking---------------------
	enum Command { Login, Logout, FindServers, MoveToWaitingRoom }
	struct ClientInfo
	{
		public int id;
		//private string name; //bez newline?
		//private Color color; //?
		public IPEndPoint ipEndPoint;
	}

	class Host
	{
		formBambulanci form;
		public Host(formBambulanci form)
		{
			this.form = form;
		}

		List<ClientInfo> clientList;
		UdpClient host;
		public void StartHost(int numOfPlayers, int listenPort)
		{
			clientList = new List<ClientInfo>(); //size is known...could be array
			int id = 1; //0 is host

			IPAddress hostIP = null; //might not work in case of multiple IPv4 addresses
			IPAddress[] addresses = Dns.GetHostAddresses(Dns.GetHostName());
			foreach (var address in addresses)
			{
				if (address.AddressFamily == AddressFamily.InterNetwork)
				{
					hostIP = address;
					break;
				}
			}
			host = new UdpClient(new IPEndPoint(hostIP, listenPort));

			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, listenPort);

			while (clientList.Count < numOfPlayers)
			{
				byte[] data = host.Receive(ref clientEP);

				//parser-----
				Command command = (Command)data[0];

				switch (command)
				{
					case Command.Login:
						Console.WriteLine($"New client: {clientEP}");
						ClientInfo clientInfo = new ClientInfo() { id = id, ipEndPoint = clientEP };
						clientList.Add(clientInfo);
						id++; //muze zpusobit potize v pripade reconnection
						break;
					case Command.Logout:
						break;
					case Command.FindServers:
						byte[] serverInfo = Encoding.ASCII.GetBytes(host.Client.LocalEndPoint.ToString()); //"ping"
						host.Send(serverInfo, serverInfo.Length, clientEP);
						Console.WriteLine("broadcast received");
						break;
					default:
						break;
				}
			}
			form.ChangeGameState(GameState.HostWaitingRoom);
		}

		public void MoveClientsToWaitingRoom()
		{
			foreach (var client in clientList)
			{
				byte[] message = { (byte)Command.MoveToWaitingRoom, (byte)client.id }; //INT AS BYTES--------------------------?????
				host.Send(message, message.Length, client.ipEndPoint);
			}
		}


		private BackgroundWorker backgroundWorker1;
		private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
		{
			(int numOfPlayers, int nListenPortValue) = (ValueTuple<int, int>)e.Argument;

			StartHost(numOfPlayers, nListenPortValue);

		}
		private void BackgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			//nic
		}

	}

	class Client
	{
		private formBambulanci form;
		public Client(formBambulanci form)
		{
			this.form = form;
		}
		public Socket clientSocket; //asi bych chtel spise private??--

		public void MoveSelfToWaitingRoom()
		{
			byte[] data = new byte[1024]; //1024???
			while (true)
			{
				clientSocket.Receive(data);
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
			clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			clientSocket.EnableBroadcast = true;
		}

	}

}
