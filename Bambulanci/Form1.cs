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
		private enum GameState { Intro, HostSelect, HostWaiting, HostWaitingRoom}
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
					EnableControl(lWaiting);
					break;
				case GameState.HostWaitingRoom:
					DisableControl(lWaiting);
					Console.WriteLine("host is in the waiting room");
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
			//not implemented yet
		}


		//networking---------------------
		enum Command { Login, Logout}
		struct Client //neresim viditelnost poli
		{
			public int id;
			//private string name; //bez newline?
			//private Color color; //?
			public IPEndPoint ipEndPoint;
		}
		private void StartHost(int numOfPlayers, int listenPort)
		{
			List<Client> clientList = new List<Client>(); //size is known...
			int id = 1; //0 is host

			//int listenPort = 55555; //zatim napevno, casem pujde nastavit
			UdpClient listener = new UdpClient(listenPort);
			IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, listenPort);

			Console.WriteLine($"server IP: {listener.Client.LocalEndPoint as IPEndPoint}");

			while(clientList.Count < numOfPlayers)
			{
				byte[] data = listener.Receive(ref clientEP);

				//parser-----
				Command command = (Command)data[0];

				Console.WriteLine($"New client: {clientEP}");
				if (command == Command.Login)
				{
					Client client = new Client() { id = id, ipEndPoint = clientEP };
					clientList.Add(client);
					id++; //muze zpusobit potize v pripade reconnection
				}
			}


			ChangeGameState(GameState.HostWaitingRoom); //kazdemu hraci poslu jeho id/pozici v listu

		}
	}
}
