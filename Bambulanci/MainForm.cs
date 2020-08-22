using System;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace Bambulanci
{
	public enum GameState { Intro, HostSelect, HostWaiting, ClientWaiting, HostWaitingRoom, ClientWaitingRoom, ClientSearch }

	public partial class formBambulanci : Form
	{
		private GameState currentGameState;
		private Client client;
		private Host host;

		public formBambulanci()
		{
			InitializeComponent();
			client = new Client(this);
			host = new Host(this);
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

		public void ChangeGameState(GameState newState)
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
				case GameState.ClientWaiting:
					DisableControl(nHostPort);
					DisableControl(bRefreshServers);
					DisableControl(lBServers);
					DisableControl(bLogin);
					EnableControl(lWaiting);
					client.MoveSelfToWaitingRoom();
					break;
				case GameState.HostWaitingRoom:
					DisableControl(lWaiting);
					//vsem klientum napisu, ze se maji presunout do waiting room + pridam jejich ID (pozice)...
					host.MoveClientsToWaitingRoom();
					//chci nejakou grafiku....
					break;
				case GameState.ClientWaitingRoom:
					DisableControl(lWaiting);
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
			//nasledujici provest paralelne***********************************************************
			//Thread t = new Thread(() => StartHost(numOfPlayers, (int)nListenPort.Value));
			//t.Start();


			host.StartHost(numOfPlayers, (int)nListenPort.Value);
			/*
			backgroundWorker1 = new BackgroundWorker();
			backgroundWorker1.DoWork += new DoWorkEventHandler(BackgroundWorker1_DoWork);
			backgroundWorker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BackgroundWorker1_RunWorkerCompleted);

			backgroundWorker1.RunWorkerAsync((numOfPlayers, (int)nListenPort.Value));
			*/
		}

		private void bConnect_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.ClientSearch);
			client.StartClient();
		}

		private void bLogin_Click(object sender, EventArgs e)
		{
			string[] tokens = lBServers.SelectedItem.ToString().Split(':');
			IPEndPoint serverEP = new IPEndPoint(IPAddress.Parse(tokens[0]), int.Parse(tokens[1]));
			client.clientSocket.SendTo(new byte[] { (byte)Command.Login }, serverEP);
			ChangeGameState(GameState.ClientWaiting);
		}
		private void bRefreshServers_Click(object sender, EventArgs e)
		{
			lBServers.Items.Clear();
			int hostPort = (int)nHostPort.Value;
			IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, hostPort);
			client.clientSocket.SendTo(new byte[] { (byte)Command.FindServers }, broadcastEP);

			Console.WriteLine("broadcast sent");

			byte[] serverInfo = new byte[1024]; //1024???

			//find all servers---------------------------------------melo by se dit paralelne
			for (int i = 0; i < 1; i++) //dokud si nevyberu, vyhledavam...
			{
				int receivedAmount = client.clientSocket.Receive(serverInfo);
				string serverInfoString = Encoding.ASCII.GetString(serverInfo);
				lBServers.Items.Add(serverInfoString);
			}

		}

	}
}
