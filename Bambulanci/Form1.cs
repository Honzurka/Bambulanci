using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace Bambulanci
{
	public partial class formBambulanci : Form
	{
		private enum GameState { Intro, HostSelect, HostWaiting, HostWaitingRoom, ClientSearch}
		private GameState currentGameState;

		public formBambulanci()
		{
			InitializeComponent();
			ChangeGameState(GameState.Intro);
		}

		private void DisableControl(Control c)
		{
			c.Enabled = false;
			c.Visible = false;
		}

		private void EnableControl(Control c)
		{
			c.Enabled = true;
			c.Visible = true;
		}

		private void ChangeGameState(GameState newState)
		{
			currentGameState = newState;
			switch (currentGameState)
			{
				case GameState.Intro:
						EnableControl(bCreateGame);
						EnableControl(bConnect);
						break;
				case GameState.HostSelect:
					DisableControl(bCreateGame);
					DisableControl(bConnect);
					EnableControl(lBNumOfPlayers);
					EnableControl(bCreateGame2);
					EnableControl(nListenPort);
					break;
				case GameState.HostWaiting:
					DisableControl(lBNumOfPlayers);
					DisableControl(bCreateGame2);
					DisableControl(nListenPort);
					EnableControl(lWaiting); //okno se neprekresli...
					break;
				case GameState.HostWaitingRoom:
					DisableControl(lWaiting);
					Console.WriteLine("host is in the waiting room");
					break;
				case GameState.ClientSearch:
					DisableControl(bCreateGame);
					DisableControl(bConnect);
					EnableControl(lBServers);
					EnableControl(bLogin);
					EnableControl(nHostPort);
					EnableControl(bRefreshServers);
					break;
				default:
					break;
			}
		}
		
		private void bCreateGame_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostSelect);
		}

		private void bCreateGame2_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostWaiting);
			int numOfPlayers = lBNumOfPlayers.SelectedIndex + 1;
			StartHost(numOfPlayers, (int)nListenPort.Value);
		}

		private void bConnect_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.ClientSearch);
			StartClient();
		}


		//networking---------------------
		enum Command { Login, Logout, FindServers}
		struct Client //neresim viditelnost poli
		{
			public int id;
			//private string name; //bez newline?
			//private Color color; //?
			public IPEndPoint ipEndPoint;
		}
		private void StartHost(int numOfPlayers, int listenPort)
		{
			List<Client> clientList = new List<Client>(); //size is known...could be array
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
			UdpClient listener = new UdpClient(new IPEndPoint(hostIP, listenPort));

			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, listenPort);

			while(clientList.Count < numOfPlayers)
			{
				byte[] data = listener.Receive(ref clientEP);

				//parser-----
				Command command = (Command)data[0];

				switch (command)
				{
					case Command.Login:
						Console.WriteLine($"New client: {clientEP}");
						Client client = new Client() { id = id, ipEndPoint = clientEP };
						clientList.Add(client);
						id++; //muze zpusobit potize v pripade reconnection
						break;
					case Command.Logout:
						break;
					case Command.FindServers:
						byte[] serverInfo = Encoding.ASCII.GetBytes(listener.Client.LocalEndPoint.ToString()); //"ping"
						listener.Send(serverInfo, serverInfo.Length, clientEP);
						Console.WriteLine("broadcast received");
						break;
					default:
						break;
				}
			}
			ChangeGameState(GameState.HostWaitingRoom); //kazdemu hraci poslu jeho id/pozici v listu
		}

		Socket clientSocket;
		private void StartClient()
		{
			clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			clientSocket.EnableBroadcast = true;
		}

		private void bLogin_Click(object sender, EventArgs e)
		{
			string[] tokens = lBServers.SelectedItem.ToString().Split(':');
			IPEndPoint serverEP = new IPEndPoint(IPAddress.Parse(tokens[0]), int.Parse(tokens[1]));
			clientSocket.SendTo(new byte[] { (byte)Command.Login }, serverEP);
		}

		private void bRefreshServers_Click(object sender, EventArgs e)
		{
			lBServers.Items.Clear();
			int hostPort = (int)nHostPort.Value;
			IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, hostPort);
			clientSocket.SendTo(new byte[] { (byte)Command.FindServers }, broadcastEP);

			Console.WriteLine("broadcast sent");

			byte[] serverInfo = new byte[1024]; //1024???

			//find all servers---------------------------------------melo by se dit paralelne
			for (int i = 0; i < 1; i++) //dokud si nevyberu, vyhledavam...
			{
				int receivedAmount = clientSocket.Receive(serverInfo);
				string serverInfoString = Encoding.ASCII.GetString(serverInfo);
				lBServers.Items.Add(serverInfoString);
			}

		}
	}
}
