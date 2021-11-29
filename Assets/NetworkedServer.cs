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
    int socketPort = 5491;//5496

    LinkedList<PlayerAccount> playerAccounts;

    const int PlayerAccountNameAndPassword = 1;

    string playerAccountsFilePath;


    int playerWaitingForMatchWithID = -1;

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

        //foreach(PlayerAccount pa in playerAccounts);

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

            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.name == n)
                {
                    nameIsInUse = true;
                    break;
                }
            }
            if (nameIsInUse)
            {
                Debug.Log("Name is in use");
                SendMessageToClient(ServerToClientSignifiers.AccountCreationFailed + "", id);

            }
            else
            {
                Debug.Log("Name is not in use");
                PlayerAccount newPlayerAccount = new PlayerAccount(n, p);
                playerAccounts.AddLast(newPlayerAccount);
                SendMessageToClient(ServerToClientSignifiers.AccountCreationComplete + "", id);

                SavePlayerAccounts();

            }
        }
        else if (signifier == ClientToServerSignifiers.Login)
        {
            Debug.Log("Login start");

            string n = csv[1];
            string p = csv[2];
            bool hasNameBeenFound = false;
            bool msgHasBeenSentToClient = false;


            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.name == n)
                {

                    hasNameBeenFound = true;
                    Debug.Log("name founnd");

                    if (pa.password == p)
                    {

                        SendMessageToClient(ServerToClientSignifiers.LoginComplete + "", id);
                        msgHasBeenSentToClient = true;
                        Debug.Log("Username and Password found Login complete!");
                    }
                    else
                    {
                        Debug.Log("Username or Password NOT found Login FAIL!!!!!");
                        SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);
                        msgHasBeenSentToClient = true;


                    }
                    Debug.Log("Login Complete!!!!!!!!!!!");
                }
            }
            if (!hasNameBeenFound)
            {
                SendMessageToClient(ServerToClientSignifiers.LoginFailed + ",", id);
                Debug.Log("!hasNameBeenFound?");
                Debug.Log("Account Name " + n);

                if (!msgHasBeenSentToClient)
                {
                    Debug.Log("message not sent?");
                    SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);

                }
            }
        }//this used to be after tic tac toe play
        else if (signifier == ClientToServerSignifiers.JoinQueueForGameRoom)
        {
            Debug.Log("Need to get player into a waiting queue!");


            if (playerWaitingForMatchWithID == -1)
            {
                Debug.Log("Client is waiting for another player!");
                playerWaitingForMatchWithID = id;
            }
            else
            {
                Debug.Log("Client in quese else");
                GameRoom gr = new GameRoom(playerWaitingForMatchWithID, id);
                gameRooms.AddLast(gr);

                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerID2);
                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerID1);


                playerWaitingForMatchWithID = -1;
            }
        }
        else if (signifier == ClientToServerSignifiers.TicTacToePlay)
        {
            Debug.Log("GameStart");
            GameRoom gr = GetGameRoomWithClientID(id);

            if (gr != null)
            {

                if (gr.playerID1 == id)
                {
                    SendMessageToClient(ServerToClientSignifiers.OpponentPlay + "", gr.playerID2);
                }
            }
            else
            {

                SendMessageToClient(ServerToClientSignifiers.OpponentPlay + "", gr.playerID1);
            }

        }
    }

    private void SavePlayerAccounts()
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

    private void LoadPlayerAccounts()
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




    private GameRoom GetGameRoomWithClientID(int id)
    {
        Debug.Log("game room start");
        foreach (GameRoom gr in gameRooms)
        {
            Debug.Log("Game room for each");
            if (gr.playerID1 == id || gr.playerID2 == id)
                return gr;
        }
        return null;
    }

    public class PlayerAccount
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
        public int playerID1, playerID2;

        public GameRoom(int PlayerID1, int PlayerID2)
        {
            playerID1 = PlayerID1;
            playerID2 = PlayerID2;

            

        }
    }
}
public static class ClientToServerSignifiers
{
    public const int CreateAccount = 1;

    public const int Login = 2;

    public const int JoinQueueForGameRoom = 3;

    public const int TicTacToePlay = 4;
    public const int MsgSentFromClientToClient = 5;


}

public static class ServerToClientSignifiers
{
    public const int LoginComplete = 11;
    public const int LoginFailed = 12;

    public const int AccountCreationComplete = 13;
    public const int AccountCreationFailed = 14;

    public const int OpponentPlay = 15;

    public const int GameStart = 16;
    public const int UpdateClientsBoard = 17;

}


