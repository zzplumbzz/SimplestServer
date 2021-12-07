using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;



public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;

    LinkedList<PlayerAccount> playerAccounts;

    const int PlayerAccountNameAndPassword = 1;

    string playerAccountsFilePath;


    int playerWaitingForMatchWithID = -1;
    int spectatorJoiningMatchWithID = -1;

    LinkedList<GameRoom> gameRooms;

    

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Server Running");
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        playerAccountsFilePath = Application.dataPath + Path.DirectorySeparatorChar + "PlayerAccounts.txt";
        playerAccounts = new LinkedList<PlayerAccount>();

        LoadPlayerAccounts();

        

        gameRooms = new LinkedList<GameRoom>();



    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }




    }

    public void SendMessageToClient(string msg, int id)
    {
        Debug.Log("Sending Message to client");
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);

        
    }

    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');

        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToServerSignifiers.CreateAccount)
        {

            Debug.Log("Create Account");

            string n = csv[1];
            string p = csv[2];
            bool nameIsInUse = false;

            foreach (PlayerAccount pa in playerAccounts)// check if name is in use
            {
                if (pa.name == n)
                {
                    nameIsInUse = true;
                    break;
                }
            }
            if (nameIsInUse)// account creation fail, name is in use
            {
                Debug.Log("Name is in use");
                SendMessageToClient(ServerToClientSignifiers.AccountCreationFailed + "", id);

            }
            else// name is not in use, create account and save it to text file
            {
                Debug.Log("Name is not in use");
                PlayerAccount newPlayerAccount = new PlayerAccount(n, p);
                playerAccounts.AddLast(newPlayerAccount);
                SendMessageToClient(ServerToClientSignifiers.AccountCreationComplete + "", id);

                SavePlayerAccounts();

            }
        }
        else if (signifier == ClientToServerSignifiers.Login)// login start
        {
            Debug.Log("Login start");

            string n = csv[1];
            string p = csv[2];
            bool hasNameBeenFound = false;
            bool msgHasBeenSentToClient = false;


            foreach (PlayerAccount pa in playerAccounts)// check for player account
            {
                if (pa.name == n)//name found!
                {

                    hasNameBeenFound = true;
                    Debug.Log("name founnd");

                    if (pa.password == p)// password found, login complete
                    {

                        SendMessageToClient(ServerToClientSignifiers.LoginComplete + "", id);
                        msgHasBeenSentToClient = true;
                        Debug.Log("Username and Password found Login complete!");
                    }
                    else// username or password not found, login fail
                    {
                        Debug.Log("Username or Password NOT found Login FAIL!!!!!");
                        SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);
                        msgHasBeenSentToClient = true;


                    }
                    Debug.Log("Login Complete!!!!!!!!!!!");
                }
            }
            if (!hasNameBeenFound)// account not found, login fail
            {
                SendMessageToClient(ServerToClientSignifiers.LoginFailed + ",", id);
                Debug.Log("!hasNameBeenFound?");
                Debug.Log("Account Name " + n);

                if (!msgHasBeenSentToClient)// send error message
                {
                    Debug.Log("message not sent?");
                    SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);

                }
            }
        }
        else if (signifier == ClientToServerSignifiers.JoinQueueForGameRoom)//joining queue for game room
        {
            Debug.Log("Need to get player into a waiting queue!");


            if (playerWaitingForMatchWithID == -1)// check for player with ID
            {
                Debug.Log("Client is waiting for another player!");
                playerWaitingForMatchWithID = id;
            }
            else//if both clients are in queue create the game room
            {
                Debug.Log("Client in quese else");
                GameRoom gr = new GameRoom(playerWaitingForMatchWithID, id);
                gameRooms.AddLast(gr);

                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerIDO);
                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerIDX);
                SendMessageToClient(ServerToClientSignifiers.SpectatorJoined + "", gr.spectatorID);


                playerWaitingForMatchWithID = -1;
            }
            
            
        }
        else if (signifier == ClientToServerSignifiers.TicTacToePlay)// start the game
        {
            Debug.Log("GameStart");
            GameRoom gr = GetGameRoomWithClientID(id);

            if (gr != null)
            {

                if (gr.playerIDX == id)
                {
                    SendMessageToClient(ServerToClientSignifiers.OpponentPlay + "", gr.playerIDO);
                    Debug.Log("Your Turn!");
                }
            }
            else
            {

                SendMessageToClient(ServerToClientSignifiers.OpponentPlay + "", gr.playerIDX);
                Debug.Log("Opponents Turn!");
            }
            if(signifier == ClientToServerSignifiers.TicTacToePlay)
            {
                SendMessageToClient(ServerToClientSignifiers.OpponentPlay + "", gr.spectatorID);
                spectatorJoiningMatchWithID = -1;
            }


        }
        else if(signifier == ClientToServerSignifiers.HelloButtonPressed)// hello button pressed send message
        {
            SendMessageToClient(ServerToClientSignifiers.SendHelloButtonPressed + "Hello CHAD!", id);
        }
        if(signifier == ClientToServerSignifiers.GGButtonPressed)//Good game button pressed send message
        {
            SendMessageToClient(ServerToClientSignifiers.SendGGButtonPressed + "Good Game CHAD!", id);
        }
    }

    private void SavePlayerAccounts()// save the players account on creation
    {
        Debug.Log("Start of SavePlayerAccounts");
        StreamWriter sw = new StreamWriter(playerAccountsFilePath);

        foreach (PlayerAccount pa in playerAccounts)
        {
            Debug.Log("ForEach SavePlayerAccounts");
            sw.WriteLine(PlayerAccountNameAndPassword + "," + pa.name + "," + pa.password);
        }
        sw.Close();
    }

    private void LoadPlayerAccounts()// load players account on login
    {
        Debug.Log("Start LoadPlayerAccounts");
        if (File.Exists(playerAccountsFilePath))
        {

            Debug.Log("Start File.Exists");

            StreamReader sr = new StreamReader(playerAccountsFilePath);

            string line;

            while (true)
            {
                Debug.Log("Start while(True)");
                line = sr.ReadLine();
                if (line == null)
                    break;

                string[] csv = line.Split(',');

                int signifier = int.Parse(csv[0]);

                if (signifier == PlayerAccountNameAndPassword)
                {
                    Debug.Log("Start signifier == PlayerAccountNameAndPassword");
                    PlayerAccount pa = new PlayerAccount(csv[1], csv[2]);
                    playerAccounts.AddLast(pa);
                }

            }
            Debug.Log("Load account complete!");
            sr.Close();
        }

    }




    private GameRoom GetGameRoomWithClientID(int id)// create the game room when there are 2 players, spectator can join also
    {
        Debug.Log("game room start");
        foreach (GameRoom gr in gameRooms)
        {
            Debug.Log("Game room for each");
            if (gr.playerIDX == id || gr.playerIDO == id)
            {
                return gr;
            }
            if(gr.spectatorID == id)
            {
                Debug.Log("Spectator Joined!!!!!!!!!!");
            }
                
        }
        return null;
    }

    public class PlayerAccount//  set up for player account
    {

        public string name, password;

        public PlayerAccount(string Name, string Password)
        {
            name = Name;
            password = Password;
        }
    }

    public class GameRoom
    {
        public int playerIDX, playerIDO;
        public int spectatorID;
        public GameRoom(int PlayerIDX, int PlayerIDO)
        {
            playerIDX = PlayerIDX;
            playerIDO = PlayerIDO;
            
            
            
            

        }
        
    }
}
public static class ClientToServerSignifiers
{
    public const int CreateAccount = 1;

    public const int Login = 2;

    public const int JoinQueueForGameRoom = 3;

    public const int TicTacToePlay = 4;

     public const int Win = 6;

     public const int GGButtonPressed = 7;
     public const int HelloButtonPressed = 8;
}

public static class ServerToClientSignifiers
{
    public const int LoginComplete = 11;
    public const int LoginFailed = 12;


    public const int AccountCreationComplete = 13;
    public const int AccountCreationFailed = 14;
    public const int GameStart = 15;
    public const int PlayerTurn = 16;
    public const int OpponentPlay = 17;
    public const int UpdateClientsBoard = 18;
    public const int SpectatorJoined = 19;
    public const int SendGGButtonPressed = 20;
     public const int SendHelloButtonPressed = 21;
   

}


