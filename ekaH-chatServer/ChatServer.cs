using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ekaH_chatServer
{
    class Client
    {
        public Socket ClientSocket { get; set; }
        public string EndPoint { get; set; }
        public string EmailID { get; set; }

        public Client(Socket soc, string tempName, string id)
        {
            EmailID = id;
            ClientSocket = soc;
            EndPoint = tempName;
        }

        public Client(Socket soc, string tempName)
        {
            ClientSocket = soc;
            EndPoint = tempName;
        }
    }

    class ChatServer
    {
        private static byte[] globalBuffer = new byte[1024];
        private static int totalClients = 0;

        private static Socket serverSocket;
        //private static List<Client> allClients;

        // This stores the map of client end point address to the particular clients.
        private static Dictionary<string, Client> clientTable = new Dictionary<string,Client>();

        private static ChatServer chatServer;

        private ChatServer()
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //allClients = new List<Client>();

            setupServer();
            Console.ReadLine();
        }

        public static ChatServer getInstance()
        {
            if (chatServer == null)
            {
                chatServer = new ChatServer();
            }

            return chatServer;
        }
        
        private void setupServer()
        {
            Console.WriteLine("Setting up the server...........");
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, 4050));
            serverSocket.Listen(5);

            Console.Write("Server Running.............");
            serverSocket.BeginAccept(new AsyncCallback(acceptCallBack), null);
        }

        /*
         * This method accepts the client and saves it for later connections purposes.
         * It then starts to accept other connections.
         * */
        private static void acceptCallBack(IAsyncResult asyncRes )
        {
            Socket clientSoc = serverSocket.EndAccept(asyncRes);
            string tempName = clientSoc.RemoteEndPoint.ToString();

            Client newClient = new Client(clientSoc, tempName);

            //addClient(clientSoc);

            // Begins receiving the data in the socket.
            clientSoc.BeginReceive(globalBuffer, 0, globalBuffer.Length, SocketFlags.None, new AsyncCallback(receiveCallBack), newClient);

            /* After ending the accept to get the client, the server stops accepting the
             * clients. So, begins it again */
            serverSocket.BeginAccept(new AsyncCallback(acceptCallBack), null);
        }


        private static void receiveCallBack(IAsyncResult asyncRes)
        {
            Client client = (Client)asyncRes.AsyncState;
            //Socket clientSoc = (Socket)asyncRes.AsyncState;
            Socket clientSoc = client.ClientSocket;

            string remoteEndpoint = clientSoc.RemoteEndPoint.ToString();

            // Remove the socket from the connected list if the client is not connected.
            if (!clientSoc.Connected)
            {
                removeClient(client);
                Console.WriteLine("Client left!");
                Console.WriteLine("Total online clients: " + totalClients);

            }

            // Receives the data amount.
            int received = 0;

            try
            {
                received = clientSoc.EndReceive(asyncRes);
            }
            catch (Exception)
            {
                Console.WriteLine("Could not receive the data");
                return;
            }

            if (received != 0)
            {
                byte[] tempBuff = new byte[received];

                Array.Copy(globalBuffer, tempBuff, received);

                string textReceived = Encoding.ASCII.GetString(tempBuff);
                Console.WriteLine("Received from " + clientSoc.RemoteEndPoint.ToString());
                Console.WriteLine("\t " + textReceived);

                // Check if the string starts with @@@. It means that the client just connected. So, modify the value in the map.
                if (textReceived.StartsWith("@@@"))
                {
                    // This is the first time the client is actually identified with the email id.
                    // Add the client to the dictionary of active users.
                    addClient(clientSoc, textReceived.Substring(3));
                    
                }
                else if (textReceived.Contains(":@:"))
                {
                    // Handles the message sending process to the email address mentioned.
                    // :@: represents the message and to whom is it being sent. Left side represents who should receive it.
                    string[] splitted = textReceived.Split(new string[] { ":@:" }, 2, StringSplitOptions.None);
                    string email = splitted[0];
                    string toSend = client.EmailID+":@:"+ splitted[1];
                    
                    // TODO Check what is received and what is to be done to the things received 
                    // and who to send it to.

                    byte[] sendingBuff = Encoding.ASCII.GetBytes(toSend);
                    if (clientTable.ContainsKey(email))
                    {
                        Client tempClient = clientTable[email];
                        Console.WriteLine("sending message to: " + email + " " + clientTable[email].EndPoint + "is");
                        tempClient.ClientSocket.BeginSend(sendingBuff, 0, sendingBuff.Length, SocketFlags.None, new AsyncCallback(sendCallBack), tempClient.ClientSocket);
                    }
                }
                else
                {
                    string textToSend = "Something went quack. Message wasn't delivered";
                    byte[] sendingBuff = Encoding.ASCII.GetBytes(textToSend);
                    clientSoc.BeginSend(sendingBuff, 0, sendingBuff.Length, SocketFlags.None, new AsyncCallback(sendCallBack), client);
                }
                
            }

            clientSoc.BeginReceive(globalBuffer, 0, globalBuffer.Length, SocketFlags.None, new AsyncCallback(receiveCallBack), client);

        }

        private static void sendCallBack(IAsyncResult asyncRes)
        {
            
            Socket clientSoc = (Socket)asyncRes.AsyncState;
            

            clientSoc.EndSend(asyncRes);
        }

        private static void addClient(Socket soc, string email)
        {
            
            if (!clientTable.ContainsKey(email))
            {
                string tempName = soc.RemoteEndPoint.ToString();

                Client newClient = new Client(soc, tempName,email);

                clientTable.Add(email, newClient);
                //allClients.Add(new Client(soc, tempName));

                Console.WriteLine("Client connected: " + email + " -->" + tempName);

                totalClients++;
            }
        }

        private static void removeClient(Client soc)
        {
            // If the email id is null or empty, then we have not even added this client to our list. So, no need to worry about it.
            if (string.IsNullOrEmpty(soc.EmailID))
            {
                return;
            }
            if (clientTable.ContainsKey(soc.EmailID))
            {
                // Remove it from the dictionary and the list.
                clientTable.Remove(soc.EmailID);

                totalClients--;
            }
        }
    }
}
