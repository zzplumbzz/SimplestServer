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

        //foreach(PlayerAccount pa in playerAccounts)
        //Debug.Log(pa.name + " " + pa.password);

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
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }
    
    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');

        int signifier = int.Parse(csv[0]);

        if(signifier == ClientToServerSignifiers.CreateAccount)
        {
            
            Debug.Log("Create Account");

            string n = csv[1];
            string p = csv[2];
            bool nameIsInuse = false;

            foreach(PlayerAccount pa in playerAccounts)
            {
                    if(pa.name == n)
                    {
                        nameIsInuse = true;
                        break;
                    }
            }
                    if (nameIsInuse)
                    {
                        SendMessageToClient(ServerToClientSignifiers.AccountCreationFailed + "", id);
                        
                    }
                    else
                    {
                        PlayerAccount newPlayerAccount = new PlayerAccount(n, p);
                        playerAccounts.AddLast(newPlayerAccount);
                        SendMessageToClient(ServerToClientSignifiers.AccountCreationComplete + "", id);

                        SavePlayerAccounts();

                    }
            }
             else if(signifier == ClientToServerSignifiers.Login)
                    {
                        Debug.Log("Login to account");

                        string n = csv[1];
                        string p = csv[2];
                        bool hasNameBeenFound = false;
                        bool msgHasBeenSentToClient = false;


                        foreach(PlayerAccount pa in playerAccounts)
                        {
                            if(pa.name == n)
                            {
                                hasNameBeenFound = true;

                                if(pa.password == p)
                                {
                                    SendMessageToClient(ServerToClientSignifiers.LoginComplete + "", id);
                                    msgHasBeenSentToClient = true;
                                }
                                else
                                {
                                    SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);
                                    msgHasBeenSentToClient = true;


                                }

                            }

                            else if(signifier == ClientToServerSignifiers.JoinQueueForGame)
                            {
                                Debug.Log("waiting Queue");


                                if(playerWaitingForMatchWithID == -1)
                                {
                                    playerWaitingForMatchWithID = id;   
                                }
                                else
                                {
                                    GameRoom gr = new GameRoom(playerWaitingForMatchWithID, id);
                                    gameRooms.AddLast(gr);

                                    SendMessageToClient(ServerToClientSignifiers.GameStart + "" , gr.playerID2);
                                    SendMessageToClient(ServerToClientSignifiers.GameStart + "" , gr.playerID1);

                                   playerWaitingForMatchWithID = -1;
                                }
                                
                            }

                            else if(signifier == ClientToServerSignifiers.TicTacToePlay)
                            {
                                GameRoom gr = GetGameRoomWithClientID(id);

                                if (gr != null)
                                {
                                    if(gr.playerID1 == id)
                                    SendMessageToClient(ServerToClientSignifiers.OpponentPlay + "" , gr.playerID2);
                                }
                                else
                                {
                                    SendMessageToClient(ServerToClientSignifiers.OpponentPlay + "" , gr.playerID1);
                                }
                            }
                            
                        }

                        if(!hasNameBeenFound)
                        {
                            if(!msgHasBeenSentToClient)
                            {
                                SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);

                            }
                        }
                    }
        
        }

        private void SavePlayerAccounts()
        {
            StreamWriter sw = new StreamWriter(playerAccountsFilePath);

            foreach(PlayerAccount pa in playerAccounts)
            {
                sw.WriteLine(PlayerAccountNameAndPassword + "," + pa.name + "," + pa.password);
            }
            sw.Close();
        }

        private void LoadPlayerAccounts()
        {

            if(File.Exists(playerAccountsFilePath))
            {

            

            StreamReader sr = new StreamReader(playerAccountsFilePath);

            string line;

                while(true)
                {
                    line = sr.ReadLine();
                    if(line == null)
                    break;
                    string[] csv = line.Split(',');

                    int signifier = int.Parse(csv[0]);

                    if(signifier == PlayerAccountNameAndPassword)
                    {
                        PlayerAccount pa = new PlayerAccount(csv[1], csv[2]);
                        playerAccounts.AddLast(pa);
                    }
                    /*else if(signifier == )
                    {
                        
                    }*/
                }
                sr.Close();
            }

        }
    

    private GameRoom GetGameRoomWithClientID(int id)
    {
        foreach(GameRoom gr in gameRooms)
        {
            if(gr.playerID1 == id || gr.playerID2 == id)
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


public static class ClientToServerSignifiers
{
    public const int CreateAccount = 1;

    public const int Login = 2;

    public const int JoinQueueForGame = 3;

    public const int TicTacToePlay = 4;

}

public static class ServerToClientSignifiers
{
    public const int LoginComplete = 1;
     public const int LoginFailed = 2;

    public const int AccountCreationComplete = 3;
    public const int AccountCreationFailed = 4;

    public const int OpponentPlay = 5;

    public const int GameStart = 6;


}
}