using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
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

		private void DisableAllControls()
		{
			foreach (var control in this.Controls)
			{
				DisableControl((Control)control);
			}
		}

		public void ChangeGameState(GameState newState)
		{
			currentGameState = newState;
			switch (currentGameState)
			{
				case GameState.Intro:;
					DisableAllControls();
					EnableControl(bCreateGame);
					EnableControl(bConnect);
					EnableControl(bExit);
					break;
				case GameState.HostSelect:
					DisableAllControls();
					EnableControl(lBNumOfPlayers);
					EnableControl(bCreateGame2);
					EnableControl(nListenPort);
					EnableControl(bIntro);
					break;
				case GameState.HostWaiting:
					DisableAllControls();
					EnableControl(bCancelHost);
					EnableControl(lWaiting);
					break;
				case GameState.HostWaitingRoom: //musim pridat alespon text waiting room------------
					DisableAllControls(); 
					//vsem klientum napisu, ze se maji presunout do waiting room + pridam jejich ID (pozice)...
					host.MoveClientsToWaitingRoom();
					//chci nejakou grafiku....
					break;
				case GameState.ClientSearch:
					DisableAllControls();
					EnableControl(lBServers);
					EnableControl(bLogin);
					EnableControl(nHostPort);
					EnableControl(bRefreshServers);
					EnableControl(bIntro);
					break;
				case GameState.ClientWaiting:
					DisableAllControls();
					EnableControl(lWaiting);
					client.MoveSelfToWaitingRoom();
					break;
				case GameState.ClientWaitingRoom:
					DisableAllControls();
					break;
				default:
					break;
			}
		}
		
		private void bCreateGame_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostSelect);
			lBNumOfPlayers.SelectedIndex = 0; //default select
		}

		private void bCreateGame2_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostWaiting);
			int numOfPlayers = lBNumOfPlayers.SelectedIndex + 1;
			host.BWStartHost(numOfPlayers, (int)nListenPort.Value);
		}

		private void bConnect_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.ClientSearch);
			//client.StartClient();
		}

		private void bLogin_Click(object sender, EventArgs e)
		{
			IPEndPoint serverEP = (IPEndPoint)lBServers.SelectedItem;
			byte[] loginMessage = Data.ToBytes(Command.Login);
			client.client.Send(loginMessage, loginMessage.Length, serverEP);
			ChangeGameState(GameState.ClientWaiting); //asi bych mel pockat na potvrzeni serveru, muzou se najednou pripojovat 2 klienti

			/*
			string[] tokens = lBServers.SelectedItem.ToString().Split(':');
			IPEndPoint serverEP = new IPEndPoint(IPAddress.Parse(tokens[0]), int.Parse(tokens[1]));
			client.clientSocket.SendTo(new byte[] { (byte)Command.Login }, serverEP);
			ChangeGameState(GameState.ClientWaiting);
			*/
		}
		private void bRefreshServers_Click(object sender, EventArgs e)
		{
			lBServers.Items.Clear();
			int hostPort = (int)nHostPort.Value;
			int listenPort = hostPort + 1; //odlisny od host portu pro stejny PC, jinak je to asi jedno?
			IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, hostPort);
			IPEndPoint hostEP = new IPEndPoint(IPAddress.Any, listenPort);

			client.client = new UdpClient(new IPEndPoint(IPAddress.Any, listenPort)); //pri opetovanem refreshi na stejnem portu hazi chybu!!! -- snad vyresi backgroundWorker
			
			byte[] findServerMessage = Data.ToBytes(Command.FindServers);
			client.client.Send(findServerMessage, findServerMessage.Length, broadcastEP);
			while(true) //for (int i = 0; i < 1; i++)
			{
				Data received = new Data(client.client.Receive(ref hostEP));
				if (received.Cmd == Command.FoundServer)
				{
					lBServers.Items.Add(hostEP);
					break;
				}
			}
		}

		private void bExit_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void bCancelHost_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.HostSelect);
			host.BWCancelHost();
		}

		private void bIntro_Click(object sender, EventArgs e)
		{
			ChangeGameState(GameState.Intro);
		}
	}
}
