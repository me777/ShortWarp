using Eleon;
using Eleon.Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;


namespace ReforgedEdenMKII
{
    public class ReforgedEdenMKII : IMod, ModInterface
    {

        public static DediHelper MyDediHelper { get; private set; }
        internal static ModGameAPI LegacyAPI { get; private set; }
        internal static IModApi GameAPI { get; private set; }
        public static IPlayfield PFA2D = null;
        public static IPlayfield PFD2A = null;
        public static long LastUpdated = 0;
        private static Dictionary<int, long> delay = new Dictionary<int, long>();
        public void Init(IModApi modApi)
        {
            GameAPI = modApi;
            if (GameAPI.Application.Mode == ApplicationMode.DedicatedServer)
            {
                MyDediHelper = new DediHelper(GameAPI);
                return;
            }
            GameAPI.Application.Update += Application_Update;
            GameAPI.Application.OnPlayfieldLoaded += Application_OnPlayfieldLoaded;
            GameAPI.Application.OnPlayfieldUnloading += Application_OnPlayfieldUnLoaded;
            GameAPI.Log("ReforgedEdenMKII v1 - Initialized");
        }
        private void Application_Update()
        {

            if (LastUpdated < DateTime.Now.Ticks - 100000000)
            {
                LastUpdated = DateTime.Now.Ticks;
                if (PFA2D != null)
                {
                    Check(PFA2D);
                }
                if (PFD2A != null)
                {
                    Check(PFD2A);
                }

            }
        }
        private void Check(IPlayfield P)
        {
            Vector3 Pos = new Vector3();
#if DEBUG
            GameAPI.Log($"Check {P.Name}");
#endif
            var WG = P.Entities.Values.Where(E => E.IsPoi && E.Name == "Ancient Warp Gate").Single();
#if DEBUG
            GameAPI.Log($"WarpGate found at {WG.Position}");
#endif
            Pos = WG.Position;
            foreach (IEntity E in P.Entities.Values.Where(e => e.Type == EntityType.CV && !e.IsPoi))
            {
                int pilotid = E.Structure.Pilot.Id;
                if (pilotid != 0)
                {
#if DEBUG
                    GameAPI.Log($"Entity {E.Name} E.s");
#endif
                    //GameAPI.Log($"WarpGate {WarpGateA.Position}");
                    float distance = (E.Position - WG.Position).magnitude;
#if DEBUG
                    GameAPI.Log($"dist {distance}");
#endif
                    if (distance < 100)
                    {
#if DEBUG
                        GameAPI.Log($"distance of {E.Name} < 100");
#endif
                        if (E.Structure.Pilot != null)
                        {
                            var pid = E.Structure.Pilot.Id;
#if DEBUG
                            GameAPI.Log($"delay {delay[E.Structure.Pilot.Id]}");
#endif
                            long now = DateTime.Now.Ticks;
                            long value;
                            if (!delay.TryGetValue(pid, out value))

                            {
                                delay.Add(pid, now);
                                return;
                            }
                            if (value < now - 100000000)
                            {
                                delay[pid] = now;

                                if (WG.Structure.IsPowered)
                                {
#if DEBUG
                                    GameAPI.Log($"WarpGate in {P.Name} powered");
#endif
                                    byte[] data = Encoding.ASCII.GetBytes(E.Structure.Pilot.Id.ToString());
                                    var r = GameAPI.Network.SendToDedicatedServer("ReforgedEdenMKII", data, P.Name);
#if DEBUG
                                    GameAPI.Log($"GameAPI.Network.SendToDedicatedServer result {r} {data.Length}");
#endif
                                }
                                else
                                {
#if DEBUG
                                    GameAPI.Log($"WarpGate in {P.Name} not powered");
#endif
                                }
                            }
                        }
                    }

                }

            }
        }
        private void Application_OnPlayfieldLoaded(IPlayfield playfield)
        {
#if DEBUG
            GameAPI.Log($"Application_OnPlayfieldLoaded.begin {playfield.Name}");
#endif
            if (playfield.Name == "Andromeda to Decay Warp Gate")
            {
                PFA2D = playfield;
            }
            else if (playfield.Name == "Decay to Andromeda Warp Gate")
            {
                PFD2A = playfield;
            }

        }
        private void Application_OnPlayfieldUnLoaded(IPlayfield playfield)
        {
#if DEBUG
            GameAPI.Log($"Application_OnPlayfieldLoaded.begin {playfield.Name}");
#endif
            if (playfield.Name == "Andromeda to Decay Warp Gate")
            {
                PFA2D = null;
            }
            else if (playfield.Name == "Decay to Andromeda Warp Gate")
            {
                PFD2A = null;
            }

        }



        //------------------------------------------------------------------
        public class DediHelper
        {
            public IModApi ModApi { get; }
            //public static Action<string, LogLevel> Log { get; set; }
            internal static readonly ushort WARPTASK_PLAYER_CHANGEPF = 61001;
            internal static readonly ushort WARPTASK_ENTITY_CHANGEPF = 61002;
            internal static readonly ushort WARPMANAGER_LOAD_PF = 61005;
            internal static List<int> Wplayers = new List<int>();
            internal static int state = 0;
            internal static List<WarpTask> WarpTasks = new List<WarpTask>();
            public DediHelper(IModApi modApi)
            {
                ModApi = modApi;

                try
                {
                    if (!ModApi.Network.RegisterReceiverForPlayfieldPackets(CCallback)) ModApi.Log("RegisterReceiverForPlayfieldPackets failed");
                    else ModApi.Log($"DediHelper: RegisterReceiverForPlayfieldPackets ok");
                }
                catch (Exception error)
                {
                    ModApi.Log($"DediHelper: {error}");
                }
                ModApi.Application.ChatMessageSent += Application_ChatMessageSent;
                ModApi.Application.Update += Application_UpdateDedi;
                ModApi.Application.OnPlayfieldLoaded += Application_OnPlayfieldLoadedDedi;
            }
            private void Application_OnPlayfieldLoadedDedi(IPlayfield playfield)
            {
#if DEBUG
                GameAPI.Log($"Application_OnPlayfieldLoaded.begin {playfield.Name}");
#endif
                if (playfield.Name == "Andromeda to Decay Warp Gate")
                {
                    PFA2D = playfield;
                }
                else if (playfield.Name == "Decay to Andromeda Warp Gate")
                {
                    PFD2A = playfield;
                }
            }
            private void Application_OnPlayfieldUnLoadedDedi(IPlayfield playfield)
            {
#if DEBUG
                GameAPI.Log($"Application_OnPlayfieldLoaded.begin {playfield.Name}");
#endif
                if (playfield.Name == "Andromeda to Decay Warp Gate")
                {
                    PFA2D = null;
                }
                else if (playfield.Name == "Decay to Andromeda Warp Gate")
                {
                    PFD2A = null;
                }
            }
            private void Application_UpdateDedi()
            {

                if (LastUpdated < DateTime.Now.Ticks - 10000000)
                {
                    LastUpdated = DateTime.Now.Ticks;
                    WarpTasks.RemoveAll(w => w.Timestamp < DateTime.Now.Ticks - 100000000);
                }
            }

            private void CCallback(string sender, string playfieldName, byte[] data)
            {

                try
                {
#if DEBUG
                    ModApi.Log($"CommandCallback {sender} {playfieldName}");
#endif

                    if (sender != "ReforgedEdenMKII") return;

                    int.TryParse(Encoding.ASCII.GetString(data), out var result);

                    if (WarpTasks.Where(w => w.EntityId == result).Count() == 0)
                    {
                        SendActivationQuery(result);
                        System.Random r = new System.Random();
                        float spread = 500;
                        WarpTasks.Add(new WarpTask
                        {
                            EntityId = result,
                            IsPlayer = true,
                            Playfield = playfieldName == "Decay to Andromeda Warp Gate" ? "Andromeda to Decay Warp Gate" : "Decay to Andromeda Warp Gate",
                            Position = new PVector3
                            {
                                x = (float)((r.NextDouble() - .5) * 2) * spread,
                                y = (float)((r.NextDouble() - .5) * 2) * spread,
                                z = (float)((r.NextDouble() - .5) * 2) * spread
                            },
                            Rotation = new PVector3
                            {
                                x = (float)((r.NextDouble() - .5) * 2),
                                y = (float)((r.NextDouble() - .5) * 2),
                                z = (float)((r.NextDouble() - .5) * 2)
                            },
                            Timestamp = DateTime.Now.Ticks
                        });
#if DEBUG
                        ModApi.Log($"ReforgedEdenMKII:Teleport call from {playfieldName} for playerid {result}");
#endif
                    }
                }
                catch (Exception error)
                {
                    ModApi.Log($"ReforgedEdenMKII:Teleport {error}");
                }
            }

            private void Application_ChatMessageSent(MessageData chatMsgData)
            {
                if (chatMsgData.Text.ToLower().Contains("!mods"))
                {
                    ModApi.Application.SendChatMessage(new MessageData()
                    {
                        RecipientEntityId = chatMsgData.SenderEntityId,
                        Text = "Reforged Eden WarpGate - v1",
                        Channel = Eleon.MsgChannel.SinglePlayer,
                        SenderType = Eleon.SenderType.System
                    });
                }
            }
            private void SendActivationQuery(int entityId)
            {
                //ActivatingEntityId = entityId;
                GameAPI.Application.ShowDialogBox(entityId, new DialogConfig()
                {
                    BodyText = "The ancient device pulses with gravitational energy. As the gate draws near your vision swims and you feel a strange presence brush against your consciousness.",
                    ButtonTexts = new string[]
                    {
                    "Give in to the sensation.",
                    "Resist the alien influence."
                    },
                    ButtonIdxForEnter = 0,
                    ButtonIdxForEsc = 1,
                    TitleText = "Ancient Warp Gate"
                },
                (ix, lid, ic, pid, cv) =>
                {
                    if (ix == 0)
                    {
                        var WT = WarpTasks.Where(w => w.EntityId == entityId).FirstOrDefault();
                        var tpf = WT.Playfield == "Decay to Andromeda Warp Gate" ? PFD2A : PFA2D;
                        if (tpf == null)
                        {
                            LegacyAPI.Game_Request(CmdId.Request_Load_Playfield, WARPMANAGER_LOAD_PF, new PlayfieldLoad() { playfield = WT.Playfield });
                        }
                        WT.Warp(LegacyAPI);
                    }
                    else
                    {
                        WarpTasks.RemoveAll(w => w.EntityId == entityId);
                    }
                }, 0);

            }

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
    }
}
