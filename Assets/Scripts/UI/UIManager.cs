using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ElevatorGame
{
    public sealed class UIManager : MonoBehaviour
    {
        enum Menu { Home, Single, Multi }
        Menu menu;
        GUIStyle hero,h1,h2,body,small,button,chip;
        Texture2D panel,accent,muted,white;
        readonly Color ink=new(.055f,.1f,.14f),mint=new(.67f,.96f,.79f),cream=new(.98f,.94f,.84f),gray=new(.63f,.72f,.73f);
        bool debug,lanMode;
        float tutorialAt;
        void Setup()
        {
            panel=Tex(new Color(.035f,.075f,.1f,.96f));accent=Tex(mint);muted=Tex(new Color(.13f,.22f,.24f,.96f));white=Tex(cream);
            var font=Font.CreateDynamicFontFromOSFont(new[]{"Segoe UI","Arial"},18);
            hero=Style(76,FontStyle.Bold,font);h1=Style(43,FontStyle.Bold,font);h2=Style(24,FontStyle.Bold,font);
            body=Style(18,FontStyle.Normal,font);small=Style(13,FontStyle.Normal,font);
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=16,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,border=new RectOffset(0,0,0,0)};
            button.normal.background=accent;button.normal.textColor=ink;button.hover.background=white;button.hover.textColor=ink;button.active.background=muted;button.active.textColor=cream;
            chip=new GUIStyle(button){fontSize=14};chip.normal.background=muted;chip.normal.textColor=cream;
        }
        GUIStyle Style(int size,FontStyle weight,Font font)=>new GUIStyle(GUI.skin.label){font=font,fontSize=size,fontStyle=weight,wordWrap=true};
        Texture2D Tex(Color c){var t=new Texture2D(1,1);t.SetPixel(0,0,c);t.Apply();return t;}
        void Update()
        {
            if(Keyboard.current!=null&&Keyboard.current.f1Key.wasPressedThisFrame&&(Application.isEditor||Debug.isDebugBuild))
            {debug=!debug;Cursor.lockState=debug?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=debug;}
        }
        void OnGUI()
        {
            if(hero==null)Setup();
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            var session=GameSession.Instance;var net=NetworkGameManager.Instance;var round=RoundManager.Instance;var manager=NetworkManager.Singleton;
            if(!session||!net||!round)return;
            var steam=SteamSession.Instance;
            if(steam&&(steam.Migrating||steam.Leaving||(steam.InRoom&&!(manager&&manager.IsConnectedClient))))
            {
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                Panel(320,220,640,260);Label(355,250,570,65,steam.Migrating?"CHANGING HOST...":steam.Leaving?"LEAVING ROOM...":"CONNECTING...",h1,cream);
                Label(360,325,560,66,steam.Status,body,gray);
                if(!steam.Leaving&&Button(360,415,560,38,"LEAVE ROOM",false))steam.LeaveRoom();return;
            }
            bool connected=GameSession.Offline||(manager&&manager.IsConnectedClient);
            if(!connected){DrawMenu(session,net);return;}
            var players=RoundManager.Players();var local=players.FirstOrDefault(p=>p.ControlledLocally);
            if(round.Phase.Value==RoundPhase.Lobby)
            {
                Panel(24,24,320,84+players.Length*32);
                Label(44,39,280,30,"WAITING ROOM  /  "+players.Length+" / 8",h2,mint);
                int y=84;
                foreach(var p in players)
                {
                    RectColor(44,y+7,8,8,PlayerController.Colors[p.Slot.Value%8]);
                    Label(64,y,176,27,p.DisplayName+(p==local?" / YOU":""),small,cream);
                    Label(244,y,88,27,p.IsLobbyHost?"HOST":p.Ready.Value?"READY":"WAITING",small,p.IsLobbyHost||p.Ready.Value?mint:gray);y+=32;
                }
                string prompt=!local?"JOINING ELEVATOR...":local.IsLobbyHost?
                    players.Length<2?"WAITING FOR PASSENGERS":round.CanStart?"ALL READY  /  PRESS R TO START":"WAITING FOR OTHER PLAYERS TO READY UP":
                    local.Ready.Value?"YOU ARE READY  /  R TO CANCEL":"PRESS R WHEN YOU ARE READY";
                Panel(365,606,880,89);Label(389,617,832,30,prompt,h2,mint);
                Label(389,659,832,24,"WASD  MOVE    SPACE  JUMP    LMB  GRAB    ESC  ROOM MENU",small,cream);
                if(Cursor.lockState!=CursorLockMode.Locked)
                {
                    Panel(460,190,360,325);Label(486,210,310,36,"ROOM MENU",h2,cream);
                    if(Button(486,266,308,40,"RESUME")){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
                    if(net.UsingSteam&&steam&&Button(486,321,308,40,"INVITE FRIENDS",false))steam.Invite();
                    GUI.enabled=round.IsAuthority&&players.Length<8;
                    if(Button(486,376,308,40,"ADD AI",false))net.AddDummy();GUI.enabled=true;
                    if(Button(486,450,308,38,"LEAVE ROOM",false))net.Leave();
                }
                return;
            }
            if(round.Phase.Value!=RoundPhase.Results)DrawHUD(round,local);
            if(round.Phase.Value==RoundPhase.Results){DrawResults(round,session,local);return;}
            if(session.ShowTutorial&&GameSession.Offline)
            {
                if(tutorialAt==0)tutorialAt=Time.unscaledTime;
                Panel(338,178,604,365);Label(378,204,526,62,"STAY INSIDE.",h1,cream);
                Label(380,283,510,117,"WASD  Move    SPACE  Jump\nMOUSE  Look    HOLD LMB  Grab with both hands\nF  Pick up / throw    RMB  Use weapon",body,gray);
                Label(380,402,500,30,"LAST ONE INSIDE WINS.",h2,mint);
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                if(Button(380,461,520,47,"LET'S RIDE"))
                {session.DismissTutorial();Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            }
            if(Cursor.lockState!=CursorLockMode.Locked&&!session.ShowTutorial&&!debug)
            {
                Panel(490,265,300,170);Label(515,284,255,37,"TAKE A BREATH",h2,cream);
                if(Button(515,334,250,36,"RESUME")){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
                if(Button(515,384,250,32,"MAIN MENU",false))net.Leave();
            }
            if(debug&&!session.ShowTutorial&&round.IsAuthority)DrawDebug(round,session,players,local);
        }
        void DrawMenu(GameSession session,NetworkGameManager net)
        {
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            Panel(35,32,496,655);
            Label(74,61,420,28,"ODD HOURS HOTEL    /    EST. 1987",small,mint);
            RectColor(75,111,58,5,mint);
            if(menu==Menu.Home)
            {
                Label(68,145,455,115,"ELEVATOR",hero,cream);
                Label(76,265,400,80,"Going up.\nProbably not all of you.",h2,cream);
                Label(76,357,405,85,"A small lift. A very bad day.\nA physics party for you and your questionable friends.",body,gray);
                if(Button(76,463,413,53,"SINGLE PLAYER  >"))menu=Menu.Single;
                if(Button(76,531,413,53,"MULTIPLAYER  >",false))menu=Menu.Multi;
                Label(76,628,415,26,"ONE RULE: THE LAST ONE INSIDE WINS.",small,mint);
            }
            else if(menu==Menu.Single)
            {
                Label(72,148,420,64,"SOLO, TOGETHER.",h1,cream);
                Label(76,226,395,60,"Bring a lift full of delightfully unreliable AI passengers.",body,gray);
                Label(76,317,400,28,"AI PASSENGERS",small,mint);
                int[] counts={3,5,7};
                for(int i=0;i<3;i++)if(Button(76+i*141,359,130,48,counts[i]+" AI",session.AICount==counts[i]))session.AICount=counts[i];
                Label(76,436,400,25,"HOW QUICK ARE YOUR COMPANIONS?",small,mint);
                for(int i=0;i<3;i++)if(Button(76+i*141,478,130,45,((AIDifficulty)i).ToString().ToUpperInvariant(),session.Difficulty==(AIDifficulty)i))session.Difficulty=(AIDifficulty)i;
                if(Button(76,553,413,52,"START  /  "+(session.AICount+1)+" PASSENGERS"))session.StartSinglePlayer();
                if(Button(76,625,413,32,"<  BACK",false))menu=Menu.Home;
            }
            else
            {
                if(!lanMode){DrawSteamMenu();return;}
                Label(72,148,420,64,"BRING FRIENDS.",h1,cream);
                Label(76,231,410,56,"Direct IP / LAN testing",body,gray);
                if(Button(76,326,413,50,"CREATE ROOM"))net.CreateRoom();
                Label(76,411,410,26,"HOST IP ADDRESS",small,mint);
                GUI.skin.textField.fontSize=18;net.Address=GUI.TextField(new Rect(76,451,413,39),net.Address,64);
                if(Button(76,509,413,50,net.Connecting?"CONNECTING...":"JOIN ROOM",false)&&!net.Connecting)net.JoinRoom();
                Label(76,578,410,40,net.Status,small,gray);
                if(Button(76,637,413,30,"<  STEAM MULTIPLAYER",false))lanMode=false;
            }
            Label(929,645,310,30,"SERVICE MAY BE UNPREDICTABLE.",small,cream);
        }
        void DrawSteamMenu()
        {
            var steam=SteamSession.Instance;
            Label(72,148,420,64,"BRING FRIENDS.",h1,cream);
            Label(76,225,410,66,steam&&steam.Ready?"Signed in as "+steam.NameFor(steam.LocalId):"Sign in to the Steam client to play with friends.",body,gray);
            GUI.enabled=steam&&steam.Ready;
            if(Button(76,315,413,48,"CREATE FRIENDS ROOM"))steam.Create(false);
            if(Button(76,376,413,48,"CREATE PUBLIC ROOM",false))steam.Create(true);
            if(Button(76,437,413,48,"FIND PUBLIC ROOMS",false))steam.Search();
            GUI.enabled=true;
            if(steam&&!steam.Ready&&Button(76,498,413,42,"CONNECT STEAM"))steam.EnsureReady();
            if(Button(76,548,413,32,"DIRECT IP / LAN",false))lanMode=true;
            Label(76,589,410,43,steam?steam.Status:"Steam session unavailable",small,gray);
            if(Button(76,641,413,30,"<  BACK",false))menu=Menu.Home;
            Panel(565,148,675,500);Label(590,172,620,43,"PUBLIC ELEVATORS",h2,mint);
            if(!steam||steam.Rooms.Count==0)Label(595,239,590,100,"Find a public room, or ask a friend to invite you through Steam.",body,gray);
            else for(int i=0;i<Math.Min(6,steam.Rooms.Count);i++){var room=steam.Rooms[i];Label(592,236+i*62,430,43,room.name+"  "+room.members+"/8",body,cream);if(Button(1040,235+i*62,165,40,"JOIN"))steam.Join(room.id);}
        }
        void DrawHUD(RoundManager round,PlayerController local)
        {
            Panel(30,25,179,104);Label(50,36,145,22,"FLOOR",small,mint);Label(48,62,148,59,round.Floor.Value.ToString("00"),h1,cream);
            Panel(1070,25,180,104);Label(1090,36,145,22,"STILL INSIDE",small,mint);Label(1088,64,150,54,round.AliveCount.Value+" / "+round.TotalCount.Value,h1,cream);
            string headline=round.Stage.Value==ElevatorStage.Event?round.EventTitle.Value.ToString():round.Stage.Value==ElevatorStage.Moving?"NEXT FLOOR...":round.Stage.Value==ElevatorStage.Waiting?"...":round.Stage.Value==ElevatorStage.Opening?"MIND THE DOORS":round.Stage.Value==ElevatorStage.Ding?"YOU HAVE ARRIVED":round.Stage.Value.ToString().ToUpperInvariant();
            Label(360,40,600,54,headline,h2,cream);
            if(local&&local.Alive.Value)
            {
                bool held=local.HeldHands.Value>0;
                Panel(430,643,420,48);Label(454,653,385,27,held?"HOLDING ON   /   RELEASE LMB TO LET GO":"HOLD LMB TO REACH & GRAB WITH BOTH HANDS",small,held?mint:gray);
                if(local.transform.position.z>2.3f){Label(460,555,400,44,"HOLD ON!",h2,new Color(1,.55f,.35f));}
                if(!debug){var weapon=WeaponSystem.Held(local);Panel(30,596,350,95);Label(48,607,315,34,weapon?weapon.DisplayName.ToUpperInvariant()+(weapon.Capacity>0?"  "+weapon.RemainingAmmo+"/"+weapon.Capacity:""):"F  PICK UP NEARBY WEAPON",small,mint);Label(48,646,315,33,weapon?(weapon.HasAmmo?"HOLD RMB  USE    F  THROW":"EMPTY    F  THROW"):"WASD  MOVE   SPACE  JUMP",small,gray);}
                RectColor(638,357,4,4,held?mint:cream);
            }
            else
            {
                Panel(342,618,596,75);Label(367,628,547,26,"YOU ARE OUT  /  THE RIDE GOES ON",h2,mint);
                Label(367,664,547,22,"Q / E  Watch another passenger     F  Free camera",small,gray);
            }
        }
        void DrawResults(RoundManager r,GameSession session,PlayerController local)
        {
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            Panel(342,127,596,480);
            bool win=local&&r.Winner.Value==local.Slot.Value;
            string headline=GameSession.Offline?(win?"YOU WIN!":"YOU LOST"):r.Winner.Value<0?"NO SURVIVORS":"LAST ONE STANDING";
            Label(382,163,520,74,headline,h1,win?mint:cream);
            Label(385,260,470,26,"WINNER",small,gray);
            Label(380,294,510,53,session.NameFor(r.Winner.Value),h2,mint);
            RectColor(384,363,508,1,new Color(.24f,.34f,.34f));
            Label(385,389,250,23,"FLOOR REACHED",small,gray);Label(685,389,230,23,"SURVIVAL TIME",small,gray);
            Label(382,426,240,47,r.Floor.Value.ToString("00"),h2,cream);
            double seconds=session.SurvivalSeconds;Label(682,426,200,47,TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(@"mm\:ss"),h2,cream);
            if(r.IsAuthority)
            {
                GUI.enabled=r.Remaining<=0;
                if(Button(382,522,243,46,"PLAY AGAIN"))r.StartRound();
                if(Button(645,522,253,46,GameSession.Offline?"MAIN MENU":"LOBBY",false))
                {if(GameSession.Offline)NetworkGameManager.Instance.Leave();else r.ReturnToLobby();}
                GUI.enabled=true;
            }
            else Label(388,530,450,35,"Waiting for your host...",body,gray);
        }
        void DrawDebug(RoundManager r,GameSession session,PlayerController[] players,PlayerController local)
        {
            Panel(20,145,310,475);Label(38,157,280,30,"DIRECTOR / F1",h2,mint);
            if(Button(38,204,136,32,"NEXT FLOOR"))r.NextFloor();
            if(Button(182,204,130,32,"RESET"))r.StartRound();
            int i=0;
            foreach(EventKind kind in Enum.GetValues(typeof(EventKind)))
            {if(GUI.Button(new Rect(38+(i%2)*139,250+(i/2)*30,134,26),kind.ToString())){r.events.Force(kind);r.NextFloor();}i++;}
            if(GUI.Button(new Rect(38,467,130,29),"+ AI"))session.AddAI();
            if(GUI.Button(new Rect(179,467,130,29),"- AI"))session.RemoveAI();
            if(GUI.Button(new Rect(38,503,272,27),"DIFFICULTY: "+session.Difficulty))session.Difficulty=(AIDifficulty)(((int)session.Difficulty+1)%3);
            if(GUI.Button(new Rect(38,538,130,29),"AI GRAB"))foreach(var p in players)if(p.IsBot.Value&&p.AI)p.AI.DebugGrab=!p.AI.DebugGrab;
            if(GUI.Button(new Rect(179,538,130,29),"AI FLOP"))foreach(var p in players)if(p.IsBot.Value)p.Knock(Vector3.up*3);
            if(GUI.Button(new Rect(38,577,130,28),"ELIMINATE")&&local)r.elimination.Eliminate(local);
            if(GUI.Button(new Rect(179,577,130,28),"COMBINE")){r.events.Force(EventKind.Wind,true);r.NextFloor();}
            int y=156;foreach(var p in players)if(p.IsBot.Value&&p.AI){Panel(945,y,308,34);Label(957,y+4,290,28,p.DisplayName+" / "+p.AI.State,small,mint);y+=39;}
        }
        void BottomControls()=>Label(578,603,650,75,"WASD  MOVE    SPACE  JUMP    MOUSE  LOOK\nHOLD LEFT MOUSE  Grab with both hands\nESC  Cursor / menu",body,cream);
        void Panel(float x,float y,float w,float h)=>GUI.DrawTexture(new Rect(x,y,w,h),panel);
        void RectColor(float x,float y,float w,float h,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=old;}
        bool Button(float x,float y,float w,float h,string text,bool primary=true)
        {bool clicked=GUI.Button(new Rect(x,y,w,h),text,primary?button:chip);if(clicked)AudioManager.Instance?.Play("click",.4f);return clicked;}
        void Label(float x,float y,float w,float h,string text,GUIStyle style,Color color){var old=GUI.color;GUI.color=color;var fitted=new GUIStyle(style);
            while(fitted.fontSize>10&&(fitted.CalcHeight(new GUIContent(text),w)>h||((style==hero||style==h1||style==h2)&&!text.Contains("\n")&&fitted.CalcSize(new GUIContent(text)).x>w)))fitted.fontSize--;
            GUI.Label(new Rect(x,y,w,h),text,fitted);GUI.color=old;}
    }
}






