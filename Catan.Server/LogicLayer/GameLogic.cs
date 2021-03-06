﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Catan.Game;
using Catan.Network.Messaging;
using System.Net;
using System.Drawing;
using Catan.Server.PersistenceLayer;
using System.Xml.Serialization;
using System.IO;
using System.Runtime.Serialization;

namespace Catan.Server.LogicLayer
{
    public class GameLogic : Interfaces.ILogicLayer
    {
        private Interfaces.INetworkLayer iNetworkLayer;
        private Interfaces.IPersistenceLayer iPersistenceLayer;

        private CatanClient currentClient;
        private CatanClientMessageHandler messageHandler;
        private bool previousTurnDone ;

        private List<CatanClient> catanClients;
        private const int MAX_SIEGPUNKTE_WINN = 5;
        private Color[] clientColors ={Color.Red,Color.Blue,Color.Green,Color.Yellow };

        public GameLogic()
        {
            this.iPersistenceLayer = new PersistenceManager();
            this.catanClients = new List<CatanClient>();
            this.messageHandler = new LogicLayer.CatanClientMessageHandler();
        }
        private CatanClient getNextClient()
        {
            if (currentClient == null)
            {
                // Der letzte Spieler ist als erster dran 
                catanClients.Reverse();
                return catanClients.First();
            }
            else
            {
                return catanClients[(catanClients.IndexOf(currentClient) + 1) % catanClients.Count];
            }

        }
        public void ServerFinishedListening(List<CatanClient> catanClients)
        {
            // Let clients play catan !
            this.catanClients.Clear();
            this.catanClients.AddRange(catanClients);
            setClientsColor();

            currentClient = getNextClient();

            // damit die Clients bei der ersten Runde eine Stadt und Strasse bauen können ...
            catanClients.ForEach(client => initKartenContainerWithStartKarten(client));

            clearAllowedSpielFigurenByClients();
            setAllowedSpielFigurenByClient(currentClient);

            iNetworkLayer.SendBroadcastMessage(new GameStateMessage(catanClients, currentClient, null, HexagonGrid.Instance.Hexagones));
        }

        private void initKartenContainerWithStartKarten(CatanClient client)
        {
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Eisen);
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Eisen);
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Eisen);
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Eisen);

            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Getreide);
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Getreide);
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Getreide);
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Getreide);

            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Wolle);
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Wolle);
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Wolle);
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Wolle);

            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Wasser);
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Wasser);
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Wasser);
            client.KartenContainer.AddRohstoffkarte(KartenContainer.Rohstoffkarte.Wasser);

        }
        private void setClientsColor()
        {
            if (catanClients.Count>clientColors.Length)
            {
                throw new IndexOutOfRangeException();
            }

            for (int clientîndex = 0; clientîndex < this.catanClients.Count; clientîndex++)
            {
                catanClients[clientîndex].Color = clientColors[clientîndex];
            }
        }
        private void clearAllowedSpielFigurenByClients()
        {
            this.catanClients.ForEach(client => client.AllowedSiedlungen = client.AllowedStaedte = client.AllowedStrassen = null);
        }
        private void setAllowedSpielFigurenByClient(CatanClient client)
        {
            if (BuildChecker.CanBuildSiedlung(client.KartenContainer))
            {
                client.AllowedSiedlungen = getAllowedSiedlungenByClient(client);
            }
            if (BuildChecker.CanBuildStadt(client.KartenContainer))
            {
                client.AllowedStaedte = getAllowedStaedteByClient(client);
            }
            if (BuildChecker.CanBuildStrasse(client.KartenContainer))
            {
                client.AllowedStrassen = getAllowedStrassenByClient(client);
            }
        }
        private bool[][][] getAllowedStrassenByClient(CatanClient client)
        {
            if (client.SpielfigurenContainer.Siedlungen?.Count<=0)
                return null;

            bool[][][] allowedStrassen = initilize3DBoolArrayBasedOnHexfields();
            #region An den eigenen Siedlungen und Städten dürfen Straßen gebaut werden

            // Siedlungen
            foreach (var siedlung in client.SpielfigurenContainer.Siedlungen)
            {
                var hexPosEdges = HexagonGrid.GetHexagonEdgesByGridPoint(HexagonGrid.Instance.HexagonesList, HexagonGrid.GetGridPointByHexagonPositionAndPoint(siedlung.HexagonPosition, siedlung.HexagonPoint));
                foreach (var hexPosEdge in hexPosEdges)
                {
                    allowedStrassen[hexPosEdge.HexagonPosition.RowIndex][hexPosEdge.HexagonPosition.ColumnIndex][hexPosEdge.HexagonEdge.Index] =
                    client.SpielfigurenContainer.Strassen.Find(strasse => 
                    HexagonGrid.IsHexagonEdgeOnHexagonEdge(hexPosEdge, new HexagonPositionHexagonEdge(strasse.HexagonPosition, strasse.HexagonEdge))) == null;
                }
            }
            // Städte
            foreach (var stadt in client.SpielfigurenContainer.Staedte)
            {
                var hexPosEdges = HexagonGrid.GetHexagonEdgesByGridPoint(HexagonGrid.Instance.HexagonesList, HexagonGrid.GetGridPointByHexagonPositionAndPoint(stadt.HexagonPosition, stadt.HexagonPoint));
                foreach (var hexPosEdge in hexPosEdges)
                {
                    allowedStrassen[hexPosEdge.HexagonPosition.RowIndex][hexPosEdge.HexagonPosition.ColumnIndex][hexPosEdge.HexagonEdge.Index] =
                    client.SpielfigurenContainer.Strassen.Find(strasse =>
                    HexagonGrid.IsHexagonEdgeOnHexagonEdge(hexPosEdge, new HexagonPositionHexagonEdge(strasse.HexagonPosition, strasse.HexagonEdge))) == null;
                }
            }

            #endregion

            #region An den eigenen Straßen dürfen Straßen gebaut werden und auf der keine fremde Siedlung oder Stadt steht

            foreach (var strasse in client.SpielfigurenContainer.Strassen)
            {

                var gridPointA = HexagonGrid.GetGridPointByHexagonPositionAndPoint(strasse.HexagonPosition, strasse.HexagonEdge.PointA);
                var gridPointB = HexagonGrid.GetGridPointByHexagonPositionAndPoint(strasse.HexagonPosition, strasse.HexagonEdge.PointB);

                foreach (var allowedGridPoint in new List<GridPoint>() {gridPointA,gridPointB })
                {
                    foreach (var allowedHexagonEdge in HexagonGrid.GetHexagonEdgesByGridPoint(HexagonGrid.Instance.HexagonesList, allowedGridPoint).Where(hexPosEdge =>
                              catanClients.Find(_client => _client.SpielfigurenContainer.Strassen.Find(_strasse =>
                              HexagonGrid.IsHexagonEdgeOnHexagonEdge(hexPosEdge, new HexagonPositionHexagonEdge(_strasse.HexagonPosition, _strasse.HexagonEdge))) != null) == null).ToList())

                    {
                        allowedStrassen[allowedHexagonEdge.HexagonPosition.RowIndex][allowedHexagonEdge.HexagonPosition.ColumnIndex][allowedHexagonEdge.HexagonEdge.Index] = true;
                    }
                }
            }

            #endregion

            return allowedStrassen;
        }
        private bool[][][] getAllowedStaedteByClient(CatanClient client)
        {
            if (client.SpielfigurenContainer.Siedlungen?.Count<=0)
                return null;

            var allowedStaedte = initilize3DBoolArrayBasedOnHexfields();

            foreach (var siedlung in client.SpielfigurenContainer.Siedlungen)
            {
                var gridPoint = HexagonGrid.GetGridPointByHexagonPositionAndPoint(siedlung.HexagonPosition, siedlung.HexagonPoint);
                foreach (var hexPosEdge in HexagonGrid.GetHexagonEdgesByGridPoint(HexagonGrid.GetHexagonesByGridPoint(gridPoint), gridPoint))
                {
                    if (client.SpielfigurenContainer.Strassen.Find(strasse =>
                        HexagonGrid.IsHexagonEdgeOnHexagonEdge(hexPosEdge, new HexagonPositionHexagonEdge(strasse.HexagonPosition, strasse.HexagonEdge))) != null)
                    {
                        allowedStaedte[siedlung.HexagonPosition.RowIndex][siedlung.HexagonPosition.ColumnIndex][siedlung.HexagonPoint.Index] = true;
                        break;
                    }
                }
            }

            return allowedStaedte;
        }
        private bool[][][] initilize3DBoolArrayBasedOnHexfields()
        {
            var hexfields = HexagonGrid.Instance.Hexagones;

            bool[][][] ob = new bool[hexfields.Length][][];
            for (int rowIndex = 0; rowIndex < hexfields.Length; rowIndex++)
            {
                ob[rowIndex] = new bool[hexfields[rowIndex].GetLength(0)][];
                for (int columnIndex = 0; columnIndex < ob[rowIndex].GetLength(0); columnIndex++)
                {
                    ob[rowIndex][columnIndex] = new bool[6];
                }
            }

            return ob;
        }
        private bool[][][] getAllowedSiedlungenByClient(CatanClient client)
        {
            bool[][][] allowedSiedlungen = initilize3DBoolArrayBasedOnHexfields();

            for (int rowIndex = 0; rowIndex < allowedSiedlungen.GetLength(0); rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < allowedSiedlungen[rowIndex].GetLength(0); columnIndex++)
                {
                    for (int pointIndex = 0; pointIndex < allowedSiedlungen[rowIndex][columnIndex].GetLength(0); pointIndex++)
                    {
                        var currentGridPoint = HexagonGrid.GetGridPointByHexagonPositionAndPoint(new HexagonPosition(rowIndex, columnIndex), new HexagonPoint(pointIndex));

                        var foundHexagones = HexagonGrid.GetHexagonesByGridPoint(currentGridPoint);
                        if (foundHexagones.Count >= 2)
                        {
                            #region Überprüfen, ob es schon eine Siedlung an diesem GridPoint angelegt wurde, wenn ja, dann ignorieren

                            var hexPositionAndHexPoint =HexagonGrid.GetHexagonAndHexagonPointByGridPoint(currentGridPoint);
                            if (hexPositionAndHexPoint.Exists(hexPosHexPoint=> allowedSiedlungen[hexPosHexPoint.HexagonPosition.RowIndex][hexPosHexPoint.HexagonPosition.ColumnIndex][hexPosHexPoint.Point.Index]))
                                continue;

                            #endregion

                            #region Überprüfen, ob es hier eine Stadt oder Siedlung gibt

                            var stadtGefunden = catanClients.Exists(_client => _client.SpielfigurenContainer.Staedte.Find(
                                stadt => HexagonGrid.GetGridPointByHexagonPositionAndPoint(stadt.HexagonPosition, stadt.HexagonPoint).Equals(currentGridPoint)) != null);

                            var siedlungGefunden = catanClients.Exists(_client => _client.SpielfigurenContainer.Siedlungen.Find(
                                siedlung => HexagonGrid.GetGridPointByHexagonPositionAndPoint(siedlung.HexagonPosition, siedlung.HexagonPoint).Equals(currentGridPoint)) != null);

                            #endregion

                            if (!stadtGefunden && !siedlungGefunden)
                            {
                                #region Überprüfen, ob die drei angrenzenden Kreuzungen von Siedlungen oder Städten besetzt sind

                                allowedSiedlungen[rowIndex][columnIndex][pointIndex] = catanClients.Find(_client =>

                                  _client.SpielfigurenContainer.Siedlungen.Find(siedlung =>
                                  HexagonGrid.GetHexagonEdgesByGridPoint(foundHexagones, currentGridPoint).Find(hex =>
                                  HexagonGrid.IsGridPointOnHexagonEdge(hex.HexagonPosition, hex.HexagonEdge,
                                  HexagonGrid.GetGridPointByHexagonPositionAndPoint(siedlung.HexagonPosition, siedlung.HexagonPoint))) != null) != null ||

                                   _client.SpielfigurenContainer.Staedte.Find(stadt =>
                                  HexagonGrid.GetHexagonEdgesByGridPoint(foundHexagones, currentGridPoint).Find(hex =>
                                  HexagonGrid.IsGridPointOnHexagonEdge(hex.HexagonPosition, hex.HexagonEdge,
                                  HexagonGrid.GetGridPointByHexagonPositionAndPoint(stadt.HexagonPosition, stadt.HexagonPoint))) != null) != null) == null;

                                #endregion
                            }
                            else
                            {
                                allowedSiedlungen[rowIndex][columnIndex][pointIndex] = false;
                            }
                        }
                    }
                }
            }
            return allowedSiedlungen;
        }
        public void StartServer()
        {
            this.iNetworkLayer = new NetworkLayer.CatanServer(3, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1234), "ibo", this);
            this.iNetworkLayer.StartTcpListener();
        }
        /*
        public void ClientGameStateChangeMessageReceived(CatanClientStateChangeMessage catanClientStateChangeMessage)
        {
            CatanClient receivedMessageCatanClient = catanClients.Find(client => client.ID == catanClientStateChangeMessage.ClientID);
            if (receivedMessageCatanClient == null)
                new NullReferenceException("ClientGameStateChangeMessageReceived");

            if (this.currentClient == receivedMessageCatanClient)
            {
                //modifyCatanClientStateMessageIfNecessary(catanClientStateChangeMessage);
                this.messageHandler.Handle(receivedMessageCatanClient, catanClientStateChangeMessage);

                #region Gewinner gefunden? 

                CatanClient winner = null;
                if (receivedMessageCatanClient.Siegpunkte >= MAX_SIEGPUNKTE_WINN)
                {
                    winner = receivedMessageCatanClient;
                }
                else
                {
                    currentClient = getNextClient();
                    clearAllowedSpielFigurenByClients();
                    setAllowedSpielFigurenByClient(currentClient);
                }

                #endregion
                
                iNetworkLayer.SendBroadcastMessage(new GameStateMessage(this.catanClients, currentClient, winner, null));
            }
        }*/

        public void ClientGameStateChangeMessageReceived(CatanClientStateChangeMessage catanClientStateChangeMessage)
        {
            CatanClient receivedMessageCatanClient = catanClients.Find(client => client.ID == catanClientStateChangeMessage.ClientID);
            if (receivedMessageCatanClient == null)
                new NullReferenceException("ClientGameStateChangeMessageReceived");
            
            if (this.currentClient==receivedMessageCatanClient)
            {
                modifyCatanClientStateMessageIfNecessary(catanClientStateChangeMessage);
                this.messageHandler.Handle(receivedMessageCatanClient, catanClientStateChangeMessage);
                CatanClient winner = null;


                // Neue Karten verdient?
                int newStrassen = catanClientStateChangeMessage.NewSpielFiguren != null ? catanClientStateChangeMessage.NewSpielFiguren.Where(sf => sf is Strasse).ToList().Count : 0;
                int newSiedlung = catanClientStateChangeMessage.NewSpielFiguren != null ? catanClientStateChangeMessage.NewSpielFiguren.Where(sf => sf is Siedlung).ToList().Count : 0;


                if (catanClientStateChangeMessage.IsTurnDone)
                {
                    #region Gewinner gefunden? 
                    
                    if (receivedMessageCatanClient.Siegpunkte >= MAX_SIEGPUNKTE_WINN)
                    {
                        winner = receivedMessageCatanClient;
                    }

                    #endregion

                    currentClient = getNextClient();
                    previousTurnDone = true;
                }

                clearAllowedSpielFigurenByClients();
                setAllowedSpielFigurenByClient(currentClient);

               iNetworkLayer.SendBroadcastMessage(new GameStateMessage(this.catanClients, currentClient, winner, null));

                //catanClients.ForEach(cclient => addNewKartenByNewStrassenNewSiedlungen(cclient,0, newSiedlung));

                if (catanClientStateChangeMessage.IsTurnDone && previousTurnDone)
                {
                    // Neue Karten verdient?
                    catanClients.ForEach(cclient => addNewKartenByClient(cclient));
                    previousTurnDone = false;
                }
            }
        }

        private void addNewKartenByNewStrassenNewSiedlungen(CatanClient cclient,int newStrassen, int newSiedlung)
        {
            if (newStrassen >= 2)
            {
                cclient.Siegpunkte += 1;
            }
            if (newSiedlung >= 1)
            {
                cclient.Siegpunkte += 1;
            }
        }

        private void modifyCatanClientStateMessageIfNecessary(CatanClientStateChangeMessage catanClientStateChangeMessage)
        {
            if (catanClientStateChangeMessage.NewSpielFiguren?.Count>0)
            {
                foreach (var strasse in (catanClientStateChangeMessage.NewSpielFiguren.Where(spielFigur => spielFigur is Strasse).ToList()))
                {
                    Strasse foundStrasse = strasse as Strasse;
                    foundStrasse.HexagonEdge = HexagonGrid.Instance.Hexagones[strasse.HexagonPosition.RowIndex][strasse.HexagonPosition.ColumnIndex].Edges[foundStrasse.HexagonEdge.Index];
                }
            }
        }

        private void addNewKartenByClient(CatanClient cclient)
        {
            foreach (var strasse in cclient.SpielfigurenContainer.Strassen)
            {
                var foundHexagons = new List<Hexagon>();
                List<Hexagon> tempHexagons;
                tempHexagons = HexagonGrid.GetHexagonesByGridPoint(HexagonGrid.GetGridPointByHexagonPositionAndPoint(strasse.HexagonPosition, strasse.HexagonEdge.PointA));
                foreach (var tempHex in tempHexagons)
                {
                    if (!foundHexagons.Exists(hex=>hex.Position.Equals(tempHex.Position)))
                    {
                        foundHexagons.Add(tempHex);
                    }
                }
                tempHexagons=HexagonGrid.GetHexagonesByGridPoint(HexagonGrid.GetGridPointByHexagonPositionAndPoint(strasse.HexagonPosition, strasse.HexagonEdge.PointB));

                foreach (var tempHex in tempHexagons)
                {
                    if (!foundHexagons.Exists(hex => hex.Position.Equals(tempHex.Position)))
                    {
                        foundHexagons.Add(tempHex);
                    }
                }

                // checking edges
                foreach (var hex in foundHexagons)
                {
                    foreach (var edge in hex.Edges)
                    {
                        if (HexagonGrid.IsHexagonEdgeOnHexagonEdge(new HexagonPositionHexagonEdge(hex.Position, edge), new HexagonPositionHexagonEdge(strasse.HexagonPosition, strasse.HexagonEdge)))
                            cclient.KartenContainer.AddRohstoffkarte(Game.LandFeld.GetErtragByLandFeldTyp(hex.LandFeldTyp)); // matched hex
                    }
                }
            }
        }

        public void ThrowException(Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }

        public bool IsAuthenticationRequestMessageOk(CatanClientAuthenticationRequestMessage authMessage)
        {
            return this.iPersistenceLayer.ExistPlayer(authMessage.MailAddress, authMessage.Password);
        }
    }
}
