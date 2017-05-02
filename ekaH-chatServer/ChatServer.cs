using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace ekaH_chatServer
{
    /// <summary>
    /// This class represents the current open client connection. It connects
    /// the client's email to their socket. The email acts as the id for the client.
    /// </summary>
    class Client
    {
        /// <summary>
        /// It holds the client's socke.t
        /// </summary>
        public Socket m_clientSocket { get; set; }

        /// <summary>
        /// It holds the end point address of the client.
        /// </summary>
        public string m_endPoint { get; set; }

        /// <summary>
        /// It holds the email id of the client.
        /// </summary>
        public string m_emailID { get; set; }

        /// <summary>
        /// This is a constructor.
        /// </summary>
        /// <param name="a_soc">It holds the client's socket.</param>
        /// <param name="a_tempName">It holds the end point address of the client.</param>
        public Client(Socket a_soc, string a_tempName)
        {
            m_clientSocket = a_soc;
            m_endPoint = a_tempName;
        }
    }

    /// <summary>
    /// This class is the server for Ekah's online chat functionality.
    /// </summary>
    class ChatServer
    {
        /// <summary>
        /// It is the global buffer tha holds the messages received.
        /// </summary>
        private static byte[] m_globalBuffer = new byte[1024];

        /// <summary>
        /// It holds the total number of clients currently connected.
        /// </summary>
        private static int m_totalClients = 0;

        /// <summary>
        /// It represents the server's socket.
        /// </summary>
        private static Socket m_serverSocket;

        /// <summary>
        /// It is the list of all the clients. It holds the email addresses.
        /// </summary>
        private static List<string> m_allClients = new List<string>();

        /// <summary>
        /// This stores the map of client email address to the particular clients.
        /// email --> Client
        /// </summary>
        private static Dictionary<string, Client> m_clientTable = new Dictionary<string,Client>();

        /// <summary>
        /// It is the instance of this class made in order to make the class singleton.
        /// </summary>
        private static ChatServer m_chatServer;

        /// <summary>
        /// This is a constructor that initiates the server to start listening.
        /// </summary>
        private ChatServer()
        {
            m_serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            /// Sets up the server so that it can start listening to a port.
            SetupServer();
            Console.ReadLine();
        }

        /// <summary>
        /// This function returns the instance of this class.
        /// </summary>
        /// <returns>Returns the instance of the class.</returns>
        public static ChatServer GetInstance()
        {
            if (m_chatServer == null)
            {
                m_chatServer = new ChatServer();
            }

            return m_chatServer;
        }
        
        /// <summary>
        /// This function sets up the server by listening to port 4050 
        /// and lets up to 5 connections to wait.
        /// </summary>
        private void SetupServer()
        {
            Console.WriteLine("Setting up the server...........");
            m_serverSocket.Bind(new IPEndPoint(IPAddress.Any, 4050));
            m_serverSocket.Listen(5);

            Console.Write("Server Running.............");
            m_serverSocket.BeginAccept(new AsyncCallback(AcceptCallBack), null);
        }

        /// <summary>
        /// This method accepts the client and saves it for later connections purposes.
        /// It then starts to accept other connections.
        /// </summary>
        /// <param name="a_asyncRes">It holds the async result which holds the values passed
        /// between the threads.</param>
        private static void AcceptCallBack(IAsyncResult a_asyncRes )
        {
            /// Gets the client socket
            Socket clientSoc = m_serverSocket.EndAccept(a_asyncRes);
            string tempName = clientSoc.RemoteEndPoint.ToString();

            Client newClient = new Client(clientSoc, tempName);
            
            /// Begins receiving the data in the socket.
            clientSoc.BeginReceive(m_globalBuffer, 0, m_globalBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallBack), newClient);

            /* After ending the accept to get the client, the server stops accepting the
             * clients. So, begins it again */
            m_serverSocket.BeginAccept(new AsyncCallback(AcceptCallBack), null);
        }

        /// <summary>
        /// This is a call back function called when data is received from a client.
        /// </summary>
        /// <param name="a_asyncRes">It holds the async result.</param>
        private static void ReceiveCallBack(IAsyncResult a_asyncRes)
        {
            /// Gets the client that is passing in the data.
            Client client = (Client)a_asyncRes.AsyncState;

            Socket clientSoc = client.m_clientSocket;

            string remoteEndpoint = clientSoc.RemoteEndPoint.ToString();

            /// Remove the socket from the connected list if the client is not connected.
            if (!clientSoc.Connected)
            {
                RemoveClient(client);
                Console.WriteLine("Client left!");
                Console.WriteLine("Total online clients: " + m_totalClients);
            }

            /// Receives the data amount.
            int received = 0;

            try
            {
                received = clientSoc.EndReceive(a_asyncRes);
            }
            catch (Exception)
            {
                Console.WriteLine("Could not receive the data");
                RemoveClient(client);
                return;
            }

            /// Checks if received data is empty. If not, then addresses the text received and
            /// handles it according to the plan.
            if (received != 0)
            {
                byte[] tempBuff = new byte[received];

                Array.Copy(m_globalBuffer, tempBuff, received);

                /// Decodes the message received.
                string textReceived = Encoding.ASCII.GetString(tempBuff);
                Console.WriteLine("Received from " + clientSoc.RemoteEndPoint.ToString());
                Console.WriteLine("\t " + textReceived);

                /// Check if the string starts with @@@. It means that the client just connected. 
                /// So, modify the value in the map.
                if (textReceived.StartsWith("@@@"))
                {
                    /// This is the first time the client is actually identified with the email id.
                    /// Add the client to the dictionary of active users.
                    AddClient(ref client, textReceived.Substring(3));
                    
                }
                else if (textReceived.Contains(":@:"))
                {
                    /// Handles the message sending process to the email address mentioned.
                    /// :@: represents the message and to whom is it being sent. Left side 
                    /// represents who should receive it.
                    string[] splitted = textReceived.Split(new string[] { ":@:" }, 2, StringSplitOptions.None);
                    string email = splitted[0];
                    string toSend = client.m_emailID+":@:"+ splitted[1];
                    

                    byte[] sendingBuff = Encoding.ASCII.GetBytes(toSend);

                    /// Sends the text from the sender to the desired receiver that we have 
                    /// decoded from the data sent.
                    if (m_clientTable.ContainsKey(email))
                    {
                        Client tempClient = m_clientTable[email];
                        Console.WriteLine("sending message to: " + email + " " + m_clientTable[email].m_endPoint + "is");
                        tempClient.m_clientSocket.BeginSend(sendingBuff, 0, sendingBuff.Length, SocketFlags.None, new AsyncCallback(SendCallBack), tempClient.m_clientSocket);
                    }
                }
                else if (textReceived.StartsWith("@@ll"))
                {
                    /// Send back the list of online users.
                    SendActiveUsersList();
                }
                else
                {
                    /// Says that the message was not delivered if any issues were found.
                    string textToSend = "Something went quack. Message wasn't delivered";
                    byte[] sendingBuff = Encoding.ASCII.GetBytes(textToSend);
                    clientSoc.BeginSend(sendingBuff, 0, sendingBuff.Length, SocketFlags.None, new AsyncCallback(SendCallBack), client);
                }
                
            }

            /// Begins receiving in the socket again after the message is handled.
            clientSoc.BeginReceive(m_globalBuffer, 0, m_globalBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallBack), client);
        }

        /// <summary>
        /// This is a callback function when the data is sent.
        /// </summary>
        /// <param name="a_asyncRes">It is the async result.</param>
        private static void SendCallBack(IAsyncResult a_asyncRes)
        {
            
            Socket clientSoc = (Socket)a_asyncRes.AsyncState;

            clientSoc.EndSend(a_asyncRes);
        }

        /// <summary>
        /// This function adds the client to the list of existing open connections.
        /// </summary>
        /// <param name="a_newClient">It represents the new client.</param>
        /// <param name="a_email">It holds the email of the client.</param>
        private static void AddClient(ref Client a_newClient, string a_email)
        {
            if (!m_clientTable.ContainsKey(a_email))
            {
                /// Adds the client to the lists.
                a_newClient.m_emailID = a_email;

                m_clientTable.Add(a_email, a_newClient);
                m_allClients.Add(a_email);


                Console.WriteLine("Client connected: " + a_email + " -->" + a_newClient.m_endPoint);

                /// Updates all the connected users about the new list of online users.
                SendActiveUsersList();
                m_totalClients++;
            }
        }

        /// <summary>
        /// This function remores the client from the list.
        /// </summary>
        /// <param name="a_soc">It represents the client socket that is being removed.</param>
        private static void RemoveClient(Client a_soc)
        {
            /// If the email id is null or empty, then we have not even added 
            /// this client to our list. So, no need to worry about it.
            if (string.IsNullOrEmpty(a_soc.m_emailID))
            {
                return;
            }

            if (m_clientTable.ContainsKey(a_soc.m_emailID))
            {
                /// Remove it from the dictionary and the list.
                m_clientTable.Remove(a_soc.m_emailID);
                m_allClients.Remove(a_soc.m_emailID);

                Console.WriteLine("Removing client " + a_soc.m_emailID);

                SendActiveUsersList();
                m_totalClients--;
            }
        }

        /// <summary>
        /// This function sends the new list of online users to all the online users.
        /// </summary>
        private static void SendActiveUsersList()
        {
            /// Builds the string such that the clients know that it is not a conversation
            /// message but it is the new list of online users.
            StringBuilder toSend = new StringBuilder("$clients$");
            
            foreach(string client in m_allClients)
            {
                toSend.Append(client);
                toSend.Append("|");
            }

            byte[] dataToSend = Encoding.ASCII.GetBytes(toSend.ToString());

            /// It sends the new list of online users to all the existing users.
            foreach (KeyValuePair<string, Client> entry in m_clientTable)
            {
                Console.WriteLine("Sending user list to " + entry.Value.m_emailID);
                Socket soc = entry.Value.m_clientSocket;
                soc.BeginSend(dataToSend, 0, dataToSend.Length, SocketFlags.None, new AsyncCallback(SendCallBack), soc);
            }
        }
    }
}
