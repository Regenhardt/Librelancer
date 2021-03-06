﻿// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Lidgren.Network;
namespace LibreLancer
{
	public class GameServer
	{
		static readonly object TagConnecting = new object();

		public string ServerName = "Librelancer Server";
		public string ServerDescription = "Description of the server is here.";
		public string ServerNews = "News of the server goes here";

		public int Port = NetConstants.DEFAULT_PORT;
		public string AppIdentifier = NetConstants.DEFAULT_APP_IDENT;
		public string DbConnectionString;
		public GameDataManager GameData;
		public ServerDatabase Database;
        public ServerResourceManager Resources;

		volatile bool running = false;
		Thread netThread;
		Thread gameThread;
		public NetServer NetServer;

		public GameServer(string fldir)
		{
            Resources = new ServerResourceManager();
			GameData = new GameDataManager(fldir, Resources);	
		}

		public void Start()
		{
			running = true;
			gameThread = new Thread(GameThread);
            gameThread.Name = "Game";
            gameThread.Start();
			netThread = new Thread(NetThread);
            netThread.Name = "NetServer";
            netThread.Start();
		}


        Dictionary<GameData.StarSystem, ServerWorld> worlds = new Dictionary<GameData.StarSystem, ServerWorld>();
        List<GameData.StarSystem> availableWorlds = new List<GameData.StarSystem>();
        ConcurrentQueue<Action> worldRequests = new ConcurrentQueue<Action>();

        public void RequestWorld(GameData.StarSystem system, Action<ServerWorld> spunUp)
        {
            lock(availableWorlds)
            {
                if (availableWorlds.Contains(system)) { spunUp(worlds[system]); return; }
            }
            worldRequests.Enqueue(() =>
            {
                var world = new ServerWorld(system, this);
                FLLog.Info("Server", "Spun up " + system.Nickname + " (" + system.Name + ")");
                worlds.Add(system, world);
                lock (availableWorlds)
                {
                    availableWorlds.Add(system);
                }
                spunUp(world);
            });
        }

        void GameThread()
		{
			Stopwatch sw = Stopwatch.StartNew();
			double lastTime = 0;
			while (running)
			{
                Action a;
                if (worldRequests.Count > 0 && worldRequests.TryDequeue(out a))
                    a();
                //Start Loop
                var time = sw.Elapsed.TotalMilliseconds;
                var elapsed = (time - lastTime);
                if (elapsed < 1) continue;
                elapsed /= 1000f;
                lastTime = time;
                //Update
                foreach(var world in worlds.Values) {
                    world.Update(TimeSpan.FromSeconds(elapsed));
                }
                //Sleep
                Thread.Sleep(0);
			}
		}

		void NetThread()
		{
			FLLog.Info("Server","Loading Game Data...");
			GameData.LoadData();
			FLLog.Info("Server","Finished Loading Game Data");
			Database = new ServerDatabase(DbConnectionString);
			var netconf = new NetPeerConfiguration(AppIdentifier);
			netconf.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
			netconf.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            netconf.EnableMessageType(NetIncomingMessageType.UnconnectedData);
			netconf.Port = Port;
			netconf.MaximumConnections = 200;
			NetServer = new NetServer(netconf);
			NetServer.Start();
			FLLog.Info("Server", "Listening on port " + Port);
			NetIncomingMessage im;
			while (running)
			{
				while ((im = NetServer.ReadMessage()) != null)
				{
					switch (im.MessageType)
					{
						case NetIncomingMessageType.DebugMessage:
						case NetIncomingMessageType.ErrorMessage:
						case NetIncomingMessageType.WarningMessage:
						case NetIncomingMessageType.VerboseDebugMessage:
							FLLog.Info("Lidgren", im.ReadString());
							NetServer.Recycle(im);
							break;
						case NetIncomingMessageType.ConnectionApproval:
							//Ban IP?
							im.SenderConnection.Approve();
							NetServer.Recycle(im);
							break;
						case NetIncomingMessageType.DiscoveryRequest:
							NetOutgoingMessage dresp = NetServer.CreateMessage();
							//Include Server Data
							dresp.Write(ServerName);
							dresp.Write(ServerDescription);
                            dresp.Write(GameData.DataVersion);
							dresp.Write(NetServer.ConnectionsCount);
							dresp.Write(NetServer.Configuration.MaximumConnections);
							//Send off
							NetServer.SendDiscoveryResponse(dresp, im.SenderEndPoint);
							NetServer.Recycle(im);
							break;
                        case NetIncomingMessageType.UnconnectedData:
                            //Respond to pings
                            try
                            {
                                if(im.ReadUInt32() == NetConstants.PING_MAGIC)
                                {
                                    var om = NetServer.CreateMessage();
                                    om.Write(NetConstants.PING_MAGIC);
                                    NetServer.SendUnconnectedMessage(om, im.SenderEndPoint);
                                }
                            }
                            catch (Exception)
                            {
                            }
                            break;
						case NetIncomingMessageType.StatusChanged:
							NetConnectionStatus status = (NetConnectionStatus)im.ReadByte();

							string reason = im.ReadString();
							FLLog.Info("Lidgren", NetUtility.ToHexString(im.SenderConnection.RemoteUniqueIdentifier) + " " + status + ": " + reason);

							if (status == NetConnectionStatus.Connected)
							{
								FLLog.Info("Lidgren", "Remote hail: " + im.SenderConnection.RemoteHailMessage.ReadString());
								BeginAuthentication(NetServer, im.SenderConnection);
							}
                            else if (status == NetConnectionStatus.Disconnected)
                            {
                                FLLog.Info("Lidgren", im.SenderEndPoint.ToString() + " disconnected");
                                if (im.SenderConnection.Tag is NetPlayer)
                                    ((NetPlayer)im.SenderConnection.Tag).Disconnected();
                            }
                            NetServer.Recycle(im);
							break;
						case NetIncomingMessageType.Data:
                            var pkt = im.ReadPacket();
							if (im.SenderConnection.Tag == TagConnecting)
							{
                                if (pkt is AuthenticationReplyPacket)
								{
                                    var auth = (AuthenticationReplyPacket)pkt;

                                    //im.SenderConnection.Disconnect("boilerplate reason from server");
                                    /*
									var authkind = (AuthenticationKind)im.ReadByte();
									var guid = new Guid(im.ReadBytes(16));
									if (guid == Guid.Empty) im.SenderConnection.Disconnect("Invalid UUID");
									FLLog.Info("Lidgren", "GUID for " + im.SenderEndPoint + " = " + guid.ToString());
									var p = new NetPlayer(im.SenderConnection, this, guid);
									im.SenderConnection.Tag = p;
									AsyncManager.RunTask(() => p.DoAuthSuccess());*/

                                    var p = new NetPlayer(im.SenderConnection, this, auth.Guid);
                                    im.SenderConnection.Tag = p;
                                    AsyncManager.RunTask(() => p.DoAuthSuccess());
								}
								else
								{
									im.SenderConnection.Disconnect("Invalid Packet");
								}
                                NetServer.Recycle(im);

                            }
                            else
							{
                                var player = (NetPlayer)im.SenderConnection.Tag;
								AsyncManager.RunTask(() => player.ProcessPacket(pkt));
                                NetServer.Recycle(im);

                            }
                            break;
					}
				}
				Thread.Sleep(0); //Reduce CPU load
			}
            NetServer.Shutdown("Shutdown");
			Database.Dispose();
		}
            
		void BeginAuthentication(NetServer server, NetConnection connection)
		{
			var msg = server.CreateMessage();
            msg.Write(new AuthenticationPacket()
            {
                Type = AuthenticationKind.GUID
            });
			server.SendMessage(msg, connection, NetDeliveryMethod.ReliableOrdered);
			connection.Tag = TagConnecting;
		}

		public void Stop()
		{
			running = false;
			netThread.Join();
			gameThread.Join();
		}
	}
}
