using Eleon.Modding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace ShortWarp
{
    public class ShortWarp : IMod, ModInterface
    {

        internal static ModGameAPI LegacyAPI { get; private set; }
        internal static IModApi GameAPI { get; private set; }

        public static long LastUpdated = 0;

        public static Dictionary<string, IPlayfield> Playfields = new Dictionary<string, IPlayfield>();
        public class contact
        {
            public int id { get; set; }
            public float dist { get; set; }
            public Vector3 pos { get; set; }
            public string name { get; set; }
            public float angle { get; set; }

        }

        [Serializable]
        public class TeleportTarget
        {
            public int Pid { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public string PF { get; set; }
        }

        public static Dictionary<int, int> armed_count = new Dictionary<int, int>();

        public PlayerCommandsDediHelper PlayerCommandsDediHelperO { get; private set; }

        public void Init(IModApi modApi)
        {
            GameAPI = modApi;
            if (GameAPI.Application.Mode == ApplicationMode.DedicatedServer)
            {
                PlayerCommandsDediHelperO = new PlayerCommandsDediHelper(GameAPI);
                return;
            }
            GameAPI.Application.Update += Application_Update;
            GameAPI.Application.OnPlayfieldLoaded += Application_OnPlayfieldLoaded;
            GameAPI.Application.OnPlayfieldUnloading += Application_OnPlayfieldUnLoaded;
            GameAPI.Log("ShortWarp BETA1 - Initialized");
        }

        private readonly float langle = 5;
        private readonly float save_dist = 1000;




        private void Application_Update()
        {
            try
            {
                if (LastUpdated < DateTime.Now.Ticks - 10000000)
                {
                    LastUpdated = DateTime.Now.Ticks;
                    foreach (var PF in Playfields.Where(p => p.Value.PlayfieldType == "Space"))
                    {
#if DEBUG
                        GameAPI.Log($"playfield {PF.Key} {PF.Value.PlayfieldType}");
#endif
                        foreach (var P in (PF.Value.Players.Where(p => p.Value.IsPilot)))
                        {
#if DEBUG
                            GameAPI.Log($"pilot {P.Value.Name} id {P.Value.Id}");
#endif
                            List<ILcd> OutLcds = new List<ILcd>();
                            List<contact> Contacts = new List<contact>();
                            List<contact> Friendly_Contacts = new List<contact>();
                            IEntity PlayerShip = PF.Value.Entities.Where(t => !t.Value.IsProxy && t.Value.Type == EntityType.CV).Where(t => t.Value.Structure.Pilot.Id == P.Key).First().Value;
                            if (PlayerShip == null) continue;
                            string LCDtext = "<align=center>Warp</align>\n";
#if DEBUG
                            GameAPI.Log($"PlayerShip {PlayerShip.Name}");
#endif
                            try
                            {
                                foreach (var pos in PlayerShip.Structure.GetDevicePositions("WARP"))
                                {
                                    var lcd = PlayerShip.Structure.GetDevice<ILcd>(pos);
                                    if (lcd != null)
                                    {
                                        OutLcds.Add(lcd);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                GameAPI.Log($"catch find lcds {ex.Message}");
                            }
                            foreach (var E in PF.Value.Entities)
                            {
                                if (E.Key == PlayerShip.Id) continue;
                                var dist = (E.Value.Position - PlayerShip.Position).magnitude;
                                var angle = Vector3.Angle(PlayerShip.Forward.normalized, (E.Value.Position - PlayerShip.Position).normalized);
                                Contacts.Add(new contact { dist = dist, id = E.Key, pos = E.Value.Position, name = E.Value.Name, angle = angle });
                                if (E.Value.Faction.Id != P.Value.Faction.Id) continue;
                                if (!(E.Value.Type == EntityType.CV || E.Value.Type == EntityType.BA || E.Value.Type == EntityType.Proxy)) continue;
#if DEBUG
                                GameAPI.Log($"DEBUG {E.Value.Type} {E.Key}");
#endif
                                if (angle < langle && dist > 2 * save_dist)
                                {
                                    Friendly_Contacts.Add(new contact { dist = dist, id = E.Key, pos = E.Value.Position, name = E.Value.Name, angle = angle });
                                }
#if DEBUG
                                GameAPI.Log($"entity {E.Value.Name} distance {dist} angle {angle}");
                                GameAPI.Log($"DEBUG forward {PlayerShip.Forward}");
#endif
                            }
                            Friendly_Contacts.Sort(delegate (contact x, contact y)
                            {
                                return x.dist.CompareTo(y.dist);
                            });
#if DEBUG
                            GameAPI.Log($"Friendly_Contacts.count{Friendly_Contacts.Count}");
#endif
                            bool Warp = PlayerShip.Structure.GetSignalState("WARP");
                            if (Friendly_Contacts.Count > 1)
                            {
                                var first = Friendly_Contacts[0];
                                LCDtext += $"Warp Target:\n{first.name} {first.dist}\n";
                                Vector3 Target = (first.pos - ((first.pos - PlayerShip.Position).normalized * save_dist * 1.05f));

#if DEBUG
                                GameAPI.Log($"Target {Target}");
                                GameAPI.Log($"PlayerShip Signal WARP {Warp}");
#endif
                                if (Warp)
                                {
                                    foreach (var C in Contacts)
                                    {
                                        if ((Target - C.pos).magnitude < save_dist)
                                        {
                                            Warp = false;
                                            LCDtext += "<align=center><color=red><b>Blocked</b></color></align>\n";
                                            break;
                                        }
                                    }
                                    if (Warp)
                                    {

                                        if (!(armed_count.TryGetValue(P.Key, out var ac)))
                                        {
                                            armed_count.Add(P.Key, 10);
                                            ac = 10;
                                        }
                                        if (ac > 0)
                                        {
                                            armed_count[P.Key] = --ac;
                                            LCDtext += $"{ac}\n";

                                        }
                                        if (ac == 0)
                                        {
                                            LCDtext += "WARPING!";
                                            Teleport(Target, P.Key, PF.Value.Name);
                                            armed_count[P.Key] = -1;
                                        }
                                        else if (ac == -1)
                                        {
                                            LCDtext += "Please restart Warp";
                                        }
                                    }
                                }
                                else
                                {
                                    armed_count[P.Key] = 10;
                                    LCDtext += "Disabled\nPlease restart Warp";
                                }
                            }
                            else
                            {
                                if (!Warp)
                                {
                                    LCDtext = "";
                                    armed_count[P.Key] = 10;
                                }
                                else
                                {
                                    LCDtext += "No Target\n";
                                    if (armed_count[P.Key] == -1) LCDtext += "Please restart Warp";
                                }
                            }

                            Contacts.Clear();
                            Friendly_Contacts.Clear();
                            foreach (var L in OutLcds)
                            {
                                L.SetText(LCDtext);
                            }


                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GameAPI.Log($"Application_Update {ex.Message}");
            }
        }
        // Convert an object to a byte array
        public static byte[] ObjectToByteArray(System.Object obj)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }
        // Convert a byte array to an Object
        public static System.Object ByteArrayToObject(byte[] arrBytes)
        {
            using (var memStream = new MemoryStream())
            {
                var binForm = new BinaryFormatter();
                memStream.Write(arrBytes, 0, arrBytes.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                var obj = binForm.Deserialize(memStream);
                return obj;
            }
        }
        private void Teleport(Vector3 TargetPos, int Player, string PlayField)
        {
            try
            {
                TeleportTarget TT = new TeleportTarget();
                TT.PF = PlayField;
                TT.X = TargetPos.x; TT.Y = TargetPos.y; TT.Z = TargetPos.z;
                TT.Pid = Player;
                byte[] data = ObjectToByteArray(TT);
                var r = GameAPI.Network.SendToDedicatedServer("ShortWarp", data, PlayField);
            }
            catch (Exception ex)
            {
                GameAPI.Log($"sendtodedi teleporttarget exception {ex.Message}");
            }
        }
        private void Application_OnPlayfieldLoaded(IPlayfield playfield)
        {
#if DEBUG
            GameAPI.Log($"Application_OnPlayfieldLoaded {playfield.Name}");
#endif
            if (!Playfields.TryGetValue(playfield.Name, out var data))
            {
                Playfields.Add(playfield.Name, playfield);
            }
        }
        private void Application_OnPlayfieldUnLoaded(IPlayfield playfield)
        {
#if DEBUG
            GameAPI.Log($"Application_OnPlayfieldUnLoaded {playfield.Name}");
#endif
            Playfields.Remove(playfield.Name);
        }
        public void Shutdown()
        {
            GameAPI.Log("Shut Down");
        }
        public void Game_Start(ModGameAPI dediAPI)
        {
            LegacyAPI = dediAPI;
        }
        public void Game_Update()
        {
        }
        public void Game_Exit()
        {
        }
        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            /*
            switch (eventId)
            {             
                case CmdId.Event_GlobalStructure_List:
                    if (seqNr == WarpGate.WARPGATE_GSI)
                        WarpGateManager.OnGSL(this, (GlobalStructureList)data);
                    break;
                case CmdId.Event_Playfield_Loaded:                    
                    WarpGateManager.OnPlayfieldLoaded((data as PlayfieldLoad).playfield);                    
                    WarpManager.OnPlayfieldLoaded(this, (data as PlayfieldLoad).playfield);
                    break;
                case CmdId.Event_Playfield_Unloaded:
                    WarpGateManager.OnPlayfieldUnloaded((data as PlayfieldLoad).playfield);
                    break;
                case CmdId.Event_Player_Info:
                    if (seqNr == WarpGate.WARPGATE_PLAYERINFO_ID)
                        WarpGateManager.OnPlayerInfo(this, (PlayerInfo)data);
                    break;
            }
                */
        }
        public class PlayerCommandsDediHelper
        {
            public IModApi ModApi { get; }

            public PlayerCommandsDediHelper(IModApi modApi)
            {
                ModApi = modApi;

                try
                {
                    if (!ModApi.Network.RegisterReceiverForPlayfieldPackets(CommandCallback)) ModApi.Log("RegisterReceiverForPlayfieldPackets failed");
                    else modApi.Log($"PlayerCommandsDediHelper: RegisterReceiverForPlayfieldPackets");
                }
                catch (Exception error)
                {
                    ModApi.Log($"PlayerCommandsDediHelper: {error}");
                }
            }
            private void CommandCallback(string sender, string playfieldName, byte[] data)
            {
                if (sender != "ShortWarp") return;
                try
                {
                    TeleportTarget TT = (TeleportTarget)ByteArrayToObject(data);
#if DEBUG
                    ModApi.Log($"XYZ {TT.X} {TT.Y} {TT.Z} Pid {TT.Pid} PF {TT.PF}");
#endif
                    LegacyAPI.Game_Request(CmdId.Request_Entity_Teleport, 77, (new IdPositionRotation() { id = TT.Pid, pos = new PVector3(TT.X, TT.Y, TT.Z), rot = new PVector3() }));
                }
                catch (Exception error)
                {
                    ModApi.Log($"CommandCallback:Teleport {error}");
                }
            }
        }

    }
}
